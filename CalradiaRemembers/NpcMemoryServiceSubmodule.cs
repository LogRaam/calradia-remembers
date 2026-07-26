// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Chronicle;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Portraits;
using CalradiaRemembers.Rescue;
using CalradiaRemembers.Scenarios;
using CalradiaRemembers.Stance;
using CalradiaRemembers.WorldEvents;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   Entry point for the NPC Memory Service mod. Bannerlord instantiates this
   ///   class on game launch and invokes lifecycle hooks. We use them to register
   ///   our <see cref="NpcMemoryCampaignBehavior" /> with the campaign system and
   ///   to expose the mod's Gauntlet UI prefabs to the engine.
   /// </summary>
   public class NpcMemoryServiceSubmodule : MBSubModuleBase
   {
      private const string ModName = "NPC Memory Service";
      private const string ModuleId = "CalradiaRemembers";

      /// <summary>
      ///   Per-frame tick, fired by the engine even when the campaign is paused and no chat is open. Used only
      ///   to pump the opt-in <see cref="VramWatch" /> heartbeat (off by default) so the background map screen's
      ///   VRAM can be traced during the no-chat control test.
      /// </summary>
      protected override void OnApplicationTick(float dt)
      {
         base.OnApplicationTick(dt);
         VramWatch.Tick();
         CaptiveSceneStager.Tick(); // opens a staged captive chat once its conversation scene has loaded
         Duels.DuelSceneSpike.Tick(dt); // drives the wilderness-duel spike's open/close/VRAM state machine
      }

      /// <summary>
      ///   Called after all submodules have loaded — safe to assume other mods
      ///   are present at this point.
      /// </summary>
      protected override void OnBeforeInitialModuleScreenSetAsRoot()
      {
         base.OnBeforeInitialModuleScreenSetAsRoot();
         Log($"{ModName} ready (main menu reached).");
      }

      /// <summary>
      ///   Called every time the user starts or loads a campaign. This is where
      ///   we hook our <see cref="CampaignBehaviorBase" /> into the active campaign.
      /// </summary>
      protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
      {
         base.OnGameStart(game, gameStarterObject);

         // Only register for campaign games (not custom battles, sieges, etc.)
         if (game.GameType is not Campaign) return;

         var campaignStarter = (CampaignGameStarter) gameStarterObject;
         // The ambush fires INSIDE the town (only there does the alley fight spawn combatants in the street). The
         // access model is registered NOT to block the ambush entry, but to bar the player re-entering a town for
         // the brief moment a lost-ambush capture is pending on the open map — closing the "re-click the town to
         // slip the capture" exploit. (The GateAmbushMenuBehavior gate-fight experiment stays unregistered.)
         // It DECORATES the model already in effect (forwarding every decision to it) rather than replacing it, so
         // another mod's location rules survive — notably War Sails' ports, which the vanilla default locks out.
         SettlementAccessModel priorAccessModel = gameStarterObject.Models.OfType<SettlementAccessModel>().LastOrDefault();
         campaignStarter.AddModel(new AmbushSettlementAccessModel(priorAccessModel));
         campaignStarter.AddBehavior(new NpcMemoryCampaignBehavior());
         // Player-authored character data: applies hand-crafted personalities (traits via SetTraitLevel,
         // the single source of truth) from ModuleData/character_overrides.json when the MCM toggle is on.
         campaignStarter.AddBehavior(new CharacterLoreBehavior());
         // Dynastic succession: on player death, reframes every NPC's memory through the heir — drops the
         // predecessor's private history, keeps and marks the political/relational history as inherited.
         campaignStarter.AddBehavior(new SuccessionBehavior());
         // Marriage system (family-approval engine) — SHIPPED from v1.7.0. The approval chat path
         // (grant_blessing, Phase A.2a) now exists, and the consequence only ever lands on the
         // PLAYER's own marriage (OnBeforeHeroesMarried bails when the player is not a spouse), so
         // vanilla NPC-NPC weddings are untouched. Marrying into a house without first earning its
         // blessing chills that family — the intended love-vs-ambition mechanic.
         campaignStarter.AddBehavior(new MarriageApprovalBehavior());
         // Jealousy (Sprint 24): a romantic act can provoke a third party with a real stake.
         // Knowledge-gated, probabilistic, delayed; reactions are a grudge + optional threat letter.
         campaignStarter.AddBehavior(new JealousyBehavior());
         // Rescue awareness: freeing a captive lord by force makes the lord, their family and a few close
         // friends grateful (memory + regard), and the captor may trace it back and hold a grudge. Registered
         // after GrudgeBehavior below? No: it reads GrudgeBehavior.Instance at runtime, so order is harmless.
         campaignStarter.AddBehavior(new RescueBehavior());
         // Pregnancy birth: a CR-created pregnancy's child is placed in the MOTHER's clan at birth (the
         // bastard rule). Tracks CR's own conceptions only, so vanilla NPC patrilineality is untouched.
         campaignStarter.AddBehavior(new PregnancyBirthBehavior());
         // Bastards (consequence layer): tags the player's non-marital children and, once a season, may bring
         // a hidden one to light — a scandal recorded into the world-event store (read at runtime, so the
         // registration order is harmless) + a hit to the player's standing. Console: cr.bastards / cr.bastard_*.
         campaignStarter.AddBehavior(new BastardBehavior());
         // World-event / rumor store (v1.2): persisted log of dated happenings feeding street
         // rumors, NPC awareness, and the player's chronicle. Feeders register separately.
         campaignStarter.AddBehavior(new WorldEventStore());
         // Schemes (v1.3): bitter rivals plot against each other; the lifecycle is mechanical (pure
         // SchemeAdvancer), the effect is standing damage, and the player only ever hears of it as a
         // rumour fed to the world-event store. Registered AFTER WorldEventStore (it records into it).
         campaignStarter.AddBehavior(new SchemeStore());
         // RP with teeth, increment 2 (Phase 1 spine): the TrackedCommitment engine — the symmetric of
         // InformalQuest; an NPC's promise TO THE PLAYER (flagship: pledge_against), verified by real
         // campaign events (a resolved scheme, or an organic battle) or breached on a lapsed deadline.
         // Registered after SchemeStore, which notifies it on a scheme's success — load order does not
         // strictly matter (Instance is set in each behavior's own constructor).
         campaignStarter.AddBehavior(new CommitmentBehavior());
         // Divorce, Phase 1 (the first verb built on the IConversationVerb contract): end_marriage begins a
         // multi-day estrangement process (the spouse may resist, delay, or defeat it outright); reaching the
         // end day attempts the real dissolution and, on success, records a culture-scaled scandal. Console:
         // cr.divorce_info / cr.divorce / cr.divorce_tick.
         campaignStarter.AddBehavior(new DivorceProcessBehavior());
         // Divorce, Phase 2b: the player's OWN spouse demands the divorce, entirely through conversation.
         // Owns the persisted demand flag + the weekly evaluation/grace-period-refusal sweep; the
         // accept_divorce/decline_divorce actions and the dissolution itself live in
         // BannerlordGameStateBridge/MarriageDissolutionService. Load order does not strictly matter
         // (Instance is set in the constructor); registered beside DivorceProcessBehavior for locality.
         // Console: cr.spouse_divorce_demand / cr.spouse_divorce_info / cr.spouse_divorce_tick.
         campaignStarter.AddBehavior(new SpouseDivorceDemandBehavior());
         // Grudges (consolidation pillar): a resolvable, event-anchored grievance, coexisting with regard
         // instead of being diluted into it. Registered AFTER WorldEventStore (its one-shot backfill scans
         // the bastard-claim world events) and after Bastards/DivorceProcessBehavior (it scans their
         // persisted markers too). Console: cr.grudges / cr.grudge_test / cr.grudge_backfill.
         campaignStarter.AddBehavior(new GrudgeBehavior());
         // Grudges Tier 3 LIVE plant site: the player executes a lord, that lord's close living kin
         // (spouse/parents/children/siblings, capped to a handful) each form a permanent ExecutedKin
         // vendetta. Registered right after GrudgeBehavior (this listener only ever calls its TryPlant);
         // no SyncData of its own, no backfill by design (Tier 3 has none). Console: cr.grudge_execute_kin.
         campaignStarter.AddBehavior(new ExecutedKinGrudgeBehavior());
         // Grudges Tier 2 LIVE plant site: sway_opinion's own Rivalry rumour (recorded by SwayOpinionVerb
         // into WorldEventStore) is watched here for the swayed-against target's own awareness, planting a
         // hidden SwayExposed grudge once it plausibly reaches them. Registered after WorldEventStore (it
         // reads from it) and GrudgeBehavior (this listener only ever calls its TryPlant); no backfill (a
         // LIVE-only source, mirroring ExecutedKinGrudgeBehavior). Console: cr.grudge_sway_test.
         campaignStarter.AddBehavior(new SwayExposureBehavior());

         campaignStarter.AddBehavior(new NemesisBehavior());
         // Nemesis pillar increment 2 (resolution layer): closes out a tracked nemesis on
         // CampaignEvents.HeroKilledEvent (R1 kill in battle / R2 execute), recording a closure deed
         // when the player dealt the death. Registered right after NemesisBehavior (this listener only
         // ever reads/mutates its roster); no SyncData of its own. Console: cr.nemesis_kill / cr.nemesis_execute.
         campaignStarter.AddBehavior(new NemesisResolutionBehavior());
         // Captive-escape gear consequence (ratified 2026-07-2x): the mod-side ledger of gear a nemesis or
         // lord holder has accumulated from the player's Fled escapes, and the recovery routes (capture the
         // holder alive, or defeat him in a battle the player wins). Now only the LORD branch uses this holder
         // model; a bandit escape scatters loot instead (ScatteredLootBehavior below). Console: cr.gear_stock /
         // cr.gear_recover / cr.gear_test.
         campaignStarter.AddBehavior(new Duels.CaptiveGearBehavior());
         // Scattered-loot recovery (AUDIT-NEMESIS M1, 2026-07-24): a FLED bandit escape scatters the player's
         // gear among that culture's bands; defeating a band of the culture has a chance to return a piece.
         // Replaces the old "mint a nemesis to hold the whole stock" bandit path. Save-safe, additive key.
         campaignStarter.AddBehavior(new Duels.ScatteredLootBehavior());
         // Appreciations (the positive mirror of Grudges): a lingering felt kindness, coexisting with regard
         // instead of being folded into it. Registered AFTER GrudgeBehavior (its prompt-note tarnish reads
         // the holder's dominant live grudge) and after WorldEventStore/Bastards/DivorceProcessBehavior (its
         // one-shot backfill scans their persisted markers too). Console: cr.appreciations / cr.appreciation_test
         // / cr.appreciation_backfill.
         campaignStarter.AddBehavior(new AppreciationBehavior());
         // Scripted scenarios (the world-reaction engine): a trigger (death/marriage/war/peace) fires a
         // rule whose consequences mutate the world and whose message is dropped into the world-event store.
         // Pure selection lives in CalradiaRemembers.Logic; packs load from ModuleData/scenarios. Registered
         // AFTER WorldEventStore (it records its message into it). Console: cr.scenarios / cr.scenario_fire.
         campaignStarter.AddBehavior(new ScenarioBehavior());
         // Interceptions: a nearby lord may ride out to the player (hostile territorial challenge, or
         // a reaction to fresh news from the rumour store). Decision is pure; this only moves parties.
         campaignStarter.AddBehavior(new InterceptionBehavior());
         // Stance (multi-axis posture): time-based fear decay + the prisoner-taken deed. Other deeds
         // are applied at their own event source; the decision/tuning is pure (CalradiaRemembers.Logic).
         campaignStarter.AddBehavior(new StanceBehavior());
         // Stance with teeth: weekly, a hardened posture becomes an unbidden hostile/favourable letter.
         campaignStarter.AddBehavior(new StanceConsequenceBehavior());
         // Stance with teeth, increment 3: murderous intent can spring a street ambush when the player is in town.
         campaignStarter.AddBehavior(new TownAmbushBehavior());
         // Stance with teeth, increment 3.2a: losing the ambush -> captured and convoyed to the paymaster lord.
         campaignStarter.AddBehavior(new AmbushCaptivityBehavior());
         // Duels in town: reuses the ambush's own town-centre injection recipe to stage a consented, non-lethal
         // duel where most conversations actually happen. Registered next to the ambush because it listens to
         // the same character-spawn event and shares its Mission.AfterStart seam.
         campaignStarter.AddBehavior(new Duels.TownDuelBehavior());
         // Chronicle (v1.2): persists the LLM-illuminated chronicle prose across the campaign.
         campaignStarter.AddBehavior(new ChronicleBehavior());
         // Companion missions (first slice "bring me the news"): a dispatched companion returns after a
         // timer with a report drawn from the world-event store. Decision logic is pure; this persists the
         // in-flight errands. Registered AFTER WorldEventStore (it reads from it). Console: cr.send_news.
         campaignStarter.AddBehavior(new CompanionMissionBehavior());
         // Companion happiness over time: a weekly tick presses idleness / being kept from a loved one and
         // eases toward baseline; a companion left bitterly unhappy for a season departs. The grievance is
         // voiced in conversation. The score lives on the profile; the pure model is CalradiaRemembers.Logic.
         campaignStarter.AddBehavior(new CompanionHappinessBehavior());
         // The positive counterpart: a happy, deeply-bonded companion may ask for a private word to confide
         // affection and become the player's discreet secret lover (adult-gated). Pure model in the Logic project.
         campaignStarter.AddBehavior(new CompanionAffectionBehavior());
         // Found-topic companion audiences (player feedback, Nexus): a companion asks for a private word only
         // once the mod has FOUND something worth saying (mission report / party grudge / practical counsel /
         // an old callback / a rumour), never a mood-only check-in. One shared cooldown across the whole
         // roster. Registered after CompanionMissionBehavior/GrudgeBehavior/WorldEventStore (it reads all three).
         campaignStarter.AddBehavior(new CompanionAudienceTopicBehavior());
         // War-weariness: wounds and captures accrue a toll on each companion (a SEPARATE axis from happiness);
         // rest eases it; a spent veteran asks for an honorable retirement — a landless one leaves, a landed one
         // (governor / party-leader) steps back from the field but stays in the clan. Pure model in Logic.
         campaignStarter.AddBehavior(new WarWearinessBehavior());
         // Camp life: a weekly tick nudges companion↔companion relations by trait compatibility (the
         // well-matched warm, the ill-matched feud) and weds the warm/eligible (kept in your clan). The
         // opinion is voiced in conversation; the pure rules are in CalradiaRemembers.Logic.
         campaignStarter.AddBehavior(new CompanionRapportBehavior());
         // Player appearance: persists the player-authored per-hero appearance prose that (with the derived body
         // facts) renders THE PLAYER'S APPEARANCE and each NPC's own appearance in the prompt, replacing the old
         // static player_description.txt. Console: cr.appearance.
         campaignStarter.AddBehavior(new AppearanceProseBehavior());
         // Keeps a long campaign from inflating every prompt: one character's superseded memories are folded
         // into a background outline per day. Gated by the "Compress old memories" option, so a mod that
         // curates memory itself can take over (extension API).
         campaignStarter.AddBehavior(new CalradiaRemembers.Memory.MemoryCompressionBehavior());
         // ASSEMBLY pillar: GATED OFF for release (built, not yet play-tested in a live campaign). To test it in
         // a DEV build, UNCOMMENT the two lines below, then stage a session fast with cr.assembly_test [n] and
         // cr.assembly_open (bypasses the travel wait). It ships as a real feature only once validated.
         // campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.AssemblyBehavior());
         // campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.AssemblyMenuBehavior());
         Log($"{ModName} campaign behavior registered.");
      }

      /// <summary>
      ///   Called once when Bannerlord finishes loading all mods and is ready
      ///   to show the main menu. Earliest reliable hook for "the mod is live".
      /// </summary>
      protected override void OnSubModuleLoad()
      {
         base.OnSubModuleLoad();
         Log($"{ModName} v1.9.0 loaded.");

         try
         {
            var harmony = new Harmony(ModuleId);
            harmony.PatchAll();
            Log("Harmony patches applied.");
         }
         catch (Exception ex)
         {
            Log($"Harmony patch failed: {ex.Message}");
         }

         RegisterGuiResources();
         InitializeUiExtender();
      }

      #region private

      // ── Logging helper ────────────────────────────────────────────────────

      /// <summary>
      ///   Logs a message to the Bannerlord in-game console (top-left) and the
      ///   game's log files. Useful during development; consider toggling off
      ///   for release once the mod is stable.
      /// </summary>
      private static void Log(string message)
      {
         InformationManager.DisplayMessage(new InformationMessage($"[{ModName}] {message}"));
      }

      /// <summary>
      ///   Inserts a single Widget-derived type into the built-in types map.
      ///   Uses the type's short name (no namespace) as the XML tag.
      /// </summary>
      private static void RegisterBuiltInType(Dictionary<string, Type> builtinTypes, Type widgetType)
      {
         builtinTypes[widgetType.Name] = widgetType;
      }


      /// <summary>
      ///   Boots UIExtenderEx so our encyclopedia mixin + prefab patch are applied
      ///   (adds the NPC's personal regard to their encyclopedia hero page).
      /// </summary>
      private void InitializeUiExtender()
      {
         try
         {
            var extender = UIExtender.Create(ModuleId);
            extender.Register(typeof(NpcMemoryServiceSubmodule).Assembly);
            extender.Enable();
            Log("UIExtender enabled (encyclopedia personal-relation extension).");
         }
         catch (Exception ex)
         {
            Log($"UIExtender init failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Registers our custom Widget-derived classes as built-in types in the
      ///   WidgetFactory, so &lt;ChatScrollablePanel/&gt; (and similar) are recognized
      ///   by the XML parser without requiring a separate .xml definition file.
      ///   WidgetFactory.AddCustomType is for XML-defined widgets and expects a
      ///   .xml file matching the type name. For pure-C# widgets we need to
      ///   inject them directly into the private _builtinTypes dictionary —
      ///   the same dictionary that WidgetFactory.Initialize populates from
      ///   WidgetInfo.GetWidgetInfos() at engine startup.
      /// </summary>
      private void RegisterCustomBuiltInWidgets()
      {
         try
         {
            WidgetFactory factory = UIResourceManager.WidgetFactory;
            if (factory == null)
            {
               Log("WidgetFactory not available — custom widgets not registered.");

               return;
            }

            FieldInfo field = typeof(WidgetFactory).GetField("_builtinTypes", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
               Log("Could not find WidgetFactory._builtinTypes field.");

               return;
            }

            if (field.GetValue(factory) is not Dictionary<string, Type> builtinTypes)
            {
               Log("WidgetFactory._builtinTypes was null or unexpected type.");

               return;
            }

            RegisterBuiltInType(builtinTypes, typeof(ChatScrollablePanel));
            RegisterBuiltInType(builtinTypes, typeof(CrRuntimeImageWidget));

            Log("Custom widget types registered.");
         }
         catch (Exception ex)
         {
            Log($"RegisterCustomBuiltInWidgets failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      // ── GUI registration ──────────────────────────────────────────────────

      /// <summary>
      ///   Registers the module's GUI folder with Bannerlord's resource depot so
      ///   our Gauntlet prefabs (chat window, etc.) can be loaded by name at runtime.
      ///   Also registers our custom built-in widget types so the XML parser
      ///   recognizes them.
      /// </summary>
      private void RegisterGuiResources()
      {
         string moduleRoot = ModuleHelper.GetModuleFullPath(ModuleId);
         string guiFolder = Path.Combine(moduleRoot, "GUI");
         string prefabFolder = Path.Combine(guiFolder, "Prefabs");

         if (!Directory.Exists(prefabFolder))
         {
            Log($"GUI prefab folder not found: {prefabFolder}");

            return;
         }

         if (UIResourceManager.ResourceDepot != null) UIResourceManager.ResourceDepot.AddLocation(moduleRoot, "/GUI/");

         RegisterCustomBuiltInWidgets();

         Log($"Registered GUI resources at {guiFolder}.");
      }

      #endregion
   }
}