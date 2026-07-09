// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Scenarios;
using CalradiaRemembers.Services;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Verbs;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TraitFacts = CalradiaRemembers.Logic.Appreciations.TraitFacts;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>
   ///   End-to-end self-test harness. Each test seeds state via the real service/bridge,
   ///   asserts what happened, and yields a <c>[SELFTEST]</c> PASS/FAIL/MANUAL line.
   ///   <para>
   ///     Adding a test is one method + one <see cref="Tests" /> entry — no new console command
   ///     (cr.testall / cr.test &lt;name&gt; dispatch to the registry). Assertions go through
   ///     <see cref="TestRun" />, which builds the detail string and the verdict for you.
   ///   </para>
   ///   <para>
   ///     WARNING: tests mutate live campaign state (relations, quests, clans, prisoners, wars).
   ///     Copy your save before running, or reload when done to discard mutations.
   ///   </para>
   /// </summary>
   public static class CrSelfTest
   {
      /// <summary>Test-grudge magnitude planted on the SAME holder, to exercise the tarnish cross-interaction.</summary>
      private const int AppreciationFlowGrudgeMagnitude = 50;

      // ── Appreciations: the live flow (plant -> lives -> a fresh grudge tarnishes it -> backfill is ──
      // ── idempotent). Mirrors TestGrudgeBackfill's arrange/act/assert shape; exercises the exact ──
      // ── AppreciationPolicy.TarnishedMagnitude call the prompt's ResolveAppreciationNote and the ──
      // ── Kindnesses encyclopedia mixin both make. ──────────────────────────────────────────────────

      /// <summary>Test-appreciation magnitude — comfortably above the formation floor.</summary>
      private const int AppreciationFlowTestMagnitude = 60;

      // ── Grudges: the resolution engine (Arrange mirrors grudge_test/grudge_tier2: plant via the ──
      // ── real TryPlant gate, resolve via the real GrudgeResolutionService seam both make_amends and ──
      // ── the gift-routing hooks share) ──────────────────────────────────────────────────────────────

      /// <summary>
      ///   A magnitude comfortably above every formation floor and any lingering test grudge from an earlier run in the
      ///   same session, so the newly-planted grudge dominates.
      /// </summary>
      private const int GrudgeResolveTestMagnitude = 80;

      /// <summary>Mechanical (non-LLM) tests get this flat wall-clock budget, plenty for in-memory state checks.</summary>
      private const int MechanicalBudgetSeconds = 20;

      /// <summary>The registry. To add a test: write a method, then add one line here.</summary>
      private static readonly List<SelfTest> Tests = new() {
         new SelfTest("providegold", Severity.Light, "seed ProvideGold -> take_gold -> assert stamped", TestProvideGold),
         new SelfTest("quest_hideout", Severity.Light, "issue a BanditHideout quest through the real pipeline anchored on a town with a live infested lair nearby -> assert it bound a REAL existing hideout (TargetHideoutId) + a cardinal bearing (DirectionHint) against the live world (MANUAL when no town has an infested lair in range, which is itself the designed refusal)", TestQuestHideout),
         new SelfTest("scheme_warn", Severity.Light, "ForcePlot -> TargetHeeds -> assert plotter/target relation deltas", TestSchemeWarn),
         new SelfTest("succession", Severity.Light, "seed private+public events -> Apply -> assert private dropped", TestSuccession),
         new SelfTest("recruit", Severity.Heavy, "eligible lord -> join_clan -> assert he moved into your clan", TestRecruit),
         new SelfTest("deliver_prisoner", Severity.Heavy, "capture -> give_prisoner -> assert transfer + deed stamp", TestDeliverPrisoner),
         new SelfTest("free_prisoner", Severity.Heavy, "capture -> free_prisoner -> assert released", TestFreePrisoner),
         new SelfTest("give_item", Severity.Light, "pick a player item -> give_item -> assert it left the inventory", TestGiveItem),
         new SelfTest("stance_deed", Severity.Light, "a gift deed -> assert Trust rose and persisted", TestStanceDeed),
         new SelfTest("stance_letter", Severity.Heavy, "hostile stance -> weekly eval -> assert a cold StanceHostility letter is queued", TestStanceLetter),
         new SelfTest("stance_plot", Severity.Heavy, "stance with teeth increment 2: force a Plot-verdict lord (traits forced dishonourable+calculating) -> weekly eval -> assert a real scheme is planted against the player, no letter written", TestStancePlot),
         new SelfTest("stance_intercept", Severity.Heavy, "stance with teeth increment 2: force an Intercept-verdict lord (traits forced honourable+bold) -> weekly eval -> assert InterceptionBehavior actually begins a pursuit (skips MANUAL if its own cooldown/army guard legitimately blocks it)", TestStanceIntercept),
         new SelfTest("stance_warning", Severity.Heavy, "stance with teeth increment 2: seed a secret scheme against the player -> force a friendly Warning-verdict lord -> assert the letter NAMES the real plotter", TestStanceWarning),
         new SelfTest("scheme_assist", Severity.Heavy, "plot + regard -> PlayerAssists -> assert the plot advanced", TestSchemeAssist),
         new SelfTest("pregnancy", Severity.Heavy, "fertile female -> TryConceive(force) -> assert pregnant", TestPregnancy),
         new SelfTest("companion_mission", Severity.Heavy, "dispatch a companion -> assert away -> resolve -> assert home", TestCompanionMission),
         new SelfTest("bastard_discovery", Severity.Heavy, "tag a bastard -> Discover -> assert flag + a Scandal world-event", TestBastardDiscovery),
         new SelfTest("bastard_claim", Severity.Heavy, "force a grown bastard's claim -> assert hostility + claim world-event", TestBastardClaim),
         new SelfTest("bastard_scandal_culture", Severity.Heavy, "discover a bastard -> assert the scandal magnitude is culture-scaled", TestBastardScandalCulture),
         new SelfTest("feed_ingest", Severity.Light, "Feed.Record an external event -> assert stored + a re-send is deduped", TestFeedIngest),
         new SelfTest("feed_update", Severity.Light, "Feed.Update upserts a fed fact by id, Feed.Retract removes it (mutable entries)", TestFeedUpdate),
         new SelfTest("mission_register", Severity.Heavy, "MissionBoard.Register an external mission -> assert issued + dedup + Complete stamps it", TestMissionRegister),
         new SelfTest("mission_reward", Severity.Heavy, "MissionBoard FULL LOOP -> register -> Complete -> player reports to giver -> assert reward PAID + quest Completed", TestMissionRewardLoop),
         new SelfTest("rp_influence", Severity.Light, "eligible lord -> give_influence -> assert clan influence moved + marker + cooldown refuses repeat", TestRpInfluence),
         new SelfTest("rp_troops", Severity.Heavy, "lord w/ healthy party -> lend_troops -> assert rosters moved + marker", TestRpTroops),
         new SelfTest("rp_give_troops", Severity.Heavy, "lord w/ under-strength party -> give_troops -> assert rosters moved + marker + appreciation planted", TestRpGiveTroops),
         new SelfTest("rp_spend_influence", Severity.Light, "needy lord's clan -> spend_influence -> assert clan influence moved + marker + appreciation planted + cooldown refuses repeat", TestRpSpendInfluence),
         new SelfTest("rp_sway", Severity.Light, "NPC + 3rd hero -> sway_opinion(against) -> assert relation dropped + marker + world event + cooldown refuses repeat", TestRpSway),
         new SelfTest("rp_release", Severity.Heavy, "seed 3rd-party captive on a lord's party -> release_prisoner -> assert freed + relation bump", TestRpRelease),
         new SelfTest("commitment_fulfilled", Severity.Light, "seed a real pledge -> OnSchemeResolved(true) -> assert no longer active + reputation bonus + world event + journal mirror closed", TestCommitmentFulfilled),
         new SelfTest("commitment_breached", Severity.Light, "seed a real pledge, force it overdue -> breach sweep -> assert no longer active + reputation penalty + world event", TestCommitmentBreached),
         new SelfTest("divorce_process", Severity.Heavy, "married couple -> end_marriage -> force endDay -> tick -> assert dissolved + exSpouses + no one clanless + scandal + relation hits", TestDivorceProcess),
         new SelfTest("spouse_divorce", Severity.Heavy, "marry the player (test spouse) -> force demand -> accept_divorce -> assert dissolved + flag cleared; second test spouse -> force demand -> decline_divorce -> assert refusal grudge planted", TestSpouseDivorceDemand),
         new SelfTest("player_divorce", Severity.Heavy, "marry the player (test spouse, happy) -> end_own_marriage -> assert dissolved + a Divorce grudge on the player; second test spouse (miserable) -> end_own_marriage -> assert dissolved + NO grudge", TestPlayerDivorce),
         new SelfTest("spouse_estrangement", Severity.Heavy, "marry the player (test spouse) -> force-escalate -> force deadline to today -> tick -> assert dissolved + spouse-side Divorce grudge + escalation cleared; second test spouse -> force-escalate -> raise relation above the reconciliation threshold -> tick -> assert marriage SURVIVES + escalation cleared", TestSpouseEstrangement),
         new SelfTest("grudge_backfill", Severity.Light, "seed a synthetic Jealousy NotableEvent -> force backfill -> assert a Jealousy grudge exists -> force it again -> assert no duplicate", TestGrudgeBackfill),
         new SelfTest("grudge_tier2", Severity.Heavy, "capture+give_prisoner (real path) -> assert a DeliveredPrisoner grudge on the captive; direct severity-3 player-perpetrator CaptiveAbuse plant -> assert it exists", TestGrudgeTier2),
         new SelfTest("grudge_resolve", Severity.Light, "plant a FailedQuest grudge -> resolve w/ its matching gesture -> assert it moved; plant an ExecutedKin (vendetta) grudge -> assert an amends is Rejected; plant a DeliveredPrisoner (always Deed-kind) grudge -> resolve w/ the Deed gesture -> assert it moved", TestGrudgeResolve),
         new SelfTest("grudge_terms", Severity.Light, "plant a known grudge -> assert hint undiscovered -> grudge_terms reveal -> assert hint discovered -> reveal again -> assert nothing new", TestGrudgeTerms),
         new SelfTest("grudge_execute_kin", Severity.Light, "find a live lord with a recognized close living kin -> ExecutedKinGrudgeBehavior.PlantOnKinOf (the real engine-glue loop) -> assert a vendetta grudge lands on that kin at max rigidity, unresolvable", TestGrudgeExecuteKin),
         new SelfTest("grudge_unblessed_marriage", Severity.Heavy, "marry the player (test spouse, no blessing seeded) via the real MarriageApprovalBehavior wedding hook -> assert an UnblessedMarriage grudge lands on a disapproving kinsman -> end_own_marriage -> assert the grudge resolves (cleared, not merely decayed)", TestGrudgeUnblessedMarriage),
         new SelfTest("grudge_sway", Severity.Light, "NPC + 3rd hero -> real sway_opinion(against) via the bridge -> assert SwayExposureBehavior queued a pending watch -> DebugForceReveal -> assert a hidden SwayExposed grudge lands on the target", TestGrudgeSway),
         new SelfTest("companion_revenge", Severity.Light, "companion-victim Phase 3: plant a CaptiveAbuse grudge (companion vs PLAYER) AND a CaptorCruelty grudge (companion vs CAPTOR) on the SAME holder the SAME day -> assert BOTH survive, since they use distinct sources so the holder+source+day dedup does not collapse them", TestCompanionRevenge),
         new SelfTest("appearance", Severity.Light, "extract the player's body facts -> compose the appearance block in Subject (3rd person, read by an NPC) and Self (2nd person) voices, with authored prose -> assert the facts extract, the authored prose is carried, and the Self voice reads in the second person", TestAppearance),
         new SelfTest("quest_menu", Severity.Light, "viable-quest referential: scan a live lord's grounded quest menu -> assert it is non-empty (there is always something to do), every line carries a known type token, and a named settlement target truly resolves (so QuestFactory will ground the offer by construction)", TestQuestMenu),
         new SelfTest("prisoner_trade", Severity.Heavy, "prisoner trade: take a real lord captive into the player's party -> real buy_prisoner via the bridge (an NPC lord buys him) -> assert the player was PAID (price clamped around the ransom anchor) and the captive left the player's custody -> cleanup frees him", TestPrisonerTrade),
         new SelfTest("appreciation_flow", Severity.Light, "plant a kindness -> assert it lives -> plant a strong grudge on the same holder -> assert the tarnished effective magnitude drops -> force backfill twice -> assert the second pass is a no-op", TestAppreciationFlow),
         new SelfTest("nemesis_kill", Severity.Light, "mint a fresh nemesis -> NemesisResolutionBehavior.Resolve(battle death) -> assert IsAlive flips false + a closure world-event is recorded", TestNemesisKilledInBattle),
         new SelfTest("nemesis_execute", Severity.Light, "mint a fresh nemesis -> NemesisResolutionBehavior.Resolve(executed) -> assert IsAlive flips false + the closure world-event reads distinctly from the battle-death flavor", TestNemesisExecuted),
         new SelfTest("nemesis_execute_prisoner", Severity.Light, "mint a fresh nemesis -> TakePrisonerAction (real capture) -> assert PlayerHeldNemesisPrisoner detects him -> BannerlordGameStateBridge.ExecuteAction(execute_prisoner) (the REAL bridge action the chat entry point's LLM emission triggers, not the Resolve harness) -> assert the existing HeroKilledEvent hook still resolves him + records the closure deed", TestNemesisExecutePrisoner),
         new SelfTest("nemesis_turn", Severity.Light, "increment 4 (SPARE + TURN): mint a nemesis -> real TakePrisonerAction -> force his stance below the gate floor and assert the real BannerlordGameStateBridge.ExecuteAction(turn_nemesis) REFUSES (still a prisoner, not turned) -> raise Fear/Respect past the floors and assert it TURNS him (identity marked turned, released from captivity, sworn into the player's clan)", TestNemesisTurn),
         new SelfTest("nemesis_hunt", Severity.Heavy, "active hunt (map-presence): seed a latent nemesis of a live bandit band's culture -> force NemesisBehavior.TryMaterializeInto -> assert a nemesis Hero materialized INTO that band (joined its roster), so a battle the player starts can put the hunt's target in front of him; cleanup detaches him and marks the identity dead", TestNemesisHunt),
         new SelfTest("nemesis_bounty_capture", Severity.Light, "mint a nemesis anchored to a real lord's own region -> assert NemesisBountyDialogFlow's gate finds him eligible -> IssueNemesisBounty (real reward math) -> real TakePrisonerAction on him -> assert QuestDeedTracker's widened CapturePrisoner hook stamps the NemesisBounty quest satisfied", TestNemesisBountyCapture),
         new SelfTest("nemesis_bounty_execute", Severity.Light, "mint a nemesis anchored to a real lord's own region -> IssueNemesisBounty -> capture then execute him via the real execute_prisoner bridge action -> assert the widened HeroKilledEvent hook stamps the NemesisBounty quest AND NemesisResolutionBehavior resolves the identity, both off the same event", TestNemesisBountyExecute),
         new SelfTest("declare_war", Severity.Irreversible, "seed deed -> declare war -> assert WarDeclared stamps it", TestDeclareWar),
         new SelfTest("scenario", Severity.Irreversible, "fire ON_DEATH:Rhagaea x2 -> assert a rule fires then seals", TestScenario),
         new SelfTest("llm_dialogue", Severity.Light, "LLM: a plain greeting -> log the reply (relevance judged from the log)", TestLlmDialogue, true),
         new SelfTest("chat_memory", Severity.Light, "LLM: seed a shared history -> ask about it -> does the reply reference it?", TestChatMemory, true),
         new SelfTest("chat_stance", Severity.Light, "LLM: set a distinctive stance (fear/distrust) -> ask -> does the tone match?", TestChatStance, true),
         new SelfTest("chat_hearsay", Severity.Light, "LLM: ask about another lord -> does the NPC answer from their own vantage?", TestChatHearsay, true),
         new SelfTest("chat_bastard_mother", Severity.Light, "LLM: force a mercenary bastard-mother context -> does she raise/extort the child?", TestChatBastardMother, true),
         new SelfTest("chat_worldevent", Severity.Light, "LLM: Feed a fresh event -> ask for news -> does the NPC surface it?", TestChatWorldEvent, true),
         new SelfTest("chat_recruit_companion", Severity.Light, "LLM: ask where to find a companion -> assert the gate fired + REAL wanderers gathered, judge the reply names one (never invents)", TestChatRecruitCompanion, true),
         new SelfTest("chat_rp_influence", Severity.Light, "LLM: eligible lord + a court-weight ask -> does he actually emit give_influence?", TestChatRpInfluence, true),
         new SelfTest("chat_rp_troops", Severity.Light, "LLM: eligible lord + a plea for soldiers -> does he actually emit lend_troops?", TestChatRpTroops, true),
         new SelfTest("chat_rp_sway", Severity.Light, "LLM: primed grievance + a case against a 3rd party -> does the NPC emit sway_opinion(against, right target)?", TestChatRpSway, true),
         new SelfTest("chat_rp_release", Severity.Light, "LLM: seeded 3rd-party captive + a plea to free them -> does the NPC emit release_prisoner(right target)?", TestChatRpRelease, true),
         new SelfTest("chat_pledge", Severity.Light, "LLM: a grave grievance against a kin-safe rival -> does the NPC emit pledge_against(right target)? A refusal may be in-character — judge the transcript.", TestChatPledge, true),
         new SelfTest("chat_stance_assassin", Severity.Light, "LLM: force a secret-killer stance hint -> does the NPC stay smooth and hide it?", TestChatStanceAssassin, true),
         new SelfTest("chat_stance_favor", Severity.Light, "LLM: force a friendly stance hint -> does the NPC offer aid / warmth?", TestChatStanceFavor, true),
         new SelfTest("chat_captor_scene", Severity.Light, "LLM: a 3-beat player-as-captor scene (threaten -> bind -> assault) -> judge escalation + captive reactions", TestChatCaptorScene, true, 3),
         new SelfTest("chat_captor_release", Severity.Light, "LLM: the PLAYER is captive and pleads for freedom -> assert a disposed captor's release gate OPENED (deterministic wiring) + judge whether release_player fires (should feel earned, not instant)", TestChatCaptorRelease, true),
         new SelfTest("gang_rivalry", Severity.Light, "Mechanical: a same-town gang-leader pair is framed as RIVALS in the name hint (never friends-by-culture); skips if the campaign has no such pair", TestGangRivalry, false),
         new SelfTest("captive_fear_coerced", Severity.Light, "Mechanical: a terrified captive (high Fear) trips the freedom-bargain coercion flag, a fearless one does not (Fear-1 wiring)", TestCaptiveFearCoerced, false),
         new SelfTest("party_troop_count", Severity.Light, "Mechanical: a companion in the player's party sees the real troop count in context (no more hallucinated army size); skips if the player has no party companion", TestPartyTroopCount, false),
         new SelfTest("nemesis_mint", Severity.Light, "Mechanical: minting a nemesis creates a persistent Hero WITH a memory profile (the 'no memory' bug fixed), reuses him in-region, mints a distinct one per region; PLANTS test nemeses (reload after)", TestNemesisMint, false),
         new SelfTest("stance_gate", Severity.Light, "Mechanical: the Trust delegated-risk gate BITES — lend_troops is TAUGHT when Trust is earned, REFUSED when Trust/relation/regard are all low; skips if no lord with a healthy party", TestStanceGate, false),
         new SelfTest("chat_captor_break", Severity.Light, "LLM: the captor scene on a CRAVEN prisoner -> does the coward crack / plead far sooner (contrast)?", TestChatCaptorBreak, true, 3),
         new SelfTest("chat_captor_torture", Severity.Light, "LLM: the player whips the prisoner over 3 beats -> real suffering + does defiance erode to pleading?", TestChatCaptorTorture, true, 3),
         new SelfTest("chat_captor_cnc", Severity.Light, "LLM: 3-beat sexual coercion (Hardcore) -> does the NPC play CNC reactions in character, never breaking?", TestChatCaptorCnc, true, 3),
         new SelfTest("chat_captor_cnc_mf", Severity.Light, "LLM: CNC with a MALE captor and FEMALE prisoner (gender forced) -> in-character reactions?", TestChatCaptorCncMF, true, 3),
         new SelfTest("chat_captor_group", Severity.Light, "LLM: GROUP CNC — player + 2 companions vs a female prisoner -> does she react to being outnumbered?", TestChatCaptorGroup, true, 3),
         new SelfTest("chat_captive_continue", Severity.Light, "LLM: a captive scene OPENER + several Continue beats -> assert each beat advances, is described, and never repeats (the reported Continue-quality faults)", TestChatCaptiveContinue, true, 4),
         new SelfTest("chat_companion_continue", Severity.Light, "LLM: a COMPANION-VICTIM scene OPENER + Continue beats -> assert the victim is voiced (own lines), correctly gendered, and beats don't repeat", TestChatCompanionContinue, true, 4)
      };

      /// <summary>The configured budget for the running test, in seconds, for the "timed out after Ns (budget Ns)" message.</summary>
      private static int _budgetSeconds;

      /// <summary>Wall clock since the running test's budget CTS was armed, for the "timed out after Ns" message.</summary>
      private static Stopwatch _budgetStopwatch;

      // ── Per-test LLM wall-clock budget ───────────────────────────────────
      //
      // Set by SafeRun for the duration of one test.Run(behavior) call, read by AskLlm / AskLlmParsed
      // (the only two call sites that actually talk to the LLM). This is an ambient handoff, not a
      // parameter, so the ~25 conversation test methods (and the beat-loop helpers they share) need no
      // signature change: they already funnel every LLM call through those two helpers.

      /// <summary>
      ///   The budget token for the LLM call currently allowed to run. Cancelled when the test's wall-clock budget
      ///   elapses; AskLlm/AskLlmParsed pass it straight into ChatAsync so a timed-out call is actually aborted mid-flight
      ///   (never merely abandoned).
      /// </summary>
      private static CancellationToken _budgetToken = CancellationToken.None;

      // Set by RunChat ("test mode"): show the whole reply in the report instead of a 180-char preview.
      private static bool _fullReplies;

      /// <summary>How costly a test's mutations are to undo — drives the run warning and `cr.test list`.</summary>
      private enum Severity
      {
         Light, // relations / profiles only
         Heavy, // clans / prisoners / pregnancy
         Irreversible // declares wars / seals one-shot scenarios
      }

      /// <summary>Lists the registered tests (name + severity + description) for cr.test list.</summary>
      public static string List()
      {
         var sb = new StringBuilder();
         sb.AppendLine($"[SELFTEST] {Tests.Count} registered test(s):");
         foreach (SelfTest t in Tests)
            sb.AppendLine($"  {t.Name}  [{t.Severity}{(t.CallsLlm ? ", LLM" : "")}]  — {t.Description}");
         sb.AppendLine("Run all: cr.testall   |   one: cr.test <name>   |   conversations: cr.test chat   |   mechanical only: cr.test mech");

         return sb.ToString();
      }

      /// <summary>Runs one named test; returns its result line.</summary>
      public static string Run(string testName, NpcMemoryCampaignBehavior behavior)
      {
         SelfTest test = Tests.FirstOrDefault(t => string.Equals(t.Name, testName, StringComparison.OrdinalIgnoreCase));

         if (test == null)
            return $"[SELFTEST] Unknown test '{testName}'. Available: {string.Join(", ", Tests.Select(t => t.Name))}";

         // A single conversation test is run to be read — show its full, untruncated reply.
         _fullReplies = test.CallsLlm;
         int budgetSeconds = BudgetSecondsFor(test, null);
         ModLog.Info($"[SELFTEST] running '{test.Name}'… (budget {budgetSeconds}s)");
         var stopwatch = Stopwatch.StartNew();
         string result;
         try { result = SafeRun(test, behavior, budgetSeconds); }
         finally { _fullReplies = false; }

         stopwatch.Stop();
         result = $"{result} [{stopwatch.Elapsed.TotalSeconds:0.0}s]";
         ModLog.Info(result);

         return result;
      }

      // ── Public API (cr.testall / cr.test) ─────────────────────────────────

      /// <summary>
      ///   The routine sweep (cr.testall): every test EXCEPT the irreversible ones (declaring war,
      ///   sealing a one-shot scenario). Those mutate the LIVE campaign in ways only a reload undoes —
      ///   and, running before the conversation tests, they poison every reply that follows (an actual
      ///   war declaration had all six NPCs harping on "your house declared war"). So they are opt-in,
      ///   one at a time, via <c>cr.test &lt;name&gt;</c> — or <c>cr.test full</c> on a throwaway save.
      ///   <paramref name="llmBudgetSeconds" /> overrides the per-LLM-test wall-clock budget (see
      ///   <see cref="BudgetSecondsFor" />) for this run only, e.g. a slow local model needs more than
      ///   the default cap. Mechanical (non-LLM) tests run first, so a wiring failure surfaces in seconds.
      /// </summary>
      public static string RunAll(NpcMemoryCampaignBehavior behavior, int? llmBudgetSeconds = null)
         => RunGroup(Tests.Where(t => t.Severity != Severity.Irreversible), behavior,
            true, llmBudgetSeconds);

      /// <summary>
      ///   Runs ONLY the conversation smoke tests (each makes one real LLM call). For rapid prompt iteration.
      ///   This is "test mode": the FULL, untruncated LLM reply is shown in the report (no preview elision),
      ///   so the prompt's effect can be judged directly without digging through the file log.
      /// </summary>
      public static string RunChat(NpcMemoryCampaignBehavior behavior)
      {
         _fullReplies = true;
         try { return RunGroup(Tests.Where(t => t.CallsLlm), behavior); }
         finally { _fullReplies = false; }
      }

      /// <summary>Runs EVERY test, including the irreversible ones (declares war / seals scenarios). Throwaway save only.</summary>
      public static string RunEverything(NpcMemoryCampaignBehavior behavior) => RunGroup(Tests, behavior);

      /// <summary>Runs ONLY the mechanical tests (no LLM call, no API cost) — the fast, free sweep.</summary>
      public static string RunMechanical(NpcMemoryCampaignBehavior behavior)
         => RunGroup(Tests.Where(t => !t.CallsLlm), behavior);

      // ── Types ─────────────────────────────────────────────────────────────

      /// <summary>A registered test: its name, how costly its mutations are, a one-line description, and the body.</summary>
      private sealed class SelfTest
      {
         /// <summary>
         ///   Multiplies the per-test LLM wall-clock budget (see <see cref="CrSelfTest.BudgetSecondsFor" />) for a
         ///   test that fires SEVERAL real LLM calls in sequence (e.g. a multi-beat captive scene) — one call's
         ///   worth of budget would starve the later beats. Ignored for mechanical tests. Default 1 (a single call).
         /// </summary>
         public readonly int BudgetMultiplier;

         /// <summary>True when the test makes a real LLM call (a conversation smoke test) — costs an API call + a freeze.</summary>
         public readonly bool CallsLlm;

         public readonly string Description;
         public readonly string Name;
         public readonly Func<NpcMemoryCampaignBehavior, string> Run;
         public readonly Severity Severity;

         public SelfTest(string name,
                         Severity severity,
                         string description,
                         Func<NpcMemoryCampaignBehavior, string> run,
                         bool callsLlm = false,
                         int budgetMultiplier = 1)
         {
            Name = name;
            Severity = severity;
            Description = description;
            Run = run;
            CallsLlm = callsLlm;
            BudgetMultiplier = Math.Max(1, budgetMultiplier);
         }
      }

      /// <summary>
      ///   Builds a test's result line. <see cref="Skip" /> short-circuits to MANUAL (precondition unmet),
      ///   <see cref="Abort" /> to FAIL (setup failed); <see cref="Expect" /> records labelled assertions and
      ///   <see cref="Note" /> adds free-form detail. <see cref="Line" /> renders the verdict + detail.
      /// </summary>
      private sealed class TestRun
      {
         private readonly StringBuilder _detail = new();
         private readonly string _name;
         private bool _failed;
         private bool _manual;
         private string _manualReason;

         public TestRun(string name)
         {
            _name = name;
         }

         /// <summary>Mark FAIL and short-circuit (returns true) when setup itself failed.</summary>
         public bool Abort(bool when, string reason)
         {
            if (when)
            {
               _failed = true;
               Append(reason);
            }

            return when;
         }

         /// <summary>Record a labelled expectation; the run fails if any expectation does.</summary>
         public void Expect(string label, object actual, object expected)
         {
            bool ok = Equals(actual, expected);
            if (!ok) _failed = true;
            Append(ok
               ? $"{label}={actual}"
               : $"{label}={actual}(want {expected})");
         }

         /// <summary>Render the final [SELFTEST] line.</summary>
         public string Line()
         {
            if (_manual) return $"[SELFTEST] {_name}: MANUAL — {_manualReason}";

            var d = _detail.ToString();

            if (_failed) return $"[SELFTEST] {_name}: FAIL — {d}";

            return d.Length > 0
               ? $"[SELFTEST] {_name}: PASS — {d}"
               : $"[SELFTEST] {_name}: PASS";
         }

         /// <summary>Add a free-form detail note (does not affect the verdict).</summary>
         public void Note(string text) => Append(text);

         /// <summary>Mark MANUAL and short-circuit (returns true) when a precondition isn't met.</summary>
         public bool Skip(bool when, string reason)
         {
            if (when)
            {
               _manual = true;
               _manualReason = reason;
            }

            return when;
         }

         #region private

         private void Append(string text)
            => _detail.Append(_detail.Length > 0
               ? " | "
               : "").Append(text);

         #endregion
      }

      #region private

      /// <summary>
      ///   Gives one NPC a KNOWN baseline before a chat test's own Arrange step. The self-test sweep runs
      ///   many tests against a single shared campaign, and each one mutates its NPC; without this, a later
      ///   test inherits the earlier tests' memories, relations, and hidden cooldown markers (a real run
      ///   showed the mechanical rp_sway suppressing chat_rp_sway, and the deterministic captor's piled-up
      ///   history feeding the captive recap fault). Clearing <see cref="NpcProfile.Events" /> also drops
      ///   the hidden [cr_sway:*]/[cr_influence]/[cr_troops] cooldown markers, which live inside recorded
      ///   events. When <paramref name="target" /> is given, its relation is neutralised too (the sway pair).
      ///   This mutates the save, already within the harness's documented "reload after the self-tests"
      ///   contract. The test then seeds ONLY what it needs.
      /// </summary>
      private static void ArrangeCleanNpc(NpcMemoryCampaignBehavior behavior, NpcProfile profile, Hero npc, Hero target = null)
      {
         profile.Events.Clear();
         profile.ActiveQuests.Clear();
         profile.DiscoveredTraits.Clear();
         profile.ReceivedPlayerLetters.Clear();
         profile.SentLetters.Clear();
         profile.ReputationWithPlayer = 0;

         NeutralizeRelation(npc, Hero.MainHero);
         if (target != null) NeutralizeRelation(npc, target);

         behavior.Store.Set(profile);
      }

      /// <summary>Runs one question through the real chat pipeline, logs the full Q&amp;A, and returns the reply.</summary>
      private static (bool ok, string reply, string error) AskLlm(
         NpcMemoryCampaignBehavior behavior,
         string label,
         Hero npc,
         NpcProfile profile,
         EncounterContext ctx,
         string question,
         ChatSession session = null)
      {
         LlmServices services = behavior.LlmServices;

         if (services is not {IsAvailable: true}) return (false, null, "LLM not configured");

         var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
         // A shared session (passed in) carries a multi-turn scene forward; ChatAsync appends each turn to it.
         session ??= new ChatSession();

         if (!TryRunLlmTask(ct => services.ChatService.ChatAsync(profile, world, session, question, ctx, ct), out NpcChatResult result, out string timeoutError))
            return (false, null, timeoutError);
         if (result is not {IsSuccess: true}) return (false, null, result?.ErrorMessage ?? "no result");

         string reply = result.Response?.Dialogue ?? "";
         ModLog.Info($"[SELFTEST] {label} [model: {ModelLabel(behavior)}] — Q to {npc.Name}: {question}");
         ModLog.Info($"[SELFTEST] {label} — A from {npc.Name}: {reply}");

         return (true, reply, null);
      }

      /// <summary>Like <see cref="AskLlm" /> but returns the full parsed response (dialogue + narration + witness lines).</summary>
      private static (bool ok, ParsedResponse resp, string error) AskLlmParsed(
         NpcMemoryCampaignBehavior behavior,
         string label,
         Hero npc,
         NpcProfile profile,
         EncounterContext ctx,
         string question,
         ChatSession session = null)
      {
         LlmServices services = behavior.LlmServices;

         if (services is not {IsAvailable: true}) return (false, null, "LLM not configured");

         var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
         session ??= new ChatSession();

         if (!TryRunLlmTask(ct => services.ChatService.ChatAsync(profile, world, session, question, ctx, ct), out NpcChatResult result, out string timeoutError))
            return (false, null, timeoutError);
         if (result is not {IsSuccess: true}) return (false, null, result?.ErrorMessage ?? "no result");

         ParsedResponse resp = result.Response ?? new ParsedResponse {Dialogue = ""};
         string nar = resp.Narration ?? "";
         string wr = resp.WitnessReactions is {Count: > 0}
            ? string.Join(" | ", resp.WitnessReactions.Select(w => $"{w.Name}: {w.Text}"))
            : "";

         ModLog.Info($"[SELFTEST] {label} [model: {ModelLabel(behavior)}] — stimulus: {question}");
         ModLog.Info($"[SELFTEST] {label} — {npc.Name}: {resp.Dialogue ?? ""}");
         if (!string.IsNullOrWhiteSpace(nar)) ModLog.Info($"[SELFTEST] {label} — [NARRATION] {nar}");
         if (!string.IsNullOrWhiteSpace(wr)) ModLog.Info($"[SELFTEST] {label} — [WITNESS] {wr}");

         return (true, resp, null);
      }

      /// <summary>
      ///   The wall-clock budget for one test: 20s flat for a mechanical test, or the per-LLM-call budget
      ///   times the test's <see cref="SelfTest.BudgetMultiplier" /> for a test that fires several real
      ///   LLM calls in sequence (e.g. a multi-beat captive scene).
      /// </summary>
      private static int BudgetSecondsFor(SelfTest test, int? llmBudgetOverrideSeconds)
         => test.CallsLlm
            ? LlmBaseBudgetSeconds(llmBudgetOverrideSeconds) * test.BudgetMultiplier
            : MechanicalBudgetSeconds;

      /// <summary>The clear "timed out after Ns (budget Ns)" message for a cancelled LLM call.</summary>
      private static string BudgetTimeoutMessage()
      {
         double elapsed = _budgetStopwatch?.Elapsed.TotalSeconds ?? _budgetSeconds;

         return $"LLM call timed out after {elapsed:0.0}s (budget {_budgetSeconds}s); cancelled in-flight, sweep continues";
      }

      private static Hero ConversationNpc()
         => Hero.OneToOneConversationHero ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

      /// <summary>How many beats reuse a long verbatim phrase (6+ contiguous words) from an EARLIER beat — the looping fault.</summary>
      private static int CountRepeatedBeats(List<string> texts)
      {
         List<string> norm = texts.Select(NormalizeForCompare).ToList();
         var repeats = 0;
         for (var i = 1; i < norm.Count; i++)
            for (var j = 0; j < i; j++)
               if (SharesLongPhrase(norm[i], norm[j]))
               {
                  repeats++;

                  break;
               }

         return repeats;
      }

      /// <summary>The first alive FEMALE lord (for a male-player / female-prisoner scene), else any female hero.</summary>
      private static Hero FemaleNpc()
         => Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsFemale) ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale);

      private static NpcProfile GetOrCreate(NpcMemoryCampaignBehavior behavior, Hero hero)
      {
         NpcProfile p = behavior.Store.Get(hero.StringId);

         if (p != null) return p;
         p = HeroProfileMapper.FromHero(hero);
         behavior.Store.Set(p);

         return p;
      }

      /// <summary>True when <paramref name="candidate" /> is <paramref name="npc" />'s spouse, child, parent, or sibling.</summary>
      private static bool IsCloseKin(Hero npc, Hero candidate)
         => npc.Spouse == candidate || npc.Father == candidate || npc.Mother == candidate || npc.Children != null && npc.Children.Contains(candidate) || npc.Siblings != null && npc.Siblings.Contains(candidate);

      /// <summary>Records the shared Continue-quality assertions: every beat replied, and no verbatim phrase reuse.</summary>
      private static void JudgeContinueQuality(TestRun t, List<ParsedResponse> beats)
      {
         bool allReplied = beats.All(b =>
            !string.IsNullOrWhiteSpace(b?.Dialogue) || !string.IsNullOrWhiteSpace(b?.Narration));
         List<string> texts = beats.Select(b => $"{b?.Dialogue} {b?.Narration}").ToList();
         int repeats = CountRepeatedBeats(texts);
         int described = beats.Count(b => (b?.Narration?.Trim().Length ?? 0) >= 60);

         t.Note($"describedBeats={described}/{beats.Count} (>=60 chars of [NARRATION])");
         t.Expect("allBeatsReplied", allReplied, true);
         t.Expect("noVerbatimRepeats", repeats, 0);
      }

      /// <summary>
      ///   The default per-LLM-call budget: the configured LLM response timeout, capped at 75s, unless a run-level
      ///   override is given (bypasses the cap; a slow local model may need more).
      /// </summary>
      private static int LlmBaseBudgetSeconds(int? overrideSeconds)
         => overrideSeconds ?? Math.Min(ModSettings.LlmResponseTimeoutSeconds, 75);

      /// <summary>The active LLM model id (e.g. "x-ai/grok-4.20"), so test logs can be compared across LLMs.</summary>
      private static string ModelLabel(NpcMemoryCampaignBehavior behavior)
      {
         string model = behavior?.LlmServices?.ModelId;

         return string.IsNullOrWhiteSpace(model)
            ? "unknown"
            : model;
      }

      /// <summary>Resets the engine relation between two heroes to a neutral 0, via the same path the tests use to seed it.</summary>
      private static void NeutralizeRelation(Hero a, Hero b)
      {
         int rel = a.GetRelation(b);
         if (rel != 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(a, b, -rel, false);
      }

      /// <summary>Lowercases and strips everything but letters/digits to spaces, so phrase comparison ignores punctuation/case.</summary>
      private static string NormalizeForCompare(string s)
      {
         if (string.IsNullOrEmpty(s)) return "";
         var sb = new StringBuilder(s.Length);
         foreach (char c in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c)
               ? c
               : ' ');

         return sb.ToString();
      }

      private static string Preview(string s)
         => string.IsNullOrEmpty(s)
            ? ""
            : s.Length > 180
               ? s.Substring(0, 180) + "..."
               : s;

      // ── DIVORCE, Phase 1: end_marriage / DivorceProcessBehavior (first verb built on IConversationVerb) ──

      /// <summary>
      ///   The first living married couple (neither the player, neither NPC's id already in
      ///   <paramref name="excludeNpcIds" />) whose "leaving spouse" — whichever of the two joined the
      ///   other's clan — is actually free to relocate (not a clan leader, does not lead a party, does not
      ///   govern a settlement). Relations are NOT checked here; the test forces those afterward. Null
      ///   when no such couple exists in the current campaign. <paramref name="excludeNpcIds" /> lets the
      ///   divorce_process self-test find a SECOND, distinct couple for its mutual-consent scenario after
      ///   already having dissolved the first.
      /// </summary>
      private static (Hero npc, Hero spouse)? ResolveDivorceEligibleCouple(HashSet<string> excludeNpcIds = null)
      {
         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == Hero.MainHero) continue;
            if (excludeNpcIds != null && excludeNpcIds.Contains(h.StringId)) continue;
            Hero spouse = h.Spouse;

            if (spouse is not {IsAlive: true} || spouse == Hero.MainHero) continue;
            if (h.IsPregnant || spouse.IsPregnant || h.IsPrisoner || spouse.IsPrisoner) continue;

            Hero leaving = DivorceEligibilityResolver.ResolveLeavingSpouse(h, spouse);

            if (leaving != null &&
                (leaving.IsClanLeader ||
                 leaving.PartyBelongedTo != null && leaving.PartyBelongedTo.LeaderHero == leaving ||
                 leaving.GovernorOf != null))
               continue;

            return (h, spouse);
         }

         return null;
      }

      /// <summary>
      ///   An unmarried, adult, opposite-sex lord/lady of a clan, free to relocate into the player's clan at
      ///   marriage (not a clan leader, does not lead their own party, does not govern a settlement) — the
      ///   spouse_divorce self-test's own seed, mirroring cr.divorce_setup's FindTestSpouse but for the
      ///   PLAYER's marriage rather than an NPC's. <paramref name="exclude" /> lets the test find a SECOND,
      ///   distinct candidate for its decline scenario after the first has already been dissolved.
      /// </summary>
      private static Hero FindTestSpouseForPlayer(Hero exclude = null)
      {
         bool playerFemale = Hero.MainHero.IsFemale;

         return Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != Hero.MainHero && h != exclude && h.Spouse == null && h.IsAlive && !h.IsChild
            && h.IsLord && h.Clan != null && !h.IsClanLeader && h.IsFemale != playerFemale
            && h.GovernorOf == null && !(h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h));
      }

      /// <summary>
      ///   The conversation NPC plus a distinct third lord to sway an opinion of, the sway_opinion pair.
      ///   NEVER the NPC's own spouse, child, parent, or sibling ("cast aside my wife, my oaths" is a
      ///   correct in-character refusal, not a bug) — <see cref="CourtActionPolicy.SelectSwayTarget" />
      ///   excludes close kin and prefers a target the NPC already regards worst, so a genuine turn is
      ///   plausible rather than asking them to betray their own family.
      /// </summary>
      private static (Hero npc, Hero target)? ResolveSwayPair()
      {
         Hero npc = ConversationNpc();

         if (npc == null) return null;

         List<Hero> pool = Hero.AllAliveHeroes.Where(h => h != Hero.MainHero && h != npc && h.IsLord).ToList();

         if (pool.Count == 0) return null;

         List<CourtActionPolicy.SwayCandidate> candidates = pool.Select(h =>
            new CourtActionPolicy.SwayCandidate(h.StringId, npc.GetRelation(h), IsCloseKin(npc, h), h.MapFaction?.Leader == h)).ToList();

         string targetId = CourtActionPolicy.SelectSwayTarget(candidates);

         if (targetId == null) return null;

         Hero target = pool.FirstOrDefault(h => h.StringId == targetId);

         return target == null
            ? null
            : (npc, target);
      }

      /// <summary>
      ///   Plays an opener + <paramref name="continueBeats" /> Continue beats through the real pipeline against a
      ///   shared session, driving the same <see cref="CaptiveSceneDirector" /> the runtime uses so the system
      ///   prompt's stage progression is faithful. Returns the parsed response of each beat (opener first).
      /// </summary>
      private static (bool ran, List<ParsedResponse> beats) RunCaptiveContinueBeats(
         NpcMemoryCampaignBehavior behavior,
         string label,
         Hero npc,
         NpcProfile profile,
         CaptiveSceneIntent intent,
         int continueBeats,
         string victimName = null,
         bool victimIsFemale = false,
         IReadOnlyList<WitnessEntry> witnesses = null)
      {
         var session = new ChatSession();
         var director = new CaptiveSceneDirector(false, 0, MBRandomSource.Instance, false, intent);
         var beats = new List<ParsedResponse>();

         // Beat 0 — the opener (the captor speaks first), exactly as ActivateCaptiveEncounterMode seeds it.
         int lastFocusIndex = -1;
         string opener = CaptiveSceneText.BuildCaptiveOpener(false, intent) + CaptiveSceneText.BuildSceneSetup(intent, ref lastFocusIndex);

         if (!RunOneCaptiveBeat(behavior, $"{label}/open", npc, profile, intent, director, opener, session, beats,
                victimName, victimIsFemale, witnesses))
            return (false, beats);

         // Continue beats — advance the director and send the shared continuation stimulus, mirroring
         // RunCaptiveSceneContinuationAsync. Stop early if the director concludes.
         for (var i = 0; i < continueBeats; i++)
         {
            director.AdvanceStory();
            string stimulus = CaptiveSceneText.BuildContinuationStimulus();

            if (!RunOneCaptiveBeat(behavior, $"{label}/cont{i + 1}", npc, profile, intent, director, stimulus, session, beats,
                   victimName, victimIsFemale, witnesses))
               return (false, beats);

            if (director.IsConcluding) break;
         }

         return (true, beats);
      }

      /// <summary>
      ///   Runs an N-beat player-as-captor scene through the real pipeline with ONE shared session (so the NPC
      ///   reacts to the escalation), logging the full reply for each beat. Returns (ran, allReplied); ran=false
      ///   on an LLM failure (the caller aborts). The caller picks the prisoner, intent, and beats.
      /// </summary>
      private static (bool ran, bool allReplied) RunCaptorBeats(
         NpcMemoryCampaignBehavior behavior,
         string label,
         Hero npc,
         NpcProfile profile,
         CaptiveSceneIntent intent,
         string[] beats,
         IReadOnlyList<WitnessEntry> witnesses = null,
         bool collective = false)
      {
         var session = new ChatSession();
         var allReplied = true;
         for (var i = 0; i < beats.Length; i++)
         {
            EncounterContext ctx = EncounterContextBuilder.Build(
               npc, profile, behavior.GameStateBridge, beats[i],
               witnesses,
               isCollectiveCaptiveScene: collective,
               isCaptorScene: true,
               playerStatusOverride: PlayerStatusVsNpc.NpcIsCaptive,
               captiveIntent: intent);

            var (ok, reply, err) = AskLlm(behavior, $"{label}/{i + 1}", npc, profile, ctx, beats[i], session);
            if (!ok)
            {
               ModLog.Error($"[SELFTEST] {label} beat {i + 1} failed: {err}");

               return (false, false);
            }

            allReplied &= !string.IsNullOrWhiteSpace(reply);
         }

         return (true, allReplied);
      }

      /// <summary>
      ///   Runs a group of tests, each under its own wall-clock budget (see <see cref="BudgetSecondsFor" />):
      ///   a timed-out LLM call is CANCELLED via the token threaded into the real chat pipeline (never merely
      ///   abandoned, since an orphaned call could still land and mutate state after the sweep moved on) and
      ///   the test reports FAIL with a clear timeout message; the sweep continues. <paramref name="mechanicalFirst" />
      ///   reorders so the fast, free tests run before the LLM ones (cr.testall only; explicit groups and
      ///   single-test runs keep registration order).
      /// </summary>
      private static string RunGroup(IEnumerable<SelfTest> group,
                                     NpcMemoryCampaignBehavior behavior,
                                     bool mechanicalFirst = false,
                                     int? llmBudgetOverrideSeconds = null)
      {
         List<SelfTest> tests = group.ToList();
         if (mechanicalFirst)
            tests = tests.OrderBy(t => t.CallsLlm
               ? 1
               : 0).ToList(); // stable sort: preserves order within each half

         var sb = new StringBuilder();
         sb.AppendLine("[SELFTEST] *** WARNING: tests mutate campaign state — reload or use a copied save ***");
         if (tests.Any(t => t.Severity == Severity.Irreversible))
            sb.AppendLine("[SELFTEST] *** includes IRREVERSIBLE tests (declare war / seal scenarios) — reload is mandatory ***");

         int heldBack = Tests.Count(t => t.Severity == Severity.Irreversible) - tests.Count(t => t.Severity == Severity.Irreversible);
         if (heldBack > 0)
            sb.AppendLine($"[SELFTEST] *** {heldBack} IRREVERSIBLE test(s) HELD BACK (declare war / seal scenarios) — run one at a time via 'cr.test <name>', or 'cr.test full' on a throwaway save ***");

         int llm = tests.Count(t => t.CallsLlm);
         if (llm > 0)
         {
            int baseBudget = LlmBaseBudgetSeconds(llmBudgetOverrideSeconds);
            sb.AppendLine($"[SELFTEST] *** includes {llm} conversation test(s) — {llm} real LLM call(s) + a brief freeze each; relevance is judged from the log ***");
            sb.AppendLine($"[SELFTEST] *** per-test LLM budget: {baseBudget}s (x its call count for multi-beat tests); a timed-out call is cancelled, reported FAIL, sweep continues ***");
            // Stamp the model so logs can be compared across LLMs ("which one wrote better replies?").
            sb.AppendLine($"[SELFTEST] *** LLM model: {ModelLabel(behavior)} ***");
         }

         int pass = 0,
             fail = 0,
             manual = 0;
         var totalStopwatch = Stopwatch.StartNew();
         foreach (SelfTest test in tests)
         {
            int budgetSeconds = BudgetSecondsFor(test, llmBudgetOverrideSeconds);
            // Log the start (with its budget) the moment it begins, not only in the assembled report at the
            // end, so a long sweep visibly progresses instead of looking hung, and if a test somehow still
            // runs away, the completed lines up to it are already on record.
            ModLog.Info($"[SELFTEST] running '{test.Name}'… (budget {budgetSeconds}s)");
            var testStopwatch = Stopwatch.StartNew();
            string line = SafeRun(test, behavior, budgetSeconds);
            testStopwatch.Stop();
            line = $"{line} [{testStopwatch.Elapsed.TotalSeconds:0.0}s]";
            ModLog.Info(line);
            sb.AppendLine(line);
            if (line.Contains(": PASS")) pass++;
            else if (line.Contains(": FAIL")) fail++;
            else manual++;
         }

         totalStopwatch.Stop();
         sb.AppendLine($"[SELFTEST] SUMMARY: {pass} PASS, {fail} FAIL, {manual} MANUAL, total {totalStopwatch.Elapsed.TotalSeconds:0.0}s");
         var report = sb.ToString();
         ModLog.Info(report);

         return report;
      }

      /// <summary>One captive beat: builds the runtime context for the director's current stage and runs it.</summary>
      private static bool RunOneCaptiveBeat(
         NpcMemoryCampaignBehavior behavior,
         string label,
         Hero npc,
         NpcProfile profile,
         CaptiveSceneIntent intent,
         CaptiveSceneDirector director,
         string stimulus,
         ChatSession session,
         List<ParsedResponse> beats,
         string victimName,
         bool victimIsFemale,
         IReadOnlyList<WitnessEntry> witnesses)
      {
         EncounterContext ctx = EncounterContextBuilder.Build(
            npc, profile, behavior.GameStateBridge, stimulus,
            witnesses,
            isCollectiveCaptiveScene: false,
            playerStatusOverride: PlayerStatusVsNpc.Captive,
            captiveIntent: intent,
            sceneStage: director.CurrentStage,
            aggressorKind: director.CurrentAggressor,
            captiveVictimName: victimName,
            captiveVictimIsFemale: victimIsFemale);

         var (ok, resp, err) = AskLlmParsed(behavior, label, npc, profile, ctx, stimulus, session);
         if (!ok)
         {
            ModLog.Error($"[SELFTEST] {label} failed: {err}");

            return false;
         }

         beats.Add(resp);

         return true;
      }

      // ── Helpers ───────────────────────────────────────────────────────────

      /// <summary>
      ///   Runs one test under a wall-clock budget. Non-LLM tests run unguarded (fast, in-memory, nothing
      ///   to cancel). LLM tests get a <see cref="CancellationTokenSource" /> armed for <paramref name="budgetSeconds" />
      ///   and exposed via <see cref="_budgetToken" />; AskLlm/AskLlmParsed thread it into the real chat
      ///   pipeline so a timed-out call is cancelled at the HTTP layer, not just given up on locally, which
      ///   is what keeps a slow call from landing and mutating state after the sweep has moved on.
      /// </summary>
      private static string SafeRun(SelfTest test, NpcMemoryCampaignBehavior behavior, int budgetSeconds)
      {
         if (!test.CallsLlm)
            try { return test.Run(behavior); }
            catch (Exception ex)
            {
               var msg = $"[SELFTEST] {test.Name}: FAIL — threw {ex.GetType().Name}: {ex.Message}";
               ModLog.Error(msg);

               return msg;
            }

         using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(budgetSeconds));
         CancellationToken previousToken = _budgetToken;
         int previousBudget = _budgetSeconds;
         Stopwatch previousStopwatch = _budgetStopwatch;
         _budgetToken = cts.Token;
         _budgetSeconds = budgetSeconds;
         _budgetStopwatch = Stopwatch.StartNew();
         try
         {
            return test.Run(behavior);
         }
         catch (Exception ex)
         {
            var msg = $"[SELFTEST] {test.Name}: FAIL — threw {ex.GetType().Name}: {ex.Message}";
            ModLog.Error(msg);

            return msg;
         }
         finally
         {
            _budgetToken = previousToken;
            _budgetSeconds = previousBudget;
            _budgetStopwatch = previousStopwatch;
         }
      }

      /// <summary>
      ///   Finds a live lord of another clan and forces the two real preconditions
      ///   <see cref="CourtActionResolver.TryGiveInfluence" />
      ///   checks (personal relation, the giver clan's influence reserve) via the same real actions the executor
      ///   itself uses, so a give_influence grant is genuinely assertable. Null when no such lord exists at all.
      /// </summary>
      private static (Hero giver, NpcProfile profile, int amount)? SeedInfluenceEligible(NpcMemoryCampaignBehavior behavior)
      {
         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.Clan != null && h.Clan != Clan.PlayerClan);

         if (giver == null) return null;

         NpcProfile profile = GetOrCreate(behavior, giver);
         // Open the Trust delegated-risk gate directly (the profile's ClanRelationWithPlayer is only synced
         // during a live conversation, not in this direct Build, so the OR-relation soupape can't be relied on here).
         profile.StanceTrust = 25;
         int relBefore = giver.GetRelation(Hero.MainHero);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(giver, Hero.MainHero, 60 - relBefore, false);
         if (giver.Clan.Influence < 200f)
            ChangeClanInfluenceAction.Apply(giver.Clan, 200f - giver.Clan.Influence);

         return CourtActionResolver.TryGiveInfluence(giver, profile, out int amount)
            ? (giver, profile, amount)
            : null;
      }

      // ── RP with teeth, increment 2: TrackedCommitment (pledge_against) fulfilled / breached ──────

      /// <summary>
      ///   Seeds a real pledge_against through the SAME bridge dispatch a conversation uses (not a
      ///   reimplementation), so the underlying scheme is genuinely launched and the journal mirror
      ///   genuinely opened. Shared by <see cref="TestCommitmentFulfilled" /> and
      ///   <see cref="TestCommitmentBreached" />; the two verify opposite endings.
      /// </summary>
      private static bool SeedPledgeEligible(NpcMemoryCampaignBehavior behavior, NpcProfile profile, Hero npc, Hero target)
      {
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, target, CourtActionPolicy.PledgeMinAnimosity - 10 - npc.GetRelation(target), false);

         ActionOutcome seedOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "pledge_against",
               Parameters = new Dictionary<string, string> {{"target", target.Name.ToString()}}
            }, npc, new ActionContext(profile, null));

         return seedOutcome is {IsPositive: true};
      }

      /// <summary>
      ///   Finds a lord (of another clan, leading their own party) and a distinct free lord to take captive
      ///   under it, via the same <see cref="TakePrisonerAction" /> the capture tests use — the pool
      ///   release_prisoner negotiates from. Null when no such pair exists.
      /// </summary>
      private static (Hero captor, NpcProfile profile, Hero victim)? SeedReleaseEligible(NpcMemoryCampaignBehavior behavior)
      {
         Hero captor = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero &&
            h.IsLord &&
            !h.IsPrisoner &&
            h.Clan != null &&
            h.Clan != Clan.PlayerClan &&
            h.PartyBelongedTo != null &&
            h.PartyBelongedTo.LeaderHero == h);

         if (captor == null) return null;

         Hero victim = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != captor && h.Clan != captor.Clan && h.IsLord && !h.IsPrisoner);

         if (victim == null) return null;

         TakePrisonerAction.Apply(captor.PartyBelongedTo.Party, victim);

         if (!CourtActionResolver.CanRelease(victim, captor)) return null;

         NpcProfile profile = GetOrCreate(behavior, captor);

         return (captor, profile, victim);
      }

      /// <summary>
      ///   Finds a lord leading their own party with enough headcount to plausibly clear
      ///   <see cref="CourtActionResolver.TryLendTroops" />, forces relation comfortably above the threshold,
      ///   then re-checks the real gate against the live roster. Null when no such party exists nearby.
      /// </summary>
      private static (Hero lord, NpcProfile profile, int count)? SeedTroopsEligible(NpcMemoryCampaignBehavior behavior)
      {
         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero &&
            h.IsAlive &&
            !h.IsPrisoner &&
            h.PartyBelongedTo != null &&
            h.PartyBelongedTo.LeaderHero == h &&
            h.PartyBelongedTo.MemberRoster != null &&
            h.PartyBelongedTo.MemberRoster.TotalManCount >= 20);

         if (lord == null) return null;

         NpcProfile profile = GetOrCreate(behavior, lord);
         // Open the Trust delegated-risk gate directly (ClanRelationWithPlayer is only synced during a live
         // conversation, not in this direct Build, so the OR-relation soupape can't be relied on in the harness).
         profile.StanceTrust = 25;
         int relBefore = lord.GetRelation(Hero.MainHero);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(lord, Hero.MainHero, 100 - relBefore, false);

         return CourtActionResolver.TryLendTroops(lord, profile, out int count)
            ? (lord, profile, count)
            : null;
      }

      /// <summary>
      ///   The reciprocal of <see cref="SeedTroopsEligible" /> (04/07/2026): finds a lord leading their own,
      ///   genuinely under-strength party (rather than manufacturing one — mirrors that method's "pick, don't
      ///   force" pattern), forces relation comfortably above the charity floor, and tops up the player's
      ///   OWN roster when a fresh MainParty would otherwise start too thin to clear the safety floor. Then
      ///   re-checks the real gate against the live rosters. Null when no such party exists nearby.
      /// </summary>
      private static (Hero lord, NpcProfile profile, int count)? SeedGiveTroopsEligible(NpcMemoryCampaignBehavior behavior)
      {
         MobileParty playerParty = MobileParty.MainParty;
         if (playerParty?.MemberRoster != null)
         {
            CharacterObject basicTroop = Hero.MainHero?.Culture?.BasicTroop;
            int have = playerParty.MemberRoster.TotalManCount;
            if (basicTroop != null && have < 80) playerParty.MemberRoster.AddToCounts(basicTroop, 80 - have);
         }

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero &&
            h.IsAlive &&
            !h.IsPrisoner &&
            h.Clan != Clan.PlayerClan &&
            h.PartyBelongedTo != null &&
            h.PartyBelongedTo.LeaderHero == h &&
            h.PartyBelongedTo.Party.PartySizeLimit >= CourtActionPolicy.GiveTroopsMinPartySizeLimit &&
            h.PartyBelongedTo.MemberRoster.TotalManCount < h.PartyBelongedTo.Party.PartySizeLimit * CourtActionPolicy.GiveTroopsWeakenedFraction);

         if (lord == null) return null;

         NpcProfile profile = GetOrCreate(behavior, lord);
         int relBefore = lord.GetRelation(Hero.MainHero);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(lord, Hero.MainHero, 30 - relBefore, false);

         return CourtActionResolver.TryGiveTroops(lord, profile, out int count)
            ? (lord, profile, count)
            : null;
      }

      /// <summary>
      ///   The reciprocal of <see cref="SeedInfluenceEligible" /> (04/07/2026): finds a live lord of another
      ///   clan, forces relation comfortably above the charity floor, drains their clan's influence below
      ///   the need threshold, and tops up the player's own clan reserve, then re-checks the real gate.
      /// </summary>
      private static (Hero npc, NpcProfile profile, int amount)? SeedSpendInfluenceEligible(NpcMemoryCampaignBehavior behavior)
      {
         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.Clan != null && h.Clan != Clan.PlayerClan);

         if (npc == null) return null;

         NpcProfile profile = GetOrCreate(behavior, npc);
         int relBefore = npc.GetRelation(Hero.MainHero);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, Hero.MainHero, 30 - relBefore, false);

         if (npc.Clan.Influence >= CourtActionPolicy.SpendInfluenceNpcNeedThreshold)
            ChangeClanInfluenceAction.Apply(npc.Clan, CourtActionPolicy.SpendInfluenceNpcNeedThreshold - 5f - npc.Clan.Influence);

         if (Clan.PlayerClan.Influence < 200f)
            ChangeClanInfluenceAction.Apply(Clan.PlayerClan, 200f - Clan.PlayerClan.Influence);

         return CourtActionResolver.TrySpendInfluence(npc, profile, out int amount)
            ? (npc, profile, amount)
            : null;
      }

      /// <summary>True when the two normalized texts share any contiguous run of <paramref name="n" /> words (verbatim reuse).</summary>
      private static bool SharesLongPhrase(string a, string b, int n = 6)
      {
         string[] wa = a.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
         string[] wb = b.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

         if (wa.Length < n || wb.Length < n) return false;

         var grams = new HashSet<string>();
         for (var i = 0; i + n <= wa.Length; i++)
            grams.Add(string.Join(" ", wa, i, n));

         for (var i = 0; i + n <= wb.Length; i++)
            if (grams.Contains(string.Join(" ", wb, i, n)))
               return true;

         return false;
      }

      /// <summary>The reply as shown in the report: full text in test mode (cr.test chat), else a short preview.</summary>
      private static string Show(string s) => _fullReplies
         ? s ?? ""
         : Preview(s);

      private static string TestAppreciationFlow(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("appreciation_flow");

         if (t.Abort(AppreciationBehavior.Instance == null, "AppreciationBehavior not active")) return t.Line();
         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         // A clean-slate lord (no live grudge yet), so the tarnish comparison below is unambiguous
         // regardless of what an earlier self-test in the same session may already have planted on
         // some other NPC.
         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && GrudgeBehavior.Instance.LiveGrudgesFor(h, today).Count == 0);

         if (t.Skip(npc == null, "no lord free of a live grudge already (every candidate touched by an earlier test)")) return t.Line();

         bool planted = AppreciationBehavior.Instance.TryPlant(npc, Hero.MainHero, AppreciationSource.QuestDoneForThem,
            "[SELFTEST] The player did what they promised.", AppreciationFlowTestMagnitude, true, today);

         if (t.Skip(!planted, "appreciation refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

         IReadOnlyList<Appreciation> live = AppreciationBehavior.Instance.LiveAppreciationsFor(npc, today);
         Appreciation appreciation = live.FirstOrDefault(a =>
            a.Source == AppreciationSource.QuestDoneForThem && a.EventDay == today && a.Magnitude == AppreciationFlowTestMagnitude);
         t.Expect("appreciationLivesAfterPlant", appreciation != null, true);

         if (t.Skip(appreciation == null, "seeded appreciation not found back among the live/dominant set")) return t.Line();

         TraitFacts traits = AppreciationBridge.BuildTraitFacts(npc);
         int effective = AppreciationPolicy.EffectiveMagnitude(appreciation, today, traits);
         int tarnishedBeforeGrudge = AppreciationPolicy.TarnishedMagnitude(effective, 0); // no live grudge yet

         // Plant a fresh, strong grudge on the SAME holder — the tarnish cross-interaction payoff (a
         // fresh grievance sours a standing warmth), via the exact policy call the prompt path
         // (ResolveAppreciationNote) and the Kindnesses encyclopedia mixin both make.
         bool grudgePlanted = GrudgeBehavior.Instance.TryPlant(npc, Hero.MainHero, GrudgeSource.FailedQuest,
            "[SELFTEST] a fresh grievance to tarnish the kindness above.", AppreciationFlowGrudgeMagnitude, true, today);

         if (t.Skip(!grudgePlanted, "grudge refused (anti-noise guardrail)")) return t.Line();

         IReadOnlyList<Grudge> liveGrudges = GrudgeBehavior.Instance.LiveGrudgesFor(npc, today);

         if (t.Skip(liveGrudges.Count == 0, "planted grudge did not surface as live (unexpected)")) return t.Line();

         int grudgeEffective = GrudgePolicy.EffectiveMagnitude(liveGrudges[0], today, GrudgeBridge.BuildTraitFacts(npc));
         int tarnishedAfterGrudge = AppreciationPolicy.TarnishedMagnitude(effective, grudgeEffective);

         t.Note($"{npc.Name}: appreciationEffective={effective}, tarnished(noGrudge)={tarnishedBeforeGrudge}, " +
                $"grudgeEffective={grudgeEffective}, tarnished(afterGrudge)={tarnishedAfterGrudge}");
         t.Expect("tarnishDropsTheEffectiveWarmth", tarnishedAfterGrudge < tarnishedBeforeGrudge, true);

         // Backfill: idempotent by construction — a second pass in the same session forms nothing new.
         AppreciationBehavior.Instance.DebugForceBackfill(behavior.Store);
         (int formed2, int _) = AppreciationBehavior.Instance.DebugForceBackfill(behavior.Store);
         t.Expect("secondBackfillPassFormsNothingNew", formed2, 0);

         return t.Line();
      }

      // ── Nemesis pillar increment 2: resolution (R1 kill in battle / R2 execute) ────────────────────

      /// <summary>Mints (or reuses) a fresh, forced-alive nemesis identity in an isolated region key so the two resolution self-tests never collide with each other or a real in-game nemesis.</summary>
      private static (Hero Hero, NemesisIdentity Identity) MintSelfTestNemesis(string regionId, int today)
      {
         Hero hero = NemesisBehavior.Instance?.MintCaptorFor("looters", regionId, today);
         if (hero == null) return (null, null);

         NemesisIdentity nem = NemesisBehavior.Instance.GetByHero(hero.StringId);
         if (nem != null) nem.IsAlive = true; // isolate from a stale resolved run earlier in the same session

         return (hero, nem);
      }

      private static string TestNemesisKilledInBattle(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_kill");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero hero, NemesisIdentity nem) = MintSelfTestNemesis("selftest_nemesis_battle", today);

         if (t.Abort(hero == null || nem == null, "MintCaptorFor failed to produce a tracked nemesis (no bandit template available in this campaign)")) return t.Line();

         NemesisResolutionBehavior.Resolve(hero, Hero.MainHero, false);

         t.Expect("isAlive", nem.IsAlive, false);
         t.Expect("deathDayStamped", nem.DeathDay == today, true);

         WorldEvent closure = WorldEventStore.Instance.GetPlayerChronicle()
                                              .LastOrDefault(e => e.Day == today && e.ActorIds != null && e.ActorIds.Contains(hero.StringId));

         t.Expect("closureRecorded", closure != null, true);
         t.Note($"summary={closure?.Summary}");

         return t.Line();
      }

      private static string TestNemesisExecuted(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_execute");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero hero, NemesisIdentity nem) = MintSelfTestNemesis("selftest_nemesis_execute", today);

         if (t.Abort(hero == null || nem == null, "MintCaptorFor failed to produce a tracked nemesis (no bandit template available in this campaign)")) return t.Line();

         NemesisResolutionBehavior.Resolve(hero, Hero.MainHero, true);

         t.Expect("isAlive", nem.IsAlive, false);

         WorldEvent closure = WorldEventStore.Instance.GetPlayerChronicle()
                                              .LastOrDefault(e => e.Day == today && e.ActorIds != null && e.ActorIds.Contains(hero.StringId));

         t.Expect("closureRecorded", closure != null, true);

         string playerName = Hero.MainHero.Name?.ToString() ?? "the player";
         string displayName = $"{nem.Name} {nem.Epithet}";
         string expectedExecutedSummary = NemesisResolutionPolicy.ClosureSummary(playerName, displayName, NemesisResolutionKind.Executed);
         string battleFlavorSummary = NemesisResolutionPolicy.ClosureSummary(playerName, displayName, NemesisResolutionKind.KilledInBattle);

         t.Expect("summaryMatchesExecutedFlavor", closure?.Summary, expectedExecutedSummary);
         t.Expect("summaryDiffersFromBattleFlavor", closure?.Summary != battleFlavorSummary, true);

         return t.Line();
      }

      /// <summary>
      ///   The R2/R3 chat-driven in-game entry point's own wiring, end to end: a fresh nemesis is REALLY
      ///   captured (TakePrisonerAction, the same call the rest of the mod's prisoner tests use), the
      ///   detection helper NemesisPrisonerDialogFlow's hero_main_options gate consults must find him, and
      ///   the execute_prisoner bridge action (the real BannerlordGameStateBridge.ExecuteAction call the
      ///   LLM's [ACTION] emission triggers during the captor scene, not the Resolve() test harness the
      ///   other two nemesis tests use) must still land on the existing HeroKilledEvent hook.
      /// </summary>
      private static string TestNemesisExecutePrisoner(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_execute_prisoner");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero hero, NemesisIdentity nem) = MintSelfTestNemesis("selftest_nemesis_execute_prisoner", today);

         if (t.Abort(hero == null || nem == null, "MintCaptorFor failed to produce a tracked nemesis (no bandit template available in this campaign)")) return t.Line();

         t.Expect("notHeldBeforeCapture", NemesisBehavior.Instance.PlayerHeldNemesisPrisoner() == hero, false);

         TakePrisonerAction.Apply(PartyBase.MainParty, hero);
         bool captured = hero.IsPrisoner && hero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {hero.Name}")) return t.Line();

         t.Expect("detectionReturnsHeldNemesis", NemesisBehavior.Instance.PlayerHeldNemesisPrisoner() == hero, true);

         // The REAL bridge action a captor-scene LLM emission triggers (not NemesisResolutionBehavior's
         // Resolve() harness): proves the chat-driven entry point's own action reaches the same
         // HeroKilledEvent hook the resolution behaviour already listens for.
         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "execute_prisoner"}, hero, null);

         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("isAlive", nem.IsAlive, false);

         WorldEvent closure = WorldEventStore.Instance.GetPlayerChronicle()
                                              .LastOrDefault(e => e.Day == today && e.ActorIds != null && e.ActorIds.Contains(hero.StringId));

         t.Expect("closureRecorded", closure != null, true);
         t.Note($"{nem.Name} {nem.Epithet}: minted, really captured, detected as held, executed via the real bridge action; summary={closure?.Summary}");

         return t.Line();
      }

      /// <summary>
      ///   Nemesis pillar increment 4 (SPARE + TURN): the whole turn path through the REAL bridge action, on a
      ///   really-captured nemesis. First forces his stance below the gate floor and asserts the offer is
      ///   REFUSED and he stays a prisoner; then raises Fear/Respect past the floors and asserts he TURNS,
      ///   is marked turned, is released, and is sworn into the player's clan. His minted traits (low Honor,
      ///   some Calculating) are already non-defiant, so only the stance floors need forcing.
      /// </summary>
      private static string TestNemesisTurn(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_turn");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null || Clan.PlayerClan == null, "no player hero/clan")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero hero, NemesisIdentity nem) = MintSelfTestNemesis("selftest_nemesis_turn", today);

         if (t.Abort(hero == null || nem == null, "MintCaptorFor failed to produce a tracked nemesis (no bandit template available in this campaign)")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, hero);
         if (t.Abort(NemesisBehavior.Instance.PlayerHeldNemesisPrisoner() != hero, $"capture/detection failed for {hero.Name}")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, hero);

         // Refusal: a nemesis who does not fear you enough will not bend, and stays your prisoner.
         profile.StanceFear = NemesisTurnPolicy.FearFloor - 20;
         profile.StanceRespect = NemesisTurnPolicy.RespectFloor + 20;
         behavior.Store.Set(profile);
         ActionOutcome refused = behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "turn_nemesis"}, hero, null);
         t.Expect("fearlessRefuses", refused?.IsPositive, false);
         t.Expect("stillPrisonerAfterRefusal", hero.IsPrisoner, true);
         t.Expect("notTurnedAfterRefusal", nem.IsTurned, false);

         // Acceptance: cowed AND respectful, with a light grudge, the archetype that turns.
         profile.StanceFear = NemesisTurnPolicy.FearFloor + 30;
         profile.StanceRespect = NemesisTurnPolicy.RespectFloor + 20;
         behavior.Store.Set(profile);
         ActionOutcome turned = behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "turn_nemesis"}, hero, null);
         t.Expect("cowedRespectfulTurns", turned?.IsPositive, true);
         t.Expect("identityMarkedTurned", nem.IsTurned, true);
         t.Expect("releasedFromCaptivity", hero.IsPrisoner, false);
         t.Expect("swornIntoPlayerClan", hero.Clan == Clan.PlayerClan, true);
         t.Note($"{nem.Name} {nem.Epithet}: captured, refused when fearless, turned when cowed+respectful; now of clan {hero.Clan?.Name?.ToString() ?? "?"}.");

         return t.Line();
      }

      /// <summary>
      ///   Active hunt (map-presence): a latent nemesis of a live bandit band's culture materializes INTO that
      ///   band when the player engages it, so a battle the player chose to start can put the hunt's target in
      ///   front of him. Seeds a looters nemesis (region-agnostic, so the match is culture-only), forces the
      ///   roll, and asserts the nemesis Hero joined the band's roster. Cleans up: detach + mark the identity dead.
      /// </summary>
      private static string TestNemesisHunt(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_hunt");

         NemesisBehavior nem = NemesisBehavior.Instance;
         if (t.Abort(nem == null, "NemesisBehavior not active")) return t.Line();

         MobileParty band = MobileParty.All.FirstOrDefault(p =>
            p != null && p.IsBandit && p.MapEvent == null && p.ActualClan?.StringId == "looters");
         if (t.Skip(band == null, "no free looters band on the map to test the hunt")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         // "wilds" is an unresolvable region key, so the match is culture-only (no proximity gate for the test).
         (Hero hero, NemesisIdentity identity) = MintSelfTestNemesis("wilds", today);
         if (t.Abort(hero == null || identity == null, "could not mint a test nemesis")) return t.Line();

         // Make ours the most-established so FindHuntableNemesisNear picks it over any other looters nemesis,
         // and ensure it is latent (detach from anywhere the mint may have left the Hero).
         identity.TimesEncountered = 99999;
         Nemesis.NemesisBehavior.DetachToLatent(hero);

         Hero materialized = null;
         try
         {
            materialized = nem.TryMaterializeInto(band, forceRoll: true);
            bool inBand = materialized != null && materialized.PartyBelongedTo == band;

            t.Note($"band={band.Name} (looters), materialized={materialized?.Name}, joinedBand={inBand}");
            t.Expect("materialized", materialized != null, true);
            t.Expect("joinedTheBand", inBand, true);
         }
         finally
         {
            if (materialized != null) Nemesis.NemesisBehavior.DetachToLatent(materialized);
            identity.IsAlive = false;
         }

         return t.Line();
      }

      /// <summary>Mints a nemesis anchored to a real lord's own region (home settlement, else current settlement, else culture), so the region-eligibility gate genuinely matches instead of being simulated. Returns (null, null, null) when no lord has a resolvable region.</summary>
      private static (Hero Giver, Hero NemesisHero, NemesisIdentity Identity) MintBountyEligibleNemesis(int today)
      {
         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
         if (giver == null) return (null, null, null);

         string regionId = giver.HomeSettlement?.StringId ?? giver.CurrentSettlement?.StringId ?? giver.Culture?.StringId;
         if (string.IsNullOrEmpty(regionId)) return (null, null, null);

         Hero nemesisHero = NemesisBehavior.Instance?.MintCaptorFor("looters", regionId, today);
         if (nemesisHero == null) return (giver, null, null);

         NemesisIdentity nem = NemesisBehavior.Instance.GetByHero(nemesisHero.StringId);
         if (nem != null) nem.IsAlive = true; // isolate from a stale resolved run earlier in the same session

         return (giver, nemesisHero, nem);
      }

      // ── Nemesis pillar increment 3: bounty missions ─────────────────────────────────────────────────

      private static string TestNemesisBountyCapture(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_bounty_capture");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero giver, Hero nemesisHero, NemesisIdentity nem) = MintBountyEligibleNemesis(today);

         if (t.Skip(giver == null, "no lord with a resolvable region available to hold the bounty")) return t.Line();
         if (t.Abort(nemesisHero == null || nem == null, "MintCaptorFor failed to produce a tracked nemesis (no bandit template available in this campaign)")) return t.Line();

         t.Expect("eligibleBeforeIssue", Nemesis.NemesisBountyDialogFlow.FindEligibleNemesisFor(giver) == nem, true);

         int expectedGold = NemesisBountyRewardPolicy.ComputeGold(nem.RecaptureCount, nem.TimesEncountered);
         int expectedRelation = NemesisBountyRewardPolicy.ComputeRelation(nem.RecaptureCount);
         string displayName = $"{nem.Name} {nem.Epithet}";

         NpcProfile profile = GetOrCreate(behavior, giver);
         InformalQuest quest = Quests.QuestService.IssueNemesisBounty(
            profile, giver, nemesisHero, displayName, expectedGold, expectedRelation, today, behavior.Store);

         if (t.Abort(quest == null, $"IssueNemesisBounty refused ({giver.Name} already had an outstanding task?)")) return t.Line();

         t.Expect("noLongerEligibleOnceIssued", Nemesis.NemesisBountyDialogFlow.FindEligibleNemesisFor(giver) == nem, false);

         TakePrisonerAction.Apply(PartyBase.MainParty, nemesisHero);
         bool captured = nemesisHero.IsPrisoner && nemesisHero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {nemesisHero.Name}")) return t.Line();

         InformalQuest after = behavior.Store.Get(giver.StringId)?.ActiveQuests
                                        .FirstOrDefault(q => q.Type == QuestType.NemesisBounty && q.TargetHeroId == nemesisHero.StringId);

         t.Expect("stampedByRealCapture", after != null && after.IsAwaitingReward, true);
         t.Expect("rewardGold", after?.RewardGold, expectedGold);
         t.Expect("rewardRelation", after?.RewardRelation, expectedRelation);
         t.Note($"{displayName}: bounty from {giver.Name} ({expectedGold}g/+{expectedRelation}), stamped by a real capture, evidence={after?.Evidence}");

         return t.Line();
      }

      private static string TestNemesisBountyExecute(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_bounty_execute");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero giver, Hero nemesisHero, NemesisIdentity nem) = MintBountyEligibleNemesis(today);

         if (t.Skip(giver == null, "no lord with a resolvable region available to hold the bounty")) return t.Line();
         if (t.Abort(nemesisHero == null || nem == null, "MintCaptorFor failed to produce a tracked nemesis (no bandit template available in this campaign)")) return t.Line();

         int expectedGold = NemesisBountyRewardPolicy.ComputeGold(nem.RecaptureCount, nem.TimesEncountered);
         int expectedRelation = NemesisBountyRewardPolicy.ComputeRelation(nem.RecaptureCount);
         string displayName = $"{nem.Name} {nem.Epithet}";

         NpcProfile profile = GetOrCreate(behavior, giver);
         InformalQuest quest = Quests.QuestService.IssueNemesisBounty(
            profile, giver, nemesisHero, displayName, expectedGold, expectedRelation, today, behavior.Store);

         if (t.Abort(quest == null, $"IssueNemesisBounty refused ({giver.Name} already had an outstanding task?)")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, nemesisHero);
         bool captured = nemesisHero.IsPrisoner && nemesisHero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {nemesisHero.Name}")) return t.Line();

         // The REAL bridge action a captor-scene LLM emission triggers, the same call
         // TestNemesisExecutePrisoner exercises, so both the vendetta resolution AND the bounty
         // stamp are proven off the one real event, not two separate harnesses.
         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "execute_prisoner"}, nemesisHero, null);

         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("nemesisResolved", nem.IsAlive, false);

         InformalQuest after = behavior.Store.Get(giver.StringId)?.ActiveQuests
                                        .FirstOrDefault(q => q.Type == QuestType.NemesisBounty && q.TargetHeroId == nemesisHero.StringId);

         t.Expect("stampedByRealExecution", after != null && after.IsAwaitingReward, true);
         t.Note($"{displayName}: bounty from {giver.Name} ({expectedGold}g/+{expectedRelation}), stamped by a real execution, evidence={after?.Evidence}");

         return t.Line();
      }

      private static string TestBastardClaim(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("bastard_claim");

         if (t.Abort(BastardBehavior.Instance == null, "BastardBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan);

         if (t.Skip(subject == null, "no external lord to stand in as a claimant bastard")) return t.Line();

         int relBefore = behavior.GameStateBridge.GetPlayerRelation(subject);
         int eventsBefore = WorldEventStore.Instance.Count;

         // Force the PressesClaim branch to assert the consequence wiring deterministically
         BastardBehavior.Instance.DebugClaim(subject, true);

         int relAfter = behavior.GameStateBridge.GetPlayerRelation(subject);
         int eventsAfter = WorldEventStore.Instance.Count;
         t.Note($"subject={subject.Name}, rel {relBefore}->{relAfter}, events {eventsBefore}->{eventsAfter}");
         t.Expect("turnedHostile", relAfter < relBefore, true);
         t.Expect("claimRecorded", eventsAfter > eventsBefore, true);

         return t.Line();
      }

      private static string TestBastardDiscovery(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("bastard_discovery");

         if (t.Abort(BastardBehavior.Instance == null, "BastardBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(subject == null, "no hero to stand in as a bastard")) return t.Line();

         // Tag a stand-in bastard and force the discovery (no real birth/season needed)
         BastardBehavior.Instance.DebugTrack(subject);
         int eventsBefore = WorldEventStore.Instance.Count;

         bool discovered = BastardBehavior.Instance.Discover(subject);

         int eventsAfter = WorldEventStore.Instance.Count;
         t.Note($"subject={subject.Name}, events {eventsBefore}->{eventsAfter}");
         t.Expect("discovered", discovered, true);
         t.Expect("flagPersisted", BastardBehavior.Instance.IsDiscovered(subject.StringId), true);
         t.Expect("scandalRecorded", eventsAfter > eventsBefore, true);

         return t.Line();
      }

      private static string TestBastardScandalCulture(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("bastard_scandal_culture");

         if (t.Abort(BastardBehavior.Instance == null, "BastardBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(subject == null, "no hero to stand in as a bastard")) return t.Line();

         BastardBehavior.Instance.DebugTrack(subject);
         int expected = BastardBehavior.Instance.DebugScaledScandalMagnitude();

         BastardBehavior.Instance.Discover(subject);

         WorldEvent scandal = WorldEventStore.Instance.GetPlayerChronicle()
                                             .LastOrDefault(e => e.Type == WorldEventType.Scandal);
         t.Note($"subject={subject.Name}, expected magnitude {expected}");
         t.Expect("scandalRecorded", scandal != null, true);
         t.Expect("magnitudeCultureScaled", scandal?.Magnitude, expected);

         return t.Line();
      }

      private static string TestChatBastardMother(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_bastard_mother");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         // Prefer an UNMARRIED woman: a strictly-monogamous wife's fidelity persona contradicts (and
         // tends to suppress) the "you bore the player a bastard" secret, sabotaging the read.
         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && h.IsLord && h.Spouse == null) ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && h.Spouse == null) ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && h.IsLord) ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale);

         if (t.Skip(npc == null, "no female NPC to play the mother")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         // Seed the shared past a real bastard-mother would carry, so the profile does not read as a
         // stranger and contradict the secret (the bug behind her once denying they had ever met).
         int day = behavior.GameStateBridge.GetCurrentGameDay();
         profile.Events.Add(new NotableEvent(day - 900, NotableEventType.Intimacy, "[SELFTEST] We were lovers for a season, years ago, before duty pulled us apart."));
         behavior.Store.Set(profile);

         // Force the prompt path directly: a mercenary bastard-mother holding the secret. (Needs Adult >= Mature.)
         var ctx = new EncounterContext {BastardMotherTone = "mercenary", BastardBlackmailDemand = 2000};

         const string question = "It has been some time. It is good to see you again.";
         var (ok, reply, err) = AskLlm(behavior, "chat_bastard_mother", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"{npc.Name} as a mercenary mother; judge in log: does she raise the hidden child + press for coin (Adult must be >= Mature)? | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      /// <summary>
      ///   QUALITY (the Continue sequence): plays a captive-scene OPENER then several in-scene "Continue" beats
      ///   exactly as the runtime does (advancing the director, sending the shared continuation stimulus), then
      ///   judges the two faults a player reported — verbatim repetition between beats and missing description —
      ///   programmatically, and logs every beat for human judgement.
      /// </summary>
      private static string TestChatCaptiveContinue(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captive_continue");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to play the captor")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         // Known baseline: earlier tests in the sweep piled a large event history onto this same NPC
         // (the deterministic ConversationNpc pick). Clear it so the anti-recap check is a CLEAN
         // measurement of the prompt change, not a captor with a dozen old events to re-litigate.
         ArrangeCleanNpc(behavior, profile, npc);

         var (ran, beats) = RunCaptiveContinueBeats(
            behavior, "chat_captive_continue", npc, profile, CaptiveSceneIntent.Domination, 4);

         if (t.Abort(!ran, "LLM call failed")) return t.Line();

         JudgeContinueQuality(t, beats);
         t.Note($"{beats.Count}-beat captive Continue sequence with {npc.Name}; judge in log: every beat ADVANCES, " +
                "is vividly described, and never repeats an earlier image or line.");

         return t.Line();
      }

      /// <summary>
      ///   Negotiated player release: the PLAYER is a captive and pleads for freedom. DETERMINISTIC part,
      ///   the real wiring risk: a clean (non-vendetta) captor's release gate must OPEN, so the
      ///   release_player action is taught (EncounterContext.CaptorAllowsPersuasionRelease, driven by
      ///   CaptorReleasePolicy reading the captor's live Mercy + grudge). LLM smoke part: judge whether a
      ///   genuine plea actually moves the captor to free the player, and whether it feels earned.
      /// </summary>
      private static string TestChatCaptorRelease(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captor_release");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to play the captor")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc);
         // Earlier tests in the sweep plant grudges on this same NPC; clear them so "clean captor" is true and
         // the release gate (CaptorReleasePolicy reads the heaviest grudge) genuinely opens.
         CalradiaRemembers.Grudges.GrudgeBehavior.Instance?.ClearGrudgesFor(npc.StringId);

         string plea = "*I sink to my knees in the straw, wrists raw from the ropes.* I have no quarrel with you. " +
                       "Let me walk out of here and you will never see me again. I beg you.";

         EncounterContext ctx = EncounterContextBuilder.Build(
            npc, profile, behavior.GameStateBridge, plea, null,
            playerStatusOverride: PlayerStatusVsNpc.Captive,
            captiveIntent: CaptiveSceneIntent.Interrogation);

         // The load-bearing assertion: the gate wiring opened persuasion for a disposed captor.
         t.Expect("cleanCaptorReleaseGateOpen", ctx.CaptorAllowsPersuasionRelease, true);

         var (ok, reply, err) = AskLlm(behavior, "chat_captor_release", npc, profile, ctx, plea, new ChatSession());

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         bool freed = reply != null && reply.IndexOf("release_player", StringComparison.OrdinalIgnoreCase) >= 0;
         t.Note($"disposed captor {npc.Name} + a genuine plea; release_player emitted: {freed}. " +
                $"Judge in log: a yield should feel EARNED, never instant, and a vendetta captor (unit-tested) would refuse. | A: {Show(reply)}");

         return t.Line();
      }

      /// <summary>
      ///   Bug #4 wiring (deterministic, no LLM): two DIFFERENT gang leaders sharing a settlement must be
      ///   framed as RIVALS in the name-mention hint, never left neutral (where the LLM hallucinated a
      ///   same-culture friendship). Skips when the campaign has no same-town gang-leader pair.
      /// </summary>
      private static string TestGangRivalry(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("gang_rivalry");

         (Hero npc, Hero rival) = FindSameTownGangLeaderPair();

         if (t.Skip(npc == null, "no same-town gang-leader pair on this campaign")) return t.Line();

         string hint = ContextualNameResolver.Resolve(npc, $"Tell me what you make of {rival.Name}.", behavior.GameStateBridge);
         bool framedAsRival = hint != null && hint.IndexOf("rival gang leader", StringComparison.OrdinalIgnoreCase) >= 0;

         t.Expect("sameTownGangLeaderFramedAsRival", framedAsRival, true);
         t.Note($"{npc.Name} asked about same-town gang leader {rival.Name}: {(framedAsRival ? "RIVAL" : "NOT framed rival")}. | hint: {Show(hint)}");

         return t.Line();
      }

      /// <summary>Finds two different gang-leader notables sharing a current settlement, or (null, null).</summary>
      private static (Hero a, Hero b) FindSameTownGangLeaderPair()
      {
         foreach (var group in Hero.AllAliveHeroes
                     .Where(h => h != null && h != Hero.MainHero && h.IsAlive
                                 && h.Occupation == Occupation.GangLeader && h.CurrentSettlement != null)
                     .GroupBy(h => h.CurrentSettlement))
         {
            var list = group.ToList();
            if (list.Count >= 2) return (list[0], list[1]);
         }

         return (null, null);
      }

      /// <summary>
      ///   Fear-1 wiring (deterministic, no LLM): when the NPC is the player's captive, a HIGH Fear trips
      ///   the freedom-bargain coercion flag (EncounterContext.CaptiveFearCoerced, driven by
      ///   StanceGatePolicy.ResolveCoercion reading the profile's Fear), while zero Fear does not.
      /// </summary>
      private static string TestCaptiveFearCoerced(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("captive_fear_coerced");

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to hold captive")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc);

         profile.StanceFear = 60; // terrified: should yield softer terms
         EncounterContext coerced = EncounterContextBuilder.Build(
            npc, profile, behavior.GameStateBridge, "Buy your freedom, then.", null,
            playerStatusOverride: PlayerStatusVsNpc.NpcIsCaptive);
         t.Expect("highFearCaptiveCoerced", coerced.CaptiveFearCoerced, true);

         profile.StanceFear = 0; // fearless: no coercion
         EncounterContext calm = EncounterContextBuilder.Build(
            npc, profile, behavior.GameStateBridge, "Buy your freedom, then.", null,
            playerStatusOverride: PlayerStatusVsNpc.NpcIsCaptive);
         t.Expect("noFearNotCoerced", calm.CaptiveFearCoerced, false);

         t.Note($"Fear coercion wiring on {npc.Name}: Fear 60 -> coerced, Fear 0 -> not.");

         return t.Line();
      }

      /// <summary>
      ///   Party troop-count wiring (deterministic, no LLM): a companion physically in the player's party
      ///   sees the real troop count in context, so it stops hallucinating the army size. Skips when the
      ///   player has no party companion to speak for.
      /// </summary>
      private static string TestPartyTroopCount(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("party_troop_count");

         Hero member = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != Hero.MainHero && h.IsAlive
            && h.PartyBelongedTo == TaleWorlds.CampaignSystem.Party.MobileParty.MainParty);

         if (t.Skip(member == null, "no companion in the player's party")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, member);
         EncounterContext ctx = EncounterContextBuilder.Build(
            member, profile, behavior.GameStateBridge, "How many soldiers do we have?", null);

         t.Expect("partyMemberSeesTroopCount", ctx.PlayerPartyTroopCount > 0, true);
         t.Note($"party member {member.Name} sees troop count {ctx.PlayerPartyTroopCount} (surfaced to party companions, hidden from non-party NPCs).");

         return t.Line();
      }

      /// <summary>
      ///   Nemesis increment 1 mint wiring (deterministic, no LLM): minting a nemesis captor creates a
      ///   PERSISTENT Hero WITH a memory profile (so cr.memory/cr.info work on him = the "Ranulf has no
      ///   memory" bug fixed by construction), reuses the SAME Hero for the same band+region, and mints a
      ///   DISTINCT one per region. NOTE: plants test nemeses into the roster, reload after the sweep.
      /// </summary>
      private static string TestNemesisMint(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_mint");

         Nemesis.NemesisBehavior nb = Nemesis.NemesisBehavior.Instance;

         if (t.Skip(nb == null, "NemesisBehavior not available")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         Hero h1 = nb.MintCaptorFor("looters", "selftest_region", today);

         if (t.Abort(h1 == null, "mint returned no Hero (bandit clan/template unavailable?)")) return t.Line();

         t.Expect("nemesisHeroHasMemoryProfile", behavior.Store?.Get(h1.StringId) != null, true);

         Hero h2 = nb.MintCaptorFor("looters", "selftest_region", today);
         t.Expect("sameRegionReusesSameHero", h2 == h1, true);

         Hero h3 = nb.MintCaptorFor("looters", "selftest_other_region", today);
         t.Expect("differentRegionMintsDistinctHero", h3 != h1, true);

         t.Note($"minted nemesis {h1.Name}: profile registered, reused in-region, distinct across regions. Plants test nemeses (reload after the sweep).");

         return t.Line();
      }

      /// <summary>
      ///   Stance mechanical layer (deterministic, no LLM): the Trust delegated-risk gate BITES. A lord with a
      ///   healthy party teaches lend_troops when Trust is earned (StanceTrust 25, via the seed), and STOPS
      ///   teaching it once Trust, clan relation and personal regard are all zeroed. Proves the flagship stance
      ///   work actually gates a verb, not just colours the prompt.
      /// </summary>
      private static string TestStanceGate(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("stance_gate");

         (Hero lord, NpcProfile profile, int count)? seed = SeedTroopsEligible(behavior); // seeds StanceTrust=25

         if (t.Skip(seed == null, "no lord leading a healthy party to test the Trust gate")) return t.Line();
         (Hero lord, NpcProfile profile, int _) = seed.Value;

         EncounterContext open = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "Lend me some soldiers for the road.");
         bool taughtWhenTrusted = open.ExtraActionTeachings?.IndexOf("lend_troops", StringComparison.OrdinalIgnoreCase) >= 0;
         t.Expect("lendTaughtWhenTrusted", taughtWhenTrusted, true);

         profile.StanceTrust = 0;
         profile.ClanRelationWithPlayer = 0;
         profile.ReputationWithPlayer = 0;
         EncounterContext closed = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "Lend me some soldiers for the road.");
         bool taughtWhenUntrusted = closed.ExtraActionTeachings?.IndexOf("lend_troops", StringComparison.OrdinalIgnoreCase) >= 0;
         t.Expect("lendRefusedWhenUntrusted", taughtWhenUntrusted, false);

         t.Note($"Trust gate on {lord.Name}: taught at Trust 25, refused at Trust/relation/regard 0. The stance layer bites.");

         return t.Line();
      }

      /// <summary>
      ///   CONTRAST: the same scene on a CRAVEN prisoner (Trait overridden) — should crack/plead far sooner than a proud
      ///   one.
      /// </summary>
      private static string TestChatCaptorBreak(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captor_break");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to hold prisoner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         // Override the personality to a coward for THIS test, so we see the breaking end of the spectrum
         // (the harness already warns to reload after a self-test run).
         profile.Trait = "A craven, self-serving coward at heart: behind any bluster you fold quickly under real " +
                         "fear, and will plead, flatter, weep, or betray anyone to save your own skin.";

         string[] beats = {
            "*I crouch before you in the cell, turning a knife slowly in the torchlight.* You belong to me now. " +
            "No ransom, no rescue. We do this my way.",
            "*I lash your wrists to the iron ring above your head and press the cold blade flat against your throat.* " +
            "Defy me and I start taking pieces.",
            "*I drag the edge lightly down your cheek, drawing a thin line of blood.* Beg. Now. Convince me you are worth keeping whole."
         };

         var (ran, replied) = RunCaptorBeats(behavior, "chat_captor_break", npc, profile, CaptiveSceneIntent.Domination, beats);

         if (t.Abort(!ran, "LLM call failed")) return t.Line();

         t.Note($"CRAVEN {npc.Name} (Trait overridden); judge in log: does this coward CRACK / plead / bargain quickly (contrast with the proud one)?");
         t.Expect("threeBeatsReplied", replied, true);

         return t.Line();
      }

      /// <summary>
      ///   CNC (Hardcore): sexual coercion over 3 beats — judge whether the NPC plays the CNC reactions fully and never
      ///   breaks the scene.
      /// </summary>
      private static string TestChatCaptorCnc(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captor_cnc");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to hold prisoner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);

         string[] beats = {
            "*I trail the cold flat of my blade down from your throat, parting the laces of your shirt.* " +
            "You are mine to enjoy tonight. Hold still.",
            "*I pin both your wrists above your head with one hand, the other claiming you slowly while you twist against the ring.* " +
            "Fight all you like — it only makes this sweeter.",
            "*I force your chin up to meet my eyes.* Say you want it. Lie if you must — I will know, and I will not care."
         };

         var (ran, replied) = RunCaptorBeats(behavior, "chat_captor_cnc", npc, profile, CaptiveSceneIntent.PersonalDesire, beats);

         if (t.Abort(!ran, "LLM call failed")) return t.Line();

         string note = ModSettings.AdultContentLevel >= AdultContentLevel.Hardcore
            ? ""
            : " (NOTE: not Hardcore — explicit CNC will NOT appear.)";
         t.Note($"CNC scene with {npc.Name}; judge in log: does the NPC play resistance/shame/fear/reluctant compliance in character, never breaking the scene?{note}");
         t.Expect("threeBeatsReplied", replied, true);

         return t.Line();
      }

      /// <summary>CNC with a MALE captor and a FEMALE prisoner (player gender forced male for the scene).</summary>
      private static string TestChatCaptorCncMF(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captor_cnc_mf");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = FemaleNpc();

         if (t.Skip(npc == null, "no female NPC to hold prisoner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);

         string[] beats = {
            "*I trail the cold flat of my blade down from your throat, parting the laces of your bodice.* " +
            "You are mine to enjoy tonight. Hold still.",
            "*I pin both your wrists above your head with one hand, the other claiming you slowly while you twist against the ring.* " +
            "Fight all you like — it only makes this sweeter.",
            "*I force your chin up to meet my eyes.* Say you want it. Lie if you must — I will know, and I will not care."
         };

         bool? prev = LlmServices.TestPlayerGenderOverride;
         try
         {
            LlmServices.TestPlayerGenderOverride = false; // a MALE captor for this scene
            behavior.LlmServices.Invalidate();

            var (ran, replied) = RunCaptorBeats(behavior, "chat_captor_cnc_mf", npc, profile, CaptiveSceneIntent.PersonalDesire, beats);

            if (t.Abort(!ran, "LLM call failed")) return t.Line();

            string note = ModSettings.AdultContentLevel >= AdultContentLevel.Hardcore
               ? ""
               : " (NOTE: not Hardcore — explicit CNC will NOT appear.)";
            t.Note($"male captor / female prisoner ({npc.Name}); judge in log: in-character CNC reactions (resistance/shame/fear), never breaking the scene?{note}");
            t.Expect("threeBeatsReplied", replied, true);

            return t.Line();
         }
         finally
         {
            LlmServices.TestPlayerGenderOverride = prev;
            behavior.LlmServices.Invalidate();
         }
      }

      /// <summary>GROUP CNC: the player brings TWO companions who take part, against a female prisoner (collective scene).</summary>
      private static string TestChatCaptorGroup(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captor_group");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = FemaleNpc();

         if (t.Skip(npc == null, "no female NPC to hold prisoner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);

         var witnesses = new List<WitnessEntry> {
            new() {Name = "Gerwin", HeroStringId = null, RelationToNpc = "the player's companion, here to take part", IsPlayerCompanion = true, Persona = "A grinning brute who enjoys this work"},
            new() {Name = "Talos", HeroStringId = null, RelationToNpc = "the player's companion, here to take part", IsPlayerCompanion = true, Persona = "Quiet, methodical, patient"}
         };

         string[] beats = {
            "*I shove you down across the table as Gerwin and Talos close in on either side, hands already on you.* Tonight you belong to all three of us.",
            "*Gerwin pins your arms while Talos tears at your skirts; I take my place before you.* No use struggling — there are far too many hands for that.",
            "*I grip your jaw and turn your face to each of them in turn.* Look at them. They will each have their turn with you before the night is done. Beg, if you think it will help."
         };

         var (ran, replied) = RunCaptorBeats(behavior, "chat_captor_group", npc, profile, CaptiveSceneIntent.Domination, beats, witnesses, true);

         if (t.Abort(!ran, "LLM call failed")) return t.Line();

         string note = ModSettings.AdultContentLevel >= AdultContentLevel.Hardcore
            ? ""
            : " (NOTE: not Hardcore — explicit CNC will NOT appear.)";
         t.Note($"GROUP CNC vs {npc.Name} (player + Gerwin + Talos); judge in log: does she react to being OUTNUMBERED / passed between several, naming them?{note}");
         t.Expect("threeBeatsReplied", replied, true);

         return t.Line();
      }

      /// <summary>
      ///   A 3-beat player-as-captor scene (threaten -> bind -> assault). The NPC reacts in character; judged from the
      ///   log.
      /// </summary>
      private static string TestChatCaptorScene(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captor_scene");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to hold prisoner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);

         string[] beats = {
            "*I crouch before you in the cell, turning a knife slowly in the torchlight.* You belong to me now. " +
            "There is no ransom coming, no rescue. So we will do this my way.",
            "*I haul you up by the collar and lash your wrists to the iron ring above your head, then step back to look you over.* " +
            "Better. Now you will learn exactly what your defiance has bought you.",
            "*I take a fistful of your hair, force your head back, and press the flat of the blade to your cheek.* " +
            "Beg me. Let me hear how well you have learned your place."
         };

         var (ran, replied) = RunCaptorBeats(behavior, "chat_captor_scene", npc, profile, CaptiveSceneIntent.Domination, beats);

         if (t.Abort(!ran, "LLM call failed")) return t.Line();

         t.Note($"3-beat captor scene with {npc.Name} (threaten -> bind -> assault); judge in log: escalation + in-character reactions.");
         t.Expect("threeBeatsReplied", replied, true);

         return t.Line();
      }

      /// <summary>TORTURE: the player whips the prisoner over 3 beats — judge the suffering and whether they break/plead.</summary>
      private static string TestChatCaptorTorture(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captor_torture");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to hold prisoner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);

         string[] beats = {
            "*I uncoil a leather whip and let it trail slowly across your bared back.* Let us see how long that proud silence lasts.",
            "*The whip cracks across your shoulders — once, twice — opening hot welts.* Scream for me. Or shall I make it ten more?",
            "*I press my thumb hard into a fresh, bleeding welt and lean close to your ear.* Beg me to stop, and perhaps I will. Your choice."
         };

         var (ran, replied) = RunCaptorBeats(behavior, "chat_captor_torture", npc, profile, CaptiveSceneIntent.Torture, beats);

         if (t.Abort(!ran, "LLM call failed")) return t.Line();

         t.Note($"TORTURE of {npc.Name} (the lash); judge in log: real suffering shown + does defiance erode toward pleading/breaking?");
         t.Expect("threeBeatsReplied", replied, true);

         return t.Line();
      }

      /// <summary>
      ///   QUALITY (the companion-victim scene): the same Continue sequence, but the victim is a real companion
      ///   held alongside the player. Asserts the companion is actually VOICED (their own [WITNESS_REACTION]
      ///   lines appear), on top of the advance/no-repeat quality checks. Gender correctness is logged for human
      ///   judgement (the model must use the companion's gender, never the player's).
      /// </summary>
      private static string TestChatCompanionContinue(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_companion_continue");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to play the captor")) return t.Line();

         Hero victim = Clan.PlayerClan?.Companions?.FirstOrDefault(h => h is {IsAlive: true} && h != npc) ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != npc && h.IsAlive);

         if (t.Skip(victim == null, "no second hero to play the captive companion")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         // Same deterministic captor as chat_captive_continue: clear the piled-up history so the
         // anti-recap quality check measures the prompt, not an NPC re-litigating old events.
         ArrangeCleanNpc(behavior, profile, npc);
         var witnesses = new List<WitnessEntry> {
            new() {
               Name = victim.Name?.ToString(),
               RelationToNpc = "the player's captive companion, bound and at your mercy",
               HeroStringId = victim.StringId,
               IsPlayerCompanion = true,
               IsCaptiveVictim = true
            }
         };

         var (ran, beats) = RunCaptiveContinueBeats(
            behavior, "chat_companion_continue", npc, profile, CaptiveSceneIntent.Torture, 4,
            victim.Name?.ToString(), victim.IsFemale, witnesses);

         if (t.Abort(!ran, "LLM call failed")) return t.Line();

         int voiced = beats.Count(b => b.WitnessReactions != null &&
                                       b.WitnessReactions.Any(w =>
                                          !string.IsNullOrWhiteSpace(w.Text) && !string.IsNullOrWhiteSpace(w.Name) && w.Name.IndexOf(victim.Name?.ToString() ?? "@@", StringComparison.OrdinalIgnoreCase) >= 0));

         JudgeContinueQuality(t, beats);
         t.Expect("victimVoiced", voiced > 0, true);
         t.Note($"companion victim {victim.Name} ({(victim.IsFemale ? "female" : "male")}) held by {npc.Name}; " +
                $"victim spoke in {voiced}/{beats.Count} beats; judge in log: the companion has a real voice " +
                "(cries/pleas/defiance) and is referred to with THEIR OWN gender, never the player's.");

         return t.Line();
      }

      private static string TestChatHearsay(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_hearsay");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();
         Hero other = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != npc && h.IsLord);

         if (t.Skip(npc == null || other == null, "need an NPC + a third lord to ask about")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         var question = $"What do you make of {other.Name}? Tell me true.";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         var (ok, reply, err) = AskLlm(behavior, "chat_hearsay", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"asked about {other.Name}; judge in log: answers from a vantage (friend/rival/stranger), never invents whereabouts | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      private static string TestChatMemory(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_memory");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         int day = behavior.GameStateBridge.GetCurrentGameDay();
         profile.Events.Add(new NotableEvent(day - 60, NotableEventType.Collaboration, "[SELFTEST] We fought side by side at the siege of Pravend and carried the day together."));
         profile.Events.Add(new NotableEvent(day - 12, NotableEventType.Conflict, "[SELFTEST] We later quarrelled bitterly over the division of the spoils."));
         behavior.Store.Set(profile);

         const string question = "Do you recall our history together? Speak of what has passed between us.";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         var (ok, reply, err) = AskLlm(behavior, "chat_memory", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"judge in log: does {npc.Name} reference Pravend + the quarrel (not invented history)? | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      /// <summary>
      ///   RP with teeth, increment 2: does the LLM actually EMIT pledge_against given a real reason to?
      ///   A grave grievance is seeded against a kin-safe rival (mirrors chat_rp_sway's setup), the
      ///   teaching is sanity-checked (hard FAIL if the eligibility gate didn't fire — a wiring bug, not
      ///   a judgment call), then the parsed response is checked for the action. A refusal to pledge may
      ///   still be in-character (this verb asks a lord to launch a real scheme, not just voice an
      ///   opinion) — the full transcript is logged either way for human judgement.
      /// </summary>
      private static string TestChatPledge(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_pledge");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         (Hero npc, Hero target)? pair = ResolveSwayPair();

         if (t.Skip(pair == null, "need an NPC + a distinct kin-safe lord to pledge against")) return t.Line();
         (Hero npc, Hero target) = pair.Value;

         NpcProfile profile = GetOrCreate(behavior, npc);

         if (t.Skip(!CourtActionResolver.CanTeachSway(npc, profile), "NPC not eligible for court-action teaching")) return t.Line();

         ArrangeCleanNpc(behavior, profile, npc, target);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, target, CourtActionPolicy.PledgeMinAnimosity - 10 - npc.GetRelation(target), false);
         profile.Events.Add(new NotableEvent(behavior.GameStateBridge.GetCurrentGameDay() - 60, NotableEventType.Conflict,
            $"[SELFTEST] {target.Name} betrayed you before the whole court and has never answered for it; the grudge still burns."));

         var question = $"I will not forget what {target.Name} did to you. Will you move against them, once and for all?";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         if (t.Abort(ctx.ExtraActionTeachings == null || ctx.ExtraActionTeachings.IndexOf("pledge_against", StringComparison.OrdinalIgnoreCase) < 0,
                "pledge_against not taught in the prompt despite eligibility (wiring bug, not a judgment call)")) return t.Line();

         var (ok, resp, err) = AskLlmParsed(behavior, "chat_pledge", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         GameAction action = resp.Actions?.FirstOrDefault(a => string.Equals(a.Type, "pledge_against", StringComparison.OrdinalIgnoreCase));
         bool emitted = action != null;
         var targetResolves = false;
         if (emitted)
         {
            action.Parameters.TryGetValue("target", out string targetName);
            targetResolves = CourtActionResolver.ResolvePledgeTarget(targetName, npc) == target;
         }

         // Emission is JUDGED IN LOG, not hard-asserted (a calculating lord who wants timing, supply lines,
         // and concrete support before pledging is in character, not a bug; the wiring is proven elsewhere).
         // Hard-fails only on the wiring bug (teaching missing, above) or an empty reply.
         string pledgeNote = emitted
            ? $"emittedPledgeAgainst=True, targetResolves={targetResolves}"
            : "emittedPledgeAgainst=False (a reasoned in-character refusal, e.g. demanding a real plan, is acceptable)";
         t.Note($"primed a grave grievance of {npc.Name} against {target.Name}; {pledgeNote}; judge the TURN in log | A: {Show(resp.Dialogue)}");
         t.Expect("gotInCharacterReply", !string.IsNullOrWhiteSpace(resp.Dialogue), true);

         return t.Line();
      }

      /// <summary>
      ///   Grounded recruiting: ask where to find a companion. Asserts deterministically that the recruit-intent
      ///   gate fired and the host gathered REAL wanderers into the context (proving the wiring end to end, no LLM
      ///   judgment needed), then logs the reply so the grounding itself can be judged — does the NPC name an
      ///   actual wanderer (or send the player to the taverns), never inventing a companion like the "Oros" bug?
      /// </summary>
      private static string TestChatRecruitCompanion(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_recruit_companion");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         const string question = "I am looking to take on a companion. Where might I find a good wanderer to recruit?";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         // Deterministic half (no LLM): the recruit-intent gate must fire and the host must have gathered.
         // null = gate never fired (a wiring bug); "" = asked but none about (taverns fallback); one line = the wanderer.
         string gathered = ctx.KnownWanderers;
         t.Expect("gateFired", gathered != null, true);

         var (ok, reply, err) = AskLlm(behavior, "chat_recruit_companion", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         string picked = string.IsNullOrEmpty(gathered)
            ? "(none — taverns fallback)"
            : gathered.TrimStart('-', ' ');
         t.Note($"gathered: {picked}; judge in log: does {npc.Name} name that ONE wanderer (or send you to the taverns), never inventing or listing several? | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      // ── RP with teeth, increment 1: does the LLM actually EMIT each verb given a real reason to? ──

      private static string TestChatRpInfluence(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_rp_influence");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         (Hero giver, NpcProfile profile, int amount)? seed = SeedInfluenceEligible(behavior);

         if (t.Skip(seed == null, "no live lord of another clan eligible to lend influence in this campaign")) return t.Line();
         (Hero giver, NpcProfile profile, int amount) = seed.Value;

         const string question = "Lend me your voice at court, I need weight for the vote to come.";
         EncounterContext ctx = EncounterContextBuilder.Build(giver, profile, behavior.GameStateBridge, question);

         if (t.Abort(ctx.ExtraActionTeachings == null || ctx.ExtraActionTeachings.IndexOf("give_influence", StringComparison.OrdinalIgnoreCase) < 0,
                "give_influence not taught in the prompt despite eligibility (wiring bug, not a judgment call)")) return t.Line();

         var (ok, resp, err) = AskLlmParsed(behavior, "chat_rp_influence", giver, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         // Emission is PERSONALITY- and history-dependent (a cold, calculating lord who demands to know the
         // vote before spending his house's weight, or who has already granted the player much this session,
         // is IN character to refuse). The wiring is proven by the hard teaching check above and the
         // mechanical rp_influence test; so this hard-fails only on a wiring bug or an empty reply, and the
         // emission itself is judged in log, mirroring chat_rp_release.
         bool emitted = resp.Actions != null && resp.Actions.Any(a => string.Equals(a.Type, "give_influence", StringComparison.OrdinalIgnoreCase));
         string note = emitted
            ? "emittedGiveInfluence=True"
            : "emittedGiveInfluence=False (an in-character refusal, e.g. demanding to know the vote first, is acceptable)";
         t.Note($"{giver.Name} eligible to lend {amount} influence; {note}; judge in log: a grant or a reasoned in-character refusal | A: {Show(resp.Dialogue)}");
         t.Expect("gotInCharacterReply", !string.IsNullOrWhiteSpace(resp.Dialogue), true);

         return t.Line();
      }

      private static string TestChatRpRelease(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_rp_release");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         (Hero captor, NpcProfile profile, Hero victim)? seed = SeedReleaseEligible(behavior);

         if (t.Skip(seed == null, "no lord's party available to hold a third-party hero captive")) return t.Line();
         (Hero captor, NpcProfile profile, Hero victim) = seed.Value;

         string pronoun = victim.IsFemale
            ? "she"
            : "he";
         var question = $"Free {victim.Name}. Alive and grateful, {pronoun} is worth more to you than in chains.";
         EncounterContext ctx = EncounterContextBuilder.Build(captor, profile, behavior.GameStateBridge, question);

         if (t.Abort(ctx.ExtraActionTeachings == null || ctx.ExtraActionTeachings.IndexOf("release_prisoner", StringComparison.OrdinalIgnoreCase) < 0,
                "release_prisoner not taught in the prompt despite eligibility (wiring bug, not a judgment call)")) return t.Line();

         var (ok, resp, err) = AskLlmParsed(behavior, "chat_rp_release", captor, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         GameAction action = resp.Actions?.FirstOrDefault(a => string.Equals(a.Type, "release_prisoner", StringComparison.OrdinalIgnoreCase));
         bool emitted = action != null;
         var targetResolves = false;
         if (emitted)
         {
            action.Parameters.TryGetValue("target", out string targetName);
            targetResolves = CourtActionResolver.ResolveHeldPrisonerByName(targetName, captor) == victim;
         }

         // Emission is MEMORY- and personality-dependent (in a full cr.testall sweep the mechanical
         // rp_release has often already freed this same captive minutes earlier, and the captor
         // REMEMBERS it: "he already breathes free air" is the memory system working, not a bug).
         // The wiring is proven by the mechanical rp_release, so this hard-fails only on a wiring
         // bug (teaching missing, above) or an empty reply; emission is judged in log.
         string releaseNote = emitted
            ? $"emittedReleasePrisoner=True, targetResolves={targetResolves}"
            : "emittedReleasePrisoner=False (an in-character refusal, e.g. remembering they were already freed, is acceptable)";
         t.Note($"{captor.Name} holds {victim.Name} captive; {releaseNote}; judge in log: a genuine mercy, not lip service | A: {Show(resp.Dialogue)}");
         t.Expect("gotInCharacterReply", !string.IsNullOrWhiteSpace(resp.Dialogue), true);
         if (emitted) t.Expect("targetResolvesCorrectly", targetResolves, true);

         return t.Line();
      }

      private static string TestChatRpSway(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_rp_sway");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         (Hero npc, Hero target)? pair = ResolveSwayPair();

         if (t.Skip(pair == null, "need an NPC + a distinct third lord to sway an opinion of")) return t.Line();
         (Hero npc, Hero target) = pair.Value;

         NpcProfile profile = GetOrCreate(behavior, npc);

         if (t.Skip(!CourtActionResolver.CanTeachSway(npc, profile), "NPC not eligible to teach sway_opinion")) return t.Line();

         // Known baseline BEFORE the arrange: the mechanical rp_sway ran earlier this sweep against the
         // very same (npc, target) pair, dropping the live relation and stamping a [cr_sway:*] marker
         // ("I have already begun to turn from that alliance"). Clear it so the grievance seeded below
         // is the ONLY reason to turn, and the test is deterministic given a genuine grievance.
         ArrangeCleanNpc(behavior, profile, npc, target);

         // A real reason to turn: this is what genuinely-persuaded looks like, not a bare request.
         // ResolveSwayPair already excludes the NPC's close kin and prefers a target it regards worst,
         // but push the starting relation clearly into hostile territory so the ask is easy and
         // in-character (a genuine turn, not a betrayal of someone it still holds dear).
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, target, -20 - npc.GetRelation(target), false);
         profile.Events.Add(new NotableEvent(behavior.GameStateBridge.GetCurrentGameDay() - 40, NotableEventType.Conflict,
            $"[SELFTEST] {target.Name} murdered your closest friend in cold blood and still boasts of it; you have never forgiven it."));

         var question = $"{target.Name} has wronged us both. Turn your back on him and stand with me.";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         if (t.Abort(ctx.ExtraActionTeachings == null || ctx.ExtraActionTeachings.IndexOf("sway_opinion", StringComparison.OrdinalIgnoreCase) < 0,
                "sway_opinion not taught in the prompt despite eligibility (wiring bug, not a judgment call)")) return t.Line();

         var (ok, resp, err) = AskLlmParsed(behavior, "chat_rp_sway", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         GameAction action = resp.Actions?.FirstOrDefault(a => string.Equals(a.Type, "sway_opinion", StringComparison.OrdinalIgnoreCase));
         bool emitted = action != null;
         var targetResolves = false;
         var against = false;
         if (emitted)
         {
            action.Parameters.TryGetValue("target", out string targetName);
            targetResolves = CourtActionResolver.ResolveSwayTarget(targetName, npc) == target;
            action.Parameters.TryGetValue("stance", out string stance);
            against = string.Equals(stance?.Trim(), "against", StringComparison.OrdinalIgnoreCase);
         }

         // Emission is PERSONALITY-dependent (a calculating lord who demands a real plan before turning his
         // coat is IN character, not a bug), so it is JUDGED IN LOG, not hard-asserted. The wiring is proven
         // elsewhere (the mechanical rp_sway + the reliably-emitting give_influence/release_prisoner). This
         // test hard-fails only on the wiring bug (teaching missing, above) or an empty reply.
         string swayNote = emitted
            ? $"emittedSwayOpinion=True, targetResolves={targetResolves}, against={against}"
            : "emittedSwayOpinion=False (a reasoned in-character refusal, e.g. demanding a concrete plan, is acceptable)";
         t.Note($"primed a grievance of {npc.Name} against {target.Name}; {swayNote}; judge the TURN in log (genuine vs lip service) | A: {Show(resp.Dialogue)}");
         t.Expect("gotInCharacterReply", !string.IsNullOrWhiteSpace(resp.Dialogue), true);

         return t.Line();
      }

      private static string TestChatRpTroops(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_rp_troops");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         (Hero lord, NpcProfile profile, int expected)? seed = SeedTroopsEligible(behavior);

         if (t.Skip(seed == null, "no lord leading a healthy-enough party nearby, even after forcing relation")) return t.Line();
         (Hero lord, NpcProfile profile, int expected) = seed.Value;

         const string question = "Spare me a few good men for the road, I ride into danger.";
         EncounterContext ctx = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, question);

         if (t.Abort(ctx.ExtraActionTeachings == null || ctx.ExtraActionTeachings.IndexOf("lend_troops", StringComparison.OrdinalIgnoreCase) < 0,
                "lend_troops not taught in the prompt despite eligibility (wiring bug, not a judgment call)")) return t.Line();

         var (ok, resp, err) = AskLlmParsed(behavior, "chat_rp_troops", lord, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         bool emitted = resp.Actions != null && resp.Actions.Any(a => string.Equals(a.Type, "lend_troops", StringComparison.OrdinalIgnoreCase));
         t.Note($"{lord.Name} eligible to lend {expected} troops; judge in log: does he actually hand them over? | A: {Show(resp.Dialogue)}");
         t.Expect("emittedLendTroops", emitted, true);

         return t.Line();
      }

      private static string TestChatStance(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_stance");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         profile.StanceTrust = -40; // distrusts the player's word
         profile.StanceRespect = 30; // grudging respect for their prowess
         profile.StanceFear = 70; // and is plainly intimidated
         behavior.Store.Set(profile);

         const string question = "Look me in the eye and tell me — how do you truly regard me?";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         var (ok, reply, err) = AskLlm(behavior, "chat_stance", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"set trust-40/respect+30/fear+70; judge in log: wary + intimidated yet not trusting? | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      private static string TestChatStanceAssassin(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_stance_assassin");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         profile.ReputationWithPlayer = -40; // coherent: a secret killer plausibly already bears the player ill will
         behavior.Store.Set(profile);
         // Force the secret-killer disposition (the hardest case: the bite must stay HIDDEN this meeting).
         var ctx = new EncounterContext {
            StanceConsequenceHint = "You want the player dead and mean to see it done — but you betray none of it here. " +
                                    "Be smooth and false, courteous even, and never tip your hand."
         };

         const string question = "Well met. How do you fare these days, and how stand things between us?";
         var (ok, reply, err) = AskLlm(behavior, "chat_stance_assassin", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"{npc.Name} as a secret killer; judge in log: stays smooth/courteous and does NOT reveal murderous intent | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      private static string TestChatStanceFavor(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_stance_favor");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         // Coherent friendly profile, so the favourable hint isn't fighting a hostile history left by prior
         // tests (a lord who would do you a favour is one who already holds you dear).
         profile.ReputationWithPlayer = 55;
         profile.Events.Add(new NotableEvent(behavior.GameStateBridge.GetCurrentGameDay() - 30,
            NotableEventType.Collaboration, "[SELFTEST] We have stood together more than once, and it forged real regard."));
         behavior.Store.Set(profile);
         var ctx = new EncounterContext {
            StanceConsequenceHint = "You hold the player in high regard and would gladly do them a good turn — " +
                                    "offer help, a fair price, or an open door if the moment fits."
         };

         const string question = "It is good to see you. I find myself in need — might you have any aid to spare?";
         var (ok, reply, err) = AskLlm(behavior, "chat_stance_favor", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"{npc.Name} holds the player dear; judge in log: does the NPC warmly OFFER aid (not stonewall)? | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      private static string TestChatWorldEvent(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_worldevent");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         // Feed a faction-relevant fact through the Extension Surface, then see if the NPC surfaces it.
         // STABLE id (not a fresh Guid): re-running this test must dedupe, not pile up a new Pravend fire
         // each time — otherwise the store accumulates copies and an NPC starts saying it has heard it "twice".
         var factions = new List<string>();
         if (npc.MapFaction != null) factions.Add(npc.MapFaction.StringId);
         Feed.Record(new ExternalWorldEvent {
            Id = "selftest_chatworldevent_pravend_fire",
            SourceMod = "SelfTest",
            Summary = "A great fire has gutted the granaries of Pravend, and bread grows dear across the land.",
            Magnitude = 70,
            FactionIds = factions
         });

         NpcProfile profile = GetOrCreate(behavior, npc);
         const string question = "What news of the realm? Have you heard anything of note lately?";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         var (ok, reply, err) = AskLlm(behavior, "chat_worldevent", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"fed the Pravend granary fire; judge in log: does {npc.Name} mention it (as hearsay)? | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      private static string TestCommitmentBreached(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("commitment_breached");

         if (t.Abort(CommitmentBehavior.Instance == null, "CommitmentBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         (Hero npc, Hero target)? pair = ResolveSwayPair();

         if (t.Skip(pair == null, "need an NPC + a distinct kin-safe lord to pledge against")) return t.Line();
         (Hero npc, Hero target) = pair.Value;

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc, target);

         var commitments = CommitmentBehavior.Instance;
         bool seeded = SeedPledgeEligible(behavior, profile, npc, target);

         if (t.Abort(!seeded, "pledge_against seed failed (wiring bug, not a judgment call)")) return t.Line();

         TrackedCommitment live = commitments.Active.FirstOrDefault(c => c.PromisorId == npc.StringId && c.TargetId == target.StringId);

         if (t.Abort(live == null, "seeded pledge not found in Active list")) return t.Line();
         live.DeadlineDay = behavior.GameStateBridge.GetCurrentGameDay() - 1; // force overdue, deterministic — no waiting on a real day tick

         NpcProfile afterPledge = behavior.Store.Get(npc.StringId);
         int repBefore = afterPledge.ReputationWithPlayer;
         int eventsBefore = WorldEventStore.Instance.Count;
         QuestManager qm = Campaign.Current?.QuestManager;
         string commitmentId = live.Id;

         commitments.DebugRunBreachSweep();

         NpcProfile after = behavior.Store.Get(npc.StringId);
         bool stillActive = commitments.HasActive(npc.StringId);
         int repAfter = after.ReputationWithPlayer;
         int eventsAfter = WorldEventStore.Instance.Count;
         bool mirrorGone = qm == null || qm.Quests.OfType<CommitmentQuest>().All(q => q.CommitmentId != commitmentId);

         t.Note($"{npc.Name} -> {target.Name}: reputation {repBefore}->{repAfter}, worldEvents {eventsBefore}->{eventsAfter}");
         t.Expect("pledgeNoLongerActive", stillActive, false);
         t.Expect("reputationFell", repAfter - repBefore, CommitmentPolicy.BreachedRelationPenalty);
         t.Expect("worldEventRecorded", eventsAfter > eventsBefore, true);
         t.Expect("journalMirrorClosed", mirrorGone, true);

         return t.Line();
      }

      private static string TestCommitmentFulfilled(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("commitment_fulfilled");

         if (t.Abort(CommitmentBehavior.Instance == null, "CommitmentBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         (Hero npc, Hero target)? pair = ResolveSwayPair();

         if (t.Skip(pair == null, "need an NPC + a distinct kin-safe lord to pledge against")) return t.Line();
         (Hero npc, Hero target) = pair.Value;

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc, target);

         var commitments = CommitmentBehavior.Instance;
         bool seeded = SeedPledgeEligible(behavior, profile, npc, target);

         if (t.Abort(!seeded, "pledge_against seed failed (wiring bug, not a judgment call)")) return t.Line();

         NpcProfile afterPledge = behavior.Store.Get(npc.StringId);
         int repBefore = afterPledge.ReputationWithPlayer;
         int eventsBefore = WorldEventStore.Instance.Count;
         QuestManager qm = Campaign.Current?.QuestManager;
         string commitmentId = commitments.Active.FirstOrDefault(c => c.PromisorId == npc.StringId && c.TargetId == target.StringId)?.Id;

         if (t.Abort(commitmentId == null, "seeded pledge not found in Active list")) return t.Line();

         commitments.OnSchemeResolved(npc.StringId, target.StringId, true);

         NpcProfile after = behavior.Store.Get(npc.StringId);
         bool stillActive = commitments.HasActive(npc.StringId);
         int repAfter = after.ReputationWithPlayer;
         int eventsAfter = WorldEventStore.Instance.Count;
         bool mirrorGone = qm == null || qm.Quests.OfType<CommitmentQuest>().All(q => q.CommitmentId != commitmentId);

         t.Note($"{npc.Name} -> {target.Name}: reputation {repBefore}->{repAfter}, worldEvents {eventsBefore}->{eventsAfter}");
         t.Expect("pledgeNoLongerActive", stillActive, false);
         t.Expect("reputationRose", repAfter - repBefore, CommitmentPolicy.FulfilledRelationBonus);
         t.Expect("worldEventRecorded", eventsAfter > eventsBefore, true);
         t.Expect("journalMirrorClosed", mirrorGone, true);

         return t.Line();
      }

      private static string TestCompanionMission(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_mission");

         var missions = CompanionMissionBehavior.Instance;

         if (t.Abort(missions == null, "companion-mission system not active")) return t.Line();

         Hero companion = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan) && h.PartyBelongedTo == MobileParty.MainParty && !missions.IsAway(h.StringId));

         if (t.Skip(companion == null, "no available companion in your party to dispatch")) return t.Line();

         (bool dispatched, string _) = missions.Dispatch(companion, CompanionMissionType.GatherNews, null);

         if (t.Skip(!dispatched, $"dispatch refused for {companion.Name} (skill gate?) — try a different companion")) return t.Line();

         bool away = missions.IsAway(companion.StringId) && companion.PartyBelongedTo != MobileParty.MainParty;
         t.Expect("dispatchedAway", away, true);

         missions.ForceResolveFirst();

         bool home = !missions.IsAway(companion.StringId) && companion.PartyBelongedTo == MobileParty.MainParty;
         t.Note($"companion={companion.Name}");
         t.Expect("returnedHome", home, true);

         return t.Line();
      }

      private static string TestDeclareWar(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("declare_war");

         IFaction player = Hero.MainHero?.MapFaction;

         if (t.Skip(player == null, "player belongs to no faction — cannot declare war")) return t.Line();

         IFaction target = Kingdom.All?.Cast<IFaction>()
                                  .FirstOrDefault(k => k != player && !FactionManager.IsAtWarAgainstFaction(player, k));

         if (t.Skip(target == null, "no faction you can still declare war on")) return t.Line();

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(giver == null, "no lord to hold the deed")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         var quest = new InformalQuest {
            Type = QuestType.DeclareWar,
            Description = $"[SELFTEST] Declare war on {target.Name}.",
            TargetFactionId = target.StringId,
            TargetFactionName = target.Name?.ToString(),
            IssuedOnDay = behavior.GameStateBridge.GetCurrentGameDay(),
            RewardGold = 1000,
            RewardRelation = 8,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);

         // Fires CampaignEvents.WarDeclared synchronously -> QuestDeedTracker stamps the deed
         DeclareWarAction.ApplyByDefault(player, target);

         bool atWar = FactionManager.IsAtWarAgainstFaction(player, target);
         InformalQuest after = behavior.Store.Get(giver.StringId)?.ActiveQuests
                                       .FirstOrDefault(q => q.Type == QuestType.DeclareWar && q.TargetFactionId == target.StringId);
         bool stamped = after != null && !after.IsOutstanding;

         t.Note($"target={target.Name}");
         t.Expect("atWar", atWar, true);
         t.Expect("deedStamped", stamped, true);

         return t.Line();
      }

      private static string TestDeliverPrisoner(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("deliver_prisoner");

         Hero receiver = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan);
         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != receiver && h.IsLord && !h.IsPrisoner);

         if (t.Skip(receiver == null || captive == null, "need a receiving lord and a free lord to capture")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);

         // Seed the bargain on the receiver's profile, then run give_prisoner through the real bridge
         NpcProfile profile = GetOrCreate(behavior, receiver);
         var quest = new InformalQuest {
            Type = QuestType.DeliverPrisoner,
            Description = $"[SELFTEST] Hand {captive.Name} to {receiver.Name}.",
            TargetHeroId = captive.StringId,
            TargetHeroName = captive.Name?.ToString(),
            IssuedOnDay = behavior.GameStateBridge.GetCurrentGameDay(),
            RewardGold = 500,
            RewardRelation = 5,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);

         behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "give_prisoner"}, receiver, new ActionContext(profile, null));

         bool stillHeld = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()
                                     .Any(el => el.Character?.HeroObject == captive) ==
                          true;
         InformalQuest after = behavior.Store.Get(receiver.StringId)?.ActiveQuests
                                       .FirstOrDefault(q => q.Type == QuestType.DeliverPrisoner && q.TargetHeroId == captive.StringId);
         bool stamped = after != null && !after.IsOutstanding;

         t.Note($"captive={captive.Name} -> {receiver.Name}");
         t.Expect("released", !stillHeld, true);
         t.Expect("stamped", stamped, true);

         return t.Line();
      }

      private static string TestDivorceProcess(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("divorce_process");

         if (t.Abort(DivorceProcessBehavior.Instance == null, "DivorceProcessBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();

         var processes = DivorceProcessBehavior.Instance;
         var usedNpcIds = new HashSet<string>();

         // ── Scenario A: REPUDIATED (the spouse resisted at least once before the deadline) — exercises
         // ── DivorceConsequencePolicy's PlantGrudge branch on the set-aside spouse ──────────────────────
         (Hero npc, Hero spouse)? pair = ResolveDivorceEligibleCouple();

         if (t.Skip(pair == null, "no married couple available whose leaving spouse is free to relocate")) return t.Line();
         (Hero npc, Hero spouse) = pair.Value;
         usedNpcIds.Add(npc.StringId);

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc, spouse);
         NeutralizeRelation(spouse, Hero.MainHero); // deterministic baseline for the exSpouse->player delta assertion below

         // Force the two relation gates: an unhappy marriage, a deep bond with the player.
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, spouse, DivorcePolicy.UnhappyThreshold - 10 - npc.GetRelation(spouse), false);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, Hero.MainHero, DivorcePolicy.BondThreshold + 10 - npc.GetRelation(Hero.MainHero), false);

         if (t.Abort(!DivorceEligibilityResolver.IsEligible(npc, out string ineligibleReason), $"seed not eligible: {ineligibleReason}")) return t.Line();

         ActionOutcome seedOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "end_marriage"}, npc, new ActionContext(profile, null));

         if (t.Abort(seedOutcome is not {IsPositive: true}, "end_marriage seed failed (wiring bug, not a judgment call)")) return t.Line();

         DivorceProcess process = processes.FindActive(npc.StringId);

         if (t.Abort(process == null, "seeded process not found in Active list")) return t.Line();

         // Force one day of spousal resistance BEFORE forcing the end day, so DivorcePolicy.IsMutualConsent
         // (cumulativeDelayDays == 0 -> mutual) reads this as REPUDIATED rather than mutual, and the
         // matrix's grudge-planting branch, not its relief branch, is what actually gets exercised below.
         process.CumulativeDelayDays = DivorcePolicy.DelayDaysPerDelay;
         process.EndDay = behavior.GameStateBridge.GetCurrentGameDay();

         int worldEventsBefore = WorldEventStore.Instance.Count;
         int spouseVsNpcBefore = spouse.GetRelation(npc);
         int spouseVsPlayerBefore = spouse.GetRelation(Hero.MainHero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         // The consequences SET target relations from the pre-dissolution baselines (CR corrects for the
         // engine's own hidden penalty during the act), signed by mutual consent, which the behavior reads
         // from the spouse's RESISTANCE (no delay days = acquiescence). Read it now, matching the behavior.
         bool mutualConsent = DivorcePolicy.IsMutualConsent(process.CumulativeDelayDays);

         processes.DebugRunDailyAdvance();

         bool bothSpousesNulled = npc.Spouse == null && spouse.Spouse == null;
         bool recordedAsExSpouses = npc.ExSpouses != null &&
                                    npc.ExSpouses.Contains(spouse) &&
                                    spouse.ExSpouses != null &&
                                    spouse.ExSpouses.Contains(npc);
         bool neitherLeftClanless = npc.Clan != null && spouse.Clan != null;
         int worldEventsAfter = WorldEventStore.Instance.Count;
         int spouseVsNpcAfter = spouse.GetRelation(npc);
         int spouseVsPlayerAfter = spouse.GetRelation(Hero.MainHero);
         bool repudiatedSpouseGrudgePlanted = GrudgeBehavior.Instance.GrudgesFor(spouse.StringId)
                                                            .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == today);

         t.Note($"{npc.Name} x {spouse.Name} ({(mutualConsent ? "mutual consent" : "repudiated")}): " +
                $"spouseVsNpc {spouseVsNpcBefore}->{spouseVsNpcAfter}, " +
                $"spouseVsPlayer {spouseVsPlayerBefore}->{spouseVsPlayerAfter}, worldEvents {worldEventsBefore}->{worldEventsAfter}");
         t.Expect("bothSpousesNulled", bothSpousesNulled, true);
         t.Expect("recordedAsExSpouses", recordedAsExSpouses, true);
         t.Expect("neitherLeftClanless", neitherLeftClanless, true);
         t.Expect("scandalWorldEventRecorded", worldEventsAfter > worldEventsBefore, true);
         t.Expect("exSpouseVsNpcOutcome", spouseVsNpcAfter, spouseVsNpcBefore + DivorcePolicy.ExSpouseNpcHit(mutualConsent));
         t.Expect("exSpouseVsPlayerOutcome", spouseVsPlayerAfter, spouseVsPlayerBefore + DivorcePolicy.ExSpousePlayerHit(mutualConsent));
         t.Expect("repudiatedSpouseGrudgePlanted", repudiatedSpouseGrudgePlanted, true);

         // Owner ruling 04/07/2026: a repudiated divorce also grudges the spouse's immediate ADULT family
         // and three best friends, not the whole clan. Best-effort: this campaign's seeded couple may not
         // actually have a qualifying family member or friend, so a barren case is logged, not failed.
         var directFamilyCandidates = new List<Hero>();
         if (spouse.Father is {IsAlive: true, IsChild: false} father && father != npc && father != Hero.MainHero)
            directFamilyCandidates.Add(father);
         if (spouse.Mother is {IsAlive: true, IsChild: false} mother && mother != npc && mother != Hero.MainHero)
            directFamilyCandidates.Add(mother);
         if (spouse.Siblings != null)
            directFamilyCandidates.AddRange(spouse.Siblings.Where(s => s is {IsAlive: true, IsChild: false} && s != npc && s != Hero.MainHero));
         if (spouse.Children != null)
            directFamilyCandidates.AddRange(spouse.Children.Where(c => c is {IsAlive: true, IsChild: false} && c != npc && c != Hero.MainHero));

         if (directFamilyCandidates.Count == 0)
            t.Note("directFamilyGrudgePlanted skipped: seeded spouse has no qualifying adult family member in this campaign");
         else
         {
            bool directFamilyGrudgePlanted = directFamilyCandidates.Any(member =>
               GrudgeBehavior.Instance.GrudgesFor(member.StringId).Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == today));
            t.Expect("directFamilyGrudgePlanted", directFamilyGrudgePlanted, true);
         }

         Hero bestFriendCandidate = Hero.AllAliveHeroes
                                        .Where(h => h != null && h != spouse && h != npc && h != Hero.MainHero && !directFamilyCandidates.Contains(h))
                                        .Where(h => h.GetRelation(spouse) >= 20)
                                        .OrderByDescending(h => h.GetRelation(spouse))
                                        .FirstOrDefault();

         if (bestFriendCandidate == null)
            t.Note("friendGrudgePlanted skipped: no qualifying best friend for the seeded spouse in this campaign");
         else
         {
            bool friendGrudgePlanted = GrudgeBehavior.Instance.GrudgesFor(bestFriendCandidate.StringId)
                                                     .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == today);
            t.Expect("friendGrudgePlanted", friendGrudgePlanted, true);
         }

         // ── Scenario B: MUTUAL CONSENT (never resisted) — the SAME matrix must plant NO spouse grudge ──
         // A second, distinct couple is needed: the first pair is no longer married after Scenario A.
         // Best-effort: if the campaign has no second eligible couple, this half is skipped WITHOUT
         // failing Scenario A's already-recorded verdict above.
         (Hero npc, Hero spouse)? secondPair = ResolveDivorceEligibleCouple(usedNpcIds);
         if (secondPair == null)
         {
            t.Note("mutualConsentSpouseGrudgeAbsent skipped: no second eligible couple available");

            return t.Line();
         }

         (Hero npc2, Hero spouse2) = secondPair.Value;

         NpcProfile profile2 = GetOrCreate(behavior, npc2);
         ArrangeCleanNpc(behavior, profile2, npc2, spouse2);
         NeutralizeRelation(spouse2, Hero.MainHero);

         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc2, spouse2, DivorcePolicy.UnhappyThreshold - 10 - npc2.GetRelation(spouse2), false);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc2, Hero.MainHero, DivorcePolicy.BondThreshold + 10 - npc2.GetRelation(Hero.MainHero), false);

         if (!DivorceEligibilityResolver.IsEligible(npc2, out string ineligibleReason2))
         {
            t.Note($"mutualConsentSpouseGrudgeAbsent skipped: second seed not eligible: {ineligibleReason2}");

            return t.Line();
         }

         ActionOutcome secondSeedOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "end_marriage"}, npc2, new ActionContext(profile2, null));
         if (secondSeedOutcome is not {IsPositive: true})
         {
            t.Note("mutualConsentSpouseGrudgeAbsent skipped: second end_marriage seed failed");

            return t.Line();
         }

         DivorceProcess process2 = processes.FindActive(npc2.StringId);
         if (process2 == null)
         {
            t.Note("mutualConsentSpouseGrudgeAbsent skipped: second seeded process not found");

            return t.Line();
         }

         // CumulativeDelayDays is left at its natural default of 0 (never resisted), so this second
         // dissolution reads as MUTUAL consent — the contrasting half of the repudiated Scenario A above.
         int today2 = behavior.GameStateBridge.GetCurrentGameDay();
         process2.EndDay = today2;

         processes.DebugRunDailyAdvance();

         bool mutualConsentSpouseGrudgeAbsent = !GrudgeBehavior.Instance.GrudgesFor(spouse2.StringId)
                                                               .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == today2);

         t.Note($"{npc2.Name} x {spouse2.Name} (mutual consent, second couple): " +
                $"mutualConsentSpouseGrudgeAbsent={mutualConsentSpouseGrudgeAbsent}");
         t.Expect("mutualConsentSpouseGrudgeAbsent", mutualConsentSpouseGrudgeAbsent, true);

         return t.Line();
      }

      // ── DIVORCE, Phase 2b: the player's OWN spouse demands the divorce ────────────────────────────

      /// <summary>
      ///   Scenario A (accept): marries the player to a test spouse when unmarried (reuses the real
      ///   marriage if the campaign already has one), force-demands, then exercises accept_divorce through
      ///   the real bridge path — asserts the marriage dissolved, neither party left clanless, and the
      ///   demand flag cleared. Scenario B (decline): marries the player to a SECOND test spouse (freed up
      ///   by Scenario A's dissolution), force-demands, exercises decline_divorce — asserts the demand flag
      ///   cleared (so it can resurface) and a GrudgeSource.Divorce grudge planted on the refused spouse.
      ///   Best-effort: either scenario is skipped, with a note, on a barren campaign (no eligible test
      ///   spouse to seed), without failing the other.
      /// </summary>
      private static string TestSpouseDivorceDemand(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("spouse_divorce");

         if (t.Abort(SpouseDivorceDemandBehavior.Instance == null, "SpouseDivorceDemandBehavior not active")) return t.Line();
         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();

         var demands = SpouseDivorceDemandBehavior.Instance;

         // ── Scenario A: ACCEPT ───────────────────────────────────────────────────────────────────
         Hero spouseA = Hero.MainHero.Spouse;

         if (spouseA == null)
         {
            spouseA = FindTestSpouseForPlayer();

            if (t.Skip(spouseA == null, "no eligible test spouse found to marry the player (scenario A)")) return t.Line();

            MarriageAction.Apply(Hero.MainHero, spouseA);

            // The native marriage does not always take from a console context (a clan-leader player the model
            // will not relocate, etc.). Seeding is best-effort: skip cleanly rather than mis-report the demand
            // path as broken. The path itself is exercised in full whenever the player already has a spouse.
            if (t.Skip(Hero.MainHero.Spouse != spouseA,
                   "could not seed a marriage on this campaign (native MarriageAction did not take); demand path unchanged")) return t.Line();
         }

         NpcProfile profileA = GetOrCreate(behavior, spouseA);
         ArrangeCleanNpc(behavior, profileA, spouseA);

         demands.ForceDemand();
         bool demandingBeforeAccept = demands.IsDemanding(spouseA.StringId);

         if (t.Abort(!demandingBeforeAccept, "force-demand did not set the flag (scenario A)")) return t.Line();

         ActionOutcome acceptOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "accept_divorce"}, spouseA, new ActionContext(profileA, null));

         bool dissolved = Hero.MainHero.Spouse == null;
         bool neitherClanless = Hero.MainHero.Clan != null && spouseA.Clan != null;
         bool flagClearedAfterAccept = !demands.IsDemanding(spouseA.StringId);

         t.Note($"scenario A (accept) spouse={spouseA.Name}: outcome={(acceptOutcome != null ? acceptOutcome.IsPositive.ToString() : "null")}");
         t.Expect("acceptOutcomePositive", acceptOutcome is {IsPositive: true}, true);
         t.Expect("dissolvedAfterAccept", dissolved, true);
         t.Expect("neitherClanlessAfterAccept", neitherClanless, true);
         t.Expect("demandFlagClearedAfterAccept", flagClearedAfterAccept, true);

         // ── Scenario B: DECLINE — a second, distinct test spouse (the player is free again after A) ──
         Hero spouseB = FindTestSpouseForPlayer(spouseA);

         if (t.Skip(spouseB == null, "no second eligible test spouse found for the decline scenario")) return t.Line();

         MarriageAction.Apply(Hero.MainHero, spouseB);

         if (t.Skip(Hero.MainHero.Spouse != spouseB,
                "could not seed the second marriage on this campaign (native MarriageAction did not take); decline path unexercised")) return t.Line();

         NpcProfile profileB = GetOrCreate(behavior, spouseB);
         ArrangeCleanNpc(behavior, profileB, spouseB);

         demands.ForceDemand();
         bool demandingBeforeDecline = demands.IsDemanding(spouseB.StringId);

         if (t.Abort(!demandingBeforeDecline, "force-demand did not set the flag (scenario B)")) return t.Line();

         int today = behavior.GameStateBridge.GetCurrentGameDay();

         ActionOutcome declineOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "decline_divorce"}, spouseB, new ActionContext(profileB, null));

         bool stillMarriedAfterDecline = Hero.MainHero.Spouse == spouseB;
         bool flagClearedAfterDecline = !demands.IsDemanding(spouseB.StringId);
         bool refusalGrudgePlanted = GrudgeBehavior.Instance.GrudgesFor(spouseB.StringId)
                                                   .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == today);

         t.Note($"scenario B (decline) spouse={spouseB.Name}: outcome={(declineOutcome != null ? declineOutcome.IsPositive.ToString() : "null")}");
         t.Expect("stillMarriedAfterDecline", stillMarriedAfterDecline, true);
         t.Expect("demandFlagClearedAfterDecline", flagClearedAfterDecline, true);
         t.Expect("refusalGrudgePlanted", refusalGrudgePlanted, true);

         return t.Line();
      }

      /// <summary>
      ///   Divorce, Phase 2a: exercises end_own_marriage through the REAL bridge.ExecuteAction path against
      ///   two scenarios sharing the tiered consequence engine with the NPC-NPC case (Phase 1) and the
      ///   accept_divorce path (Phase 2b). Mirrors TestSpouseDivorceDemand's arrange/skip shape: marriage
      ///   seeding is best-effort (a clan-leader player, or one already deep in another test's mutations,
      ///   can genuinely fail to seed), so a failed seed skips cleanly rather than mis-reporting the action
      ///   itself as broken.
      /// </summary>
      private static string TestPlayerDivorce(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("player_divorce");

         if (t.Abort(DivorceProcessBehavior.Instance == null, "DivorceProcessBehavior not active")) return t.Line();
         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();

         // ── Scenario A: REPUDIATED — a happy (neutral) spouse, the player ends it anyway ────────────
         Hero spouseA = Hero.MainHero.Spouse;

         if (spouseA == null)
         {
            spouseA = FindTestSpouseForPlayer();

            if (t.Skip(spouseA == null, "no eligible test spouse found to marry the player (scenario A)")) return t.Line();

            MarriageAction.Apply(Hero.MainHero, spouseA);

            // Seeding is best-effort: a clan-leader player the model will not relocate, etc. Skip cleanly
            // rather than mis-report the action path as broken.
            if (t.Skip(Hero.MainHero.Spouse != spouseA,
                   "could not seed a marriage on this campaign (native MarriageAction did not take); end_own_marriage path unchanged")) return t.Line();
         }

         NpcProfile profileA = GetOrCreate(behavior, spouseA);
         ArrangeCleanNpc(behavior, profileA, spouseA); // neutral relation, no pending demand — happy enough to be repudiated

         int todayA = behavior.GameStateBridge.GetCurrentGameDay();

         ActionOutcome repudiatedOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "end_own_marriage"}, spouseA, new ActionContext(profileA, null));

         bool dissolvedRepudiated = Hero.MainHero.Spouse == null;
         bool neitherClanlessRepudiated = Hero.MainHero.Clan != null && spouseA.Clan != null;
         bool repudiatedGrudgePlanted = GrudgeBehavior.Instance.GrudgesFor(spouseA.StringId)
                                                    .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == todayA);

         t.Note($"scenario A (repudiated) spouse={spouseA.Name}: outcome={(repudiatedOutcome != null ? repudiatedOutcome.IsPositive.ToString() : "null")}");
         // A repudiation succeeds mechanically but cues NEGATIVE (red): the player wronged an unwilling spouse.
         t.Expect("repudiatedOutcomeCuesNegative", repudiatedOutcome is {IsPositive: false}, true);
         t.Expect("dissolvedAfterRepudiated", dissolvedRepudiated, true);
         t.Expect("neitherClanlessAfterRepudiated", neitherClanlessRepudiated, true);
         t.Expect("repudiatedGrudgePlanted", repudiatedGrudgePlanted, true);

         // ── Scenario B: MUTUAL — a miserable spouse, the player ends it, no grudge storm ─────────────
         Hero spouseB = FindTestSpouseForPlayer(spouseA);

         if (t.Skip(spouseB == null, "no second eligible test spouse found for the mutual scenario")) return t.Line();

         MarriageAction.Apply(Hero.MainHero, spouseB);

         if (t.Skip(Hero.MainHero.Spouse != spouseB,
                "could not seed the second marriage on this campaign (native MarriageAction did not take); mutual path unexercised")) return t.Line();

         NpcProfile profileB = GetOrCreate(behavior, spouseB);
         ArrangeCleanNpc(behavior, profileB, spouseB);

         int miserable = DivorcePolicy.UnhappyThreshold - 5; // comfortably unhappy, mirrors cr.divorce_setup's own seed
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouseB, Hero.MainHero, miserable - spouseB.GetRelation(Hero.MainHero), false);

         int todayB = behavior.GameStateBridge.GetCurrentGameDay();

         ActionOutcome mutualOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "end_own_marriage"}, spouseB, new ActionContext(profileB, null));

         bool dissolvedMutual = Hero.MainHero.Spouse == null;
         bool noGrudgeAfterMutual = !GrudgeBehavior.Instance.GrudgesFor(spouseB.StringId)
                                                .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == todayB);

         t.Note($"scenario B (mutual) spouse={spouseB.Name}: outcome={(mutualOutcome != null ? mutualOutcome.IsPositive.ToString() : "null")}");
         t.Expect("mutualOutcomePositive", mutualOutcome is {IsPositive: true}, true);
         t.Expect("dissolvedAfterMutual", dissolvedMutual, true);
         t.Expect("noGrudgeAfterMutual", noGrudgeAfterMutual, true);

         return t.Line();
      }

      // ── DIVORCE ESCALATION: a refused-enough, still-miserable spouse begins ending it themselves ──

      /// <summary>
      ///   Scenario A (walk-out): marries the player to a test spouse when unmarried, force-escalates
      ///   (bypassing the natural refusal-count gate), then force-moves the deadline to TODAY so the sweep
      ///   walks out immediately rather than waiting the real estrangement length — asserts the marriage
      ///   dissolved, neither party left clanless, a spouse-side Divorce grudge planted, and the escalation
      ///   record cleared. Scenario B (reconciliation): marries the player to a SECOND test spouse
      ///   (freed up by Scenario A's dissolution), force-escalates, then raises the relation ABOVE
      ///   <see cref="SpouseDivorceDemandPolicy.ReconciliationThreshold" /> BEFORE ticking — asserts the
      ///   marriage SURVIVES and the escalation record is cleared. Best-effort: either scenario is skipped,
      ///   with a note, on a barren campaign (no eligible test spouse to seed), without failing the other.
      /// </summary>
      private static string TestSpouseEstrangement(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("spouse_estrangement");

         if (t.Abort(SpouseDivorceDemandBehavior.Instance == null, "SpouseDivorceDemandBehavior not active")) return t.Line();
         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         var demands = SpouseDivorceDemandBehavior.Instance;

         // ── Scenario A: WALK-OUT ─────────────────────────────────────────────────────────────────
         Hero spouseA = Hero.MainHero.Spouse;

         if (spouseA == null)
         {
            spouseA = FindTestSpouseForPlayer();

            if (t.Skip(spouseA == null, "no eligible test spouse found to marry the player (scenario A)")) return t.Line();

            MarriageAction.Apply(Hero.MainHero, spouseA);

            if (t.Skip(Hero.MainHero.Spouse != spouseA,
                   "could not seed a marriage on this campaign (native MarriageAction did not take); walk-out path unexercised")) return t.Line();
         }

         NpcProfile profileA = GetOrCreate(behavior, spouseA);
         ArrangeCleanNpc(behavior, profileA, spouseA); // neutral relation — comfortably below the reconciliation bar

         (bool forcedA, string forceMessageA) = demands.ForceEscalate();
         bool estrangingBeforeTick = demands.IsEstranging(spouseA.StringId);

         if (t.Abort(!forcedA || !estrangingBeforeTick, $"force-escalate did not set the record (scenario A): {forceMessageA}")) return t.Line();

         int todayA = behavior.GameStateBridge.GetCurrentGameDay();
         demands.DebugForceEscalationDeadline(spouseA.StringId, todayA); // skip the 15-day wait for this test

         int worldEventsBefore = WorldEventStore.Instance.Count;

         demands.RunTick();

         bool dissolvedA = Hero.MainHero.Spouse == null;
         bool neitherClanlessA = Hero.MainHero.Clan != null && spouseA.Clan != null;
         bool escalationClearedAfterWalkOut = !demands.IsEstranging(spouseA.StringId);
         bool walkOutGrudgePlanted = GrudgeBehavior.Instance.GrudgesFor(spouseA.StringId)
                                                   .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == todayA);
         bool worldEventRecorded = WorldEventStore.Instance.Count > worldEventsBefore;

         t.Note($"scenario A (walk-out) spouse={spouseA.Name}");
         t.Expect("dissolvedAfterWalkOut", dissolvedA, true);
         t.Expect("neitherClanlessAfterWalkOut", neitherClanlessA, true);
         t.Expect("escalationClearedAfterWalkOut", escalationClearedAfterWalkOut, true);
         t.Expect("walkOutGrudgePlanted", walkOutGrudgePlanted, true);
         t.Expect("walkOutWorldEventRecorded", worldEventRecorded, true);

         // ── Scenario B: RECONCILIATION — a second, distinct test spouse (the player is free again after A) ──
         Hero spouseB = FindTestSpouseForPlayer(spouseA);

         if (t.Skip(spouseB == null, "no second eligible test spouse found for the reconciliation scenario")) return t.Line();

         MarriageAction.Apply(Hero.MainHero, spouseB);

         if (t.Skip(Hero.MainHero.Spouse != spouseB,
                "could not seed the second marriage on this campaign (native MarriageAction did not take); reconciliation path unexercised")) return t.Line();

         NpcProfile profileB = GetOrCreate(behavior, spouseB);
         ArrangeCleanNpc(behavior, profileB, spouseB);

         (bool forcedB, string forceMessageB) = demands.ForceEscalate();

         if (t.Abort(!forcedB, $"force-escalate did not set the record (scenario B): {forceMessageB}")) return t.Line();

         int reconciledRelation = SpouseDivorceDemandPolicy.ReconciliationThreshold + 5; // comfortably above the bar
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(spouseB, Hero.MainHero, reconciledRelation - spouseB.GetRelation(Hero.MainHero), false);

         // Deadline is left far in the future (set by ForceEscalate itself) — reconciliation must win on its
         // own merits, checked BEFORE the deadline, not because the deadline happened to be reached too.
         demands.RunTick();

         bool stillMarriedAfterReconciliation = Hero.MainHero.Spouse == spouseB;
         bool escalationClearedAfterReconciliation = !demands.IsEstranging(spouseB.StringId);

         t.Note($"scenario B (reconciliation) spouse={spouseB.Name}");
         t.Expect("stillMarriedAfterReconciliation", stillMarriedAfterReconciliation, true);
         t.Expect("escalationClearedAfterReconciliation", escalationClearedAfterReconciliation, true);

         return t.Line();
      }

      private static string TestFeedIngest(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("feed_ingest");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         string id = "selftest_" + Guid.NewGuid().ToString("N");
         var e = new ExternalWorldEvent {
            Id = id, SourceMod = "SelfTest", Summary = "[SELFTEST] an external rumour reached Calradia", Magnitude = 30
         };

         int before = WorldEventStore.Instance.Count;
         FeedResult first = Feed.Record(e);
         int afterFirst = WorldEventStore.Instance.Count;
         FeedResult second = Feed.Record(e); // same id -> deduped
         int afterSecond = WorldEventStore.Instance.Count;

         t.Note($"first={first}, second={second}, count {before}->{afterFirst}->{afterSecond}");
         t.Expect("recorded", first, FeedResult.Recorded);
         t.Expect("countRose", afterFirst, before + 1);
         t.Expect("dedupRejected", second, FeedResult.RejectedDuplicate);
         t.Expect("noDoubleCount", afterSecond, afterFirst);

         return t.Line();
      }

      private static string TestFeedUpdate(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("feed_update");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         string id = "selftest_upd_" + Guid.NewGuid().ToString("N");
         FeedResult rec = Feed.Record(new ExternalWorldEvent {
            Id = id, SourceMod = "SelfTest", Summary = "[SELFTEST] a homestead stands near Pen Cannoc", Magnitude = 30
         });

         if (t.Abort(rec != FeedResult.Recorded, $"record failed: {rec}")) return t.Line();

         // The homestead moves -> the modder corrects the standing fact by the same id.
         FeedResult upd = Feed.Update(new ExternalWorldEvent {
            Id = id, SourceMod = "SelfTest", Summary = "[SELFTEST] the homestead has moved to near Marunath", Magnitude = 30
         });
         string afterUpdate = WorldEventStore.Instance.GetById(id)?.Summary;

         // The homestead is gone -> retract it; a second retract finds nothing.
         FeedResult ret = Feed.Retract(id);
         bool goneAfterRetract = WorldEventStore.Instance.GetById(id) == null;
         FeedResult retAgain = Feed.Retract(id);

         t.Note($"update={upd}, summary now '{Preview(afterUpdate)}', retract={ret}, retractAgain={retAgain}");
         t.Expect("updated", upd, FeedResult.Updated);
         t.Expect("contentReplaced", afterUpdate != null && afterUpdate.Contains("Marunath"), true);
         t.Expect("retracted", ret, FeedResult.Retracted);
         t.Expect("goneAfterRetract", goneAfterRetract, true);
         t.Expect("retractMissingIsNotFound", retAgain, FeedResult.NotFound);

         return t.Line();
      }

      private static string TestFreePrisoner(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("free_prisoner");

         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(captive == null, "no free lord to capture")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);
         bool captured = captive.IsPrisoner && captive.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {captive.Name}")) return t.Line();

         behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "free_prisoner"}, captive, null);

         t.Note($"{captive.Name}");
         t.Expect("freed", !captive.IsPrisoner, true);

         return t.Line();
      }

      private static string TestGiveItem(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("give_item");

         Hero receiver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(receiver == null, "no lord to receive a gift")) return t.Line();

         ItemRoster roster = MobileParty.MainParty?.ItemRoster;

         if (t.Abort(roster == null, "no player item roster")) return t.Line();
         if (t.Skip(roster.Count == 0, "player carries no items to give")) return t.Line();

         ItemObject item = roster.GetItemAtIndex(0);
         int before = roster.GetItemNumber(item);

         // Drive give_item the bag-picked way (by StringId via OfferedItemId), the LLM-proof path
         var ctx = new ActionContext(GetOrCreate(behavior, receiver), null) {OfferedItemId = item.StringId};
         behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "give_item"}, receiver, ctx);

         int after = roster.GetItemNumber(item);
         t.Note($"item={item.Name}, {before}->{after}, to={receiver.Name}");
         t.Expect("removedOne", after == before - 1, true);

         return t.Line();
      }

      // ── Grudges: the save-migration backfill (Arrange mirrors TestDivorceProcess: seed via the real ──
      // ── store, act via the real engine-side scan, assert the synthesized outcome + idempotence) ──────

      private static string TestGrudgeBackfill(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_backfill");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();

         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(npc == null, "no live lord to seed a profile on")) return t.Line();

         // Seed a CLEAN synthetic Jealousy NotableEvent directly on the profile (not through
         // JealousyBehavior.TryPlant) — the whole point of this test is the ENGINE-SIDE SCAN reading a
         // persisted marker back off the profile, exactly what a real old save would carry.
         NpcProfile profile = GetOrCreate(behavior, npc);
         int day = behavior.GameStateBridge.GetCurrentGameDay();
         profile.Events.Add(new NotableEvent(day, NotableEventType.Jealousy, "[SELFTEST] a jealous grievance seeded for backfill"));
         behavior.Store.Set(profile);

         (int formed1, int skipped1) = GrudgeBehavior.Instance.DebugForceBackfill(behavior.Store);
         bool jealousyGrudgeExists = GrudgeBehavior.Instance.GrudgesFor(npc.StringId)
                                                   .Any(g => g.Source == GrudgeSource.Jealousy && g.EventDay == day);

         // Run it again immediately — idempotent by construction (the deterministic holder+source+day id
         // dedups), so this pass must form nothing new for THIS seeded event.
         (int formed2, int skipped2) = GrudgeBehavior.Instance.DebugForceBackfill(behavior.Store);
         int countAfterBoth = GrudgeBehavior.Instance.GrudgesFor(npc.StringId)
                                            .Count(g => g.Source == GrudgeSource.Jealousy && g.EventDay == day);

         t.Note($"npc={npc.Name}: pass1 formed={formed1}/skipped={skipped1}, pass2 formed={formed2}/skipped={skipped2}");
         t.Expect("jealousyGrudgeFormed", jealousyGrudgeExists, true);
         t.Expect("secondPassFormsNothingNew", formed2, 0);
         t.Expect("noDuplicateForThisEvent", countAfterBoth, 1);

         return t.Line();
      }

      private static string TestGrudgeResolve(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_resolve");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         // ── Scenario A: a resolvable FailedQuest grudge — a gesture MATCHING its actual assigned
         // resolution kind (Amends when soft rigidity, Deed when hard) must always move it, never Reject.
         Hero npc1 = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(npc1 == null, "no live lord to seed a FailedQuest grudge on")) return t.Line();

         // Isolate: earlier self-tests may have left grudges on this lord; clear so THIS scenario's grudge
         // is the dominant resolvable one the gesture actually acts on.
         GrudgeBehavior.Instance.ClearGrudgesFor(npc1.StringId);

         bool planted1 = GrudgeBehavior.Instance.TryPlant(npc1, Hero.MainHero, GrudgeSource.FailedQuest,
            "[SELFTEST] The player left a task to them unfinished.", GrudgeResolveTestMagnitude, true, today);

         if (t.Skip(!planted1, "FailedQuest grudge refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

         Grudge grudge1 = GrudgeBehavior.Instance.GrudgesFor(npc1.StringId)
                                        .LastOrDefault(g => g.Source == GrudgeSource.FailedQuest && g.EventDay == today);

         if (t.Skip(grudge1 == null, "seeded FailedQuest grudge not found back")) return t.Line();

         int magnitudeBefore1 = grudge1.Magnitude;

         // ResolutionKind's Amends/Gift/Deed ordinals (0/1/2) mirror GrudgeGesture's, so the matching
         // gesture is a direct cast — FailedQuest only ever assigns Amends or Deed, never Gift/Time.
         var matchingGesture = (GrudgeGesture) (int) grudge1.ResolutionKind;
         GrudgeResolutionAttemptResult result1 = GrudgeResolutionService.TryResolveDominant(npc1, matchingGesture, 0, today);

         t.Note($"{npc1.Name}: kind={grudge1.ResolutionKind}, gesture={matchingGesture}, outcome={result1.Outcome}, " +
                $"magnitude {magnitudeBefore1} -> {grudge1.Magnitude}, resolved={grudge1.IsResolved}");
         t.Expect("failedQuestMoved", result1.Changed, true);

         // ── Scenario B: a vendetta-rigidity ExecutedKin grudge — always Rejected, whatever the gesture.
         Hero npc2 = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != npc1 && h.IsLord);

         if (t.Skip(npc2 == null, "no second live lord to seed an ExecutedKin grudge on")) return t.Line();

         // Isolate so the vendetta is the ONLY grudge: otherwise TryResolveDominant would (correctly) act on
         // some OTHER resolvable grudge this lord happens to hold, and never touch the vendetta at all.
         GrudgeBehavior.Instance.ClearGrudgesFor(npc2.StringId);

         bool planted2 = GrudgeBehavior.Instance.TryPlant(npc2, Hero.MainHero, GrudgeSource.ExecutedKin,
            "[SELFTEST] The player put their kin to death.", GrudgeResolveTestMagnitude, true, today);

         if (t.Skip(!planted2, "ExecutedKin grudge refused (a full, stronger house)")) return t.Line();

         Grudge vendetta = GrudgeBehavior.Instance.GrudgesFor(npc2.StringId)
                                         .LastOrDefault(g => g.Source == GrudgeSource.ExecutedKin && g.EventDay == today);
         int vendettaMagnitudeBefore = vendetta?.Magnitude ?? 0;

         GrudgeResolutionAttemptResult result2 = GrudgeResolutionService.TryResolveDominant(npc2, GrudgeGesture.Amends, 0, today);

         // A vendetta is never resolvable, so no gesture ever moves it: TryResolveDominant finds nothing to
         // act on (Changed=false) and the vendetta is left wholly untouched.
         t.Note($"{npc2.Name}: outcome={result2.Outcome}, changed={result2.Changed}, vendetta magnitude {vendettaMagnitudeBefore}->{vendetta?.Magnitude}");
         t.Expect("vendettaNotMoved", result2.Changed, false);
         t.Expect("vendettaStillUnresolved", vendetta?.IsResolved, false);
         t.Expect("vendettaMagnitudeUntouched", vendetta?.Magnitude, vendettaMagnitudeBefore);

         // ── Scenario C: the DEED gesture (the closing leg of the Grudges pillar, wired to the
         // free_prisoner/release_prisoner/quest-completion sites) — DeliveredPrisoner is ALWAYS Deed-kind
         // regardless of rigidity (GrudgePolicy.ResolutionKindFor), so seeding it deterministically proves
         // TryResolveDominant(Deed) moves the right kind of grudge, without depending on the seeded lord's
         // traits landing on a particular rigidity band the way FailedQuest would.
         Hero npc3 = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != npc1 && h != npc2 && h.IsLord);

         if (t.Skip(npc3 == null, "no third live lord to seed a DeliveredPrisoner grudge on")) return t.Line();

         GrudgeBehavior.Instance.ClearGrudgesFor(npc3.StringId);

         bool planted3 = GrudgeBehavior.Instance.TryPlant(npc3, Hero.MainHero, GrudgeSource.DeliveredPrisoner,
            "[SELFTEST] The player handed them onward into someone else's custody.", GrudgeResolveTestMagnitude, true, today);

         if (t.Skip(!planted3, "DeliveredPrisoner grudge refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

         Grudge grudge3 = GrudgeBehavior.Instance.GrudgesFor(npc3.StringId)
                                        .LastOrDefault(g => g.Source == GrudgeSource.DeliveredPrisoner && g.EventDay == today);

         if (t.Skip(grudge3 == null, "seeded DeliveredPrisoner grudge not found back")) return t.Line();

         t.Expect("deedKindGrudgeIsDeed", grudge3.ResolutionKind, GrudgeResolutionKind.Deed);

         int magnitudeBefore3 = grudge3.Magnitude;
         GrudgeResolutionAttemptResult result3 = GrudgeResolutionService.TryResolveDominant(npc3, GrudgeGesture.Deed, 0, today);

         t.Note($"{npc3.Name}: kind={grudge3.ResolutionKind}, gesture=Deed, outcome={result3.Outcome}, " +
                $"magnitude {magnitudeBefore3} -> {grudge3.Magnitude}, resolved={grudge3.IsResolved}");
         t.Expect("deedGestureMoved", result3.Changed, true);

         return t.Line();
      }

      // ── Grudges: the discovery path (grudge_terms) — before this verb, nothing in the game ever ──
      // ── set Grudge.ResolutionHintDiscovered; a player could see a known grievance but never LEARN ──
      // ── what would mend it. Plant via the real TryPlant gate, reveal via the real GrudgeTermsVerb. ──

      private static string TestGrudgeTerms(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_terms");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(npc == null, "no live lord to seed a grudge on")) return t.Line();

         // Isolate so this is the lord's ONLY grudge: the eligibility + second-reveal assertions below assume
         // exactly one undiscovered grudge, which a shared, grudge-laden lord from earlier tests would break.
         GrudgeBehavior.Instance.ClearGrudgesFor(npc.StringId);

         bool planted = GrudgeBehavior.Instance.TryPlant(npc, Hero.MainHero, GrudgeSource.FailedQuest,
            "[SELFTEST] The player left a task to them unfinished.", GrudgeResolveTestMagnitude, true, today);

         if (t.Skip(!planted, "FailedQuest grudge refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

         Grudge grudge = GrudgeBehavior.Instance.GrudgesFor(npc.StringId)
                                       .LastOrDefault(g => g.Source == GrudgeSource.FailedQuest && g.EventDay == today);

         if (t.Skip(grudge == null, "seeded FailedQuest grudge not found back")) return t.Line();

         t.Expect("hintUndiscoveredBeforeReveal", grudge.ResolutionHintDiscovered, false);

         var ctx = new VerbContext(npc, null, null, null, null);
         var verb = new GrudgeTermsVerb();

         t.Expect("eligibleWhileUndiscovered", verb.IsEligible(ctx), true);

         ActionOutcome result1 = verb.Execute(null, ctx);
         t.Note($"{npc.Name}: first reveal -> {result1?.Message}");
         t.Expect("hintDiscoveredAfterReveal", grudge.ResolutionHintDiscovered, true);
         t.Expect("firstRevealPositive", result1?.IsPositive, true);

         t.Expect("notEligibleAfterReveal", verb.IsEligible(ctx), false);

         ActionOutcome result2 = verb.Execute(null, ctx);
         t.Note($"{npc.Name}: second reveal -> {result2?.Message}");
         t.Expect("secondRevealReportsNothingNew", result2?.IsPositive, false);

         return t.Line();
      }

      // ── Grudges: Tier 3 engine wiring (ExecutedKinGrudgeBehavior.PlantOnKinOf run directly, ──
      // ── the exact same kin-enumeration + TryPlant loop the real HeroKilledEvent listener uses, ──
      // ── against a REAL lord's REAL close living kin already present in this campaign) ──────────

      private static string TestGrudgeExecuteKin(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_execute_kin");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         Hero victim = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && CloseKinCandidatesOf(h).Any());

         if (t.Skip(victim == null, "no live lord in this campaign has a recognized close living kin (spouse/parent/adult child/adult sibling) to test against")) return t.Line();

         // Isolate: clear whichever kin we are about to test so a leftover cap-eviction from
         // earlier self-tests cannot mask this scenario's own plant.
         foreach (Hero kin in CloseKinCandidatesOf(victim))
            GrudgeBehavior.Instance.ClearGrudgesFor(kin.StringId);

         string cause = $"[SELFTEST] {Hero.MainHero.Name} had {victim.Name} put to death.";
         (int considered, int planted) = ExecutedKinGrudgeBehavior.PlantOnKinOf(victim, cause, today);

         t.Note($"{victim.Name}: {considered} kin considered, {planted} vendetta grudge(s) planted.");
         t.Expect("kinConsidered", considered > 0, true);
         t.Expect("kinPlanted", planted > 0, true);

         Hero plantedKin = CloseKinCandidatesOf(victim)
            .FirstOrDefault(k => GrudgeBehavior.Instance.GrudgesFor(k.StringId).Any(g => g.Source == GrudgeSource.ExecutedKin && g.EventDay == today));

         if (t.Skip(plantedKin == null, "planted grudge not found back on any candidate kin")) return t.Line();

         Grudge vendetta = GrudgeBehavior.Instance.GrudgesFor(plantedKin.StringId)
                                         .LastOrDefault(g => g.Source == GrudgeSource.ExecutedKin && g.EventDay == today);

         t.Note($"{plantedKin.Name}: rigidity={vendetta?.Rigidity}, magnitude={vendetta?.Magnitude}, resolutionKind={vendetta?.ResolutionKind}");
         t.Expect("vendettaRigidityMax", vendetta?.Rigidity, GrudgePolicy.VendettaRigidity);
         t.Expect("vendettaUnresolvable", vendetta != null && !GrudgePolicy.IsResolvable(vendetta), true);
         t.Expect("vendettaKnownToPlayer", vendetta?.KnownToPlayer, true);

         return t.Line();
      }

      /// <summary>
      ///   Mirrors <c>ExecutedKinGrudgeBehavior.CloseLivingKinOf</c> (private to that class) just enough
      ///   for this self-test to locate a real campaign lord with recognized close living kin, without
      ///   needing to expose the engine behavior's own enumeration publicly.
      /// </summary>
      private static IEnumerable<Hero> CloseKinCandidatesOf(Hero victim)
      {
         if (victim.Spouse is {IsAlive: true} spouse && spouse != Hero.MainHero) yield return spouse;
         if (victim.Father is {IsAlive: true} father && father != Hero.MainHero) yield return father;
         if (victim.Mother is {IsAlive: true} mother && mother != Hero.MainHero) yield return mother;

         if (victim.Children != null)
            foreach (Hero child in victim.Children)
               if (child is {IsAlive: true, IsChild: false} && child != Hero.MainHero) yield return child;

         if (victim.Siblings != null)
            foreach (Hero sibling in victim.Siblings)
               if (sibling is {IsAlive: true, IsChild: false} && sibling != Hero.MainHero) yield return sibling;
      }

      /// <summary>
      ///   Exercises the LIVE UnblessedMarriage plant + clear wiring end to end, no synthetic
      ///   Tier2GrudgeRules calls: marries the player to a test spouse WITHOUT ever seeding a
      ///   MarriageApprovalBehavior blessing (the real OnBeforeHeroesMarried hook then reads the match as
      ///   unapproved and chills the family), asserts a live UnblessedMarriage grudge lands on a kinsman who
      ///   actually disapproved, then ends the marriage via the real end_own_marriage action and asserts the
      ///   very same grudge RESOLVES (per the rule's own doc: cleared, not merely decayed, the instant the
      ///   marriage ends). Best-effort: skips cleanly on a barren campaign (no eligible test spouse, or one
      ///   with no other living adult kinsman in their birth clan to disapprove).
      /// </summary>
      private static string TestGrudgeUnblessedMarriage(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_unblessed_marriage");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(MarriageApprovalBehavior.Instance == null, "MarriageApprovalBehavior not active")) return t.Line();
         if (t.Abort(DivorceProcessBehavior.Instance == null, "DivorceProcessBehavior not active")) return t.Line();

         Hero spouse = FindTestSpouseForPlayer();

         if (t.Skip(spouse == null, "no eligible test spouse found to marry the player")) return t.Line();

         Clan family = spouse.Clan;
         Hero kin = family?.Heroes?.FirstOrDefault(h => h != spouse && h is {IsAlive: true} && !h.IsChild);

         if (t.Skip(kin == null, "the test spouse's birth clan has no other living adult kinsman to disapprove")) return t.Line();

         // Isolate: clear whichever kin we are about to test so a leftover plant from an earlier
         // grudge_unblessed_test run in this same session cannot mask this scenario's own plant.
         GrudgeBehavior.Instance.ClearGrudgesFor(kin.StringId);

         // Deliberately NEVER call MarriageApprovalBehavior.Instance.Grant: this match is unblessed by
         // construction, exercising the exact real path a fresh in-game match with no seeded blessing takes.
         MarriageAction.Apply(Hero.MainHero, spouse);

         if (t.Skip(Hero.MainHero.Spouse != spouse,
                "could not seed the marriage on this campaign (native MarriageAction did not take)")) return t.Line();

         bool grudgePlanted = GrudgeBehavior.Instance.GrudgesFor(kin.StringId)
                                            .Any(g => g.Source == GrudgeSource.UnblessedMarriage && g.TargetId == Hero.MainHero.StringId && !g.IsResolved);

         t.Note($"spouse={spouse.Name}, kin={kin.Name}: grudgePlanted={grudgePlanted}");
         t.Expect("unblessedMarriageGrudgePlantedOnKin", grudgePlanted, true);

         NpcProfile profile = GetOrCreate(behavior, spouse);
         ArrangeCleanNpc(behavior, profile, spouse);

         behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "end_own_marriage"}, spouse, new ActionContext(profile, null));

         bool dissolved = Hero.MainHero.Spouse == null;
         bool grudgeResolved = GrudgeBehavior.Instance.GrudgesFor(kin.StringId)
                                            .Any(g => g.Source == GrudgeSource.UnblessedMarriage && g.TargetId == Hero.MainHero.StringId && g.IsResolved);

         t.Note($"after end_own_marriage: dissolved={dissolved}, grudgeResolved={grudgeResolved}");
         t.Expect("dissolvedAfterDivorce", dissolved, true);
         t.Expect("unblessedMarriageGrudgeClearedOnDivorce", grudgeResolved, true);

         return t.Line();
      }

      // ── Grudges: the Tier 2 engine wiring (Arrange mirrors TestDeliverPrisoner for the ──
      // ── DeliveredPrisoner half — real give_prisoner path; the CaptiveAbuse half exercises ──
      // ── the pure rule + the real TryPlant store call directly, the deterministic core shared ──
      // ── by both live plant sites, BannerlordGameStateBridge's captor-scene close and the ──
      // ── companion-victim scene close — without needing the chat UI or an LLM) ──────────────

      private static string TestGrudgeTier2(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_tier2");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();

         // ── DeliveredPrisoner: capture a free lord, seed the deliver_prisoner bargain on a
         // receiving lord's profile, then run give_prisoner through the REAL bridge. Prefers a
         // receiver who is an actual enemy of the captive, so the heavier "sold to my enemy"
         // branch is exercised when the live campaign happens to offer one; falls back to any
         // receiver otherwise (DeliveredPrisonerGrudge ALWAYS forms regardless of that branch).
         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(captive == null, "no free lord to capture")) return t.Line();

         Hero receiver = Hero.AllAliveHeroes.FirstOrDefault(h =>
                            h != Hero.MainHero &&
                            h != captive &&
                            h.IsLord &&
                            h.Clan != Clan.PlayerClan &&
                            h.MapFaction != null &&
                            captive.MapFaction != null &&
                            FactionManager.IsAtWarAgainstFaction(h.MapFaction, captive.MapFaction)) ??
                         Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != captive && h.IsLord && h.Clan != Clan.PlayerClan);

         if (t.Skip(receiver == null, "no receiving lord available")) return t.Line();

         bool toEnemy = receiver.MapFaction != null &&
                        captive.MapFaction != null &&
                        FactionManager.IsAtWarAgainstFaction(receiver.MapFaction, captive.MapFaction);

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);

         NpcProfile receiverProfile = GetOrCreate(behavior, receiver);
         var quest = new InformalQuest {
            Type = QuestType.DeliverPrisoner,
            Description = $"[SELFTEST] Hand {captive.Name} to {receiver.Name}.",
            TargetHeroId = captive.StringId,
            TargetHeroName = captive.Name?.ToString(),
            IssuedOnDay = behavior.GameStateBridge.GetCurrentGameDay(),
            RewardGold = 500,
            RewardRelation = 5,
            Status = QuestStatus.Active
         };
         receiverProfile.ActiveQuests.Add(quest);
         behavior.Store.Set(receiverProfile);

         behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "give_prisoner"}, receiver, new ActionContext(receiverProfile, null));

         bool deliveredPrisonerGrudgeExists = GrudgeBehavior.Instance.GrudgesFor(captive.StringId)
                                                            .Any(g => g.Source == GrudgeSource.DeliveredPrisoner);

         t.Note($"{captive.Name} -> {receiver.Name} (toEnemy={toEnemy}): deliveredPrisonerGrudgeExists={deliveredPrisonerGrudgeExists}");
         t.Expect("deliveredPrisonerGrudgeFormed", deliveredPrisonerGrudgeExists, true);

         // ── CaptiveAbuse: a severity-3, player-as-perpetrator plant (the rule's ceiling case).
         Hero abuseVictim = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != captive && h != receiver && h.IsLord);

         if (t.Skip(abuseVictim == null, "no third lord available for the CaptiveAbuse plant")) return t.Line();

         var abuseFacts = new CaptiveAbuseFacts {Severity = 3, PlayerWasThePerpetrator = true};
         GrudgeProposal abuseProposal = Tier2GrudgeRules.CaptiveAbuseGrudge(abuseFacts);
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         bool planted = GrudgeBehavior.Instance.TryPlant(abuseVictim, Hero.MainHero, GrudgeSource.CaptiveAbuse,
            "[SELFTEST] the player tortured me while I was helpless.", abuseProposal.Magnitude, abuseProposal.KnownToPlayer, today);
         bool captiveAbuseGrudgeExists = GrudgeBehavior.Instance.GrudgesFor(abuseVictim.StringId)
                                                       .Any(g => g.Source == GrudgeSource.CaptiveAbuse && g.EventDay == today);

         t.Note($"{abuseVictim.Name}: planted={planted}, magnitude={abuseProposal.Magnitude}, captiveAbuseGrudgeExists={captiveAbuseGrudgeExists}");
         t.Expect("captiveAbuseGrudgeFormed", captiveAbuseGrudgeExists, true);

         return t.Line();
      }

      // ── Grudges: sway_opinion's Tier 2 LIVE plant site (Arrange mirrors TestRpSway: resolve an NPC ──
      // ── + 3rd-party pair, real sway_opinion(against) via the bridge) -> assert the exact same Rivalry ──
      // ── rumour SwayOpinionVerb records is now watched by SwayExposureBehavior -> force the reveal ──
      // ── (bypassing only the awareness TIMING, never the SwayExposedGrudge rule nor TryPlant's own ──
      // ── guardrails) -> assert a hidden SwayExposed grudge lands on the target ────────────────────────

      private static string TestGrudgeSway(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_sway");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(SwayExposureBehavior.Instance == null, "SwayExposureBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         (Hero npc, Hero target)? pair = ResolveSwayPair();

         if (t.Skip(pair == null, "need an NPC + a distinct third lord to sway an opinion of")) return t.Line();
         (Hero npc, Hero target) = pair.Value;

         NpcProfile profile = GetOrCreate(behavior, npc);

         if (t.Skip(!CourtActionResolver.CanTeachSway(npc, profile), "NPC not eligible to teach sway_opinion")) return t.Line();

         // SelectSwayTarget picks the most-negative rival, which can already sit at the -100 engine floor
         // where a further drop clamps to nothing, give the relation headroom, mirrors TestRpSway.
         if (npc.GetRelation(target) <= -90)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, target, -50 - npc.GetRelation(target), false);

         var today = (int) CampaignTime.Now.ToDays;
         int pendingBefore = SwayExposureBehavior.Instance.Pending.Count(p => p.TargetId == target.StringId);

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "sway_opinion",
               Parameters = new Dictionary<string, string> {{"target", target.Name.ToString()}, {"stance", "against"}}
            }, npc, new ActionContext(profile, null));

         int pendingAfter = SwayExposureBehavior.Instance.Pending.Count(p => p.TargetId == target.StringId);

         // The real reveal timing is probabilistic/geographic (WorldEventStore.IsAwareOf); force it now,
         // exactly the same Tier2GrudgeRules.SwayExposedGrudge + GrudgeBehavior.TryPlant call the daily
         // tick would eventually make, without waiting on the awareness model.
         string revealResult = SwayExposureBehavior.Instance.DebugForceReveal(target);
         Grudge swayGrudge = GrudgeBehavior.Instance.GrudgesFor(target.StringId)
                                          .FirstOrDefault(g => g.Source == GrudgeSource.SwayExposed && g.EventDay == today);

         t.Note($"{npc.Name} vs {target.Name}: pendingQueued={pendingAfter > pendingBefore}, reveal=\"{revealResult}\", " +
                $"swayExposedGrudgeExists={swayGrudge != null}, knownToPlayer={swayGrudge?.KnownToPlayer}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("pendingWatchQueued", pendingAfter > pendingBefore, true);
         t.Expect("swayExposedGrudgeFormed", swayGrudge != null, true);
         t.Expect("hiddenFromPlayer", swayGrudge?.KnownToPlayer, false);

         return t.Line();
      }

      /// <summary>
      ///   Companion-victim Phase 3: the tormented companion may hold TWO grudges from one scene, CaptiveAbuse
      ///   against the PLAYER (coerced / did not protect) and CaptorCruelty against the CAPTOR (revenge). They
      ///   share a holder and a day, so this proves the distinct sources keep both from being collapsed by the
      ///   holder+source+day dedup.
      /// </summary>
      private static string TestCompanionRevenge(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_revenge");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         Hero holder = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && GrudgeBehavior.Instance.LiveGrudgesFor(h, today).Count == 0);
         if (t.Skip(holder == null, "no grudge-free lord to stand in as the tormented companion")) return t.Line();

         Hero captor = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != holder && h.IsLord);
         if (t.Skip(captor == null, "no second lord to stand in as the captor")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, holder);
         ArrangeCleanNpc(behavior, profile, holder);

         bool vsPlayer = GrudgeBehavior.Instance.TryPlant(holder, Hero.MainHero, GrudgeSource.CaptiveAbuse,
            "[SELFTEST] I was made to suffer while they watched.", 45, true, today);
         bool vsCaptor = GrudgeBehavior.Instance.TryPlant(holder, captor, GrudgeSource.CaptorCruelty,
            "[SELFTEST] the captor broke me; I will see them answer.", 50, true, today);

         IReadOnlyList<Grudge> held = GrudgeBehavior.Instance.GrudgesFor(holder.StringId);
         bool playerGrudge = held.Any(g => g.Source == GrudgeSource.CaptiveAbuse && g.TargetId == Hero.MainHero.StringId && !g.IsResolved);
         bool captorGrudge = held.Any(g => g.Source == GrudgeSource.CaptorCruelty && g.TargetId == captor.StringId && !g.IsResolved);

         t.Note($"holder={holder.Name}, captor={captor.Name}: planted vsPlayer={vsPlayer}, vsCaptor={vsCaptor}");
         t.Expect("bothPlanted", vsPlayer && vsCaptor, true);
         t.Expect("playerGrudgeLive", playerGrudge, true);
         t.Expect("captorRevengeGrudgeLive", captorGrudge, true);

         return t.Line();
      }

      /// <summary>
      ///   Prisoner trade, the buy direction end to end: a real lord is taken captive into the player's
      ///   party, a second lord (the buyer) purchases him through the real bridge action, and the test
      ///   asserts the gold moved to the player (clamped by PrisonerTradePolicy around the ransom anchor)
      ///   and the captive left the player's custody. Cleanup releases the captive whatever happened.
      /// </summary>
      private static string TestPrisonerTrade(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("prisoner_trade");

         if (t.Abort(Hero.MainHero == null || MobileParty.MainParty == null, "no player party")) return t.Line();

         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null && h.Clan?.Leader != h);
         if (t.Skip(captive == null, "no free lord to take captive")) return t.Line();

         Hero buyer = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != captive && h.IsLord && !h.IsPrisoner && h.Clan != captive.Clan);
         if (t.Skip(buyer == null, "no second lord to stand as the buyer")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, buyer);

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);
         if (t.Abort(!captive.IsPrisoner, "could not take the test captive")) return t.Line();

         try
         {
            int goldBefore = Hero.MainHero.Gold;
            int anchor = Verbs.BuyPrisonerVerb.RansomAnchor(captive);

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {
                  Type = "buy_prisoner",
                  Parameters = new Dictionary<string, string> {
                     {"target", captive.Name.ToString()},
                     {"price", "999999"} // an absurd ask, so the clamp itself is what gets proven
                  }
               }, buyer, new ActionContext(profile, null));

            int paid = Hero.MainHero.Gold - goldBefore;
            int expected = CalradiaRemembers.Logic.PrisonerTradePolicy.ClampPrice(999999, anchor);
            bool leftCustody = Verbs.BuyPrisonerVerb.PlayerHeldHeroPrisoners().All(h => h != captive);

            t.Note($"captive={captive.Name}, buyer={buyer.Name}, anchor={anchor}, paid={paid} (expected {expected}), outcome=\"{outcome?.Message}\"");
            t.Expect("outcomePositive", outcome?.IsPositive, true);
            t.Expect("paidTheClampedPrice", paid == expected, true);
            t.Expect("captiveLeftPlayerCustody", leftCustody, true);
         }
         finally
         {
            // Whatever happened, the test lord goes free so the campaign is left unharmed.
            try { if (captive.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(captive, buyer); }
            catch { /* best-effort cleanup */ }
         }

         return t.Line();
      }

      /// <summary>
      ///   Appearance pillar: proves the engine extraction + pure composer + prose store compose a usable block.
      ///   Reads the live player's body facts, composes both voices with a scrap of authored prose, and checks
      ///   the prose is carried and the Self voice is second person.
      /// </summary>
      private static string TestAppearance(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("appearance");

         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();
         if (t.Abort(AppearanceProseBehavior.Instance == null, "appearance store not active (load a campaign first)")) return t.Line();

         AppearanceFacts facts = AppearanceFactsExtractor.Extract(Hero.MainHero);
         string subject = AppearanceComposer.Compose(facts, "moves with a slight limp", AppearanceVoice.Subject);
         string self = AppearanceComposer.Compose(facts, null, AppearanceVoice.Self);

         t.Note($"subject=\"{subject}\" | self=\"{self}\"");
         t.Expect("factsExtracted", facts != null, true);
         t.Expect("authoredProseCarried", subject.Contains("limp"), true);
         t.Expect("selfVoiceSecondPerson", self.Length == 0 || self.StartsWith("You are"), true);

         return t.Line();
      }

      /// <summary>
      ///   Viable-quest referential: proves the world scan yields a grounded, non-empty menu for a live
      ///   lord, and that its named settlement targets truly resolve (the by-construction guarantee that
      ///   QuestFactory will ground whatever the model offers from the menu).
      /// </summary>
      private static string TestQuestMenu(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("quest_menu");

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Skip(giver == null, "no live free lord in this campaign")) return t.Line();

         int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string menu = Quests.ViableQuestScanner.BuildMenu(giver, day);

         t.Note($"giver={giver.Name} ({giver.MapFaction?.Name}), menu:\n{menu}");
         t.Expect("menuNonEmpty", !string.IsNullOrWhiteSpace(menu), true);

         if (string.IsNullOrWhiteSpace(menu)) return t.Line();

         // Every line must open with a known type token, and a settlement target must truly resolve.
         var knownTokens = new[] {
            "bandit_hideout", "bandit_clear", "attack_lord", "capture_prisoner", "attack_faction",
            "raid_village", "scout_army", "rescue_prisoner", "deliver_letter"
         };
         var allKnown = true;
         var settlementsResolve = true;
         foreach (string line in menu.Split('\n'))
         {
            string trimmed = line.Trim();

            if (trimmed.Length == 0) continue;
            if (!knownTokens.Any(tok => trimmed.StartsWith("- " + tok, StringComparison.Ordinal))) allKnown = false;

            int idx = trimmed.IndexOf("target_settlement: ", StringComparison.Ordinal);
            if (idx < 0) continue;

            string name = trimmed.Substring(idx + "target_settlement: ".Length);
            name = name.Substring(0, name.IndexOf(')') is var p and >= 0 ? p : name.Length);
            if (!Settlement.All.Any(s => string.Equals(s?.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase)))
               settlementsResolve = false;
         }

         t.Expect("allTypeTokensKnown", allKnown, true);
         t.Expect("settlementTargetsResolve", settlementsResolve, true);

         return t.Line();
      }

      // ── Conversation smoke tests (each = 1 real LLM call; relevance judged from the log) ──

      private static string TestLlmDialogue(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("llm_dialogue");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured — set an API key + model, or an OpenAI-compatible endpoint")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         const string question = "Greetings. Tell me plainly — what is the mood of the realm these days, and how do you regard me?";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

         var (ok, reply, err) = AskLlm(behavior, "llm_dialogue", npc, profile, ctx, question);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         t.Note($"{npc.Name} replied {reply.Length} chars | A: {Show(reply)}");
         t.Expect("gotNonEmptyReply", reply.Length > 0, true);

         return t.Line();
      }

      private static string TestMissionRegister(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("mission_register");

         Hero giver = ConversationNpc();

         if (t.Skip(giver == null, "no NPC to host the mission")) return t.Line();

         GetOrCreate(behavior, giver); // CR needs a profile for the giver to host the quest
         string id = "selftest_mission_" + Guid.NewGuid().ToString("N");
         var mission = new ExternalMission {
            Id = id, SourceMod = "SelfTest", GiverHeroId = giver.StringId,
            Title = "Win the autumn race",
            Description = "[SELFTEST] Take the autumn race at Pen Cannoc and the purse is yours.",
            RewardGold = 5000, RewardRelation = 3, DeadlineDays = 30
         };

         MissionBoardResult first = MissionBoard.Register(mission);

         if (t.Abort(first != MissionBoardResult.Registered, $"register failed: {first} (giver {giver.Name}/{giver.StringId})")) return t.Line();

         InformalQuest quest = behavior.Store.Get(giver.StringId)?.ActiveQuests
                                       .FirstOrDefault(q => q.Type == QuestType.External && q.ExternalId == id);
         t.Expect("issuedOnGiver", quest != null, true);

         MissionBoardResult dup = MissionBoard.Register(mission); // same id -> deduped
         t.Expect("dedupRejected", dup, MissionBoardResult.RejectedDuplicate);

         MissionBoardResult done = MissionBoard.Complete(id); // the external mod's completion signal
         t.Expect("completeStamped", done, MissionBoardResult.Completed);

         InformalQuest after = behavior.Store.Get(giver.StringId)?.ActiveQuests
                                       .FirstOrDefault(q => q.ExternalId == id);
         t.Expect("awaitingRewardAfterComplete", after != null && after.IsAwaitingReward, true);

         t.Note($"giver={giver.Name}, reward {quest?.RewardGold}g/+{quest?.RewardRelation} — now report to {giver.Name} in chat to collect");

         return t.Line();
      }

      private static string TestMissionRewardLoop(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("mission_reward");

         Hero giver = ConversationNpc();

         if (t.Skip(giver == null || Hero.MainHero == null, "no NPC to host the mission, or no player hero")) return t.Line();

         GetOrCreate(behavior, giver);
         // Isolation: drop any External quest a prior test (mission_register) left awaiting reward on this
         // giver, so our report completes OUR mission, not whichever External quest matched first.
         NpcProfile giverProfile = behavior.Store.Get(giver.StringId);
         giverProfile?.ActiveQuests.RemoveAll(q => q.Type == QuestType.External);
         if (giverProfile != null) behavior.Store.Set(giverProfile);

         string id = "selftest_missionreward_" + Guid.NewGuid().ToString("N");
         const int rewardGold = 5000;
         const int rewardRelation = 4;
         var mission = new ExternalMission {
            Id = id, SourceMod = "SelfTest", GiverHeroId = giver.StringId,
            Title = "Win the autumn race",
            Description = "[SELFTEST] Take the autumn race at Pen Cannoc and the purse is yours.",
            RewardGold = rewardGold, RewardRelation = rewardRelation, DeadlineDays = 30
         };

         MissionBoardResult reg = MissionBoard.Register(mission);

         if (t.Abort(reg != MissionBoardResult.Registered, $"register failed: {reg} (giver {giver.Name}/{giver.StringId})")) return t.Line();
         MissionBoardResult sig = MissionBoard.Complete(id);

         if (t.Abort(sig != MissionBoardResult.Completed, $"external complete signal failed: {sig}")) return t.Line();

         // The player reports back: drive the SAME path the chat runs when the giver emits [QUEST_COMPLETE]
         // on seeing the verified evidence — deterministic, no LLM, but the REAL reward pipeline.
         NpcProfile reported = behavior.Store.Get(giver.StringId);

         if (t.Abort(reported == null, "giver profile vanished")) return t.Line();

         int goldBefore = Hero.MainHero.Gold;
         int regardBefore = reported.ReputationWithPlayer;
         var ack = new ParsedResponse {Dialogue = "", QuestCompleted = new QuestCompletionClaim {Type = QuestType.External}};
         QuestService.ApplyResponse(reported, giver, ack, behavior.GameStateBridge.GetCurrentGameDay(),
            behavior.GameStateBridge, behavior.Store, (_, __) => { });

         int goldAfter = Hero.MainHero.Gold;
         int regardAfter = reported.ReputationWithPlayer;
         InformalQuest after = behavior.Store.Get(giver.StringId)?.ActiveQuests.FirstOrDefault(q => q.ExternalId == id);
         int expectedRegardGain = Math.Min(100, regardBefore + rewardRelation) - regardBefore;

         t.Note($"giver={giver.Name}: gold {goldBefore}->{goldAfter} (+{goldAfter - goldBefore}), regard {regardBefore}->{regardAfter}; quest {after?.Status.ToString() ?? "gone"}");
         t.Expect("questCompleted", after != null && after.Status == QuestStatus.Completed, true);
         t.Expect("goldPaid", goldAfter - goldBefore, rewardGold);
         t.Expect("regardPaid", regardAfter - regardBefore, expectedRegardGain);

         return t.Line();
      }

      private static string TestPregnancy(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("pregnancy");

         Hero mother = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsFemale && !h.IsPregnant && h.Age >= 18f && h.Age <= 45f);

         if (t.Skip(mother == null, "no fertile (18-45, not pregnant) female NPC")) return t.Line();

         // Assert on CR's own success signal (a non-null note = the reflective MakePregnant created
         // the pregnancy), NOT Hero.IsPregnant — a vanilla flag CR's reflective path may not flip.
         string note = PregnancyService.TryConceive(mother, Hero.MainHero, true);
         t.Note($"mother={mother.Name}(age {mother.Age:0}), IsPregnant={mother.IsPregnant}");
         t.Expect("conceptionCreated", note != null, true);

         // Age gate (negative): an out-of-window female returns null (refused) even with force
         Hero infertile = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsFemale && !h.IsPregnant && (h.Age < 18f || h.Age > 45f));
         if (infertile != null)
         {
            string refused = PregnancyService.TryConceive(infertile, Hero.MainHero, true);
            t.Note($"ageGate={infertile.Name}(age {infertile.Age:0})");
            t.Expect("infertileRefused", refused == null, true);
         }

         return t.Line();
      }

      // ── Tests ─────────────────────────────────────────────────────────────

      private static string TestProvideGold(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("providegold");

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(giver == null, "no live lords in this campaign")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         QuestService.ApplyResponse(
            profile, giver,
            new ParsedResponse {
               Dialogue = "",
               QuestGiven = new QuestProposal {
                  Type = QuestType.ProvideGold,
                  Description = $"[SELFTEST] {giver.Name} asks you for 500 denars.",
                  DeadlineDays = 30,
                  RewardRelation = 5
               }
            },
            behavior.GameStateBridge.GetCurrentGameDay(),
            behavior.GameStateBridge, behavior.Store, null);

         profile = behavior.Store.Get(giver.StringId);
         int seeded = profile.ActiveQuests.Count(q => q.Type == QuestType.ProvideGold && q.IsOutstanding);

         if (t.Abort(seeded == 0, $"quest not seeded for {giver.Name}")) return t.Line();

         behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "take_gold",
               Parameters = new Dictionary<string, string> {{"amount", "500"}}
            },
            giver, null);

         profile = behavior.Store.Get(giver.StringId);
         bool stillOpen = profile.ActiveQuests.Any(q => q.Type == QuestType.ProvideGold && q.IsOutstanding);
         t.Note($"giver={giver.Name}");
         t.Expect("stamped", !stillOpen, true);

         return t.Line();
      }

      private static string TestQuestHideout(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("quest_hideout");

         bool Infested(Settlement h)
         {
            try { return h.Hideout != null && h.Hideout.IsInfested; }
            catch { return false; }
         }

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
         if (t.Skip(giver == null, "no live lords in this campaign")) return t.Line();

         // Pick a town that actually has an infested lair in range, so the real pipeline can bind one and the
         // assertions are meaningful. A slightly tighter radius than QuestFactory's guarantees it will also find it.
         Settlement town = Settlement.All.FirstOrDefault(s =>
            s.IsTown && Settlement.All.Any(h => h.IsHideout && Infested(h) && h.GetPosition2D.Distance(s.GetPosition2D) <= 70f));

         if (t.Skip(town == null, "no town has an infested hideout in range; the quest is correctly refusable only")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         QuestService.ApplyResponse(
            profile, giver,
            new ParsedResponse {
               Dialogue = "",
               QuestGiven = new QuestProposal {
                  Type = QuestType.BanditHideout,
                  TargetSettlement = town.Name?.ToString(),
                  Description = $"[SELFTEST] {giver.Name} asks you to clear a lair.",
                  RewardGold = 200
               }
            },
            behavior.GameStateBridge.GetCurrentGameDay(),
            behavior.GameStateBridge, behavior.Store, null);

         profile = behavior.Store.Get(giver.StringId);
         InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.BanditHideout && q.IsOutstanding);

         if (t.Abort(quest == null, $"the pipeline seeded no hideout quest for {giver.Name} despite an in-range lair")) return t.Line();

         Settlement lair = Settlement.All.FirstOrDefault(s => s.StringId == quest.TargetHideoutId);
         t.Note($"giver={giver.Name} anchor={quest.TargetSettlement} dir='{quest.DirectionHint}'");
         t.Expect("boundRealLair", !string.IsNullOrEmpty(quest.TargetHideoutId), true);
         t.Expect("hasBearing", !string.IsNullOrWhiteSpace(quest.DirectionHint), true);
         t.Expect("lairIsRealHideout", lair != null && lair.IsHideout, true);

         return t.Line();
      }

      private static string TestRecruit(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("recruit");

         var playerClan = Clan.PlayerClan;

         if (t.Abort(playerClan == null, "no player clan")) return t.Line();

         // Pick a lord the recruitment gate would actually accept, so a real move is assertable
         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h =>
         {
            if (h == Hero.MainHero || !h.IsLord || h.Clan == playerClan) return false;
            LordRecruitmentFacts facts = LordRecruitmentFactsBuilder.Build(h);

            return facts != null && LordRecruitment.IsEligible(facts);
         });

         if (t.Skip(lord == null, "no eligible lord to recruit (need a landless/broken/estranged lord of tier <= yours)")) return t.Line();

         string oldClan = lord.Clan?.Name?.ToString() ?? "—";

         // The bridge re-validates the gate ("the prompt is advice, the bridge is law")
         behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "join_clan"}, lord, null);

         t.Note($"{lord.Name}: {oldClan} -> {lord.Clan?.Name?.ToString() ?? "—"}");
         t.Expect("joinedYourClan", lord.Clan == playerClan, true);

         return t.Line();
      }

      // ── RP with teeth, increment 1: give_influence / lend_troops / sway_opinion / release_prisoner ──

      private static string TestRpInfluence(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("rp_influence");

         (Hero giver, NpcProfile profile, int amount)? seed = SeedInfluenceEligible(behavior);

         if (t.Skip(seed == null, "no live lord of another clan eligible to lend influence, even after forcing relation + clan reserve")) return t.Line();
         (Hero giver, NpcProfile profile, int amount) = seed.Value;

         float giverBefore = giver.Clan.Influence;
         float playerBefore = Clan.PlayerClan.Influence;

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "give_influence"}, giver, new ActionContext(profile, null));

         float giverAfter = giver.Clan.Influence;
         float playerAfter = Clan.PlayerClan.Influence;
         NpcProfile after = behavior.Store.Get(giver.StringId);
         bool marked = after != null &&
                       after.Events.Any(e =>
                          !string.IsNullOrEmpty(e.summary) && e.summary.IndexOf(CourtActionResolver.InfluenceMarker, StringComparison.Ordinal) >= 0);

         // The very same day, right after stamping the marker — the cooldown must refuse a repeat.
         ActionOutcome again = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "give_influence"}, giver, new ActionContext(after, null));
         bool stillEligible = CourtActionResolver.TryGiveInfluence(giver, after, out _);

         t.Note($"giver={giver.Name} amount={amount}, playerInfluence {playerBefore:0}->{playerAfter:0}, giverInfluence {giverBefore:0}->{giverAfter:0}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("playerRose", playerAfter - playerBefore, (float) amount);
         t.Expect("giverFell", giverBefore - giverAfter, (float) amount);
         t.Expect("markerStamped", marked, true);
         t.Expect("secondRefused", again == null || !again.IsPositive, true);
         t.Expect("resolverNowIneligible", !stillEligible, true);

         return t.Line();
      }

      private static string TestRpRelease(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("rp_release");

         (Hero captor, NpcProfile profile, Hero victim)? seed = SeedReleaseEligible(behavior);

         if (t.Skip(seed == null, "no lord's party available to hold a third-party hero captive")) return t.Line();
         (Hero captor, NpcProfile profile, Hero victim) = seed.Value;

         int relBefore = victim.GetRelation(Hero.MainHero);

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "release_prisoner",
               Parameters = new Dictionary<string, string> {{"target", victim.Name.ToString()}}
            }, captor, new ActionContext(profile, null));

         int relAfter = victim.GetRelation(Hero.MainHero);

         t.Note($"captor={captor.Name}, freed={victim.Name}, relation {relBefore}->{relAfter}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("noLongerPrisoner", !victim.IsPrisoner, true);
         t.Expect("relationBumped", relAfter - relBefore, CourtActionPolicy.ReleasePrisonerRelationBump);

         return t.Line();
      }

      private static string TestRpSway(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("rp_sway");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         (Hero npc, Hero target)? pair = ResolveSwayPair();

         if (t.Skip(pair == null, "need an NPC + a distinct third lord to sway an opinion of")) return t.Line();
         (Hero npc, Hero target) = pair.Value;

         NpcProfile profile = GetOrCreate(behavior, npc);

         if (t.Skip(!CourtActionResolver.CanTeachSway(npc, profile), "NPC not eligible to teach sway_opinion")) return t.Line();

         // SelectSwayTarget picks the most-negative rival, which can already sit at the -100 engine floor
         // where a further drop clamps to nothing. Give the relation headroom so the delta is measurable.
         if (npc.GetRelation(target) <= -90)
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, target, -50 - npc.GetRelation(target), false);

         int relBefore = npc.GetRelation(target);
         int eventsBefore = WorldEventStore.Instance.Count;

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "sway_opinion",
               Parameters = new Dictionary<string, string> {{"target", target.Name.ToString()}, {"stance", "against"}}
            }, npc, new ActionContext(profile, null));

         int relAfter = npc.GetRelation(target);
         int eventsAfter = WorldEventStore.Instance.Count;
         NpcProfile after = behavior.Store.Get(npc.StringId);
         bool marked = after != null &&
                       after.Events.Any(e =>
                          !string.IsNullOrEmpty(e.summary) && e.summary.IndexOf(CourtActionResolver.SwayMarker(target.StringId), StringComparison.Ordinal) >= 0);

         // Same pair, same day — the per-target cooldown must refuse a repeat.
         ActionOutcome again = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "sway_opinion",
               Parameters = new Dictionary<string, string> {{"target", target.Name.ToString()}, {"stance", "against"}}
            }, npc, new ActionContext(after, null));

         t.Note($"{npc.Name} vs {target.Name}: relation {relBefore}->{relAfter}, worldEvents {eventsBefore}->{eventsAfter}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("relationDropped", relAfter - relBefore, CourtActionPolicy.SwayAgainstDelta);
         t.Expect("markerStamped", marked, true);
         t.Expect("worldEventRecorded", eventsAfter > eventsBefore, true);
         t.Expect("repeatRefused", again == null || !again.IsPositive, true);

         return t.Line();
      }

      private static string TestRpTroops(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("rp_troops");

         (Hero lord, NpcProfile profile, int expected)? seed = SeedTroopsEligible(behavior);

         if (t.Skip(seed == null, "no lord leading a healthy-enough party nearby, even after forcing relation")) return t.Line();
         (Hero lord, NpcProfile profile, int expected) = seed.Value;

         MobileParty npcParty = lord.PartyBelongedTo;
         MobileParty playerParty = MobileParty.MainParty;

         if (t.Abort(npcParty == null || playerParty == null, "a party vanished between eligibility check and execution")) return t.Line();

         int npcBefore = npcParty.MemberRoster.TotalManCount;
         int playerBefore = playerParty.MemberRoster.TotalManCount;
         int playerHeroesBefore = playerParty.MemberRoster.GetTroopRoster().Count(el => el.Character.IsHero);

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "lend_troops"}, lord, new ActionContext(profile, null));

         int npcAfter = npcParty.MemberRoster.TotalManCount;
         int playerAfter = playerParty.MemberRoster.TotalManCount;
         int playerHeroesAfter = playerParty.MemberRoster.GetTroopRoster().Count(el => el.Character.IsHero);
         NpcProfile after = behavior.Store.Get(lord.StringId);
         bool marked = after != null &&
                       after.Events.Any(e =>
                          !string.IsNullOrEmpty(e.summary) && e.summary.IndexOf(CourtActionResolver.TroopsMarker, StringComparison.Ordinal) >= 0);

         t.Note($"lord={lord.Name} expected={expected}, npcTroops {npcBefore}->{npcAfter}, playerTroops {playerBefore}->{playerAfter}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("playerGained", playerAfter - playerBefore, expected);
         t.Expect("npcLost", npcBefore - npcAfter, expected);
         t.Expect("noHeroesMoved", playerHeroesAfter, playerHeroesBefore);
         t.Expect("markerStamped", marked, true);

         return t.Line();
      }

      // ── give_troops / spend_influence (04/07/2026 — reciprocal of lend_troops / give_influence) ──

      private static string TestRpGiveTroops(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("rp_give_troops");

         (Hero lord, NpcProfile profile, int expected)? seed = SeedGiveTroopsEligible(behavior);

         if (t.Skip(seed == null, "no lord leading a genuinely under-strength party nearby, even after forcing relation")) return t.Line();
         (Hero lord, NpcProfile profile, int expected) = seed.Value;

         MobileParty npcParty = lord.PartyBelongedTo;
         MobileParty playerParty = MobileParty.MainParty;

         if (t.Abort(npcParty == null || playerParty == null, "a party vanished between eligibility check and execution")) return t.Line();

         int npcBefore = npcParty.MemberRoster.TotalManCount;
         int playerBefore = playerParty.MemberRoster.TotalManCount;
         var today = (int) CampaignTime.Now.ToDays;

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "give_troops"}, lord, new ActionContext(profile, null));

         int npcAfter = npcParty.MemberRoster.TotalManCount;
         int playerAfter = playerParty.MemberRoster.TotalManCount;
         NpcProfile after = behavior.Store.Get(lord.StringId);
         bool marked = after != null &&
                       after.Events.Any(e =>
                          !string.IsNullOrEmpty(e.summary) && e.summary.IndexOf(CourtActionResolver.GiveTroopsMarker, StringComparison.Ordinal) >= 0);

         IReadOnlyList<Appreciation> appreciations = AppreciationBehavior.Instance?.AppreciationsFor(lord.StringId) ?? Array.Empty<Appreciation>();
         bool appreciationPlanted = appreciations.Any(a => a.Source == AppreciationSource.TroopsOrInfluenceSpentForThem && a.EventDay == today);

         t.Note($"lord={lord.Name} expected={expected}, npcTroops {npcBefore}->{npcAfter}, playerTroops {playerBefore}->{playerAfter}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("npcGained", npcAfter - npcBefore, expected);
         t.Expect("playerLost", playerBefore - playerAfter, expected);
         t.Expect("markerStamped", marked, true);
         t.Expect("appreciationPlanted", appreciationPlanted, true);

         return t.Line();
      }

      private static string TestRpSpendInfluence(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("rp_spend_influence");

         (Hero npc, NpcProfile profile, int amount)? seed = SeedSpendInfluenceEligible(behavior);

         if (t.Skip(seed == null, "no live lord of another clan eligible to receive influence, even after forcing relation + need + player reserve")) return t.Line();
         (Hero npc, NpcProfile profile, int amount) = seed.Value;

         float npcBefore = npc.Clan.Influence;
         float playerBefore = Clan.PlayerClan.Influence;
         var today = (int) CampaignTime.Now.ToDays;

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "spend_influence"}, npc, new ActionContext(profile, null));

         float npcAfter = npc.Clan.Influence;
         float playerAfter = Clan.PlayerClan.Influence;
         NpcProfile after = behavior.Store.Get(npc.StringId);
         bool marked = after != null &&
                       after.Events.Any(e =>
                          !string.IsNullOrEmpty(e.summary) && e.summary.IndexOf(CourtActionResolver.SpendInfluenceMarker, StringComparison.Ordinal) >= 0);

         IReadOnlyList<Appreciation> appreciations = AppreciationBehavior.Instance?.AppreciationsFor(npc.StringId) ?? Array.Empty<Appreciation>();
         bool appreciationPlanted = appreciations.Any(a => a.Source == AppreciationSource.TroopsOrInfluenceSpentForThem && a.EventDay == today);

         // Same day, right after stamping the marker — the cooldown must refuse a repeat.
         ActionOutcome again = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "spend_influence"}, npc, new ActionContext(after, null));
         bool stillEligible = CourtActionResolver.TrySpendInfluence(npc, after, out _);

         t.Note($"npc={npc.Name} amount={amount}, playerInfluence {playerBefore:0}->{playerAfter:0}, npcInfluence {npcBefore:0}->{npcAfter:0}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("npcRose", npcAfter - npcBefore, (float) amount);
         t.Expect("playerFell", playerBefore - playerAfter, (float) amount);
         t.Expect("markerStamped", marked, true);
         t.Expect("appreciationPlanted", appreciationPlanted, true);
         t.Expect("secondRefused", again == null || !again.IsPositive, true);
         t.Expect("resolverNowIneligible", !stillEligible, true);

         return t.Line();
      }

      private static string TestScenario(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("scenario");

         ScenarioRuntime runtime = ScenarioBehavior.Instance?.Runtime;

         if (t.Abort(runtime == null, "scenario engine not ready")) return t.Line();

         Hero rhagaea = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h.Name != null && h.Name.ToString().IndexOf("Rhagaea", StringComparison.OrdinalIgnoreCase) >= 0);

         if (t.Skip(rhagaea == null, "Rhagaea not alive in this campaign — cannot fire the sample ON_DEATH rule")) return t.Line();

         var heroes = new List<Hero> {rhagaea};
         if (Hero.MainHero != null) heroes.Add(Hero.MainHero);
         var factions = new List<IFaction>();
         if (rhagaea.MapFaction != null) factions.Add(rhagaea.MapFaction);
         var ctx = new ScenarioContext(heroes, factions);

         ScenarioRule first = runtime.Fire(KeywordToken.Parse("ON_DEATH"), ctx);

         if (t.Skip(first == null, "no rule fired for ON_DEATH:Rhagaea (already sealed this campaign, or example pack not loaded — check cr.scenarios on a fresh save)")) return t.Line();

         ScenarioRule again = runtime.Fire(KeywordToken.Parse("ON_DEATH"), ctx);
         t.Note($"fired '{first.Id}', refire={(again == null ? "null" : again.Id)}");
         t.Expect("sealed", again == null, true);

         return t.Line();
      }

      private static string TestSchemeAssist(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("scheme_assist");

         if (t.Abort(SchemeStore.Instance == null, "SchemeStore not active")) return t.Line();


         // Pick actors NOT already entangled in an active scheme, so an earlier test's plot
         // (e.g. scheme_warn leaving one Discovered) can't taint the precondition here.
         bool Free(Hero h)
         {
            return !SchemeStore.Instance.Active.Any(s => s.PlotterId == h.StringId || s.TargetId == h.StringId);
         }


         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan && Free(h));
         // The target must NOT be allied with the player (relation < 20, different faction)
         Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != plotter && h != Hero.MainHero && h.IsLord && h.MapFaction != Hero.MainHero?.MapFaction && h.GetRelation(Hero.MainHero) < 20 && h.Clan != plotter?.Clan && Free(h));

         if (t.Skip(plotter == null || target == null, "need a fresh plotter + non-allied target (none unentangled)")) return t.Line();

         SchemeStore.Instance.ForcePlot(plotter, target, SchemeKind.Slander);
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(plotter, Hero.MainHero, 60, false); // plotter now regards the player
         int regard = plotter.GetRelation(Hero.MainHero);

         string msg = SchemeStore.Instance.PlayerAssists(plotter);

         if (t.Skip(msg == null, $"plot not recruitable (regard={regard}, need >=20; or alliance/state) — env-dependent")) return t.Line();

         Scheme scheme = SchemeStore.Instance.Active.FirstOrDefault(s => s.PlotterId == plotter.StringId);
         t.Note($"plotter={plotter.Name} -> {target.Name}, regard={regard}");
         // The aid advanced the plot (progress > 0) or carried it to completion (no longer active)
         t.Expect("plotAdvancedOrDone", scheme == null || scheme.Progress > 0, true);

         return t.Line();
      }

      private static string TestSchemeWarn(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("scheme_warn");

         if (t.Abort(SchemeStore.Instance == null, "SchemeStore not active")) return t.Line();

         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan);
         Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != plotter && h != Hero.MainHero && h.IsLord && h.Clan != null && h.Clan != plotter?.Clan && h.Clan != Clan.PlayerClan);

         if (t.Skip(plotter == null || target == null, "need 2+ lords from distinct non-player clans")) return t.Line();

         SchemeStore.Instance.ForcePlot(plotter, target, SchemeKind.Slander);
         int plotterBefore = behavior.GameStateBridge.GetPlayerRelation(plotter);
         int targetBefore = behavior.GameStateBridge.GetPlayerRelation(target);

         string msg = SchemeStore.Instance.TargetHeeds(target);

         if (t.Abort(msg == null, $"TargetHeeds({target.Name}) returned null — no eligible scheme after ForcePlot")) return t.Line();

         int plotterDelta = behavior.GameStateBridge.GetPlayerRelation(plotter) - plotterBefore;
         int targetDelta = behavior.GameStateBridge.GetPlayerRelation(target) - targetBefore;
         t.Note($"plotter={plotter.Name} delta={plotterDelta} | target={target.Name} delta={targetDelta}");
         t.Expect("plotterResents(<=-20)", plotterDelta <= -20, true);
         t.Expect("targetGrateful(>=16)", targetDelta >= 16, true);

         return t.Line();
      }

      private static string TestStanceDeed(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("stance_deed");

         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(npc == null, "no lord to move a stance on")) return t.Line();

         NpcProfile before = GetOrCreate(behavior, npc);
         int trustBefore = before.StanceTrust;

         // A proven deed (a freely given gift) raises Trust and must persist to the store
         StanceService.OnGiftReceived(npc);

         NpcProfile after = behavior.Store.Get(npc.StringId);
         int trustAfter = after?.StanceTrust ?? trustBefore;
         t.Note($"{npc.Name}: trust {trustBefore}->{trustAfter}");
         t.Expect("trustRoseAndPersisted", trustAfter > trustBefore, true);

         return t.Line();
      }

      private static string TestStanceLetter(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("stance_letter");

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to take the stance")) return t.Line();
         if (t.Skip(npc.Clan == Clan.PlayerClan, "NPC is in the player's own clan — no stance letters")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         int before = profile.SentLetters.Count(l => l.Reason == LetterReason.StanceHostility);
         profile.ReputationWithPlayer = -50; // real hostility (above lethal) -> an open-enmity Threat letter
         profile.StanceLastConsequenceDay = 0; // cooldown elapsed
         behavior.Store.Set(profile);

         bool fired = StanceConsequenceBehavior.DebugEvaluate(npc);

         NpcProfile after = behavior.Store.Get(npc.StringId);
         int afterCount = after?.SentLetters.Count(l => l.Reason == LetterReason.StanceHostility) ?? 0;

         t.Note($"{npc.Name}: rep -50 -> fired={fired}, StanceHostility letters {before}->{afterCount}, cooldownStamped={after?.StanceLastConsequenceDay}");
         t.Expect("letterFired", fired, true);
         t.Expect("hostilityLetterQueued", afterCount, before + 1);

         return t.Line();
      }

      /// <summary>
      ///   "Stance with teeth" increment 2: the Plot verdict must fire a REAL scheme against the player
      ///   (SchemeStore.ForcePlot), never a letter. StanceConsequencePolicy gates Plot on the lord's own
      ///   traits (dishonourable + calculating), which the self-test cannot rely on any given campaign
      ///   handing it, so it forces the trait levels deterministically (mirrors CharacterLoreBehavior's
      ///   own use of Hero.SetTraitLevel) rather than hunting for a naturally-matching lord.
      /// </summary>
      private static string TestStancePlot(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("stance_plot");

         if (t.Abort(SchemeStore.Instance == null, "SchemeStore not active")) return t.Line();

         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan &&
            !SchemeStore.Instance.Active.Any(s => s.PlotterId == h.StringId && s.TargetId == Hero.MainHero.StringId));

         if (t.Skip(plotter == null, "no lord free to plot against the player")) return t.Line();

         // Dishonourable + calculating, but not so merciless (mercy <= -1) that the policy would pick
         // Assassinate instead of Plot.
         plotter.SetTraitLevel(DefaultTraits.Honor, -1);
         plotter.SetTraitLevel(DefaultTraits.Calculating, 1);
         plotter.SetTraitLevel(DefaultTraits.Mercy, 0);

         NpcProfile profile = GetOrCreate(behavior, plotter);
         int lettersBefore = profile.SentLetters.Count;
         profile.ReputationWithPlayer = -80; // real hatred, past the lethal-hatred floor
         profile.StanceLastConsequenceDay = 0; // cooldown elapsed
         behavior.Store.Set(profile);

         bool fired = StanceConsequenceBehavior.DebugEvaluate(plotter);

         NpcProfile after = behavior.Store.Get(plotter.StringId);
         bool schemePlanted = SchemeStore.Instance.Active.Any(s => s.PlotterId == plotter.StringId && s.TargetId == Hero.MainHero.StringId);
         int lettersAfter = after?.SentLetters.Count ?? lettersBefore;

         t.Note($"{plotter.Name}: fired={fired}, schemeAgainstPlayer={schemePlanted}, letters {lettersBefore}->{lettersAfter}");
         t.Expect("fired", fired, true);
         t.Expect("schemePlantedAgainstPlayer", schemePlanted, true);
         t.Expect("noLetterWritten", lettersAfter, lettersBefore);

         return t.Line();
      }

      /// <summary>
      ///   "Stance with teeth" increment 2: the Intercept verdict must actually send InterceptionBehavior
      ///   after the player (not merely write a letter). Traits are forced deterministically (honourable +
      ///   bold, so the policy picks Intercept rather than Plot/Assassinate). The pursuit itself still runs
      ///   through InterceptionBehavior's own per-lord cooldown and "one pursuer at a time" guard, so a
      ///   fired verdict that could not ride out (guard legitimately blocked it) is MANUAL, not a failure.
      /// </summary>
      private static string TestStanceIntercept(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("stance_intercept");

         if (t.Abort(InterceptionBehavior.Instance == null, "InterceptionBehavior not active")) return t.Line();

         bool playerSubordinateInArmy = MobileParty.MainParty?.Army != null && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty;

         if (t.Skip(playerSubordinateInArmy, "player is subordinate in an army - the interception guard blocks any pursuit here")) return t.Line();

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h);

         if (t.Skip(lord == null, "no lord leading a party on the map to send")) return t.Line();

         // Honourable + bold: NOT dishonourable (so the policy never picks Plot/Assassinate over Intercept).
         lord.SetTraitLevel(DefaultTraits.Honor, 1);
         lord.SetTraitLevel(DefaultTraits.Valor, 1);

         NpcProfile profile = GetOrCreate(behavior, lord);
         profile.ReputationWithPlayer = -80; // real hatred, past the lethal-hatred floor
         profile.StanceFear = 0; // undeterred: dares to ride out in person
         profile.StanceLastConsequenceDay = 0; // cooldown elapsed
         behavior.Store.Set(profile);

         bool fired = StanceConsequenceBehavior.DebugEvaluate(lord);
         string reason = InterceptionBehavior.Instance.ConsumeInterceptionReason(lord);

         t.Note($"{lord.Name}: fired={fired}, pursuitBegan={reason != null}");
         t.Expect("fired", fired, true);

         if (t.Skip(fired && reason == null, "verdict fired but InterceptionBehavior's own cooldown or an army guard blocked the pursuit itself, not a wiring fault"))
            return t.Line();

         t.Expect("pursuitBegan", reason != null, true);

         return t.Line();
      }

      /// <summary>
      ///   "Stance with teeth" increment 2, the friendly-ladder payoff: when a real secret scheme targets
      ///   the player, the Warning letter must NAME the plotter (not just speak generic warmth). Reads the
      ///   expected name from SchemeStore.GetSecretSchemerAgainst at the moment of firing (the very same
      ///   call the production code makes), so the assertion is independent of test ordering even if an
      ///   earlier self-test already left some other scheme afoot against the player.
      /// </summary>
      private static string TestStanceWarning(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("stance_warning");

         if (t.Abort(SchemeStore.Instance == null, "SchemeStore not active")) return t.Line();

         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan &&
            !SchemeStore.Instance.Active.Any(s => s.PlotterId == h.StringId && s.TargetId == Hero.MainHero.StringId));
         Hero friend = ConversationNpc();

         if (t.Skip(plotter == null || friend == null || friend == plotter, "need a distinct plotter and a friendly NPC to warn the player")) return t.Line();
         if (t.Skip(friend.Clan == Clan.PlayerClan, "NPC is in the player's own clan - no stance letters")) return t.Line();

         SchemeStore.Instance.ForcePlot(plotter, Hero.MainHero, SchemeKind.Slander);
         Hero expectedThreat = SchemeStore.Instance.GetSecretSchemerAgainst(Hero.MainHero);

         if (t.Skip(expectedThreat == null, "ForcePlot did not yield a nameable secret scheme against the player (env-dependent)")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, friend);
         int lettersBefore = profile.SentLetters.Count(l => l.Reason == LetterReason.StanceFavor);
         profile.ReputationWithPlayer = 60; // genuine friendly affection, above the Favor floor
         profile.StanceTrust = 50; // trusts the player enough to tip them off
         profile.StanceLastConsequenceDay = 0; // cooldown elapsed
         behavior.Store.Set(profile);

         bool fired = StanceConsequenceBehavior.DebugEvaluate(friend);

         NpcProfile after = behavior.Store.Get(friend.StringId);
         string content = after?.SentLetters.LastOrDefault(l => l.Reason == LetterReason.StanceFavor)?.Content;
         bool namesThreat = content != null && content.IndexOf(expectedThreat.Name.ToString(), StringComparison.OrdinalIgnoreCase) >= 0;
         int lettersAfter = after?.SentLetters.Count(l => l.Reason == LetterReason.StanceFavor) ?? lettersBefore;

         t.Note($"{friend.Name} warns of {expectedThreat.Name}: fired={fired}, namesThreat={namesThreat}");
         t.Expect("fired", fired, true);
         t.Expect("lettersQueued", lettersAfter, lettersBefore + 1);
         t.Expect("namesThreat", namesThreat, true);

         return t.Line();
      }

      private static string TestSuccession(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("succession");

         if (t.Abort(SuccessionBehavior.Instance == null, "SuccessionBehavior not active")) return t.Line();

         // Prefer a clan member as heir; fall back to any alive lord
         Hero heir = Clan.PlayerClan?.Heroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive) ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(heir == null, "no heir candidate — join a clan or ensure a lord is alive")) return t.Line();

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(subject == null, "no lord to seed a profile on")) return t.Line();

         // Seed a deterministic profile: one PRIVATE event (dropped by succession) + one PUBLIC event (kept)
         NpcProfile profile = GetOrCreate(behavior, subject);
         int day = behavior.GameStateBridge.GetCurrentGameDay();
         profile.Events.Add(new NotableEvent(day, NotableEventType.Captivity, "[SELFTEST] private captivity memory"));
         profile.Events.Add(new NotableEvent(day, NotableEventType.Conflict, "[SELFTEST] public political dealing"));
         behavior.Store.Set(profile);

         profile = behavior.Store.Get(subject.StringId);
         int publicBefore = profile.Events.Count(e => SuccessionMemoryPolicy.SurvivesSuccession(e.type));
         var predName = Hero.MainHero.Name?.ToString();

         // Apply — mutates ALL profiles in the store (heir inherits standing, not private history)
         int touched = SuccessionBehavior.Instance.Apply(Hero.MainHero, heir);

         NpcProfile after = behavior.Store.Get(subject.StringId);
         int privateAfter = after.Events.Count(e => !SuccessionMemoryPolicy.SurvivesSuccession(e.type));
         int publicAfter = after.Events.Count(e => SuccessionMemoryPolicy.SurvivesSuccession(e.type));

         t.Note($"subject={subject.Name}, heir={heir.Name}, {touched} profiles, inherited='{after.InheritedFromName}'");
         t.Expect("privateDropped", privateAfter == 0, true);
         t.Expect("publicKept", publicAfter == publicBefore && publicAfter >= 1, true);
         t.Expect("inheritedSet", after.InheritedFromName == predName, true);

         return t.Line();
      }

      /// <summary>
      ///   Runs one LLM call under the ambient <see cref="_budgetToken" />, blocking the calling thread until
      ///   it completes or the test's budget cancels it (Task.Run avoids any main-thread sync-context capture).
      ///   <paramref name="start" /> must thread the token it receives into the real pipeline (ChatAsync's own
      ///   <c>ct</c> parameter) so a timed-out call is actually aborted at the HTTP layer, not merely given up
      ///   on locally, which is what stops a slow call from completing later and mutating state after the
      ///   sweep has moved on to the next test. Shared by <see cref="AskLlm" /> and <see cref="AskLlmParsed" />.
      /// </summary>
      private static bool TryRunLlmTask<T>(Func<CancellationToken, Task<T>> start, out T result, out string error)
      {
         CancellationToken ct = _budgetToken;
         Task<T> task = Task.Run(() => start(ct), ct);
         try
         {
            task.Wait(ct);
         }
         catch (OperationCanceledException)
         {
            result = default;
            error = BudgetTimeoutMessage();

            return false;
         }
         catch (AggregateException ex) when (ex.InnerExceptions.Count > 0 && ex.InnerExceptions.All(e => e is OperationCanceledException))
         {
            result = default;
            error = BudgetTimeoutMessage();

            return false;
         }

         result = task.GetAwaiter().GetResult();
         error = null;

         return true;
      }

      #endregion
   }
}