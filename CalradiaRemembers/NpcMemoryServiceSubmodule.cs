// Code written by Gabriel Mailhot, 02/06/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bannerlord.UIExtenderEx;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
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
         campaignStarter.AddModel(new Stance.AmbushSettlementAccessModel(priorAccessModel));
         campaignStarter.AddBehavior(new NpcMemoryCampaignBehavior());
         // Player-authored character data: applies hand-crafted personalities (traits via SetTraitLevel,
         // the single source of truth) from ModuleData/character_overrides.json when the MCM toggle is on.
         campaignStarter.AddBehavior(new Lore.CharacterLoreBehavior());
         // Dynastic succession: on player death, reframes every NPC's memory through the heir — drops the
         // predecessor's private history, keeps and marks the political/relational history as inherited.
         campaignStarter.AddBehavior(new Dynasty.SuccessionBehavior());
         // Marriage system (family-approval engine) — SHIPPED from v1.7.0. The approval chat path
         // (grant_blessing, Phase A.2a) now exists, and the consequence only ever lands on the
         // PLAYER's own marriage (OnBeforeHeroesMarried bails when the player is not a spouse), so
         // vanilla NPC-NPC weddings are untouched. Marrying into a house without first earning its
         // blessing chills that family — the intended love-vs-ambition mechanic.
         campaignStarter.AddBehavior(new Marriage.MarriageApprovalBehavior());
         // Jealousy (Sprint 24): a romantic act can provoke a third party with a real stake.
         // Knowledge-gated, probabilistic, delayed; reactions are a grudge + optional threat letter.
         campaignStarter.AddBehavior(new Jealousy.JealousyBehavior());
         // Pregnancy birth: a CR-created pregnancy's child is placed in the MOTHER's clan at birth (the
         // bastard rule). Tracks CR's own conceptions only, so vanilla NPC patrilineality is untouched.
         campaignStarter.AddBehavior(new GameState.PregnancyBirthBehavior());
         // Bastards (consequence layer): tags the player's non-marital children and, once a season, may bring
         // a hidden one to light — a scandal recorded into the world-event store (read at runtime, so the
         // registration order is harmless) + a hit to the player's standing. Console: cr.bastards / cr.bastard_*.
         campaignStarter.AddBehavior(new Bastards.BastardBehavior());
         // World-event / rumor store (v1.2): persisted log of dated happenings feeding street
         // rumors, NPC awareness, and the player's chronicle. Feeders register separately.
         campaignStarter.AddBehavior(new WorldEvents.WorldEventStore());
         // Schemes (v1.3): bitter rivals plot against each other; the lifecycle is mechanical (pure
         // SchemeAdvancer), the effect is standing damage, and the player only ever hears of it as a
         // rumour fed to the world-event store. Registered AFTER WorldEventStore (it records into it).
         campaignStarter.AddBehavior(new WorldEvents.SchemeStore());
         // RP with teeth, increment 2 (Phase 1 spine): the TrackedCommitment engine — the symmetric of
         // InformalQuest; an NPC's promise TO THE PLAYER (flagship: pledge_against), verified by real
         // campaign events (a resolved scheme, or an organic battle) or breached on a lapsed deadline.
         // Registered after SchemeStore, which notifies it on a scheme's success — load order does not
         // strictly matter (Instance is set in each behavior's own constructor).
         campaignStarter.AddBehavior(new Commitments.CommitmentBehavior());
         // Divorce, Phase 1 (the first verb built on the IConversationVerb contract): end_marriage begins a
         // multi-day estrangement process (the spouse may resist, delay, or defeat it outright); reaching the
         // end day attempts the real dissolution and, on success, records a culture-scaled scandal. Console:
         // cr.divorce_info / cr.divorce / cr.divorce_tick.
         campaignStarter.AddBehavior(new Marriage.DivorceProcessBehavior());
         // Divorce, Phase 2b: the player's OWN spouse demands the divorce, entirely through conversation.
         // Owns the persisted demand flag + the weekly evaluation/grace-period-refusal sweep; the
         // accept_divorce/decline_divorce actions and the dissolution itself live in
         // BannerlordGameStateBridge/MarriageDissolutionService. Load order does not strictly matter
         // (Instance is set in the constructor); registered beside DivorceProcessBehavior for locality.
         // Console: cr.spouse_divorce_demand / cr.spouse_divorce_info / cr.spouse_divorce_tick.
         campaignStarter.AddBehavior(new Marriage.SpouseDivorceDemandBehavior());
         // Grudges (consolidation pillar): a resolvable, event-anchored grievance, coexisting with regard
         // instead of being diluted into it. Registered AFTER WorldEventStore (its one-shot backfill scans
         // the bastard-claim world events) and after Bastards/DivorceProcessBehavior (it scans their
         // persisted markers too). Console: cr.grudges / cr.grudge_test / cr.grudge_backfill.
         campaignStarter.AddBehavior(new Grudges.GrudgeBehavior());
         // Grudges Tier 3 LIVE plant site: the player executes a lord, that lord's close living kin
         // (spouse/parents/children/siblings, capped to a handful) each form a permanent ExecutedKin
         // vendetta. Registered right after GrudgeBehavior (this listener only ever calls its TryPlant);
         // no SyncData of its own, no backfill by design (Tier 3 has none). Console: cr.grudge_execute_kin.
         campaignStarter.AddBehavior(new Grudges.ExecutedKinGrudgeBehavior());
         // Grudges Tier 2 LIVE plant site: sway_opinion's own Rivalry rumour (recorded by SwayOpinionVerb
         // into WorldEventStore) is watched here for the swayed-against target's own awareness, planting a
         // hidden SwayExposed grudge once it plausibly reaches them. Registered after WorldEventStore (it
         // reads from it) and GrudgeBehavior (this listener only ever calls its TryPlant); no backfill (a
         // LIVE-only source, mirroring ExecutedKinGrudgeBehavior). Console: cr.grudge_sway_test.
         campaignStarter.AddBehavior(new Grudges.SwayExposureBehavior());

         campaignStarter.AddBehavior(new Nemesis.NemesisBehavior());
         // Appreciations (the positive mirror of Grudges): a lingering felt kindness, coexisting with regard
         // instead of being folded into it. Registered AFTER GrudgeBehavior (its prompt-note tarnish reads
         // the holder's dominant live grudge) and after WorldEventStore/Bastards/DivorceProcessBehavior (its
         // one-shot backfill scans their persisted markers too). Console: cr.appreciations / cr.appreciation_test
         // / cr.appreciation_backfill.
         campaignStarter.AddBehavior(new Appreciations.AppreciationBehavior());
         // Scripted scenarios (the world-reaction engine): a trigger (death/marriage/war/peace) fires a
         // rule whose consequences mutate the world and whose message is dropped into the world-event store.
         // Pure selection lives in CalradiaRemembers.Logic; packs load from ModuleData/scenarios. Registered
         // AFTER WorldEventStore (it records its message into it). Console: cr.scenarios / cr.scenario_fire.
         campaignStarter.AddBehavior(new Scenarios.ScenarioBehavior());
         // Interceptions: a nearby lord may ride out to the player (hostile territorial challenge, or
         // a reaction to fresh news from the rumour store). Decision is pure; this only moves parties.
         campaignStarter.AddBehavior(new Interception.InterceptionBehavior());
         // Stance (multi-axis posture): time-based fear decay + the prisoner-taken deed. Other deeds
         // are applied at their own event source; the decision/tuning is pure (CalradiaRemembers.Logic).
         campaignStarter.AddBehavior(new Stance.StanceBehavior());
         // Stance with teeth: weekly, a hardened posture becomes an unbidden hostile/favourable letter.
         campaignStarter.AddBehavior(new Stance.StanceConsequenceBehavior());
         // Stance with teeth, increment 3: murderous intent can spring a street ambush when the player is in town.
         campaignStarter.AddBehavior(new Stance.TownAmbushBehavior());
         // Stance with teeth, increment 3.2a: losing the ambush -> captured and convoyed to the paymaster lord.
         campaignStarter.AddBehavior(new Stance.AmbushCaptivityBehavior());
         // Chronicle (v1.2): persists the LLM-illuminated chronicle prose across the campaign.
         campaignStarter.AddBehavior(new Chronicle.ChronicleBehavior());
         // Companion missions (first slice "bring me the news"): a dispatched companion returns after a
         // timer with a report drawn from the world-event store. Decision logic is pure; this persists the
         // in-flight errands. Registered AFTER WorldEventStore (it reads from it). Console: cr.send_news.
         campaignStarter.AddBehavior(new Missions.CompanionMissionBehavior());
         // Companion happiness over time: a weekly tick presses idleness / being kept from a loved one and
         // eases toward baseline; a companion left bitterly unhappy for a season departs. The grievance is
         // voiced in conversation. The score lives on the profile; the pure model is CalradiaRemembers.Logic.
         campaignStarter.AddBehavior(new Missions.CompanionHappinessBehavior());
         // War-weariness: wounds and captures accrue a toll on each companion (a SEPARATE axis from happiness);
         // rest eases it; a spent veteran asks for an honorable retirement — a landless one leaves, a landed one
         // (governor / party-leader) steps back from the field but stays in the clan. Pure model in Logic.
         campaignStarter.AddBehavior(new Missions.WarWearinessBehavior());
         // Camp life: a weekly tick nudges companion↔companion relations by trait compatibility (the
         // well-matched warm, the ill-matched feud) and weds the warm/eligible (kept in your clan). The
         // opinion is voiced in conversation; the pure rules are in CalradiaRemembers.Logic.
         campaignStarter.AddBehavior(new Missions.CompanionRapportBehavior());
         Log($"{ModName} campaign behavior registered.");
      }

      /// <summary>
      ///   Per-frame tick, fired by the engine even when the campaign is paused and no chat is open. Used only
      ///   to pump the opt-in <see cref="VramWatch" /> heartbeat (off by default) so the background map screen's
      ///   VRAM can be traced during the no-chat control test.
      /// </summary>
      protected override void OnApplicationTick(float dt)
      {
         base.OnApplicationTick(dt);
         VramWatch.Tick();
         Chat.CaptiveSceneStager.Tick(); // opens a staged captive chat once its conversation scene has loaded
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