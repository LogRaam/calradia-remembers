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
using CalradiaRemembers.Battles;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Chronicle;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.Escort;
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
      ///   Per-frame tick, fired by the engine even when the campaign is paused and no chat is open. It drains any
      ///   deferred main-thread work (<see cref="MainThreadDispatcher" />) and drives the staged scene openers, plus
      ///   the opt-in <see cref="VramWatch" /> heartbeat. Being pause-independent is why a chat open deferred out of
      ///   an inquiry consequence still runs promptly even while a settlement menu holds the campaign paused.
      /// </summary>
      protected override void OnApplicationTick(float dt)
      {
         base.OnApplicationTick(dt);
         // Runs continuations queued off the main path (e.g. an audience chat opened ONE FRAME AFTER its inquiry
         // has fully dismissed, so the overlay never contends with the inquiry teardown inside a settlement menu).
         MainThreadDispatcher.Pump();
         VramWatch.Tick();
         ChatSceneStager.Tick(); // opens a staged captive chat once its conversation scene has loaded
         CouncilConversationStager.Tick(); // opens a staged council once its conversation scene has loaded
         Duels.DuelSceneSpike.Tick(dt); // drives the wilderness-duel spike's open/close/VRAM state machine
         Duels.DuelCoordinator.OnTick(dt); // stages an armed duel; here (not CampaignEvents.TickEvent) so it runs inside a settlement too
         Companions.CompanionBriefingCoordinator.TickReturn(); // takes the player back into the meeting a briefing was for
         UI.Spike.CrMapTickSpike.Tick(dt); // throwaway spike (cr.tick_probe): closes its own measurement window
         Chat.CrVideoPlayer.Tick(); // CR's own video player: the frame due, as a new texture (runs in conversations too)
         Chat.ChatManager.TickEscapeClose(); // an Escape close, once the key is up, outside the chat's widget update (philip0105)
         Interception.AudienceInquiry.Tick(dt); // an invitation away while its "Who is this?" encyclopedia page is read (Lordfadooboo)
         Map.MapNavigationCouncilItemPatch.Tick(); // reports ONCE if another mod removed our council bar button
      }

      /// <summary>
      ///   Called after all submodules have loaded — safe to assume other mods
      ///   are present at this point.
      /// </summary>
      protected override void OnBeforeInitialModuleScreenSetAsRoot()
      {
         base.OnBeforeInitialModuleScreenSetAsRoot();
         // Attach the siege-visual tick guard now that every module (incl. SandBox.View, which owns the target type)
         // is loaded: while a CR panel owns the screen, its Harmony prefix skips the vanilla settlement-visual tick
         // that otherwise crashes natively under a pushed panel. Self-guarded, so a version mismatch is a no-op.
         UI.SettlementVisualTickGuard.EnsurePatched();
         // Same class of problem, same shape of guard: the map keeps reading its own hotkeys under a pushed panel,
         // so the face generator opened while the player was typing a letter reply (Gabriel, 2026-09-06).
         UI.MapNavigationInputGuard.EnsurePatched();
         // The Prompt Composer: fill MCM's "Prompt Profile" list before its screen can be opened (MCM's saved place may
         // point past the one-entry list the settings start with).
         Mapping.PromptProfileService.Refresh();
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

         // A meeting held for landfall belongs to the campaign it was asked in, never to the next one loaded.
         Chat.ChatSceneStager.ForgetAwaitingLandfall();

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
         // Self-esteem (30/09/2026, Lordfadooboo): battles, captivities, tournaments, fiefs and deaths move how a met
         // character regards himself; recorded on his profile, nothing saved of its own.
         campaignStarter.AddBehavior(new Standing.SelfEsteemBehavior());
         // Marriage system (family-approval engine) — SHIPPED from v1.7.0. The approval chat path
         // (grant_blessing, Phase A.2a) now exists, and the consequence only ever lands on the
         // PLAYER's own marriage (OnBeforeHeroesMarried bails when the player is not a spouse), so
         // vanilla NPC-NPC weddings are untouched. Marrying into a house without first earning its
         // blessing chills that family — the intended love-vs-ambition mechanic.
         campaignStarter.AddBehavior(new MarriageApprovalBehavior());
         // Jealousy (Sprint 24): a romantic act can provoke a third party with a real stake.
         // Knowledge-gated, probabilistic, delayed; reactions are a grudge + optional threat letter.
         campaignStarter.AddBehavior(new JealousyBehavior());
         // Open relationship (Inc 3a): the symmetry of the jealousy suppression. A partner who holds reciprocal
         // open terms with the player autonomously takes a lover of her OWN over time, the likelier the more the
         // player neglects her (reusing the jealousy loop's neglect measure). Silent this increment: it only
         // forms the lover link (RomanticProfile.TakenLoverId) + a faint rumour for a later increment to surface.
         // Registered after JealousyBehavior (it reads its public NeglectDaysFor seam); Instance is set in the
         // constructor so load order is harmless. Not gated by the jealousy MCM toggle (it is her own agency).
         campaignStarter.AddBehavior(new OpenPartnerLover.OpenPartnerLoverBehavior());
         // Mystery-lover: once a secret lover has been secret long enough, an anonymous whisper of its EXISTENCE
         // (never the player's identity) may begin to circulate, which lords then probe the player about via the
         // world-gossip register. Instance set in the constructor, so load order is harmless.
         campaignStarter.AddBehavior(new WorldEvents.SecretLoverGossipBehavior());
         campaignStarter.AddBehavior(new Tournaments.TournamentRivalryBehavior());
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

         // The seasonal historian writes INTO the store above, so it is registered after it (Gabriel's design,
         // 2026-09-12: one historian for the world, and no fourth memory store - the chronicle is WorldEvents).
         campaignStarter.AddBehavior(new WorldAlmanac.SeasonalChronicleBehavior());
         // The balance-of-power survey (28/09/2026): a developer measurement, off until cr.balance_survey on.
         campaignStarter.AddBehavior(new WorldAlmanac.BalanceSurveyBehavior());
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
         // Vassal war duty (Maifailun, Gabriel, 15/10/2026): keeps the service ledger; the ruler acts only with the MCM switch on.
         campaignStarter.AddBehavior(new WarDuty.WarDutyBehavior());
         // CrAims (tashmetu 14/10, Gabriel 15/10): standing aims and the leave to raise one; internal until the public door is decided.
         campaignStarter.AddBehavior(new Aims.AimsBehavior());
         Aims.AimsBehavior.PersonalRegard = h => NpcMemoryCampaignBehavior.Instance?.Store?.Get(h.StringId)?.ReputationWithPlayer;
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
         // Stolen Love pillar: watches the player's married affair partners and paces the estrangement of their
         // marriage (daily tick). Registered before GrudgeBehavior since Increment 3 will plant the jilted
         // spouse's grudge; gated by the MCM EnableStolenLove toggle (dormant when off).
         campaignStarter.AddBehavior(new Marriage.NpcMarriageEstrangementBehavior());
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
         // Realm tidings: weekly, a friendly lord relays a real known world event (looming war, town hardship,
         // a rumour) by unbidden letter, competing in the same mailbox budget. Registered AFTER WorldEventStore
         // (it reads from it) and reuses the LetterCandidateIntent mailbox path.
         campaignStarter.AddBehavior(new CalradiaRemembers.RealmTidings.RealmTidingsBehavior());
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
         // Fief audiences (player wish, Fakade): the road-only InterceptionBehavior above needs a field party to
         // move, so anyone without one (ladies, notables, a garrisoned lord standing around in a fief) never got
         // a turn. On settlement entry, offers an Accept/Decline word with the strongest fief-valid partyless NPC
         // standing there. CompanionAudienceTopicBehavior.OnSettlementEntered drives this (see that file) so at
         // most one audience popup -- companion or fief -- appears per entry; registered after it for that reason.
         campaignStarter.AddBehavior(new FiefAudienceBehavior());
         // Prisoner exchange (fkasad, 25/09/2026): the envoy that replaced the PrisonerOffer letter.
         campaignStarter.AddBehavior(new Letters.PrisonerEnvoyBehavior());
         // Courier secrets (raphareish, 24/09/2026; Gabriel): an intercepted letter travels with its captors to whoever
         // beats them, and up to the lord who answers for that troop.
         campaignStarter.AddBehavior(new Letters.CourierSecretBehavior());
         // The mod's transient parties (sergeants, bounty hunters): one save despawn, one loss report (26/09/2026, the
         // event-listeners map's proposal). Before its owners.
         campaignStarter.AddBehavior(new Travel.TransientPartyRegistry());
         // Arrests in the King's name (Gabriel, 26/09/2026): the player's case, opened by a grave act.
         campaignStarter.AddBehavior(new Arrests.ArrestBehavior());
         // fkasad's grid (25/09/2026): a mercenary contract's end judged by the realm's situation.
         campaignStarter.AddBehavior(new WorldEvents.MercenaryReputationBehavior());
         // A hungry town remembers who fed it, and who emptied its market (fkasad, Lordfadooboo, 01/10/2026).
         campaignStarter.AddBehavior(new WorldEvents.FoodTradeMemoryBehavior());
         // The day of every wedding, for the family line (raphareish, 01/10/2026).
         campaignStarter.AddBehavior(new Mapping.WeddingDayBehavior());
         campaignStarter.AddBehavior(new Marriage.ForeignDivorceBehavior());
         campaignStarter.AddBehavior(new Quests.AuthoredQuestBehavior());
         campaignStarter.AddBehavior(new Mapping.RealmFaceBehavior()); // queens with a chosen face (Gabriel, 12/10/2026)
         campaignStarter.AddBehavior(new Quests.TaleCompanyFightSpike());
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
         // Council session persistence (C4): always on. It only holds state while a council (cr.council, the
         // shipped path) is actually open, and recovers its pledges if a save was caught mid-sitting; harmless
         // and idle otherwise, so it is not gated with the rest of the Assembly pillar below.
         campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.CouncilSessionBehavior());
         // Council stipend escrow (grant_stipend, the fifth executable resolution and the first that touches
         // persisted save state): always on, mirroring CouncilSessionBehavior above. Idle (no active stipends)
         // outside a council that actually grants one, so it is not gated with the rest of the Assembly pillar.
         campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.CouncilStipendBehavior());
         // Council tribute (Partie 8, "tribute"): a Parley-scoped, pay-as-you-go daily payment from a defeated
         // enemy leader's own coffers, always on, mirroring CouncilStipendBehavior/CouncilTributeBehavior above.
         // Idle (no active tributes) outside a Parley that actually strikes one, so it is not gated with the
         // rest of the Assembly pillar either.
         campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.CouncilTributeBehavior());
         // Council troop delegation (Partie 1, "lend_troops", the highest-detail item in COUNCIL_ACTIONS.md): the
         // first council executor whose deed does not settle at the lift itself - a real MobileParty travels the
         // map with the loaned troops already aboard, always on, mirroring CouncilStipendBehavior/
         // CouncilTributeBehavior above. Idle (no in-flight deliveries) outside a council that actually strikes
         // one, so it is not gated with the rest of the Assembly pillar either.
         campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.CouncilTroopDeliveryBehavior());
         // Council hostage (Partie 8, "give_hostage", Kimi's "v1 invite d'honneur"): a Parley-scoped, fixed-term
         // guest-hostage residing in the player's own party, always on, mirroring CouncilStipendBehavior/
         // CouncilTributeBehavior above. Idle (no active hostage terms) outside a Parley that actually strikes
         // one, so it is not gated with the rest of the Assembly pillar either.
         campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.CouncilHostageBehavior());
         // Council non-aggression pact (Partie 8, "non_aggression_pact", rounding out the Parley toolkit with a
         // FIFTH concession): a Parley-scoped, fixed-term promise binding only the mod's OWN hostile-quest
         // emission and a detected/sanctioned breach (see NonAggressionPactPolicy's own header - vanilla AI does
         // NOT respect it), always on, mirroring CouncilStipendBehavior/CouncilTributeBehavior/CouncilHostageBehavior
         // above. Idle (no active pacts) outside a Parley that actually strikes one, so it is not gated with the
         // rest of the Assembly pillar either.
         campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.CouncilPactBehavior());
         // Council ward (Partie 8, "sponsor_ward", Kimi's design MINIMAL v1): a ClanLords/Family-scoped,
         // no-term mentor-protege bond between a seated clan member and a young companion of the player's own
         // clan, always on, mirroring CouncilStipendBehavior/CouncilTributeBehavior/CouncilHostageBehavior above.
         // Idle (no active bonds) outside a council that actually strikes one, so it is not gated with the rest
         // of the Assembly pillar either.
         campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.CouncilWardBehavior());
         // follow_me (1:1 bridge action, player wish: a companion "staying by my side", the map-escort half of
         // it, the battlefield version stays roadmapped): a lord's own party escorts the player's for a fixed
         // term, mirroring CouncilStipendBehavior/CouncilHostageBehavior's own SyncData shape. Always on, idle
         // (no active escorts) outside a conversation that actually strikes one.
         campaignStarter.AddBehavior(new FollowMeBehavior());
         // Retainer (the idle-lord ride_with_me path, complement of the follow_me escort): a lord/lady of ANOTHER
         // clan rides IN the player's own party (MobileParty.MainParty member roster) for a time WITHOUT marriage
         // and WITHOUT joining the player's clan, staying affiliated to their OWN clan, and breaks off when their
         // faction goes to war with the player. Additive, save-safe StringId track; every read guarded. The
         // player-facing verb is ON by default (RetainerBehavior.Enabled, backed by the MCM "Lords can ride in
         // your party" toggle), turned off from the menu or a cr.retainer enable override; the console cr.retainer
         // and the self-test drive the raw mechanism regardless. Idle otherwise.
         campaignStarter.AddBehavior(new CalradiaRemembers.Retainer.RetainerBehavior());
         // The giver of one of the game's own quests remembers it, begun and ended (tashmetu, Maifailun, 08/10/2026).
         campaignStarter.AddBehavior(new CalradiaRemembers.Quests.NativeQuestMemoryBehavior());
         // The lord who takes the player prisoner remembers how: the battle, where, the men led (Windmolen, 08/10/2026).
         campaignStarter.AddBehavior(new CalradiaRemembers.Battles.CaptureMemoryBehavior());
         // Courtship trial (author's story): a marriage prospect who agrees to wed BUT first rides at the player's
         // side to test him. Records the day a valid, not-hard-blocked prospect BEGINS accompanying (follow_me or
         // ride_with_me), lapses a trial abandoned early, and once ~two seasons are served latches a passed flag
         // AND has her reach out (a MarriageProposal letter in her own voice) to accept; the flag then opens the
         // marriage gate by clearing ONLY the soft RegardTooLow block (hard blockers stay shut). Additive, save-safe.
         campaignStarter.AddBehavior(new CalradiaRemembers.Marriage.CourtshipTrialBehavior());
         // War-council OFFER trigger (player suggestion): after the player's army forms or takes a considerable
         // action (settlement captured, notable battle won), offer to convene the already-built WarCouncil instead
         // of typing cr.council war. GATED OFF by default (WarCouncilOfferBehavior.Enabled) since the council window
         // is still being validated in-game; armed for a session with cr.warcouncil on. The behavior only decides
         // WHEN to offer (pure WarCouncilTriggerPolicy + a save-safe cooldown) and reuses cr.council war's own
         // convening path on acceptance. Always registered (idle while disarmed). Console: cr.warcouncil [on|off|now].
         campaignStarter.AddBehavior(new CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior());
         // ASSEMBLY pillar: GATED OFF for release (built, not yet play-tested in a live campaign). To test it in
         // a DEV build, UNCOMMENT the two lines below, then stage a session fast with cr.assembly_test [n] and
         // cr.assembly_open (bypasses the travel wait). It ships as a real feature only once validated.
         // campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.AssemblyBehavior());
         // campaignStarter.AddBehavior(new CalradiaRemembers.Assemblies.AssemblyMenuBehavior());
         Log($"{ModName} campaign behavior registered.");
      }

      /// <summary>
      ///   Attaches the battle-feat journal to every fight the player can be part of, so a battle leaves REAL facts
      ///   (whom the player bested, who put them down) instead of one generic line. Arena combat is included on
      ///   purpose: it is how a tournament bout yields the same named deeds. Non-combat missions (conversations,
      ///   town walks) get nothing. The journal is mission-scoped and persists nothing by itself.
      /// </summary>
      public override void OnMissionBehaviorInitialize(Mission mission)
      {
         base.OnMissionBehaviorInitialize(mission);

         try
         {
            if (mission == null) return;
            if (mission.CombatType != Mission.MissionCombatType.Combat
                && mission.CombatType != Mission.MissionCombatType.ArenaCombat) return;

            mission.AddMissionBehavior(new BattleFeatJournal());
         }
         catch (Exception ex) { ModLog.Error($"[BATTLEFEAT] could not attach the journal: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Called once when Bannerlord finishes loading all mods and is ready
      ///   to show the main menu. Earliest reliable hook for "the mod is live".
      /// </summary>
      protected override void OnSubModuleLoad()
      {
         base.OnSubModuleLoad();
         Log($"{ModName} v1.9.0 loaded.");

         // The one regard-change notice, re-raised as the public CrNpcMemory.RegardChanged (29/09/2026). Once per
         // process: this runs once, and the SDK event is static.
         NpcMemoryService.Core.Services.ProfileMutator.RegardChanged += CrNpcMemory.ForwardRegardChange;
         // The same notice completes an earn_regard task (Lordfadooboo, 01/10/2026). Static, so subscribed once here.
         NpcMemoryService.Core.Services.ProfileMutator.RegardChanged += Quests.QuestDeedTracker.OnRegardChanged;
         // What CR did to fit a local model's declared window (fkasad, 13/10/2026): logged, kept, told once.
         NpcMemoryService.Core.Services.NpcChatService.ContextFitted += LocalContextState.OnFitted;

         try
         {
            var harmony = new Harmony(ModuleId);
            harmony.PatchAll();
            Log("Harmony patches applied.");

            // One patch is asked to prove itself by name: the map-bar button is the one players lose, and
            // "Harmony patches applied" covers the whole set, so a target that never resolved left no trace.
            Map.MapNavigationCouncilItemPatch.ReportAttachment(harmony);
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
            ModLog.Info("[UIEXTENDER] Register+Enable completed.");
            VerifyClanScreenMixinPatch();
         }
         catch (Exception ex)
         {
            Log($"UIExtender init failed: {ex.GetType().Name}: {ex.Message}");
            ModLog.Error($"[UIEXTENDER] Register/Enable FAILED: {ex}");
         }
      }

      // DIAGNOSTIC (2026-09-05, clan-screen BoP button missing): proves whether UIExtenderEx's constructor
      // transpiler actually landed on ClanManagementVM. If the ctor lists a transpiler, the mixin MUST attach when
      // the Clan screen builds its VM (look for "[BOP-CLAN] mixin attached"); if it lists none, the registration
      // never reached the patch stage and the problem is upstream. Remove once the button ships.
      private static void VerifyClanScreenMixinPatch()
      {
         try
         {
            foreach (ConstructorInfo ctor in typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanManagementVM).GetConstructors())
            {
               var info = Harmony.GetPatchInfo(ctor);
               ModLog.Info($"[BOP-CLAN] ClanManagementVM ctor Harmony state: transpilers={info?.Transpilers?.Count ?? -1}, "
                           + $"prefixes={info?.Prefixes?.Count ?? -1}, postfixes={info?.Postfixes?.Count ?? -1}.");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BOP-CLAN] ctor patch verification failed: {ex.GetType().Name}: {ex.Message}");
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
         Log($"[PATHS] module files read from {CalradiaRemembers.Paths.ModulePaths.Root}");
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