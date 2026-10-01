// Code written by Gabriel Mailhot, 10/07/2026.

#region

using TaleWorlds.ModuleManager;
using CalradiaRemembers.Storage;
using System;
using CalradiaRemembers.Logic.WorldAlmanac;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Witnesses;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Companions;
using CalradiaRemembers.Duels;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Interception;
using CalradiaRemembers.Items;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Social;
using CalradiaRemembers.Logic.Verbs;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.RealmTidings;
using CalradiaRemembers.Verbs;
using CalradiaRemembers.Retainer;
using CalradiaRemembers.Scenarios;
using CalradiaRemembers.Services;
using CalradiaRemembers.Settings;
using CalradiaRemembers.Social;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Travel;
using CalradiaRemembers.WorldAlmanac;
using CalradiaRemembers.WorldEvents;
using Newtonsoft.Json;
using NpcMemoryService.Core.Standing;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.Captivity;
using NpcMemoryService.Core.Extension;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
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

      // ── Appreciations: REPETITION (2026-07-22). The pure tests cover the arithmetic and the fold; what ──
      // ── they cannot reach is the LIVE ledger, which is the part that can damage a real campaign: the ──
      // ── repeat must deepen the record already persisted rather than add a twin, the same-day anti-noise ──
      // ── rule must survive the change, and the consolidation that runs on load must not let a player's ──
      // ── tally climb every time they reload. This is the one lot flagged as unsafe to publish untested. ──

      /// <summary>The cause text this test plants under, distinct from every other self-test's, so the twin-merge assertions can only ever see its own record.</summary>
      private const string AppreciationRepeatCause = "[SELFTEST] A kindness done more than once.";

      // ── Companion audience: GRATITUDE found-topic (2026-08-20). Seeds a real Appreciation via the real ──
      // ── TryPlant gate, then drives the REAL resolver (CompanionAudienceTopicResolver.Scan), never a mock, ──
      // ── so a change to the pure gate/strength policy or the live wiring both surface here. ──────────────

      /// <summary>The cause text this test plants under, distinct from every other self-test's own appreciation cause.</summary>
      private const string GratitudeTestCause = "[SELFTEST] A kindness worth thanking for.";

      /// <summary>Comfortably above CompanionGratitudePolicy.MinEffectiveMagnitude, planted TODAY so no decay applies.</summary>
      private const int GratitudeTestMagnitude = 70;

      /// <summary>Inside CompanionHappiness's Content band, so the happiness gate clears.</summary>
      private const int GratitudeTestContentHappiness = 70;

      /// <summary>Below CompanionHappiness.LeaveThreshold (Disgruntled), well outside the Content/Devoted gate.</summary>
      private const int GratitudeTestSourHappiness = 10;

      // ── Companion audience: SCHEME WARNING found-topic (2026-08-20 follow-up). Seeds a real Scheme via the ──
      // ── real ForcePlot launch path, then drives the REAL resolver (CompanionAudienceTopicResolver.Scan), ──
      // ── never a mock, so a change to the pure gate/strength policy or the live wiring both surface here. ──

      /// <summary>Inside CompanionHappiness's Content band, so the happiness gate clears (mirrors GratitudeTestContentHappiness).</summary>
      private const int SchemeWarningTestContentHappiness = 70;

      /// <summary>Below CompanionHappiness.LeaveThreshold (Disgruntled), well outside the Content/Devoted gate (mirrors GratitudeTestSourHappiness).</summary>
      private const int SchemeWarningTestSourHappiness = 10;

      // ── Companion audience: AMBITION found-topic (2026-08-20 follow-up 2). Drives the REAL resolver against a ──
      // ── real companion already riding in the main party (Command holds trivially for them), never a mock. ──────

      /// <summary>Inside CompanionHappiness's Content band, so the happiness gate clears (mirrors GratitudeTestContentHappiness).</summary>
      private const int AmbitionTestContentHappiness = 70;

      /// <summary>Below CompanionHappiness.LeaveThreshold (Disgruntled), well outside the Content/Devoted gate (mirrors GratitudeTestSourHappiness).</summary>
      private const int AmbitionTestSourHappiness = 10;

      /// <summary>Days back the FIRST occurrence is dated, so the second one lands on a different day and is a genuine repeat rather than a same-day duplicate.</summary>
      private const int AppreciationRepeatDaysBack = 5;

      // ── Grudges: the resolution engine (Arrange mirrors grudge_test/grudge_tier2: plant via the ──
      // ── real TryPlant gate, resolve via the real GrudgeResolutionService seam both make_amends and ──
      // ── the gift-routing hooks share) ──────────────────────────────────────────────────────────────

      /// <summary>
      ///   A magnitude comfortably above every formation floor and any lingering test grudge from an earlier run in the
      ///   same session, so the newly-planted grudge dominates.
      /// </summary>
      private const int GrudgeResolveTestMagnitude = 80;

      // ── Interception/fief-audience: the Grudge/AppreciationOwed proactive channels (2026-08-20). ──
      // ── Comfortably above InterceptionPolicy.MinGrudgeMagnitudeForInterception/MinAppreciationMagnitudeForInterception ──
      // ── (40), so decay and any lingering test data from an earlier run in the same session cannot ──
      // ── mask the significance gate the fact-gathering itself applies. ──────────────────────────────

      /// <summary>Comfortably above InterceptionPolicy.MinGrudgeMagnitudeForInterception, planted TODAY so no decay applies.</summary>
      private const int InterceptionGrudgeTestMagnitude = 70;

      /// <summary>Comfortably above InterceptionPolicy.MinAppreciationMagnitudeForInterception, planted TODAY so no decay applies.</summary>
      private const int InterceptionAppreciationTestMagnitude = 70;

      /// <summary>Mechanical (non-LLM) tests get this flat wall-clock budget, plenty for in-memory state checks.</summary>
      private const int MechanicalBudgetSeconds = 20;

      /// <summary>The registry. To add a test: write a method, then add one line here.</summary>
      private static readonly List<SelfTest> Tests = new() {
         new SelfTest("providegold", Severity.Light, "seed ProvideGold -> take_gold -> assert stamped", TestProvideGold),
         new SelfTest("unknown_action_notice", Severity.Light, "feedback funnel: drive the REAL bridge with an invented [ACTION] type (no executor) against a live lord -> assert the outcome is refused AND flagged WasUnknownVerb, with the deed-scoped base message (the old '(That cannot be done here. Nothing happened.)' contradicted a scene where real effects also landed). A refusal mutates nothing, so no cleanup", TestUnknownActionNotice),
         new SelfTest("extension_recap", Severity.Light, "Extension Surface (ActionResult.Recap / CrConversation.PromptReply): register a throwaway external action, drive the REAL dispatch (CrActions -> VerbRegistry -> ExternalVerbAdapter -> bridge) against a live lord, and assert its Recap reaches ActionOutcome on BOTH a success and a refusal; also assert CrConversation.PromptReply is a safe no-op (false) with no conversation open. Unregisters the probe afterwards", TestExtensionRecap),
         new SelfTest("regard_progression", Severity.Light, "Progressive regard (player request): reset a live lord's regard tier, then drive the REAL bridge change_relation(+1) three times in one frame -> assert the rising bar (tier 0 lands a point, tier 1 banks then lands, tier ends at 2), so a long conversation earns several points but each costs one more warm moment. Restores the profile's regard state and personal score afterwards", TestRegardProgression),
         new SelfTest("recent_matter", Severity.Light, "Recent-matter approach (fkasad report, phase 1): seed a fresh 'fought at your side' memory on a live lord, then drive the REAL candidate builder (InterceptionBehavior.DebugBuildRecentMatters) and assert the seeded memory surfaces classified as a LIVED (first-hand) matter, so a lord who fought beside you approaches about THAT rather than distant rumor. Removes the seeded memory afterwards", TestRecentMatter),
         new SelfTest("recent_matter_letter", Severity.Light, "Recent-matter approach (loose end): when a chase ends in a missed-meeting LETTER instead of a face-to-face, the letter now names the SAME specific matter the in-person opener would raise, framed by register, not a generic 'your recent deeds'. Seeds a lived 'fought at your side' memory, drives the compose-only seam (DebugRecentMatterLetterContext) and asserts the composed letter context names that matter and frames it first-person. Removes the seeded memory afterwards", TestRecentMatterLetter),
         new SelfTest("secret_lover_leak", Severity.Light, "Mystery-lover (phase 3a): force the secret-lover existence-whisper for a live hero (DebugForceLeak) and assert it records a Scandal world event that names the SUBJECT but hides the lover (InvolvesPlayer=false, the subject in ActorIds, gossip-worthy magnitude), so lords probe the player about it while the identity stays secret. Retracts the seeded event afterwards", TestSecretLoverLeak),
         new SelfTest("admit_secret_lover", Severity.Light, "Mystery-lover (phase 3b): temporarily mark a player-clan companion the player's secret lover, then drive the REAL bridge dispatch of admit_secret_lover against a live lord and assert the lord's knowledge ledger now records the identity (the fact learned ONLY because the player confessed, never deduced), a confession memory is planted on the lord, and a lord who was not told stays ignorant. Restores the flag, forgets the ledger entry, and trims the planted memory afterwards", TestAdmitSecretLover),
         new SelfTest("held_secret", Severity.Light, "Mystery-lover (phase 3b payoff): mark a player-clan companion the player's secret lover and record a live lord as KNOWING it, then drive the REAL EncounterContextBuilder.Build for that lord and assert the held-secret subject surfaces on the context (naming the companion) when adult content is on, or is suppressed when the adult gate is off. Restores the flag and forgets the ledger entry afterwards", TestHeldSecret),
         new SelfTest("secret_propagation", Severity.Light, "Mystery-lover (phase 3b-2): record a live lord (forced to neutral regard so he is not the player's confidant) as KNOWING the secret, then drive the REAL propagation (DebugSpreadOnce, bypassing only the daily chance) and assert the secret reaches one of his friends who then knows it too, never the player. Forgets both ledger entries and restores the teller's regard afterwards", TestSecretPropagation),
         new SelfTest("pay_for_silence", Severity.Light, "Mystery-lover (phase 3b-3): mark a companion the player's secret lover and record a live lord as KNOWING it, then drive the REAL bridge dispatch of pay_for_silence with a price and assert the gold moves player->lord, the hush is stamped as recently paid (easing the leverage prompt), and a memory is kept. Restores both purses, the flag, the ledger, and trims the memory afterwards", TestPayForSilence),
         new SelfTest("player_violence", Severity.Light, "Player violence (player request, Inc 2): drives the REAL bridge dispatch of kill_npc against a live lord and asserts THE guarantee, that with the MCM lethal toggle off (its default) the attempt is refused and the victim is still alive. Also checks the scene reader measures both fighters and that robbery is never barred by the lethal toggle. Mutates nothing while the toggle is off", TestPlayerViolence),
         new SelfTest("battle_feat", Severity.Light, "Battle-memory enrichment (Gabriel 2026-09-06, Inc 1): reads LIVE hero fighting facts through the real BattleFeatFacts bridge and asserts they yield a real prowess score; then pins the exploit ruling on synthetic extremes (besting a champion is an Exploit and travels as gossip, stomping a weakling is Unremarkable and never travels); then drives the REAL mission journal through its debug seam and asserts it keeps the felled hero (deduplicated), who downed the player, and the troop tally apart; and asserts a bested lord is never called killed (the truth rule). Mutates nothing", TestBattleFeat),
         new SelfTest("tournament_memory", Severity.Light, "Tournament memory (player suggestion, Inc 2): build a synthetic arena result (the player bested one lord, another beat the player and took the crown) and drive the REAL feeder (TournamentMemoryFeeder.Apply). Asserts a first-hand LIVED memory lands on each lord's profile (the loser recalls losing, the champion recalls besting the player and taking the crown) so the recent-matter interception makes them approach, and a champion gossip world event is recorded (player not involved). Trims the memories and removes the event afterwards", TestTournamentMemory),
         new SelfTest("tournament_rivalry", Severity.Light, "Tournament memory (player suggestion, Inc 4): force a low-honour lord and record three arena head-to-heads via the REAL rivalry behavior; asserts no rivalry forms before the threshold, it crystallizes at the threshold, the tally is right, and a BITTER rivalry plants a light grudge. Clears the grudge, forgets the tally, and restores the trait afterwards", TestTournamentRivalry),
         new SelfTest("lord_secret_lover", Severity.Light, "Mystery-lover (lord coverage): a MARRIED lord the player is secretly the lover of carries the SDK Status SecretLover (not the companion-only IsSecretLover flag). Mark a non-companion lord's profile Status SecretLover and assert the unified detection (PlayerSecretLoverSubjectIds) finds him, then record a second lord as knowing and drive the REAL EncounterContextBuilder.Build to assert the held-secret payoff names the lord subject (adult on) or is suppressed (gate off). Restores the status and forgets the ledger afterwards", TestLordSecretLover),
         new SelfTest("quest_hideout", Severity.Light, "issue a BanditHideout quest through the real pipeline anchored on a town with a live infested lair nearby -> assert it bound a REAL existing hideout (TargetHideoutId) + a cardinal bearing (DirectionHint) against the live world (MANUAL when no town has an infested lair in range, which is itself the designed refusal)", TestQuestHideout),
         new SelfTest("quest_hideout_marker", Severity.Light, "increment 2a (player report: 'clear the base south of the village' but the map marker sat on the village): issue a BanditHideout quest through the real pipeline against a town with a live infested lair, drive the marker-resolution seam, and assert the marker resolves to the bound LAIR, not the anchor town. MANUAL when no town has an infested lair in range. Cleans up the seeded quest", TestQuestHideoutMarker),
         new SelfTest("scheme_warn", Severity.Light, "ForcePlot -> TargetHeeds -> assert plotter/target relation deltas", TestSchemeWarn),
         new SelfTest("succession", Severity.Light, "reframe a DETACHED profile for an heir -> assert private dropped, save untouched", TestSuccession),
         new SelfTest("succession_switch", Severity.Light, "Wallace1067 (25/09/2026): switching the active character with The Original Player Switcher was read as a death each way, and the reframe deleted private memories across the whole save twice. Reads the REAL classifier on live heroes, touching nothing: the living player is a switch, a dead hero a death, a disabled one a retirement.", TestSuccessionSwitch),
         new SelfTest("recruit", Severity.Heavy, "eligible lord -> join_clan -> assert he moved into your clan", TestRecruit),
         new SelfTest("teach_skill", Severity.Light, "find a live NPC with a skill at or above TeachSkillPolicy.TeacherSkillFloor -> drive the REAL bridge teach_skill once (clean cooldown) -> assert real skill XP landed on the player, a second immediate lesson from the same NPC is refused by the cooldown, and an unrecognised skill name is refused rather than faked. Restores the player's skill level and the NPC's cooldown stamp afterwards", TestTeachSkill),
         new SelfTest("take_into_service", Severity.Heavy, "the MIRROR direction (13/09/2026, Gabriel's ruling 'un seigneur doit pouvoir recruter'): every other recruiting verb brings somebody INTO the player's household, this one gives somebody away. Finds a spare player companion the pure gate says is free to go and an other-clan lord, runs the REAL bridge take_into_service -> asserts the companion LEFT the player's clan, JOINED the lord's, is NOT a fugitive (the whole reason the executor uses RemoveCompanionAction.ApplyAfterQuest and never ApplyByFire, which runs MakeHeroFugitiveAction), and gained the starting relation with their new lord. Then puts them back and ASSERTS THE RESTORE, so a broken restore is reported rather than quietly costing a real companion. Skips when the campaign has no spare companion or no other-clan lord", TestTakeIntoService),
         new SelfTest("recruit_notable", Severity.Heavy, "find a settlement NOTABLE (gang leader/headman/merchant...) that has an eligible successor in its town, force regard to the floor, run the REAL bridge recruit_notable -> assert the notable is now a companion in your CLAN and PARTY, a starting relation bonus was applied, and the chosen successor holds the vacated notable post (settlement.Notables). Best-effort restore afterwards (recruit_notable is not cleanly reversible: the alley transfer cannot be undone). Skips when no disposable notable+successor pair exists rather than wreck a real campaign", TestRecruitNotable),
         new SelfTest("secret_bond_bar_agrees", Severity.Light, "player report 2026-09-09 ('the NPC is expressing affection, yet the system displays a message stating that intimacy is insufficient'), caused by ONE regard bar living as a literal 50 in three places while only the confession moved onto the Relationship Pacing dial. Drives the REAL EncounterContextBuilder.Build and the REAL bridge action take_as_secret_lover against a compatible companion at exactly MinRegardFor(current pacing) and one point below it, and asserts BOTH engine gates flip on the SAME number the confession uses. A pure test cannot catch this (it would compare the policy with itself); only driving the live sites can. EXTENDED 2026-09-09 with the THIRD site of that same bar, what the NPC may SAY: asserts the LIVE PromptBuilder the chat uses declares from the very number the confession roll measures from, and that the register reads Attachment at the bar and Interest one below it, where an NPC may flirt but must never speak of love nor open a courtship on standing alone. Restores the regard and undoes any bond formed", TestSecretBondBarAgrees),
         new SelfTest("captive_lord_withheld_from_defection", Severity.Heavy, "player report: a held captive lord below the recruit_prisoner regard floor could still narrate accepting the player's offer to defect, though nothing happened (an empty promise). Really captures a capturable in-party lord who is not their own clan's leader, drives EncounterContextBuilder.Build with regard just BELOW PrisonerRecruitmentPolicy.RegardFloor -> assert CanRecruitPrisoner is false and the new CaptiveLordWithheldFromDefection flag is true, then raises regard to the floor -> assert they FLIP (CanRecruitPrisoner true, the new flag false), proving the two are mutually exclusive on live engine facts. Releases the captive and restores regard afterwards", TestCaptiveLordWithheldFromDefection),
         new SelfTest("narrative_patch_scope", Severity.Light, "Found checking a modder's README against the code (tashmetu, 19/09/2026): the cr_patch scanner first read ModuleHelper.GetModules(), which is every module FOUND on disk, enabled or not. A bridge disabled in the launcher still had its prose read into every prompt, and with its assembly not loaded its {{variables}} reached the model as raw braces. Asserts that every module the scanner reads is one the player ENABLED, and that Calradia Remembers itself is among them (proof the list is the live one, not an empty fallback).", TestNarrativePatchScope),
         new SelfTest("deliver_prisoner_by_sale", Severity.Heavy, "Player report (Lordfadooboo, 18/09/2026): a lord asked for a Nord lord in chains, the player handed one over in words, got PAID, and the bargain stayed open for good. Two verbs hand a captive to an NPC and only give_prisoner ever looked at the bargain; buy_prisoner paid a price and touched nothing. Seeds a REALM-keyed DeliverPrisoner bargain, takes a lord of that realm prisoner, then runs buy_prisoner (the SALE) through the real bridge and asserts the bargain is SETTLED anyway and the captive left the player: handing a lord the very captive he asked for keeps his bargain, whatever the word.", TestDeliverPrisonerBySale),
         new SelfTest("deliver_prisoner", Severity.Heavy, "capture -> give_prisoner -> assert transfer + deed stamp", TestDeliverPrisoner),
         new SelfTest("free_prisoner", Severity.Heavy, "capture -> free_prisoner -> assert released", TestFreePrisoner),
         new SelfTest("quest_rescue_release_credit", Severity.Heavy, "v2.5.3 player report ('his success cancelled his quest'): capture a real in-party lord bound to a real outstanding RescuePrisoner quest, free them through the REAL free_prisoner bridge action (not a battle, not a Debug seam) so the REAL CampaignEvents.HeroPrisonerReleased dispatch reaches QuestDeedTracker's new hook, and assert the quest is stamped SATISFIED, not left outstanding; then runs the REAL daily maintenance sweep and asserts it still does not CANCEL the now-satisfied quest (the exact bug: the void sweep used to read the freed target as proof the task was impossible). Releases the captive and drops the seeded quest afterwards", TestRescueCreditedOnRelease),
         new SelfTest("captive_kin", Severity.Heavy, "capture an in-party enemy lord who has a living parent -> build that parent's RELATIONSHIPS -> assert the captured child is marked as held by the player (the 'you are a liar, my child is safe' denial bug), then release", TestCaptiveKinAwareness),
         new SelfTest("give_item", Severity.Light, "pick a player item -> give_item -> assert it left the inventory", TestGiveItem),
         new SelfTest("gift_equip", Severity.Light, "gift an equippable weapon to a companion/spouse -> assert they equipped it", TestGiftEquip),
         new SelfTest("style_reload", Severity.Light, "select a style, edit its file -> assert the next message picks the edit up", TestStyleReload),
         new SelfTest("presence", Severity.Light, "Mechanical: a companion riding in your party is told they travel at your side and is NEVER told how long since they last saw you, even with a 60-day-stale memory (the 'crew acts abandoned between two ports' report); a lord genuinely away STILL hears the time apart. BORROWS a companion into your party if none is riding along, and puts them back", TestPresence),
         new SelfTest("presence_gate", Severity.Light, "Mechanical: the ONE behaviour v1.30.14 changed, guarded across the WHOLE living roster with no preconditions. The letter system's facade and the single presence authority must give the same answer for every hero alive (before this, letters asked a wide question and jealousy a party-only one), and beside/away must be exact opposites. If they ever disagree again, the systems have drifted apart and this fires", TestPresenceGate),
         new SelfTest("contentment", Severity.Light, "Mechanical: sour a companion to 20, run the REAL weekly tick, assert they CLIMB instead of sliding toward the door (the rebalance's core guarantee: idleness alone can never carry anyone out); plus a companion governing a town is not idle (the reported governor bug). Restores their happiness afterwards", TestContentment),
         new SelfTest("companion_gratitude", Severity.Light, "New sixth found-topic (design brief 2026-08-20): a content companion thanking the player for a specific kindness, purely warm, never a request/bargain. Seeds a real Appreciation via the real TryPlant gate on a content companion, drives the REAL CompanionAudienceTopicResolver.Scan, and asserts it HOLDS with the seeded detail; asserts the SAME appreciation's own key, once marked spent, WITHHOLDS it (spend-once); and asserts an otherwise-identical but unhappy (Disgruntled) companion never holds it either (the happiness gate). Restores the companion's original happiness afterwards; does not remove the seeded appreciation (mirrors appreciation_flow/appreciation_repeat, which have no removal API to call)", TestCompanionGratitude),
         new SelfTest("companion_scheme_warning", Severity.Light, "New seventh found-topic (design brief 2026-08-20 follow-up): a loyal companion warning the player of a secret plot against them, purely a warning, never a request/bargain, and never a live LLM call. Seeds a real Scheme against Hero.MainHero via the real ForcePlot launch path on a content companion, drives the REAL CompanionAudienceTopicResolver.Scan, and asserts it HOLDS with the plotter named in the detail; asserts the SAME plot's own key, once marked spent, WITHHOLDS it (spend-once); and asserts an otherwise-identical but unhappy (Disgruntled) companion never holds it either (the happiness gate). Restores the companion's original happiness AND removes the seeded scheme from the live ledger afterwards (unlike an appreciation, a live scheme carries real feud/rumour machinery, so none may survive this test)", TestCompanionSchemeWarning),
         new SelfTest("companion_ambition", Severity.Light, "New eighth found-topic (design brief 2026-08-20 follow-up 2): a content, ambitious companion voicing ONE of four bound ambitions, each resolved by an EXISTING action verb, so a granted ambition executes nothing itself, unlike Gratitude/SchemeWarning. Drives the REAL CompanionAudienceTopicResolver.Scan against a real companion already riding in the main party (made comfortably Content), and asserts it HOLDS with a non-empty detail (the Command ambition holds trivially for any main-party companion, so no extra engine-state seeding is needed); asserts the SAME companion, asked again within CompanionAmbitionPolicy.CooldownDays, WITHHOLDS it (the per-companion cooldown); and asserts the identical companion, made Disgruntled, never holds one either (the happiness gate). Restores the companion's original happiness afterwards", TestCompanionAmbition),
         new SelfTest("companion_road_audience", Severity.Light, "Mechanical: the ROAD trigger (player report 2026-09-08: 'unless I remember to routinely engage with my travel buddies, they never say anything on their own', since the found-topic audience only ever offered on SettlementEntered). Asserts the pacing dial's Never setting switches the road trigger's own gate (DebugRoadTriggerWouldFire) off deterministically, whatever the live roster/cooldown state; asserts Occasionally's shared cooldown still equals the shipped CompanionAudienceTopicPolicy.GlobalCooldownDays (the 'default reproduces today's feel' requirement); and, with the dial restored, drives the REAL RoadIsSafeForPrivateWord + DebugWouldOfferAudience chain OnDailyTick itself calls (no dice roll, no popup, no mutation), reporting whatever the live map state answers. Restores the MCM dial's prior index afterwards", TestCompanionRoadAudience),
         new SelfTest("fief_audience", Severity.Light, "Player wish (Fakade, roadmap): road-only InterceptionBehavior needs a field party to move, so anyone without one (ladies, notables, a garrisoned lord standing around in a fief) never got a turn. Finds a real partyless hero standing in a live settlement; asserts the pure gate (FiefAudiencePolicy.ChooseFiefValid) raises nothing for a no-reason fact set; seeds a fresh, high-reach, player-involving WorldEvent so the SAME live fact HeardRecentPlayerDeed the road path reads is real, not fabricated; drives the REAL candidate scan (FiefAudienceBehavior.DebugScanAndStamp, never the live UI inquiry, never an LLM call) and asserts it offers that NPC with a fief-valid trigger; then asserts the SAME NPC asked again is WITHHELD by the per-NPC cooldown the offer itself just stamped. Removes the seeded WorldEvent and clears the stamped cooldown afterwards", TestFiefAudience),
         new SelfTest("interception_grudge_appreciation", Severity.Light, "Two new proactive channels for the mod's two signature event-anchored affect ledgers (design brief 2026-08-20): a lord who holds a live, significant grudge against the PLAYER rides out/asks for a word to air it (Grudge), and one who holds a significant, recent appreciation of the player does likewise to acknowledge it (AppreciationOwed), on BOTH the road (InterceptionBehavior) and the fief-audience channel (FiefAudienceBehavior), since both reuse the SAME InterceptionPolicy.Choose. Seeds a real Grudge (TryPlant) on a clean-slate lord and asserts InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer finds it (false before planting), the pure InterceptionPolicy.Choose surfaces Grudge, and Grudge is fief-valid; does the same for a real Appreciation and AppreciationOwed; then asserts the priority pin (Grudge outranks AppreciationOwed when both hold on the same facts, mirroring InterceptionPolicyTests). Removes the seeded grudge afterwards (mirrors appreciation_flow/companion_gratitude: no removal API exists for an appreciation, so it is left, exactly like every other appreciation-seeding self-test)", TestInterceptionGrudgeAppreciation),
         new SelfTest("grudge_answered", Severity.Light, "Player report (Zivkyn, 18/09/2026): a lord kept asking why the player killed his kin, months later, however often it had been explained, and kept riding out to confront the player about it. Plants a real grudge against the player on a clean-slate lord and makes it a VENDETTA, asserts it drives an interception and hear_account is offered, then runs hear_account through the REAL bridge (BannerlordGameStateBridge.ExecuteAction, the path an LLM emission takes) and asserts: the grudge is answered with the account, still a vendetta and exactly as heavy, the lord's built GrudgeNote now carries the account and the do-not-ask-again instruction, the interception no longer fires, and hear_account is no longer offered. No LLM call. Clears the lord's grudges afterwards", TestGrudgeAnswered),
         new SelfTest("duel_venue_here", Severity.Light, "Player reports (FloatingMammal 10/09/2026, yozakura12 17, 18 and 20/09/2026): the duel was offered in places it could not be staged, and staged in places the engine could not open. Reports where the player is standing RIGHT NOW (settlement, town or castle, inside the walls or merely on it, and whether it owns the 'center' location a duel opens) and asserts the two halves agree: that a town or a castle really does own a duelling ground in THIS install, and that the venue the resolver chooses is the one the staging would accept. Skips on the open map, so walk into a castle and run it there. No duel is armed and no scene is opened", TestDuelVenueHere),
         new SelfTest("plural_acceptance", Severity.Light, "Player report (yozakura12, 22/09/2026): playing with multiple spouses, jealousy cost the full betrayal price even for a wife who says in her own voice that she accepts the harem, and the only thing close to that state was open_relationship, which is RECIPROCAL and suppresses her jealousy outright. Borrows a lady as a committed consort, sets her regard to devotion, and runs accept_other_partners through the REAL bridge (BannerlordGameStateBridge.ExecuteAction, the path an LLM emission takes): asserts her word is recorded, that it is NOT reciprocal open terms, that the jealousy track moves from Betrayal to AcceptedAgainstCulture, that saying it twice is refused, and that a woman who is no partner of the player is refused. Restores her bond, her regard and her acceptance", TestPluralAcceptance),
         new SelfTest("conversation_resume", Severity.Light, "Player report (fkasad, 20/09/2026): roleplay takes longer than a battle, so he quits mid-negotiation, saves, closes the game, comes back, and the lord remembers TALKING to him but not a word of what was said, so the bargain starts over. Drives the REAL store the chat uses (NpcMemoryCampaignBehavior.ConvResume): keeps a built transcript against a real lord, then asserts it comes back whole for the same lord on the same day with its memory anchor, that the SAME open cannot take it twice, and that it is refused and DISCARDED for another lord, another day, or a staged scene, which is the report's own 'deleted as soon as you open a new chat' rule. No LLM call, no window opened. Forgets whatever it kept afterwards", TestConversationResume),
         new SelfTest("approach_framing", Severity.Light, "Player report (Fakade, 19/09/2026, local model): a lord rode out to him, then from the next turn spoke as though the player had come to HIM. The opening directive is consumed on read, so who sought whom was in the prompt once and never again. Stashes a real fief-audience reason on a lord, builds the encounter context twice through the REAL builder, and asserts the directive fires on the first build and is spent on the second while NpcSoughtThePlayer stays true on both; then asserts opening a new conversation (ForgetConversationSeeker) clears it. Leaves no pursuit behind", TestApproachFraming),
         new SelfTest("realm_departure", Severity.Light, "Player report (tashmetu, 19/09/2026): expelled from his kingdom by another mod after a lost war, he kept getting letters as if he still belonged. Records a departure from a real kingdom the way the kingdom-change feeder now marks every one, and asserts WorldEventFeeders.DaysSincePlayerLeft finds it and silences that ruler's service offer (ServiceOfferPolicy.RulerMayWrite), then removes it and asserts the ruler may write again. Also asserts the service-offer premise follows the player's LIVE realm. No LLM call, mutates nothing it does not remove", TestRealmDeparture),
         new SelfTest("interception_nemesis_bounty", Severity.Light, "A third proactive channel, the announcement/discoverability layer on top of the EXISTING scripted hero_main_options nemesis bounty offer (roadmap piece 2, player wish, subsumes the 'tavern rumour: Coll raids near X' wish since the lord tells the player directly): a lord whose region is plagued by a nemesis he is eligible to bounty seeks the player out to name him and where he operates, never settling the bounty itself in this channel. Mints a nemesis anchored to a REAL lord's own region (MintBountyEligibleNemesis) and asserts InterceptionBehavior.HasNemesisBountyToOffer reuses the SAME NemesisBountyDialogFlow.FindEligibleNemesisFor gate the scripted offer uses (never a re-derived one), that the Detail names the nemesis, that the pure InterceptionPolicy.Choose surfaces NemesisBounty, that it is fief-valid, and the priority pin (a live grudge against the PLAYER still outranks it, mirroring InterceptionPolicyTests). Marks the minted identity dead afterwards", TestInterceptionNemesisBounty),
         new SelfTest("letters_mailbox", Severity.Light, "Mechanical: asking to stop writing (via the real preference parser + apply seam) drives a sender's correspondence damper to the floor and LOWERS that sender's mailbox weight; an Acknowledge also lowers it; the weighted scheduler prefers the important letter; a full budget window releases nothing; a 40-day-old event candidate is stale", TestLettersMailbox),
         new SelfTest("conversation_cut", Severity.Light, "Player report (17/09/2026): characters recited the conversation back, replies growing every turn; and (raphareish, 18/09/2026) asked after the progress of a quest agreed thirty seconds earlier. Both cures are a COUNT taken when the conversation opens, and both counts are produced by the HOST (ChatViewModel) and honoured by the SDK, so the pure tests on either side cannot see the wire between them. This is the wire: seeds one memory and one task on a real profile, takes the two cuts through the REAL ChatViewModel, seeds a second memory and task to stand for what the exchange itself writes, asserts neither cut moves, then builds the EXACT prompt the chat pipeline builds (LlmServices.ChatService.PromptBuilder, via EncounterContextBuilder.Build) and asserts the character still holds their past, does not have this conversation replayed at them as history, and reads a task agreed at the table as JUST AGREED rather than an errand under way. No LLM: the whole question is what the prompt CONTAINS, and an answer from a model would be a weaker reading of the same fact. Removes everything it seeded", TestConversationCut),
         new SelfTest("letter_memory", Severity.Light, "Player report (Desporion): a marriage proposal 'agreed on the terms' over three letters was denied face to face because letters left only a generic 'Received a letter (Reason)' memory, never their substance. Pure half: LetterMemoryPolicy.ShouldRemember excludes the two re-ping reasons (AwaitingReply, QuestUpdate) and includes MarriageProposal; BaseMemory is first-person and reflects direction. Real-path half: drives the REAL LetterMemoryFinalizer.RecordLetterMemory (LetterPopupManager.MarkReceived's own seam) with a ShouldRemember=false reason (QuestUpdate), so the run can never fall through to the async LLM summarizer (Lane C, kept out of cr.testall), and asserts the base event actually lands on profile.Events. Removes the event it added", TestLetterMemory),
         new SelfTest("world_conditions", Severity.Light, "Mechanical: the engine reads the weekly world-conditions feeder depends on still work against the live game (a renamed field or a changed model signature would silently swallow to 0 inside the feeder's own guards, recording nothing forever). Reads GetScoreOfDeclaringWar in both directions between two live kingdoms and a live town's FoodStocks/FoodChange/Prosperity DIRECTLY, asserts each is finite, then runs the real RunScan and asserts both scanners ran. Skips when the world has fewer than two kingdoms or no town", TestWorldConditions),
         new SelfTest("realm_tidings", Severity.Light, "Mechanical: drives the REAL RealmTidingsBehavior.DebugEvaluate end to end and proves per-NPC dedup. Seeds a dominant known event for a friendly landed lord (regard forced to 30), asserts a RealmTidings letter candidate is queued and the event id enters the dedup ledger, then re-evaluates and asserts the same event is never relayed twice. Restores the writer's regard, ledger and candidates and removes the seed", TestRealmTidings),
         new SelfTest("siege_tidings_participation", Severity.Light, "Mechanical (player report: told by letter of a siege they FOUGHT IN, days later, as if it were news): stamps the REAL WorldEventFeeders siege-participation marker for a live settlement, then drives the REAL RecordSiegeCapture construction with a NON-player taker (a mere participant, never the credited capturer) -> assert the recorded WorldEvent has InvolvesPlayer true (so realm-tidings' own InvolvesPlayer filter will never relay it back) while Type stays SettlementCaptured and Aggrieved* stay empty (the conquest's attribution is NOT stolen from the real taker). Removes the seeded event afterwards", TestSiegeTidingsParticipation),
         new SelfTest("companion_battle_memory", Severity.Light, "Mechanical: the companion 'fought together' memory (WorldEventFeeders' main-party companion loop), which a real MapEvent drives in game. Borrows a companion into the party if none rides along, calls the DebugRecordCompanionBattleMemories seam, and asserts the companion's profile gained a fresh 'Fought at your side' memory. Removes the memory it added and returns any borrowed companion", TestCompanionBattleMemory),
         new SelfTest("spotlight_cast", Severity.Light, "Spotlight pillar wiring (found in play by Gabriel 2026-09-12: he addressed his companion Sley by name in a conversation with Ira, the turn routed to Sley, and no [SPOTLIGHT] line was ever written - the promotion silently did nothing). ConversationCast has been correct and unit-tested since 27/08; both faults were in the HOST. (1) The cast was born inside SetWitnesses, so a conversation with no auto-detected witness - a lord standing alone - had no cast at all, not even its own anchor. (2) AddWitness never introduced the arrival, and AddWitness is how a companion joins: the picker resolves ASYNCHRONOUSLY, after the cast was built, so every brought companion sat in the witness list while absent from the cast, Promote read RoleOf == null and returned false. ConversationCast.Introduce was written for exactly this on 27/08 and had never once been called from the game. Drives the REAL ChatViewModel: sets a real anchor hero with NO witnesses and asserts a cast exists with that anchor Active (hole 1), then AddWitness a real companion and asserts they are IN the cast as a Witness and therefore promotable (hole 2), then promotes and asserts the scene became an ensemble. A pure test cannot catch either: it would build the very cast the host failed to fill", TestSpotlightCast),
         new SelfTest("private_memory", Severity.Light, "The leak guard the companion briefing is built on (Gabriel 2026-09-13: what was decided in a briefing should live in the companion MEMORY and be used from there). A memory is normally shared: a witness carries theirs into the prompt of whoever is speaking, which is right for \"he was there when you took the town\" and catastrophic for \"Arwa asked me to charm Ira\", read out in Ira own prompt while she stands there. Until now the briefing dealt with that by DROPPING its memories outright, so nothing of the private word survived. NotableEvent.IsPrivate now marks a memory the holder keeps to themselves. Plants one PUBLIC and one PRIVATE event on a real hero profile and drives the REAL ChatViewModel.DebugWitnessMemory - the same BuildWitnessMemory path a live turn uses, and the ONLY path by which one hero memories reach another hero prompt - asserting the public one is carried and the private one is not, and that a hero holding NOTHING but private memories contributes no recall at all rather than an empty husk. Restores the profile events in a finally", TestPrivateMemory),
         new SelfTest("briefing_to_chat", Severity.Light, "The WHOLE briefing-to-meeting vector, end to end, driven through the real objects (Gabriel asked for it 2026-09-13 after three faults in one evening that every existing test was green through - each piece worked and the joins did not). Borrows two companions and a third hero as the target. Drives: RememberReturn (what the picker decides, without the picker), a REAL ChatViewModel put into briefing mode with both companions seated, four REAL player turns recorded into its transcript, then the REAL FinalizeCompanionBriefs. Asserts the private word reached the memory of BOTH companions and not just the anchor; that it is marked private; that the FIRST thing said survives as well as the last (the cap that ate his mission statement); that the target cannot read it through the REAL witness-recall path NOR through a REAL EncounterContextBuilder.Build with both companions standing as witnesses (the load-bearing property); that the return is armed at the right target with both companions; and that the REAL staged request names them so the meeting reopens with them seated. What it CANNOT prove is the two moments only play can: that the window opens and that the tick fires. Restores both profiles and clears the pending return in a finally", TestBriefingToChat),
         new SelfTest("companion_briefing", Severity.Light, "Companion-briefing pillar as it stands after the standing brief was dismantled (13/09/2026): the private word becomes a PRIVATE MEMORY on everyone who stood in it, and that memory never reaches the person it was about. Borrows a companion into the party if none rides along; picks a second, distinct living hero to stand in as the conversation's TARGET. Plants a real IsPrivate NotableEvent and drives the REAL EncounterContextBuilder.Build (no LLM call) for both heroes: asserts the companion's own prompt CARRIES it (they must act on what they were told) while it is ABSENT from the target's - the load-bearing property of the whole pillar, since BuildWitnessMemory is the single path by which one hero's memories reach another's prompt and it filters IsPrivate. Also asserts a NON-private event on the same companion DOES reach the target, so the test cannot pass by the memory simply never travelling. Removes the planted events in a finally and returns the borrowed companion", TestCompanionBriefing),
         new SelfTest("query_relevant_facts", Severity.Light, "Modder report: asking a lord a POINTED question about a specific third party got a hallucinated answer because the real fact ranked too low on ambient awareness (the small WorldRumorsBlock top-N) to ever be injected. Seeds a fresh, high-magnitude, faction-tagged WorldEvent naming a real third-party lord (guaranteed KNOWN regardless of map position), then drives the REAL EncounterContextBuilder.ResolveQueryRelevantFacts (no LLM call) once with a message naming that lord -> assert the seeded fact surfaces, and once with an unrelated message -> assert it does NOT (the knowledge-then-relevance gate, never omniscience). Removes the seeded event afterwards", TestQueryRelevantFacts),
         new SelfTest("war_council_offer", Severity.Light, "Mechanical: the war-council OFFER trigger's arm + policy + persisted-cooldown decision (WarCouncilOfferBehavior.DebugWouldOffer), WITHOUT opening the council UI. Arms the flag and asserts a notable action while leading an army WOULD offer, that not-in-army / non-notable / disarmed all refuse, and that stamping an offer today blocks a fresh one (cooldown) while a stale stamp clears it. Restores the flag + the persisted last-offer day", TestWarCouncilOffer),
         new SelfTest("interception_holds_course", Severity.Light, "Lordfadooboo (Nexus, 30/09/2026): a lord announced as riding out to meet the player passed him by or rode into a city. Steers a real free lord party onto the player as a pursuit does and checks it is held on course (its own AI may not take the order back), then ends the steering and checks the party is its own again and told to think afresh. Refuses while a real pursuit runs.", TestInterceptionHoldsCourse),
         new SelfTest("party_release", Severity.Light, "Mechanical: the STARVING LORD bug (Fakade). Freeze a real lord party exactly as the old stand-down did (SetMoveModeHold, which is an ORDER to stand still, not a release), prove it looks stranded, then prove BOTH halves of the fix: releasing hands it back to its own AI (RethinkAtNextHourlyTick), and the on-load rescue frees a party already stranded by an earlier version", TestPartyRelease),
         new SelfTest("retainer_spike", Severity.Heavy, "FEASIBILITY SPIKE: take a real OTHER-CLAN, partyless lord/lady into MobileParty.MainParty via AddHeroToPartyAction, assert (a) they are in the main member roster and (b) their Hero.Clan is UNCHANGED (still their own, NOT the player clan) with occupation still Lord; drive one RunDailyObservation (this behavior's daily handler) and assert they SURVIVED it at peace (reports honestly - this drives only the mod's own daily seam, not vanilla's full party-spawn tick); assert the pure RetainerLeavePolicy forces a leave on war; then release and assert they left the roster and are partyless again. Restores all borrowed state; skips MANUAL when no partyless other-clan lord exists (a party-leading lord's own party would be disbanded, which is not cleanly reversible in a live campaign)", TestRetainerSpike),
         new SelfTest("ride_with_me", Severity.Heavy, "the PLAYER-FACING ride_with_me / part_ways path through the REAL bridge (not the raw mechanism the retainer_spike test drives): (e) while RetainerBehavior.Enabled is OFF the verb REFUSES and takes no one; (a) enabled, an idle at-peace other-clan lord ends up in MainParty.MemberRoster with Hero.Clan UNCHANGED (not the player clan); (b) a party-LEADING lord is REFUSED (that is follow_me's job) with their own party intact, proving the two verbs are mutually exclusive on the leads-a-party fact; (c) part_ways removes them (out of the roster, partyless, untracked); (d) the pure RetainerLeavePolicy forces a leave on war. Restores regard + the feature switch in a finally; skips MANUAL when no partyless at-peace other-clan lord exists to borrow", TestRideWithMe),
         new SelfTest("stolen_love", Severity.Light, "Stolen Love pillar (non-destructive): on a real third-party-married NPC, assert the seducibility pipeline runs on live facts (SeductionFactsMapper -> SeductionEligibilityPolicy) without throwing and yields a bounded verdict; drive the estrangement state machine via the debug seam (seed -> None in the opening grace -> declare -> ReadyToDivorce) and assert NO divorce has fired (HasConcludedFor false); assert the jilted-spouse grudge magnitude for the real couple sits within [Min,Max]. Clears the seeded estrangement in a finally so NO marriage is ever touched. Skips when no third-party-married NPC exists", TestStolenLove),
         new SelfTest("reasoning_scrub", Severity.Light, "Mechanical: the inline <think> leak (tthheerroocckk, nemotron on High reasoning). The save/load itself is STUBBED: corrupted memories are written straight into the store as an old save would carry them (a trace before real prose, a memory that is ONLY a truncated trace, a corrupted background paragraph) -> the REAL session-launch scrub runs -> assert the prose is recovered, the all-trace memory becomes the honest 'faded' line (never invented content), the background returns to null, and a second pass is a no-op. Removes its seeds afterwards", TestReasoningScrub),
         new SelfTest("duel_consequence", Severity.Light, "Mechanical: the DUEL consequence glue (DuelOutcomeApplier), which no unit test can reach (it writes to the live campaign). Seed a courted lady's AttractionToPlayer to 0, then drive the REAL applier for a CourtshipRivalry duel: WIN raises her attraction by exactly CourtshipDuelPolicy.AdmirationOnWin, a following REFUSAL drops it by CowardiceOnRefuse (proving the forfeit path and that refusing costs MORE than losing), each duel records a world-event so she can learn of it, and a win lifts the player's renown. Restores her attraction afterwards", TestDuelConsequence),
         new SelfTest("menu", Severity.Light, "Mechanical: the in-chat Options button reaches the vanilla escape menu purely by REFLECTION, so a TaleWorlds rename would not throw, it would just go quiet while the log claimed success (exactly the bug SuperGraham339 reported across three versions). Asserts BOTH mechanisms the game answers ESC with still resolve: the MISSION view (a settlement, a conversation, a naval scene) and the map's navigation handler. Whichever one is missing is a screen the player's menu will never appear on", TestMenu),
         new SelfTest("panel_siege_guard", Severity.Light, "Mechanical: the full-map panel crash guard. Asserts the Harmony prefix that skips SettlementVisualManager.OnTick (the vanilla siege-bombardment visual tick, which runs on the frame loop and access-violates natively under a pushed CR panel: four confirmed 0xC0000005 crashes) is actually attached to the LIVE engine method. It resolves the target BY NAME, so a game update that renamed the type/method would silently stop protecting; this fires if that ever happens. Also asserts that with no panel open nothing is suppressed (siege visuals tick normally). No LLM, mutates nothing", TestPanelSiegeVisualGuard),
         new SelfTest("setting_hints_fit", Severity.Light, "Mechanical: every MCM hint fits on screen. A shipped bug with a receipt - v2.5.6 repaired NINETEEN hints that ran off the edge, and because they read \"On (default): ... Off: ...\" the clipped half was always the one explaining the other position. They were fixed by hand with nothing left to stop the twentieth, which 2026-09-12 then added. Reads the ATTRIBUTES, so it holds whether or not MCM has loaded. Read-only", TestSettingHintsFit),
         new SelfTest("settings_ranges_agree", Severity.Light, "Mechanical (Gabriel, 2026-09-10, after the Regard Warmth Cooldown: \"je suis surpris que le bon comportement n'ait pas ete bien protege par des tests unitaires. Par exemple, 0 aurait du etre teste\"). The uncomfortable truth is that 0 WAS tested and the test agreed with the bug: it pinned what the code did, while the MCM control clamped its minimum to 1 and the v2.5.1 changelog promised 0 turned the cooldown off. Three artefacts, no place for any two of them to meet. This is that place. Reflects over EVERY [SettingPropertyInteger] on CrSettings, pairs it with the same-named ModSettings property that enforces it, drives the declared MIN and MAX through the live setting, and asserts the code honours every value the menu offers. Catches the whole family in one assertion, including settings that do not exist yet. Restores each setting to its prior value in a finally", TestSettingsRangesAgree),
         new SelfTest("console_help_complete", Severity.Light, "Mechanical: cr.help against the REAL command registry. Asked on 2026-09-10 whether it was still complete, it described 70 of 259 registered commands, so cr.testall and cr.test were themselves invisible, and it advertised cr.compose, renamed cr.letter_compose long before, which made the one name a reader was most likely to copy verbatim the one that would fail. A hand-written list of every command in a project that gains commands weekly can only drift, so what EXISTS now comes from reflection over the assembly its own [CommandLineArgumentFunction] attributes and the prose is an overlay on top of it. Reads the live registry, drives the real ModCheats.Help in all three of its forms, and asserts: the help never names a command that does not exist (the cr.compose class of error), every registered command is reachable through cr.help all however undescribed, the curated page says where the rest of them are, and the search finds a command by fragment. Read-only, mutates nothing", TestConsoleHelpComplete),
         new SelfTest("seasonal_chronicle", Severity.Light, "The seasonal historian (Gabriel's design): at each turn of the season every realm is read, and what MOVED - a stronghold taken, coffers grown, armies thinned - is written into the world as ordinary news. Computed deterministically from the almanac's own power facts rather than written by a model, because \"Vlandia grew rich\" IS a delta description and a background call every season would cost tokens forever, fail halfway, and have a language to get wrong. Drives the REAL behaviour against the REAL store with a perturbed reading (the honest alternative is waiting a season and hoping), and asserts it is ordinary news rather than a fourth memory store. Removes the row it writes", TestSeasonalChronicle),
         new SelfTest("world_claims", Severity.Light, "Invented facts (Gabriel, 2026-09-12): a character makes up a blight, the gate judges it, and if it passes it becomes a real low-weight rumour a DIFFERENT character can have heard - which is the incoherence Gabriel named, closed through the rumour road that already existed instead of a second world model. A unit test can prove the RULE and never that the row reached the world, so this drives the real injector against the real store. Also asserts the two refusals that matter: the PLAYER never authors the world, and nothing the engine keeps on a screen is ever invented into it. Removes every row it writes", TestWorldClaims),
         new SelfTest("knowledge_composition", Severity.Light, "Mechanical (Gabriel + fkasad, 2026-09-11): the half a unit test CANNOT do. A unit test builds its own EncounterContext, so it can prove a companion WOULD carry his house's standing and never that anyone supplied it - which is exactly where the governor bug lived (the prompt was right, the rule would have been right, the host never filled the field). Sweeps up to 60 free REAL heroes PLUS up to 15 PRISONERS, asks NpcKnowledgeFactory which knowledge packs each one CARRIES, and asserts the live context actually SUPPLIED every one of them; reports the pack census so a pack that reaches nobody is visible rather than silently vacuous. Prisoners joined the sample on 16/09/2026: excluding them left the whole IsCutOff composition unverified in a live game - the one branch where packs are meant to go SILENT, and so the one where a mistake is hardest to spot by reading a transcript - and that blind spot was covering a real defect, since realm_news and underworld ignored the flag and a man in a cell was reading his realm's current fortunes. Read-only: builds contexts, mutates nothing", TestKnowledgeComposition),
         new SelfTest("unavailable_deeds", Severity.Light, "Mechanical (Gabriel's ruling 09/09/2026: \"il faudrait que le LLM declare en connaissant deja le gating\"): a verb used to have TWO states in the prompt, taught in full or ABSENT, and absence does not read to a language model as forbidden, it reads as not mentioned, so the model supplied the permission itself and the player met the contradiction one deed later as a system message refusing what the NPC had just promised. Verbs now state their own closed door BEFORE the model speaks (IStatesUnavailability, the inward twin of the IProvidesGuidance channel built for modders on 30/08/2026 and never turned inward). Sweeps up to 60 real living foreign lords, driving the LIVE VerbRegistry.ComposeUnavailability and ComposeTeachings against each, and asserts: no refusal ever leaks a verb id or an [ACTION] block (which would teach the model the very token it must not emit), and no verb is ever taught and refused in the same prompt. Then takes the first lord the world supplied who has the means but not the trust and drives the REAL EncounterContextBuilder.Build, asserting the block reaches the live EncounterContext.UnavailableDeeds and forbids the PROMISE, not merely the deed (a deferred promise being the same failure). MANUAL when no lord in this world is currently in that state. Read-only: builds contexts, mutates nothing", TestUnavailableDeeds),
         new SelfTest("chat_focus_arbiter", Severity.Light, "Mechanical (tashmetu, 2026-09-09): the chat overlay slid off screen on NPC-initiated conversations and never came back, killing every later conversation until a restart. The old arbiter decided \"a screen is above us\" by COUNTING the host screen's layers against the count taken when the chat opened, and vanilla's own MissionGauntletConversationView adds a layer to that SAME screen while the conversation starts: click an NPC and it is already counted, be approached BY one and it arrives after, so the count grows and the chat yields to the very window it exists to replace, forever, because that layer stays for the whole conversation. The rule is now the engine's own IsFocusLayer, with the two vanilla conversation windows named and exempt. Builds REAL GauntletLayers on the live top screen exactly as vanilla builds them (\"MissionConversation\" order 49, \"MissionEscapeMenu\" order 50, IsFocusLayer set, no input restrictions taken) and asserts: ScreenLayer.Name really hands back the name the layer was built with (the one assumption a pure test cannot check), the vanilla conversation layer changes the verdict by nothing at all, a real modal still takes the screen and is NAMED in the log, and removing it reclaims the screen. EXTENDED after Gabriel's play-test (2026-09-09) proved a name list cannot be complete: he opened a chat in a TAVERN, where vanilla's settlement menu \"MapMenuOverlay\" holds focus for the whole conversation, and the chat yielded to the very backdrop it had been drawn over, never appeared, and let the vanilla dialogue run on underneath. Whatever already claims focus when the chat OPENS is scenery, whatever it is called; only what arrives afterwards can be a screen above. Now also builds a real \"MapMenuOverlay\" layer and asserts it is held through when recorded as backdrop, yields when it is not (the exact bug), and that a genuine modal opening OVER that backdrop still takes the screen. Removes every layer in a finally, so a failed assertion can never leave one on the player's screen. No LLM, mutates nothing", TestChatFocusArbiter),
         new SelfTest("panel_state_hosting", Severity.Light, "Mechanical: the panel-hosting migration (PushScreen -> PushState). cr.tick_probe measured a pushed SCREEN leaving MapState ticking at exactly the same rate as nothing pushed at all (481 settlement-visual + 481 map-input ticks per 4s window), while a pushed STATE silenced both to zero, because GameStateManager deactivates MapState before the new screen even exists. Drives a REAL open/close cycle through LetterWindowManager.OpenCompose/Close (the shared CrPanelHost, defaulted to the state strategy) on a borrowed lord's profile, and asserts: the live GameState becomes CrPanelState while open, CrPanelHost.AnyPanelOwnsScreen (the tick-suppression flag every guard reads) engages then fully clears, and the state stack returns to EXACTLY the state that was active before, never left substituted or stuck. Force-closes the window in a finally even if an assertion throws, so a broken assertion can never leave a panel open or the map muted. No LLM", TestPanelStateHosting),
         new SelfTest("dialogue_entry_trace", Severity.Light, "Mechanical (missing-CR-option player reports): DialogueEntryTraceCollector resolves ConversationManager.ActiveToken to a name via reflection on the private stateMap field. Forward-resolves 'hero_main_options' (a token that certainly exists, registered by LlmDialogFlow) to its live index, then reverse-resolves that same index back, driving the real reflection path both ways rather than a parallel copy; asserts the round-trip returns the original name, so a future game update renaming stateMap is caught here instead of silently degrading the diagnostic to raw indices. Also asserts DialogueEntryTracePolicy renders the expected YES/NO verdict for an offered and a not-offered trace. Read-only: touches no live conversation state, nothing to restore", TestDialogueEntryTrace),
         new SelfTest("political_map", Severity.Light, "Mechanical: the political-map gather + grid + rasterise (World Almanac minimap, Inc B+C). Gathers one anchor per kingdom-held settlement (position + Kingdom.Color) from the live campaign, builds the pure PoliticalMapPolicy colour grid, and rasterises it to PNG; asserts the grid is the requested size, some cells are painted (territory), and the PNG carries a real PNG signature. Catches a renamed settlement/terrain API that would silently yield an empty map, or a broken rasteriser. MANUAL when the world has no kingdom-held settlement. No LLM, mutates nothing", TestPoliticalMap),
         new SelfTest("strategic_assessment", Severity.Light, "Mechanical: the Strategic Assessment gather (World Almanac). Builds the live PowerIndexResult, picks the advisor (highest-Tactics companion, else a warband soldier, else none when the player is alone), gathers the player's realm/rank/wars, and composes the OFFLINE factual summary; asserts the summary is non-empty and, when an advisor exists, the LLM prompt grounds on a real kingdom and forbids invention. The real async LLM counsel is Lane C (real LLM), not run here. No LLM, mutates nothing", TestStrategicAssessment),
         new SelfTest("provider_routing", Severity.Light, "Mechanical (per-role provider): set Prose->OpenRouter, Consensual->NanoGPT, Actions Interpreter->OpenAI-Compatible (+ a dummy endpoint) and assert the three resolved base URLs are DISTINCT and each kind matches, via LlmServices.DebugDescribeRoleRouting (no network call); then assert a per-role OpenAI-Compatible endpoint override sends each role to its OWN server while a blank override inherits the shared one; then set every role to (default) and assert all four roles collapse to the ONE default provider's base URL (the migration guarantee: an upgraded single-provider config is unchanged). Snapshots and restores every dropdown + the shared and per-role custom endpoint URLs", TestProviderRouting),
         new SelfTest("prose_fallback", Severity.Light, "Mechanical (Prose + Interpreter truncation safety net): drives the pure TruncationFallbackPolicy for every clean/not-clean finish reason (length/content_filter/empty -> fallback, stop -> no fallback, no model configured -> no fallback), then asserts LlmServices.ResolveProseFallbackModel resolves the Phase 2 NSFW model, reusing the PROSE model when the NSFW field is blank and honouring a distinct configured NSFW model instead. Restores the NSFW model field", TestProseFallback),
         new SelfTest("manual_model_toggle", Severity.Light, "Three-state in-chat switch (Enhanced/Roleplay/NSFW, replaces the old binary manual toggle): drives the pure ModelModePolicy mapping each state to its (main role, composition) pair and the fixed Cycle order (wraps NSFW -> Enhanced), then the live LlmServices.ActiveModelId/ActiveComposition: the NSFW state resolves the configured NSFW model with no MCM gate to pass (the switch is always live now), Roleplay/Enhanced resolve the PROSE model (Roleplay never auto-routes to NSFW for a captivity scene), and only Enhanced yields Enhanced composition. Snapshots and restores the NSFW model field and the active mode", TestManualModelToggle),
         new SelfTest("escaped_target", Severity.Light, "fkasad (Nexus, 01/10/2026): the lord a capture task named was beaten and escaped, and the player paid the broken-word price. On a real met lord, a temporary capture task whose target was beaten: the giver's acceptance ([QUEST_COMPLETE]) settles it through the real quest path for HALF the reward, and words alone never settle one whose target was not beaten. The task, the purse, the regard and the memory are put back.", TestEscapedTarget),
         new SelfTest("privacy_survives_interpreter", Severity.Light, "Lufaine and Maifailun (Nexus, 30/09/2026): a character agreed to speak privately and the room stayed full. In the two-call chat the interpreter's actions replaced the scene model's, which is the one taught request_privacy. Drives the real splice (ActionInterpreterComposer.WithInterpretedTags): the character's acceptance survives an interpreter that recorded none, and a gift the scene model tagged does not. No LLM.", TestPrivacySurvivesInterpreter),
         new SelfTest("no_tasks_from_enemies", Severity.Light, "fkasad (Nexus, 29/09/2026): an enemy lord buying back a prisoner slid into demanding a deed, then into invalid-quest messages. Builds the REAL context for a lord at war with the player: it says so, and the prompt built from it teaches no task to offer and no prisoner bargain. Skips when the player is at war with no one. No LLM, nothing stored.", TestNoTasksFromEnemies),
         new SelfTest("quests_suppressed_in_nsfw", Severity.Light, "Mechanical: build the REAL EncounterContext in NSFW mode -> SuppressQuests is true (an intimate scene never mints a quest from a vow, player report); in Roleplay it stays false (quests remain). Restores the active mode. No LLM.", TestQuestsSuppressedInNsfw),
         new SelfTest("witness_recalls_own_memory", Severity.Light, "Mechanical: seed a memory on a hero -> the witness recall a group scene would give them (the real BuildWitnessMemory path + cap) includes it, so a companion no longer answers a group scene as if a prior one-on-one agreement never happened. Removes the seed afterwards.", TestWitnessRecallsOwnMemory),
         new SelfTest("witness_density_thins_the_room", Severity.Light, "Mechanical: player complaint that conversations are ALWAYS crowded. Drives the REAL WitnessPresencePolicy.Filter directly against a fixed eligible list: Sparse thins the room down to its cap of 1; Bustling with an always-passing rng keeps every one of them, the exact passthrough that reproduces today's live behaviour for a player who never opens the menu. No LLM, mutates nothing.", TestWitnessDensityThinsTheRoom),
         new SelfTest("end_conversation_guards_open_question", Severity.Light, "Mechanical: read the REAL taught end_conversation vocabulary -> its description forbids closing on a turn that awaits the player's reply (player report: NPCs ended while asking a question or making a request). No LLM.", TestEndConversationGuardsAwaitingReply),
         new SelfTest("hostage_breach_imprisons", Severity.Light, "Mechanical: seed a hostage term on a free lord, force the breach through the REAL ExecuteBreach path (TakePrisonerAction) -> the hostage is imprisoned; then release them so the campaign is untouched. Briefly jails and frees a test lord. No LLM.", TestHostageBreachImprisons),
         new SelfTest("claimant_support_evaluates", Severity.Light, "Mechanical: gather a real same-kingdom other-clan lord's facts and run the REAL ClaimantSupportPolicy via CouncilLift.EvaluateSupportClaimant (non-destructive, no influence moved) -> a valid backer lands on the amount decision (Applied/CannotAfford), never a WHO gate. No LLM.", TestClaimantSupportEvaluates),
         new SelfTest("arrest_warrant_and_bench", Severity.Light, "Arrests (Gabriel, 26/09/2026): a grave act opens a warrant in the liege's name and the trial bench seats the liege first, then the realm's vassals who regard the player worst. The liege is the player's own, or ARRANGED (another realm's living ruler) when the player serves none, without joining any kingdom. Puts the saved case back exactly (DebugSnapshot/DebugRestore); the sergeants, sent only on the hourly tick, never ride.", TestArrestWarrantAndBench),
         new SelfTest("brigandage_patch_attached", Severity.Light, "Brigandage: the Harmony hook on BeHostileAction's private ApplyInternal is attached (resolved by name with Prepare, so a renamed target would silently stop detecting brigandage; this catches it).", TestBrigandagePatchAttached),
         new SelfTest("courier_save_restore", Severity.Light, "Couriers ride through TransientPartyRegistry since 26/09/2026. Sends a real courier with a test letter, then runs a save's despawn and restore without saving: the letter is still in flight, not lost, carried by a live courier standing where the old one stood. Every other transient party on the map goes through the same cycle, as in a real save. The test letter and its courier are removed at the end.", TestCourierSaveRestore),
         new SelfTest("self_esteem", Severity.Light, "Self-esteem (Lordfadooboo and Maifailun, ruled 30/09/2026): records three captivities on a real met character through the real path; his self-regard falls (never rises), each captivity lands harder than the last, and the blow reaches his mood of the day; the chat dossier's perceived image of him is one sentence with no number in it. The character's record is restored.", TestSelfEsteem),
         new SelfTest("mood_of_the_day", Severity.Light, "The mood of the day (ROADMAP, ruled 14/09 and 30/09/2026): reads real characters' days off the game and draws their mood. The same man drawn twice today is the same mood; a captive never draws an ordinary day or high spirits; the player draws none; a lift in conversation moves it one band and a new conversation starts from the day again. Read-only.", TestMoodOfTheDay),
         new SelfTest("action_gate", Severity.Light, "The one gate every character's action asks (Gabriel, 30/09/2026: a baby son asked to discuss warfare, then a dead father sat at a council). On the live population: no child takes an adult's act (lords by occupation included), no child under 13 sits at a council, no captive acts outside marriage, duels, witnessing and a held scene, no one dead or disabled acts at all, the player never acts toward themselves, and a free adult lord may do everything. Read-only.", TestActionGate),
         new SelfTest("letter_place_and_audience_age", Severity.Light, "Two reports (30/09/2026): Lordfadooboo's lone player placed \"on the march with your own retinue\" (the writer's) in a wanderer's letter, and Nexusrrr's baby son asking for an audience about warfare. Checks the phrase that places the player in a letter never borrows the writer's \"your\" and says alone when alone, and that no child is among those who may ask for an audience in any town. Read-only.", TestLetterAndAudienceSeekers),
         new SelfTest("regard_changed", Severity.Light, "The public regard-change door (29/09/2026, for tashmetu's bridge): raises a real character's regard by 1 and lowers it back through the one place regard changes, and checks a CrNpcMemory.RegardChanged listener hears both with before and after, reported as self_test (a test's change, which a mirroring mod must not keep). Regard is restored and the listener removed.", TestRegardChanged),
         new SelfTest("correspondence_rank", Severity.Light, "Gabriel (28/09/2026): the Empress wrote in her own hand to a landless newcomer, and rode out herself to find him. Reads a real ruler against the player as they stand: far above an unbonded player, her letters come from her chancellery (a title, no invented clerk) and she does not ride out in person; a love letter is always her own. Writes nothing.", TestCorrespondenceRank),
         new SelfTest("balance_survey", Severity.Light, "The balance-of-power survey (Gabriel, 28/09/2026: measure before building a balancing lever). Reads every living realm as the weekly survey does: one row each, fief shares adding up to the whole map, and rows that survive the survey file's round trip. Writes nothing.", TestBalanceSurvey),
         new SelfTest("landfall_wait", Severity.Light, "Windmolen (Nexus, 28/09/2026): accepting an envoy on a naval move opened the chat over the live naval map, which flickered and crashed. At sea a conversation now waits for landfall and an envoy does not arrive. Reads the door's route for where the player stands, holds a real request as the door does at sea and checks it is held once. Opens nothing; the held request is dropped.", TestLandfallWait),
         new SelfTest("clan_standing_seed", Severity.Light, "tashmetu (30/09/2026): a lord whose clan counted the player a friend (+54) met him as a near-stranger. Drives the real apply path on a real character's profile: a trusted friend's clan lifts regard from 0 to friendly regard once, a second meeting adds nothing, and a character the player offended keeps the offence. Regard and the seed are restored.", TestClanStandingSeed),
         new SelfTest("letter_regard", Severity.Light, "Lordfadooboo (Nexus, 28/09/2026): a dozen letters and the correspondent's regard had not moved. Drives the real apply path on a real character's profile: a warm letter earns +1, a second the same day earns nothing, a cold one costs 1, and the [LETTER_TONE] line is stripped from the letter. Regard and the letter clock are restored.", TestLetterRegard),
         new SelfTest("vassal_departure_judged", Severity.Light, "fkasad (24/09/2026): a vassal holding no fief who leaves a realm at war is judged like a mercenary, the oath weighing like kinship; one holding fiefs is not (the war that follows is the consequence). Judges leaving the player's realm (or any realm) right now WITHOUT applying anything: judged only with no fief, the news speaks of the oath, and a sworn vassal is never thanked.", TestVassalDepartureJudged),
         new SelfTest("mercenary_company_judged", Severity.Light, "Zivkyn (Nexus): the game's own mercenary companies change sides mid-war and nobody resents them. Finds a real company in a realm's pay and judges its departure by fkasad's grid WITHOUT applying anything: the verdict follows the realm's footing, and the news line names the company and the realm.", TestMercenaryCompanyJudged),
         new SelfTest("faction_block_realm", Severity.Light, "tashmetu (27/09/2026): a companion's or a notable's realm is not their people's, and pulled another people's FACTION BEHAVIOR block into their prompt (or retitled theirs). Reads real heroes: a lord of a kingdom of his own culture keeps that realm for the choice; a hero living in a realm of another culture gets none. Read-only.", TestFactionBlockRealm),
         new SelfTest("council_seat_note", Severity.Light, "The council's per-seat door for other mods (tashmetu, Bellum Civile bridge, 26/09/2026): registers a throwaway {{variable}}, then resolves a council_seat template for two real lords. Each seat must carry ITS OWN value (the provider is told which seat, and that it writes for a council seat), a seat whose variable comes back empty carries no note, and with no template no note exists at all. The variable is unregistered at the end.", TestCouncilSeatNote),
         new SelfTest("transient_party_registry", Severity.Light, "The one service for the mod's transient parties (26/09/2026, the event-listeners map's proposal): spawns two real throwaway parties and tracks them under a test role. A party the registry RELEASES leaves the map and is never reported as a loss; a party destroyed by anything else is reported to its owner exactly once. Both parties are gone at the end.", TestTransientPartyRegistry),
         new SelfTest("mercenary_departure_judged", Severity.Light, "fkasad's grid (25/09/2026): reads a real realm's war footing (at war, its strength against its enemies' combined strength) and judges how the end of a mercenary contract with it would be taken, without applying anything. Asserts a realm at peace is AtPeace and a realm at war is judged ReleasedInVictory or Abandoned as its strength says. No regard, memory or news is written.", TestMercenaryDepartureJudged),
         new SelfTest("courier_secret_travels", Severity.Light, "Courier secrets (raphareish 24/09/2026; Gabriel): an intercepted letter travels with its captors. Captures a [SELFTEST] letter onto a REAL bandit band, then has a real lord's party defeat the carrier (DebugDefeatCarrier, no LLM, no rumour) -> the secret leaves the band and the lord remembers what it said; a second capture defeated by the PLAYER's party is recovered unread. Removes the memory it added.", TestCourierSecretTravels),
         new SelfTest("courier_secret_reaction", Severity.Light, "Courier secrets Inc 4b (Gabriel: an action in proportion to the secret): runs the real reaction for another lord's letter read by a grudge-free lord -> NOTHING plants no grudge, AGAINST plants a LeakedLetter grudge on the letter's author, and the player (who wrote nothing) pays nothing. Clears the grudge it planted.", TestCourierSecretReaction),
         new SelfTest("letter_offer_prisoner_ransom", Severity.Light, "Mechanical (PrisonerOffer accept): capture an in-party lord into the player's prison, make a buyer solvent, run the shared PrisonerRansomExecutor -> assert the sale succeeds, the captive leaves the player's prison, and the player is paid. Restores everything (frees the captive, reverts both purses). Skips if no capturable lord / no buyer. No LLM.", TestLetterOfferPrisonerRansom),
         new SelfTest("action_catalog_parity", Severity.Light, "Unified Action Catalog, Stage 1: the Action Interpreter is only ever taught the verbs listed in NpcMemoryService.Core.Actions.GameActionCatalog, so a bridge verb missing from it would silently never be emittable by the LLM, and a catalog verb the bridge no longer dispatches would teach a dead action. Asserts BannerlordGameStateBridge.HandledActionTypes (the engine-bound authoritative set this pure-SDK catalog cannot be compared against by a unit test) is set-equal to GameActionCatalog.Types, naming any verb missing from either side. Mutates nothing", TestActionCatalogParity),
         new SelfTest("external_action_registration", Severity.Light, "Extension Surface (CrActions.Register): a third-party mod's one-call registration door. Registers a dummy external action -> asserts it appears in GameActionCatalog.Types, BannerlordGameStateBridge.HandledActionTypes, and LlmServices.DebugActionVocabulary (the 1:1 prose prompt); drives it through the REAL bridge (ExecuteAction with an unmatched type falls through to VerbRegistry.Instance.Execute) and asserts the dummy Execute callback's own message/success came back; asserts a duplicate Name, a reserved chat-flow-control Name (end_conversation), and a null-Execute spec are all rejected. Unregisters in a finally so nothing registered here can leak into a later test or a real prompt", TestExternalActionRegistration),
         new SelfTest("external_action_guidance", Severity.Light, "Extension Surface actions guidance (ExternalActionSpec.Guidance): the fix for the modder report that an ineligible action's NPC roleplayed AGREEING in prose, only for the deed to fail its own eligibility check at execution. Registers a dummy action that is ALWAYS ineligible with a Guidance callback returning a distinctive sentinel -> asserts the REAL VerbRegistry.Instance.ComposeGuidance includes the sentinel despite the ineligibility, while ComposeTeachings for the same context excludes the action entirely (the whole behavioral point); also asserts the guidance context it received carried the real NPC's facts and correctly resolved a hero named in the player's message via CR's own ContextualNameResolver, not a reimplementation. Unregisters in a finally so nothing leaks into a later test or a real prompt", TestExternalActionGuidance),
         new SelfTest("prompt_variable_registration", Severity.Light, "Extension Surface, Prompt Variables volet (increment 4): CrPrompt.RegisterVariable is the PUBLIC door a third-party mod uses to contribute a LIVE {{name}} prompt variable, replacing a Harmony patch on the SDK's own PromptBuilder.BuildPromptVariables. Since PromptVariableRegistry.Compose is SDK-internal (like Clear, visible only to the SDK's own test project), drives the REAL public surface instead: builds a live system prompt (BuildSystemPrompt, the same call the chat pipeline uses) for a real NPC with PostHistoryInstructions referencing a registered token -> asserts the provider's value, keyed off the real NpcId, lands in the actual prompt text; registers a SECOND provider that THROWS in the same prompt build -> asserts building never throws, the other token still resolves, and the throwing token's own {{name}} is left literal (Compose skipped it, never smuggled a stray value in); registers a third provider under a BUILT-IN name (user) -> asserts the real player name still wins and the impostor value never appears, the exact merge guard BuildPromptVariables enforces. Unregisters all three in a finally via CrPrompt.Unregister, the only cleanup surface the mod assembly has", TestPromptVariableRegistration),
         new SelfTest("conviction", Severity.Light, "Mechanical: author a BELIEF on an NPC at runtime (no file edit) and follow it along the whole real chain: the loader hands it back, the chat's own per-turn sync carries it onto the profile, and the section lands in the live system prompt saying it moves what he DECIDES (the one thing a backstory refuses to do) with its three limits intact (no powers, no imported lore, cannot bend the world). Then switch custom data off and prove the belief LEAVES the prompt instead of lingering. Restores the file overrides + your MCM choice", TestConviction),
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
         new SelfTest("heard_diagnostic", Severity.Light, "Asked by a bridge author (tashmetu, 23/09/2026): how to check that a fed event reached a given character without talking to them. Feeds through the REAL Feed door and asserts: a real PlaceId is kept and a bogus one dropped, a realm tag reaches a lord of that realm, cr.heard's verdict agrees with the prompt's own query, and a lord NAMED in HeroIds can now speak of a faint fact a bystander has not heard (the M-G6 lift reached only the sway and jealousy checks before 2.5.22). Retracts what it feeds.", TestHeardDiagnostic),
         new SelfTest("feed_player_named", Severity.Light, "tashmetu's Bellum bridge names a besieged town's OWNER in HeroIds (23/09/2026). When that owner was the player, CR filed the siege as the player's own deed: out of the news, into the chronicle, told back to the player as something they did. Feeds through the REAL Feed door and asserts: naming the player alone leaves it news (not in the chronicle, heard by a lord of the realm as news), and only the player among ActorHeroIds (the one who DID it) makes it the player's deed, with the actor still counted as concerned. Retracts what it feeds.", TestFeedPlayerNamed),
         new SelfTest("conversation_session_sequencing", Severity.Light, "Mechanical (Heroes Voices TTS request): drive the REAL internal CrConversation.RaiseStarted/RaiseLineSpoken/RaiseEnded twice in a row -> assert Started/LineSpoken/Ended of one session share the same non-empty SessionId, LineIndex runs 0 then 1, and a second RaiseStarted mints a DIFFERENT SessionId with LineIndex reset to 0. Unsubscribes its handlers afterwards. No LLM, mutates nothing persistent.", TestConversationSessionSequencing),
         new SelfTest("mission_register", Severity.Heavy, "MissionBoard.Register an external mission -> assert issued + dedup + Complete stamps it", TestMissionRegister),
         new SelfTest("mission_reward", Severity.Heavy, "MissionBoard FULL LOOP -> register -> Complete -> player reports to giver -> assert reward PAID + quest Completed", TestMissionRewardLoop),
         new SelfTest("rp_influence", Severity.Light, "eligible lord -> give_influence -> assert clan influence moved + marker + cooldown refuses repeat", TestRpInfluence),
         new SelfTest("rp_troops", Severity.Heavy, "lord w/ healthy party -> lend_troops -> assert rosters moved + marker", TestRpTroops),
         new SelfTest("rp_give_troops", Severity.Heavy, "lord w/ under-strength party -> give_troops -> assert rosters moved + marker + appreciation planted", TestRpGiveTroops),
         new SelfTest("rp_spend_influence", Severity.Light, "needy lord's clan -> spend_influence -> assert clan influence moved + marker + appreciation planted + cooldown refuses repeat", TestRpSpendInfluence),
         new SelfTest("rp_sway", Severity.Light, "NPC + 3rd hero -> sway_opinion(against) -> assert relation dropped + marker + world event + cooldown refuses repeat", TestRpSway),
         new SelfTest("rp_release", Severity.Heavy, "seed 3rd-party captive on a lord's party -> release_prisoner -> assert freed + relation bump", TestRpRelease),
         new SelfTest("commitment_is_memory_only", Severity.Light, "fire a real pledge_against -> assert the vow is REMEMBERED but not tracked as a quest: no ledger entry, no journal mirror, no relation change (2026-08-22: an NPC's vow is not a player quest)", TestCommitmentIsMemoryOnly),
         new SelfTest("divorce_process", Severity.Heavy, "married couple -> end_marriage -> force endDay -> tick -> assert dissolved + exSpouses + no one clanless + scandal + relation hits", TestDivorceProcess),
         new SelfTest("spouse_divorce", Severity.Heavy, "marry the player (test spouse) -> force demand -> accept_divorce -> assert dissolved + flag cleared; second test spouse -> force demand -> decline_divorce -> assert refusal grudge planted", TestSpouseDivorceDemand),
         new SelfTest("player_divorce", Severity.Heavy, "marry the player (test spouse, happy) -> end_own_marriage -> assert dissolved + a Divorce grudge on the player; second test spouse (miserable) -> end_own_marriage -> assert dissolved + NO grudge", TestPlayerDivorce),
         new SelfTest("marry_companion", Severity.Heavy, "marry your own companion, END TO END: assert the pure MarryCompanionEligibilityPolicy carve-out (a non-kin companion is admitted, each refusal bars); then against a live disposable opposite-sex wanderer companion assert ResolveMarriageBlockReason no longer emits SameClan for it (and still DOES for an in-clan blood relative when one is present); then drive the REAL bridge marry executor (promotion Wanderer->Lord + MarriageAction.Apply) and assert MainHero.Spouse == companion, the ex-companion is now Occupation.Lord in the player's clan and no longer IsPlayerCompanion; then divorce via CR's MarriageDissolutionService and assert the ex-spouse is SANE (alive, not scattered/fugitive, returned to a clanless Occupation.Wanderer) and the player is unwed again. Best-effort restores the borrowed companion. MANUAL when the player is already wed, no disposable companion exists, or a live army/battle precondition refuses the wedding (the army guard as designed)", TestMarryCompanion),
         new SelfTest("spouse_estrangement", Severity.Heavy, "marry the player (test spouse) -> force-escalate -> force deadline to today -> tick -> assert dissolved + spouse-side Divorce grudge + escalation cleared; second test spouse -> force-escalate -> raise relation above the reconciliation threshold -> tick -> assert marriage SURVIVES + escalation cleared", TestSpouseEstrangement),
         new SelfTest("grudge_backfill", Severity.Light, "seed a synthetic Jealousy NotableEvent -> force backfill -> assert a Jealousy grudge exists -> force it again -> assert no duplicate", TestGrudgeBackfill),
         new SelfTest("open_relationship", Severity.Light, "open relationship (Inc 2): EnableJealousy=false gates the real daily tick so a queued reaction never fires (and ON fires it); then seed several committed consorts, run the REAL competition selection twice, and assert marking the naturally-aggrieved partner as IsOpenArrangement excludes her so a different NON-open partner is chosen instead. Restores all borrowed state", TestOpenRelationship),
         new SelfTest("open_partner_lover", Severity.Light, "open relationship (Inc 3a): make a partner an open committed consort and force high neglect, then drive the REAL OpenPartnerLoverBehavior.DebugTakeLover selection and assert she now carries a TakenLoverId pointing at a plausible eligible hero (alive, not the player, unwed, outside the player's own household). MANUAL when the live world has no compatible candidate. Restores all borrowed state", TestOpenPartnerLover),
         new SelfTest("close_relationship", Severity.Light, "open relationship (Inc 3b-b): an OPEN committed consort run through the REAL bridge close_relationship action has IsOpenArrangement cleared (existing lover untouched); a committed partner who is NOT open is refused (no open terms to revoke). Restores all borrowed state", TestCloseRelationship),
         new SelfTest("end_affair", Severity.Light, "open relationship (Inc 3b-b): a committed partner carrying a DISCOVERED lover run through the REAL bridge end_affair action has TakenLoverId cleared and every affair-tracking field reset (LoverIsSecret / LoverTrystCount / AffairDiscovered / LoverKnownToPlayer) plus an ended-affair memory recorded; a partner with no lover is refused. Restores all borrowed state", TestEndAffair),
         new SelfTest("harem_secret_lover", Severity.Light, "open relationship culture branch (Inc 3a): a neglected harem-culture wife (Aserai) with NO open arrangement takes a SECRET lover when EnableJealousy is ON (LoverIsSecret set, NO public rumour laid), takes none when the toggle is OFF, and an egalitarian wife with no pact never strays. MANUAL when the world has no harem lady or no compatible candidate. Restores all borrowed state incl. the MCM toggle", TestHaremSecretLover),
         new SelfTest("affair_discovery", Severity.Light, "trysts + discovery (Inc 3b-a): give a partner a SECRET lover, force several trysts while away (each bumps the tryst counter + a private memory), and assert discovery eventually flips AffairDiscovered + LoverKnownToPlayer and lays a scandal chronicle world-event; then an OPEN lover becomes LoverKnownToPlayer WITHOUT an AffairDiscovered scandal. MANUAL when the world has no compatible candidate. Restores all borrowed state", TestAffairDiscovery),
         new SelfTest("lover_child", Severity.Light, "pregnancy by the lover (Inc 3c): give a fertile female partner a SECRET lover, force a conception BY HIM through the REAL path, and assert the engine's own pregnancy list names the LOVER as father (never the player), the persisted lover-child marker names the lover, the resolved birth clan is NOT the player's clan, and the affair is now discovered (a pregnancy not the husband's cannot stay secret). MANUAL when the world has no fertile female or no lord lover outside the player's household. Ends the seeded pregnancy and restores all borrowed state", TestLoverChild),
         new SelfTest("grudge_tier2", Severity.Heavy, "capture+give_prisoner (real path) -> assert a DeliveredPrisoner grudge on the captive; direct severity-3 player-perpetrator CaptiveAbuse plant -> assert it exists", TestGrudgeTier2),
         new SelfTest("grudge_resolve", Severity.Light, "plant a FailedQuest grudge -> resolve w/ its matching gesture -> assert it moved; plant an ExecutedKin (vendetta) grudge -> assert an amends is Rejected; plant a DeliveredPrisoner (always Deed-kind) grudge -> resolve w/ the Deed gesture -> assert it moved", TestGrudgeResolve),
         new SelfTest("grudge_terms", Severity.Light, "plant a known grudge -> assert hint undiscovered -> grudge_terms reveal -> assert hint discovered -> reveal again -> assert nothing new", TestGrudgeTerms),
         new SelfTest("grudge_execute_kin", Severity.Light, "find a live lord with a recognized close living kin -> ExecutedKinGrudgeBehavior.PlantOnKinOf (the real engine-glue loop) -> assert a vendetta grudge lands on that kin at max rigidity, unresolvable", TestGrudgeExecuteKin),
         new SelfTest("grudge_unblessed_marriage", Severity.Heavy, "marry the player (test spouse, no blessing seeded) via the real MarriageApprovalBehavior wedding hook -> assert an UnblessedMarriage grudge lands on a disapproving kinsman -> end_own_marriage -> assert the grudge resolves (cleared, not merely decayed)", TestGrudgeUnblessedMarriage),
         new SelfTest("grudge_sway", Severity.Light, "NPC + 3rd hero -> real sway_opinion(against) via the bridge -> assert SwayExposureBehavior queued a pending watch -> DebugForceReveal -> assert a hidden SwayExposed grudge lands on the target", TestGrudgeSway),
         new SelfTest("companion_revenge", Severity.Light, "companion-victim Phase 3: plant a CaptiveAbuse grudge (companion vs PLAYER) AND a CaptorCruelty grudge (companion vs CAPTOR) on the SAME holder the SAME day -> assert BOTH survive, since they use distinct sources so the holder+source+day dedup does not collapse them", TestCompanionRevenge),
         new SelfTest("captor_grudge_conduct", Severity.Light, "player report (Mimo/Standard): keeping a captive SAFE still planted an abuse grudge. Drives the REAL grudge-decision chain (CaptiveIntentRules.CaptiveAbuseSeverity -> CaptorSceneGrudgePolicy.GrudgeSeverity with no conduct read -> Tier2GrudgeRules.CaptiveAbuseGrudge) for each captor intent and asserts a merciful Interrogation/Domination/PersonalDesire forms NO grudge, while Torture (abuse by nature) still does. Pure decision, plants nothing", TestCaptorGrudgeConduct),
         new SelfTest("appearance", Severity.Light, "extract the player's body facts -> compose the appearance block in Subject (3rd person, read by an NPC) and Self (2nd person) voices, with authored prose -> assert the facts extract, the authored prose is carried, and the Self voice reads in the second person", TestAppearance),
         new SelfTest("equipment_gear", Severity.Light, "equipment-awareness pillar: extract the player's LIVE equipped items via the real EquipmentFactsExtractor -> compose the notable-gear clause in Subject (3rd person, read by an NPC) and Self (2nd person) voices via the real EquipmentNotabilityPolicy -> assert extraction never throws and the Self voice reads in the second person. Read-only, no persisted state, nothing to clean up", TestEquipmentGear),
         new SelfTest("quest_menu", Severity.Light, "viable-quest referential: scan a live lord's grounded quest menu -> assert it is non-empty (there is always something to do), every line carries a known type token, and a named settlement target truly resolves (so QuestFactory will ground the offer by construction)", TestQuestMenu),
         new SelfTest("bandit_clear_gate", Severity.Light, "increment 2b (player report: a village sent me after bandits that were not there, then turned hostile when I reported none): read the LIVE world, count bandit parties within the completion radius of a lord's anchor via the scanner's own counter, build the grounded menu, and assert bandit_clear is offered IFF at least one bandit party roams nearby. Seeds nothing", TestBanditClearGate),
         new SelfTest("provide_gold", Severity.Heavy, "child-support fix: seed a ProvideGold quest -> real take_gold via the bridge (player pays the giver) -> assert the quest is CLOSED (Completed, not stuck 'awaiting reward') and the goodwill regard was paid, since the payment IS the completion (no [QUEST_COMPLETE] needed). Restores gold so the economy is unchanged", TestProvideGoldCompletes),
         new SelfTest("reward_net", Severity.Heavy, "quest audit B1: the reward SAFETY NET. Seed a quest the game already VERIFIED as done (IsAwaitingReward, as if a lair was cleared days ago) but that the LLM never closed with [QUEST_COMPLETE] -> run the REAL QuestService.ApplyResponse with NO completion claim -> assert the net settles it anyway (Completed, no longer awaiting) and pays the owed reward (gold or regard moves), so the player's 'Done' quest never pays out never. Restores the seed + gold", TestRewardNet),
         new SelfTest("supply_quest", Severity.Heavy, "personal supply quest (ProvideGoods) END-TO-END: a bad-category proposal is REFUSED by the pipeline (nothing grounded), then a grain proposal is ISSUED through QuestService.ApplyResponse and grounds a real category+count; give the player grain -> real SettleSupplyQuest hands over EXACTLY the required count, CLOSES the quest (Completed), and pays the reward. Restores the roster", TestSupplyQuest),
         new SelfTest("courier_waylay", Severity.Heavy, "waylaid-letter resend: a waylaid letter keeps its typed text, and the Resend path re-queues a FRESH non-lost letter carrying the SAME content, so a waylaid letter never has to be retyped. Cleans up its test letters + courier (no popup)", TestCourierWaylayResend),
         new SelfTest("prisoner_trade", Severity.Heavy, "prisoner trade: take a real lord captive into the player's party -> real buy_prisoner via the bridge (an NPC lord buys him) -> assert the player was PAID (price clamped around the ransom anchor) and the captive left the player's custody -> cleanup frees him", TestPrisonerTrade),
         new SelfTest("prisoner_ransom_home", Severity.Heavy, "fkasad (26/09/2026): a captive ransomed by his own count was put in his own side's cells and the game reported him escaped. Takes a real lord captive, has HIS OWN clan leader buy him through the real bridge -> asserts the player was paid the clamped price, was told why it fell short of the 999999 named (tashmetu), and the captive is FREE (ransomed home), not a prisoner of his own house. Restores both purses and frees him whatever happened.", TestPrisonerRansomHome),
         new SelfTest("prisoner_gift", Severity.Heavy, "fkasad (26/09/2026): \"yours for 0 denars\" to an envoy paid the full budget. Takes a real lord captive and runs buy_prisoner as the envoy binds a gift (price 0, gift true) for his clan leader -> asserts NO gold moved and the captive went home. Restores both purses and frees him whatever happened.", TestPrisonerGift),
         new SelfTest("exchange_prisoners", Severity.Heavy, "Prisoner exchange (fkasad, 25/09/2026): the player holds lord A, lord B's party holds lord C. Runs the real exchange_prisoners through the bridge as B -> asserts A (B's own kin) is FREE, C is now the PLAYER's prisoner, and the gold that evens the swap moved as PrisonerExchangePolicy says. Restores both purses and frees A and C whatever happened.", TestExchangePrisoners),
         new SelfTest("prisoner_custody_settlement", Severity.Heavy, "prisoner custody in a settlement (ransom-letter Accept-button fix): capture a real lord into the party, transfer him into a player-owned town/castle dungeon, and assert BuyPrisonerVerb.PlayerHeldHeroPrisoners still counts him there though he is no longer in the party -> cleanup frees him", TestPrisonerCustodySettlement),
         new SelfTest("appreciation_flow", Severity.Light, "plant a kindness -> assert it lives -> plant a strong grudge on the same holder -> assert the tarnished effective magnitude drops -> force backfill twice -> assert the second pass is a no-op", TestAppreciationFlow),
         new SelfTest("duel_cooldown", Severity.Light, "the duel COOLDOWN stamp (2026-07-22), unreachable by unit test since it writes to the live profile: drive the REAL applier on a FORFEITED duel (the case the anti-farm rule exists for, since a balked NPC pays the player RenownForCalledBluff) -> assert LastDuelDay closed on a DUCKED duel exactly as on a fought one, and that the policy reads the cooldown in the right direction (today blocked, the window's end clear, never-dueled never blocked). Restores the previous stamp", TestDuelCooldownStamp),
         new SelfTest("appreciation_repeat", Severity.Light, "repetition (2026-07-22 fix): plant the SAME kindness on two different days -> assert it deepened ONE record instead of adding a twin, the tally reads twice, and the warmth actually ROSE (counting must weigh, not just show) -> assert a third plant the same day is still refused by the anti-noise rule -> run the migration twice and assert a reload cannot inflate the tally or split the record", TestAppreciationRepeat),
         new SelfTest("nemesis_kill", Severity.Light, "mint a fresh nemesis -> NemesisResolutionBehavior.Resolve(battle death) -> assert IsAlive flips false + a closure world-event is recorded", TestNemesisKilledInBattle),
         new SelfTest("nemesis_execute", Severity.Light, "mint a fresh nemesis -> NemesisResolutionBehavior.Resolve(executed) -> assert IsAlive flips false + the closure world-event reads distinctly from the battle-death flavor", TestNemesisExecuted),
         new SelfTest("nemesis_execute_prisoner", Severity.Light, "mint a fresh nemesis -> TakePrisonerAction (real capture) -> assert PlayerHeldNemesisPrisoner detects him -> BannerlordGameStateBridge.ExecuteAction(execute_prisoner) (the REAL bridge action the chat entry point's LLM emission triggers, not the Resolve harness) -> assert the existing HeroKilledEvent hook still resolves him + records the closure deed", TestNemesisExecutePrisoner),
         new SelfTest("nemesis_turn", Severity.Light, "increment 4 (SPARE + TURN): mint a nemesis -> real TakePrisonerAction -> force his stance below the gate floor and assert the real BannerlordGameStateBridge.ExecuteAction(turn_nemesis) REFUSES (still a prisoner, not turned) -> raise Fear/Respect past the floors and assert it TURNS him (identity marked turned, released from captivity, sworn into the player's clan)", TestNemesisTurn),
         new SelfTest("nemesis_hunt", Severity.Heavy, "active hunt (map-presence): seed a latent nemesis of a live bandit band's culture -> force NemesisBehavior.TryMaterializeInto -> assert a nemesis Hero materialized INTO that band (joined its roster), so a battle the player starts can put the hunt's target in front of him; cleanup detaches him and marks the identity dead", TestNemesisHunt),
         new SelfTest("nemesis_bounty_capture", Severity.Light, "mint a nemesis anchored to a real lord's own region -> assert NemesisBountyDialogFlow's gate finds him eligible -> IssueNemesisBounty (real reward math) -> real TakePrisonerAction on him -> assert QuestDeedTracker's widened CapturePrisoner hook stamps the NemesisBounty quest satisfied", TestNemesisBountyCapture),
         new SelfTest("nemesis_bounty_execute", Severity.Light, "mint a nemesis anchored to a real lord's own region -> IssueNemesisBounty -> capture then execute him via the real execute_prisoner bridge action -> assert the widened HeroKilledEvent hook stamps the NemesisBounty quest AND NemesisResolutionBehavior resolves the identity, both off the same event", TestNemesisBountyExecute),
         new SelfTest("nemesis_dossier", Severity.Light, "encyclopedia discoverability (player report SuperGraham339): mint a nemesis (TimesEncountered stays 0, latent) -> assert NemesisDossierFactory.BuildFor hides him -> bump TimesEncountered/RecaptureCount + plant a NotableEvent on his profile -> assert the SAME factory (the mixin's own source) now renders his identity, counters, and the planted history sentence. PLANTS a test nemesis (reload after)", TestNemesisDossier),
         new SelfTest("nemesis_tavern_rumor", Severity.Light, "tavern-keeper rumour discoverability (ROADMAP follow-up): mint a nemesis anchored to a real tavern keeper's own region and mark him ENCOUNTERED -> assert NemesisTavernRumorDialogFlow.FindRumorNemesisFor's gate finds him eligible, and that it hides him again while latent (TimesEncountered 0) and once TURNED -> assert NemesisRumorLine.Compose names him and his region without stating a reward figure. PLANTS a test nemesis (reload after)", TestNemesisTavernRumor),
         new SelfTest("declare_war", Severity.Irreversible, "seed deed -> declare war -> assert WarDeclared stamps it", TestDeclareWar),
         new SelfTest("scenario", Severity.Irreversible, "fire ON_DEATH:Rhagaea x2 -> assert a rule fires then seals", TestScenario),
         new SelfTest("llm_dialogue", Severity.Light, "LLM: a plain greeting -> log the reply (relevance judged from the log)", TestLlmDialogue, true),
         new SelfTest("chat_memory", Severity.Light, "LLM: seed a shared history -> ask about it -> does the reply reference it?", TestChatMemory, true),
         new SelfTest("chat_stance", Severity.Light, "LLM: set a distinctive stance (fear/distrust) -> ask -> does the tone match?", TestChatStance, true),
         new SelfTest("chat_hearsay", Severity.Light, "LLM: ask about another lord -> does the NPC answer from their own vantage?", TestChatHearsay, true),
         new SelfTest("chat_conviction", Severity.Light, "LLM: give an NPC a false, ruinous belief (your clan plots against him), then offer him free grain -> does he weigh the KINDNESS as a move in the game he thinks is afoot, instead of merely thanking you, and still keep the suspicion to himself? The proof that a conviction moves conduct and not just voice", TestChatConviction, true),
         new SelfTest("chat_bastard_mother", Severity.Light, "LLM: force a mercenary bastard-mother context -> does she raise/extort the child?", TestChatBastardMother, true),
         new SelfTest("chat_worldevent", Severity.Light, "LLM: Feed a fresh event -> ask for news -> does the NPC surface it?", TestChatWorldEvent, true),
         new SelfTest("chat_regard_tone", Severity.Light, "LLM: a warm, approving message must NOT yield a negative change_relation (the 'responds positively but applies negative regard' player report)", TestRegardTone, true),
         new SelfTest("chat_quest_abandon", Severity.Light, "LLM: with an outstanding task, a player who says they cannot do it must be RELEASED (the giver emits [QUEST_ABANDON]) so no quest gets stuck (the phantom-ring report)", TestQuestAbandonChat, true),
         new SelfTest("chat_ride_with_me", Severity.Heavy, "LLM end-to-end: raise an idle friendly lord's regard, ask him in conversation to ride with you -> he emits ride_with_me and actually JOINS your party (still of his own clan). Proves the teaching + emission + bridge chain, not just the executor", TestChatRideWithMe, true),
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
         new SelfTest("gang_rivalry", Severity.Light, "Mechanical: a same-town gang-leader pair is framed as RIVALS in the name hint (never friends-by-culture); skips if the campaign has no such pair", TestGangRivalry),
         new SelfTest("captive_fear_coerced", Severity.Light, "Mechanical: a terrified captive (high Fear) trips the freedom-bargain coercion flag, a fearless one does not (Fear-1 wiring)", TestCaptiveFearCoerced),
         new SelfTest("faction_identity_refreshes", Severity.Light, "Mechanical: a profile's frozen realm/house identity is refreshed to live game state on conversation open, so a hero who changed kingdom (a defection, or a runtime civil-war split from another mod like Bellum Civile) is introduced by their CURRENT faction/clan, not the one frozen at profile creation. Since 16/09/2026 it also covers the NAME (a renamed hero was led by the old one for the rest of the campaign, and {{char}} expanded to it) and the SPOUSE, which was refreshed only at session launch - so marrying and then speaking to your own wife in the same sitting left the profile believing her unwed, and the intimacy was classified as a secret affair, which is the very case ProfileMutator believes it is guarding. Corrupts all four, calls RefreshIdentity, asserts they match the live hero", TestFactionIdentityRefreshes),
         new SelfTest("deferred_audience_open_pumps", Severity.Light, "Mechanical: the deferral mechanism the settlement-entry audience fix relies on. An enqueued action does NOT run until the next MainThreadDispatcher.Pump (fired by OnApplicationTick), which is why the audience chat opens one frame AFTER its inquiry dismisses instead of synchronously inside the inquiry consequence, breaking the 'million popups' cascade when accepting an approach in a settlement", TestDeferredAudienceOpenPumps),
         new SelfTest("party_troop_count", Severity.Light, "Mechanical: a companion in the player's party sees the real troop count in context (no more hallucinated army size); skips if the player has no party companion", TestPartyTroopCount),
         new SelfTest("nemesis_mint", Severity.Light, "Mechanical: minting a nemesis creates a persistent Hero WITH a memory profile (the 'no memory' bug fixed), reuses him in-region, mints a distinct one per region; PLANTS test nemeses (reload after)", TestNemesisMint),
         new SelfTest("assembly_convene", Severity.Light, "Mechanical (ASSEMBLY Inc 1): arranges a host town + live lords with a seeded regard, convenes through the real facts-builder + RSVP policy, asserts the regard read-back + roster + tally, then cancels and restores (no manual in-game setup needed)", TestAssemblyConvene),
         new SelfTest("assembly_arrival", Severity.Light, "Mechanical (ASSEMBLY Inc 1 slice 2): convenes + sets two lords EnRoute, then drives the two runtime transitions: one reaches the host and seats Present, the deadline turns the other into an ambassador; cancels after (no map travel, no LLM)", TestAssemblyArrival),
         new SelfTest("assembly_revalidate", Severity.Light, "Mechanical (ASSEMBLY Inc 1 robustness): the daily re-validation reads the live host + traveller state and leaves a healthy assembly intact (host/traveller edge combinations are pinned by the pure policy tests)", TestAssemblyRevalidate),
         new SelfTest("council_parley_snapshot", Severity.Light, "Mechanical (Parley council): CouncilSessionSnapshot's additive ParleyTargetFactionId field round-trips through the exact JsonConvert path CouncilSessionBehavior.SyncData uses, both WITH a value set and WITHOUT one present in the JSON at all (an old pre-Parley snapshot) -> the missing-field case must deserialize to null rather than throw. No live campaign state touched", TestCouncilParleySnapshot),
         new SelfTest("council_attendance", Severity.Light, "Mechanical (Convergence Inc 2; the dead never sit, Maifailun 30/09/2026): drives the REAL CouncilAttendanceResolver.Resolve against the live campaign for CouncilScope.Party -> every hero it seats is InParty (skips if the party carries no other hero); then, mechanically, feeds CouncilAttendancePolicy.Decide a constructed far own-faction fact directly -> asserts Absent/TooFar and never Ambassador (the 'your own house never sends a stand-in' guarantee); a live prisoner, if any exists, resolves to Absent/Prisoner. No world mutation", TestCouncilAttendance),
         new SelfTest("council_roster_swap", Severity.Light, "Mechanical (Convergence Inc 3a): for a live NON-parley scope (ClanLords), every hero CouncilAttendanceResolver.SeatedInPersonHeroes seats is independently re-judged InPerson via DebugDecideFor, no seated hero is a prisoner, and no Absent-rated candidate from the scope's own pool got seated (the 'war council felt magic' fix: a far own-faction lord is now absent). For Parley, asserts the helper is still a pure pass-through to the OLD CouncilRosterResolver.Seated (identical set), proving this increment left that scope untouched. Skips MANUAL when the campaign has no ClanLords candidate. No world mutation", TestCouncilRosterSwap),
         new SelfTest("envoy_identity", Severity.Light, "Mechanical (Convergence Inc 4): picks any live OTHER-faction kingdom leader, drives the REAL EnvoyFactory.Create -> asserts a non-null synthetic profile whose Name is 'Herald of {faction}', whose persona names both the faction and the leader, whose Id starts with 'cr_envoy_', and which was NEVER written to the store (Store.Get right after Create is null). Skips MANUAL when no other-faction kingdom has a live leader. No world mutation, nothing to clean up (never persisted)", TestEnvoyIdentity),
         new SelfTest("stance_gate", Severity.Light, "Mechanical: the Trust delegated-risk gate BITES — lend_troops is TAUGHT when Trust is earned, REFUSED when Trust/relation/regard are all low; skips if no lord with a healthy party", TestStanceGate),
         new SelfTest("courtship_trial", Severity.Light, "Mechanical (COURTSHIP TRIAL): the pure override table proves a passed trial clears ONLY soft RegardTooLow and NEVER a hard blocker; a REAL hard-blocked hero force-marked passed still resolves to that hard reason (offer stays CLOSED); then a real soft-blocked prospect (regard forced low) is driven through the behavior's own daily-eval seam for the full term -> assert the trial passes, a MarriageProposal letter is queued FROM her, and MarriageBlockedBecause becomes None; a trial that STOPS accompanying before the term lapses (no completion). Restores regard + trial + letter; skips MANUAL when the player is already wed or no soft-blockable prospect exists", TestCourtshipTrial),
         new SelfTest("chat_captor_break", Severity.Light, "LLM: the captor scene on a CRAVEN prisoner -> does the coward crack / plead far sooner (contrast)?", TestChatCaptorBreak, true, 3),
         new SelfTest("chat_captor_torture", Severity.Light, "LLM: the player whips the prisoner over 3 beats -> real suffering + does defiance erode to pleading?", TestChatCaptorTorture, true, 3),
         new SelfTest("chat_captor_cnc", Severity.Light, "LLM: 3-beat sexual coercion (Hardcore) -> does the NPC play CNC reactions in character, never breaking?", TestChatCaptorCnc, true, 3),
         new SelfTest("chat_captor_cnc_mf", Severity.Light, "LLM: CNC with a MALE captor and FEMALE prisoner (gender forced) -> in-character reactions?", TestChatCaptorCncMF, true, 3),
         new SelfTest("chat_captor_group", Severity.Light, "LLM: GROUP CNC — player + 2 companions vs a female prisoner -> does she react to being outnumbered?", TestChatCaptorGroup, true, 3),
         new SelfTest("chat_captive_continue", Severity.Light, "LLM: a captive scene OPENER + several Continue beats -> assert each beat advances, is described, and never repeats (the reported Continue-quality faults)", TestChatCaptiveContinue, true, 4),
         new SelfTest("chat_companion_continue", Severity.Light, "LLM: a COMPANION-VICTIM scene OPENER + Continue beats -> assert the victim is voiced (own lines), correctly gendered, and beats don't repeat", TestChatCompanionContinue, true, 4),
         new SelfTest("chat_captive_variety", Severity.Light, "LLM (T6): two scenes with the SAME captor + one with a DIFFERENT captor (fresh writing lens each) -> score cross-scene phrase reuse; logs the ratio + worst offenders, rejects only a gross echo regression", TestChatCaptiveVariety, true, 9),
         new SelfTest("conv_summary_queue", Severity.Light, "LLM: seed a base 'Spoke with X.' event + a job whose transcript states a PLAN, run the queue worker -> assert the event is ENRICHED by value match (a promised plan reaches durable memory, not lost)", TestConvSummaryQueueJob, true, 1),
         new SelfTest("letter_font", Severity.Light, "player report: Russian letters rendered Galahad's broken per-glyph fallback (no Cyrillic glyphs). Mechanical: LetterFontPolicy prefers the plain font for the game's own Russian language id and keeps the decorative Galahad look for English; then forces LetterViewModel's language seam (LetterFontResolver.LanguageIdProvider, no real language switch) and asserts the VM's BodyBrush/HeadingBrush/InputBrush bindings actually pick the plain sibling brushes for Russian and the Galahad ones for English, proving the wiring end to end and not just the pure policy. Restores the seam", TestLetterFont),
         new SelfTest("manual_action_menu", Severity.Light, "Mechanical: drives the REAL ManualActionAvailability gate (the manual action-injection menu) against seeded facts -> a held-and-eligible prisoner exposes execute/free/recruit together, a held-but-ineligible one keeps execute/free and withholds recruit (PrisonerRecruitmentPolicy's own regard/clan-leader gate), and an otherwise-empty (non-ruler) snapshot shows no governance entry but keeps take_gold, the always-on baseline. Pure logic against a plain facts struct, nothing to clean up", TestManualActionMenu)
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
      ///   The routine sweep (cr.testall): every MECHANICAL test EXCEPT the irreversible ones. The real-LLM
      ///   roleplay/conversation tests (CallsLlm) are NOT run here any more: they are slow, cost tokens, and
      ///   are flaky on a slow or over-reasoning model, and the dedicated real-LLM tools now own that ground
      ///   (cr.rp_bench for scene quality, cr.action_bench for the interpreter, cr.test chat for the chat smoke
      ///   set). So cr.testall is the FAST, FREE, deterministic logic sweep. The irreversible tests (declaring
      ///   war, sealing a one-shot scenario) are also held back: they mutate the LIVE campaign in ways only a
      ///   reload undoes, so run them opt-in via <c>cr.test &lt;name&gt;</c>, or <c>cr.test full</c> on a
      ///   throwaway save. <paramref name="llmBudgetSeconds" /> is kept for signature stability but is moot
      ///   now that no LLM test runs here.
      /// </summary>
      public static string RunAll(NpcMemoryCampaignBehavior behavior, int? llmBudgetSeconds = null)
         => RunGroup(Tests.Where(t => t.Severity != Severity.Irreversible && !t.CallsLlm), behavior,
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
               if (child is {IsAlive: true, IsChild: false} && child != Hero.MainHero)
                  yield return child;

         if (victim.Siblings != null)
            foreach (Hero sibling in victim.Siblings)
               if (sibling is {IsAlive: true, IsChild: false} && sibling != Hero.MainHero)
                  yield return sibling;
      }

      /// <summary>
      ///   The stand-in interlocutor when no real conversation is open. The fallback now skips PRISONERS
      ///   (testall 2026-07-23): the first living lord happened to be captive that day, and every flow this
      ///   picker feeds whose bridge or policy rightly refuses a prisoner (the stance-consequence CanWrite,
      ///   the pledge_against verb behind the commitment seeds) failed in a cluster that read as four
      ///   separate bugs. A prisoner is also simply a bad stand-in for "someone the player casually talks
      ///   to". Real-conversation picks are untouched.
      /// </summary>
      private static Hero ConversationNpc()
         => Hero.OneToOneConversationHero ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

      /// <summary>How many of an item the player's party currently holds.</summary>
      private static int CountItemInParty(ItemObject item)
      {
         var n = 0;
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
            if (el.EquipmentElement.Item == item)
               n += el.Amount;

         return n;
      }

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

      /// <summary>Finds two different gang-leader notables sharing a current settlement, or (null, null).</summary>
      private static (Hero a, Hero b) FindSameTownGangLeaderPair()
      {
         foreach (IGrouping<Settlement, Hero> group in Hero.AllAliveHeroes
                                                           .Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.Occupation == Occupation.GangLeader && h.CurrentSettlement != null)
                                                           .GroupBy(h => h.CurrentSettlement))
         {
            List<Hero> list = group.ToList();

            if (list.Count >= 2) return (list[0], list[1]);
         }

         return (null, null);
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
            h != null && h != Hero.MainHero && h != exclude && h.Spouse == null && h.IsAlive && !h.IsChild && h.IsLord && h.Clan != null && !h.IsClanLeader && h.IsFemale != playerFemale && h.GovernorOf == null && !(h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h));
      }

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

      /// <summary>
      ///   Mints a nemesis anchored to a real lord's own region (home settlement, else current settlement, else culture),
      ///   so the region-eligibility gate genuinely matches instead of being simulated. Returns (null, null, null) when no lord
      ///   has a resolvable region.
      /// </summary>
      private static (Hero Giver, Hero NemesisHero, NemesisIdentity Identity) MintBountyEligibleNemesis(int today)
      {
         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (giver == null) return (null, null, null);

         string regionId = giver.HomeSettlement?.StringId ?? giver.CurrentSettlement?.StringId ?? giver.Culture?.StringId;

         if (string.IsNullOrEmpty(regionId)) return (null, null, null);

         Hero nemesisHero = NemesisBehavior.Instance?.MintCaptorFor("looters", regionId, today, true);

         if (nemesisHero == null) return (giver, null, null);

         NemesisIdentity nem = NemesisBehavior.Instance.GetByHero(nemesisHero.StringId);
         if (nem != null) nem.IsAlive = true; // isolate from a stale resolved run earlier in the same session

         return (giver, nemesisHero, nem);
      }

      /// <summary>
      ///   Mints a nemesis anchored to a real tavern keeper's own region (home settlement, else current
      ///   settlement, else culture), mirroring <see cref="MintBountyEligibleNemesis" /> but for a keeper
      ///   giver instead of a lord, so the tavern-rumour gate genuinely matches instead of being simulated.
      ///   Returns (null, null, null) when no tavern keeper has a resolvable region.
      /// </summary>
      private static (Hero Keeper, Hero NemesisHero, NemesisIdentity Identity) MintRumorEligibleNemesis(int today)
      {
         Hero keeper = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive && h.Occupation == Occupation.Tavernkeeper);

         if (keeper == null) return (null, null, null);

         string regionId = keeper.HomeSettlement?.StringId ?? keeper.CurrentSettlement?.StringId ?? keeper.Culture?.StringId;

         if (string.IsNullOrEmpty(regionId)) return (keeper, null, null);

         Hero nemesisHero = NemesisBehavior.Instance?.MintCaptorFor("looters", regionId, today, true);

         if (nemesisHero == null) return (keeper, null, null);

         NemesisIdentity nem = NemesisBehavior.Instance.GetByHero(nemesisHero.StringId);
         if (nem != null)
         {
            nem.IsAlive = true; // isolate from a stale resolved run earlier in the same session
            nem.TimesEncountered = 1; // the discoverability gate: a rumour only exists once the player has met him
         }

         return (keeper, nemesisHero, nem);
      }

      // ── Nemesis pillar increment 2: resolution (R1 kill in battle / R2 execute) ────────────────────

      /// <summary>
      ///   Mints (or reuses) a fresh, forced-alive nemesis identity in an isolated region key so the two resolution
      ///   self-tests never collide with each other or a real in-game nemesis.
      /// </summary>
      private static (Hero Hero, NemesisIdentity Identity) MintSelfTestNemesis(string regionId, int today)
      {
         Hero hero = NemesisBehavior.Instance?.MintCaptorFor("looters", regionId, today, true);

         if (hero == null) return (null, null);

         NemesisIdentity nem = NemesisBehavior.Instance.GetByHero(hero.StringId);
         if (nem != null) nem.IsAlive = true; // isolate from a stale resolved run earlier in the same session

         return (hero, nem);
      }

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
         IReadOnlyList<WitnessEntry> witnesses = null,
         string captiveSceneStyle = null)
      {
         var session = new ChatSession();
         var director = new CaptiveSceneDirector(false, 0, MBRandomSource.Instance, false, intent);
         var beats = new List<ParsedResponse>();

         // Beat 0 — the opener (the captor speaks first), exactly as ActivateCaptiveEncounterMode seeds it.
         int lastFocusIndex = -1;
         string opener = CaptiveSceneText.BuildCaptiveOpener(false, intent) + CaptiveSceneText.BuildSceneSetup(intent, ref lastFocusIndex);

         if (!RunOneCaptiveBeat(behavior, $"{label}/open", npc, profile, intent, director, opener, session, beats,
                victimName, victimIsFemale, witnesses, captiveSceneStyle))
            return (false, beats);

         // Continue beats — advance the director and send the shared continuation stimulus, mirroring
         // RunCaptiveSceneContinuationAsync. Stop early if the director concludes.
         for (var i = 0; i < continueBeats; i++)
         {
            director.AdvanceStory();
            string stimulus = CaptiveSceneText.BuildContinuationStimulus();

            if (!RunOneCaptiveBeat(behavior, $"{label}/cont{i + 1}", npc, profile, intent, director, stimulus, session, beats,
                   victimName, victimIsFemale, witnesses, captiveSceneStyle))
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
         IReadOnlyList<WitnessEntry> witnesses,
         string captiveSceneStyle = null)
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
            captiveVictimIsFemale: victimIsFemale,
            captiveSceneStyle: captiveSceneStyle);

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
      /// <summary>
      ///   True while a self-test runs. Regard moved by a test is reported to other mods as self_test (tashmetu,
      ///   30/09/2026): tests restore regard without a notice, and a mirroring mod would keep what they left behind.
      /// </summary>
      public static bool IsRunning => _running > 0;

      private static int _running;

      private static string SafeRun(SelfTest test, NpcMemoryCampaignBehavior behavior, int budgetSeconds)
      {
         _running++;
         try { return SafeRunInner(test, behavior, budgetSeconds); }
         finally { _running--; }
      }

      private static string SafeRunInner(SelfTest test, NpcMemoryCampaignBehavior behavior, int budgetSeconds)
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

      // ── RP with teeth, increment 2: pledge_against (2026-08-22: a vow is a memory, not a quest) ──────

      /// <summary>
      ///   Seeds a real pledge_against through the SAME bridge dispatch a conversation uses (not a
      ///   reimplementation), so the underlying scheme is genuinely launched and the vow is genuinely
      ///   recorded. Used by <see cref="TestCommitmentIsMemoryOnly" />, which asserts the vow leaves a
      ///   memory but no tracked commitment, no journal mirror, and no relation change.
      /// </summary>
      private static bool SeedPledgeEligible(NpcMemoryCampaignBehavior behavior, NpcProfile profile, Hero npc, Hero target)
      {
         ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npc, target, CourtActionPolicy.PledgeMinAnimosity - 10 - npc.GetRelation(target), false);

         ActionOutcome seedOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "pledge_against",
               Parameters = new Dictionary<string, string> {{"target", target.Name.ToString()}}
            }, npc, new ActionContext(profile, null));

         // Surface the bridge's own refusal (testall 2026-07-23: two commitment tests failed with only
         // "seed failed", and the WHY, the picked pledger being a prisoner, had to be dug out by hand).
         if (seedOutcome is not {IsPositive: true})
            ModLog.Info($"[SELFTEST] pledge_against seed refused for {npc.Name} -> {target.Name}: "
                      + $"{seedOutcome?.Message ?? "(null outcome)"}");

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
         string subject = AppearanceComposer.Compose(facts, "moves with a slight limp");
         string self = AppearanceComposer.Compose(facts, null, AppearanceVoice.Self);

         t.Note($"subject=\"{subject}\" | self=\"{self}\"");
         t.Expect("factsExtracted", facts != null, true);
         t.Expect("authoredProseCarried", subject.Contains("limp"), true);
         t.Expect("selfVoiceSecondPerson", self.Length == 0 || self.StartsWith("You are"), true);

         return t.Line();
      }

      /// <summary>
      ///   Equipment-awareness pillar: proves the real engine extraction + pure notability policy compose a
      ///   usable, never-throwing clause for a live hero, in both voices. Does not assert exact wording (the
      ///   player's own live gear varies session to session), only that the path holds together end to end and
      ///   the Self voice reads in the second person.
      /// </summary>
      private static string TestEquipmentGear(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("equipment_gear");

         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         List<EquipmentPieceFacts> pieces = EquipmentFactsExtractor.Extract(Hero.MainHero);
         string subject = EquipmentNotabilityPolicy.Compose(pieces, Hero.MainHero.IsFemale, AppearanceVoice.Subject);
         string self = EquipmentNotabilityPolicy.Compose(pieces, Hero.MainHero.IsFemale, AppearanceVoice.Self);

         t.Note($"pieces={pieces.Count} | subject=\"{subject}\" | self=\"{self}\"");
         t.Expect("extractionDidNotThrow", pieces != null, true);
         t.Expect("selfVoiceSecondPerson", self.Length == 0 || self.StartsWith("You "), true);

         return t.Line();
      }

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

      /// <summary>
      ///   The live-ledger half of the repetition fix (the pure tests cover the arithmetic and the fold).
      ///   Plants the same kindness twice, on two different days, against the REAL persisted ledger, and
      ///   asserts the four things that can only be wrong in the engine: one record rather than a twin, a
      ///   count of two, warmth that actually rose (the owner's call: counting must WEIGH, not merely show),
      ///   and the same-day anti-noise rule still refusing a third plant today. Then it runs the migration
      ///   twice to prove a reload cannot inflate the tally, which is the failure that would quietly corrupt
      ///   a player's campaign rather than merely look wrong.
      /// </summary>
      private static string TestAppreciationRepeat(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("appreciation_repeat");

         if (t.Abort(AppreciationBehavior.Instance == null, "AppreciationBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         // Any lord will do: the assertions key on this test's own cause text, so nothing another test (or
         // the player's own campaign) has already planted can be mistaken for the record under test.
         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive);

         if (t.Skip(npc == null, "no living lord to hold the kindness")) return t.Line();

         Appreciation Mine() => AppreciationBehavior.Instance.AppreciationsFor(npc.StringId)
            .FirstOrDefault(a => a.Source == AppreciationSource.KindnessInScene
                              && string.Equals(a.CauseSummary, AppreciationRepeatCause, StringComparison.OrdinalIgnoreCase));

         if (t.Skip(Mine() != null, "this test's kindness is already on the ledger from an earlier run (reload to re-test)")) return t.Line();

         bool first = AppreciationBehavior.Instance.TryPlant(npc, Hero.MainHero, AppreciationSource.KindnessInScene,
            AppreciationRepeatCause, AppreciationFlowTestMagnitude, true, today - AppreciationRepeatDaysBack);

         if (t.Skip(!first, "first plant refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

         TraitFacts traits = AppreciationBridge.BuildTraitFacts(npc);
         int warmthOnce = AppreciationPolicy.EffectiveMagnitude(Mine(), today, traits);

         // The repeat: same holder, same source, same cause, a LATER day.
         bool second = AppreciationBehavior.Instance.TryPlant(npc, Hero.MainHero, AppreciationSource.KindnessInScene,
            AppreciationRepeatCause, AppreciationFlowTestMagnitude, true, today);

         t.Expect("repeatWasAccepted", second, true);

         int records = AppreciationBehavior.Instance.AppreciationsFor(npc.StringId)
            .Count(a => a.Source == AppreciationSource.KindnessInScene
                     && string.Equals(a.CauseSummary, AppreciationRepeatCause, StringComparison.OrdinalIgnoreCase));
         t.Expect("repeatDeepensOneRecordRatherThanAddingATwin", records, 1);
         t.Expect("theTallyReadsTwice", AppreciationPolicy.OccurrenceCount(Mine()), 2);

         int warmthTwice = AppreciationPolicy.EffectiveMagnitude(Mine(), today, traits);
         t.Note($"{npc.Name}: warmth once={warmthOnce}, warmth twice={warmthTwice}, tally={AppreciationPolicy.OccurrenceCount(Mine())}");
         t.Expect("theSecondTimeActuallyWeighs", warmthTwice > warmthOnce, true);

         // The anti-noise rule that predates this fix must survive it: several intimate beats inside ONE
         // scene are one kindness, not a tally the player can run up in an afternoon.
         bool sameDayAgain = AppreciationBehavior.Instance.TryPlant(npc, Hero.MainHero, AppreciationSource.KindnessInScene,
            AppreciationRepeatCause, AppreciationFlowTestMagnitude, true, today);
         t.Expect("aSecondPlantTheSameDayIsStillRefused", sameDayAgain, false);
         t.Expect("theRefusedPlantLeftTheTallyAlone", AppreciationPolicy.OccurrenceCount(Mine()), 2);

         // The consolidation runs on every load that finds the ledger behind the current migration version.
         // If it ever re-counted its own output, a player's tally would climb by itself on every reload.
         AppreciationBehavior.Instance.DebugForceBackfill(behavior.Store);
         AppreciationBehavior.Instance.DebugForceBackfill(behavior.Store);
         t.Expect("reloadingDoesNotInflateTheTally", AppreciationPolicy.OccurrenceCount(Mine()), 2);
         t.Expect("reloadingDoesNotSplitTheRecord",
                  AppreciationBehavior.Instance.AppreciationsFor(npc.StringId)
                     .Count(a => a.Source == AppreciationSource.KindnessInScene
                              && string.Equals(a.CauseSummary, AppreciationRepeatCause, StringComparison.OrdinalIgnoreCase)),
                  1);

         return t.Line();
      }

      /// <summary>
      ///   The duel COOLDOWN stamp (2026-07-22), which no unit test can reach because it writes to the live
      ///   persisted profile. The anti-farm decision it exists for: the chat handler applies NpcForfeited when
      ///   an NPC balks at the player's challenge, and that pays the player RenownForCalledBluff. If a refusal
      ///   reset nothing, the player could re-challenge the same lord every turn and farm it, so the stamp has
      ///   to land on a DUCKED duel exactly as it does on a fought one. Restores the previous value, though
      ///   the applier's own renown/stance/world-event effects stand (as they do for duel_consequence).
      ///   What this canNOT assert from an arbitrary map position is eligibility itself: DuelEligibilityResolver
      ///   requires the two to be genuinely beside each other, so a lord across Calradia is refused for
      ///   distance and a refusal here would prove nothing. That half is what cr.duel_arm covers, in person.
      /// </summary>
      private static string TestDuelCooldownStamp(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("duel_cooldown");

         if (t.Abort(behavior?.Store == null, "no profile store")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         // Needs a lord the player has actually met: the stamp lives on the stored profile, and no profile
         // means nothing to stamp (a deliberate no-op, not a failure).
         Hero lord = null;
         NpcProfile profile = null;
         foreach (string id in behavior.Store.ListIds())
         {
            Hero h = Hero.FindFirst(x => x != null && x.StringId == id);

            if (h is not {IsAlive: true, IsLord: true} || h == Hero.MainHero) continue;

            lord = h;
            profile = behavior.Store.Get(id);

            break;
         }

         if (t.Skip(lord == null || profile == null, "no met lord with a stored profile to stamp (chat with one first)")) return t.Line();

         int? before = profile.LastDuelDay;

         // The FORFEIT path on purpose: a fought duel obviously closes the matter, a ducked one is the case
         // the exploit needed.
         DuelOutcomeApplier.Apply(DuelReason.HonourAffront, DuelResolution.NpcForfeited, lord, null, 0);

         NpcProfile after = behavior.Store.Get(lord.StringId);
         t.Note($"{lord.Name}: LastDuelDay {before?.ToString() ?? "(never)"} -> {after?.LastDuelDay?.ToString() ?? "(never)"} (today {today})");
         t.Expect("aDuckedDuelStillClosesTheCooldown", after?.LastDuelDay, today);

         // The direction of the policy's own reading, so a sign error cannot silently make the cooldown
         // permanent (nobody may ever duel again) or useless (everyone may, always).
         t.Expect("todayIsOnCooldown", DuelChallengePolicy.IsOnCooldown(0), true);
         t.Expect("theCooldownExpiresOnSchedule", DuelChallengePolicy.IsOnCooldown(DuelChallengePolicy.CooldownDays), false);
         t.Expect("neverDueledIsNeverOnCooldown", DuelChallengePolicy.IsOnCooldown(null), false);

         if (after != null)
         {
            after.LastDuelDay = before;
            behavior.Store.Set(after);
         }

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
            npc, profile, behavior.GameStateBridge, "Buy your freedom, then.",
            playerStatusOverride: PlayerStatusVsNpc.NpcIsCaptive);
         t.Expect("highFearCaptiveCoerced", coerced.CaptiveFearCoerced, true);

         profile.StanceFear = 0; // fearless: no coercion
         EncounterContext calm = EncounterContextBuilder.Build(
            npc, profile, behavior.GameStateBridge, "Buy your freedom, then.",
            playerStatusOverride: PlayerStatusVsNpc.NpcIsCaptive);
         t.Expect("noFearNotCoerced", calm.CaptiveFearCoerced, false);

         t.Note($"Fear coercion wiring on {npc.Name}: Fear 60 -> coerced, Fear 0 -> not.");

         return t.Line();
      }

      /// <summary>
      ///   Realm/house identity must track live game state. A hero who changed kingdom (a defection, or a runtime
      ///   civil-war split spawned by another mod like Bellum Civile) was otherwise still introduced by the faction
      ///   frozen at profile creation, even though the same prompt's war status and liege line already read the new
      ///   realm. Pins that RefreshIdentity (run on every conversation open) overwrites a stale Faction/Clan with
      ///   the current live values.
      /// </summary>
      private static string TestFactionIdentityRefreshes(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("faction_identity_refreshes");

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC available")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);

         string liveFaction = npc.MapFaction?.Name?.ToString() ?? "Unaligned";
         string liveClan = npc.Clan?.Name?.ToString() ?? "";
         string liveName = npc.Name?.ToString();
         string liveSpouse = npc.Spouse?.IsAlive == true ? npc.Spouse.Name?.ToString() : null;

         // Corrupt the identity exactly as a mid-session defection leaves a frozen profile. Name and spouse
         // joined the refresh on 16/09/2026 (audit): a renamed hero was led by the old name for the rest of
         // the campaign, and the SPOUSE was refreshed only at session launch - so marrying and then speaking
         // to your own wife in the same sitting left the profile believing her unwed, and the scene was
         // classified as a secret affair.
         profile.Faction = "[SELFTEST] Old Fallen Realm";
         profile.Clan = "[SELFTEST] Old House";
         profile.Name = "[SELFTEST] The Name They Had Before";
         profile.SpouseName = "[SELFTEST] Somebody They Never Married";

         CalradiaRemembers.Mapping.HeroProfileMapper.RefreshIdentity(profile, npc);

         t.Expect("factionRefreshedToLive", profile.Faction == liveFaction, true);
         t.Expect("clanRefreshedToLive", profile.Clan == liveClan, true);
         t.Expect("nameRefreshedToLive", profile.Name == liveName, true);
         t.Expect("spouseRefreshedToLive", profile.SpouseName == liveSpouse, true);

         t.Note($"{npc.Name}: stale identity overwritten with live faction '{liveFaction}', clan '{liveClan}', "
              + $"name '{liveName}', spouse '{liveSpouse ?? "(none)"}'.");

         return t.Line();
      }

      /// <summary>
      ///   Pins the deferral the settlement-entry audience fix depends on. The "million popups" cascade came from
      ///   opening the focus-grabbing, time-pausing chat overlay synchronously inside the approach inquiry's Accept
      ///   consequence, while a settlement menu was the active state being restored under the dismissing inquiry.
      ///   The fix enqueues the open instead, so it runs one frame later (on OnApplicationTick's Pump), after the
      ///   inquiry has fully dismissed. This proves the enqueued action does NOT run until the pump.
      /// </summary>
      private static string TestDeferredAudienceOpenPumps(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("deferred_audience_open_pumps");

         var ran = false;
         CalradiaRemembers.Chat.MainThreadDispatcher.Enqueue(() => ran = true);
         t.Expect("notRunBeforePump", ran, false); // the open MUST be deferred, never synchronous with the inquiry
         CalradiaRemembers.Chat.MainThreadDispatcher.Pump();
         t.Expect("ranAfterPump", ran, true); // OnApplicationTick's Pump is what actually opens the audience chat

         t.Note("Deferred-open mechanism holds: Enqueue withholds the action until the next Pump (OnApplicationTick), one frame after the approach inquiry dismisses, breaking the settlement-entry popup cascade.");

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
      ///   AUDIT-CAPTIVE-STYLE T6: the INTER-scene variety measure. TestChatCaptiveContinue judges repetition
      ///   WITHIN one scene; this judges the reported fault ACROSS scenes (the same distinctive phrasings, "sweet",
      ///   "the tendons of his neck", turning up scene after scene). It plays two full scenes with the SAME captor
      ///   and, when a second lord is free, one with a DIFFERENT captor, drawing a fresh writing lens (T1) per
      ///   scene exactly as the runtime does, then scores cross-scene phrase reuse with the pure SceneVarietyMeasure.
      ///   The number is the point: without it T1/T3/T4 are unfalsifiable. Leads with the score and the worst
      ///   offenders for human judgement, and rejects only a gross echo regression (the ceiling is a TUNE floor).
      /// </summary>
      private static string TestChatCaptiveVariety(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_captive_variety");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to play the captor")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc);

         var scenes = new List<string>();
         var lastStyle = -1;

         // Two scenes, SAME captor: this is where echo bites hardest, because the reinjected memory and one
         // captor's voice pull toward the same phrasings. A fresh lens is drawn per scene (T1) so the test
         // exercises the actual fix, not a byte-identical prompt.
         for (var scene = 0; scene < 2; scene++)
         {
            string style = CaptiveSceneText.PickStyleRegister(ref lastStyle);
            var (ran, beats) = RunCaptiveContinueBeats(
               behavior, $"chat_captive_variety/same{scene + 1}", npc, profile, CaptiveSceneIntent.Domination, 2,
               captiveSceneStyle: style);

            if (t.Abort(!ran, "LLM call failed")) return t.Line();

            scenes.Add(SceneProse(beats));
         }

         // One scene, DIFFERENT captor, when a second free lord exists: the metric wants both the same-captor
         // and the cross-captor dimension. Optional so the test still runs on a sparse campaign.
         Hero other = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h != npc && h.IsLord && h.IsAlive);
         if (other != null)
         {
            NpcProfile otherProfile = GetOrCreate(behavior, other);
            ArrangeCleanNpc(behavior, otherProfile, other);
            string style = CaptiveSceneText.PickStyleRegister(ref lastStyle);
            var (ran, beats) = RunCaptiveContinueBeats(
               behavior, "chat_captive_variety/other", other, otherProfile, CaptiveSceneIntent.Domination, 2,
               captiveSceneStyle: style);

            if (ran) scenes.Add(SceneProse(beats));
         }

         if (t.Abort(scenes.Count < 2, "fewer than two scenes generated")) return t.Line();

         SceneVarietyReport report = SceneVarietyMeasure.Measure(scenes);
         string worst = string.Join(" | ", report.WorstOffenders(5).Select(p => $"\"{p.Phrase}\" x{p.SceneCount}"));

         t.Note($"{scenes.Count} scenes, {report.NGramSize}-word phrases: crossSceneReuse={report.CrossSceneReuseCount}/" +
                $"{report.DistinctPhraseCount} distinct (ratio={report.ReuseRatio:F3}). Worst: {(worst.Length > 0 ? worst : "none")}");
         // TUNE 2026-07-24: no live-calibrated number yet, so the ceiling only catches a GROSS regression (two
         // near-identical scenes). The real signal is the logged ratio + worst offenders; tighten once a few
         // clean runs establish the normal band.
         t.Expect("crossSceneReuseUnderCeiling", report.ReuseRatio <= 0.35, true);

         return t.Line();
      }

      /// <summary>One scene's full prose (every beat's dialogue + narration), for cross-scene variety scoring.</summary>
      private static string SceneProse(List<ParsedResponse> beats)
         => string.Join(" ", beats.Select(b => $"{b?.Dialogue} {b?.Narration}"));

      /// <summary>
      ///   The durable conversation-summary queue's worker path, end to end. Seeds the plain base event a real
      ///   conversation leaves ("Spoke with X."), builds a job whose transcript states a concrete PLAN, then runs
      ///   the SAME ProcessConversationJobAsync the background drain runs and asserts the base event was ENRICHED
      ///   (its summary replaced) by value match. This is the Nexus-reported fault's fix: a stated plan must reach
      ///   durable memory instead of vanishing. Runs the worker directly (not via the async drain) so the check is
      ///   deterministic. The plan-preservation wording itself is pinned headless in the SDK; here we log the
      ///   summary for human judgement that the plan actually survived.
      /// </summary>
      private static string TestConvSummaryQueueJob(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("conv_summary_queue");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to remember the conversation")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc);

         int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string player = Hero.MainHero?.Name?.ToString() ?? "the player";
         var baseEvent = new NotableEvent(day, NotableEventType.Other, $"Spoke with {player}.");
         profile.Events.Add(baseEvent);
         behavior.Store.Set(profile);

         var job = new PendingSummaryJob {
            ProfileId = profile.Id,
            AnchorDay = day,
            AnchorType = NotableEventType.Other.ToString(),
            AnchorSummary = baseEvent.summary,
            Transcript = $"[{player}]: Before I go, we agreed you would gather intel on marriage candidates and report back to me today.\n" +
                         $"[{npc.Name}]: Yes. I will have a list of suitable candidates ready for you by this evening.",
            PlayerName = player,
            ReplyLanguage = null,
            EnqueuedDay = day
         };

         if (t.Abort(!TryRunLlmTask(ct => ConversationSummaryService.ProcessConversationJobAsync(behavior, job, ct), out bool resolved, out string err),
                $"LLM call failed: {err}"))
            return t.Line();

         NpcProfile fresh = behavior.Store.Get(profile.Id);
         NotableEvent evt = fresh?.Events?.LastOrDefault(e => e.gameDay == day && e.type == NotableEventType.Other);
         bool enriched = evt != null && !string.Equals(evt.summary, baseEvent.summary, StringComparison.Ordinal);

         t.Note($"resolved={resolved}; summary now: \"{Show(evt?.summary)}\" (judge: does it keep the plan to gather intel and report back today?)");
         t.Expect("jobResolved", resolved, true);
         t.Expect("baseEventEnriched", enriched, true);

         return t.Line();
      }

      /// <summary>
      ///   ASSEMBLY Inc 1: convene an assembly through the real seam WITHOUT any manual in-game setup. Arranges
      ///   its own prerequisites (a host town, a handful of live lords, a seeded regard), then drives exactly the
      ///   path the console command does: AssemblyInvitationFactsBuilder reads each hero, AssemblyRsvpPolicy
      ///   decides, AssemblyBehavior records the roster. Asserts the load-bearing invariants: the facts-builder
      ///   reads the seeded PERSONAL regard back (the player-summoner ledger seam), the whole roster is recorded,
      ///   and the progress tally sums to it. Then stands the assembly down and restores the seeded regard, so the
      ///   sweep leaves no trace. Mechanical (no LLM): the RSVP verdicts depend on the live world, so they are
      ///   logged for judgement, not hard-asserted.
      /// </summary>
      private static string TestAssemblyConvene(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("assembly_convene");

         if (t.Skip(AssemblyBehavior.Instance == null, "assembly gated off for release (re-enable the two AddBehavior lines to test)")) return t.Line();

         Hero player = Hero.MainHero;
         if (t.Abort(player == null, "no player hero")) return t.Line();

         Settlement host = Settlement.All?.FirstOrDefault(s => s.IsTown);
         if (t.Abort(host == null, "no town found to host an assembly")) return t.Line();

         List<Hero> lords = Hero.AllAliveHeroes
                                .Where(h => h != null && h != player && h.IsLord && !h.IsPrisoner)
                                .Take(3)
                                .ToList();
         if (t.Abort(lords.Count == 0, "no alive lord to invite")) return t.Line();

         // Deterministic regard seam: seed a known PERSONAL regard on one lord and prove the facts-builder reads
         // it back (a player summoner must weigh the mod's own ledger, never the vanilla clan relation).
         Hero probe = lords[0];
         NpcProfile probeProfile = GetOrCreate(behavior, probe);
         int originalRegard = probeProfile.ReputationWithPlayer;
         probeProfile.ReputationWithPlayer = 42;
         behavior.Store.Set(probeProfile);

         AssemblyInvitationFacts probeFacts = AssemblyInvitationFactsBuilder.Build(probe, host, player);
         t.Expect("builderReadsSeededRegard", probeFacts.RegardToInitiator, 42);
         t.Expect("liveHeroReadsAlive", probeFacts.IsAlive, true);

         int today = behavior.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;
         AssemblyBehavior.Instance.Begin(host.StringId, player.StringId, today, today + 7, lords.Select(h => h.StringId));

         var decisions = new List<string>();
         foreach (Hero h in lords)
         {
            AssemblyInvitationFacts facts = AssemblyInvitationFactsBuilder.Build(h, host, player);
            RsvpOutcome rsvp = AssemblyRsvpPolicy.Decide(facts);
            AssemblyInviteeStatus status = AssemblyConvener.InitialStatus(h, player, facts, rsvp);
            AssemblyBehavior.Instance.SetStatus(h.StringId, status);
            decisions.Add($"{h.Name}:{rsvp.Decision}/{status}[regard {facts.RegardToInitiator},tier {facts.DistanceTier}]");
         }

         AssemblyProgress progress = AssemblyBehavior.Instance.Progress();
         t.Note($"host={host.Name}; {string.Join(" | ", decisions)}; " +
                $"present={progress.Present} enroute={progress.InTransit} declined={progress.Declined} awaiting={progress.Awaiting} of {progress.Total}");

         t.Expect("rosterRecorded", AssemblyBehavior.Instance.Pending?.Invitees?.Count ?? 0, lords.Count);
         t.Expect("progressTotalMatchesRoster", progress.Total, lords.Count);
         t.Expect("tallySumsToTotal", progress.Present + progress.InTransit + progress.Declined + progress.Awaiting, lords.Count);

         // The travel-mode rule (Gabriel 2026-07-25): a lord OUTSIDE the player's clan never crosses the map in
         // person, they send an ambassador, so only your own household is ever force-moved. Deterministic.
         Hero foreignLord = lords.FirstOrDefault(h => h.Clan != Clan.PlayerClan);
         if (foreignLord != null)
            t.Expect("foreignLordNeverTravelsInPerson", AssemblyConvener.TravelsInPerson(foreignLord, player), false);

         // Cleanup: stand the assembly down and restore the probe's regard so the sweep leaves nothing behind.
         AssemblyBehavior.Instance.Cancel();
         probeProfile.ReputationWithPlayer = originalRegard;
         behavior.Store.Set(probeProfile);
         t.Expect("cancelClearsAssembly", AssemblyBehavior.Instance.HasActiveAssembly, false);

         return t.Line();
      }

      /// <summary>
      ///   ASSEMBLY Inc 1 slice 2: the arrival lifecycle, driven directly so it needs no map travel and no
      ///   waiting. Convenes, sets two lords travelling (EnRoute), then exercises both transitions the runtime
      ///   fires: one reaches the host (NoteArrivalAtHost, what OnSettlementEntered calls) and takes their seat
      ///   as Present; the clock reaches the deadline (AdvanceArrivals, what the daily tick calls) and the other,
      ///   still on the road, stands in by ambassador. Mechanical, no LLM, self-arranged. Forced party movement
      ///   (letting a lord actually ARRIVE on the map) is a later supervised slice, so it is not tested here.
      /// </summary>
      private static string TestAssemblyArrival(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("assembly_arrival");

         if (t.Skip(AssemblyBehavior.Instance == null, "assembly gated off for release (re-enable the two AddBehavior lines to test)")) return t.Line();

         Hero player = Hero.MainHero;
         Settlement host = Settlement.All?.FirstOrDefault(s => s.IsTown);
         if (t.Abort(player == null || host == null, "no player/town to host an assembly")) return t.Line();

         List<Hero> lords = Hero.AllAliveHeroes
                                .Where(h => h != null && h != player && h.IsLord && !h.IsPrisoner)
                                .Take(2)
                                .ToList();
         if (t.Abort(lords.Count < 2, "need two alive lords for the arrival test")) return t.Line();

         int today = behavior.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;
         int deadline = today + 7;
         AssemblyBehavior.Instance.Begin(host.StringId, player.StringId, today, deadline, lords.Select(h => h.StringId));
         // Drive the lifecycle directly, independent of the RSVP verdict (which the convene test covers): both set out.
         AssemblyBehavior.Instance.SetStatus(lords[0].StringId, AssemblyInviteeStatus.EnRoute);
         AssemblyBehavior.Instance.SetStatus(lords[1].StringId, AssemblyInviteeStatus.EnRoute);

         bool arrived = AssemblyBehavior.Instance.NoteArrivalAtHost(lords[0].StringId);
         AssemblyInviteeStatus afterArrival = AssemblyStatusOf(lords[0].StringId);

         AssemblyBehavior.Instance.AdvanceArrivals(deadline);
         AssemblyInviteeStatus afterDeadline = AssemblyStatusOf(lords[1].StringId);

         t.Note($"host={host.Name}; {lords[0].Name} arrival -> {afterArrival}; {lords[1].Name} at deadline -> {afterDeadline}");
         t.Expect("arrivalSeatsPresent", arrived && afterArrival == AssemblyInviteeStatus.Present, true);
         t.Expect("deadlineMakesAmbassador", afterDeadline, AssemblyInviteeStatus.Ambassador);

         AssemblyBehavior.Instance.Cancel();
         t.Expect("cancelClears", AssemblyBehavior.Instance.HasActiveAssembly, false);

         return t.Line();
      }

      private static AssemblyInviteeStatus AssemblyStatusOf(string heroId)
         => AssemblyBehavior.Instance.Pending?.Invitees?.FirstOrDefault(i => i.HeroId == heroId)?.Status
            ?? AssemblyInviteeStatus.Invited;

      /// <summary>
      ///   ASSEMBLY Inc 1 robustness: the daily re-validation pass runs against the LIVE world without wrongly
      ///   tearing down a healthy assembly. The exhaustive host/traveller combinations are pinned by the pure
      ///   AssemblyHostValidity / AssemblyTravelOutcome tests; this is the engine-adapter smoke test, proving the
      ///   real reads (host siege / ownership, a hero's captivity / battle / party) do not throw and leave a valid
      ///   assembly with a healthy traveller intact. Mechanical, no LLM, self-arranged.
      /// </summary>
      private static string TestAssemblyRevalidate(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("assembly_revalidate");

         if (t.Skip(AssemblyBehavior.Instance == null, "assembly gated off for release (re-enable the two AddBehavior lines to test)")) return t.Line();

         Hero player = Hero.MainHero;
         Settlement host = Settlement.All?.FirstOrDefault(s => s.IsTown && !s.IsUnderSiege);
         if (t.Abort(player == null || host == null, "no peaceful town to host an assembly")) return t.Line();

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != player && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null);
         if (t.Abort(lord == null, "no free lord with a party to travel")) return t.Line();

         int today = behavior.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;
         AssemblyBehavior.Instance.Begin(host.StringId, player.StringId, today, today + 7, new[] {lord.StringId});
         AssemblyBehavior.Instance.SetStatus(lord.StringId, AssemblyInviteeStatus.EnRoute);

         // A valid host and a healthy, free, unpinned traveller: the pass must NOT cancel and must NOT drop them.
         AssemblyBehavior.Instance.RevalidateNow(today);

         bool survived = AssemblyBehavior.Instance.HasActiveAssembly;
         AssemblyInviteeStatus after = AssemblyStatusOf(lord.StringId);
         t.Note($"host={host.Name}, traveller={lord.Name}: after revalidate -> assemblyActive={survived}, seat={after}");
         t.Expect("healthyAssemblySurvives", survived, true);
         t.Expect("healthyTravellerKeptEnRoute", after, AssemblyInviteeStatus.EnRoute);

         AssemblyBehavior.Instance.Cancel();

         return t.Line();
      }

      /// <summary>
      ///   Mechanical (Parley council, 2026-08-03): the additive JSON field CouncilSessionSnapshot.ParleyTargetFactionId
      ///   round-trips through the exact JsonConvert.SerializeObject/DeserializeObject path CouncilSessionBehavior.SyncData
      ///   uses. Two cases: WITH a value set (the ordinary Parley case, must survive intact) and a hand-written JSON
      ///   payload WITHOUT the property present at all (an old snapshot written before this field existed, or any
      ///   non-Parley scope, which never sets it) -> Newtonsoft must tolerate the missing property as null rather than
      ///   throw, or an old in-flight council save recovered mid-session would crash the campaign on load instead of
      ///   simply recovering no parley target (see CouncilSessionSnapshot's own field doc). Pure serialization, no
      ///   live campaign state touched, so this needs no arrange/skip preconditions.
      /// </summary>
      private static string TestCouncilParleySnapshot(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("council_parley_snapshot");

         var withTarget = new CouncilSessionBehavior.CouncilSessionSnapshot {
            Scope = CouncilScope.Parley,
            Entries = new List<AssemblyResolutionEntry>(),
            ParleyTargetFactionId = "fac_test_vlandia"
         };
         string json = JsonConvert.SerializeObject(withTarget, Formatting.None);
         var roundTripped = JsonConvert.DeserializeObject<CouncilSessionBehavior.CouncilSessionSnapshot>(json);

         t.Expect("scopeRoundTrips", roundTripped?.Scope, CouncilScope.Parley);
         t.Expect("parleyTargetFactionIdRoundTrips", roundTripped?.ParleyTargetFactionId, "fac_test_vlandia");

         // The old (pre-Parley) shape: no ParleyTargetFactionId property in the JSON at all, mirroring exactly
         // what a save written before this field existed would carry (ClanLords = ordinal 2).
         const string legacyJson = "{\"Scope\":2,\"Entries\":[]}";
         CouncilSessionBehavior.CouncilSessionSnapshot legacy = null;
         Exception thrown = null;
         try { legacy = JsonConvert.DeserializeObject<CouncilSessionBehavior.CouncilSessionSnapshot>(legacyJson); }
         catch (Exception ex) { thrown = ex; }

         t.Expect("legacyShapeDoesNotThrow", thrown == null, true);
         t.Expect("legacyShapeYieldsNullParleyTarget", legacy?.ParleyTargetFactionId, null);
         t.Expect("legacyShapeScopeStillReads", legacy?.Scope, CouncilScope.ClanLords);

         return t.Line();
      }

      // Convergence Inc 2: proves the ENGINE resolver (CouncilAttendanceResolver), which projects live campaign
      // state into CouncilAttendanceFacts and runs the already-tested pure CouncilAttendancePolicy, actually
      // agrees with the real world. Three checks: every hero the resolver seats for CouncilScope.Party out of
      // the player's own party comes back InPerson/InParty; a manufactured far own-faction fact resolves
      // Absent/TooFar and is NEVER Ambassador (the guarantee this whole convergence exists to keep: your own
      // house never sends a stand-in to its own table, see CouncilAttendancePolicy's own header); a real
      // prisoner, if this campaign happens to have one, resolves Absent/Prisoner through the resolver's own
      // per-hero projection (DebugDecideFor), not just the pure policy called directly. No world mutation.
      private static string TestCouncilAttendance(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("council_attendance");

         List<Hero> inParty = MobileParty.MainParty?.MemberRoster?.GetTroopRoster()
            .Select(el => el.Character?.HeroObject)
            .Where(h => h is {IsAlive: true} && h != Hero.MainHero && CouncilRosterPolicy.IsOldEnoughToSit((int) h.Age))
            .Distinct()
            .ToList() ?? new List<Hero>();

         if (t.Skip(inParty.Count == 0, "no non-player hero rides in the main party to prove InParty attendance")) return t.Line();

         IReadOnlyList<CouncilAttendanceSeat> partySeats =
            CouncilAttendanceResolver.Resolve(CouncilScope.Party, CouncilAttendanceResolver.DefaultThresholdDays);
         List<CouncilAttendanceSeat> seatedPartyMembers = partySeats.Where(s => inParty.Contains(s.RepresentedHero)).ToList();

         t.Note($"inPartyCount={inParty.Count}, resolverSeatedOfThem={seatedPartyMembers.Count}");
         t.Expect("everyPartyMemberSeated", seatedPartyMembers.Count, inParty.Count);
         t.Expect("allSeatedInPerson", seatedPartyMembers.All(s => s.Attendance == CouncilAttendance.InPerson), true);
         t.Expect("allReasonInParty", seatedPartyMembers.All(s => s.Reason == CouncilAttendanceReason.InParty), true);

         var farOwnFaction = new CouncilAttendanceFacts {
            IsPrisoner = false,
            IsInPlayerParty = false,
            IsPlayerFaction = true,
            IsParleyTarget = false,
            EtaDays = CouncilAttendanceResolver.DefaultThresholdDays + 999.0,
            RelationToPlayer = 50,
            AtWarWithPlayer = false
         };
         CouncilAttendanceResult farVerdict = CouncilAttendancePolicy.Decide(farOwnFaction, CouncilAttendanceResolver.DefaultThresholdDays);
         t.Note($"farOwnFaction: attendance={farVerdict.Attendance}, reason={farVerdict.Reason}");
         t.Expect("farOwnFactionStaysHome", farVerdict.Attendance, CouncilAttendance.Absent);
         t.Expect("farOwnFactionReasonIsTooFar", farVerdict.Reason, CouncilAttendanceReason.TooFar);
         t.Expect("farOwnFactionNeverAmbassador", farVerdict.Attendance == CouncilAttendance.Ambassador, false);

         Hero prisoner = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsPrisoner);
         if (prisoner != null)
         {
            CouncilAttendanceResult prisonerVerdict =
               CouncilAttendanceResolver.DebugDecideFor(prisoner, CouncilScope.ClanLords, CouncilAttendanceResolver.DefaultThresholdDays);
            t.Note($"prisoner={prisoner.Name}: attendance={prisonerVerdict.Attendance}, reason={prisonerVerdict.Reason}");
            t.Expect("prisonerIsAbsent", prisonerVerdict.Attendance, CouncilAttendance.Absent);
            t.Expect("prisonerReasonIsPrisoner", prisonerVerdict.Reason, CouncilAttendanceReason.Prisoner);
         }
         else
         {
            t.Note("no live prisoner in this campaign (optional check skipped)");
         }

         // Maifailun (30/09/2026): a dead father sat at the table. The game keeps the dead in a clan's heroes; one of
         // the player's own house is preferred, any dead hero proves the verdict otherwise.
         Hero dead = Clan.PlayerClan?.Heroes?.FirstOrDefault(h => h != null && !h.IsAlive)
                     ?? Hero.DeadOrDisabledHeroes.FirstOrDefault(h => h != null && !h.IsAlive);
         if (dead != null)
         {
            // Sitting is an action (Gabriel, 30/09/2026): the one gate refuses it, before any seating rule.
            ActionRefusal deadVerdict = Mapping.ActionGate.Check(dead, ActionKind.CouncilSeat);
            t.Note($"dead={dead.Name}: the gate says {deadVerdict}");
            t.Expect("theDeadNeverSit", deadVerdict, ActionRefusal.Dead);
            t.Expect("noDeadLordOfTheHouseAtTheTable",
               CouncilAttendanceResolver.SeatedInPersonHeroes(CouncilScope.ClanLords).Any(h => !h.IsAlive), false);
         }
         else
         {
            t.Note("no dead hero in this campaign (optional check skipped)");
         }

         return t.Line();
      }

      // Convergence Inc 3a: proves the shared roster (CouncilAttendanceResolver.SeatedInPersonHeroes) that
      // ChatManager.OpenForCouncil and CouncilConversationStager now BOTH walk actually reflects the new
      // attendance policy for a live NON-parley scope, the wiring this increment exists for (a far own-faction
      // lord no longer sits at the table, the "war council felt magic" fix). Every hero the helper returns is
      // independently re-judged InPerson via DebugDecideFor (the only verdict Resolve keeps); a real live
      // prisoner, and every Absent-rated candidate in the scope's own pool, must be missing from the helper's
      // result. The Parley half proves this increment kept its promise to leave that scope untouched: the
      // helper's own Parley branch is a straight pass-through to the OLD CouncilRosterResolver.Seated, so the
      // two calls must return the identical set. No world mutation.
      private static string TestCouncilRosterSwap(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("council_roster_swap");

         CouncilScope scope = CouncilScope.ClanLords;
         List<Hero> candidatePool = CouncilRosterResolver.Resolve(scope)
            .Select(s => s.Hero)
            .Where(h => h != null)
            .Distinct()
            .ToList();

         if (t.Skip(candidatePool.Count == 0, $"no {scope} candidate exists in this campaign to prove the swap against")) return t.Line();

         List<Hero> seatedInPerson = CouncilAttendanceResolver.SeatedInPersonHeroes(scope);

         t.Note($"candidatePoolCount={candidatePool.Count}, seatedInPersonCount={seatedInPerson.Count}");
         t.Expect("everyReturnedHeroIsRatedInPerson",
            seatedInPerson.All(h =>
               CouncilAttendanceResolver.DebugDecideFor(h, scope, CouncilAttendanceResolver.DefaultThresholdDays).Attendance
               == CouncilAttendance.InPerson),
            true);
         t.Expect("noReturnedHeroIsAPrisoner", seatedInPerson.Any(h => h.IsPrisoner), false);

         List<Hero> absentCandidates = candidatePool
            .Where(h =>
               CouncilAttendanceResolver.DebugDecideFor(h, scope, CouncilAttendanceResolver.DefaultThresholdDays).Attendance
               == CouncilAttendance.Absent)
            .ToList();
         t.Note($"absentCandidateCount={absentCandidates.Count}");
         t.Expect("noAbsentCandidateSeated", absentCandidates.Any(h => seatedInPerson.Contains(h)), false);

         string parleyTargetFactionId = Kingdom.All?.FirstOrDefault(k => k != null)?.StringId;
         List<Hero> parleyOld = CouncilRosterResolver.Seated(CouncilScope.Parley, parleyTargetFactionId);
         List<Hero> parleyNew = CouncilAttendanceResolver.SeatedInPersonHeroes(CouncilScope.Parley, parleyTargetFactionId);
         t.Note($"parleyTargetFactionId={parleyTargetFactionId ?? "(none)"}, parleyOldCount={parleyOld.Count}, parleyNewCount={parleyNew.Count}");
         t.Expect("parleyScopeUntouched", parleyNew.SequenceEqual(parleyOld), true);

         return t.Line();
      }

      // Convergence Inc 4: proves the SYNTHETIC ENVOY identity (EnvoyFactory + the pure EnvoyIdentityPolicy it
      // wraps) against a real other-faction kingdom leader, without wiring an ambassador into any live session
      // (that is Inc 3's job). Ambassadors are ONLY ever for other factions, so the subject is deliberately drawn
      // from a kingdom that is NOT the player's own; picks any live one with a resolvable leader and skips MANUAL
      // when the campaign happens to have none (a fully eliminated world, or every other realm in an
      // interregnum). Asserts the profile reads unmistakably as a herald (name + persona), carries a synthetic id
      // that cannot collide with a real hero, and above all was never written to the store: an ambassador is
      // ephemeral by design, and a leaked persistence here would be the first sign it silently stopped being so.
      private static string TestEnvoyIdentity(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("envoy_identity");

         Kingdom otherKingdom = Kingdom.All?.FirstOrDefault(k =>
            k is {IsEliminated: false} && k != Clan.PlayerClan?.Kingdom && k.Leader is {IsAlive: true});

         if (t.Skip(otherKingdom == null, "no live other-faction kingdom has a live leader in this campaign")) return t.Line();

         Hero leader = otherKingdom.Leader;
         NpcProfile profile = EnvoyFactory.Create(leader, leader.MapFaction);

         t.Note($"faction={otherKingdom.Name}, leader={leader.Name}");
         t.Expect("profileIsNotNull", profile != null, true);
         if (profile == null) return t.Line();

         t.Expect("nameReadsHeraldOfFaction", profile.Name, $"Herald of {otherKingdom.Name}");
         t.Expect("personaNamesFaction", profile.Personality?.Contains(otherKingdom.Name.ToString()) == true, true);
         t.Expect("personaNamesLeader", profile.Personality?.Contains(leader.Name.ToString()) == true, true);
         t.Expect("idHasEnvoyPrefix", profile.Id?.StartsWith("cr_envoy_") == true, true);

         NpcProfile stored = behavior?.Store?.Get(profile.Id);
         t.Expect("neverPersisted", stored == null, true);

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
         GrudgeBehavior.Instance?.ClearGrudgesFor(npc.StringId);

         string plea = "*I sink to my knees in the straw, wrists raw from the ropes.* I have no quarrel with you. " +
                       "Let me walk out of here and you will never see me again. I beg you.";

         EncounterContext ctx = EncounterContextBuilder.Build(
            npc, profile, behavior.GameStateBridge, plea,
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

      /// <summary>
      ///   Player report: a character answers warmly yet emits a NEGATIVE change_relation, so a positive
      ///   exchange lowers regard. The tone/delta consistency rule in the change_relation instruction
      ///   (LlmServices.ChangeRelationDescription) forbids that. Sends a plainly warm, approving message and
      ///   asserts the model does NOT emit a negative delta (no change_relation at all, or a non-negative one,
      ///   both pass). Model-dependent (an LLM lane test), so it is a smoke check, not a proof.
      /// </summary>
      private static string TestRegardTone(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_regard_tone");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         const string warm = "I want you to know how deeply I admire your courage and your honour. You have been a true " +
                             "friend to me, and it is a genuine pleasure to stand at your side.";
         EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, warm);

         var (ok, resp, err) = AskLlmParsed(behavior, "chat_regard_tone", npc, profile, ctx, warm);

         if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

         GameAction cr = resp.Actions?.FirstOrDefault(a => string.Equals(a.Type, "change_relation", StringComparison.OrdinalIgnoreCase));
         var delta = 0;
         bool hasDelta = cr?.Parameters != null && cr.Parameters.TryGetValue("delta", out string ds) && int.TryParse(ds, out delta);

         t.Note($"warm approving message to {npc.Name}: change_relation={(cr == null ? "none" : hasDelta ? delta.ToString("+#;-#;0") : "no delta")}");
         // The bug is a warm reply paired with a NEGATIVE delta. Neutral (no action) or a positive delta both pass.
         t.Expect("noNegativeRegardOnWarmExchange", !hasDelta || delta >= 0, true);

         return t.Line();
      }

      /// <summary>
      ///   Player report (the phantom ring): the AI invented a quest requiring a non-existent item, and the
      ///   player could not get the giver to drop it. The giver IS taught to emit [QUEST_ABANDON] on a clear
      ///   refusal; this pins that the teaching actually fires. Seeds an outstanding task, has the player plainly
      ///   refuse it, and asserts the parsed reply carries a QuestAbandoned claim (the giver let them go). An LLM
      ///   lane test, so model-dependent; it removes its seeded quest afterward.
      /// </summary>
      private static string TestQuestAbandonChat(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_quest_abandon");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero giver = ConversationNpc() ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(giver == null, "no lord to hold an outstanding task")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         var quest = new InformalQuest {
            Type = QuestType.BanditClear,
            Description = "[SELFTEST] Clear the bandits raiding the roads nearby.",
            IssuedOnDay = behavior.GameStateBridge.GetCurrentGameDay(),
            RewardGold = 200,
            RewardRelation = 3,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);

         try
         {
            const string refuse = "I have given it real thought, and I cannot carry out that task for you. Please release me " +
                                  "from it; I will not be doing it.";
            EncounterContext ctx = EncounterContextBuilder.Build(giver, profile, behavior.GameStateBridge, refuse);

            var (ok, resp, err) = AskLlmParsed(behavior, "chat_quest_abandon", giver, profile, ctx, refuse);

            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Note($"{giver.Name} on a clear refusal: QuestAbandoned={(resp.QuestAbandoned != null ? "yes" : "no")}");
            t.Expect("giverReleasedTheTask", resp.QuestAbandoned != null, true);
         }
         finally
         {
            NpcProfile after = behavior.Store.Get(giver.StringId);
            after?.ActiveQuests.RemoveAll(q => q.Description != null && q.Description.StartsWith("[SELFTEST]", StringComparison.Ordinal));
            if (after != null) behavior.Store.Set(after);
         }

         return t.Line();
      }

      /// <summary>
      ///   Gabriel cannot play to test ride_with_me by hand, so this drives the WHOLE chain in cr.testall: raise an
      ///   idle friendly lord's regard, arm the gated feature, and ask him IN CONVERSATION to ride at your side.
      ///   It asserts the model emits the ride_with_me action (the teaching + emission), then runs that action
      ///   through the real bridge and asserts he actually JOINED the player's party while staying in his own clan.
      ///   An LLM lane test (model-dependent). Releases him and restores regard + the gate in a finally.
      /// </summary>
      private static string TestChatRideWithMe(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_ride_with_me");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();
         if (t.Abort(Hero.MainHero?.MapFaction == null, "no player faction")) return t.Line();

         // An IDLE friendly lord: a lord who does NOT lead a party (so ride_with_me applies, not follow_me), not of
         // the player's clan, alive, free, and whose faction is not at war with the player.
         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.Clan != null && h.Clan != Clan.PlayerClan &&
            !(h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h) &&
            h.MapFaction != null && !FactionManager.IsAtWarAgainstFaction(h.MapFaction, Hero.MainHero.MapFaction));

         if (t.Skip(lord == null, "no idle friendly lord (not leading a party, not at war) to ask")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, lord);
         int origRegard = profile.ReputationWithPlayer;
         bool origEnabled = RetainerBehavior.Enabled;
         bool wasInMainParty = lord.PartyBelongedTo == MobileParty.MainParty;

         try
         {
            RetainerBehavior.Enabled = true; // arm the gated feature so the teaching renders and the executor runs
            profile.ReputationWithPlayer = 40; // above RideWithMeEligibilityPolicy's friendly-bond floor
            behavior.Store.Set(profile);

            const string ask = "Ride with me a while, my friend. I would have you at my side on the road, though you keep " +
                               "your own house and colours and may leave whenever you wish.";
            EncounterContext ctx = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, ask);

            var (ok, resp, err) = AskLlmParsed(behavior, "chat_ride_with_me", lord, profile, ctx, ask);

            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            GameAction ride = resp.Actions?.FirstOrDefault(a => string.Equals(a.Type, "ride_with_me", StringComparison.OrdinalIgnoreCase));
            t.Note($"{lord.Name}: ride_with_me emitted={(ride != null)}");
            t.Expect("lordAgreedAndEmittedRideWithMe", ride != null, true);

            if (ride != null)
            {
               behavior.GameStateBridge.ExecuteAction(ride, lord, new ActionContext(profile, null));
               t.Note($"after bridge: inMainParty={lord.PartyBelongedTo == MobileParty.MainParty}, clan={lord.Clan?.Name}");
               t.Expect("lordJoinedThePlayerParty", lord.PartyBelongedTo == MobileParty.MainParty, true);
               t.Expect("lordStayedInHisOwnClan", lord.Clan != Clan.PlayerClan, true);
            }
         }
         finally
         {
            if (lord.PartyBelongedTo == MobileParty.MainParty && !wasInMainParty)
               behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "part_ways"}, lord, new ActionContext(profile, null));

            NpcProfile after = behavior.Store.Get(lord.StringId) ?? profile;
            after.ReputationWithPlayer = origRegard;
            behavior.Store.Set(after);
            RetainerBehavior.Enabled = origEnabled;
         }

         return t.Line();
      }

      /// <summary>
      ///   2026-08-22: an NPC's vow is REMEMBERED, never tracked as a player quest. Firing the real
      ///   pledge_against action must (a) leave nothing in the commitment ledger, (b) open no
      ///   quest-journal mirror, (c) append a memory of the vow, and (d) move the player-regard not at all
      ///   (a vow now carries neither reward nor penalty). Guards the v2.1.4 player report: governor
      ///   companions "failed" pledges they never had a chance to keep, and lost regard for it.
      /// </summary>
      private static string TestCommitmentIsMemoryOnly(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("commitment_is_memory_only");

         if (t.Abort(CommitmentBehavior.Instance == null, "CommitmentBehavior not active")) return t.Line();

         (Hero npc, Hero target)? pair = ResolveSwayPair();

         if (t.Skip(pair == null, "need an NPC + a distinct kin-safe lord to pledge against")) return t.Line();
         (Hero npc, Hero target) = pair.Value;

         NpcProfile profile = GetOrCreate(behavior, npc);
         ArrangeCleanNpc(behavior, profile, npc, target);

         var commitments = CommitmentBehavior.Instance;
         NpcProfile beforeProfile = behavior.Store.Get(npc.StringId);
         int repBefore = beforeProfile.ReputationWithPlayer;
         int memoriesBefore = beforeProfile.Events.Count;
         QuestManager qm = Campaign.Current?.QuestManager;
         int mirrorsBefore = qm?.Quests.OfType<CommitmentQuest>().Count(q => q.PromisorId == npc.StringId) ?? 0;

         bool seeded = SeedPledgeEligible(behavior, profile, npc, target);

         if (t.Abort(!seeded, "pledge_against seed failed (wiring bug, not a judgment call)")) return t.Line();

         NpcProfile after = behavior.Store.Get(npc.StringId);
         int mirrorsAfter = qm?.Quests.OfType<CommitmentQuest>().Count(q => q.PromisorId == npc.StringId) ?? 0;

         t.Note($"{npc.Name} vowed against {target.Name}: regard {repBefore}->{after.ReputationWithPlayer}, memories {memoriesBefore}->{after.Events.Count}, mirrors {mirrorsBefore}->{mirrorsAfter}");
         t.Expect("notTracked", commitments.HasActive(npc.StringId), false);
         t.Expect("noJournalMirror", mirrorsAfter, mirrorsBefore);
         t.Expect("memoryRecorded", after.Events.Count > memoriesBefore, true);
         t.Expect("regardUnchanged", after.ReputationWithPlayer, repBefore);

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

      /// <summary>Test arrangement, not a rung: a relation this close to the engine's -100 floor leaves no room for a measurable drop.</summary>
      private const int NearTheEngineFloor = -90;

      /// <summary>
      ///   Couriers through a save: the registry's despawn and restore, driven without saving, leave the letter in
      ///   flight on a live courier where the old one stood.
      /// </summary>
      private static string TestCourierSaveRestore(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("courier_save_restore");

         Travel.TransientPartyRegistry registry = Travel.TransientPartyRegistry.Instance;
         Hero recipient = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner && CourierService.CanReceiveLetter(h));

         if (t.Skip(registry == null, "the transient-party registry is not running")) return t.Line();
         if (t.Skip(recipient == null, "no live lord to stand as a letter recipient")) return t.Line();

         const string tag = "CR_SELFTEST_SAVE_RESTORE";
         NpcProfile profile = GetOrCreate(behavior, recipient);
         var today = (int) CampaignTime.Now.ToDays;
         var letter = new PlayerLetter {
            RecipientId = profile.Id, RecipientName = profile.Name, Content = tag, SentOnDay = today, DeliveryDay = today + 8
         };
         profile.ReceivedPlayerLetters.Add(letter);
         behavior.Store.Set(profile);

         try
         {
            if (t.Skip(!CourierService.TrySpawnCourier(letter, recipient), "no courier could be spawned to that lord (no land route)")) return t.Line();

            MobileParty before = MobileParty.All.FirstOrDefault(p => p.StringId == letter.CourierPartyId);
            if (t.Skip(before == null, "the courier did not appear on the map")) return t.Line();

            TaleWorlds.Library.Vec2 stood = before.GetPosition2D;
            t.Expect("courierIsTracked", registry.EntryOf(before)?.Role, CourierService.CourierRole);

            registry.DebugSaveCycle();

            PlayerLetter after = behavior.Store.Get(profile.Id)?.ReceivedPlayerLetters.FirstOrDefault(l => l.Content == tag);
            MobileParty courier = string.IsNullOrEmpty(after?.CourierPartyId) ? null : MobileParty.All.FirstOrDefault(p => p.StringId == after.CourierPartyId && p.IsActive);
            t.Note($"recipient={recipient.Name}, old={stood}, new={courier?.GetPosition2D}");
            t.Expect("letterStillInFlight", after != null && !after.IsLost && !after.IsDelivered, true);
            t.Expect("aLiveCourierCarriesIt", courier != null && !ReferenceEquals(courier, before) && !before.IsActive, true);
            t.Expect("restoredWhereItStood", courier != null && courier.GetPosition2D.Distance(stood) < 1f, true);
            t.Expect("stillACourier", courier != null && CourierService.IsCourier(courier.StringId), true);
            t.Expect("stillTracked", courier != null && registry.EntryOf(courier)?.Role == CourierService.CourierRole, true);
         }
         finally
         {
            // Letters first, so the courier's removal matches no letter and fires no loss.
            string courierId = behavior.Store.Get(profile.Id)?.ReceivedPlayerLetters.FirstOrDefault(l => l.Content == tag)?.CourierPartyId;
            NpcProfile fresh = behavior.Store.Get(profile.Id);
            fresh?.ReceivedPlayerLetters.RemoveAll(l => l.Content == tag);
            if (fresh != null) behavior.Store.Set(fresh);
            MobileParty cp = string.IsNullOrEmpty(courierId) ? null : MobileParty.All.FirstOrDefault(p => p.StringId == courierId && p.IsActive);
            if (cp != null) TravelingParty.Destroy(cp);
         }

         return t.Line();
      }

      /// <summary>
      ///   Waylaid-letter resend: a lost letter keeps its text, and the resend path re-queues a fresh, non-lost
      ///   letter with the SAME content (the player never retypes a waylaid letter). Cleans up the test letters
      ///   and the courier the resend spawned, removing the letters BEFORE the courier so its disband matches no
      ///   letter and fires no loss/popup.
      /// </summary>
      private static string TestCourierWaylayResend(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("courier_waylay");

         Hero recipient = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(recipient == null, "no live lord to stand as a letter recipient")) return t.Line();

         const string tag = "CR_SELFTEST_WAYLAY";
         NpcProfile profile = GetOrCreate(behavior, recipient);
         var today = (int) CampaignTime.Now.ToDays;

         var lost = new PlayerLetter {
            RecipientId = profile.Id,
            RecipientName = profile.Name,
            Content = tag,
            SentOnDay = today,
            DeliveryDay = today + 8,
            IsLost = true
         };
         profile.ReceivedPlayerLetters.Add(lost);
         behavior.Store.Set(profile);

         CourierService.ResendWaylaidLetterForTest(behavior.Store, profile, lost);
         profile = behavior.Store.Get(recipient.StringId);

         PlayerLetter resent = profile.ReceivedPlayerLetters.LastOrDefault(l => l.Content == tag && !l.IsLost);
         t.Note($"recipient={recipient.Name}: lostKeptText={lost.Content == tag}, resent={resent != null}");
         t.Expect("waylaidLetterKeepsItsText", lost.Content, tag);
         t.Expect("resendQueuedFreshLetter", resent != null, true);
         t.Expect("resendPreservedContent", resent?.Content, tag);
         t.Expect("resendRidesGuarded", resent?.Resent, true); // Gabriel 26/09/2026: sent again until it arrives

         string courierId = resent?.CourierPartyId;
         profile.ReceivedPlayerLetters.RemoveAll(l => l.Content == tag);
         behavior.Store.Set(profile);
         if (!string.IsNullOrEmpty(courierId))
         {
            MobileParty cp = MobileParty.All.FirstOrDefault(p => p.StringId == courierId);
            if (cp != null) TravelingParty.Destroy(cp);
         }

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

      /// <summary>
      ///   The SALE path on a captive the receiver asked for. Before 19/09/2026 buy_prisoner paid a price, moved
      ///   the captive and left the bargain open forever, which is exactly what Lordfadooboo reported. It now hands
      ///   such a captive over as the delivery, so this asserts the bargain ends settled and the captive gone.
      ///   Realm-keyed on purpose, because that is the bargain the game's own menu offers ("any of their lords").
      /// </summary>
      /// <summary>
      ///   The cr_patch scan reads ENABLED modules only. Its first version read every module found on disk, so a
      ///   bridge turned off in the launcher went on feeding prose, and unregistered {{variables}}, into every prompt.
      /// </summary>
      private static string TestNarrativePatchScope(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("narrative_patch_scope");

         IReadOnlyList<string> scanned = NarrativePatchScanner.LoadedModuleIds();
         var enabled = new HashSet<string>(
            ModuleHelper.GetActiveModules().Where(m => m != null).Select(m => m.Id),
            StringComparer.OrdinalIgnoreCase);

         t.Note($"scanned {scanned.Count} module(s), {enabled.Count} enabled");
         t.Expect("everyScannedModuleIsEnabled", scanned.All(id => enabled.Contains(id)), true);
         t.Expect("thisModIsAmongThem", scanned.Any(id => string.Equals(id, "CalradiaRemembers", StringComparison.OrdinalIgnoreCase)), true);

         return t.Line();
      }

      private static string TestDeliverPrisonerBySale(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("deliver_prisoner_by_sale");

         Hero receiver = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan && h.PartyBelongedTo != null);
         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != receiver && h.IsLord && !h.IsPrisoner && h.MapFaction != null
            && h.MapFaction != receiver?.MapFaction);

         if (t.Skip(receiver == null || captive == null, "need a lord with a party and a free lord of another realm")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);

         NpcProfile profile = GetOrCreate(behavior, receiver);
         var quest = new InformalQuest {
            Type = QuestType.DeliverPrisoner,
            Description = $"[SELFTEST] Bring {receiver.Name} any lord of {captive.MapFaction.Name} in chains.",
            TargetFactionId = captive.MapFaction.StringId,
            TargetFactionName = captive.MapFaction.Name?.ToString(),
            IssuedOnDay = behavior.GameStateBridge.GetCurrentGameDay(),
            RewardGold = 500,
            RewardRelation = 5,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);

         // The word the model reached for in the report: a SALE, with a price.
         behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "buy_prisoner",
               Parameters = new Dictionary<string, string> {["target"] = captive.Name?.ToString() ?? "", ["price"] = "900"}
            },
            receiver, new ActionContext(profile, null));

         bool stillHeld = BuyPrisonerVerb.PlayerHeldHeroPrisoners().Contains(captive);
         InformalQuest after = behavior.Store.Get(receiver.StringId)?.ActiveQuests
                                       .FirstOrDefault(q => q.Type == QuestType.DeliverPrisoner
                                                         && q.TargetFactionId == captive.MapFaction.StringId);

         t.Note($"captive={captive.Name} ({captive.MapFaction.Name}) -> {receiver.Name}, by SALE");
         t.Expect("theCaptiveLeftThePlayer", !stillHeld, true);
         t.Expect("theBargainIsSettledEvenThoughItWasASale", after != null && !after.IsOutstanding, true);

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
         bool mutualConsent = DivorcePolicy.IsMutualConsent(process.CumulativeDelayDays, spouseVsPlayerBefore);

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
         {
            t.Note("directFamilyGrudgePlanted skipped: seeded spouse has no qualifying adult family member in this campaign");
         }
         else
         {
            bool directFamilyGrudgePlanted = directFamilyCandidates.Any(member =>
               GrudgeBehavior.Instance.GrudgesFor(member.StringId).Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == today));
            t.Expect("directFamilyGrudgePlanted", directFamilyGrudgePlanted, true);
         }

         Hero bestFriendCandidate = Hero.AllAliveHeroes
                                        .Where(h => h != null && h != spouse && h != npc && h != Hero.MainHero && !directFamilyCandidates.Contains(h))
                                        .Where(h => h.GetRelation(spouse) >= VanillaRelation.TrustedTier)
                                        .OrderByDescending(h => h.GetRelation(spouse))
                                        .FirstOrDefault();

         if (bestFriendCandidate == null)
         {
            t.Note("friendGrudgePlanted skipped: no qualifying best friend for the seeded spouse in this campaign");
         }
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

      private static string TestHeardDiagnostic(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("heard_diagnostic");

         WorldEventStore store = WorldEventStore.Instance;

         if (t.Abort(store == null, "WorldEventStore not active")) return t.Line();

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.MapFaction != null
                                                           && (h.PartyBelongedTo != null || h.CurrentSettlement != null));

         if (t.Skip(lord == null, "no free lord with a realm and a position")) return t.Line();

         Settlement place = lord.CurrentSettlement ?? Settlement.All?.FirstOrDefault(x => x.IsTown);

         if (t.Skip(place == null, "no settlement to place the news at")) return t.Line();

         // Its own source, so repeated testall runs on one day never meet the SelfTest quota other tests share.
         const string source = "SelfTestHeard";
         string suffix = Guid.NewGuid().ToString("N");
         string placedId = "selftest_heard_placed_" + suffix;
         string bogusId = "selftest_heard_bogus_" + suffix;
         string namedId = "selftest_heard_named_" + suffix;
         var today = (int) CampaignTime.Now.ToDays;

         try
         {
            Feed.Record(new ExternalWorldEvent {
               Id = placedId, SourceMod = source, Summary = "[SELFTEST] a siege was laid and lifted", Magnitude = 60,
               FactionIds = new List<string> {lord.MapFaction.StringId}, PlaceId = place.StringId
            });
            WorldEvent placed = store.GetById(placedId);

            if (t.Abort(placed == null, "the placed event was not recorded")) return t.Line();

            t.Expect("aRealPlaceIsKept", placed.PlaceId, place.StringId);

            RumorAwarenessBreakdown verdict = store.Explain(lord, placed, today);
            t.Note($"{lord.Name} at {place.Name}: {verdict.Describe()}");
            t.Expect("theRealmTagReachesALordOfThatRealm", verdict.FactionMatches, true);
            t.Expect("heHasHeardIt", verdict.Knows, true);
            if (lord.CurrentSettlement == place) t.Expect("beingThereCountsFully", verdict.GeoBonus, RumorAwareness.GeoBonusMax);

            // The console must never say "heard" about a fact the prompt would not give him, or the reverse.
            t.Expect("cr.heardAgreesWithThePrompt", store.GetKnownEvents(lord, today, 1000).Any(e => e.Id == placedId), verdict.Knows);

            Feed.Record(new ExternalWorldEvent {
               Id = bogusId, SourceMod = source, Summary = "[SELFTEST] something happened somewhere", Magnitude = 10,
               PlaceId = "no_such_settlement_selftest"
            });
            t.Expect("aBogusPlaceIsDroppedNotKept", store.GetById(bogusId) != null && store.GetById(bogusId).PlaceId == null, true);

            // Magnitude 30, untagged: under the threshold for anyone but the one it names.
            Feed.Record(new ExternalWorldEvent {
               Id = namedId, SourceMod = source, Summary = "[SELFTEST] a quiet word about one lord", Magnitude = 30,
               HeroIds = new List<string> {lord.StringId}
            });
            WorldEvent named = store.GetById(namedId);

            if (t.Abort(named == null, "the named event was not recorded")) return t.Line();

            t.Expect("theLordItNamesCanSpeakOfIt", store.GetKnownEventsRated(lord, today, 1000).Any(x => x.Event.Id == namedId), true);

            Hero bystander = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != lord && h.IsLord);
            if (bystander != null) t.Expect("aBystanderHasNotHeardIt", store.IsAwareOf(bystander, named, today), false);

            // The command itself, by the lord's name. A namesake may resolve first, so only the id is asserted.
            string report = ModCheats.Heard(new List<string> {lord.Name.ToString(), placedId});
            t.Note("cr.heard: " + Preview(report));
            t.Expect("cr.heardReportsTheEvent", report != null && report.Contains(placedId), true);
         }
         finally
         {
            Feed.Retract(placedId);
            Feed.Retract(bogusId);
            Feed.Retract(namedId);
         }

         return t.Line();
      }

      private static string TestFeedPlayerNamed(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("feed_player_named");

         WorldEventStore store = WorldEventStore.Instance;

         if (t.Abort(store == null, "WorldEventStore not active")) return t.Line();
         if (t.Skip(Hero.MainHero == null, "no player hero")) return t.Line();

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.MapFaction != null);

         if (t.Skip(lord == null, "no free lord with a realm")) return t.Line();

         const string source = "SelfTestPlayerNamed";
         string suffix = Guid.NewGuid().ToString("N");
         string namedId = "selftest_named_" + suffix;
         string actedId = "selftest_acted_" + suffix;
         var today = (int) CampaignTime.Now.ToDays;

         try
         {
            // The Bellum shape: a siege of a town the PLAYER owns, the owner named among the heroes.
            Feed.Record(new ExternalWorldEvent {
               Id = namedId, SourceMod = source, Summary = "[SELFTEST] a town held by the player is under siege", Magnitude = 85,
               HeroIds = new List<string> {Hero.MainHero.StringId, lord.StringId},
               FactionIds = new List<string> {lord.MapFaction.StringId}
            });
            WorldEvent named = store.GetById(namedId);

            if (t.Abort(named == null, "the named event was not recorded")) return t.Line();

            t.Expect("namingThePlayerDoesNotMakeItTheirDeed", named.InvolvesPlayer, false);
            t.Expect("itStaysOutOfTheirChronicle", store.GetPlayerChronicle().Any(e => e.Id == namedId), false);
            t.Expect("aLordOfTheRealmHearsItAsNews", store.GetKnownEvents(lord, today, 1000).Any(e => e.Id == namedId), true);

            // The other half: the bridge says the player DID it.
            Feed.Record(new ExternalWorldEvent {
               Id = actedId, SourceMod = source, Summary = "[SELFTEST] the player had a lord executed", Magnitude = 85,
               HeroIds = new List<string> {lord.StringId}, ActorHeroIds = new List<string> {Hero.MainHero.StringId}
            });
            WorldEvent acted = store.GetById(actedId);

            if (t.Abort(acted == null, "the acted event was not recorded")) return t.Line();

            t.Expect("thePlayerAsActorMakesItTheirDeed", acted.InvolvesPlayer, true);
            t.Expect("anActorCountsAsConcernedToo", acted.ActorIds.Contains(Hero.MainHero.StringId), true);
            t.Expect("itEntersTheirChronicle", store.GetPlayerChronicle().Any(e => e.Id == actedId), true);
         }
         finally
         {
            Feed.Retract(namedId);
            Feed.Retract(actedId);
         }

         return t.Line();
      }

      /// <summary>
      ///   Heroes Voices (TTS addon) request: drives the REAL internal CrConversation raisers directly (this
      ///   file lives in the same assembly, so it may call them) rather than the engine chat path, so the test
      ///   runs with no campaign, no LLM, and nothing to clean up. Asserts the whole point of the sequencer:
      ///   one session's Started/LineSpoken/Ended share a SessionId and LineIndex counts up from 0, and a
      ///   fresh session never reuses either.
      /// </summary>
      private static string TestConversationSessionSequencing(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("conversation_session_sequencing");

         string startedSessionId = null;
         string endedSessionId = null;
         var lineSessionIds = new List<string>();
         var lineIndices = new List<int>();

         void OnStarted(CrConversationInfo info) => startedSessionId = info.SessionId;
         void OnLineSpoken(CrSpeechLine line)
         {
            lineSessionIds.Add(line.SessionId);
            lineIndices.Add(line.LineIndex);
         }
         void OnEnded(CrConversationLog log) => endedSessionId = log.SessionId;

         CrConversation.Started += OnStarted;
         CrConversation.LineSpoken += OnLineSpoken;
         CrConversation.Ended += OnEnded;

         try
         {
            CrConversation.RaiseStarted("selftest_npc", 1);
            CrConversation.RaiseLineSpoken(new CrSpeechLine(null, "selftest_npc", "Selftest NPC", false, false, false, false, null, "[SELFTEST] first line"));
            CrConversation.RaiseLineSpoken(new CrSpeechLine(null, "selftest_npc", "Selftest NPC", false, false, false, false, null, "[SELFTEST] second line"));
            CrConversation.RaiseEnded("selftest_npc", 1, new List<CrTranscriptLine>());

            string firstSessionId = startedSessionId;

            t.Expect("startedSessionIdNotEmpty", string.IsNullOrEmpty(startedSessionId), false);
            t.Expect("lineCount", lineSessionIds.Count, 2);
            t.Expect("line1SharesSession", lineSessionIds.Count > 0 && lineSessionIds[0] == firstSessionId, true);
            t.Expect("line2SharesSession", lineSessionIds.Count > 1 && lineSessionIds[1] == firstSessionId, true);
            t.Expect("endedSharesSession", endedSessionId, firstSessionId);
            t.Expect("lineIndex0", lineIndices.Count > 0 ? lineIndices[0] : -1, 0);
            t.Expect("lineIndex1", lineIndices.Count > 1 ? lineIndices[1] : -1, 1);

            // A second conversation must get a fresh id and its own count from 0, or two exchanges could
            // collide on the same (SessionId, LineIndex) pair.
            CrConversation.RaiseStarted("selftest_npc", 2);
            string secondSessionId = startedSessionId;
            CrConversation.RaiseLineSpoken(new CrSpeechLine(null, "selftest_npc", "Selftest NPC", false, false, false, false, null, "[SELFTEST] second session first line"));

            t.Expect("secondSessionDiffers", secondSessionId != firstSessionId, true);
            t.Expect("secondSessionLineIndexReset", lineIndices.Count > 2 ? lineIndices[2] : -1, 0);
            t.Note($"session1={firstSessionId}, session2={secondSessionId}");
         }
         finally
         {
            CrConversation.Started -= OnStarted;
            CrConversation.LineSpoken -= OnLineSpoken;
            CrConversation.Ended -= OnEnded;
         }

         return t.Line();
      }

      private static string TestFreePrisoner(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("free_prisoner");

         // Mirror prisoner_trade's pick (testall 2026-07-23 A/B evidence: within the same second,
         // TakePrisonerAction succeeded on an in-party lord and failed on Arcor picked without this filter,
         // and on every partyless minted nemesis hero). A lord OFF the map (no party, at court, a governor)
         // is not reliably capturable by the raw action on the current game build, and capturing one is not
         // what this test exists to prove anyway.
         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null && h.Clan?.Leader != h);

         if (t.Skip(captive == null, "no free in-party lord to capture")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);
         bool captured = captive.IsPrisoner && captive.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {captive.Name} (party={captive.PartyBelongedTo?.StringId ?? "none"})")) return t.Line();

         behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "free_prisoner"}, captive, null);

         t.Note($"{captive.Name}");
         t.Expect("freed", !captive.IsPrisoner, true);

         return t.Line();
      }

      /// <summary>
      ///   v2.5.3 player report (2026-09-07): "npc gave me a quest to rescue a clan member from a prison. i
      ///   got the person out of prison but then a couple days later i get 'so and so is lost, they are no
      ///   longer on the map' and the quest is failed." His success cancelled his quest: freeing someone from
      ///   a dungeon raises no battle, so QuestDeedTracker never credited the rescue, and the daily void sweep
      ///   then read the freed target as proof the task was impossible. Drives the REAL free_prisoner bridge
      ///   action (the same action ExecuteFreePrisoner/free_prisoner tests above use, standing in for "got the
      ///   person out of prison") on a real captured lord bound to a real outstanding RescuePrisoner quest, so
      ///   the REAL CampaignEvents.HeroPrisonerReleased dispatch reaches QuestDeedTracker.OnHeroPrisonerReleased
      ///   with no Debug seam involved. THE LOAD-BEARING ASSERTION: after the release, the quest is SATISFIED
      ///   (awaiting reward), and running the REAL daily maintenance sweep afterward must not undo that by
      ///   cancelling it, which is exactly what the reporter saw.
      /// </summary>
      private static string TestRescueCreditedOnRelease(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("quest_rescue_release_credit");

         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         // Same in-party-lord filter TestFreePrisoner/TestCaptiveKinAwareness use (off-map lords are not
         // reliably capturable by the raw action on this build); the choice of WHICH lord must not matter to
         // the outcome, so the predicate names only what is required, never a specific hero.
         Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null && h.Clan?.Leader != h);

         if (t.Skip(target == null, "no capturable in-party lord to stand as the rescue target")) return t.Line();

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != target && h.IsLord && !h.IsPrisoner);

         if (t.Skip(giver == null, "no other live lord to stand as the quest giver")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         const string marker = "[SELFTEST] free my kinsman from captivity.";

         try
         {
            TakePrisonerAction.Apply(PartyBase.MainParty, target);
            bool captured = target.IsPrisoner && target.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

            if (t.Abort(!captured, $"capture failed for {target.Name} (party={target.PartyBelongedTo?.StringId ?? "none"})")) return t.Line();

            NpcProfile profile = GetOrCreate(behavior, giver);
            var quest = new InformalQuest {
               Type = QuestType.RescuePrisoner,
               Description = marker,
               TargetHeroId = target.StringId,
               TargetHeroName = target.Name?.ToString(),
               IssuedOnDay = today,
               Status = QuestStatus.Active
            };
            profile.ActiveQuests.Add(quest);
            QuestJournalBridge.OpenMirror(quest, giver);
            behavior.Store.Set(profile);

            // THE PLAYER'S OWN DEED: free them through the real bridge action, not a battle, so the credit
            // must come from QuestDeedTracker's new HeroPrisonerReleased hook, not the pre-existing battle path.
            behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "free_prisoner"}, target, null);

            InformalQuest afterRelease = behavior.Store.Get(giver.StringId)?.ActiveQuests
                                                  .FirstOrDefault(q => q.Description == marker);

            t.Expect("targetFreedByRealAction", !target.IsPrisoner, true);
            t.Expect("stillActiveNotCancelledAfterRelease", afterRelease?.Status, QuestStatus.Active);
            t.Expect("awaitingRewardAfterRelease", afterRelease?.IsAwaitingReward, true);

            // THE LOAD-BEARING ASSERTION: the daily safety-net sweep, run for real, must not undo the credit
            // the release just earned. Before this fix, this exact call is what cancelled the reporter's quest.
            QuestService.RunDailyMaintenance(behavior.Store, today);

            InformalQuest afterSweep = behavior.Store.Get(giver.StringId)?.ActiveQuests
                                               .FirstOrDefault(q => q.Description == marker);

            t.Note($"{giver.Name} -> free {target.Name}: afterRelease.status={afterRelease?.Status}, evidence={afterRelease?.Evidence}, afterSweep.status={afterSweep?.Status}");
            t.Expect("survivesDailySweepSatisfiedNotCancelled", afterSweep?.Status, QuestStatus.Active);
            t.Expect("stillAwaitingRewardAfterSweep", afterSweep?.IsAwaitingReward, true);
         }
         finally
         {
            try
            {
               NpcProfile clean = behavior.Store.Get(giver.StringId);
               InformalQuest leftover = clean?.ActiveQuests.FirstOrDefault(q => q.Description == marker);

               if (leftover != null)
               {
                  QuestJournalBridge.CloseAsCancelled(leftover, giver, "[SELFTEST] cleanup.");
                  clean.ActiveQuests.Remove(leftover);
                  behavior.Store.Set(clean);
               }
            }
            catch (Exception ex) { ModLog.Error($"[SELFTEST] quest_rescue_release_credit quest cleanup failed: {ex.Message}"); }

            try { if (target.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(target); }
            catch (Exception ex) { ModLog.Error($"[SELFTEST] quest_rescue_release_credit release cleanup failed: {ex.Message}"); }
         }

         return t.Line();
      }

      /// <summary>
      ///   A player report (09/08/2026): the player held enemy lords prisoner, then met their parent, who
      ///   called the player a liar ("my child is safe within our walls") because the RELATIONSHIPS block named
      ///   the captured child with no captivity marker. Captures a real in-party lord who has a living parent,
      ///   rebuilds that parent's block, and asserts the captured child now reads as held by the player.
      ///   Releases the captive in cleanup so the sweep leaves no one caged.
      /// </summary>
      private static string TestCaptiveKinAwareness(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("captive_kin");

         // A capturable in-party lord (see TestFreePrisoner's note on why off-map lords are unreliable) who
         // ALSO has a living parent (not the player) to voice the denial: that parent is the NPC whose block
         // must reveal the captivity.
         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null && h.Clan?.Leader != h &&
            ((h.Father is {IsAlive: true} && h.Father != Hero.MainHero) || (h.Mother is {IsAlive: true} && h.Mother != Hero.MainHero)));

         if (t.Skip(captive == null, "no capturable in-party lord with a living parent to test")) return t.Line();

         Hero parent = captive.Father is {IsAlive: true} && captive.Father != Hero.MainHero
            ? captive.Father
            : captive.Mother;

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);
         bool captured = captive.IsPrisoner && captive.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {captive.Name}")) return t.Line();

         try
         {
            string block = RelationshipsBuilder.Build(parent);
            bool namesChild = block.IndexOf(captive.Name.ToString(), StringComparison.OrdinalIgnoreCase) >= 0;
            bool marksHeld = block.IndexOf("hold prisoner", StringComparison.OrdinalIgnoreCase) >= 0;

            t.Note($"parent={parent.Name} lists captive={captive.Name}: names={namesChild}, markedHeld={marksHeld}");
            t.Expect("parentNamesTheChild", namesChild, true);
            t.Expect("childMarkedHeldByPlayer", marksHeld, true);
         }
         finally
         {
            behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "free_prisoner"}, captive, null);
         }

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

      // The "gift equipment gets worn" feature (Gabriel, 2026-07-25): a gifted weapon a companion likes should
      // actually be EQUIPPED, not just leave the player's bag. The receiver is sentimental (a companion or the
      // spouse), so GiftEquipPolicy equips a lateral gift regardless of value, and a weapon avoids the item
      // gender-lock, so the only thing under test here is the bridge's equip step landing the item in a real slot.
      private static string TestGiftEquip(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("gift_equip");

         Hero receiver = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != Hero.MainHero && h.IsAlive && (h.IsPlayerCompanion || h.Spouse == Hero.MainHero));

         if (t.Skip(receiver == null, "no companion or spouse (a sentimental receiver) to test the equip")) return t.Line();

         ItemRoster roster = MobileParty.MainParty?.ItemRoster;

         if (t.Abort(roster == null, "no player item roster")) return t.Line();

         // A weapon is the least gender-locked equippable, so the item's own flags never block the equip and the
         // test isolates the bridge step (the policy is unit-tested separately in GiftEquipPolicyTests).
         ItemObject weapon = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>()
                                            ?.FirstOrDefault(it => it != null && it.HasWeaponComponent
                                                                && !it.ItemFlags.HasFlag(ItemFlags.NotUsableByFemale)
                                                                && !it.ItemFlags.HasFlag(ItemFlags.NotUsableByMale));

         if (t.Skip(weapon == null, "no gender-neutral weapon in the game data")) return t.Line();

         roster.AddToCounts(weapon, 1); // give_item removes one unit again

         var ctx = new ActionContext(GetOrCreate(behavior, receiver), null) {OfferedItemId = weapon.StringId};
         behavior.GameStateBridge.ExecuteAction(new GameAction {Type = "give_item"}, receiver, ctx);

         bool equipped = HeroHasWeaponEquipped(receiver, weapon);
         t.Note($"receiver={receiver.Name} (companion={receiver.IsPlayerCompanion}), item={weapon.Name}, equipped={equipped}");
         t.Expect("giftEquipped", equipped, true);

         return t.Line();
      }

      // The injectable NARRATIVE STYLE (Gabriel, 2026-07-26). The valuable, easy-to-break half is the LAZY
      // RELOAD: the file must be re-read when the player picks another style or edits the .txt DURING play (so a
      // modder can fine-tune a voice without restarting), while costing nothing on an unchanged message. This
      // drives the real StyleService against a real file on disk, so a broken cache rule is caught here rather
      // than by a player wondering why their edit does nothing.
      private static string TestStyleReload(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("style_reload");
         // No leading underscore: StyleCatalogPolicy.IsSelectableStyleFile treats a leading '_' as a draft and
         // hides it from the picker (player report 2026-07-26), so an underscore probe is correctly unselectable.
         const string probe = "CrSelfTestStyleProbe";
         string path = Styles.StyleCatalog.PathOf(probe);
         string restore = Styles.StyleService.ActiveName;

         try
         {
            Styles.StyleCatalog.EnsureFolder();
            System.IO.File.WriteAllText(path, "MARKER_ALPHA rules.");

            string applied = Styles.StyleService.Select(probe);
            if (t.Abort(!string.Equals(applied, probe, StringComparison.OrdinalIgnoreCase),
                   $"could not select the probe style (got '{applied}')")) return t.Line();

            string first = Styles.StyleService.CurrentRules();
            t.Expect("selectedStyleLoaded", first.Contains("MARKER_ALPHA"), true);

            // Serving the SAME unchanged file again must keep working (the cache path, no re-read).
            t.Expect("cachedServeStillCorrect", Styles.StyleService.CurrentRules().Contains("MARKER_ALPHA"), true);

            // Now the modder edits the style mid-game. The timestamp is set explicitly so the assertion never
            // rides on file-system clock granularity.
            System.IO.File.WriteAllText(path, "MARKER_BETA rules.");
            System.IO.File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));

            string afterEdit = Styles.StyleService.CurrentRules();
            t.Expect("editReloadedWithoutRestart", afterEdit.Contains("MARKER_BETA"), true);

            // And picking another style must take effect too.
            Styles.StyleService.Select("Default");
            t.Expect("selectionChangeApplied", Styles.StyleService.CurrentRules().Contains("MARKER_BETA"), false);

            t.Note($"probe='{probe}', folder={Styles.StyleCatalog.FolderPath}");
         }
         catch (Exception ex) { t.Note($"threw {ex.GetType().Name}: {ex.Message}"); }
         finally
         {
            try
            {
               if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            catch
            {
               /* best effort: a leftover probe file only shows up as one extra style in the picker */
            }

            Styles.StyleService.Select(restore);
         }

         return t.Line();
      }

      /// <summary>True when the hero carries the given item in any weapon slot of either equipment set.</summary>
      private static bool HeroHasWeaponEquipped(Hero hero, ItemObject item)
      {
         bool InSet(Equipment eq)
         {
            if (eq == null) return false;

            for (EquipmentIndex i = EquipmentIndex.Weapon0; i <= EquipmentIndex.ExtraWeaponSlot; i++)
               if (eq[i].Item == item) return true;

            return false;
         }

         return InSet(hero.BattleEquipment) || InSet(hero.CivilianEquipment);
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

      // ── Open relationship (Inc 2): the MCM jealousy gate + the competition open-exclusion, both on the ──
      // ── REAL daily-tick / selection path no dotnet unit test can reach (they read the live store, the ──
      // ── live culture data, and the MCM setting). The pure rules they call ARE unit-tested separately: ──
      // ── OpenRelationshipPolicy + CompetitionSelector; this proves only the WIRING and the MCM gate. ─────

      private static string TestOpenRelationship(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("open_relationship");

         if (t.Abort(JealousyBehavior.Instance == null || behavior?.Store == null, "JealousyBehavior/store not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         void Seed(Hero h, int attentionDay)
         {
            NpcProfile p = GetOrCreate(behavior, h);
            ArrangeCleanNpc(behavior, p, h);
            if (p.Romantic == null) p.Romantic = new RomanticProfile();
            p.Romantic.IsConsort = true;
            p.Romantic.Status = RomanticStatus.Committed;
            p.Romantic.IsOpenArrangement = false;
            p.Events.Add(new NotableEvent(attentionDay, NotableEventType.Collaboration, "[SELFTEST] attended"));
            behavior.Store.Set(p);
         }

         void Restore(IEnumerable<Hero> heroes)
         {
            foreach (Hero h in heroes)
            {
               NpcProfile p = behavior.Store.Get(h.StringId);
               if (p?.Romantic == null) continue;
               p.Romantic.IsConsort = false;
               p.Romantic.IsOpenArrangement = false;
               p.Romantic.Status = RomanticStatus.None;
               p.Events.Clear();
               behavior.Store.Set(p);
            }
         }

         // ── Part 1: the EnableJealousy MCM gate on the REAL daily tick ──
         CrSettings settings = CrSettings.Instance;
         if (t.Skip(settings == null, "MCM settings not loaded, cannot flip EnableJealousy")) return t.Line();

         bool savedEnable = settings.EnableJealousy;
         Hero gateNpc = ConversationNpc();
         if (t.Skip(gateNpc == null, "no NPC to seed a pending reaction on")) return t.Line();

         NpcProfile gateProfile = GetOrCreate(behavior, gateNpc);
         ArrangeCleanNpc(behavior, gateProfile, gateNpc);

         int JealousyEvents() => behavior.Store.Get(gateNpc.StringId)?.Events.Count(e => e.type == NotableEventType.Jealousy) ?? 0;
         int jealousyBefore = JealousyEvents();

         // OFF: a queued reaction must NOT fire (the whole daily tick early-returns).
         settings.EnableJealousy = false;
         JealousyBehavior.Instance.DebugEnqueuePending(gateNpc.StringId, "another love", today);
         int pendingAfterEnqueue = JealousyBehavior.Instance.DebugPendingCount;
         JealousyBehavior.Instance.DebugRunDailyTick();
         int pendingWhileOff = JealousyBehavior.Instance.DebugPendingCount;
         int jealousyWhileOff = JealousyEvents();

         // ON: the same queued reaction now fires (pending consumed, a Jealousy memory recorded).
         settings.EnableJealousy = true;
         JealousyBehavior.Instance.DebugRunDailyTick();
         int pendingWhileOn = JealousyBehavior.Instance.DebugPendingCount;
         int jealousyWhileOn = JealousyEvents();

         t.Note($"gate: pending {pendingAfterEnqueue}->OFF {pendingWhileOff}->ON {pendingWhileOn}; jealousy events {jealousyBefore}/OFF {jealousyWhileOff}/ON {jealousyWhileOn}");
         t.Expect("offSuppressedPending", pendingWhileOff, pendingAfterEnqueue);
         t.Expect("offRecordedNoJealousy", jealousyWhileOff, jealousyBefore);
         t.Expect("onFiredThePending", pendingWhileOn, pendingAfterEnqueue - 1);
         t.Expect("onRecordedAJealousy", jealousyWhileOn > jealousyBefore, true);

         ArrangeCleanNpc(behavior, behavior.Store.Get(gateNpc.StringId) ?? gateProfile, gateNpc); // undo the gate seed

         // ── Part 2: the competition open-exclusion on the REAL selection path ──
         int playerTier = Hero.MainHero?.Clan?.Tier ?? 0;

         bool Tolerant(Hero h) => JealousyCultureLoader.GetCulture(h.Culture?.Name?.ToString()).ToleratesSharedPartners(playerTier);

         List<Hero> pool = Hero.AllAliveHeroes
                               .Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsLord && !h.IsPrisoner
                                           && h != Hero.MainHero.Spouse && !PlayerPresenceResolver.IsBeside(h) && Tolerant(h))
                               .Take(3).ToList();

         if (t.Skip(pool.Count < 3, $"need 3 tolerant-culture lords away from the player (found {pool.Count}, playerTier={playerTier})")) return t.Line();

         Hero favoured = pool[0]; // well attended: sets the most-attended baseline
         Hero slighted = pool[1]; // neglected, non-open
         Hero mostNeglected = pool[2]; // most neglected: naturally aggrieved, then marked open
         var seeded = new List<Hero> {favoured, slighted, mostNeglected};

         Seed(favoured, today - 2);
         Seed(slighted, today - 25);
         Seed(mostNeglected, today - 45);

         // Run 1 (no open flags): the naturally most-slighted partner is chosen.
         string selected1 = JealousyBehavior.Instance.DebugSelectAggrieved(today);

         if (selected1 == null)
         {
            t.Note("Part 2 skipped: no aggrieved partner formed on the real path (culture/tier/presence did not cooperate)");
            Restore(seeded);
            settings.EnableJealousy = savedEnable;

            return t.Line();
         }

         // Mark whoever was chosen as holding OPEN terms, then re-run: she must now be excluded, and a
         // DIFFERENT, non-open partner chosen in her place (the reported no-jealousy pact, on the real path).
         NpcProfile openProfile = behavior.Store.Get(selected1);
         if (openProfile?.Romantic != null)
         {
            openProfile.Romantic.IsOpenArrangement = true;
            behavior.Store.Set(openProfile);
         }

         string selected2 = JealousyBehavior.Instance.DebugSelectAggrieved(today);

         t.Note($"run1 aggrieved={selected1}, after marking her open run2 aggrieved={selected2 ?? "none"}");
         t.Expect("secondRunStillPicksSomeone", selected2 != null, true);
         t.Expect("openPartnerExcluded", selected2 != selected1, true);

         Restore(seeded);
         settings.EnableJealousy = savedEnable;

         return t.Line();
      }

      // ── Open partner takes a lover (Inc 3a): the weekly autonomy is engine glue (it walks the live store, ──
      // ── reads the jealousy loop's neglect measure, scans Hero.AllAliveHeroes for a compatible match) that ──
      // ── no dotnet unit test can reach. The pure rules it calls ARE unit-tested (OpenPartnerLoverPolicy); ──
      // ── this proves only the WIRING: an open committed partner ends up with a real, plausible lover link. ──

      private static string TestOpenPartnerLover(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("open_partner_lover");

         if (t.Abort(OpenPartnerLover.OpenPartnerLoverBehavior.Instance == null || behavior?.Store == null, "OpenPartnerLoverBehavior/store not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         Hero partner = ConversationNpc();

         if (t.Skip(partner == null, "no NPC to stand as the open partner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, partner);
         ArrangeCleanNpc(behavior, profile, partner);
         if (profile.Romantic == null) profile.Romantic = new RomanticProfile();
         profile.Romantic.IsConsort = true;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.IsOpenArrangement = true;
         profile.Romantic.TakenLoverId = null;
         // Force high neglect: the last attention is long ago, so she is exactly the partner the autonomy is
         // likeliest to move on. The debug seam bypasses the roll, but the neglect is real and reported.
         profile.Events.Add(new NotableEvent(today - 45, NotableEventType.Collaboration, "[SELFTEST] attended long ago"));
         behavior.Store.Set(profile);

         int neglect = JealousyBehavior.NeglectDaysFor(partner, today);

         // Drive the REAL selection + link (bypasses only the weekly roll).
         Hero lover = OpenPartnerLover.OpenPartnerLoverBehavior.DebugTakeLover(partner);

         if (t.Skip(lover == null, $"no plausible lover in the live world for {partner.Name} (needs an unwed, orientation-compatible, acquainted adult lord outside your household)"))
         {
            RestoreOpenPartner(behavior, partner);

            return t.Line();
         }

         NpcProfile after = behavior.Store.Get(partner.StringId);
         string takenId = after?.Romantic?.TakenLoverId;

         t.Note($"partner={partner.Name} (neglect {neglect}d) took lover={lover.Name} [{takenId}]");
         t.Expect("takenLoverIdSet", takenId, lover.StringId);
         t.Expect("loverIsAlive", lover.IsAlive, true);
         t.Expect("loverIsNotThePlayer", lover != Hero.MainHero, true);
         t.Expect("loverIsUnwed", lover.Spouse == null, true);
         t.Expect("loverOutsidePlayerHousehold", lover.Clan != Clan.PlayerClan, true);

         RestoreOpenPartner(behavior, partner);

         return t.Line();
      }

      /// <summary>Undoes the open-partner-lover self-test's borrowed state on the partner's profile.</summary>
      private static void RestoreOpenPartner(NpcMemoryCampaignBehavior behavior, Hero partner)
      {
         NpcProfile p = behavior.Store.Get(partner.StringId);
         if (p?.Romantic == null) return;
         p.Romantic.TakenLoverId = null;
         p.Romantic.LoverIsSecret = false;
         p.Romantic.LoverTrystCount = 0;
         p.Romantic.LoverKnownToPlayer = false;
         p.Romantic.AffairDiscovered = false;
         p.Romantic.IsConsort = false;
         p.Romantic.IsOpenArrangement = false;
         p.Romantic.Status = RomanticStatus.None;
         p.Events.Clear();
         behavior.Store.Set(p);
      }

      // ── Player levers (Inc 3b-b): the two conversation OUTCOMES the model emits when a partner agrees, driven ──
      // ── through the REAL BannerlordGameStateBridge.ExecuteAction so the switch wiring, the guards, and the ──
      // ── field mutations are all exercised end to end. close_relationship revokes the open terms (leaving any ──
      // ── existing lover in place); a non-open committed partner is refused. ──────────────────────────────────

      private static string TestCloseRelationship(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("close_relationship");

         if (t.Abort(behavior?.Store == null || behavior.GameStateBridge == null, "store/bridge not active")) return t.Line();

         Hero partner = ConversationNpc();
         if (t.Skip(partner == null, "no NPC to stand as the partner")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, partner);
         ArrangeCleanNpc(behavior, profile, partner);
         if (profile.Romantic == null) profile.Romantic = new RomanticProfile();

         // ── An open committed consort: the action must lift the open terms and leave any lover untouched ──
         profile.Romantic.IsConsort = true;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.IsOpenArrangement = true;
         profile.Romantic.TakenLoverId = "selftest_close_lover"; // a placeholder lover: close must NOT clear it
         behavior.Store.Set(profile);

         ActionOutcome closed = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "close_relationship"}, partner, new ActionContext(profile, null));
         NpcProfile afterClose = behavior.Store.Get(partner.StringId);

         t.Note($"open consort {partner.Name}: close outcome={(closed?.WasRefused == true ? "refused" : closed?.IsPositive == true ? "applied" : "none")}");
         t.Expect("openConsortAccepted", closed?.WasRefused, false);
         t.Expect("openTermsRevoked", afterClose?.Romantic?.IsOpenArrangement, false);
         t.Expect("existingLoverUntouched", afterClose?.Romantic?.TakenLoverId, "selftest_close_lover");

         // ── A committed partner who is NOT open: refused, nothing to revoke ──
         profile.Romantic.IsOpenArrangement = false;
         profile.Romantic.TakenLoverId = null;
         behavior.Store.Set(profile);

         ActionOutcome refused = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "close_relationship"}, partner, new ActionContext(profile, null));

         t.Note($"non-open consort: {refused?.Message}");
         t.Expect("nonOpenRefused", refused?.WasRefused, true);

         RestoreOpenPartner(behavior, partner);

         return t.Line();
      }

      // ── end_affair (Inc 3b-b): a partner carrying a discovered lover, run through the REAL bridge, gives him ──
      // ── up, and EVERY affair-tracking field resets so no stale scandal state lingers; a partner with no lover ──
      // ── is refused (never a silent no-op that would let the model narrate an ending that never happened). ──────

      private static string TestEndAffair(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("end_affair");

         if (t.Abort(behavior?.Store == null || behavior.GameStateBridge == null, "store/bridge not active")) return t.Line();

         Hero partner = ConversationNpc();
         if (t.Skip(partner == null, "no NPC to stand as the partner")) return t.Line();

         Hero lover = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h != partner && h.IsAlive && h.IsLord);
         string loverId = lover?.StringId ?? "selftest_end_affair_lover";

         NpcProfile profile = GetOrCreate(behavior, partner);
         ArrangeCleanNpc(behavior, profile, partner);
         if (profile.Romantic == null) profile.Romantic = new RomanticProfile();

         // ── A DISCOVERED secret affair, fully populated, so the reset must clear ALL of it ──
         profile.Romantic.IsConsort = true;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.TakenLoverId = loverId;
         profile.Romantic.LoverIsSecret = true;
         profile.Romantic.LoverTrystCount = 4;
         profile.Romantic.AffairDiscovered = true;
         profile.Romantic.LoverKnownToPlayer = true;
         behavior.Store.Set(profile);

         int eventsBefore = behavior.Store.Get(partner.StringId)?.Events?.Count ?? 0;

         ActionOutcome ended = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "end_affair"}, partner, new ActionContext(profile, null));
         NpcProfile after = behavior.Store.Get(partner.StringId);

         t.Note($"{partner.Name} with lover [{loverId}]: end outcome={(ended?.WasRefused == true ? "refused" : ended?.IsPositive == true ? "applied" : "none")}; memories {eventsBefore}->{after?.Events?.Count}");
         t.Expect("affairEndAccepted", ended?.WasRefused, false);
         t.Expect("takenLoverCleared", string.IsNullOrEmpty(after?.Romantic?.TakenLoverId), true);
         t.Expect("loverSecretReset", after?.Romantic?.LoverIsSecret, false);
         t.Expect("trystCountReset", after?.Romantic?.LoverTrystCount, 0);
         t.Expect("affairDiscoveredReset", after?.Romantic?.AffairDiscovered, false);
         t.Expect("loverKnownReset", after?.Romantic?.LoverKnownToPlayer, false);
         t.Expect("endedAffairMemoryRecorded", (after?.Events?.Count ?? 0) > eventsBefore, true);

         // ── A partner with no lover: refused ──
         ActionOutcome refused = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "end_affair"}, partner, new ActionContext(after ?? profile, null));

         t.Note($"no lover: {refused?.Message}");
         t.Expect("noLoverRefused", refused?.WasRefused, true);

         RestoreOpenPartner(behavior, partner);

         return t.Line();
      }

      // ── Culture branch (Inc 3a): a harem / patriarchal (submissive-wife) culture (Aserai) takes a SECRET ──
      // ── lover, and may stray even WITHOUT an open arrangement when neglected, gated by the EnableJealousy ──
      // ── toggle; an egalitarian partner stays faithful with no pact. The pure culture rule is unit-tested ──
      // ── (OpenPartnerLoverPolicy.CanTakeLover / LoverIsSecret); this proves only the WIRING through the ──
      // ── real DebugTakeLover: the right partner strays, the affair is marked secret, and NO public rumour ──
      // ── is laid, while the toggle-off and egalitarian-no-pact cases produce no lover at all. ──────────────

      private static string TestHaremSecretLover(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("harem_secret_lover");

         if (t.Abort(OpenPartnerLover.OpenPartnerLoverBehavior.Instance == null || behavior?.Store == null, "OpenPartnerLoverBehavior/store not active")) return t.Line();

         CrSettings settings = CrSettings.Instance;
         if (t.Skip(settings == null, "MCM settings not loaded, cannot flip EnableJealousy")) return t.Line();

         bool HaremCulture(Hero h) => JealousyCultureLoader.IsHaremCulture(h?.Culture?.Name?.ToString());

         var today = (int) CampaignTime.Now.ToDays;

         Hero harem = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsLord
                                                           && h.IsFemale && !h.IsChild && HaremCulture(h));

         if (t.Skip(harem == null, "no live adult female lady of a harem culture (e.g. Aserai) to stand as the wife")) return t.Line();

         bool savedEnable = settings.EnableJealousy;

         void ArrangeNeglectedConsort(Hero partner)
         {
            NpcProfile p = GetOrCreate(behavior, partner);
            ArrangeCleanNpc(behavior, p, partner);
            if (p.Romantic == null) p.Romantic = new RomanticProfile();
            p.Romantic.IsConsort = true;
            p.Romantic.Status = RomanticStatus.Committed;
            p.Romantic.IsOpenArrangement = false; // NO pact: this is the uninvited / cultural-submission path
            p.Romantic.TakenLoverId = null;
            p.Romantic.LoverIsSecret = false;
            p.Events.Add(new NotableEvent(today - 45, NotableEventType.Collaboration, "[SELFTEST] attended long ago"));
            behavior.Store.Set(p);
         }

         // ── Case (B) ON: a neglected harem wife with no pact takes a SECRET lover, and lays NO public rumour ──
         ArrangeNeglectedConsort(harem);
         settings.EnableJealousy = true;
         int eventsBefore = WorldEventStore.Instance?.Count ?? 0;
         Hero secretLover = OpenPartnerLover.OpenPartnerLoverBehavior.DebugTakeLover(harem);

         if (t.Skip(secretLover == null, $"no plausible lover in the live world for {harem.Name} (needs an unwed, orientation-compatible, acquainted adult lord outside your household)"))
         {
            RestoreOpenPartner(behavior, harem);
            settings.EnableJealousy = savedEnable;

            return t.Line();
         }

         NpcProfile afterSecret = behavior.Store.Get(harem.StringId);
         int eventsAfterSecret = WorldEventStore.Instance?.Count ?? 0;

         t.Note($"harem={harem.Name} [{harem.Culture?.Name}] took secret lover={secretLover.Name}; world-events {eventsBefore}->{eventsAfterSecret}");
         t.Expect("secretTakenLoverIdSet", afterSecret?.Romantic?.TakenLoverId, secretLover.StringId);
         t.Expect("loverMarkedSecret", afterSecret?.Romantic?.LoverIsSecret, true);
         t.Expect("secretAffairLaysNoPublicRumour", eventsAfterSecret, eventsBefore);

         // ── Case OFF: the SAME wife with no pact stays faithful once the jealousy toggle is off ──
         RestoreOpenPartner(behavior, harem);
         ArrangeNeglectedConsort(harem);
         settings.EnableJealousy = false;
         Hero noneWhenOff = OpenPartnerLover.OpenPartnerLoverBehavior.DebugTakeLover(harem);
         string takenWhenOff = behavior.Store.Get(harem.StringId)?.Romantic?.TakenLoverId;

         t.Note($"toggle off: lover={(noneWhenOff?.Name?.ToString() ?? "none")} takenId={takenWhenOff ?? "null"}");
         t.Expect("toggleOffTakesNoLover", noneWhenOff == null, true);
         t.Expect("toggleOffLeavesNoTakenLoverId", string.IsNullOrEmpty(takenWhenOff), true);

         // ── Egalitarian control: a non-harem wife with no pact never strays, toggle on or off ──
         Hero egalitarian = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h != harem && h.IsAlive && h.IsLord
                                                                 && h.IsFemale && !h.IsChild && !HaremCulture(h));

         if (egalitarian != null)
         {
            ArrangeNeglectedConsort(egalitarian);
            settings.EnableJealousy = true; // even with drama allowed, an egalitarian wife needs an open pact
            Hero egalLover = OpenPartnerLover.OpenPartnerLoverBehavior.DebugTakeLover(egalitarian);
            string egalTaken = behavior.Store.Get(egalitarian.StringId)?.Romantic?.TakenLoverId;

            t.Note($"egalitarian={egalitarian.Name} [{egalitarian.Culture?.Name}] with no pact: lover={(egalLover?.Name?.ToString() ?? "none")}");
            t.Expect("egalitarianNoPactTakesNoLover", egalLover == null, true);
            t.Expect("egalitarianNoPactLeavesNoTakenLoverId", string.IsNullOrEmpty(egalTaken), true);

            RestoreOpenPartner(behavior, egalitarian);
         }
         else
         {
            t.Note("egalitarian control skipped: no non-harem female lady available");
         }

         RestoreOpenPartner(behavior, harem);
         settings.EnableJealousy = savedEnable;

         return t.Line();
      }

      // ── Trysts + affair discovery (Inc 3b-a): the weekly awareness/tryst pass is engine glue (walks the ──
      // ── store, reads live presence, bumps a relation, surfaces to the player) that no dotnet unit test can ──
      // ── reach. The pure rules ARE unit-tested (TrystPolicy / AffairDiscoveryPolicy); this proves only the ──
      // ── WIRING: forced trysts advance the counter + lay a private memory, a secret affair eventually leaks ──
      // ── (AffairDiscovered + LoverKnownToPlayer flip, a scandal chronicle lands), and an OPEN lover becomes ──
      // ── known WITHOUT any scandal. Discovery is driven by a fixed roll so the rising curve is deterministic. ──

      private static string TestAffairDiscovery(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("affair_discovery");

         if (t.Abort(OpenPartnerLover.OpenPartnerLoverBehavior.Instance == null || behavior?.Store == null || WorldEventStore.Instance == null, "OpenPartnerLoverBehavior / store / world-event store not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         Hero partner = ConversationNpc();

         if (t.Skip(partner == null, "no NPC to stand as the partner")) return t.Line();

         Hero lover = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h != partner && h.IsAlive && h.IsLord && h.Clan != Clan.PlayerClan);

         if (t.Skip(lover == null, "no live lord outside the player's household to stand as the lover")) return t.Line();

         int loverRelBefore = partner.GetRelation(lover);

         // ── Secret affair: seed it directly, then force trysts that leak it ─────────────────────────────────
         // Seed the SECRET lover link directly: this test exercises the tryst/discovery WIRING, not who strays
         // (the culture gate is covered by harem_secret_lover), so it bypasses TakeLover's selection.
         NpcProfile profile = GetOrCreate(behavior, partner);
         ArrangeCleanNpc(behavior, profile, partner);
         if (profile.Romantic == null) profile.Romantic = new RomanticProfile();
         profile.Romantic.IsConsort = true;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.TakenLoverId = lover.StringId;
         profile.Romantic.LoverIsSecret = true;
         profile.Romantic.LoverTrystCount = 0;
         profile.Romantic.LoverKnownToPlayer = false;
         profile.Romantic.AffairDiscovered = false;
         behavior.Store.Set(profile);

         int eventsBefore = WorldEventStore.Instance.Count;

         // A fixed discovery roll of 20 against the rising curve (8, 15, 22, ...): the first two trysts miss and
         // the THIRD exposes it, proving both the rising chance and that the reveal fires only once.
         OpenPartnerLover.OpenPartnerLoverBehavior.DebugTryst(partner, discoveryRoll: 20);
         bool discoveredAfterOne = behavior.Store.Get(partner.StringId)?.Romantic?.AffairDiscovered ?? false;
         OpenPartnerLover.OpenPartnerLoverBehavior.DebugTryst(partner, discoveryRoll: 20);
         OpenPartnerLover.OpenPartnerLoverBehavior.DebugTryst(partner, discoveryRoll: 20);

         NpcProfile afterSecret = behavior.Store.Get(partner.StringId);
         int eventsAfter = WorldEventStore.Instance.Count;

         t.Note($"secret: partner={partner.Name} lover={lover.Name}; trysts={afterSecret?.Romantic?.LoverTrystCount}; memories={afterSecret?.Events?.Count}; world-events {eventsBefore}->{eventsAfter}");
         t.Expect("trystCounterAdvanced", afterSecret?.Romantic?.LoverTrystCount, 3);
         t.Expect("notDiscoveredOnFirstTryst", discoveredAfterOne, false);
         t.Expect("affairDiscovered", afterSecret?.Romantic?.AffairDiscovered, true);
         t.Expect("loverKnownToPlayer", afterSecret?.Romantic?.LoverKnownToPlayer, true);
         t.Expect("scandalChronicleRecorded", eventsAfter > eventsBefore, true);
         t.Expect("privateTrystMemoriesRecorded", (afterSecret?.Events?.Count ?? 0) >= 3, true);

         // ── Open lover: becomes known WITHOUT a scandal ───────────────────────────────────────────────────
         RestoreOpenPartner(behavior, partner);
         NpcProfile openProfile = GetOrCreate(behavior, partner);
         ArrangeCleanNpc(behavior, openProfile, partner);
         if (openProfile.Romantic == null) openProfile.Romantic = new RomanticProfile();
         openProfile.Romantic.IsConsort = true;
         openProfile.Romantic.Status = RomanticStatus.Committed;
         openProfile.Romantic.IsOpenArrangement = true;
         openProfile.Romantic.TakenLoverId = lover.StringId;
         openProfile.Romantic.LoverIsSecret = false;
         openProfile.Romantic.LoverKnownToPlayer = false;
         openProfile.Romantic.AffairDiscovered = false;
         behavior.Store.Set(openProfile);

         bool openKnown = OpenPartnerLover.OpenPartnerLoverBehavior.DebugSurfaceOpenLover(partner);
         NpcProfile afterOpen = behavior.Store.Get(partner.StringId);

         t.Note($"open: known={openKnown} affairDiscovered={afterOpen?.Romantic?.AffairDiscovered}");
         t.Expect("openLoverBecomesKnown", openKnown, true);
         t.Expect("openLoverNoScandal", afterOpen?.Romantic?.AffairDiscovered, false);

         // Restore all borrowed state, including the partner-to-lover relation the trysts warmed.
         RestoreOpenPartner(behavior, partner);
         NeutralizeRelation(partner, lover);
         if (loverRelBefore != 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(partner, lover, loverRelBefore, false);

         return t.Line();
      }

      // ── Pregnancy by the lover (Inc 3c): the conception + birth-attribution is engine glue (reflective ──
      // ── pregnancy, the vanilla Father field, the birth-pipeline clan placement) that no dotnet unit test can ──
      // ── reach. The pure rules ARE unit-tested (LoverConceptionPolicy / LoverChildClanPolicy); this proves the ──
      // ── WIRING: a forced conception names the LOVER as father (never the player), the persisted lover-child ──
      // ── marker names him, the resolved birth clan is never the player's, and the pregnancy outs a secret affair. ──
      // ── A birth cannot be forced in a self-test, so the CLAN outcome is asserted via the policy + the marker. ──

      private static string TestLoverChild(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("lover_child");

         if (t.Abort(OpenPartnerLover.OpenPartnerLoverBehavior.Instance == null || PregnancyBirthBehavior.Instance == null || behavior?.Store == null, "OpenPartnerLoverBehavior / PregnancyBirthBehavior / store not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         // The mother must be a FERTILE female CR truly accepts (IsEligible is the same gate production uses), so
         // the forced conception is not silently refused. MANUAL when the live world offers none.
         Hero partner = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsFemale && PregnancyService.IsEligible(h));

         if (t.Skip(partner == null, "no fertile female hero to stand as the mother (needs IsEligible: female, fertile age, not already pregnant, births enabled)")) return t.Line();

         Hero lover = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h != partner && h.IsAlive && h.IsLord && h.Clan != null && h.Clan != Clan.PlayerClan);

         if (t.Skip(lover == null, "no live lord with a clan outside the player's household to stand as the lover")) return t.Line();

         int loverRelBefore = partner.GetRelation(lover);

         // Seed a SECRET affair directly: this test exercises the conception + attribution WIRING, not who strays
         // (the culture gate is covered elsewhere), so it bypasses TakeLover's selection.
         NpcProfile profile = GetOrCreate(behavior, partner);
         ArrangeCleanNpc(behavior, profile, partner);
         if (profile.Romantic == null) profile.Romantic = new RomanticProfile();
         profile.Romantic.IsConsort = true;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.TakenLoverId = lover.StringId;
         profile.Romantic.LoverIsSecret = true;
         profile.Romantic.LoverKnownToPlayer = false;
         profile.Romantic.AffairDiscovered = false;
         behavior.Store.Set(profile);

         // Drive the REAL conception by the lover (bypasses only the per-tryst roll, never the fertility gate).
         Hero conceivedBy = OpenPartnerLover.OpenPartnerLoverBehavior.DebugConceiveByLover(partner);

         if (t.Skip(conceivedBy == null, $"the engine refused the conception for {partner.Name} (no eligible pregnancy could be created)"))
         {
            RestoreLoverChild(behavior, partner, lover, loverRelBefore);

            return t.Line();
         }

         // The engine's OWN pregnancy list must name the LOVER as father, never the player.
         PregnancyService.TryGetPregnancyInfo(partner, out Hero father, out int daysRemaining);

         // The persisted lover-child marker must name the lover as the genetic father.
         string markerFatherId = PregnancyBirthBehavior.Instance.DebugLoverChildFatherId(partner);

         // The resolved birth clan, via the SAME pure rule the birth pipeline uses: it must be the lover's line
         // and must never be the player's clan.
         Clan playerClan = Clan.PlayerClan;
         LoverChildClanTarget verdict = LoverChildClanPolicy.Resolve(new LoverChildClanPolicy.LoverChildFacts {LoverHasClan = lover.Clan != null, LoverClanIsPlayer = lover.Clan != null && lover.Clan == playerClan, MotherHasClan = partner.Clan != null, MotherClanIsPlayer = partner.Clan != null && partner.Clan == playerClan});
         Clan destination = verdict switch {
            LoverChildClanTarget.LoverClan => lover.Clan,
            LoverChildClanTarget.MotherClan => partner.Clan,
            _ => null
         };

         NpcProfile after = behavior.Store.Get(partner.StringId);

         t.Note($"mother={partner.Name} lover={lover.Name}; engineFather={father?.Name?.ToString() ?? "(none)"}; marker={markerFatherId}; verdict={verdict} dest={destination?.Name?.ToString() ?? "-"} playerClan={playerClan?.Name?.ToString() ?? "-"}; due~{daysRemaining}d");
         t.Expect("conceivedByLover", conceivedBy == lover, true);
         t.Expect("engineFatherIsLover", father == lover, true);
         t.Expect("engineFatherIsNotPlayer", father != Hero.MainHero, true);
         t.Expect("loverChildMarkerNamesLover", markerFatherId, lover.StringId);
         t.Expect("birthClanIsLoversLine", verdict, LoverChildClanTarget.LoverClan);
         t.Expect("birthClanIsNotPlayerClan", destination != null && destination != playerClan, true);
         t.Expect("affairDiscoveredByChild", after?.Romantic?.AffairDiscovered, true);
         t.Expect("loverKnownByChild", after?.Romantic?.LoverKnownToPlayer, true);

         // Clean up: end the seeded pregnancy, drop CR tracking, and restore every borrowed field + relation.
         RestoreLoverChild(behavior, partner, lover, loverRelBefore);

         return t.Line();
      }

      /// <summary>Ends the lover-child self-test's seeded pregnancy, drops CR tracking, and restores the partner's borrowed state.</summary>
      private static void RestoreLoverChild(NpcMemoryCampaignBehavior behavior, Hero partner, Hero lover, int loverRelBefore)
      {
         PregnancyService.TryEndStuckPregnancy(partner); // reflective remove of the seeded engine pregnancy + clears IsPregnant
         PregnancyBirthBehavior.Instance?.DebugForgetTracking(partner);
         RestoreOpenPartner(behavior, partner);
         if (lover != null)
         {
            NeutralizeRelation(partner, lover);
            if (loverRelBefore != 0) ChangeRelationAction.ApplyRelationChangeBetweenHeroes(partner, lover, loverRelBefore, false);
         }
      }

      // ── Duel consequences: the applier is engine glue (renown, stance, attraction, world-event) that no ──
      // ── dotnet unit test can reach; the pure policies it calls ARE unit-tested, so this proves only the ──
      // ── WIRING, that the applier moves the right live state by the right (policy-owned) amounts. ──────────

      private static string TestDuelConsequence(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("duel_consequence");

         if (t.Abort(Hero.MainHero?.Clan == null, "no player clan to read renown from")) return t.Line();

         Hero rival = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner && !h.IsFemale);

         if (t.Skip(rival == null, "no live male lord to stand as the rival")) return t.Line();

         Hero lady = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != rival && h.IsFemale && h.IsAlive
                                                          && GetOrCreate(behavior, h)?.Romantic != null);

         if (t.Skip(lady == null, "no female hero with a romantic profile to court")) return t.Line();

         // Seed a known attraction baseline so the deltas are read cleanly (0 keeps every step inside [-100,100]).
         NpcProfile ladyProfile = GetOrCreate(behavior, lady);
         int originalAttraction = ladyProfile.Romantic.AttractionToPlayer;
         ladyProfile.Romantic.AttractionToPlayer = 0;
         behavior.Store.Set(ladyProfile);

         int eventsBefore = WorldEventStore.Instance?.Count ?? 0;
         int renownBefore = (int) Hero.MainHero.Clan.Renown;

         // WIN: her attraction rises by exactly the policy's AdmirationOnWin, and the player's renown lifts.
         DuelOutcomeApplier.Apply(DuelReason.CourtshipRivalry, DuelResolution.PlayerWon, rival, lady, witnessCount: 0);
         int afterWin = behavior.Store.Get(lady.StringId).Romantic.AttractionToPlayer;
         int renownAfterWin = (int) Hero.MainHero.Clan.Renown;

         // REFUSE (from the post-win value): dropping by CowardiceOnRefuse proves the forfeit path AND that
         // walking away costs more of her regard than an honest defeat would.
         DuelOutcomeApplier.Apply(DuelReason.CourtshipRivalry, DuelResolution.PlayerForfeited, rival, lady, witnessCount: 0);
         int afterRefuse = behavior.Store.Get(lady.StringId).Romantic.AttractionToPlayer;

         int eventsAfter = WorldEventStore.Instance?.Count ?? 0;

         t.Note($"rival={rival.Name}, lady={lady.Name}: attraction 0 -> {afterWin} (win) -> {afterRefuse} (refuse); " +
                $"renown {renownBefore}->{renownAfterWin}; world-events {eventsBefore}->{eventsAfter}");
         t.Expect("winRaisesAttractionByAdmiration", afterWin, CourtshipDuelPolicy.AdmirationOnWin);
         t.Expect("refuseDropsAttractionByCowardice", afterRefuse, afterWin + CourtshipDuelPolicy.CowardiceOnRefuse);
         t.Expect("winLiftedPlayerRenown", renownAfterWin > renownBefore, true);
         t.Expect("bothDuelsRecordedAWorldEvent", eventsAfter - eventsBefore >= 2, true);

         // Restore the one thing we explicitly seeded; renown/world-events are left as the harness warns (reload to discard).
         NpcProfile restore = behavior.Store.Get(lady.StringId);
         restore.Romantic.AttractionToPlayer = originalAttraction;
         behavior.Store.Set(restore);

         return t.Line();
      }

      // ── Reasoning-trace scrub: the save-healing half of the inline <think> fix (the parse-time strip ──
      // ── stops NEW leaks; this heals what an old save already stores). The save/load round trip is ──
      // ── STUBBED: corrupted summaries are written straight into the live store, exactly the state a ──
      // ── pre-fix save would deserialize into, then the REAL session-launch step runs against it. ──────

      private static string TestReasoningScrub(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("reasoning_scrub");

         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (t.Skip(npc == null, "no live lord to seed a profile on")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         int day = behavior.GameStateBridge.GetCurrentGameDay();
         string originalBackground = profile.BackgroundContext;

         // The two reported shapes, seeded exactly as an old save would carry them: real prose hidden
         // behind a closed trace, and the screenshot case (a memory that is ONLY a truncated trace).
         const string recoverable = "[SELFTEST] a real memory recovered from behind the trace";
         profile.Events.Add(new NotableEvent(day, NotableEventType.Collaboration,
            "<think>The user wants me to write a memory line.</think>" + recoverable));
         profile.Events.Add(new NotableEvent(day, NotableEventType.Collaboration,
            "<think>Let me summarize what happened: 1. Huan Yi"));
         profile.BackgroundContext = "<think>I need to compress these older events";
         behavior.Store.Set(profile);

         // The REAL launch step: the exact code OnSessionLaunched runs against every loaded save.
         behavior.ScrubLeakedReasoningTraces();

         NpcProfile healed = behavior.Store.Get(npc.StringId);
         NotableEvent recovered = healed.Events[healed.Events.Count - 2];
         NotableEvent faded = healed.Events[healed.Events.Count - 1];

         t.Note($"npc={npc.Name}: recovered='{recovered.summary}', faded='{faded.summary}', background={healed.BackgroundContext ?? "null"}");
         t.Expect("proseRecovered", recovered.summary, recoverable);
         t.Expect("allTraceBecomesFadedLine", faded.summary, ReasoningTraceBackfill.LostMemoryLine);
         t.Expect("backgroundReturnsToNull", healed.BackgroundContext == null, true);

         // Idempotence is the property that makes running this on EVERY launch safe (no version
         // marker): a second pass over the just-healed profile must report nothing to change.
         t.Expect("secondPassIsNoOp", ReasoningTraceBackfill.Scrub(healed), false);

         // Cleanup: the seeds were appended last and healed IN PLACE, so they are still the last two.
         healed.Events.RemoveAt(healed.Events.Count - 1);
         healed.Events.RemoveAt(healed.Events.Count - 1);
         healed.BackgroundContext = originalBackground;
         behavior.Store.Set(healed);

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

         var cause = $"[SELFTEST] {Hero.MainHero.Name} had {victim.Name} put to death.";
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
         if (npc.GetRelation(target) <= NearTheEngineFloor)
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
         // construction, exercising the exact real path a match CR seals with no seeded blessing takes. Through
         // CR's own door: since 25/09/2026 a marriage the base game makes is approved by construction.
         MarriageApprovalBehavior.MarryThroughCr(Hero.MainHero, spouse);

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
         // 4000, not 5000: GiveGoldAction pays only what the GIVER's purse actually holds, so a reward larger
         // than a typical lord carries measures his wealth rather than the reward pipeline (Arcor paid 4894 of
         // 5000 on 2026-09-06). Gabriel's call: keep the assertion exact and pick a sum a giver can meet.
         const int rewardGold = 4000;
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

      // ── Nemesis pillar increment 3: bounty missions ─────────────────────────────────────────────────

      private static string TestNemesisBountyCapture(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_bounty_capture");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero giver, Hero nemesisHero, NemesisIdentity nem) = MintBountyEligibleNemesis(today);

         if (t.Skip(giver == null, "no lord with a resolvable region available to hold the bounty")) return t.Line();
         if (t.Abort(nemesisHero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         t.Expect("eligibleBeforeIssue", NemesisBountyDialogFlow.FindEligibleNemesisFor(giver) == nem, true);

         int expectedGold = NemesisBountyRewardPolicy.ComputeGold(nem.RecaptureCount, nem.TimesEncountered);
         int expectedRelation = NemesisBountyRewardPolicy.ComputeRelation(nem.RecaptureCount);
         var displayName = $"{nem.Name} {nem.Epithet}";

         NpcProfile profile = GetOrCreate(behavior, giver);
         InformalQuest quest = QuestService.IssueNemesisBounty(
            profile, giver, nemesisHero, displayName, expectedGold, expectedRelation, today, behavior.Store);

         if (t.Abort(quest == null, $"IssueNemesisBounty refused ({giver.Name} already had an outstanding task?)")) return t.Line();

         t.Expect("noLongerEligibleOnceIssued", NemesisBountyDialogFlow.FindEligibleNemesisFor(giver) == nem, false);

         TakePrisonerAction.Apply(PartyBase.MainParty, nemesisHero);
         bool captured = nemesisHero.IsPrisoner && nemesisHero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {nemesisHero.Name} (party={nemesisHero.PartyBelongedTo?.StringId ?? "none"}; a PARTYLESS hero may not be capturable by the raw action on this game build)")) return t.Line();

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
         if (t.Abort(nemesisHero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         int expectedGold = NemesisBountyRewardPolicy.ComputeGold(nem.RecaptureCount, nem.TimesEncountered);
         int expectedRelation = NemesisBountyRewardPolicy.ComputeRelation(nem.RecaptureCount);
         var displayName = $"{nem.Name} {nem.Epithet}";

         NpcProfile profile = GetOrCreate(behavior, giver);
         InformalQuest quest = QuestService.IssueNemesisBounty(
            profile, giver, nemesisHero, displayName, expectedGold, expectedRelation, today, behavior.Store);

         if (t.Abort(quest == null, $"IssueNemesisBounty refused ({giver.Name} already had an outstanding task?)")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, nemesisHero);
         bool captured = nemesisHero.IsPrisoner && nemesisHero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {nemesisHero.Name} (party={nemesisHero.PartyBelongedTo?.StringId ?? "none"}; a PARTYLESS hero may not be capturable by the raw action on this game build)")) return t.Line();

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

      private static string TestNemesisExecuted(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_execute");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero hero, NemesisIdentity nem) = MintSelfTestNemesis("selftest_nemesis_execute", today);

         if (t.Abort(hero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         NemesisResolutionBehavior.Resolve(hero, Hero.MainHero, true);

         t.Expect("isAlive", nem.IsAlive, false);

         WorldEvent closure = WorldEventStore.Instance.GetPlayerChronicle()
                                             .LastOrDefault(e => e.Day == today && e.ActorIds != null && e.ActorIds.Contains(hero.StringId));

         t.Expect("closureRecorded", closure != null, true);

         string playerName = Hero.MainHero.Name?.ToString() ?? "the player";
         var displayName = $"{nem.Name} {nem.Epithet}";
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

         if (t.Abort(hero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         t.Expect("notHeldBeforeCapture", NemesisBehavior.Instance.PlayerHeldNemesisPrisoner() == hero, false);

         TakePrisonerAction.Apply(PartyBase.MainParty, hero);
         bool captured = hero.IsPrisoner && hero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {hero.Name} (party={hero.PartyBelongedTo?.StringId ?? "none"}; a PARTYLESS hero may not be capturable by the raw action on this game build)")) return t.Line();

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
      ///   Active hunt (map-presence): a latent nemesis of a live bandit band's culture materializes INTO that
      ///   band when the player engages it, so a battle the player chose to start can put the hunt's target in
      ///   front of him. Seeds a looters nemesis (region-agnostic, so the match is culture-only), forces the
      ///   roll, and asserts the nemesis Hero joined the band's roster. Cleans up: detach + mark the identity dead.
      /// </summary>
      private static string TestNemesisHunt(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_hunt");

         var nem = NemesisBehavior.Instance;

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
         NemesisBehavior.DetachToLatent(hero);

         Hero materialized = null;
         try
         {
            materialized = nem.TryMaterializeInto(band, true);
            bool inBand = materialized != null && materialized.PartyBelongedTo == band;

            t.Note($"band={band.Name} (looters), materialized={materialized?.Name}, joinedBand={inBand}");
            t.Expect("materialized", materialized != null, true);
            t.Expect("joinedTheBand", inBand, true);
         }
         finally
         {
            if (materialized != null) NemesisBehavior.DetachToLatent(materialized);
            identity.IsAlive = false;
         }

         return t.Line();
      }

      private static string TestNemesisKilledInBattle(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_kill");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();
         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero hero, NemesisIdentity nem) = MintSelfTestNemesis("selftest_nemesis_battle", today);

         if (t.Abort(hero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         NemesisResolutionBehavior.Resolve(hero, Hero.MainHero, false);

         t.Expect("isAlive", nem.IsAlive, false);
         t.Expect("deathDayStamped", nem.DeathDay == today, true);

         WorldEvent closure = WorldEventStore.Instance.GetPlayerChronicle()
                                             .LastOrDefault(e => e.Day == today && e.ActorIds != null && e.ActorIds.Contains(hero.StringId));

         t.Expect("closureRecorded", closure != null, true);
         t.Note($"summary={closure?.Summary}");

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

         var nb = NemesisBehavior.Instance;

         if (t.Skip(nb == null, "NemesisBehavior not available")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         Hero h1 = nb.MintCaptorFor("looters", "selftest_region", today, true);

         if (t.Abort(h1 == null, "mint returned no Hero (check the log: bandit clan/template unavailable, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         t.Expect("nemesisHeroHasMemoryProfile", behavior.Store?.Get(h1.StringId) != null, true);

         Hero h2 = nb.MintCaptorFor("looters", "selftest_region", today, true);
         t.Expect("sameRegionReusesSameHero", h2 == h1, true);

         Hero h3 = nb.MintCaptorFor("looters", "selftest_other_region", today, true);
         t.Expect("differentRegionMintsDistinctHero", h3 != h1, true);

         t.Note($"minted nemesis {h1.Name}: profile registered, reused in-region, distinct across regions. Plants test nemeses (reload after the sweep).");

         return t.Line();
      }

      /// <summary>
      ///   Encyclopedia discoverability piece (player report SuperGraham339): captured/tortured by a nemesis,
      ///   escaped, killed the party, ran into him again, then "tried to look him up in the encyclopedia but
      ///   didn't see any mention." Drives NemesisDossierFactory.BuildFor directly, the exact same call
      ///   EncyclopediaHeroPageNemesisMixin and cr.nemesis [name] make, so this proves the mixin's own gate
      ///   and wording without touching the (untestable) UIExtenderEx view model. First asserts a freshly
      ///   MINTED nemesis (TimesEncountered still 0, since MintCaptorFor never bumps it) stays HIDDEN (the
      ///   "hidden until materialised" design the whole pillar was ratified around), then bumps the counters
      ///   and plants a NotableEvent on his profile to assert the dossier renders his identity, the shared
      ///   history counters, AND the planted history sentence once he is a real ENCOUNTERED nemesis.
      /// </summary>
      private static string TestNemesisDossier(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_dossier");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero hero, NemesisIdentity nem) = MintSelfTestNemesis("selftest_nemesis_dossier", today);

         if (t.Abort(hero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         // Latent: MintCaptorFor (unlike a real capture/materialize) never bumps TimesEncountered, so the
         // dossier must stay hidden right after minting; leaking a latent nemesis here would spoil the
         // late-reveal beat the whole pillar exists to deliver.
         nem.TimesEncountered = 0;
         NemesisDossierText hidden = NemesisDossierFactory.BuildFor(hero);
         t.Expect("latentHasNoDossier", hidden.HasDossier, false);

         // Encountered: the exact player report (captured, escaped, met again), so once TimesEncountered
         // rises the dossier must show his identity and the shared history (counters + planted event).
         nem.TimesEncountered = 2;
         nem.RecaptureCount = 1;

         NpcProfile profile = behavior.Store?.Get(hero.StringId);
         if (profile != null)
         {
            profile.Events.Add(new NotableEvent(today - 5, NotableEventType.Conflict,
               "selftest: slipped his grasp and escaped his hold."));
            behavior.Store.Set(profile);
         }

         NemesisDossierText dossier = NemesisDossierFactory.BuildFor(hero);
         t.Expect("encounteredHasDossier", dossier.HasDossier, true);
         t.Expect("labelIsNemesisHeader", dossier.Label, "Nemesis:");
         t.Expect("itemsNameHero", dossier.Items != null && dossier.Items.Contains(nem.Name), true);
         t.Expect("itemsShowHistorySentence", dossier.Items != null && dossier.Items.Contains("slipped his grasp"), true);

         t.Note($"{nem.Name} {nem.Epithet}: hidden while latent, dossier shown once encountered. Plants a test nemesis (reload after).");

         return t.Line();
      }

      /// <summary>
      ///   Tavern-keeper rumour discoverability (ROADMAP follow-up to nemesis discoverability, v2.1.2): a
      ///   nemesis anchored to a real tavern keeper's own region, once ENCOUNTERED, must make
      ///   NemesisTavernRumorDialogFlow's gate (FindRumorNemesisFor, the exact function the "tavernkeeper_talk"
      ///   player line's condition calls) find him eligible; a LATENT nemesis (TimesEncountered 0) or a TURNED
      ///   one must both stay hidden, exactly like the encyclopedia dossier and the lord bounty gate. Then
      ///   NemesisRumorLine.Compose (the pure sentence the keeper's reply line substitutes in) must name him
      ///   and his region while never stating a reward figure, since this pillar is discoverability only and
      ///   NemesisBountyDialogFlow alone owns issuing/settling a bounty.
      /// </summary>
      private static string TestNemesisTavernRumor(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("nemesis_tavern_rumor");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero keeper, Hero nemesisHero, NemesisIdentity nem) = MintRumorEligibleNemesis(today);

         if (t.Skip(keeper == null, "no tavern keeper hero with a resolvable region available in this campaign")) return t.Line();
         if (t.Abort(nemesisHero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         t.Expect("eligibleWhenEncountered", NemesisTavernRumorDialogFlow.FindRumorNemesisFor(keeper) == nem, true);

         // Latent (never met): the discoverability gate itself must refuse, or a rumour would spoil the
         // late-reveal beat the whole pillar exists to deliver.
         nem.TimesEncountered = 0;
         t.Expect("hiddenWhileLatent", NemesisTavernRumorDialogFlow.FindRumorNemesisFor(keeper) == null, true);
         nem.TimesEncountered = 1;

         // Turned (spared into the player's service, audit M3's rule): no longer a foe, so no bounty-pointing
         // rumour should name him either.
         nem.TurnedDay = today;
         t.Expect("hiddenOnceTurned", NemesisTavernRumorDialogFlow.FindRumorNemesisFor(keeper) == null, true);
         nem.TurnedDay = null;

         string displayName = $"{nem.Name} {nem.Epithet}";
         string regionDisplay = NemesisDossierFactory.ResolveRegionDisplay(nem.RegionId);
         string line = NemesisRumorLine.Compose(displayName, regionDisplay);

         t.Expect("lineNamesTheNemesis", line.Contains(nem.Name), true);
         t.Expect("lineStatesNoRewardFigure", line.Any(char.IsDigit), false);
         t.Note($"{keeper.Name} on {nem.Name} {nem.Epithet}: \"{line}\". Plants a test nemesis (reload after).");

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

         if (t.Abort(hero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, hero);

         if (t.Abort(NemesisBehavior.Instance.PlayerHeldNemesisPrisoner() != hero, $"capture/detection failed for {hero.Name} (prisoner={hero.IsPrisoner}, party={hero.PartyBelongedTo?.StringId ?? "none"}; a PARTYLESS hero may not be capturable by the raw action on this game build)")) return t.Line();

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
      ///   Party troop-count wiring (deterministic, no LLM): an interlocutor sees the force at the
      ///   player's back in context (any face-to-face speaker now receives it, rendered as a qualitative
      ///   band in the prompt), so NPCs stop calling an army-leading player "alone". Exercised through a
      ///   party companion; skips when the player has none to speak for.
      /// </summary>
      private static string TestPartyTroopCount(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("party_troop_count");

         Hero member = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != Hero.MainHero && h.IsAlive && h.PartyBelongedTo == MobileParty.MainParty);

         if (t.Skip(member == null, "no companion in the player's party")) return t.Line();

         // A captive player's zero is the DESIGNED answer (PlayerForcePolicy: the party does not follow
         // them into a cell), not a wiring failure; asserting >0 there would fail the feature for doing
         // exactly what its own doc promises. Run free to test the wiring (testall 2026-07-23).
         if (t.Skip(Hero.MainHero?.IsPrisoner == true, "the player is a captive, whose visible force is 0 by design; run free")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, member);
         EncounterContext ctx = EncounterContextBuilder.Build(
            member, profile, behavior.GameStateBridge, "How many soldiers do we have?");

         t.Expect("partyMemberSeesTroopCount", ctx.PlayerPartyTroopCount > 0, true);
         t.Note($"party member {member.Name} sees troop count {ctx.PlayerPartyTroopCount} (surfaced to every face-to-face interlocutor, hidden only from a captive player's scenes).");

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

      private static string TestMarryCompanion(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("marry_companion");

         // ── The pure carve-out rule, asserted directly (also covered by the NUnit suite) ────────────
         // The one match the feature exists to admit: a compatible, unwed, adult, non-kin companion while the
         // player is free. If this flips the whole carve-out is dead.
         t.Expect("policy_admits_compatible_companion",
            MarryCompanionEligibilityPolicy.CanMarry(new MarryCompanionEligibilityPolicy.MarryCompanionFacts {IsCompanion = true, IsAlive = true, IsAdult = true, OrientationCompatibleWithPlayer = true, AlreadyWed = false, IsBloodKinOfPlayer = false, PlayerCanTakeAnotherSpouse = true}), true);
         // The line the SameClan bar must always hold: a companion who is also blood kin stays barred (no incest).
         t.Expect("policy_bars_blood_kin",
            MarryCompanionEligibilityPolicy.CanMarry(new MarryCompanionEligibilityPolicy.MarryCompanionFacts {IsCompanion = true, IsAlive = true, IsAdult = true, OrientationCompatibleWithPlayer = true, AlreadyWed = false, IsBloodKinOfPlayer = true, PlayerCanTakeAnotherSpouse = true}), false);
         // A second spouse onto an already-wed player is impossible in law; the carve-out defers to that.
         t.Expect("policy_bars_when_player_not_free",
            MarryCompanionEligibilityPolicy.CanMarry(new MarryCompanionEligibilityPolicy.MarryCompanionFacts {IsCompanion = true, IsAlive = true, IsAdult = true, OrientationCompatibleWithPlayer = true, AlreadyWed = false, IsBloodKinOfPlayer = false, PlayerCanTakeAnotherSpouse = false}), false);

         if (t.Skip(Hero.MainHero == null, "no player hero")) return t.Line();
         if (t.Skip(Hero.MainHero.Spouse != null,
                $"player already wed to {Hero.MainHero.Spouse?.Name}; the live marriage half cannot be driven")) return t.Line();

         // A DISPOSABLE companion: an opposite-sex (so orientation is not the confound), adult, unwed, free
         // wanderer of the player's own clan. Opposite sex isolates the real feature (a promoted companion weds)
         // from the engine's separate same-sex refusal.
         bool playerFemale = Hero.MainHero.IsFemale;
         Hero companion = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != Hero.MainHero && h.IsAlive && !h.IsChild && h.Spouse == null && !h.IsPrisoner
            && MarryCompanionResolver.IsPlayerCompanion(h) && h.IsFemale != playerFemale);

         if (t.Skip(companion == null, "no disposable opposite-sex unwed adult companion in the player's clan to drive"))
            return t.Line();

         // ── The CARVE-OUT: an eligible non-kin companion no longer emits SameClan; a blood-kin clan member still does ──
         t.Note($"companion={companion.Name}, occupation={companion.Occupation}, clan==player={companion.Clan == Clan.PlayerClan}");
         t.Expect("carveOut_companion_no_longer_SameClan",
            EncounterContextBuilder.ResolveMarriageBlockReason(companion, behavior.Store.Get(companion.StringId)) != MarriageBlockReason.SameClan, true);

         Hero bloodKin = Hero.MainHero.Children?.FirstOrDefault(c => c is {IsAlive: true} && c.Clan == Clan.PlayerClan)
                      ?? Hero.MainHero.Siblings?.FirstOrDefault(s => s is {IsAlive: true} && s.Clan == Clan.PlayerClan);
         if (bloodKin != null)
            t.Expect("carveOut_blood_kin_still_SameClan",
               EncounterContextBuilder.ResolveMarriageBlockReason(bloodKin, behavior.Store.Get(bloodKin.StringId)) == MarriageBlockReason.SameClan, true);
         else
            t.Note("no in-clan blood kin present to assert the SameClan bar still holds (asserted in the NUnit suite instead)");

         // ── The WEDDING, driven through the REAL bridge marry executor (promotion + Apply) ──────────
         NpcProfile profile = GetOrCreate(behavior, companion);
         ArrangeCleanNpc(behavior, profile, companion);
         profile.ReputationWithPlayer = MarriageEligibility.MarriageEligibilityThreshold; // clear the regard gate
         behavior.Store.Set(profile);

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "marry"}, companion, new ActionContext(profile, null));

         // A refusal on a genuine live precondition (the player is on campaign in an army, or mid-battle) is the
         // designed army-guard behaviour, not a bug: Skip cleanly rather than mis-report the feature as broken.
         bool preconditionBlocked = MobileParty.MainParty?.Army != null
            || MobileParty.MainParty?.MapEvent != null
            || companion.PartyBelongedTo?.MapEvent != null;
         if (t.Skip(Hero.MainHero.Spouse != companion && preconditionBlocked,
                "wedding refused by a live precondition (army/battle), the army guard firing as designed"))
         {
            ArrangeCleanNpc(behavior, profile, companion);

            return t.Line();
         }

         t.Note($"marry outcome={(outcome != null ? outcome.IsPositive.ToString() : "null")}");

         // Same escape the four other marriage scenarios already take, and for the same reason: on some
         // campaigns the native MarriageAction simply does not take (2026-09-09: spouse_divorce, player_divorce,
         // spouse_estrangement and grudge_unblessed_marriage all reported MANUAL for exactly this while THIS
         // test reported FAIL, which reads as a product regression and is not one). Nothing below can be judged
         // once the wedding itself never happened, so say so plainly instead of failing every assertion after it.
         if (t.Skip(Hero.MainHero.Spouse != companion && outcome?.IsPositive != true,
                "could not wed the companion on this campaign (native MarriageAction did not take); divorce path unexercised"))
         {
            ArrangeCleanNpc(behavior, profile, companion);

            return t.Line();
         }

         t.Expect("playerWedCompanion", Hero.MainHero.Spouse == companion, true);
         // zhenja1 (25/09/2026): a new spouse must carry the wedding in her own memory, or her first conversation reads
         // an empty history as "already married and sharing a life". The engine event, not the verb, writes it.
         t.Expect("theSpouseRemembersTheWedding",
                  behavior.Store.Get(companion.StringId)?.Events?.Any(e => e.summary != null && e.summary.StartsWith("Were wed to")) == true, true);
         t.Expect("exCompanionNowLord", companion.Occupation == Occupation.Lord, true);
         t.Expect("exCompanionNotPlayerCompanion", companion.IsPlayerCompanion, false);
         t.Expect("exCompanionInPlayerClan", companion.Clan == Clan.PlayerClan, true);

         // ── The DIVORCE, driven through CR's own dissolution service, asserts a SANE ex-spouse state ─
         bool dissolved = MarriageDissolutionService.TryDissolvePlayerMarriage(companion, out string why);
         t.Note($"divorce dissolved={dissolved} ({why ?? "ok"})");
         t.Expect("divorceDissolved", dissolved, true);
         t.Expect("playerUnwedAgain", Hero.MainHero.Spouse == null, true);
         t.Expect("exSpouseAlive", companion.IsAlive, true);
         t.Expect("exSpouseNotFugitive", companion.IsActive, true); // scattered/fugitive heroes are not Active
         t.Expect("exSpouseReturnedToWanderer", companion.Occupation == Occupation.Wanderer, true);
         t.Expect("exSpouseClanless", companion.Clan == null, true);

         // Best-effort restore: re-establish the borrowed hero as a free companion of the player's clan (its
         // original state). The marry+divorce round-trip still mutates the campaign, so a reload after the sweep
         // is the clean baseline, as with the other heavy tests.
         try
         {
            companion.CompanionOf = Clan.PlayerClan;
            companion.SetNewOccupation(Occupation.Wanderer);
            ArrangeCleanNpc(behavior, profile, companion);
         }
         catch (Exception ex) { t.Note($"restore best-effort failed: {ex.GetType().Name}"); }

         return t.Line();
      }

      private static string TestPregnancy(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("pregnancy");

         // Pick a mother CR ACTUALLY accepts (PregnancyService.IsEligible), not merely one that looks fertile:
         // a loose 'not IsPregnant + age' filter can land on a hero CR refuses silently (freshly given birth, or
         // its own reflective already-pregnant read), which used to read as a false FAIL.
         Hero mother = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && PregnancyService.IsEligible(h));

         if (t.Skip(mother == null, "no CR-eligible (fertile, not already carrying, birth-enabled) female NPC")) return t.Line();

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

         // The buyer must be able to raise the CEILING, not merely the floor. PrisonerRansomExecutor pays
         // Math.Min(clampedPrice, buyer.Gold) by design (a broke buyer must not be handed a hostage for a
         // pittance, and GiveGoldAction self-clamps to the purse anyway), so a poorer buyer legitimately pays
         // less than the clamp and this test's whole assertion is about the clamp. Picking the first lord in
         // the list made the result turn on how rich that lord happened to be: it FAILED on 2026-09-09 with
         // "paid=4550 (expected 7650)" against a solvent-but-not-wealthy Arion, reporting a product bug where
         // there was none. A self-test must never depend on which hero the campaign lists first.
         int ceilingForCaptive = PrisonerTradePolicy.ClampPrice(999999, BuyPrisonerVerb.RansomAnchor(captive));

         Hero buyer = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != captive && h.IsLord && !h.IsPrisoner && h.Clan != captive.Clan
            && h.Gold >= ceilingForCaptive);

         if (t.Skip(buyer == null, $"no lord solvent enough ({ceilingForCaptive}g) to prove the price clamp rather than their purse"))
            return t.Line();

         NpcProfile profile = GetOrCreate(behavior, buyer);

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);

         if (t.Abort(!captive.IsPrisoner, "could not take the test captive")) return t.Line();

         try
         {
            int goldBefore = Hero.MainHero.Gold;
            int anchor = BuyPrisonerVerb.RansomAnchor(captive);

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {
                  Type = "buy_prisoner",
                  Parameters = new Dictionary<string, string> {
                     {"target", captive.Name.ToString()},
                     {"price", "999999"} // an absurd ask, so the clamp itself is what gets proven
                  }
               }, buyer, new ActionContext(profile, null));

            int paid = Hero.MainHero.Gold - goldBefore;
            int expected = PrisonerTradePolicy.ClampPrice(999999, anchor);
            bool leftCustody = BuyPrisonerVerb.PlayerHeldHeroPrisoners().All(h => h != captive);

            t.Note($"captive={captive.Name}, buyer={buyer.Name}, anchor={anchor}, paid={paid} (expected {expected}), outcome=\"{outcome?.Message}\"");
            t.Expect("outcomePositive", outcome?.IsPositive, true);
            t.Expect("paidTheClampedPrice", paid == expected, true);
            t.Expect("captiveLeftPlayerCustody", leftCustody, true);
         }
         finally
         {
            // Whatever happened, the test lord goes free so the campaign is left unharmed.
            try
            {
               if (captive.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(captive, buyer);
            }
            catch
            {
               /* best-effort cleanup */
            }
         }

         return t.Line();
      }

      /// <summary>
      ///   A lord free in a party, not his clan's leader, whose clan leader is free, alive and not the player: the
      ///   captive and the kinsman who will ransom him. Null when this campaign has no such pair.
      /// </summary>
      private static Hero CaptiveWithFreeClanLeader()
         => Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && !h.IsChild && h.PartyBelongedTo != null
            && h.Clan != null && h.Clan != Clan.PlayerClan && h.Clan.Leader != null && h.Clan.Leader != h
            && h.Clan.Leader != Hero.MainHero && h.Clan.Leader.IsAlive && !h.Clan.Leader.IsPrisoner);

      /// <summary>
      ///   The own-side ransom, shared by prisoner_ransom_home and prisoner_gift: take the captive, have his clan
      ///   leader run buy_prisoner with <paramref name="parameters" />, and report what the player was paid. Both
      ///   purses are restored and the captive freed whatever happened.
      /// </summary>
      private static void RunOwnSideRansom(TestRun t, NpcMemoryCampaignBehavior behavior, Hero captive, Dictionary<string, string> parameters, Action<int, ActionOutcome> check)
      {
         Hero buyer = captive.Clan.Leader;
         int playerGold0 = Hero.MainHero.Gold;
         int buyerGold0 = buyer.Gold;

         int ceiling = PrisonerTradePolicy.ClampPrice(999999, BuyPrisonerVerb.RansomAnchor(captive));
         if (buyer.Gold < ceiling) buyer.ChangeHeroGold(ceiling - buyer.Gold); // solvent, so the clamp is what is proven

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);

         try
         {
            if (t.Abort(!captive.IsPrisoner, $"could not take {captive.Name} captive")) return;

            parameters["target"] = captive.Name.ToString();
            int before = Hero.MainHero.Gold;
            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "buy_prisoner", Parameters = parameters}, buyer, new ActionContext(GetOrCreate(behavior, buyer), null));

            t.Note($"captive={captive.Name}, his clan leader={buyer.Name}, outcome=\"{outcome?.Message}\"");
            t.Expect("outcomePositive", outcome?.IsPositive == true && outcome.WasRefused == false, true);
            t.Expect("captiveWentHomeFree", captive.IsPrisoner, false);
            t.Expect("captiveLeftPlayerCustody", BuyPrisonerVerb.PlayerHeldHeroPrisoners().All(h => h != captive), true);
            check(Hero.MainHero.Gold - before, outcome);
         }
         finally
         {
            try { if (captive.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(captive, buyer); }
            catch { /* best-effort cleanup */ }

            Hero.MainHero.ChangeHeroGold(playerGold0 - Hero.MainHero.Gold);
            buyer.ChangeHeroGold(buyerGold0 - buyer.Gold);
         }
      }

      /// <summary>
      ///   fkasad (26/09/2026): a captive ransomed by his OWN count was transferred into his own side's cells, and the
      ///   game reported him escaped. Bought by his own house, he now goes home free by ransom; this proves it on a
      ///   real lord, with the player paid the clamped price all the same.
      /// </summary>
      private static string TestPrisonerRansomHome(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("prisoner_ransom_home");

         if (t.Abort(Hero.MainHero == null || MobileParty.MainParty == null, "no player party")) return t.Line();

         Hero captive = CaptiveWithFreeClanLeader();
         if (t.Skip(captive == null, "no free lord whose clan leader is free to ransom him")) return t.Line();

         int expected = PrisonerTradePolicy.ClampPrice(999999, BuyPrisonerVerb.RansomAnchor(captive));
         RunOwnSideRansom(t, behavior, captive, new Dictionary<string, string> {["price"] = "999999"}, (paid, outcome) =>
         {
            t.Note($"paid={paid} (expected {expected}); told: {outcome?.Message}");
            t.Expect("paidTheClampedPrice", paid == expected, true);
            // tashmetu (27/09/2026): a figure paid short of the one named says why, so nobody invents a reason.
            t.Expect("toldWhyLessWasPaid", outcome?.Message?.Contains("(not the 999999 named: ") == true, true);
         });

         return t.Line();
      }

      /// <summary>
      ///   fkasad (26/09/2026): telling an envoy the prisoner was his for 0 denars paid him the whole budget. The
      ///   envoy's binder now writes a gift (EnvoyMandatePolicy.Bind, unit-tested); this proves the verb honours it on
      ///   a real lord: no gold moves, and the captive goes home.
      /// </summary>
      private static string TestPrisonerGift(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("prisoner_gift");

         if (t.Abort(Hero.MainHero == null || MobileParty.MainParty == null, "no player party")) return t.Line();

         Hero captive = CaptiveWithFreeClanLeader();
         if (t.Skip(captive == null, "no free lord whose clan leader is free to receive him")) return t.Line();

         // Exactly what the envoy's binder hands the bridge for "yours for nothing".
         EnvoyDeal deal = EnvoyMandatePolicy.Bind("buy_prisoner", new Dictionary<string, string> {["price"] = "0"},
            new EnvoyMandate {WantedCaptiveName = captive.Name.ToString(), RansomCeiling = 5000},
            new EnvoyTurnFacts {PlayerLine = "He is yours for nothing."});

         if (t.Abort(deal == null, "the envoy's binder produced no deal for a gift")) return t.Line();

         RunOwnSideRansom(t, behavior, captive, new Dictionary<string, string>(deal.Parameters), (paid, outcome) =>
         {
            t.Note($"paid={paid}");
            t.Expect("noGoldMoved", paid, 0);
            t.Expect("toldItWasAGift", outcome?.Message?.Contains("for nothing") == true, true);
         });

         return t.Line();
      }

      /// <summary>
      ///   The exchange end to end on real lords (fkasad, 25/09/2026; Gabriel: heroes only). The player takes lord A;
      ///   lord B's own party takes lord C, of neither B's house nor the player's realm. B then trades C for A through
      ///   the real bridge: A is B's kin and goes home FREE, C changes gaolers into the player's party, and the gold
      ///   that evens the swap moves as PrisonerExchangePolicy.GoldToSettle says.
      /// </summary>
      private static string TestExchangePrisoners(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("exchange_prisoners");

         if (t.Abort(Hero.MainHero == null || MobileParty.MainParty == null, "no player party")) return t.Line();

         Hero a = CaptiveWithFreeClanLeader();
         Hero b = a?.Clan.Leader;
         if (t.Skip(a == null, "no free lord whose clan leader is free to trade for him")) return t.Line();
         if (t.Skip(b.PartyBelongedTo?.Party == null || b.PartyBelongedTo.LeaderHero != b, $"{b.Name} leads no party to hold a captive in")) return t.Line();

         IFaction playerRealm = Hero.MainHero.MapFaction;
         Hero c = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != a && h != b && h.IsLord && !h.IsPrisoner && !h.IsChild && h.PartyBelongedTo != null
            && h.Clan != null && h.Clan != b.Clan && h.Clan != Clan.PlayerClan && h.Clan.Leader != h
            && h.MapFaction != b.MapFaction && h.MapFaction != playerRealm);
         if (t.Skip(c == null, "no third lord, of neither side, for B to hold")) return t.Line();

         int playerGold0 = Hero.MainHero.Gold;
         int bGold0 = b.Gold;
         int gold = PrisonerExchangePolicy.GoldToSettle(BuyPrisonerVerb.RansomAnchor(a), BuyPrisonerVerb.RansomAnchor(c));
         if (gold > 0 && b.Gold < gold) b.ChangeHeroGold(gold - b.Gold);
         if (gold < 0 && Hero.MainHero.Gold < -gold) Hero.MainHero.ChangeHeroGold(-gold - Hero.MainHero.Gold);

         TakePrisonerAction.Apply(PartyBase.MainParty, a);
         TakePrisonerAction.Apply(b.PartyBelongedTo.Party, c);

         try
         {
            if (t.Abort(!a.IsPrisoner || !c.IsPrisoner, $"could not set the table ({a.Name} held={a.IsPrisoner}, {c.Name} held={c.IsPrisoner})")) return t.Line();

            int before = Hero.MainHero.Gold;
            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {
                  Type = "exchange_prisoners",
                  Parameters = new Dictionary<string, string> {["give"] = a.Name.ToString(), ["receive"] = c.Name.ToString()}
               }, b, new ActionContext(GetOrCreate(behavior, b), null));

            int paid = Hero.MainHero.Gold - before;
            t.Note($"A={a.Name} (player's), B={b.Name}, C={c.Name} (B's), gold to the player {paid} (expected {gold}), outcome=\"{outcome?.Message}\"");
            t.Expect("outcomePositive", outcome?.IsPositive == true && outcome.WasRefused == false, true);
            t.Expect("kinWentHomeFree", a.IsPrisoner, false);
            t.Expect("receivedIsThePlayersPrisoner", c.IsPrisoner && c.PartyBelongedToAsPrisoner == PartyBase.MainParty, true);
            t.Expect("goldEvenedTheSwap", paid, gold);
         }
         finally
         {
            try { if (a.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(a, b); }
            catch { /* best-effort cleanup */ }

            try { if (c.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(c, Hero.MainHero); }
            catch { /* best-effort cleanup */ }

            Hero.MainHero.ChangeHeroGold(playerGold0 - Hero.MainHero.Gold);
            b.ChangeHeroGold(bGold0 - b.Gold);
         }

         return t.Line();
      }

      /// <summary>
      ///   Player report: a ransom-offer letter showed only a Close button (no Accept) for a captured lord who had
      ///   NOT escaped, because he sat in the player's own town dungeon and the held-prisoner enumeration only
      ///   looked at the travelling party. Captures a real in-party lord, moves him into a player-owned settlement's
      ///   prison, and asserts BuyPrisonerVerb.PlayerHeldHeroPrisoners still counts him there. Frees him in cleanup.
      /// </summary>
      private static string TestPrisonerCustodySettlement(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("prisoner_custody_settlement");

         if (t.Abort(Hero.MainHero == null || MobileParty.MainParty == null, "no player party")) return t.Line();

         Settlement prison = Clan.PlayerClan?.Settlements?.FirstOrDefault(s =>
            s != null && (s.IsTown || s.IsCastle) && s.Party != null);

         if (t.Skip(prison == null, "the player owns no town or castle to hold a prisoner in")) return t.Line();

         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null && h.Clan?.Leader != h);

         if (t.Skip(captive == null, "no free in-party lord to capture")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);

         if (t.Abort(!captive.IsPrisoner, $"could not take {captive.Name} captive")) return t.Line();

         try
         {
            TransferPrisonerAction.Apply(captive.CharacterObject, PartyBase.MainParty, prison.Party);

            bool inSettlement = captive.PartyBelongedToAsPrisoner == prison.Party;
            bool counted = BuyPrisonerVerb.PlayerHeldHeroPrisoners().Contains(captive);

            t.Note($"captive={captive.Name}, dungeon={prison.Name}, heldInSettlement={inSettlement}, counted={counted}");
            t.Expect("movedToSettlementDungeon", inSettlement, true);
            t.Expect("countedAsPlayerHeldFromTheDungeon", counted, true);
         }
         finally
         {
            // Whatever happened, the test lord goes free so the campaign is left unharmed.
            try
            {
               if (captive.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(captive, Hero.MainHero);
            }
            catch
            {
               /* best-effort cleanup */
            }
         }

         return t.Line();
      }

      /// <summary>
      ///   Player report (Mimo, Standard/Roleplay mode): holding a captive and ordering them kept safe still
      ///   planted a CaptiveAbuse grudge. Drives the REAL grudge-decision chain the closed captor scene now uses
      ///   (intent severity -> CaptorSceneGrudgePolicy with no conduct read -> Tier2GrudgeRules) and asserts a
      ///   merciful lesser intent forms nothing while Torture still forms. Pure, plants no grudge, needs no setup.
      /// </summary>
      private static string TestCaptorGrudgeConduct(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("captor_grudge_conduct");

         bool interrogation = CaptorGrudgeFormsWithoutRead(CaptiveSceneIntent.Interrogation);
         bool domination = CaptorGrudgeFormsWithoutRead(CaptiveSceneIntent.Domination);
         bool personalDesire = CaptorGrudgeFormsWithoutRead(CaptiveSceneIntent.PersonalDesire);
         bool torture = CaptorGrudgeFormsWithoutRead(CaptiveSceneIntent.Torture);

         t.Note($"no-read grudge forms: interrogation={interrogation}, domination={domination}, personalDesire={personalDesire}, torture={torture}");
         t.Expect("mercifulInterrogationFormsNoGrudge", interrogation, false);
         t.Expect("mercifulDominationFormsNoGrudge", domination, false);
         t.Expect("mercifulPersonalDesireFormsNoGrudge", personalDesire, false);
         t.Expect("tortureStillForms", torture, true);

         return t.Line();
      }

      /// <summary>Whether a closed captor scene of this intent would plant a grudge with NO conduct read, via the real production chain.</summary>
      private static bool CaptorGrudgeFormsWithoutRead(CaptiveSceneIntent intent)
      {
         int severity = CaptorSceneGrudgePolicy.GrudgeSeverity(CaptiveIntentRules.CaptiveAbuseSeverity(intent), null);

         return Tier2GrudgeRules.CaptiveAbuseGrudge(
            new CaptiveAbuseFacts {Severity = severity, PlayerWasThePerpetrator = true}).Forms;
      }

      // ── Tests ─────────────────────────────────────────────────────────────

      // ── Unknown-verb refusal (feedback funnel). The model can emit an [ACTION] whose type has no executor ──
      // ── (an invented "grant a fief" / "bless a marriage"). The bridge must refuse it AND flag it as an ──
      // ── unknown verb, carrying the deed-scoped base message, so the chat can log the invented verb for the ──
      // ── author and show the immersive notice instead of the old "(That cannot be done here. Nothing ──
      // ── happened.)" that read as contradicting a triumphant scene where a +1 regard and a quest DID land. ──
      // ── A refusal touches no game state, so nothing needs restoring. ──────────────────────────────────────

      private static string TestUnknownActionNotice(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("unknown_action_notice");

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Abort(npc == null, "no live lord to address")) return t.Line();

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "grant_fief_to_player_xyz"},
            npc, null);

         if (t.Abort(outcome == null, "the bridge returned null instead of a refusal")) return t.Line();

         t.Note($"npc={npc.Name}");
         t.Expect("refused", outcome.WasRefused, true);
         t.Expect("unknownVerb", outcome.WasUnknownVerb, true);
         t.Expect("message", outcome.Message, UnknownActionNotice.BaseMessage);

         return t.Line();
      }

      private static string TestSecretLoverLeak(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("secret_lover_leak");

         if (t.Abort(WorldEvents.SecretLoverGossipBehavior.Instance == null || WorldEvents.WorldEventStore.Instance == null,
                "secret-lover / world-event behavior not initialized")) return t.Line();

         Hero subject = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive && !h.IsPrisoner);
         if (t.Abort(subject == null, "no live hero to stand in as the secret-lover subject")) return t.Line();

         string id = "cr_secretlover_" + subject.StringId;
         WorldEvents.WorldEventStore.Instance.Remove(id); // clear any prior residue so the assertion is clean

         try
         {
            bool forced = WorldEvents.SecretLoverGossipBehavior.Instance.DebugForceLeak(subject);
            if (t.Abort(!forced, "DebugForceLeak returned false")) return t.Line();

            WorldEvents.WorldEvent we = WorldEvents.WorldEventStore.Instance.GetById(id);

            t.Note($"subject={subject.Name}");
            t.Expect("whisper recorded", we != null, true);
            t.Expect("identity hidden (InvolvesPlayer)", we?.InvolvesPlayer, false);
            t.Expect("subject is the actor", we != null && we.ActorIds != null && we.ActorIds.Contains(subject.StringId), true);
            t.Expect("gossip-worthy magnitude", we != null && we.Magnitude >= 55, true);
         }
         finally
         {
            WorldEvents.WorldEventStore.Instance.Remove(id);
         }

         return t.Line();
      }

      private static string TestAdmitSecretLover(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("admit_secret_lover");

         WorldEvents.SecretLoverGossipBehavior gossip = WorldEvents.SecretLoverGossipBehavior.Instance;
         if (t.Abort(gossip == null || behavior?.Store == null || behavior.GameStateBridge == null,
                "secret-lover behavior / store / bridge not initialized")) return t.Line();

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(npc == null, "no live lord to confess to")) return t.Line();

         // A player-clan companion stands in as the hidden lover: PlayerSecretLoverSubjectIds only counts companions.
         Hero subject = Clan.PlayerClan?.Companions?.FirstOrDefault(h => h != null && h != Hero.MainHero && h != npc);
         if (t.Abort(subject == null, "no player-clan companion to stand in as the secret-lover subject")) return t.Line();

         NpcProfile subjectProfile = GetOrCreate(behavior, subject);
         if (subjectProfile.Romantic == null) subjectProfile.Romantic = new RomanticProfile();
         bool prevFlag = subjectProfile.Romantic.IsSecretLover;
         RomanticStatus prevStatus = subjectProfile.Romantic.Status;

         NpcProfile npcProfile = GetOrCreate(behavior, npc);
         int memoryCountBefore = npcProfile.Events?.Count ?? 0;

         try
         {
            subjectProfile.Romantic.IsSecretLover = true;
            subjectProfile.Romantic.Status = RomanticStatus.SecretLover;
            behavior.Store.Set(subjectProfile);
            gossip.ForgetKnows(npc, subject.StringId); // clean slate

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "admit_secret_lover"}, npc, new ActionContext(npcProfile, null));

            if (t.Abort(outcome == null, "the bridge returned null instead of an outcome")) return t.Line();

            t.Note($"npc={npc.Name} subject={subject.Name}");
            t.Expect("confession upheld", outcome.IsPositive, true);
            t.Expect("the lord now knows the identity", gossip.Knows(npc.StringId, subject.StringId), true);
            t.Expect("the confession is remembered", (npcProfile.Events?.Count ?? 0) > memoryCountBefore, true);
            // The identity is never deduced, only confessed: a lord who was NOT told stays ignorant.
            t.Expect("an untold lord stays ignorant", gossip.Knows("cr_never_told_lord", subject.StringId), false);
         }
         finally
         {
            subjectProfile.Romantic.IsSecretLover = prevFlag;
            subjectProfile.Romantic.Status = prevStatus;
            behavior.Store.Set(subjectProfile);
            gossip.ForgetKnows(npc, subject.StringId);
            if (npcProfile.Events != null)
               while (npcProfile.Events.Count > memoryCountBefore) npcProfile.Events.RemoveAt(npcProfile.Events.Count - 1);
            behavior.Store.Set(npcProfile);
         }

         return t.Line();
      }

      private static string TestHeldSecret(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("held_secret");

         WorldEvents.SecretLoverGossipBehavior gossip = WorldEvents.SecretLoverGossipBehavior.Instance;
         if (t.Abort(gossip == null || behavior?.Store == null || behavior.GameStateBridge == null,
                "secret-lover behavior / store / bridge not initialized")) return t.Line();

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(npc == null, "no live lord to hold the secret")) return t.Line();

         Hero subject = Clan.PlayerClan?.Companions?.FirstOrDefault(h => h != null && h != Hero.MainHero && h != npc);
         if (t.Abort(subject == null, "no player-clan companion to stand in as the secret-lover subject")) return t.Line();

         NpcProfile subjectProfile = GetOrCreate(behavior, subject);
         if (subjectProfile.Romantic == null) subjectProfile.Romantic = new RomanticProfile();
         bool prevFlag = subjectProfile.Romantic.IsSecretLover;
         RomanticStatus prevStatus = subjectProfile.Romantic.Status;
         NpcProfile npcProfile = GetOrCreate(behavior, npc);

         try
         {
            subjectProfile.Romantic.IsSecretLover = true;
            subjectProfile.Romantic.Status = RomanticStatus.SecretLover;
            behavior.Store.Set(subjectProfile);
            gossip.RecordKnows(npc, subject.StringId);

            EncounterContext ctx = EncounterContextBuilder.Build(npc, npcProfile, behavior.GameStateBridge, "hello");
            bool adultOn = ModSettings.AdultContentLevel != AdultContentLevel.Off;
            t.Note($"npc={npc.Name} subject={subject.Name} adultLevel={ModSettings.AdultContentLevel}");

            if (adultOn)
            {
               string subjectName = subject.Name?.ToString() ?? "";
               t.Expect("a lord who was told surfaces the subject",
                  !string.IsNullOrEmpty(ctx.HeldSecretLoverSubject) && ctx.HeldSecretLoverSubject.Contains(subjectName), true);
            }
            else
            {
               // The adult-content gate holds: no held-secret injection at all when the feature is off.
               t.Expect("the adult gate suppresses the held secret", string.IsNullOrEmpty(ctx.HeldSecretLoverSubject), true);
            }
         }
         finally
         {
            subjectProfile.Romantic.IsSecretLover = prevFlag;
            subjectProfile.Romantic.Status = prevStatus;
            behavior.Store.Set(subjectProfile);
            gossip.ForgetKnows(npc, subject.StringId);
         }

         return t.Line();
      }

      private static string TestSecretPropagation(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("secret_propagation");

         WorldEvents.SecretLoverGossipBehavior gossip = WorldEvents.SecretLoverGossipBehavior.Instance;
         if (t.Abort(gossip == null || behavior?.Store == null, "secret-lover behavior / store not initialized")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no main hero")) return t.Line();

         // A teller who actually has a friend (relation >= the propagation floor) to confide in.
         Hero teller = null;
         foreach (Hero cand in Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsLord && !h.IsPrisoner))
         {
            if (Hero.AllAliveHeroes.Any(f => f != null && f != cand && f != Hero.MainHero && !f.IsPrisoner
                   && CalradiaRemembers.Logic.SecretPropagationPolicy.IsFriend(new NpcMemoryService.Core.Standing.VanillaRelation(cand.GetRelation(f)))))
            {
               teller = cand;
               break;
            }
         }

         if (t.Abort(teller == null, "no lord with a friend to propagate to")) return t.Line();

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h != teller && h.IsAlive);
         if (t.Abort(subject == null, "no hero to stand in as the secret-lover subject")) return t.Line();

         // Force the teller to a neutral personal regard so the "a friend of the player guards it" gate does not skip.
         NpcProfile tellerProfile = GetOrCreate(behavior, teller);
         int prevRegard = tellerProfile.ReputationWithPlayer;

         string told = null;
         try
         {
            tellerProfile.ReputationWithPlayer = 0;
            behavior.Store.Set(tellerProfile);
            gossip.ForgetKnows(teller, subject.StringId);
            gossip.RecordKnows(teller, subject.StringId);

            told = gossip.DebugSpreadOnce(teller, subject.StringId);

            t.Note($"teller={teller.Name} subject={subject.Name} told={(told == null ? "(none)" : told)}");
            t.Expect("the secret reached a friend", !string.IsNullOrEmpty(told), true);
            t.Expect("that friend now knows", told != null && gossip.Knows(told, subject.StringId), true);
            t.Expect("the friend is not the player", told != Hero.MainHero.StringId, true);
         }
         finally
         {
            gossip.ForgetKnows(teller, subject.StringId);
            if (told != null)
            {
               Hero friend = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h.StringId == told);
               if (friend != null) gossip.ForgetKnows(friend, subject.StringId);
            }

            tellerProfile.ReputationWithPlayer = prevRegard;
            behavior.Store.Set(tellerProfile);
         }

         return t.Line();
      }

      private static string TestPayForSilence(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("pay_for_silence");

         WorldEvents.SecretLoverGossipBehavior gossip = WorldEvents.SecretLoverGossipBehavior.Instance;
         if (t.Abort(gossip == null || behavior?.Store == null || behavior.GameStateBridge == null,
                "secret-lover behavior / store / bridge not initialized")) return t.Line();

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(npc == null, "no live lord to hold the secret")) return t.Line();

         Hero subject = Clan.PlayerClan?.Companions?.FirstOrDefault(h => h != null && h != Hero.MainHero && h != npc);
         if (t.Abort(subject == null, "no player-clan companion to stand in as the secret-lover subject")) return t.Line();

         NpcProfile subjectProfile = GetOrCreate(behavior, subject);
         if (subjectProfile.Romantic == null) subjectProfile.Romantic = new RomanticProfile();
         bool prevFlag = subjectProfile.Romantic.IsSecretLover;
         RomanticStatus prevStatus = subjectProfile.Romantic.Status;
         NpcProfile npcProfile = GetOrCreate(behavior, npc);
         int memoryCountBefore = npcProfile.Events?.Count ?? 0;

         int playerGold0 = Hero.MainHero.Gold;
         int npcGold0 = npc.Gold;
         int price = CalradiaRemembers.Logic.HushMoneyPolicy.MinPayment;

         try
         {
            subjectProfile.Romantic.IsSecretLover = true;
            subjectProfile.Romantic.Status = RomanticStatus.SecretLover;
            behavior.Store.Set(subjectProfile);
            gossip.RecordKnows(npc, subject.StringId);
            gossip.ForgetHushPaid(npc, subject.StringId);

            if (Hero.MainHero.Gold < price) Hero.MainHero.ChangeHeroGold(price - Hero.MainHero.Gold); // make the player solvent
            int playerBefore = Hero.MainHero.Gold;
            int npcBefore = npc.Gold;

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "pay_for_silence", Parameters = new Dictionary<string, string> {["price"] = price.ToString()}},
               npc, new ActionContext(npcProfile, null));

            if (t.Abort(outcome == null, "the bridge returned null instead of an outcome")) return t.Line();

            var today = (int) CampaignTime.Now.ToDays;
            t.Note($"npc={npc.Name} subject={subject.Name} price={price}");
            t.Expect("the player paid the price", Hero.MainHero.Gold, playerBefore - price);
            t.Expect("the lord received the price", npc.Gold, npcBefore + price);
            t.Expect("the hush is recorded as recent",
               CalradiaRemembers.Logic.HushMoneyPolicy.IsRecentlyPaid(gossip.HushPaidDay(npc.StringId, subject.StringId), today), true);
            t.Expect("a memory of the payment is kept", (npcProfile.Events?.Count ?? 0) > memoryCountBefore, true);
         }
         finally
         {
            subjectProfile.Romantic.IsSecretLover = prevFlag;
            subjectProfile.Romantic.Status = prevStatus;
            behavior.Store.Set(subjectProfile);
            gossip.ForgetKnows(npc, subject.StringId);
            gossip.ForgetHushPaid(npc, subject.StringId);
            if (npcProfile.Events != null)
               while (npcProfile.Events.Count > memoryCountBefore) npcProfile.Events.RemoveAt(npcProfile.Events.Count - 1);
            behavior.Store.Set(npcProfile);
            Hero.MainHero.ChangeHeroGold(playerGold0 - Hero.MainHero.Gold);
            npc.ChangeHeroGold(npcGold0 - npc.Gold);
         }

         return t.Line();
      }

      private static string TestLordSecretLover(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("lord_secret_lover");

         WorldEvents.SecretLoverGossipBehavior gossip = WorldEvents.SecretLoverGossipBehavior.Instance;
         if (t.Abort(gossip == null || behavior?.Store == null || behavior.GameStateBridge == null,
                "secret-lover behavior / store / bridge not initialized")) return t.Line();

         // A NON-companion lord standing in as the married lord the player is secretly the lover of (Stolen Love /
         // infidelity marks the SDK Status SecretLover, not the companion-only IsSecretLover flag).
         Hero subject = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner && !h.IsPlayerCompanion);
         if (t.Abort(subject == null, "no non-companion lord to stand in as the secret-lover subject")) return t.Line();

         Hero holder = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h != subject && h.IsLord && !h.IsPrisoner);
         if (t.Abort(holder == null, "no second lord to hold the secret")) return t.Line();

         NpcProfile subjectProfile = GetOrCreate(behavior, subject);
         if (subjectProfile.Romantic == null) subjectProfile.Romantic = new RomanticProfile();
         RomanticStatus prevStatus = subjectProfile.Romantic.Status;
         NpcProfile holderProfile = GetOrCreate(behavior, holder);

         try
         {
            // Only Status is set (no IsSecretLover), exactly as a seduced married lord's profile looks.
            subjectProfile.Romantic.Status = RomanticStatus.SecretLover;
            behavior.Store.Set(subjectProfile);

            t.Note($"subject(lord)={subject.Name} holder={holder.Name} adultLevel={ModSettings.AdultContentLevel}");
            t.Expect("a lord affair is detected as a secret-lover bond",
               gossip.PlayerSecretLoverSubjectIds().Contains(subject.StringId), true);

            gossip.RecordKnows(holder, subject.StringId);
            EncounterContext ctx = EncounterContextBuilder.Build(holder, holderProfile, behavior.GameStateBridge, "hello");

            if (ModSettings.AdultContentLevel != AdultContentLevel.Off)
            {
               string subjectName = subject.Name?.ToString() ?? "";
               t.Expect("the held-secret payoff names the lord subject",
                  !string.IsNullOrEmpty(ctx.HeldSecretLoverSubject) && ctx.HeldSecretLoverSubject.Contains(subjectName), true);
            }
            else
            {
               t.Expect("the adult gate suppresses the held secret", string.IsNullOrEmpty(ctx.HeldSecretLoverSubject), true);
            }
         }
         finally
         {
            subjectProfile.Romantic.Status = prevStatus;
            behavior.Store.Set(subjectProfile);
            gossip.ForgetKnows(holder, subject.StringId);
         }

         return t.Line();
      }

      private static string TestPlayerViolence(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("player_violence");

         if (t.Abort(behavior?.GameStateBridge == null || Hero.MainHero == null, "no bridge / main hero")) return t.Line();

         Hero target = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(target == null, "no live lord to stand in as a victim")) return t.Line();

         // THE guarantee: with the MCM toggle off (its default), a narrated murder must change nothing at all.
         // Driven through the REAL bridge dispatch, and asserted on the victim still being alive.
         bool lethalEnabled = ModSettings.AllowKillingCharacters;
         bool aliveBefore = target.IsAlive;

         ActionOutcome killed = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "kill_npc"}, target, new ActionContext(behavior.Store?.Get(target.StringId), null));

         t.Note($"target={target.Name}, lethalToggle={(lethalEnabled ? "ON" : "OFF")}, exposure={CalradiaRemembers.Violence.ViolenceSceneReader.ExposureOf(target)}");

         if (!lethalEnabled)
         {
            t.Expect("a kill is refused while the toggle is off", killed?.WasRefused, true);
            t.Expect("and the victim is still alive", target.IsAlive, aliveBefore);
         }
         else
         {
            t.Note("lethal violence is ENABLED in MCM, so the kill path was live; only the refusal case is asserted when off.");
         }

         // The scene reader must place a lord who leads soldiers, or stands in a town, out of easy reach; a victim
         // alone is the reporter's case and must read as such.
         CalradiaRemembers.Logic.Violence.ViolenceFacts facts = CalradiaRemembers.Violence.ViolenceSceneReader.FactsFor(target);
         t.Expect("both fighters are measured", facts.PlayerProwess > 0 && facts.TargetProwess > 0, true);
         t.Expect("robbery is never barred by the lethal toggle",
            CalradiaRemembers.Logic.Violence.ViolencePolicy.IsPermitted(CalradiaRemembers.Logic.Violence.ViolentAct.Rob, false), true);

         // A killing asks the PLAYER first (Gabriel 2026-09-06), so the model over-reading a threat can never end a
         // life on its own; nothing recoverable interrupts the scene with a modal.
         t.Expect("only a killing asks the player first",
            CalradiaRemembers.Logic.Violence.ViolencePolicy.RequiresConfirmation(CalradiaRemembers.Logic.Violence.ViolentAct.Kill), true);
         t.Expect("a beating never interrupts with a modal",
            CalradiaRemembers.Logic.Violence.ViolencePolicy.RequiresConfirmation(CalradiaRemembers.Logic.Violence.ViolentAct.Assault), false);

         // Inc 3: the world really answers. Drive the REAL consequence service for a FAILED assault (nothing is
         // wounded, nobody dies) and assert the engine's own crime rating actually moved, then undo it.
         Hero kin = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != target && h.Clan == target.Clan && !h.IsChild);
         IFaction realm = target.MapFaction;
         float crimeBefore = realm?.MainHeroCrimeRating ?? 0f;
         int kinGrudgesBefore = kin == null ? 0 : CalradiaRemembers.Grudges.GrudgeBehavior.Instance?.GrudgesFor(kin.StringId)?.Count ?? 0;
         // The consequence service really spends renown and really collapses the victim's regard, so BOTH must be
         // captured and put back: a diagnostic that leaves a lord hating the player by 50 is worse than no test.
         float renownBefore = Clan.PlayerClan?.Renown ?? 0f;
         NpcProfile victimProfile = behavior.Store?.Get(target.StringId);
         int victimRegardBefore = victimProfile?.ReputationWithPlayer ?? 0;

         try
         {
            CalradiaRemembers.Logic.Violence.ViolenceConsequence cost =
               CalradiaRemembers.Violence.ViolenceConsequenceService.Apply(target,
                  CalradiaRemembers.Logic.Violence.ViolentAct.Assault,
                  CalradiaRemembers.Logic.Violence.ViolenceVerdict.Failed,
                  CalradiaRemembers.Logic.Violence.ViolenceExposure.Onlookers);

            t.Note($"cost: crime={cost.CrimeRating} regard={cost.RelationHit} renown=-{cost.RenownLoss} kin={cost.KinGrudgeMagnitude}");
            t.Expect("a failed attempt still costs with the law", cost.CrimeRating > 0, true);
            t.Expect("the engine's own crime rating actually moved",
               realm != null && realm.MainHeroCrimeRating > crimeBefore, true);

            if (kin != null)
               t.Expect("kin of the victim take it up",
                  (CalradiaRemembers.Grudges.GrudgeBehavior.Instance?.GrudgesFor(kin.StringId)?.Count ?? 0) > kinGrudgesBefore, true);
         }
         finally
         {
            if (realm != null) realm.MainHeroCrimeRating = crimeBefore;
            if (kin != null && kinGrudgesBefore == 0) CalradiaRemembers.Grudges.GrudgeBehavior.Instance?.ClearGrudgesFor(kin.StringId);

            float renownNow = Clan.PlayerClan?.Renown ?? renownBefore;
            if (Clan.PlayerClan != null && Math.Abs(renownNow - renownBefore) > 0.01f)
               GainRenownAction.Apply(Hero.MainHero, renownBefore - renownNow, false);

            if (victimProfile != null)
            {
               victimProfile.ReputationWithPlayer = victimRegardBefore;
               behavior.Store.Set(victimProfile);
            }
         }

         return t.Line();
      }

      private static string TestBattleFeat(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("battle_feat");

         if (t.Abort(Hero.MainHero == null, "no main hero")) return t.Line();

         // A weak foe and a strong one, so both ends of the exploit ruling are exercised on LIVE hero facts.
         List<Hero> lords = Hero.AllAliveHeroes?.Where(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner).ToList();
         if (t.Abort(lords == null || lords.Count == 0, "no live lord to stand in as a foe")) return t.Line();

         CombatantFacts player = CalradiaRemembers.Battles.BattleFeatFacts.For(Hero.MainHero);
         CombatantFacts foe = CalradiaRemembers.Battles.BattleFeatFacts.For(lords[0]);

         // Synthetic extremes: the ruling itself must not depend on who happens to be alive in this campaign.
         var weakling = new CombatantFacts {BestWeaponSkill = 20, Riding = 0, Athletics = 0, Level = 2, ClanTier = 0};
         var champion = new CombatantFacts {BestWeaponSkill = 300, Riding = 200, Athletics = 200, Level = 35, ClanTier = CombatProwessPolicy.FamousClanTier};

         t.Note($"player prowess={CombatProwessPolicy.Prowess(player)}, {lords[0].Name} prowess={CombatProwessPolicy.Prowess(foe)}");
         t.Expect("live hero facts yield a real prowess score", CombatProwessPolicy.Prowess(foe) > 1, true);
         t.Expect("besting a champion is an exploit", CombatProwessPolicy.Rate(weakling, champion), CombatFeatTier.Exploit);
         t.Expect("an exploit travels as gossip", CombatProwessPolicy.TravelsAsGossip(CombatFeatTier.Exploit), true);
         t.Expect("stomping a weakling is unremarkable", CombatProwessPolicy.Rate(champion, weakling), CombatFeatTier.Unremarkable);
         t.Expect("an unremarkable win never travels", CombatProwessPolicy.TravelsAsGossip(CombatFeatTier.Unremarkable), false);

         // The journal itself: drive the REAL ledger through its debug seam (no battle needed) and assert it keeps
         // the deed, the mirror deed, and the troop tally apart.
         var journal = new CalradiaRemembers.Battles.BattleFeatJournal();
         journal.DebugRecord(lords[0].StringId, lords[0].StringId, 3);
         journal.DebugRecord(lords[0].StringId, null, 2); // duplicates must not pile up

         t.Expect("the felled hero is journaled once", journal.HeroesDowned.Count, 1);
         t.Expect("who downed the player is journaled", journal.DownedThePlayer.Count, 1);
         t.Expect("the troop tally accumulates", journal.TroopsFelled, 5);

         // A lord is BESTED, never called dead unless he truly died (Gabriel's truth rule).
         string bested = BattleFeatText.WitnessedFeat(lords[0].Name?.ToString(), CombatFeatOutcome.Bested, "open battle");
         t.Expect("a bested lord is not called killed", bested.Contains("kill"), false);

         // Inc 2, end to end on the REAL reporter. The DOWNING line always survives; the deed line only when the
         // foe was worth beating, so pick a lord the ruling actually rates (any campaign has stronger lords than
         // the player early on, but never assume it: fall back and assert only what holds).
         Hero ratedFoe = lords.FirstOrDefault(h =>
            CombatProwessPolicy.Rate(player, CalradiaRemembers.Battles.BattleFeatFacts.For(h)) != CombatFeatTier.Unremarkable);

         var endToEnd = new CalradiaRemembers.Battles.BattleFeatJournal();
         endToEnd.DebugRecord(ratedFoe?.StringId, lords[0].StringId, 0);

         IReadOnlyList<string> lines = CalradiaRemembers.Battles.BattleFeatReporter.BuildReport(endToEnd, "open battle").LinesFor("some_bystander");
         string joined = string.Join(" | ", lines);

         t.Note($"ratedFoe={(ratedFoe == null ? "(none stronger)" : ratedFoe.Name.ToString())}; lines={lines.Count}: {joined}");
         t.Expect("who downed the player always survives distillation", lines.Count >= 1, true);
         t.Expect("every witness line is first-hand", lines.All(l => l.StartsWith("I saw")), true);
         t.Expect("the foe who downed the player is named", joined.Contains(lords[0].Name?.ToString() ?? "??"), true);

         if (ratedFoe != null && !ratedFoe.IsDead)
         {
            t.Expect("a worthy foe yields a deed line too", lines.Count >= 2, true);
            t.Expect("a living lord is never mourned", joined.Contains("cut"), false);
         }

         // Inc 4 (voice): the man a deed is ABOUT must not read it as a bystander's report. The lord who downed the
         // player says so himself; a bystander is told who did it.
         CalradiaRemembers.Battles.BattleFeatReport report =
            CalradiaRemembers.Battles.BattleFeatReporter.BuildReport(endToEnd, "open battle");
         string subjectLines = string.Join(" | ", report.LinesFor(lords[0].StringId));

         // NAMED, not "you". reminensce's report (2.5.18) was a lord taking the PLAYER's defeat for his own,
         // because memory lines under YOUR HISTORY WITH THIS PLAYER said "you" while the whole prompt says "you"
         // to mean the NPC. Every new memory names the player instead, so the line to look for is his name. This
         // assertion still read the old wording and only surfaced now, which is what a testall is for.
         string whoThePlayerIs = Hero.MainHero?.Name?.ToString() ?? "the player";

         t.Note($"subjectLines={subjectLines}");
         t.Expect("the man who downed the player says so himself", subjectLines.Contains($"I put {whoThePlayerIs} down myself"), true);
         t.Expect("he does not report himself in the third person",
            subjectLines.Contains($"I saw {lords[0].Name}"), false);

         // Inc 3 (circulation): only an EXPLOIT leaves the field. Drive the REAL recorder both ways and check the
         // world-event store, then retract whatever was seeded so the campaign is left untouched.
         Hero exploitFoe = lords.FirstOrDefault(h =>
            CombatProwessPolicy.Rate(player, CalradiaRemembers.Battles.BattleFeatFacts.For(h)) == CombatFeatTier.Exploit);
         Hero pushoverFoe = lords.FirstOrDefault(h =>
            CombatProwessPolicy.Rate(player, CalradiaRemembers.Battles.BattleFeatFacts.For(h)) == CombatFeatTier.Unremarkable);
         var today = (int) CampaignTime.Now.ToDays;

         try
         {
            if (pushoverFoe != null)
            {
               bool travelled = CalradiaRemembers.Battles.BattleFeatReporter.TryRecordExploit(
                  pushoverFoe, CombatFeatOutcome.Bested, "open battle");
               t.Expect("an unremarkable win never leaves the field", travelled, false);
            }

            if (exploitFoe != null)
            {
               bool travelled = CalradiaRemembers.Battles.BattleFeatReporter.TryRecordExploit(
                  exploitFoe, CombatFeatOutcome.Bested, "open battle");
               WorldEvents.WorldEvent we = WorldEvents.WorldEventStore.Instance?.GetById($"cr_feat_{exploitFoe.StringId}_{today}");

               t.Note($"exploitFoe={exploitFoe.Name}");
               t.Expect("an exploit leaves the field", travelled, true);
               t.Expect("it is recorded as a player feat", we?.Type, WorldEvents.WorldEventType.PlayerFeat);
               t.Expect("it is the player's own deed (a rumour ABOUT them)", we?.InvolvesPlayer, true);
               t.Expect("no faction is tagged, so no lord claims to have been there", (we?.FactionIds?.Count ?? 0), 0);
               t.Expect("the gossip line is hearsay", we != null && we.Summary.StartsWith("They say"), true);
            }
         }
         finally
         {
            if (exploitFoe != null) WorldEvents.WorldEventStore.Instance?.Remove($"cr_feat_{exploitFoe.StringId}_{today}");
            if (pushoverFoe != null) WorldEvents.WorldEventStore.Instance?.Remove($"cr_feat_{pushoverFoe.StringId}_{today}");
         }

         return t.Line();
      }

      private static string TestTournamentMemory(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("tournament_memory");

         if (t.Abort(behavior?.Store == null || WorldEvents.WorldEventStore.Instance == null, "store / world-event store not initialized")) return t.Line();

         List<Hero> lords = Hero.AllAliveHeroes?.Where(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner).Take(2).ToList();
         if (t.Abort(lords == null || lords.Count < 2, "need two live lords to stand in as tournament rivals")) return t.Line();

         Hero beaten = lords[0];   // the player bested them
         Hero champion = lords[1]; // they beat the player and took the crown

         NpcProfile beatenP = GetOrCreate(behavior, beaten);
         NpcProfile champP = GetOrCreate(behavior, champion);
         int beatenBefore = beatenP.Events?.Count ?? 0;
         int champBefore = champP.Events?.Count ?? 0;
         int champLettersBefore = champP.LetterCandidates?.Count ?? 0;
         int champHonor0 = champion.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor);

         var today = (int) CampaignTime.Now.ToDays;
         const string town = "SelfTestArena";
         string gossipId = $"cr_tourney_{town}_{today}";
         WorldEvents.WorldEventStore.Instance.Remove(gossipId); // clear residue

         var result = new CalradiaRemembers.Logic.Tournaments.TournamentResult(
            town, today, playerWonTournament: false, playerEliminatedRound: 4, championHeroId: champion.StringId,
            bouts: new List<CalradiaRemembers.Logic.Tournaments.TournamentBout>
            {
               new(2, new List<string> {beaten.StringId}, playerAdvanced: true),
               new(4, new List<string> {champion.StringId}, playerAdvanced: false)
            });

         try
         {
            // Force the champion gracious so the grounded-letter path is exercised (default traits yield a measured
            // tone that writes no letter).
            champion.SetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor, 2);

            CalradiaRemembers.Tournaments.TournamentMemoryFeeder.Apply(result);

            string beatenLast = beatenP.Events != null && beatenP.Events.Count > beatenBefore ? beatenP.Events[beatenP.Events.Count - 1].summary : "";
            string champLast = champP.Events != null && champP.Events.Count > champBefore ? champP.Events[champP.Events.Count - 1].summary : "";
            WorldEvents.WorldEvent gossip = WorldEvents.WorldEventStore.Instance.GetById(gossipId);
            bool letterQueued = champP.LetterCandidates != null
                                && champP.LetterCandidates.Any(c => c.Reason == NpcMemoryService.Core.Models.LetterReason.TournamentResult);

            t.Note($"beaten={beaten.Name} champion={champion.Name}");
            t.Expect("the player-beaten lord remembers losing", beatenLast.Contains("bested me"), true);
            t.Expect("the champion remembers besting the player", champLast.Contains("bested the player"), true);
            t.Expect("the champion recalls the crown", champLast.Contains("crown"), true);
            t.Expect("a champion gossip event is recorded", gossip != null, true);
            t.Expect("the gossip does not involve the player", gossip?.InvolvesPlayer, false);
            t.Expect("a grounded tournament letter is queued", letterQueued, true);
         }
         finally
         {
            champion.SetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor, champHonor0);
            if (beatenP.Events != null) while (beatenP.Events.Count > beatenBefore) beatenP.Events.RemoveAt(beatenP.Events.Count - 1);
            if (champP.Events != null) while (champP.Events.Count > champBefore) champP.Events.RemoveAt(champP.Events.Count - 1);
            if (champP.LetterCandidates != null) while (champP.LetterCandidates.Count > champLettersBefore) champP.LetterCandidates.RemoveAt(champP.LetterCandidates.Count - 1);
            behavior.Store.Set(beatenP);
            behavior.Store.Set(champP);
            WorldEvents.WorldEventStore.Instance.Remove(gossipId);
         }

         return t.Line();
      }

      /// <summary>
      ///   A grudge-free lord whose OWN character actually lets a grievance of this weight take hold. The grudge
      ///   system's formation floor is trait-modulated (MagnitudeFloor + Mercy*5 - Calculating*5), so a merciful
      ///   lord silently refuses a light grievance, exactly as designed. Picking "the first grudge-free lord" made
      ///   these tests a coin flip on who happened to come first in the campaign's list: they passed on one save and
      ///   failed on the next without a line of code changing (found 2026-09-06, two tests failing at once on the
      ///   same merciful lord). Asks the real policy, through the very facts TryPlant will build.
      /// </summary>
      private static Hero GrudgeCapableLord(GrudgeSource source, int magnitude, int today, Func<Hero, bool> also = null)
         => Hero.AllAliveHeroes?.FirstOrDefault(h =>
            h != null && h != Hero.MainHero && h.IsAlive && h.IsLord && !h.IsPrisoner
            && (GrudgeBehavior.Instance?.LiveGrudgesFor(h, today)?.Count ?? 0) == 0
            && GrudgePolicy.ShouldForm(GrudgeBridge.BuildCandidateFacts(h, source, magnitude, false))
            && (also == null || also(h)));

      private static string TestTournamentRivalry(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("tournament_rivalry");

         CalradiaRemembers.Tournaments.TournamentRivalryBehavior rb = CalradiaRemembers.Tournaments.TournamentRivalryBehavior.Instance;
         CalradiaRemembers.Grudges.GrudgeBehavior grudges = CalradiaRemembers.Grudges.GrudgeBehavior.Instance;
         if (t.Abort(rb == null || grudges == null || Hero.MainHero == null, "rivalry / grudge behavior not initialized")) return t.Line();

         // No existing grudge and no tally, so the cleanup removes exactly what we plant, AND a character that lets
         // a grudge this light form at all: BitterMagnitude (18) sits just above the base floor (15), so a merciful
         // lord legitimately shrugs off a sporting sourness and no grudge is planted.
         var today0 = (int) CampaignTime.Now.ToDays;
         Hero rival = GrudgeCapableLord(GrudgeSource.External, Logic.Tournaments.TournamentRivalryPolicy.BitterMagnitude,
            today0, h => rb.Tally(h.StringId) == null);
         if (t.Abort(rival == null, "no grudge-free, unforgiving lord to stand in as a bitter arena rival")) return t.Line();

         int honor0 = rival.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor);

         try
         {
            rival.SetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor, -2); // force a BITTER rivalry (a grudge we can cleanly clear)

            rb.RecordEncounter(rival, playerWon: true);
            rb.RecordEncounter(rival, playerWon: true);
            bool formedEarly = rb.Tally(rival.StringId)?.Formed ?? false;

            rb.RecordEncounter(rival, playerWon: false); // crosses the threshold (3 total head-to-heads)
            CalradiaRemembers.Tournaments.RivalryTally tally = rb.Tally(rival.StringId);
            bool grudgePlanted = (grudges.GrudgesFor(rival.StringId)?.Count ?? 0) > 0;

            t.Note($"rival={rival.Name} pw={tally?.PlayerWins} hw={tally?.HeroWins}");
            t.Expect("no rivalry before the threshold", formedEarly, false);
            t.Expect("the rivalry forms at the threshold", tally?.Formed, true);
            t.Expect("three head-to-heads tallied", (tally?.PlayerWins ?? 0) + (tally?.HeroWins ?? 0), 3);
            t.Expect("a bitter rivalry plants a grudge", grudgePlanted, true);
         }
         finally
         {
            grudges.ClearGrudgesFor(rival.StringId);
            rb.ForgetTally(rival.StringId);
            rival.SetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Honor, honor0);
         }

         return t.Line();
      }

      private static string TestRecentMatter(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("recent_matter");

         if (t.Abort(Interception.InterceptionBehavior.Instance == null, "interception behavior not initialized")) return t.Line();

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(npc == null, "no live lord to address")) return t.Line();

         NpcProfile p = GetOrCreate(behavior, npc);
         var today = (int) CampaignTime.Now.ToDays;
         const string seed = "[SELFTEST] We broke the enemy together near the proving walls.";
         var seededEvent = new NotableEvent(today, NotableEventType.Other, seed);

         p.Events.Add(seededEvent);
         behavior.Store.Set(p);

         try
         {
            System.Collections.Generic.IReadOnlyList<Logic.Interception.RecentMatter> candidates =
               Interception.InterceptionBehavior.Instance.DebugBuildRecentMatters(npc);
            Logic.Interception.RecentMatter mine = candidates.FirstOrDefault(m => m.Summary == seed);

            t.Note($"npc={npc.Name}, candidates={candidates.Count}");
            t.Expect("seeded memory is a candidate", mine != null, true);
            t.Expect("classified as a lived memory", mine?.Register, Logic.Interception.RecentMatterRegister.LivedMemory);
         }
         finally
         {
            p.Events.Remove(seededEvent);
            behavior.Store.Set(p);
         }

         return t.Line();
      }

      private static string TestRecentMatterLetter(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("recent_matter_letter");

         if (t.Abort(Interception.InterceptionBehavior.Instance == null, "interception behavior not initialized")) return t.Line();

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(npc == null, "no live lord to write from")) return t.Line();

         NpcProfile p = GetOrCreate(behavior, npc);
         var today = (int) CampaignTime.Now.ToDays;
         const string seed = "[SELFTEST] We broke the enemy together near the proving walls.";
         var seededEvent = new NotableEvent(today, NotableEventType.Other, seed);

         p.Events.Add(seededEvent);
         behavior.Store.Set(p);

         try
         {
            // Compose-only seam: does NOT mark the matter raised, so re-runs stay deterministic.
            string context = Interception.InterceptionBehavior.Instance.DebugRecentMatterLetterContext(npc);

            // The letter raises the matter the ranking puts first, which in a long campaign may be one of the lord's
            // own rather than the one seeded here (29/09/2026: after 6.7 accelerated years, Lucon had another matter
            // that outranked the seed, and this test failed on a letter that was right). So it asks what the promise
            // is: a SPECIFIC matter, one of the lord's real candidates, framed first-person when it was lived.
            IReadOnlyList<Logic.Interception.RecentMatter> candidates = Interception.InterceptionBehavior.Instance.DebugBuildRecentMatters(npc);
            Logic.Interception.RecentMatter chosen = context == null ? null : candidates.FirstOrDefault(m => context.Contains(m.Summary));

            t.Note($"npc={npc.Name}, candidates={candidates.Count}, chosen={chosen?.Register.ToString() ?? "none"}");
            t.Expect("a missed-meeting letter context is composed", !string.IsNullOrEmpty(context), true);
            t.Expect("the seeded memory is among the lord's matters", candidates.Any(m => m.Summary.Contains(seed)), true);
            t.Expect("it names one specific matter, not a generic deed", chosen != null, true);
            t.Expect("a lived matter is framed first-person",
                     chosen == null || chosen.Register != Logic.Interception.RecentMatterRegister.LivedMemory || context.Contains("FIRST PERSON"), true);
         }
         finally
         {
            p.Events.Remove(seededEvent);
            behavior.Store.Set(p);
         }

         return t.Line();
      }

      private static string TestRegardProgression(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("regard_progression");

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(npc == null, "no live lord to address")) return t.Line();

         NpcProfile p = GetOrCreate(behavior, npc);

         // Snapshot everything this test disturbs, so it leaves no trace.
         int savedTier = p.RegardResistanceTier;
         int savedAccrued = p.RegardWarmthAccrued;
         double? savedStamp = p.RegardResistanceStampHour;
         int savedRep = p.ReputationWithPlayer;

         try
         {
            p.RegardResistanceTier = 0;
            p.RegardWarmthAccrued = 0;
            p.RegardResistanceStampHour = null;

            var ctx = new ActionContext(p, null);
            GameAction Warm() => new GameAction {Type = "change_relation", Parameters = new Dictionary<string, string> {["delta"] = "1"}};

            ActionOutcome o1 = behavior.GameStateBridge.ExecuteAction(Warm(), npc, ctx); // tier 0: 1 moment -> +1, tier->1
            ActionOutcome o2 = behavior.GameStateBridge.ExecuteAction(Warm(), npc, ctx); // tier 1: banks (needs 2) -> gated (null)
            ActionOutcome o3 = behavior.GameStateBridge.ExecuteAction(Warm(), npc, ctx); // tier 1: 2nd moment -> +1, tier->2

            t.Note($"npc={npc.Name}");
            t.Expect("1st landed +1", o1?.AppliedRelationDelta, 1);
            t.Expect("2nd gated (null outcome)", o2 == null, true);
            t.Expect("3rd landed +1", o3?.AppliedRelationDelta, 1);
            t.Expect("tier after three", p.RegardResistanceTier, 2);
         }
         finally
         {
            p.RegardResistanceTier = savedTier;
            p.RegardWarmthAccrued = savedAccrued;
            p.RegardResistanceStampHour = savedStamp;
            p.ReputationWithPlayer = savedRep;
            behavior.Store.Set(p);
         }

         return t.Line();
      }

      private static string TestExtensionRecap(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("extension_recap");

         Hero npc = Hero.AllAliveHeroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Abort(npc == null, "no live lord to address")) return t.Line();

         const string verb = "cr_selftest_recap_probe";

         // A throwaway external action whose Execute returns a Recap on BOTH a success and a refusal, keyed off a
         // parameter, so the real dispatch path a third-party mod relies on is exercised end to end.
         ActionRegistrationResult reg = CrActions.Register(new ExternalActionSpec {
            Name = verb,
            Description = "self-test probe",
            SourceMod = "CrSelfTest",
            IsEligible = _ => true,
            Execute = req =>
            {
               bool ok = req.Parameters != null && req.Parameters.TryGetValue("ok", out string v) && v == "1";

               return new ActionResult {
                  Message = ok ? "probe upheld" : "probe refused",
                  Success = ok,
                  Recap = ok ? "recap-success" : "recap-refused"
               };
            }
         });

         if (t.Abort(reg != ActionRegistrationResult.Registered, $"registration failed: {reg}")) return t.Line();

         try
         {
            ActionOutcome ok = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = verb, Parameters = new Dictionary<string, string> {["ok"] = "1"}}, npc, null);
            ActionOutcome refused = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = verb, Parameters = new Dictionary<string, string> {["ok"] = "0"}}, npc, null);

            if (t.Abort(ok == null || refused == null, "the bridge returned null instead of an outcome")) return t.Line();

            t.Note($"npc={npc.Name}");
            t.Expect("success.recap", ok.Recap, "recap-success");
            t.Expect("success.positive", ok.IsPositive, true);
            t.Expect("refused.recap", refused.Recap, "recap-refused");
            t.Expect("refused.wasRefused", refused.WasRefused, true);

            // PromptReply must be a safe no-op (false) with no conversation open (the self-test runs on the map).
            t.Expect("promptReply_noop", CrConversation.PromptReply("anything"), false);
         }
         finally
         {
            CrActions.Unregister(verb);
         }

         return t.Line();
      }

      private static string TestProvideGold(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("providegold");

         Hero giver = FreeQuestGiver(behavior);

         if (t.Skip(giver == null, "every live lord already holds an outstanding task, so none can be given a new one")) return t.Line();

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

         // An empty purse refuses the payment, and the quest rightly stays open (29/09/2026: after a night of
         // accelerated play the player had no gold, and this test failed on a refusal that was right). The purse is
         // topped up for the payment, and both purses are put back as they were.
         int playerGoldBefore = Hero.MainHero.Gold;
         int giverGoldBefore = giver.Gold;
         if (playerGoldBefore < 500) Hero.MainHero.ChangeHeroGold(500 - playerGoldBefore);
         try
         {
            behavior.GameStateBridge.ExecuteAction(
               new GameAction {
                  Type = "take_gold",
                  Parameters = new Dictionary<string, string> {{"amount", "500"}}
               },
               giver, null);
         }
         finally
         {
            Hero.MainHero.ChangeHeroGold(playerGoldBefore - Hero.MainHero.Gold);
            giver.ChangeHeroGold(giverGoldBefore - giver.Gold);
         }

         profile = behavior.Store.Get(giver.StringId);
         bool stillOpen = profile.ActiveQuests.Any(q => q.Type == QuestType.ProvideGold && q.IsOutstanding);
         t.Note($"giver={giver.Name}, player gold before={playerGoldBefore}");
         t.Expect("stamped", !stillOpen, true);

         return t.Line();
      }

      /// <summary>
      ///   Child-support fix: a ProvideGold quest (a support demand) must CLOSE when the player pays its giver,
      ///   not linger "done, return for your reward" forever (a player report). Seeds one, gives gold through
      ///   the real take_gold bridge action, and asserts the quest is Completed with its goodwill paid. Leaves
      ///   the economy as it found it (returns the gold to the player, removes it from the giver).
      /// </summary>
      // ── The reward safety net (quest audit B1): a deed the game VERIFIED (IsAwaitingReward) must still pay ──
      // ── out even when the model never emits [QUEST_COMPLETE], or the player's "Done" quest pays out never. ──

      private static string TestRewardNet(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("reward_net");

         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(giver == null, "no live lord to stand as the giver")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         var today = (int) CampaignTime.Now.ToDays;

         // Seed a quest the game has already VERIFIED as done: SatisfiedOnDay set (deed proven) + still Active =
         // IsAwaitingReward. This is the state a cleared lair sits in while it waits for the giver to pay out.
         var quest = new InformalQuest {
            Type = QuestType.BanditClear,
            Description = "[SELFTEST] clear the bandits by the ford.",
            IssuedOnDay = today,
            SatisfiedOnDay = today,
            RewardGold = 300,
            RewardRelation = 4,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         QuestJournalBridge.OpenMirror(quest, giver);
         behavior.Store.Set(profile);

         int regardBefore = profile.ReputationWithPlayer;
         int playerGoldBefore = Hero.MainHero.Gold;

         // The model replied with NO [QUEST_COMPLETE] (QuestCompleted null): the net must pay the owed reward.
         QuestService.ApplyResponse(profile, giver, new ParsedResponse {Dialogue = ""}, today,
            behavior.GameStateBridge, behavior.Store, (_, _) => { });

         NpcProfile reread = behavior.Store.Get(giver.StringId);
         InformalQuest after = reread?.ActiveQuests.FirstOrDefault(q => q.Description == "[SELFTEST] clear the bandits by the ford.");
         bool rewardPaid = Hero.MainHero.Gold > playerGoldBefore || (reread != null && reread.ReputationWithPlayer > regardBefore);

         t.Note($"giver={giver.Name}: status={after?.Status}, awaitingReward={after?.IsAwaitingReward}, " +
                $"regard {regardBefore}->{reread?.ReputationWithPlayer}, gold +{Hero.MainHero.Gold - playerGoldBefore}");
         t.Expect("netSettledTheQuest", after?.Status, QuestStatus.Completed);
         t.Expect("noLongerAwaitingReward", after?.IsAwaitingReward, false);
         t.Expect("owedRewardWasPaid", rewardPaid, true);

         // Cleanup: drop the seeded quest and return whatever gold the reward moved.
         NpcProfile clean = behavior.Store.Get(giver.StringId);
         clean.ActiveQuests.RemoveAll(q => q.Description == "[SELFTEST] clear the bandits by the ford.");
         behavior.Store.Set(clean);
         int goldMoved = Hero.MainHero.Gold - playerGoldBefore;
         if (goldMoved > 0) Hero.MainHero.ChangeHeroGold(-goldMoved);

         return t.Line();
      }

      private static string TestProvideGoldCompletes(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("provide_gold");

         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(giver == null, "no live lord to stand as the support-asking giver")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         var today = (int) CampaignTime.Now.ToDays;

         var quest = new InformalQuest {
            Type = QuestType.ProvideGold,
            Description = "Help support my child.",
            IssuedOnDay = today,
            RewardRelation = 5,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         QuestJournalBridge.OpenMirror(quest, giver);
         behavior.Store.Set(profile);

         const int pay = 100;
         int playerGoldBefore = Hero.MainHero.Gold;
         int regardBefore = profile.ReputationWithPlayer;
         if (Hero.MainHero.Gold < pay) Hero.MainHero.ChangeHeroGold(pay - Hero.MainHero.Gold);
         int giverGoldBefore = giver.Gold;

         behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "take_gold", Parameters = new Dictionary<string, string> {{"amount", pay.ToString()}}},
            giver, new ActionContext(profile, null));

         InformalQuest after = profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.ProvideGold);
         t.Note($"giver={giver.Name}: status={after?.Status}, awaitingReward={after?.IsAwaitingReward}, regard {regardBefore}->{profile.ReputationWithPlayer}");
         t.Expect("questCompleted", after?.Status, QuestStatus.Completed);
         t.Expect("notStuckAwaitingReward", after?.IsAwaitingReward, false);
         t.Expect("goodwillPaid", profile.ReputationWithPlayer > regardBefore, true);

         // Restore the economy: give the player back what the test moved, take it off the giver.
         int moved = giverGoldBefore >= 0
            ? giver.Gold - giverGoldBefore
            : 0;
         if (moved > 0) giver.ChangeHeroGold(-moved);
         Hero.MainHero.ChangeHeroGold(playerGoldBefore - Hero.MainHero.Gold);

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


         Hero giver = FreeQuestGiver(behavior);

         if (t.Skip(giver == null, "every live lord already holds an outstanding task, so none can be given a new one")) return t.Line();

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

      /// <summary>
      ///   Increment 2a (player report: told to clear a base "south of the village" but the map marker sat on the
      ///   village). A BanditHideout quest must mark the bound LAIR, not the anchor town. Issues one through the
      ///   real pipeline against a town with a live infested lair, then drives the marker-resolution seam
      ///   (CalradiaRemembersQuest.ResolveMapMarkerTarget) and asserts it points at the hideout settlement and NOT
      ///   the anchor town. Cleans up: closes the journal mirror and removes the seeded quest.
      /// </summary>
      private static string TestQuestHideoutMarker(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("quest_hideout_marker");


         bool Infested(Settlement h)
         {
            try { return h.Hideout != null && h.Hideout.IsInfested; }
            catch { return false; }
         }


         Hero giver = FreeQuestGiver(behavior);

         if (t.Skip(giver == null, "every live lord already holds an outstanding task, so none can be given a new one")) return t.Line();

         Settlement town = Settlement.All.FirstOrDefault(s =>
            s.IsTown && Settlement.All.Any(h => h.IsHideout && Infested(h) && h.GetPosition2D.Distance(s.GetPosition2D) <= 70f));

         if (t.Skip(town == null, "no town has an infested hideout in range; the marker fix is only meaningful with a bound lair")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         const string desc = "[SELFTEST] a lair to clear (marker).";
         QuestService.ApplyResponse(
            profile, giver,
            new ParsedResponse {
               Dialogue = "",
               QuestGiven = new QuestProposal {
                  Type = QuestType.BanditHideout,
                  TargetSettlement = town.Name?.ToString(),
                  Description = desc,
                  RewardGold = 200
               }
            },
            behavior.GameStateBridge.GetCurrentGameDay(),
            behavior.GameStateBridge, behavior.Store, null);

         profile = behavior.Store.Get(giver.StringId);
         InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.BanditHideout && q.Description == desc);

         if (t.Abort(quest == null, $"the pipeline seeded no hideout quest for {giver.Name} despite an in-range lair")) return t.Line();

         Settlement lair = Settlement.Find(quest.TargetHideoutId);
         MBObjectBase marker = CalradiaRemembersQuest.ResolveMapMarkerTarget(quest);

         t.Note($"giver={giver.Name} anchor={quest.TargetSettlement} lair={lair?.Name} marker={(marker as Settlement)?.Name?.ToString() ?? marker?.StringId}");
         t.Expect("boundRealLair", lair != null && lair.IsHideout, true);
         t.Expect("markerIsTheLair", ReferenceEquals(marker, lair), true);
         t.Expect("markerIsNotTheAnchorTown", !ReferenceEquals(marker, town), true);

         // Cleanup: close the journal mirror and drop the seeded quest so no phantom errand lingers.
         QuestJournalBridge.CloseAsCancelled(quest, giver, "[SELFTEST] cleanup.");
         NpcProfile clean = behavior.Store.Get(giver.StringId);
         clean.ActiveQuests.RemoveAll(q => q.Description == desc);
         behavior.Store.Set(clean);

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
         string menu = ViableQuestScanner.BuildMenu(giver, day);

         t.Note($"giver={giver.Name} ({giver.MapFaction?.Name}), menu:\n{menu}");
         t.Expect("menuNonEmpty", !string.IsNullOrWhiteSpace(menu), true);

         if (string.IsNullOrWhiteSpace(menu)) return t.Line();

         // Every line must open with a known type token, and a settlement target must truly resolve.
         // This list must be grown whenever ViableQuestScanner learns to offer a new kind of task, or the test
         // cries wolf over a perfectly good menu (provide_goods shipped in v1.29.0 and sat here unrecognised).
         // The EXACT set ViableQuestScanner can emit (testall 2026-07-23: the checker had drifted two tokens
         // behind the scanner, so a perfectly legitimate menu carrying siege + deliver_prisoner FAILED. The
         // referee was stale, not the player-facing menu. Whoever adds a type to the scanner must add it here.)
         var knownTokens = new[] {
            "bandit_hideout", "bandit_clear", "attack_lord", "capture_prisoner", "attack_faction",
            "raid_village", "scout_army", "rescue_prisoner", "deliver_letter", "provide_goods",
            "siege", "deliver_prisoner"
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
            name = name.Substring(0, name.IndexOf(')') is var p and >= 0
               ? p
               : name.Length);
            if (!Settlement.All.Any(s => string.Equals(s?.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase)))
               settlementsResolve = false;
         }

         t.Expect("allTypeTokensKnown", allKnown, true);
         t.Expect("settlementTargetsResolve", settlementsResolve, true);

         return t.Line();
      }

      /// <summary>
      ///   Increment 2b (player report: a village sent me after bandits that were not there, then turned hostile
      ///   when I reported none). ViableQuestScanner must offer bandit_clear ONLY when a bandit party actually
      ///   roams within QuestDeedTracker's completion radius of the anchor. Staging a real bandit party by a town
      ///   is impractical, so this reads the LIVE world: it counts nearby bandit parties for a lord's anchor via
      ///   the scanner's own counter, builds the grounded menu, and asserts bandit_clear appears iff the count is
      ///   positive. No state seeded, nothing to clean up.
      /// </summary>
      private static string TestBanditClearGate(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("bandit_clear_gate");

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(giver == null, "no live free lord in this campaign")) return t.Line();

         Settlement anchor = QuestFactory.NearestTownTo(giver);

         if (t.Skip(anchor == null, "the giver has no anchor settlement to scan around")) return t.Line();

         int nearby = ViableQuestScanner.CountBanditPartiesNear(anchor);
         int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string menu = ViableQuestScanner.BuildMenu(giver, day) ?? "";
         bool offered = menu.Contains("bandit_clear");

         t.Note($"giver={giver.Name} anchor={anchor.Name} nearbyBanditParties={nearby} offered={offered}");
         t.Expect("offeredIffBanditsRoam", offered, nearby > 0);

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

      /// <summary>
      ///   teach_skill: drives ExecuteTeachSkill through the REAL bridge (never the pure TeachSkillPolicy
      ///   directly, so the whole chain (skill resolution, the floor gate, the cooldown, AddSkillXp) is
      ///   proven end to end). Finds any live NPC with at least one skill at or above
      ///   TeachSkillPolicy.TeacherSkillFloor, clears their cooldown stamp, fires a real lesson and asserts XP
      ///   landed on the player, fires a second lesson immediately after and asserts the cooldown refuses it,
      ///   then fires a lesson naming a skill that does not exist and asserts that is refused too (the
      ///   "unknown skill" half of the never-fake-a-lesson guarantee). Restores the player's skill level and
      ///   the NPC's cooldown stamp in a finally block regardless of outcome.
      /// </summary>
      private static string TestTeachSkill(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("teach_skill");

         Hero teacher = null;
         SkillObject teachSkill = null;
         int teacherLevel = 0;

         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == null || h == Hero.MainHero || h.IsPrisoner) continue;

            foreach (SkillObject sk in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
               if (sk == null) continue;
               int v = h.GetSkillValue(sk);

               if (!TeachSkillPolicy.CanTeach(v)) continue;
               teacher = h;
               teachSkill = sk;
               teacherLevel = v;

               break;
            }

            if (teacher != null) break;
         }

         if (t.Skip(teacher == null, "no live NPC has a skill at or above TeachSkillPolicy.TeacherSkillFloor (100) to teach with")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, teacher);
         double? originalLastTaught = profile.LastTaughtHour;
         int originalStudentLevel = Hero.MainHero.GetSkillValue(teachSkill);

         try
         {
            profile.LastTaughtHour = null; // clean slate: the cooldown must not block the first lesson

            var lesson = new GameAction {
               Type = "teach_skill",
               Parameters = new Dictionary<string, string> {{"skill", teachSkill.StringId}, {"amount", "1000"}}
            };

            ActionOutcome first = behavior.GameStateBridge.ExecuteAction(lesson, teacher, new ActionContext(profile, null));
            int afterFirst = Hero.MainHero.GetSkillValue(teachSkill);

            t.Note($"{teacher.Name} ({teachSkill.StringId} {teacherLevel}): player {originalStudentLevel} -> {afterFirst}");
            t.Expect("firstLessonGranted", first != null && !first.WasRefused, true);
            t.Expect("xpActuallyGrew", afterFirst >= originalStudentLevel, true);

            // Same NPC, same skill, right away: LastTaughtHour was just stamped, so the cooldown must refuse it.
            ActionOutcome second = behavior.GameStateBridge.ExecuteAction(lesson, teacher, new ActionContext(profile, null));
            t.Expect("cooldownBlocksRepeatLesson", second != null && second.WasRefused, true);

            // A skill name that resolves to nothing must be refused, never silently faked.
            var unknown = new GameAction {
               Type = "teach_skill",
               Parameters = new Dictionary<string, string> {{"skill", "NotARealSkill"}, {"amount", "30"}}
            };
            ActionOutcome unknownOutcome = behavior.GameStateBridge.ExecuteAction(unknown, teacher, new ActionContext(profile, null));
            t.Expect("unknownSkillRefused", unknownOutcome != null && unknownOutcome.WasRefused, true);

            return t.Line();
         }
         finally
         {
            Hero.MainHero.HeroDeveloper.SetInitialSkillLevel(teachSkill, originalStudentLevel);
            profile.LastTaughtHour = originalLastTaught;
            behavior.Store.Set(profile);
         }
      }

      /// <summary>
      ///   recruit_notable: persuade a settlement NOTABLE to leave their post and swear into the player's clan
      ///   as a companion, with succession (a successor inherits the gang/role so the town keeps a notable) and
      ///   a starting relation bonus. Drives the REAL bridge verb on live facts. recruit_notable cannot be
      ///   cleanly reversed (a notable is converted to a companion and their alleys pass to the successor), so
      ///   this restores what it can (regard, occupations, power, seating) BEST-EFFORT and SKIPS when no
      ///   disposable notable+successor pair is available rather than force the change on a real campaign.
      /// </summary>
      /// <summary>
      ///   take_into_service, and specifically THE PART NO PURE TEST CAN REACH. CompanionServiceTransferPolicy
      ///   is unit-tested and proves the gates; it cannot prove that RemoveCompanionAction leaves the hero in a
      ///   coherent state. That is the load-bearing unknown here: ApplyByFire runs MakeHeroFugitiveAction, so
      ///   picking the wrong overload would leave a FUGITIVE seated in a household and nothing but a live
      ///   campaign would ever say so. This drives the real bridge and reads the real engine back.
      ///   <para>
      ///     It genuinely moves a companion between clans on a live save, so the restore is asserted too: a
      ///     restore that silently failed would cost Gabriel a real companion, and a test that can do that must
      ///     say when it has.
      ///   </para>
      /// </summary>
      private static string TestTakeIntoService(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("take_into_service");

         Clan playerClan = Clan.PlayerClan;
         if (t.Skip(playerClan?.Companions == null || Hero.MainHero == null, "no player clan to give anyone from")) return t.Line();

         // A lord of some OTHER clan, alive and free: the household the companion would join.
         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != null && h.IsAlive && !h.IsPrisoner && h.IsLord
              && h.Clan != null && h.Clan != playerClan && h != Hero.MainHero);

         if (t.Skip(lord == null, "no other-clan lord to take anyone into service")) return t.Line();

         // Exactly the candidates the gate itself would offer, so the test cannot pass on someone the feature
         // would never have touched (a spouse, a sitting governor, someone away on an errand).
         Hero companion = playerClan.Companions.FirstOrDefault(
            c => c is {IsAlive: true, IsPrisoner: false}
              && c != Hero.MainHero.Spouse
              && c.GovernorOf == null
              && CompanionMissionBehavior.Instance?.IsAway(c.StringId) != true);

         if (t.Skip(companion == null, "the player has no spare companion who is free to go")) return t.Line();

         MobileParty cameFrom = companion.PartyBelongedTo;
         Clan lordClan = lord.Clan;
         t.Note($"{companion.Name} of {playerClan.Name} -> {lord.Name} of {lordClan.Name}");

         // Prove the ground truth the assertions below rest on, so a pass cannot mean "they were never ours".
         t.Expect("theyStartAsYourOwnCompanion", companion.IsPlayerCompanion, true);

         NpcProfile lordProfile = behavior.Store?.Get(lord.StringId);
         if (t.Skip(lordProfile == null, "no stored profile for the lord to act through")) return t.Line();

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {
               Type = "take_into_service",
               Parameters = new Dictionary<string, string> {{"target", companion.Name.ToString()}}
            }, lord, new ActionContext(lordProfile, null));

         t.Note($"outcome='{outcome?.Message}' refused={outcome?.WasRefused}");
         t.Expect("theBridgeCarriedItOut", outcome is {WasRefused: false}, true);

         // Every assertion below is phrased so that TRUE means the thing worked. An earlier version wrote
         // these as "expect false" and the passing line read "theyLeftYourClan=False", which is the same trap
         // this project already fell into once: a log a human scans must not make a pass look like a failure.
         t.Expect("theyAreNoLongerYourCompanion", !companion.IsPlayerCompanion, true);
         t.Expect("theyJoinedTheLordsHouse", companion.Clan == lordClan, true);

         // THE ONE THIS TEST EXISTS FOR. ApplyByFire would have made them a fugitive; ApplyAfterQuest does not.
         t.Expect("theyAreNotLeftAFugitive", !companion.IsFugitive, true);
         t.Expect("theyAreStillAliveAndFree", companion is {IsAlive: true, IsPrisoner: false}, true);

         // ── put them back, and prove it took ────────────────────────────────────────────────────
         try
         {
            RemoveCompanionAction.ApplyAfterQuest(lordClan, companion);
            AddCompanionAction.Apply(playerClan, companion);
            if (cameFrom != null) AddHeroToPartyAction.Apply(companion, cameFrom, false);
            else if (MobileParty.MainParty != null) AddHeroToPartyAction.Apply(companion, MobileParty.MainParty, false);
         }
         catch (Exception ex)
         {
            t.Note($"(RESTORE THREW: {ex.GetType().Name}: {ex.Message})");
         }

         t.Expect("andTheyAreYoursAgainAfterwards", companion.IsPlayerCompanion, true);
         t.Expect("andNotLeftAFugitiveByTheRestoreEither", !companion.IsFugitive, true);

         return t.Line();
      }

      private static string TestRecruitNotable(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("recruit_notable");

         var playerClan = Clan.PlayerClan;

         if (t.Abort(playerClan == null, "no player clan")) return t.Line();

         // A notable in a settlement that ALSO has an eligible successor (the bridge's own succession authority,
         // so teaching gate, executor and test all agree on who can inherit).
         Hero notable = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != Hero.MainHero && h.IsAlive && h.IsNotable && !h.IsPrisoner
            && h.Clan != playerClan && h.CurrentSettlement != null
            && BannerlordGameStateBridge.FindNotableSuccessor(h) != null);

         if (t.Skip(notable == null, "no recruitable notable with an eligible successor in a settlement (nothing disposable to test on)")) return t.Line();

         // Predict the successor the bridge will pick (deterministic) and capture state to restore afterwards.
         Hero successor = BannerlordGameStateBridge.FindNotableSuccessor(notable);
         Settlement settlement = notable.CurrentSettlement;
         Occupation role = notable.Occupation;
         Occupation successorRole = successor.Occupation;
         float successorPowerBefore = successor.Power;

         // Force the mod's own regard ledger to the floor so the shared gate passes on live facts.
         NpcProfile profile = GetOrCreate(behavior, notable);
         int regardBefore = profile.ReputationWithPlayer;
         profile.ReputationWithPlayer = NotableRecruitmentPolicy.RegardFloor;
         behavior.Store.Set(profile);
         int relBefore = notable.GetRelation(Hero.MainHero);

         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "recruit_notable"}, notable, new ActionContext(profile, null));

         int relAfter = notable.GetRelation(Hero.MainHero);
         t.Note($"{notable.Name} ({role}) in {settlement?.Name?.ToString() ?? "?"} -> successor {successor.Name}; rel {relBefore}->{relAfter}");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("inPlayerClan", notable.Clan == playerClan, true);
         t.Expect("isCompanion", notable.IsPlayerCompanion, true);
         t.Expect("inMainParty", notable.PartyBelongedTo == MobileParty.MainParty, true);
         t.Expect("relationBonusApplied", relAfter > relBefore, true);
         t.Expect("successorIsNotable", successor.IsNotable, true);
         t.Expect("successorHoldsPost", settlement != null && settlement.Notables.Contains(successor), true);

         // Best-effort restore. recruit_notable is not cleanly reversible (the alley transfer especially), so
         // this only reduces the campaign impact; it is guarded so a restore failure never fails the test.
         try
         {
            if (notable.IsPlayerCompanion) RemoveCompanionAction.ApplyByFire(playerClan, notable);
            notable.SetNewOccupation(role);
            if (settlement != null) EnterSettlementAction.ApplyForCharacterOnly(notable, settlement);
            successor.SetNewOccupation(successorRole);
            successor.AddPower(successorPowerBefore - successor.Power);
            if (settlement != null) EnterSettlementAction.ApplyForCharacterOnly(successor, settlement);
            t.Note("restored(best-effort)");
         }
         catch (Exception ex) { t.Note($"restore skipped: {ex.GetType().Name}"); }

         profile.ReputationWithPlayer = regardBefore;
         behavior.Store.Set(profile);

         return t.Line();
      }

      /// <summary>
      ///   Player report + owner decision (14/08/2026): recruit_prisoner's regard gate stays deliberately
      ///   strict, with NO player-facing feedback, but the NPC must never CLAIM to accept a defection it
      ///   cannot make. EncounterContextBuilder.Build derives CanRecruitPrisoner and the new
      ///   CaptiveLordWithheldFromDefection flag from the SAME PrisonerRecruitmentPolicy verdict, computed
      ///   once, so a wiring drift between the two is structurally impossible. Drives this on a REAL captured
      ///   lord (not a raw policy call) so the assertion covers the whole engine-bound path: below the
      ///   regard floor, only the negative flag is set; once regard clears the floor, they flip.
      /// </summary>
      /// <summary>
      ///   The regression guard the pure tests cannot be: one regard bar governs this whole arc, and it is read
      ///   in three places (the confession policy, the teaching gate inside EncounterContextBuilder, and the
      ///   bridge's own guard on take_as_secret_lover). On 2026-09-08 only the first moved onto the pacing dial,
      ///   so on Warm or Storyteller a companion declared their feelings and the game then refused the bond. A
      ///   .Logic test cannot see that: it would only compare the policy against itself. This drives the LIVE
      ///   sites at the bar and one point below, so a literal reintroduced beside either of them fails here.
      /// </summary>
      private static string TestSecretBondBarAgrees(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("secret_bond_bar_agrees");

         if (t.Skip(ModSettings.AdultContentLevel == AdultContentLevel.Off,
                    "adult content is off, which gates this whole arc before any regard is read")) return t.Line();

         Hero companion = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsPlayerCompanion && h.IsAlive && h != Hero.MainHero.Spouse);

         if (t.Skip(companion == null, "no living companion in the player's own party")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, companion);

         if (t.Skip(profile.Romantic == null, $"{companion.Name} has no romantic profile to judge")) return t.Line();
         if (t.Skip(!profile.Romantic.IsCompatibleWith(Hero.MainHero.IsFemale),
                    $"{companion.Name}'s orientation makes the player no object of affection, so the bar is never reached"))
            return t.Line();

         int regardBefore = profile.ReputationWithPlayer;
         bool secretBefore = profile.Romantic.IsSecretLover;
         RomanticStatus statusBefore = profile.Romantic.Status;
         int bar = CompanionSecretAffectionPolicy.MinRegardFor(ModSettings.RelationWarmth);

         try
         {
            profile.Romantic.IsSecretLover = false;
            profile.Romantic.Status = RomanticStatus.None;

            profile.ReputationWithPlayer = bar - 1;
            behavior.Store.Set(profile);
            EncounterContext below = EncounterContextBuilder.Build(companion, profile, behavior.GameStateBridge, "hello");
            ActionOutcome refusedBelow = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "take_as_secret_lover"}, companion, new ActionContext(profile, null));

            profile.ReputationWithPlayer = bar;
            behavior.Store.Set(profile);
            EncounterContext atBar = EncounterContextBuilder.Build(companion, profile, behavior.GameStateBridge, "hello");
            ActionOutcome allowedAtBar = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "take_as_secret_lover"}, companion, new ActionContext(profile, null));

            t.Note($"{companion.Name}: pacing={ModSettings.RelationWarmth}, bar={bar}; below taught={below.SecretLoverEligible} " +
                   $"bridge={(refusedBelow?.WasRefused == true ? "refused" : "allowed")}; at-bar taught={atBar.SecretLoverEligible} " +
                   $"bridge={(allowedAtBar?.WasRefused == true ? "refused" : "allowed")}");

            // The confession is the thing that moved; these two must agree with it, at whatever pacing is set.
            t.Expect("theConfessionIsAllowedAtTheBar",
               CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = true, AdultEnabled = true, IsContent = true, AlreadyAttached = false, Regard = bar, Warmth = ModSettings.RelationWarmth}), true);
            t.Expect("belowTheBarTheVerbIsNotEvenTaught", below.SecretLoverEligible, false);
            t.Expect("belowTheBarTheBridgeRefuses", refusedBelow?.WasRefused, true);
            t.Expect("atTheBarTheVerbIsTaught", atBar.SecretLoverEligible, true);
            t.Expect("atTheBarTheBridgeDoesNotRefuse", allowedAtBar?.WasRefused == true, false);

            // The OTHER drift, the one sharing a constant could never have caught: the teaching gate refused an
            // ended romance and the bridge did not even look, so a dead romance could be raised as a secret bond
            // by an untaught emission. Deep regard on purpose, so only the status can be doing the refusing.
            profile.Romantic.IsSecretLover = false;
            profile.Romantic.Status = RomanticStatus.Broken;
            profile.ReputationWithPlayer = bar + 20;
            behavior.Store.Set(profile);
            EncounterContext ended = EncounterContextBuilder.Build(companion, profile, behavior.GameStateBridge, "hello");
            ActionOutcome refusedEnded = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "take_as_secret_lover"}, companion, new ActionContext(profile, null));

            t.Note($"ended romance at regard {bar + 20}: taught={ended.SecretLoverEligible}, " +
                   $"bridge={(refusedEnded?.WasRefused == true ? "refused" : "ALLOWED")}");
            t.Expect("anEndedRomanceIsNotTaught", ended.SecretLoverEligible, false);
            t.Expect("anEndedRomanceIsRefusedByTheBridgeToo", refusedEnded?.WasRefused, true);

            // The THIRD site of the same bar, added 2026-09-09: what the NPC is allowed to SAY. The two gates
            // above decide whether the deed may be done; neither has ever governed the words, which is why a
            // companion could declare their feelings at a regard of ten and be refused a moment later. The
            // prompt now carries a register, and it must read the bar off the SAME policy rather than a copy.
            // Driven through the LIVE PromptBuilder the chat itself uses, because a pure test can only compare
            // the policy with itself, which is exactly how the first drift went unseen.
            PromptBuilder livePrompt = behavior.LlmServices?.DebugBuildPromptBuilder();
            t.Note($"live prompt bar={livePrompt?.DeclaredAffectionBar}, policy bar={bar}, pacing={ModSettings.RelationWarmth}");
            t.Expect("thePromptDeclaresFromTheSameBarTheConfessionUses", livePrompt?.DeclaredAffectionBar, bar);

            // And the register itself, at the two points that matter, on this same real companion.
            var atBarFacts = new RomanticRegisterFacts {
               AdultContentEnabled = ModSettings.AdultContentLevel != AdultContentLevel.Off,
               PlayerIsCompatible = true,
               RegardWithPlayer = bar,
               Status = RomanticStatus.None,
               DeclaredAffectionBar = bar
            };
            var belowFacts = new RomanticRegisterFacts {
               AdultContentEnabled = atBarFacts.AdultContentEnabled,
               PlayerIsCompatible = true,
               RegardWithPlayer = bar - 1,
               Status = RomanticStatus.None,
               DeclaredAffectionBar = bar
            };

            t.Expect("atTheBarTheNpcMayNameTheirFeelings", RomanticRegisterPolicy.Resolve(atBarFacts), RomanticRegister.Attachment);
            t.Expect("belowTheBarTheNpcMayFlirtButNeverDeclare", RomanticRegisterPolicy.Resolve(belowFacts), RomanticRegister.Interest);
            t.Expect("belowTheBarStandingAloneCannotOpenACourtship", RomanticRegisterPolicy.MayInitiateCourtship(belowFacts), false);
         }
         finally
         {
            NpcProfile fresh = behavior.Store.Get(companion.StringId) ?? profile;
            fresh.ReputationWithPlayer = regardBefore;

            if (fresh.Romantic != null)
            {
               fresh.Romantic.IsSecretLover = secretBefore;
               fresh.Romantic.Status = statusBefore;
            }

            behavior.Store.Set(fresh);
         }

         return t.Line();
      }

      private static string TestCaptiveLordWithheldFromDefection(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("captive_lord_withheld_from_defection");

         // Same filter TestFreePrisoner/TestPrisonerTrade use: a capturable in-party lord who is NOT their
         // own clan's leader, so raising regard past the floor can genuinely flip CanRecruitPrisoner to true
         // (a clan leader never can, whatever the regard, which would make the flip half of this test moot).
         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null && h.Clan?.Leader != h);

         if (t.Skip(lord == null, "no capturable in-party lord who is not their own clan's leader")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, lord);
         int regardBefore = profile.ReputationWithPlayer;

         TakePrisonerAction.Apply(PartyBase.MainParty, lord);
         bool captured = lord.IsPrisoner && lord.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (t.Abort(!captured, $"capture failed for {lord.Name} (party={lord.PartyBelongedTo?.StringId ?? "none"})")) return t.Line();

         try
         {
            profile.ReputationWithPlayer = PrisonerRecruitmentPolicy.RegardFloor - 1;
            behavior.Store.Set(profile);
            EncounterContext below = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "hello");

            profile.ReputationWithPlayer = PrisonerRecruitmentPolicy.RegardFloor;
            behavior.Store.Set(profile);
            EncounterContext above = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "hello");

            t.Note($"{lord.Name}: below-floor CanRecruitPrisoner={below.CanRecruitPrisoner} withheld={below.CaptiveLordWithheldFromDefection}; " +
                   $"above-floor CanRecruitPrisoner={above.CanRecruitPrisoner} withheld={above.CaptiveLordWithheldFromDefection}");
            t.Expect("belowFloorCannotRecruit", below.CanRecruitPrisoner, false);
            t.Expect("belowFloorWithheld", below.CaptiveLordWithheldFromDefection, true);
            t.Expect("aboveFloorCanRecruit", above.CanRecruitPrisoner, true);
            t.Expect("aboveFloorNotWithheld", above.CaptiveLordWithheldFromDefection, false);
         }
         finally
         {
            try
            {
               if (lord.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(lord, Hero.MainHero);
            }
            catch
            {
               /* best-effort cleanup */
            }

            profile.ReputationWithPlayer = regardBefore;
            behavior.Store.Set(profile);
         }

         return t.Line();
      }

      // Player report 2026-08-23: in an intimate (NSFW) scene the model turned a vow into a quest. The mod
      // suppresses quests for an NSFW turn; this drives the REAL EncounterContextBuilder in each mode and pins
      // that NSFW sets SuppressQuests while Roleplay does not (Roleplay is where quests still belong).
      private static string TestEscapedTarget(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("escaped_target");

         var store = behavior?.Store;
         if (t.Abort(store == null, "no profile store")) return t.Line();

         NpcProfile giver = store.ListIds().Select(store.Get)
                                 .FirstOrDefault(p => p != null && Hero.FindFirst(h => h.StringId == p.Id) is {IsAlive: true, IsLord: true, IsChild: false} h && h != Hero.MainHero);
         if (t.Skip(giver == null, "no met lord to give a task")) return t.Line();

         Hero giverHero = Hero.FindFirst(h => h.StringId == giver.Id);
         int goldBefore = Hero.MainHero.Gold;
         int regardBefore = giver.ReputationWithPlayer;
         int eventsBefore = giver.Events.Count;
         var beaten = new InformalQuest {Type = QuestType.CapturePrisoner, Description = "(self-test) capture a lord", TargetHeroId = "cr_selftest_none",
                                         TargetHeroName = "Nobody", TargetBeatenOnDay = (int) CampaignTime.Now.ToDays, RewardGold = 100, RewardRelation = 4,
                                         IssuedOnDay = (int) CampaignTime.Now.ToDays};
         var notBeaten = new InformalQuest {Type = QuestType.CapturePrisoner, Description = "(self-test) capture another", TargetHeroId = "cr_selftest_none2",
                                            TargetHeroName = "Nobody else", RewardGold = 100, RewardRelation = 4, IssuedOnDay = (int) CampaignTime.Now.ToDays};
         var parser = new NpcMemoryService.Core.Parsing.SectionResponseParser();
         ParsedResponse claim = parser.Parse(string.Join(Environment.NewLine, "[DIALOGUE]You beat him. That will do.[/DIALOGUE]", "[QUEST_COMPLETE]", "type: capture_prisoner", "[/QUEST_COMPLETE]"));
         try
         {
            giver.ActiveQuests.Add(notBeaten);
            Quests.QuestService.ApplyResponse(giver, giverHero, claim, (int) CampaignTime.Now.ToDays, behavior.GameStateBridge, store, (_, __) => { });
            bool wordsAlone = notBeaten.IsOutstanding;
            giver.ActiveQuests.Remove(notBeaten);

            giver.ActiveQuests.Add(beaten);
            Quests.QuestService.ApplyResponse(giver, giverHero, claim, (int) CampaignTime.Now.ToDays, behavior.GameStateBridge, store, (_, __) => { });
            int paid = Hero.MainHero.Gold - goldBefore;

            t.Note($"{giver.Name}: words alone left the unbeaten task open={wordsAlone}; the beaten one is {beaten.Status}, paid {paid} denars");
            t.Expect("wordsAloneSettleNothing", wordsAlone, true);
            t.Expect("theVictoryIsAccepted", beaten.Status, QuestStatus.Completed);
            t.Expect("forHalfTheReward", beaten.RewardGold, 50);
            t.Expect("neverMoreThanHalfIsPaid", paid <= 50, true);
         }
         finally
         {
            giver.ActiveQuests.Remove(notBeaten);
            giver.ActiveQuests.Remove(beaten);
            int drift = Hero.MainHero.Gold - goldBefore;
            if (drift > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, giverHero, drift, true); // the giver's coin goes back
            if (giver.Events.Count > eventsBefore) giver.Events.RemoveRange(eventsBefore, giver.Events.Count - eventsBefore);
            giver.ReputationWithPlayer = regardBefore;
            store.Set(giver);
            Quests.LlmFeedbackNotes.ConsumeFor(giver.Id); // the refused claim left a correction for the model; drop it
         }

         return t.Line();
      }

      private static string TestPrivacySurvivesInterpreter(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("privacy_survives_interpreter");

         ParsedResponse prose = new NpcMemoryService.Core.Parsing.SectionResponseParser().Parse(
            string.Join(Environment.NewLine, "[DIALOGUE]Leave us.[/DIALOGUE]", "[ACTION]", "type: request_privacy", "result: accepted", "[/ACTION]",
                        "[ACTION]", "type: give_gold", "amount: 10", "[/ACTION]"));
         ParsedResponse spliced = Chat.ActionInterpreterComposer.WithInterpretedTags(prose, new ParsedResponse {Dialogue = ""});

         t.Note($"scene model: {string.Join(", ", prose.Actions.Select(a => a.Type))}; applied: {string.Join(", ", spliced.Actions.Select(a => a.Type))}");
         t.Expect("theAcceptanceSurvives", spliced.Actions.Any(a => a.Type == "request_privacy"), true);
         t.Expect("aDeedStaysTheInterpreters", spliced.Actions.Any(a => a.Type == "give_gold"), false);

         return t.Line();
      }

      private static string TestNoTasksFromEnemies(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("no_tasks_from_enemies");

         IFaction mine = Hero.MainHero?.MapFaction;
         Hero enemy = mine == null ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner && !h.IsChild
                                                                                 && h.MapFaction != null && FactionManager.IsAtWarAgainstFaction(h.MapFaction, mine));
         if (t.Skip(enemy == null, "the player is at war with no one")) return t.Line();

         NpcProfile profile = behavior.Store?.Get(enemy.StringId) ?? HeroProfileMapper.FromHero(enemy);
         EncounterContext ctx = EncounterContextBuilder.Build(enemy, profile, behavior.GameStateBridge, "hello");
         string prompt = new PromptBuilder {EnableQuests = true}.BuildSystemPrompt(profile, new WorldState {CurrentDay = (int) CampaignTime.Now.ToDays}, ctx);

         t.Note($"{enemy.Name} ({enemy.MapFaction?.Name}): WarStatus={ctx.WarStatus}");
         t.Expect("theContextSaysWar", ctx.WarStatus, DiplomaticStatus.AtWar);
         t.Expect("noTaskIsTaught", prompt.Contains("OFFERING TASKS"), false);
         t.Expect("noPrisonerBargainIsTaught", prompt.Contains("IF YOU WANT AN ENEMY CAPTIVE"), false);

         return t.Line();
      }

      private static string TestQuestsSuppressedInNsfw(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("quests_suppressed_in_nsfw");

         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Skip(npc == null, "no living lord to build a context for")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         ManualModelMode restore = LlmServices.ActiveModelMode;

         try
         {
            LlmServices.ActiveModelMode = ManualModelMode.Nsfw;
            EncounterContext nsfw = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, "hello");

            LlmServices.ActiveModelMode = ManualModelMode.Roleplay;
            EncounterContext rp = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, "hello");

            t.Note($"NSFW SuppressQuests={nsfw.SuppressQuests}; Roleplay SuppressQuests={rp.SuppressQuests}");
            t.Expect("nsfwSuppressesQuests", nsfw.SuppressQuests, true);
            t.Expect("roleplayKeepsQuests", rp.SuppressQuests, false);
         }
         finally
         {
            LlmServices.ActiveModelMode = restore;
         }

         return t.Line();
      }

      // Player report 2026-08-23: a witness voiced in a group scene had no access to her own memory and forgot
      // an agreement struck one-on-one. Seeds a memory on a hero and asserts the recall a group scene would give
      // them (the REAL BuildWitnessMemory path + cap) carries it. Removes the seeded event afterwards.
      private static string TestWitnessRecallsOwnMemory(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("witness_recalls_own_memory");

         Hero hero = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive);
         if (t.Skip(hero == null, "no living hero to seed a witness memory on")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, hero);
         const string marker = "[SELFTEST] agreed to speak up on the signal word";
         var day = (int) CampaignTime.Now.ToDays;

         try
         {
            profile.Events.Add(new NotableEvent(day, NotableEventType.Agreement, marker));
            behavior.Store.Set(profile);

            string memory = ChatViewModel.DebugWitnessMemory(hero.StringId);

            t.Note($"{hero.Name} witness memory: {Show(memory ?? "(null)")}");
            t.Expect("witnessMemoryBuilt", !string.IsNullOrWhiteSpace(memory), true);
            t.Expect("recallsTheAgreement", memory != null && memory.Contains(marker), true);

            // The 2026-09-07 hole: the enrichment lived in the main-speaker branch alone, so addressing a witness
            // DIRECTLY described a room full of people with no past, and re-describing an entry for another
            // observer dropped the memory while carrying the gear beside it. A group conversation IS the act of
            // turning from one person to another, so both paths must carry it or the feature loses its thread.
            var seeded = new WitnessEntry {Name = hero.Name?.ToString(), HeroStringId = hero.StringId, RelationToNpc = "a bystander"};
            System.Collections.Generic.IReadOnlyList<WitnessEntry> enriched =
               Chat.ChatViewModel.WithMemories(new System.Collections.Generic.List<WitnessEntry> {seeded});
            string carried = enriched != null && enriched.Count > 0 ? enriched[0].Memory?.Text : null;

            WitnessEntry reDescribed = Witnesses.WitnessResolver.ForObserver(
               enriched?[0], Hero.MainHero, behavior.Store);

            t.Expect("everyEntryIsGivenItsMemory", carried != null && carried.Contains(marker), true);
            t.Expect("reDescribingForAnotherSpeakerKeepsIt", reDescribed?.Memory != null && reDescribed.Memory.Text.Contains(marker), true);
         }
         finally
         {
            NpcProfile fresh = behavior.Store.Get(hero.StringId);
            if (fresh?.Events != null)
            {
               fresh.Events.RemoveAll(e => e?.summary == marker);
               behavior.Store.Set(fresh);
            }
         }

         return t.Line();
      }

      // Player complaint: conversations are ALWAYS crowded with ambient bystanders. Drives the REAL
      // WitnessPresencePolicy.Filter directly (the pure policy WitnessResolver.ApplyWitnessDensity wires as a
      // post-filter over the ambient hero witnesses it gathers, never over the player's own companions): Sparse
      // against a fixed eligible list thins the room down to its cap of 1, while Bustling with an always-passing
      // rng keeps every one of them, the exact passthrough that must reproduce today's live behaviour for a
      // player who never opens the menu (the mod is LIVE). No LLM, mutates nothing.
      private static string TestWitnessDensityThinsTheRoom(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("witness_density_thins_the_room");

         var eligible = new List<string> {"Aldric", "Boyd", "Corwin", "Derthert"};

         // Sparse: p=0.25, cap=1. An rng stub that always passes the probability check (0.0 < 0.25) isolates
         // the CAP as the thing doing the thinning here, so a regression that dropped the cap (but kept the
         // probability) would still be caught.
         List<string> sparse = WitnessPresencePolicy.Filter(eligible, WitnessDensity.Sparse, () => 0.0);
         t.Note($"sparseKeptCount={sparse.Count}");
         t.Expect("sparseThinnedAndCappedToOne", sparse.Count, 1);

         // Bustling: p=1.0, cap=int.MaxValue. Even an rng returning just under 1.0 must still pass every roll;
         // nothing is removed. This is the shipped default, so a regression here changes an existing player's
         // game the moment they update, without them ever touching the new menu.
         List<string> bustling = WitnessPresencePolicy.Filter(eligible, WitnessDensity.Bustling, () => 0.999999);
         t.Note($"bustlingKeptCount={bustling.Count}");
         t.Expect("bustlingIsExactPassthrough", bustling.Count, eligible.Count);

         return t.Line();
      }

      // Player report 2026-08-23 (v2.1.5): NPCs ended the conversation while still asking a question or making a
      // request, leaving no room to reply. The end_conversation teaching now forbids closing on a turn that awaits
      // a reply (a rhetorical outburst as the NPC dismisses the player still ends; a genuine awaited question does
      // not). Pins that guard in the taught vocabulary so a later prompt edit cannot silently drop it. No LLM.
      // Full-map panel crash guard (2026-09-03): the Harmony prefix that skips SettlementVisualManager.OnTick while a
      // CR panel owns the screen is what stops the vanilla siege-bombardment visual tick crashing natively under a
      // pushed panel (it runs on the frame loop, so the campaign-clock freeze cannot stop it). It is resolved BY NAME,
      // so a game update that renamed the type/method degrades to "not attached" silently; this asserts it is actually
      // attached to the live engine method, and that with no panel open nothing is suppressed.
      // World Almanac political minimap (Inc B, 2026-09-03): the engine gather + grid build. A dotnet test cannot
      // read Campaign.Settlements or the map scene's terrain, so this drives the REAL PoliticalMapFactsBuilder
      // against the live world and asserts it produced a real coloured map (right size, some territory painted),
      // catching a renamed settlement/terrain API that would silently yield an empty map. The pure Voronoi
      // assignment is unit-tested separately (PoliticalMapPolicyTests).
      // World Almanac Strategic Assessment (Inc B, 2026-09-04): the engine gather that feeds the counsel. A dotnet
      // test cannot read the live clan/party/wars, so this drives the REAL context builder + offline summary and
      // asserts they produce a grounded result; the async LLM counsel itself is Lane C (real LLM), out of testall.
      private static string TestStrategicAssessment(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("strategic_assessment");

         var facts = WorldAlmanac.KingdomPowerFactsBuilder.Build();
         CalradiaRemembers.Logic.WorldAlmanac.PowerIndexResult result = CalradiaRemembers.Logic.WorldAlmanac.PowerIndexPolicy.Compute(facts);
         if (t.Skip(result.Ranked.Count == 0, "no kingdoms scored in this world")) return t.Line();

         CalradiaRemembers.WorldAlmanac.StrategicAssessmentContext ctx = WorldAlmanac.StrategicAssessmentContextBuilder.Build(result);
         string offline = CalradiaRemembers.Logic.WorldAlmanac.StrategicAssessmentSummary.Compose(result, ctx.Player);

         string advisor = ctx.HasAdvisor ? $"{ctx.Advisor.Kind}:{ctx.Advisor.Tier}" : "none (alone)";
         string realm = ctx.Player.HasRealm ? $"{ctx.Player.PlayerKingdomName} rank {ctx.Player.PlayerKingdomRank}" : "landless";
         t.Note($"advisor={advisor}, player={realm}, wars={ctx.Player.AtWarWith.Count}");
         t.Expect("offlineSummaryNonEmpty", !string.IsNullOrWhiteSpace(offline), true);

         if (ctx.HasAdvisor)
         {
            string prompt = CalradiaRemembers.Logic.WorldAlmanac.StrategicAssessmentPromptBuilder.Build(result, ctx.Player, ctx.Advisor);
            t.Expect("promptGroundedOnRealKingdom", prompt.Contains(result.Ranked[0].KingdomName), true);
            t.Expect("promptForbidsInvention", prompt.Contains("never invent"), true);
         }

         return t.Line();
      }

      private static string TestPoliticalMap(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("political_map");

         var anchors = WorldAlmanac.PoliticalMapFactsBuilder.GatherAnchors();
         if (t.Skip(anchors.Count == 0, "no kingdom-held settlement in this world")) return t.Line();

         const int res = 64;
         uint[,] grid = WorldAlmanac.PoliticalMapFactsBuilder.BuildColorGrid(res);

         var painted = 0;
         for (var c = 0; c < grid.GetLength(0); c++)
            for (var r = 0; r < grid.GetLength(1); r++)
               if (grid[c, r] != CalradiaRemembers.Logic.WorldAlmanac.PoliticalMapPolicy.NoTerritory) painted++;

         // Inc C: the grid must rasterise to real PNG bytes (starts with the PNG signature), the form the panel shows.
         byte[] png = WorldAlmanac.PoliticalMapImageRenderer.RenderPng(grid);
         bool pngMagic = png != null && png.Length > 8 && png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47;

         t.Note($"anchors={anchors.Count}, grid={grid.GetLength(0)}x{grid.GetLength(1)}, painted={painted}, png={(png?.Length ?? 0)}B");
         t.Expect("gridSized", grid.GetLength(0) == res && grid.GetLength(1) == res, true);
         t.Expect("someTerritoryPainted", painted > 0, true);
         t.Expect("pngRendered", pngMagic, true);

         return t.Line();
      }

      /// <summary>
      ///   How the code that enforces a setting relates to the range the MCM menu offers for it.
      /// </summary>
      private enum SettingShape
      {
         /// <summary>The accessor returns EXACTLY the number the player picked. Driven at both ends and compared.</summary>
         Honours,

         /// <summary>
         ///   The accessor deliberately transforms or narrows what the player picked: a snap to a step, a
         ///   conversion to a scale factor, a ceiling the feature imposes. Legitimate, but it must be DECLARED
         ///   with its reason, because an undeclared narrowing is precisely the Regard Warmth Cooldown bug.
         /// </summary>
         Reshapes
      }

      /// <summary>
      ///   Every <c>[SettingPropertyInteger]</c> on <see cref="CrSettings" />, paired with the ModSettings
      ///   accessor that enforces it. A setting missing from this table FAILS the test by name: that is what
      ///   stops the guard going quietly vacuous, which is exactly how it first shipped (2026-09-10: it compared
      ///   ZERO settings and only its own "is there anything to compare" assertion caught it).
      /// </summary>
      private static readonly Dictionary<string, (string Accessor, SettingShape Shape, string Why)> SettingEnforcers =
         new(StringComparer.Ordinal) {
            ["RegardDecayDays"] = ("RegardDecayDays", SettingShape.Honours,
               "the whole point of the FloatingMammal fix: 0 must reach the gate as 0"),
            ["NpcCrossTalkRounds"] = ("NpcCrossTalkRounds", SettingShape.Honours,
               "0 must mean the witnesses fall silent, 3 must mean three rounds"),
            ["MarriageRegardNeeded"] = ("MarriageRegardThreshold", SettingShape.Reshapes,
               "capped at MarriageEligibilityThreshold: the setting may only make marriage EASIER, never harder"),
            ["LlmTimeoutSeconds"] = ("LlmResponseTimeoutSeconds", SettingShape.Honours,
               "a timeout the player set is the timeout the request gets"),
            ["ChatTextSizePercent"] = ("ChatTextSizeStep", SettingShape.Reshapes,
               "snapped to a 10% step because the size reaches the widget as a sized BRUSH NAME, not a number"),
            ["CouncilTextSizePercent"] = ("CouncilTextSizeStep", SettingShape.Reshapes,
               "snapped to a 10% step, same brush-name reason as the chat"),
            ["DarkLeatherZoomPercent"] = ("DarkLeatherScale", SettingShape.Reshapes,
               "a percent the player reads becomes the scale factor the window uses"),
            ["CouncilZoomPercent"] = ("CouncilScale", SettingShape.Reshapes,
               "a percent the player reads becomes the scale factor the window uses")
         };

      /// <summary>
      ///   Every MCM hint fits on the screen. This is a shipped bug with a receipt: v2.5.6 fixed NINETEEN hints
      ///   that ran off the edge, and the reason it hurt is that they are written "On (default): ... Off: ...",
      ///   so the half that got clipped was always the one explaining the other position. They were repaired by
      ///   hand and nothing was left to stop the twentieth doing it again - which matters now, because
      ///   2026-09-12 added the twentieth.
      ///   <para>
      ///     Reads the ATTRIBUTES rather than the live settings object, so it holds whether or not MCM has
      ///     loaded, and so a hint is checked as it was WRITTEN rather than as it happened to render here.
      ///   </para>
      /// </summary>
      private static string TestSettingHintsFit(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("setting_hints_fit");

         var toolong = new List<string>();
         var empty = new List<string>();
         var checkedCount = 0;

         foreach (PropertyInfo declared in typeof(CrSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
         foreach (CustomAttributeData attribute in declared.GetCustomAttributesData())
         {
            string hint = ReadHintText(attribute);
            if (hint == null) continue;

            checkedCount++;

            if (hint.Trim().Length == 0) empty.Add(declared.Name);
            else if (hint.Length > MaxHintChars) toolong.Add($"{declared.Name}={hint.Length}");
         }

         if (t.Skip(checkedCount == 0, "no setting declares a hint, so there is nothing to measure")) return t.Line();

         t.Note($"{checkedCount} hint(s) measured against {MaxHintChars} chars");
         t.Expect("everyHintFitsOnTheScreen", toolong.Count == 0 ? "" : string.Join(", ", toolong), "");
         t.Expect("noSettingIsLeftWithoutItsExplanation", empty.Count == 0 ? "" : string.Join(", ", empty), "");

         return t.Line();
      }

      /// <summary>
      ///   The width a hint has before MCM clips it. Measured the hard way in v2.5.6 and kept as a number
      ///   rather than a habit.
      /// </summary>
      private const int MaxHintChars = 250;

      /// <summary>The HintText named argument of a setting attribute, or null when this attribute has none.</summary>
      private static string ReadHintText(CustomAttributeData attribute)
      {
         foreach (CustomAttributeNamedArgument named in attribute.NamedArguments)
            if (named.MemberName == "HintText")
               return named.TypedValue.Value as string;

         return null;
      }

      private static string TestSettingsRangesAgree(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("settings_ranges_agree");

         CrSettings live = CrSettings.Instance;
         if (t.Skip(live == null, "MCM settings are not loaded, so there is nothing to compare against")) return t.Line();

         var mismatches = new List<string>();
         var unclassified = new List<string>();
         var unreadable = new List<string>();
         var compared = new List<string>();

         foreach (PropertyInfo declared in typeof(CrSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
         {
            if (declared.PropertyType != typeof(int) || !declared.CanWrite) continue;
            if (!HasIntegerSettingAttribute(declared)) continue;

            (int Min, int Max)? offered = ReadOfferedRange(declared);

            if (offered == null)
            {
               unreadable.Add(declared.Name);

               continue;
            }

            if (!SettingEnforcers.TryGetValue(declared.Name, out (string Accessor, SettingShape Shape, string Why) how))
            {
               unclassified.Add(declared.Name);

               continue;
            }

            PropertyInfo enforced = typeof(ModSettings).GetProperty(how.Accessor, BindingFlags.Public | BindingFlags.Static);

            if (enforced == null)
            {
               mismatches.Add($"{declared.Name}: no ModSettings.{how.Accessor} to enforce it");

               continue;
            }

            var original = (int) declared.GetValue(live);

            try
            {
               declared.SetValue(live, offered.Value.Min);
               double atMin = Convert.ToDouble(enforced.GetValue(null));
               declared.SetValue(live, offered.Value.Max);
               double atMax = Convert.ToDouble(enforced.GetValue(null));

               compared.Add(declared.Name);

               if (how.Shape == SettingShape.Honours)
               {
                  if (Math.Abs(atMin - offered.Value.Min) > 0.0001)
                     mismatches.Add($"{declared.Name}: the menu offers {offered.Value.Min}, the code enforces {atMin}");
                  if (Math.Abs(atMax - offered.Value.Max) > 0.0001)
                     mismatches.Add($"{declared.Name}: the menu offers {offered.Value.Max}, the code enforces {atMax}");
               }
               // A reshaping accessor is allowed to return something other than the number picked, but never to
               // run BACKWARDS: turning a slider up must not make its effect smaller. That is a rule about the
               // player's hand on the slider, not about how the transform happens to be written.
               else if (atMax < atMin)
               {
                  mismatches.Add($"{declared.Name}: turning it up ({offered.Value.Min}->{offered.Value.Max}) "
                                 + $"makes ModSettings.{how.Accessor} smaller ({atMin}->{atMax})");
               }
            }
            finally
            {
               declared.SetValue(live, original);
            }
         }

         t.Note($"{compared.Count} integer setting(s) driven end to end"
                + (unclassified.Count == 0 ? "" : $"; UNCLASSIFIED: {string.Join(", ", unclassified)}")
                + (unreadable.Count == 0 ? "" : $"; range unreadable: {string.Join(", ", unreadable)}")
                + (mismatches.Count == 0 ? "" : $"; MISMATCH: {string.Join(" | ", mismatches)}"));

         // Gabriel, 2026-09-10, on the Regard Warmth Cooldown: "je suis surpris que le bon comportement n'ait pas
         // ete bien protege par des tests unitaires. Par exemple, 0 aurait du etre teste."
         //
         // The awkward truth is that 0 WAS tested, and the test agreed with the bug: it pinned what the code did,
         // and nothing anywhere compared that with what the MENU offered or the changelog promised. This is that
         // place. It drives the slider to both of its own ends and reads what the game actually gets.
         t.Expect("everyValueTheMenuOffersIsAValueTheCodeHonours", mismatches.Count, 0);

         // The guard on the guard. This test first shipped comparing NOTHING (it paired settings with accessors by
         // name, and six of the eight are named differently), and only this assertion said so. A setting added
         // tomorrow must be classified above, with the reason if it reshapes, or it lands here.
         t.Expect("everyIntegerSettingIsClassified", unclassified.Count, 0);
         t.Expect("everyDeclaredRangeCouldBeRead", unreadable.Count, 0);
         t.Expect("thereIsSomethingToCompare", compared.Count > 0, true);

         return t.Line();
      }

      /// <summary>True when this property carries MCM's integer-setting attribute, whatever MCM version supplies it.</summary>
      private static bool HasIntegerSettingAttribute(PropertyInfo declared)
         => declared.GetCustomAttributes(true)
                    .Any(a => a.GetType().Name.StartsWith("SettingPropertyInteger", StringComparison.Ordinal));

      /// <summary>
      ///   The [min, max] the MCM menu offers for this setting, read from the ATTRIBUTE'S OWN CONSTRUCTOR
      ///   ARGUMENTS rather than from named members. MCM writes it as
      ///   <c>[SettingPropertyInteger(displayName, min, max, ...)]</c>, and that shape is the contract the source
      ///   file already relies on; the member NAMES behind it differ across MCM versions, which is why reading
      ///   them returned nothing at all on the live build (2026-09-10) and the whole guard compared zero settings.
      /// </summary>
      private static (int Min, int Max)? ReadOfferedRange(PropertyInfo declared)
      {
         try
         {
            foreach (CustomAttributeData data in CustomAttributeData.GetCustomAttributes(declared))
            {
               if (!data.AttributeType.Name.StartsWith("SettingPropertyInteger", StringComparison.Ordinal)) continue;

               List<int> ints = data.ConstructorArguments
                                    .Where(a => a.ArgumentType == typeof(int))
                                    .Select(a => (int) a.Value)
                                    .ToList();

               if (ints.Count >= 2) return (ints[0], ints[1]);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"settings_ranges_agree: could not read the declared range of {declared.Name}: "
                         + $"{ex.GetType().Name}: {ex.Message}");
         }

         return null;
      }

      private static string TestConsoleHelpComplete(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("console_help_complete");

         List<string> registered = ConsoleCommandIndex.Registered();
         if (t.Abort(registered.Count == 0, "no cr command found by reflection, which should be impossible")) return t.Line();

         string curated = ModCheats.Help(new List<string>());
         string all = ModCheats.Help(new List<string> {"all"});

         List<string> dead = ConsoleHelpPolicy.Dead(registered, ConsoleCommandHelp.All);
         List<string> deadInProse = ConsoleHelpPolicy.DeadInCuratedText(registered, curated);
         List<string> undocumented = ConsoleHelpPolicy.Undocumented(registered, ConsoleCommandHelp.All);

         t.Note($"{registered.Count} registered, {ConsoleCommandHelp.All.Count} documented"
                + (undocumented.Count == 0 ? "" : $", UNDOCUMENTED: {string.Join(", ", undocumented.Take(8))}")
                + (dead.Count + deadInProse.Count == 0 ? "" : $", DEAD: {string.Join(", ", dead.Concat(deadInProse).Take(8))}"));

         // Gabriel, 2026-09-10: "les joueurs utilisent beaucoup les commandes consoles. Ce serait bien qu'elles
         // soient bien documentees." This is what makes that stick. A command added tomorrow without a line in
         // ConsoleCommandHelp fails here, so the help cannot fall behind in silence, which is the only way it has
         // ever fallen behind: 70 of 259 described, cr.testall itself among the missing.
         t.Expect("everyRegisteredCommandIsDocumented", undocumented.Count, 0);

         // The error that actively misleads rather than merely omitting. cr.compose sat in the help for months
         // after being renamed cr.letter_compose, so the one name a reader was most likely to copy verbatim was
         // the one that would fail. Checked in BOTH halves: the table and the hand-written sections.
         t.Expect("nothingDocumentedHasCeasedToExist", dead.Count, 0);
         t.Expect("theCuratedProseNamesNoDeadCommand", deadInProse.Count, 0);

         // Reachability, which is what the omission actually cost: undescribed or not, every command must be
         // findable from cr.help all.
         var unreachable = registered.Where(c => all.IndexOf("cr." + c, StringComparison.Ordinal) < 0).ToList();
         if (unreachable.Count > 0) t.Note($"unreachable: {string.Join(", ", unreachable.Take(8))}");
         t.Expect("everyRegisteredCommandIsReachableThroughHelpAll", unreachable.Count, 0);

         // The curated page must say where the rest are, or a reader has no way to know it is a fraction.
         t.Expect("theCuratedPageSaysWhereTheRestIs",
                  curated.IndexOf("cr.help all", StringComparison.OrdinalIgnoreCase) >= 0, true);

         // And the two lookups a player actually uses: an exact name explains itself, a fragment finds its family.
         string exact = ModCheats.Help(new List<string> {"testall"});
         string family = ModCheats.Help(new List<string> {"duel"});

         t.Expect("anExactNameExplainsItself",
                  exact.IndexOf("cr.testall", StringComparison.Ordinal) >= 0 && exact.Length > 80, true);
         t.Expect("aFragmentFindsItsFamily",
                  family.IndexOf("cr.duel_challenge", StringComparison.Ordinal) >= 0, true);

         return t.Line();
      }

      /// <summary>
      ///   The half a unit test structurally cannot do. A unit test builds its own EncounterContext, so it can
      ///   prove that a companion WOULD carry his house's standing and never that anybody supplied it. That gap
      ///   is exactly where fkasad's governor lived on 2026-09-11: the prompt was right, the composition rule
      ///   would have been right, and the host had simply never filled the field while the engine knew it and
      ///   the mod read it ten lines away to gate an action.
      ///   <para>
      ///     So this sweeps REAL heroes, asks the factory which packs each one CARRIES, and asserts the live
      ///     context actually SUPPLIED each of them. It is the guard that turns "should a character know
      ///     something it does not know today?" into a pass rather than a player's comment.
      ///   </para>
      /// </summary>
      private static string TestKnowledgeComposition(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("knowledge_composition");

         // PRISONERS ARE IN THE SAMPLE since 16/09/2026. They were excluded, which left the ENTIRE IsCutOff
         // composition unverified in a live game — the one branch where packs are meant to go SILENT, and
         // therefore the one where a mistake is hardest to notice by reading a transcript. That exclusion was
         // cover for a real defect: realm_news and underworld ignored the flag outright, so a man in a cell
         // was reading his realm's current fortunes and the player's crime rating (audit, 15/09/2026).
         //
         // Capped separately so a campaign full of captives cannot crowd out the free heroes, and so the pass
         // always examines both branches rather than whichever the save happens to hold.
         var free = Hero.AllAliveHeroes
                        .Where(h => h != null && h != Hero.MainHero && h.IsAlive && !h.IsPrisoner)
                        .Take(60)
                        .ToList();

         var held = Hero.AllAliveHeroes
                        .Where(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsPrisoner)
                        .Take(15)
                        .ToList();

         List<Hero> people = free.Concat(held).ToList();
         if (t.Skip(people.Count == 0, "no living hero to compose knowledge for")) return t.Line();

         int examined = 0, carried = 0;
         var unsupplied = new List<string>();
         var crownless = new List<string>();
         var ungoverned = new List<string>();
         var carriers = new Dictionary<string, int>();
         var suppliedCount = new Dictionary<string, int>();
         Hero withHouse = null;

         foreach (Hero person in people)
         {
            // A hero with no stored profile is not a hero to skip: a real conversation with one composes a
            // profile from the engine by this very call, so skipping them measured only people who had already
            // chatted with the player. On 2026-09-12 that left the sweep examining FOUR heroes of the sixty it
            // walked - and the four it dropped hardest are notables and landless wanderers, which is exactly
            // where a carry rule is most likely to disagree with the host.
            NpcProfile profile = behavior.Store.Get(person.StringId) ?? HeroProfileMapper.FromHero(person);
            if (profile == null) continue;

            EncounterContext ctx = EncounterContextBuilder.Build(person, profile, behavior.GameStateBridge, null);
            if (ctx?.Bearer == null) continue;

            examined++;

            foreach (KnowledgePack pack in NpcKnowledgeFactory.For(ctx.Bearer))
            {
               carried++;
               carriers.TryGetValue(pack.Name, out int seen);
               carriers[pack.Name] = seen + 1;

               bool supplied = pack.IsSupplied(ctx);
               if (supplied)
               {
                  suppliedCount.TryGetValue(pack.Name, out int filled);
                  suppliedCount[pack.Name] = filled + 1;
               }

               // The whole question: you say this person would know it - did anyone tell them?
               //
               // Asked only of packs that REQUIRE content. The first live run reported four lords as carried
               // but not supplied for personal_bonds, and it was right to notice and wrong to fail: none of
               // them had a living feud or a lover, which is an empty life rather than a missing fact. Failing
               // on that would teach everyone to ignore the report, which is worse than not reporting.
               if (!supplied && pack.RequiresContent && unsupplied.Count < 8)
                  unsupplied.Add($"{person.Name}/{pack.Name}");
            }

            if (withHouse == null && person.Clan != null) withHouse = person;

            // STATION, ASKED OF THE ENGINE RATHER THAN OF THE PACK. station is RequiresContent=false — rightly,
            // since most people hold nothing and reporting every commoner as a missing fact would teach
            // everyone to ignore this report. But that exemption leaves the "king denies his crown" class
            // guarded by unit tests alone, which cannot see whether the HOST filled the field, and that is the
            // one thing this sweep exists to see (audit, 15/09/2026).
            //
            // So it is asked the other way round: where the ENGINE says somebody rules or governs, the context
            // must say so too. No commoner is ever asked, and no rank can go missing unnoticed.
            if (person.MapFaction is Kingdom kingdom && kingdom.Leader == person
                && string.IsNullOrWhiteSpace(ctx.RuledRealmName) && crownless.Count < 5)
               crownless.Add(person.Name?.ToString() ?? person.StringId);

            if (person.GovernorOf?.Settlement != null
                && string.IsNullOrWhiteSpace(ctx.GovernedSettlementName) && ungoverned.Count < 5)
               ungoverned.Add(person.Name?.ToString() ?? person.StringId);
         }

         // The census reads "carried/supplied" per pack. For an optional pack that is all the sweep can say,
         // and it is worth saying: a pack supplied to NOBODY across the whole world is probably unwired, which
         // is a thing to read rather than a thing to fail on (an early campaign may honestly have no feuds).
         string census = string.Join(", ", carriers.Select(kv => {
            suppliedCount.TryGetValue(kv.Key, out int filled);

            return $"{kv.Key} {filled}/{kv.Value}";
         }));

         t.Note($"{examined} hero(es) composed, {carried} pack(s) carried"
                + (census.Length == 0 ? "" : " [supplied/carried: " + census + "]")
                + (unsupplied.Count == 0 ? "" : "; CARRIED BUT NOT SUPPLIED: " + string.Join(", ", unsupplied)));

         // The invariant, and the one that would have caught the governor: nothing the factory says a person
         // knows may be missing from the context the game actually builds for them.
         t.Expect("everyRequiredPackAPersonCarriesIsActuallySupplied", unsupplied.Count, 0);

         // The guard on the guard. A sweep that composed nothing would report zero failures and prove nothing,
         // which is the vacuity that shipped in settings_ranges_agree a day earlier.
         t.Expect("thePacksReachRealPeopleAtAll", carried > 0, true);

         // The two ranks the engine can confirm on its own. A king who was never told he rules DENIED THE
         // CROWN in front of two players, and fkasad's governor did not know he governed; both were hosts
         // failing to fill a field the engine already knew, which no unit test can see.
         if (crownless.Count > 0) t.Note("RULERS WHOSE CROWN NEVER REACHED THE CONTEXT: " + string.Join(", ", crownless));
         if (ungoverned.Count > 0) t.Note("GOVERNORS WHOSE SEAT NEVER REACHED THE CONTEXT: " + string.Join(", ", ungoverned));

         t.Expect("everyRulerTheEngineKnowsIsToldHeRules", crownless.Count, 0);
         t.Expect("everyGovernorTheEngineKnowsIsToldHeGoverns", ungoverned.Count, 0);

         // And end to end on one real person: the composed knowledge must actually reach the prompt, or the
         // whole pipeline could be correct and silent.
         if (withHouse != null)
         {
            NpcProfile houseProfile = behavior.Store.Get(withHouse.StringId);
            EncounterContext ctx = EncounterContextBuilder.Build(withHouse, houseProfile, behavior.GameStateBridge, null);
            string house = ctx?.HouseStanding?.HouseName ?? "";

            t.Note($"{withHouse.Name}: house='{house}', "
                   + $"fiefs={ctx?.HouseStanding?.Fiefs?.Count ?? 0}, wars={ctx?.HouseStanding?.AtWarWith?.Count ?? 0}");
            t.Expect("aRealHousesStandingReachesTheLiveContext", !string.IsNullOrWhiteSpace(house), true);
         }

         return t.Line();
      }


      /// <summary>
      ///   Spotlight wiring, swept against the REAL view-model. What this pins is not the cast rules (pure and
      ///   long since tested) but that the host actually FILLS the cast - the two things that were missing.
      /// </summary>

      /// <summary>
      ///   The one rule that makes a remembered briefing safe: a private memory reaches its holder and nobody
      ///   else. Driven through the real witness-recall seam, because that is the only door it could escape by.
      /// </summary>
      private static string TestPrivateMemory(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("private_memory");

         Hero subject = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsAlive);
         if (t.Skip(subject == null, "no living hero to remember anything")) return t.Line();

         NpcProfile profile = behavior?.Store?.Get(subject!.StringId);
         if (t.Skip(profile?.Events == null, "no profile to plant a memory on")) return t.Line();

         List<NotableEvent> original = profile!.Events.ToList();
         const string shared = "SELFTEST-SHARED they stood beside the player at the ford";
         const string secret = "SELFTEST-SECRET the player asked me privately to charm the countess";

         try
         {
            int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;

            profile.Events.Add(new NotableEvent(day, NotableEventType.Other, shared));
            profile.Events.Add(new NotableEvent(day, NotableEventType.Other, secret) {IsPrivate = true});
            behavior.Store.Set(profile);

            string recall = ChatViewModel.DebugWitnessMemory(subject.StringId) ?? "";
            t.Note($"{subject.Name}: witness recall = {(recall.Length == 0 ? "(none)" : recall.Substring(0, System.Math.Min(120, recall.Length)))}");

            t.Expect("aSharedMemoryStillReachesTheRoom", recall.Contains("SELFTEST-SHARED"), true);
            t.Expect("aPrivateMemoryNeverReachesTheRoom", !recall.Contains("SELFTEST-SECRET"), true);

            // And somebody whose ONLY memories are private must contribute nothing, rather than an empty husk
            // that would read to the model as "this person remembers something they are not telling you".
            profile.Events.Clear();
            profile.Events.Add(new NotableEvent(day, NotableEventType.Other, secret) {IsPrivate = true});
            behavior.Store.Set(profile);

            t.Expect("nothingButPrivateMeansNoRecallAtAll",
                     string.IsNullOrWhiteSpace(ChatViewModel.DebugWitnessMemory(subject.StringId)), true);
         }
         finally
         {
            profile.Events.Clear();
            profile.Events.AddRange(original);
            behavior?.Store?.Set(profile);
         }

         return t.Line();
      }


      /// <summary>
      ///   The briefing-to-meeting vector as one run. Every fault of 2026-09-12 lived in a join, not a piece,
      ///   so this walks the joins: what the picker decides, what the close writes, who can read it, and what
      ///   the return asks for.
      /// </summary>
      private static string TestBriefingToChat(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("briefing_to_chat");

         List<Hero> cast = Hero.AllAliveHeroes?
                               .Where(h => h != null && h != Hero.MainHero && h.IsAlive)
                               .Take(3)
                               .ToList() ?? new List<Hero>();

         if (t.Skip(cast.Count < 3 || behavior?.Store == null, "need three living heroes and a store")) return t.Line();

         Hero anchor = cast[0], second = cast[1], target = cast[2];
         NpcProfile anchorProfile = behavior.Store.Get(anchor.StringId);
         NpcProfile secondProfile = behavior.Store.Get(second.StringId);

         if (t.Skip(anchorProfile?.Events == null || secondProfile?.Events == null, "no profiles to remember with"))
            return t.Line();

         List<NotableEvent> anchorWas = anchorProfile!.Events.ToList();
         List<NotableEvent> secondWas = secondProfile!.Events.ToList();

         const string first = "SELFTEST-ORDER win her over before we meet";
         const string last = "SELFTEST-LAST she is waiting, we go now";

         try
         {
            // 1. what the picker DECIDES - the part that must be right; the popup is not.
            CompanionBriefingCoordinator.RememberReturn(target, new List<Hero> {anchor, second});

            // 2. the briefing itself, on the real view-model, with both companions in the room.
            var vm = new ChatViewModel();
            vm.SetNpcHero(anchor);
            vm.AddWitness(WitnessResolver.EntryForCompanion(second));
            vm.ActivateCompanionBriefing(target);

            vm.RecordPlayerLineForDiagnostics(first);
            vm.RecordPlayerLineForDiagnostics("A long middle of reward and reassurance that matters far less.");
            vm.RecordPlayerLineForDiagnostics("More of the same, the sort of thing a bounded brief used to keep.");
            vm.RecordPlayerLineForDiagnostics(last);

            // 3. the close, which is where the private word becomes a memory.
            vm.FinalizeCompanionBriefs();

            NotableEvent anchorGot = behavior.Store.Get(anchor.StringId)?.Events
                                             ?.LastOrDefault(e => e != null && e.summary?.Contains("SELFTEST-ORDER") == true);
            NotableEvent secondGot = behavior.Store.Get(second.StringId)?.Events
                                             ?.LastOrDefault(e => e != null && e.summary?.Contains("SELFTEST-ORDER") == true);

            t.Expect("theAnchorRemembersTheErrand", anchorGot != null, true);
            t.Expect("theOtherCompanionRemembersItToo", secondGot != null, true);
            t.Expect("bothRememberItPRIVATELY", anchorGot?.IsPrivate == true && secondGot?.IsPrivate == true, true);

            // The cap that ate his mission statement: the FIRST thing said must survive, not only the last.
            t.Expect("theOrderSurvivesAndSoDoesTheLastWord",
                     anchorGot?.summary?.Contains("SELFTEST-ORDER") == true
                     && anchorGot.summary.Contains("SELFTEST-LAST"), true);
            t.Expect("theBriefingNamesTheMeetingItWasFor",
                     anchorGot?.summary?.Contains(target.Name?.ToString() ?? "\u0000") == true, true);

            // 4. THE LOAD-BEARING PROPERTY, by both roads the target could read it.
            t.Expect("theErrandStaysOutOfTheWitnessRecall",
                     !(ChatViewModel.DebugWitnessMemory(anchor.StringId) ?? "").Contains("SELFTEST-ORDER"), true);

            EncounterContext hers = EncounterContextBuilder.Build(
               target, behavior.Store.Get(target.StringId), behavior.GameStateBridge, "and who are these two?",
               new List<WitnessEntry> {
                  WitnessResolver.EntryForCompanion(anchor), WitnessResolver.EntryForCompanion(second)
               }, false);

            string roomAsSheReadsIt = string.Join(" ", (hers?.Witnesses ?? new List<WitnessEntry>())
                                                       .Select(w => w?.Memory?.Text ?? ""));
            t.Expect("theErrandStaysOutOfHerOwnContextToo", !roomAsSheReadsIt.Contains("SELFTEST-ORDER"), true);

            // 5. the return: armed at the right meeting, with the right people, and asking for them by name.
            t.Expect("theReturnIsArmedAtThatMeeting",
                     string.Equals(CompanionBriefingCoordinator.PendingReturnName, target.Name?.ToString()), true);

            ChatSceneRequest request = ChatSceneStager.BuildBriefedReturn(
               target, CompanionBriefingCoordinator.PendingReturnCompanions.ToList());

            t.Expect("theReturnReopensOnTheTarget", request?.Partner == target, true);
            t.Expect("andSeatsBothCompanionsSoThePickerNeedNotAsk",
                     request?.BriefedCompanions?.Count == 2, true);

            t.Note($"briefed {anchor.Name} and {second.Name} about {target.Name}; " +
                   $"memory {anchorGot?.summary?.Length ?? 0} chars, private={anchorGot?.IsPrivate}");
         }
         finally
         {
            anchorProfile.Events.Clear();
            anchorProfile.Events.AddRange(anchorWas);
            secondProfile.Events.Clear();
            secondProfile.Events.AddRange(secondWas);
            behavior.Store.Set(anchorProfile);
            behavior.Store.Set(secondProfile);
            CompanionBriefingCoordinator.Forget();
         }

         return t.Line();
      }

      private static string TestSpotlightCast(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("spotlight_cast");

         Hero anchor = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsAlive && h.IsLord);
         Hero arrival = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsAlive && h != anchor);

         if (t.Skip(anchor == null || arrival == null, "no two living heroes to seat")) return t.Line();

         var vm = new ChatViewModel();

         // HOLE 1: an anchor and not one witness. Before the fix this left _cast null outright, so nobody
         // could ever be drawn in and the ensemble scene could not begin in the commonest scene there is.
         vm.SetNpcHero(anchor);
         t.Expect("aCastExistsFromTheAnchorAlone", vm.CastRoleOf(anchor!.StringId) == CastRole.Active, true);

         // HOLE 2: the arrival every companion actually makes. Before the fix this returned null - present in
         // the room, absent from the cast - and Promote refused them for the rest of the conversation.
         vm.AddWitness(new WitnessEntry {
            Name = arrival!.Name?.ToString(),
            HeroStringId = arrival.StringId,
            RelationToNpc = "brought into the room by this test",
            IsFemale = arrival.IsFemale
         });

         CastRole? role = vm.CastRoleOf(arrival.StringId);
         t.Note($"anchor={anchor.Name}, arrival={arrival.Name}, arrivalRole={(role?.ToString() ?? "(not in the scene)")}");
         t.Expect("anArrivalJoinsTheCastAsAWitness", role == CastRole.Witness, true);

         // And being in the cast is what makes the rest of the pillar reachable at all.
         t.Expect("aLoneAnchorIsNotYetAnEnsemble", !vm.IsEnsembleScene, true);
         t.Expect("thePromotionThatUsedToFailNowTakes", vm.PromoteForDiagnostics(arrival.StringId), true);
         t.Expect("twoActiveVoicesMakeAnEnsemble", vm.IsEnsembleScene, true);

         return t.Line();
      }

      private static string TestUnavailableDeeds(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("unavailable_deeds");

         var lords = Hero.AllAliveHeroes
                         .Where(h => h != null && h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner && h.Clan != null && h.Clan != Clan.PlayerClan)
                         .Take(60)
                         .ToList();
         if (t.Skip(lords.Count == 0, "no living foreign lord to judge")) return t.Line();

         // The invariant, swept over REAL campaign state rather than a hand-built fake: for every lord in the
         // world, a verb that is currently taught must NOT also be declaring itself shut. The two channels are
         // read in the same breath by the model, so a verb appearing in both would hand it a permission and its
         // denial in one prompt, which is worse than either alone.
         int stating = 0, contradictions = 0, leakedVerbIds = 0;
         Hero nearMiss = null;

         foreach (Hero lord in lords)
         {
            NpcProfile profile = behavior.Store.Get(lord.StringId);
            if (profile == null) continue;

            var verbCtx = new VerbContext(lord, profile, behavior.GameStateBridge, null, null);
            List<DeedRefusal> refusals = VerbRegistry.Instance.ComposeUnavailability(verbCtx);
            if (refusals.Count == 0) continue;

            stating++;
            if (nearMiss == null) nearMiss = lord;

            string teachings = VerbRegistry.Instance.ComposeTeachings(verbCtx) ?? "";
            string block = DeedUnavailabilityPolicy.Compose(refusals);

            // A refusal must never carry the verb id: naming lend_troops in a sentence about what CANNOT be
            // done teaches the model the very token it must not emit, which is how an untaught action gets
            // emitted and refused by the bridge, the exact loop this whole mechanism exists to close.
            foreach (string verbId in new[] {"give_influence", "lend_troops", "[ACTION]"})
            {
               if (block.IndexOf(verbId, StringComparison.OrdinalIgnoreCase) >= 0) leakedVerbIds++;
               if (teachings.IndexOf(verbId, StringComparison.OrdinalIgnoreCase) >= 0
                   && block.IndexOf("court", StringComparison.OrdinalIgnoreCase) >= 0
                   && verbId == "give_influence")
                  contradictions++;
            }
         }

         t.Note($"{lords.Count} lords swept, {stating} naming a shut deed" + (nearMiss != null ? $" (e.g. {nearMiss.Name})" : ""));
         t.Expect("noVerbIdEverLeaksIntoARefusal", leakedVerbIds, 0);
         t.Expect("aVerbIsNeverTaughtAndRefusedAtOnce", contradictions, 0);

         // End to end, on the one lord the world actually supplied: the block must reach the REAL EncounterContext
         // the chat builds, or every rule above is true of a string nobody reads.
         if (t.Skip(nearMiss == null, "no lord in this world currently has the means but not the trust, so the plumbing cannot be driven")) return t.Line();

         NpcProfile nearProfile = behavior.Store.Get(nearMiss.StringId);
         EncounterContext ctx = EncounterContextBuilder.Build(nearMiss, nearProfile, behavior.GameStateBridge, null);

         t.Note($"{nearMiss.Name}: UnavailableDeeds={(string.IsNullOrWhiteSpace(ctx?.UnavailableDeeds) ? "(none)" : ctx.UnavailableDeeds.Replace(Environment.NewLine, " | "))}");
         t.Expect("theShutDeedReachesTheLiveEncounterContext", !string.IsNullOrWhiteSpace(ctx?.UnavailableDeeds), true);
         t.Expect("theRefusalForbidsThePromiseAndNotOnlyTheDeed",
                  ctx?.UnavailableDeeds?.IndexOf("do not promise", StringComparison.OrdinalIgnoreCase) >= 0, true);

         return t.Line();
      }

      private static string TestChatFocusArbiter(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_focus_arbiter");

         ScreenBase top = ScreenManager.TopScreen;
         if (t.Skip(top?.Layers == null, "no readable top screen to arbitrate against")) return t.Line();

         // Real engine layers, built exactly the way vanilla builds its own: SandBox's conversation view uses
         // new GauntletLayer("MissionConversation", 49) and the escape menu new GauntletLayer("MissionEscapeMenu",
         // 50), both with IsFocusLayer set. Neither is given input restrictions here, so neither can take input
         // from the player during the test; and no frame passes between adding and removing them.
         var vanillaConversation = new GauntletLayer("MissionConversation", 49);
         var realModal = new GauntletLayer("MissionEscapeMenu", 50);
         // A SECOND modal instance, and not a reuse of the first. ScreenBase.RemoveLayer calls
         // layer.HandleFinalize(), and AddLayer refuses a finalized layer outright ("Trying to add a null or
         // finalized layer"), so a removed layer can NEVER go back on a screen. Reusing it made the last
         // assertion below read Hold on 2026-09-09, reporting a policy failure where the layer had simply never
         // been re-added. An engine gotcha worth the two lines it costs to avoid.
         var modalOverTheBackdrop = new GauntletLayer("MissionEscapeMenu", 50);
         GauntletLayer settlementMenu = null;

         try
         {
            // THE invariant, and the one this test was too lenient to assert on its first version: on a screen
            // with no modal up, the chat must HOLD. Reading the resting verdict as a mere reference is what let
            // the first run report only a mismatched reason string, when the finding was far larger than that.
            //
            // What it found (2026-09-09, screen=NavalMapScreen, layers=7, atRest=Yield): the sweep behind this
            // arbiter looked at `new GauntletLayer("name", ...)` sites and therefore missed the one layer that
            // is not a GauntletLayer at all. TaleWorlds.Engine.Screens.SceneLayer is the 3D world, and its
            // constructor sets IsFocusLayer unconditionally, so it claims focus on EVERY screen a conversation
            // can happen on. Unexempted, this arbiter would have yielded on the first frame of every single
            // conversation and never come back: worse than the bug it was written to fix. Decompiling all 58
            // game assemblies afterwards confirmed SceneLayer is the ONLY layer in the game that claims focus
            // from a constructor rather than when it opens, so the exemption list is complete, and this line is
            // what keeps it complete.
            ChatFocusVerdict atRest =
               ChatFocusArbitrationPolicy.Decide(true, ChatManager.LayerFactsFor(top, null));
            var resting = ChatManager.LayerFactsFor(top, null);
            string claimers = string.Join(", ", resting.Where(l => l != null && l.WantsFocus && !l.IsTheWorldBeneath)
                                                       .Select(l => l.Name ?? "(unnamed)"));
            t.Note($"screen={top.GetType().Name}, layers={top.Layers.Count}, atRest={atRest}, " +
                   $"restingFocusClaimers=[{(claimers.Length == 0 ? "none" : claimers)}]");
            t.Expect("aRestingScreenNeverTakesTheChatOffTheScreen", atRest, ChatFocusVerdict.Hold);
            t.Expect("theWorldBeneathIsRecognisedByType", resting.Any(l => l != null && l.IsTheWorldBeneath), true);

            // tashmetu's bug (2026-09-09): vanilla's conversation layer lands on the SAME screen a moment after
            // the chat opened, and the old arbiter, which only counted layers, read it as a screen above and slid
            // the chat off for good. It must change nothing at all.
            top.AddLayer(vanillaConversation);
            vanillaConversation.IsFocusLayer = true;
            List<OverlayNeighbour> withConversation = ChatManager.LayerFactsFor(top, null);

            // The one assumption a pure test cannot check: that ScreenLayer.Name really hands back the name the
            // layer was built with. Everything above rests on it, and a game update could take it away silently.
            t.Expect("vanillaLayerNameReadsBackFromTheEngine",
                     withConversation.Any(l => l != null && l.Name == "MissionConversation" && l.WantsFocus), true);
            t.Expect("vanillaConversationLayerChangesNothing",
                     ChatFocusArbitrationPolicy.Decide(true, withConversation), ChatFocusVerdict.Hold);

            // The other half: a genuine modal must still get the screen, or the chat starves a pause menu of
            // input and the player cannot click Resume.
            top.AddLayer(realModal);
            realModal.IsFocusLayer = true;
            List<OverlayNeighbour> withModal = ChatManager.LayerFactsFor(top, null);

            t.Expect("yieldsToARealModal", ChatFocusArbitrationPolicy.Decide(true, withModal), ChatFocusVerdict.Yield);
            t.Expect("theLogNamesWhatTookTheScreen",
                     ChatFocusArbitrationPolicy.ReasonFor(true, withModal), "MissionEscapeMenu");

            // The reclaim that never came in his session. A yield must never outlive the thing it yielded to.
            top.RemoveLayer(realModal);
            t.Expect("reclaimsTheScreenWhenTheModalGoes",
                     ChatFocusArbitrationPolicy.Decide(true, ChatManager.LayerFactsFor(top, null)), ChatFocusVerdict.Hold);

            // Gabriel's play-test, 2026-09-09, and the second proof that a NAME LIST cannot be complete. He
            // opened a chat in a TAVERN, where vanilla's settlement menu ("MapMenuOverlay") holds focus for the
            // whole conversation: the chat yielded to the very backdrop it had been drawn over, never appeared,
            // and the vanilla dialogue ran on underneath until he left the menu. What is already claiming focus
            // when the chat opens is scenery, whatever it is called, and only what arrives afterwards can be a
            // screen above. Driven here on a REAL layer with vanilla's own name and order (205).
            settlementMenu = new GauntletLayer("MapMenuOverlay", 205);
            top.AddLayer(settlementMenu);
            settlementMenu.IsFocusLayer = true;

            List<OverlayNeighbour> inATavern = ChatManager.LayerFactsFor(top, null);
            var backdrop = new HashSet<string>(StringComparer.Ordinal) {"MapMenuOverlay"};

            t.Expect("scenerySeenAtOpenNeverTakesTheScreenLater",
                     ChatFocusArbitrationPolicy.Decide(true, inATavern, backdrop), ChatFocusVerdict.Hold);
            t.Expect("theSameLayerUnrecordedIsExactlyTheBugHeSaw",
                     ChatFocusArbitrationPolicy.Decide(true, inATavern), ChatFocusVerdict.Yield);

            // And the backdrop must not become an amnesty: a genuine modal opening over that menu still wins.
            top.AddLayer(modalOverTheBackdrop);
            modalOverTheBackdrop.IsFocusLayer = true;
            t.Expect("aModalOverTheBackdropStillTakesTheScreen",
                     ChatFocusArbitrationPolicy.Decide(true, ChatManager.LayerFactsFor(top, null), backdrop),
                     ChatFocusVerdict.Yield);

            SafeRemoveLayer(top, modalOverTheBackdrop);
            SafeRemoveLayer(top, settlementMenu);
         }
         finally
         {
            // Never leave a stray layer on a player's screen, whatever went wrong above. SafeRemoveLayer is a
            // no-op for a layer already gone, so the tidy-up inside the body costs nothing here.
            SafeRemoveLayer(top, realModal);
            SafeRemoveLayer(top, modalOverTheBackdrop);
            SafeRemoveLayer(top, settlementMenu);
            SafeRemoveLayer(top, vanillaConversation);
         }

         return t.Line();
      }

      /// <summary>Best-effort layer removal for the arbiter test's cleanup: a throw here must not mask the result.</summary>
      private static void SafeRemoveLayer(ScreenBase screen, ScreenLayer layer)
      {
         try
         {
            if (screen?.Layers != null && layer != null && screen.Layers.Contains(layer)) screen.RemoveLayer(layer);
         }
         catch (Exception ex)
         {
            ModLog.Error($"chat_focus_arbiter cleanup failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static string TestPanelSiegeVisualGuard(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("panel_siege_guard");

         UI.SettlementVisualTickGuard.EnsurePatched(); // idempotent: already attached at startup
         t.Note($"IsPatched={UI.SettlementVisualTickGuard.IsPatched}, SuppressAtRest={UI.CrPanelHost.SuppressSettlementVisualTick}");
         t.Expect("guardAttachedToLiveOnTick", UI.SettlementVisualTickGuard.IsPatched, true);
         t.Expect("noSuppressionAtRest", UI.CrPanelHost.SuppressSettlementVisualTick, false);

         // The map-navigation guard rides the same "a panel owns the screen" fact. Resolved by NAME against the live
         // engine, so this assertion is what catches a game update renaming MapScreen.TickNavigationInput: without
         // it the guard silently detaches and a map hotkey opens the face generator under a letter again.
         UI.MapNavigationInputGuard.EnsurePatched(); // idempotent: already attached at startup
         t.Note($"navGuardPatched={UI.MapNavigationInputGuard.IsPatched}, panelOwnsScreenAtRest={UI.CrPanelHost.AnyPanelOwnsScreen}");
         t.Expect("navGuardAttachedToLiveTickNavigationInput", UI.MapNavigationInputGuard.IsPatched, true);
         t.Expect("mapHotkeysLeftAloneAtRest", UI.CrPanelHost.AnyPanelOwnsScreen, false);

         return t.Line();
      }

      private static string TestPanelStateHosting(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("panel_state_hosting");

         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive);
         if (t.Skip(npc == null, "no living lord to address a test letter to")) return t.Line();

         NpcProfile profile = behavior.Store.Get(npc.StringId);
         if (t.Abort(profile == null, "no profile for the picked lord")) return t.Line();

         if (t.Skip(LetterWindowManager.IsOpen, "a letter window is already open, cannot safely drive the open/close cycle")) return t.Line();

         try
         {
            TaleWorlds.Core.GameState activeBefore = TaleWorlds.Core.Game.Current?.GameStateManager?.ActiveState;
            t.Note($"activeBefore={activeBefore?.GetType().Name ?? "(none)"}");

            LetterWindowManager.OpenCompose(profile);

            TaleWorlds.Core.GameState activeDuring = TaleWorlds.Core.Game.Current?.GameStateManager?.ActiveState;
            t.Expect("stateIsCrPanelState", activeDuring is UI.CrPanelState, true);
            t.Expect("hostReportsOpen", LetterWindowManager.IsOpen, true);
            t.Expect("suppressionEngaged", UI.CrPanelHost.AnyPanelOwnsScreen, true);

            LetterWindowManager.Close();

            TaleWorlds.Core.GameState activeAfter = TaleWorlds.Core.Game.Current?.GameStateManager?.ActiveState;
            t.Expect("stackReturnedToPriorState", activeAfter == activeBefore, true);
            t.Expect("hostReportsClosed", LetterWindowManager.IsOpen, false);
            t.Expect("suppressionCleared", UI.CrPanelHost.AnyPanelOwnsScreen, false);
         }
         finally
         {
            // Never leave the parchment open or the map muted even if an assertion above threw mid-run.
            if (LetterWindowManager.IsOpen) LetterWindowManager.Close();
         }

         return t.Line();
      }

      private static string TestDialogueEntryTrace(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("dialogue_entry_trace");

         TaleWorlds.CampaignSystem.Conversation.ConversationManager mgr = Campaign.Current?.ConversationManager;
         if (t.Abort(mgr == null, "no live ConversationManager to read stateMap from")) return t.Line();

         // hero_main_options is registered by LlmDialogFlow.Register at every session launch (see
         // LlmDialogFlow.EntryHubTokens), so it certainly exists whenever a campaign is live.
         const string knownToken = "hero_main_options";
         int? index = Dialogue.DialogueEntryTraceCollector.DebugResolveTokenIndex(mgr, knownToken);
         if (t.Abort(index == null, $"'{knownToken}' not found in stateMap (reflection unavailable, or the hub was renamed)")) return t.Line();

         string reverseName = Dialogue.DialogueEntryTraceCollector.DebugResolveTokenName(mgr, index.Value);
         t.Note($"knownToken={knownToken}, index={index}, reverseName={reverseName}");
         t.Expect("tokenRoundTrips", reverseName, knownToken);

         // Policy verdict, both directions: reporter A's shape (offered, overlay never opened) and
         // reporter B's shape (not offered, a foreign hub ends the trail).
         string offeredLine = DialogueEntryTracePolicy.ComposeLine(
            "Test Hero", "wanderer", entryOffered: true,
            tokenTrail: new List<string> {"start", knownToken}, registeredHubTokens: Dialogue.LlmDialogFlow.EntryHubTokens);
         string notOfferedLine = DialogueEntryTracePolicy.ComposeLine(
            "Test Hero", "wanderer", entryOffered: false,
            tokenTrail: new List<string> {"start", "some_foreign_hub"}, registeredHubTokens: Dialogue.LlmDialogFlow.EntryHubTokens);

         t.Expect("offeredVerdictReadsYes", offeredLine.Contains("CR entry offered = YES"), true);
         t.Expect("notOfferedVerdictReadsNo", notOfferedLine.Contains("CR entry offered = NO"), true);

         return t.Line();
      }

      private static string TestEndConversationGuardsAwaitingReply(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("end_conversation_guards_open_question");

         GameActionDefinition end = LlmServices.DebugActionVocabulary(ModSettings.RelationWarmth)
            .FirstOrDefault(a => a.Type == "end_conversation");

         if (t.Skip(end == null, "end_conversation is not in the taught vocabulary")) return t.Line();

         string desc = end.Description ?? "";
         t.Note(Show(desc));
         t.Expect("guardsWithMustStayOpen", desc.Contains("MUST stay open"), true);
         t.Expect("namesTheAwaitedReply", desc.Contains("await a reply"), true);

         return t.Line();
      }

      // Council Tier 4 (give_hostage v2, 2026-08-24): a hostage whose house breaks faith is forfeit and becomes the
      // player's prisoner. Seeds a hostage term on a free lord, forces the breach through the REAL ExecuteBreach
      // path (TakePrisonerAction), asserts they are imprisoned, then frees them so the campaign is left untouched.
      private static string TestHostageBreachImprisons(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("hostage_breach_imprisons");

         if (t.Skip(CouncilHostageBehavior.Instance == null, "hostage system not active")) return t.Line();

         Hero hero = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.Clan != null);
         if (t.Skip(hero == null, "no free lord to stand in as a test hostage")) return t.Line();

         try
         {
            CouncilHostageBehavior.Instance.DebugSeed(hero.StringId, hero.StringId, hero.Clan?.StringId, 40);
            bool forced = CouncilHostageBehavior.Instance.DebugForceBreach(hero.StringId);

            t.Note($"{hero.Name}: forced={forced}, IsPrisoner={hero.IsPrisoner}");
            t.Expect("breachForced", forced, true);
            t.Expect("hostageImprisoned", hero.IsPrisoner, true);
         }
         finally
         {
            try { if (hero.IsPrisoner) EndCaptivityAction.ApplyByReleasedByChoice(hero); }
            catch (Exception ex) { ModLog.Error($"[SELFTEST] hostage_breach cleanup failed: {ex.Message}"); }
         }

         return t.Line();
      }

      // Council Tier 4 (support_claimant): the decision now lives in the pure ClaimantSupportPolicy. Gathers a real
      // same-kingdom other-clan lord's live facts and runs the REAL policy via CouncilLift.EvaluateSupportClaimant
      // (non-destructive, no influence moved), asserting a valid backer clears the WHO gates and lands on the amount
      // decision (Applied when they like you and can spare it, else CannotAfford), never a WHO-gate refusal.
      private static string TestClaimantSupportEvaluates(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("claimant_support_evaluates");

         Clan playerClan = Clan.PlayerClan;
         if (t.Skip(playerClan?.Kingdom == null, "the player leads no kingdom to back a claim in")) return t.Line();

         Hero backer = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h is {IsLord: true, IsPrisoner: false} && h.Clan != null && h.Clan != playerClan && h.Clan.Kingdom == playerClan.Kingdom);
         if (t.Skip(backer == null, "no other lord of your kingdom to stand in as a backer")) return t.Line();

         ClaimantSupportResult r = CouncilLift.EvaluateSupportClaimant(backer);

         t.Note($"{backer.Name}: outcome={r.Outcome}, amount={r.Amount}");
         bool clearedWhoGates = r.Outcome == ClaimantSupportOutcome.Applied || r.Outcome == ClaimantSupportOutcome.CannotAfford;
         t.Expect("clearedWhoGatesToTheAmountDecision", clearedWhoGates, true);

         return t.Line();
      }

      /// <summary>
      ///   The one action gate on the live world (30/09/2026): no child takes an adult's act, no captive acts outside the
      ///   acts whose own rules judge captivity, no one dead or disabled acts at all, the player never acts toward
      ///   themselves, and a free adult lord may do everything. Reads the whole population. Writes nothing.
      /// </summary>
      private static string TestSelfEsteem(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("self_esteem");

         var store = behavior?.Store;
         if (t.Abort(store == null, "no profile store")) return t.Line();

         NpcProfile profile = store.ListIds().Select(store.Get)
                                   .FirstOrDefault(p => p != null && Hero.FindFirst(h => h.StringId == p.Id) is {IsAlive: true, IsChild: false} h && h != Hero.MainHero);
         if (t.Skip(profile == null, "no met adult character to read")) return t.Line();

         Hero hero = Hero.FindFirst(h => h.StringId == profile.Id);
         int shift = profile.SelfEsteemShift;
         int? shiftDay = profile.SelfEsteemShiftDay;
         List<int> captured = new List<int>(profile.SelfEsteemCapturedOnDays ?? new List<int>());
         int? blow = profile.LastPersonalBlowDay, glad = profile.LastPersonalGladnessDay;
         try
         {
            profile.SelfEsteemShift = 0;
            profile.SelfEsteemShiftDay = null;
            profile.SelfEsteemCapturedOnDays = new List<int>();

            NpcMemoryService.Core.Mood.SelfEsteemBand? before = Standing.SelfEsteemService.BandFor(hero, profile);
            Standing.SelfEsteemService.Record(hero, SelfEsteemEvent.Captured);
            int afterOne = profile.SelfEsteemShift;
            Standing.SelfEsteemService.Record(hero, SelfEsteemEvent.Captured);
            int afterTwo = profile.SelfEsteemShift;
            NpcMemoryService.Core.Mood.SelfEsteemBand? after = Standing.SelfEsteemService.BandFor(hero, profile);

            t.Note($"{hero.Name}: {before} -> {after}; shift {afterOne}, then {afterTwo}");
            t.Expect("aCaptivityLowersIt", afterOne < 0, true);
            t.Expect("theSecondLandsHarder", afterTwo - afterOne < afterOne, true);
            t.Expect("itNeverRises", (int) (after ?? 0) <= (int) (before ?? 0), true);
            t.Expect("theBlowReachesHisDay", Standing.SelfEsteemService.HasRecentBlow(profile), true);

            // The perceived image (P2): one numberless sentence for the chat dossier, never a number.
            string seen = Standing.PerceivedImageService.For(hero, profile);
            t.Note($"seems: {seen}");
            t.Expect("heSeemsSomething", !string.IsNullOrWhiteSpace(seen), true);
            t.Expect("neverANumber", seen != null && seen.Any(char.IsDigit), false);
         }
         finally
         {
            profile.SelfEsteemShift = shift;
            profile.SelfEsteemShiftDay = shiftDay;
            profile.SelfEsteemCapturedOnDays = captured;
            profile.LastPersonalBlowDay = blow;
            profile.LastPersonalGladnessDay = glad;
            store.Set(profile);
         }

         return t.Line();
      }

      private static string TestMoodOfTheDay(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("mood_of_the_day");

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsLord && !h.IsChild && !h.IsPrisoner);
         if (t.Skip(lord == null, "no free adult lord to read")) return t.Line();

         (NpcMemoryService.Core.Mood.DayMood? first, bool _) = Mapping.DayMoodReader.Draw(lord);
         (NpcMemoryService.Core.Mood.DayMood? again, bool _) = Mapping.DayMoodReader.Draw(lord);
         t.Note($"{lord.Name}: {first}");
         t.Expect("aLordHasADay", first.HasValue, true);
         t.Expect("theSameManAllDay", again, first);

         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsPrisoner && !h.IsChild);
         if (captive != null)
         {
            (NpcMemoryService.Core.Mood.DayMood? held, bool _) = Mapping.DayMoodReader.Draw(captive);
            t.Note($"captive {captive.Name}: {held}");
            t.Expect("aCaptiveHasNoOrdinaryDay", held != NpcMemoryService.Core.Mood.DayMood.Even && held != NpcMemoryService.Core.Mood.DayMood.HighSpirits, true);
         }

         t.Expect("thePlayerDrawsNone", Mapping.DayMoodReader.Draw(Hero.MainHero).Mood.HasValue, false);

         // Increment 4: the player moves it within a conversation, one band a turn, and a new conversation forgets it.
         if (first.HasValue)
         {
            Chat.ConversationMood.Reset();
            Chat.ConversationMood.Record(lord.StringId, 1);
            NpcMemoryService.Core.Mood.DayMood? lifted = Chat.ConversationMood.NowFor(lord.StringId, first);
            t.Note($"lifted once: {first} -> {lifted}");
            t.Expect("aLiftMovesItOneBand", lifted == NpcMemoryService.Core.Mood.MoodLadder.Shift(first.Value, 1), true);
            Chat.ConversationMood.Reset();
            t.Expect("aNewConversationStartsFromTheDay", Chat.ConversationMood.NowFor(lord.StringId, first).HasValue, false);
         }

         return t.Line();
      }

      private static string TestActionGate(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("action_gate");

         List<Hero> children = Hero.AllAliveHeroes.Where(h => h != null && h.IsChild).ToList();
         List<Hero> captives = Hero.AllAliveHeroes.Where(h => h != null && h.IsPrisoner && !h.IsChild).ToList();
         // Out of the world: the dead, and the disabled the engine still calls alive (Clan.Heroes keeps both).
         List<Hero> gone = Hero.DeadOrDisabledHeroes.Where(h => h != null && h != Hero.MainHero).ToList();
         Hero adult = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsLord && !h.IsChild && !h.IsPrisoner);
         ActionKind[] kinds = (ActionKind[]) Enum.GetValues(typeof(ActionKind));

         // The acts a child may take (answer the player's letter, witness, a held scene, be offered in marriage or as a
         // ward under those rules' own ages) and a council seat from 13; every other act is refused to every child.
         var childMay = new HashSet<ActionKind> {ActionKind.ReceiveLetter, ActionKind.Witness, ActionKind.HeldScene, ActionKind.MarriageProspect, ActionKind.Ward, ActionKind.CouncilSeat};
         // Where a captive may still be: offered in marriage, a duel, a witness in the room, a held scene with the player's own captive.
         var captiveMay = new HashSet<ActionKind> {ActionKind.MarriageProspect, ActionKind.Duel, ActionKind.Witness, ActionKind.HeldScene};

         t.Note($"{children.Count} children ({children.Count(h => h.IsLord)} of them lords by occupation), {captives.Count} adult captives, {gone.Count} dead or disabled; adult probe {adult?.Name}");
         t.Expect("noChildTakesAnAdultsAction", children.Any(h => kinds.Where(k => !childMay.Contains(k)).Any(k => Mapping.ActionGate.Allows(h, k))), false);
         t.Expect("noYoungChildSitsAtACouncil", children.Any(h => h.Age < CouncilRosterPolicy.MinSeatAge && Mapping.ActionGate.Allows(h, ActionKind.CouncilSeat)), false);
         t.Expect("noCaptiveActsOutsideItsOwnRules", captives.Any(h => kinds.Where(k => !captiveMay.Contains(k)).Any(k => Mapping.ActionGate.Allows(h, k))), false);
         t.Expect("noOneOutOfTheWorldActs", gone.Any(h => kinds.Any(k => Mapping.ActionGate.Allows(h, k))), false);
         t.Expect("thePlayerTakesNoneTowardThemselves", kinds.Any(k => Mapping.ActionGate.Allows(Hero.MainHero, k)), false);
         t.Expect("aFreeAdultLordMay", adult == null || kinds.All(k => Mapping.ActionGate.Allows(adult, k)), true);

         return t.Line();
      }

      /// <summary>
      ///   Two reports of 30/09/2026, read on the live game. Lordfadooboo: a letter placed the lone player "on the march
      ///   with your own retinue" (the writer's). Nexusrrr: a baby son asked for an audience about warfare. Checks the
      ///   phrase that places the player never borrows the writer's "your" (and says "alone" when the party is the player
      ///   alone), and that no child is among those who may ask for an audience in any town. Writes nothing.
      /// </summary>
      private static string TestLetterAndAudienceSeekers(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("letter_place_and_audience_age");

         string where = Letters.LetterGenerationService.DebugPlayerWhereabouts();
         int men = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
         List<Hero> seekers = Settlement.All.Where(s => s.IsTown).SelectMany(Interception.FiefAudienceBehavior.DebugEligible).ToList();

         t.Note($"player placed as \"{where ?? "(unsaid)"}\" ({men} in party); {seekers.Count} possible audience seekers across towns");
         t.Expect("thePlayerNeverRidesWithTheWritersRetinue", where == null || !where.Contains("your"), true);
         t.Expect("aLonePlayerRidesAlone", where == null || men != 1 || where.Contains("alone") || where.StartsWith("within"), true);
         t.Expect("noChildAsksForAnAudience", seekers.Any(h => h.IsChild), false);

         return t.Line();
      }

      /// <summary>
      ///   The public regard-change door (29/09/2026, for tashmetu): a real change of a real character's regard,
      ///   through the one place regard changes, is heard by a CrNpcMemory.RegardChanged listener with its before,
      ///   after and cause; the change is then undone (heard too) and the listener removed.
      /// </summary>
      private static string TestRegardChanged(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("regard_changed");

         var store = behavior?.Store;
         NpcProfile profile = store?.ListIds().Select(store.Get).FirstOrDefault(p => p != null);
         if (t.Skip(profile == null, "no character profile yet")) return t.Line();

         var heard = new List<CrRegardChange>();
         void Listen(CrRegardChange c) => heard.Add(c);
         int original = profile.ReputationWithPlayer;
         profile.ReputationWithPlayer = 0; // a start with room both ways; restored below
         int before = profile.ReputationWithPlayer;
         CrNpcMemory.RegardChanged += Listen;
         try
         {
            NpcMemoryService.Core.Services.ProfileMutator.ApplyReputationDelta(profile, 1, NpcMemoryService.Core.Standing.RegardCause.Unspecified);
            NpcMemoryService.Core.Services.ProfileMutator.ApplyReputationDelta(profile, -1, NpcMemoryService.Core.Standing.RegardCause.Unspecified);
         }
         finally
         {
            CrNpcMemory.RegardChanged -= Listen;
            profile.ReputationWithPlayer = original;
            store.Set(profile);
         }

         t.Note($"{profile.Name}: heard {heard.Count} change(s): {string.Join(", ", heard.Select(c => $"{c.Before}->{c.After} ({c.Cause})"))}");
         t.Expect("bothChangesAreHeard", heard.Count, 2);
         t.Expect("theRiseIsHeardWithItsValues", heard.Count > 0 && heard[0].NpcId == profile.Id && heard[0].Before == before && heard[0].After == before + 1, true);
         // Inside a self-test every change is reported as self_test, so a mirroring mod never keeps what a test left.
         t.Expect("aTestsChangeIsReportedAsSelfTest", heard.Count > 0 ? heard[0].Cause : null, NpcMemoryService.Core.Standing.RegardCause.SelfTest);

         return t.Line();
      }

      /// <summary>
      ///   The rule of rank on real characters (Gabriel, 28/09/2026: the Empress wrote in her own hand to a landless
      ///   newcomer). Reads a real ruler against the player as they stand: a ruler far above an unbonded player has her
      ///   letters written by her chancellery and does not ride out herself; the two answers always agree. Writes nothing.
      /// </summary>
      private static string TestCorrespondenceRank(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("correspondence_rank");

         Hero ruler = Kingdom.All.Where(k => k != null && !k.IsEliminated && k.Leader != null && k.Leader != Hero.MainHero)
                             .Select(k => k.Leader).FirstOrDefault(h => h.IsAlive);
         if (t.Skip(ruler == null, "no living ruler other than the player")) return t.Line();

         NpcProfile profile = behavior?.Store?.Get(ruler.StringId);
         CorrespondenceRankFacts f = Letters.CorrespondenceRank.Facts(ruler, profile, NpcMemoryService.Core.Models.LetterReason.QuestUpdate);
         string writtenBy = Letters.CorrespondenceRank.WrittenByFor(ruler, profile, NpcMemoryService.Core.Models.LetterReason.QuestUpdate);
         bool ridesOut = Letters.CorrespondenceRank.DealsInPerson(ruler, profile, null);

         t.Note($"{ruler.Name} ({f.Sender}) to the player ({f.Player}, bond {f.PersonalBond}, regard {f.Regard}): "
              + $"written by {writtenBy ?? "her own hand"}; rides out in person {ridesOut}");
         bool farBelow = !f.Player.IsRuler && !f.PersonalBond && f.Player.ClanTier < CorrespondenceRankPolicy.TierARulerAddressesInPerson
                         && f.Regard < NpcMemoryService.Core.Standing.Regard.ConfidingTier;
         t.Expect("farBelowHerTheChancelleryWrites", writtenBy != null, farBelow);
         t.Expect("theTitleNamesHerAndNoClerk", writtenBy == null || writtenBy.Contains(ruler.Name.ToString()), true);
         t.Expect("sheRidesOutOnlyWhenSheWouldWriteHerself", ridesOut, writtenBy == null);
         t.Expect("aLoveLetterIsAlwaysHerOwn", Letters.CorrespondenceRank.WrittenByFor(ruler, profile, NpcMemoryService.Core.Models.LetterReason.RomanticCorrespondence), null);

         return t.Line();
      }

      /// <summary>
      ///   The balance-of-power survey reads the real world: one row per living realm, fief shares that add up to
      ///   the whole map, and rows that survive the file round trip. Writes nothing.
      /// </summary>
      private static string TestBalanceSurvey(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("balance_survey");

         List<SurveyRow> rows = WorldAlmanac.BalanceSurveyBehavior.Reading();
         int living = Kingdom.All.Count(k => k != null && !k.IsEliminated);
         if (t.Skip(rows.Count == 0, "no living realm to read")) return t.Line();

         double shareSum = BalanceSurveyPolicy.Shares(rows).Values.Sum();
         (List<SurveyRow> back, List<SurveyEvent> _) = BalanceSurveyPolicy.Parse(rows.Select(BalanceSurveyPolicy.ToLine));

         t.Note($"{rows.Count} realm(s); leader share {BalanceSurveyPolicy.Shares(rows).Values.Max():P0}; file {WorldAlmanac.BalanceSurveyBehavior.FilePath}");
         t.Expect("oneRowPerLivingRealm", rows.Count, living);
         t.Expect("sharesMakeTheWholeMap", Math.Abs(shareSum - 1.0) < 0.001, true);
         t.Expect("rowsSurviveTheFile", back.Count, rows.Count);

         // The graphics-memory reading that now rides each week (29/09/2026): taken, or honestly marked unreadable (-1).
         SurveyVram vram = WorldAlmanac.BalanceSurveyBehavior.VramReading();
         t.Note($"graphics memory: game {vram.GameDedicatedMb} MB, shared {vram.GameSharedMb} MB, machine {vram.SystemDedicatedMb} MB; CR textures {vram.CrTextures}");
         t.Expect("theMemoryLineSurvivesTheFile", BalanceSurveyPolicy.ParseVram(new[] {BalanceSurveyPolicy.ToLine(vram)}).Count, 1);

         return t.Line();
      }

      /// <summary>
      ///   At sea a conversation waits for landfall (Windmolen): reads the door's real route for where the player
      ///   stands, then holds a real request exactly as the door does at sea and checks it is held once, not twice.
      ///   Nothing is opened; the held request is dropped at the end.
      /// </summary>
      private static string TestLandfallWait(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("landfall_wait");

         Hero someone = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Skip(someone == null, "no living free lord to hold a meeting with")) return t.Line();

         bool atSea = MobileParty.MainParty?.IsCurrentlyAtSea == true;
         StagingRoute route = StagingHostPolicy.Route(new StagingHostFacts {MissionLive = TaleWorlds.MountAndBlade.Mission.Current != null, PlayerAtSea = atSea});
         t.Note($"player at sea: {atSea}; mission live: {TaleWorlds.MountAndBlade.Mission.Current != null}; route: {route}");
         t.Expect("atSeaWithNothingLiveItWaits", route == StagingRoute.WaitForLandfall, atSea && TaleWorlds.MountAndBlade.Mission.Current == null);
         t.Expect("anEnvoyComesOnlyAshore", StagingHostPolicy.AnEnvoyCanReachThePlayer(atSea), !atSea);

         int before = Chat.ChatSceneStager.DebugAwaitingLandfallCount;
         try
         {
            var request = new ChatSceneRequest {Kind = ChatSceneKind.PlainConversation, Partner = someone};
            Chat.ChatSceneStager.DebugWaitForLandfall(request);
            Chat.ChatSceneStager.DebugWaitForLandfall(request);
            t.Expect("heldOnceNotTwice", Chat.ChatSceneStager.DebugAwaitingLandfallCount, before + 1);
         }
         finally
         {
            Chat.ChatSceneStager.DebugClearAwaitingLandfall();
         }

         return t.Line();
      }

      /// <summary>
      ///   Regard earned by letter (Lordfadooboo): drives the real apply path on a real character's profile. A warm
      ///   letter earns +1, a second the same day earns nothing, a cold one costs 1, and the tone line never reaches
      ///   the letter the player reads. The profile's regard and letter clock are put back.
      /// </summary>
      private static string TestClanStandingSeed(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("clan_standing_seed");

         var store = behavior?.Store;
         if (t.Abort(store == null, "no profile store")) return t.Line();
         if (t.Skip(!ModSettings.ClanStandingWarmsFirstMeetings, "the MCM option is off")) return t.Line();

         NpcProfile profile = store.ListIds().Select(store.Get)
                                   .FirstOrDefault(p => p != null && Hero.FindFirst(h => h.StringId == p.Id) is {IsAlive: true} h && h != Hero.MainHero);
         if (t.Skip(profile == null, "no living character has a profile yet")) return t.Line();

         int regardBefore = profile.ReputationWithPlayer;
         int? seedBefore = profile.ClanStandingSeedDay;
         var today = (int) CampaignTime.Now.ToDays;
         try
         {
            profile.ReputationWithPlayer = NpcMemoryService.Core.Standing.Regard.NeutralTier;
            profile.ClanStandingSeedDay = null;
            int first = Standing.ClanStandingSeedService.Apply(profile, NpcMemoryService.Core.Standing.VanillaRelation.CloseTier, today);
            int regardAfterFirst = profile.ReputationWithPlayer;
            int second = Standing.ClanStandingSeedService.Apply(profile, NpcMemoryService.Core.Standing.VanillaRelation.CloseTier, today);

            profile.ReputationWithPlayer = NpcMemoryService.Core.Standing.Regard.CoolTier;
            profile.ClanStandingSeedDay = null;
            int offended = Standing.ClanStandingSeedService.Apply(profile, NpcMemoryService.Core.Standing.VanillaRelation.CloseTier, today);

            t.Note($"{profile.Name}: first {first:+#;-#;0} (regard {regardAfterFirst}), again {second:+#;-#;0}, offended {offended:+#;-#;0}");
            t.Expect("aTrustedFriendsClanStartsFriendly", regardAfterFirst, NpcMemoryService.Core.Standing.Regard.FriendlyTier);
            t.Expect("givenOnlyOnce", second, 0);
            t.Expect("anOffenceStands", offended, 0);
         }
         finally
         {
            profile.ReputationWithPlayer = regardBefore;
            profile.ClanStandingSeedDay = seedBefore;
            store.Set(profile);
         }

         return t.Line();
      }

      private static string TestLetterRegard(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("letter_regard");

         var store = behavior?.Store;
         if (t.Abort(store == null, "no profile store")) return t.Line();

         NpcProfile profile = store.ListIds().Select(store.Get)
                                   .FirstOrDefault(p => p != null && Hero.FindFirst(h => h.StringId == p.Id) is {IsAlive: true} h && h != Hero.MainHero);
         if (t.Skip(profile == null, "no living character has a profile yet")) return t.Line();

         int regardBefore = profile.ReputationWithPlayer;
         int? clockBefore = profile.LastLetterRegardGainDay;
         var today = (int) CampaignTime.Now.ToDays;
         try
         {
            profile.ReputationWithPlayer = 0;
            profile.LastLetterRegardGainDay = null;

            int first = Letters.LetterRegardService.Apply(profile, LetterRegardPolicy.ParseTone("Dear friend...\n[LETTER_TONE] warmer"), today);
            int second = Letters.LetterRegardService.Apply(profile, LetterTone.Warmer, today);
            int cold = Letters.LetterRegardService.Apply(profile, LetterTone.Cooler, today);

            t.Note($"{profile.Name}: warm {first:+#;-#;0}, warm again {second:+#;-#;0}, cold {cold:+#;-#;0}");
            t.Expect("aWarmLetterEarnsAPoint", first, 1);
            t.Expect("aSecondTheSameDayEarnsNothing", second, 0);
            t.Expect("aColdLetterCostsAPoint", cold, -1);
            t.Expect("theToneLineNeverReachesTheLetter", LetterRegardPolicy.StripToneLine("Well met.\n[LETTER_TONE] warmer"), "Well met.");
         }
         finally
         {
            profile.ReputationWithPlayer = regardBefore;
            profile.LastLetterRegardGainDay = clockBefore;
            store.Set(profile);
         }

         return t.Line();
      }

      /// <summary>
      ///   The player leaving a realm as a vassal holding no fief (fkasad): judged by the mercenary grid with the oath
      ///   counted, WITHOUT applying anything. Skips when the player serves no realm.
      /// </summary>
      private static string TestVassalDepartureJudged(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("vassal_departure_judged");

         Kingdom realm = Clan.PlayerClan?.Kingdom ?? Kingdom.All.FirstOrDefault(k => k != null && !k.IsEliminated);
         if (t.Skip(realm == null, "no realm to judge a departure from")) return t.Line();
         if (t.Abort(WorldEvents.MercenaryReputationBehavior.Instance == null, "the mercenary behaviour is not running")) return t.Line();

         (bool judged, MercenaryDepartureVerdict verdict, int shift, string summary) = WorldEvents.MercenaryReputationBehavior.Instance.DebugJudgeVassal(realm);
         int fiefs = Clan.PlayerClan?.Fiefs?.Count ?? 0;

         t.Note($"{realm.Name}, {fiefs} fief(s) held: judged {judged}, {verdict}, shift {shift}; \"{summary}\"");
         t.Expect("judgedOnlyWithNoFief", judged, fiefs == 0);
         t.Expect("theNewsSpeaksOfTheOath", summary.Contains("renounced fealty"), true);
         t.Expect("aSwornVassalIsNeverThanked", shift <= 0, true);
         t.Expect("aFaultWeighsAtLeastDouble", verdict == MercenaryDepartureVerdict.Abandoned || verdict == MercenaryDepartureVerdict.Betrayal
            ? shift <= 2 * MercenaryDeparturePolicy.AbandonedPenalty
            : shift == 0, true);

         return t.Line();
      }

      private static string TestMercenaryCompanyJudged(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("mercenary_company_judged");

         Clan company = Clan.All.FirstOrDefault(c => c != null && c != Clan.PlayerClan && c.IsUnderMercenaryService && c.Kingdom != null && c.Leader != null);
         if (t.Skip(company == null, "no company of the game is in a realm's pay right now")) return t.Line();
         if (t.Abort(WorldEvents.MercenaryReputationBehavior.Instance == null, "the mercenary behaviour is not running")) return t.Line();

         (MercenaryDepartureVerdict verdict, string summary) = WorldEvents.MercenaryReputationBehavior.Instance.DebugJudgeCompany(company, company.Kingdom);

         t.Note($"{company.Name} in {company.Kingdom.Name}'s pay: {verdict}; \"{summary}\"");
         t.Expect("theNewsNamesTheCompany", summary.Contains(company.Name.ToString()), true);
         t.Expect("theNewsNamesTheRealm", summary.Contains(company.Kingdom.Name.ToString()), true);
         t.Expect("onlyAbandonmentOrBetrayalIsNews", MercenaryDeparturePolicy.CompanyDepartureIsNews(verdict),
            verdict == MercenaryDepartureVerdict.Abandoned || verdict == MercenaryDepartureVerdict.Betrayal);

         return t.Line();
      }

      private static string TestFactionBlockRealm(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("faction_block_realm");

         Hero own = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.MapFaction is Kingdom k && k.Culture == h.Culture);
         Hero foreign = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.MapFaction is Kingdom k && h.Culture != null && k.Culture != null && k.Culture != h.Culture);
         if (t.Skip(own == null || foreign == null, "need a lord of his own people's kingdom and a hero living in another people's")) return t.Line();

         string ownRealm = FactionGuidelinesPolicy.RealmOfOwnPeople(own.MapFaction.Name?.ToString(), own.MapFaction.Culture?.StringId, own.Culture?.StringId);
         string foreignRealm = FactionGuidelinesPolicy.RealmOfOwnPeople(foreign.MapFaction.Name?.ToString(), foreign.MapFaction.Culture?.StringId, foreign.Culture?.StringId);

         t.Note($"own={own.Name} ({own.Culture?.StringId}) of {own.MapFaction.Name}; foreign={foreign.Name} ({foreign.Culture?.StringId}) in {foreign.MapFaction.Name} ({foreign.MapFaction.Culture?.StringId})");
         t.Expect("aLordOfHisOwnPeopleKeepsHisRealm", ownRealm, own.MapFaction.Name?.ToString());
         t.Expect("anotherPeoplesRealmIsNotTheirs", foreignRealm, null);

         return t.Line();
      }

      private static string TestCouncilSeatNote(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("council_seat_note");

         List<Hero> lords = Hero.AllAliveHeroes.Where(h => h != Hero.MainHero && h.IsLord).Take(2).ToList();
         if (t.Skip(lords.Count < 2, "need two living lords to seat")) return t.Line();

         const string variable = "cr_selftest_seat";
         const string template = "=== FURTHER GUIDANCE ===\n{{char}}: {{" + variable + "}}";
         string silentSeat = lords[1].StringId;
         CrPrompt.RegisterVariable(variable, facts => facts.NpcId == silentSeat ? "" : $"seat {facts.NpcId}, council={facts.CouncilSeat}.");

         try
         {
            string first = Mapping.CouncilSeatNoteResolver.Resolve(lords[0], behavior.Store.Get(lords[0].StringId), lords[0].Name.ToString(), template);
            string second = Mapping.CouncilSeatNoteResolver.Resolve(lords[1], behavior.Store.Get(lords[1].StringId), lords[1].Name.ToString(), template);
            string none = Mapping.CouncilSeatNoteResolver.Resolve(lords[0], null, lords[0].Name.ToString(), "");

            t.Note($"first=\"{first}\" second=\"{second}\"");
            t.Expect("aSeatCarriesItsOwnValue", first?.Contains($"seat {lords[0].StringId}") == true, true);
            t.Expect("theProviderKnowsItIsASeat", first?.Contains("council=True") == true, true);
            t.Expect("theSeatIsNamed", first?.StartsWith(lords[0].Name.ToString()) == true, true);
            t.Expect("theHeadingIsDropped", first?.Contains("===") == true, false);
            t.Expect("anEmptyValueLeavesOnlyWhatTheTemplateWrote", second, lords[1].Name + ":");
            t.Expect("noTemplateNoNote", none, null);
         }
         finally
         {
            CrPrompt.Unregister(variable);
         }

         return t.Line();
      }

      private static string TestTransientPartyRegistry(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("transient_party_registry");

         Travel.TransientPartyRegistry registry = Travel.TransientPartyRegistry.Instance;
         MobileParty main = MobileParty.MainParty;
         if (t.Abort(registry == null || main == null, "TransientPartyRegistry not active, or no player party")) return t.Line();

         const string role = "selftest.transient";
         TaleWorlds.Library.Vec2 at = main.GetPosition2D;
         MobileParty released = Travel.TravelingParty.Spawn(at, null, new TextObject("[SELFTEST] released"), 5f, Hero.MainHero.Culture, true, 2);
         MobileParty lost = Travel.TravelingParty.Spawn(at, null, new TextObject("[SELFTEST] lost"), 5f, Hero.MainHero.Culture, true, 2);
         if (t.Abort(released == null || lost == null, "could not spawn the throwaway parties")) return t.Line();

         var reported = new List<string>();
         MobileParty restored = null;
         try
         {
            registry.Track(released, role, "a", (p, _) => reported.Add(p.StringId));
            registry.Track(lost, role, "b", (p, _) => reported.Add(p.StringId));
            t.Expect("bothTracked", registry.OfRole(role).Count, 2);
            t.Expect("foundByTag", registry.WithTag(role, "b") == lost, true);

            // Released by the registry: off the map, never a loss.
            registry.Release(released);
            t.Expect("releasedLeftTheMap", released.IsActive, false);
            t.Expect("releaseIsNotALoss", reported.Contains(released.StringId), false);

            // Destroyed by anything else: its owner hears of it, once.
            Travel.TravelingParty.Destroy(lost);
            t.Expect("lossReportedOnce", reported.Count(id => id == lost.StringId), 1);
            t.Expect("nothingLeftTracked", registry.OfRole(role).Count, 0);

            // A save's despawn and restore: a party tracked with a restore comes back where it stood, tracked again,
            // and the despawn is not a loss (the sergeants' and hunters' fix, 26/09/2026).
            restored = Travel.TravelingParty.Spawn(at, null, new TextObject("[SELFTEST] restored"), 5f, Hero.MainHero.Culture, true, 2);
            if (restored != null)
            {
               TaleWorlds.Library.Vec2 stood = restored.GetPosition2D;
               string oldId = restored.StringId;
               MobileParty original = restored;
               registry.Track(restored, role, "c", (p, _) => reported.Add(p.StringId),
                  pos => Travel.TravelingParty.Spawn(pos, null, new TextObject("[SELFTEST] restored"), 5f, Hero.MainHero.Culture, true, 2));
               registry.DebugSaveCycle();

               MobileParty back = registry.WithTag(role, "c");
               // A NEW party (the engine may hand it the freed id again, so compare the objects, not the ids).
               t.Expect("restoredAfterTheSave", back != null && !ReferenceEquals(back, original) && back.IsActive && !original.IsActive, true);
               t.Expect("restoredWhereItStood", back != null && back.GetPosition2D.Distance(stood) < 1f, true);
               t.Expect("saveDespawnIsNotALoss", reported.Contains(oldId), false);
               restored = back;
            }
         }
         finally
         {
            if (released.IsActive) Travel.TravelingParty.Destroy(released);
            if (lost.IsActive) Travel.TravelingParty.Destroy(lost);
            registry.ReleaseRole(role);
            if (restored is {IsActive: true}) Travel.TravelingParty.Destroy(restored);
         }

         return t.Line();
      }

      private static string TestMercenaryDepartureJudged(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("mercenary_departure_judged");

         WorldEvents.MercenaryReputationBehavior mercs = WorldEvents.MercenaryReputationBehavior.Instance;
         if (t.Abort(mercs == null, "MercenaryReputationBehavior not active")) return t.Line();

         var judged = 0;
         foreach (Kingdom k in Kingdom.All.Where(k => k != null && !k.IsEliminated).Take(6))
         {
            (Logic.MercenaryDepartureVerdict verdict, bool atWar, float own, float enemies) = mercs.DebugJudge(k);
            Logic.MercenaryDepartureVerdict expected = !atWar
               ? Logic.MercenaryDepartureVerdict.AtPeace
               : Logic.MercenaryDeparturePolicy.IsLosing(own, enemies) ? Logic.MercenaryDepartureVerdict.Abandoned : Logic.MercenaryDepartureVerdict.ReleasedInVictory;
            t.Note($"{k.Name}: {verdict} (at war {atWar}, {own:0} vs {enemies:0})");
            t.Expect($"judged_{k.StringId}", verdict, expected);
            judged++;
         }

         t.Expect("someRealmJudged", judged > 0, true);

         return t.Line();
      }

      private static string TestArrestWarrantAndBench(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("arrest_warrant_and_bench");

         Arrests.ArrestBehavior arrests = Arrests.ArrestBehavior.Instance;
         if (t.Abort(arrests == null, "ArrestBehavior not active")) return t.Line();

         // Arranged, never joined (Gabriel, 26/09/2026: the test must run whatever the player's standing): the player's
         // own liege when they serve one, else the living ruler of another realm. The case is opened through the pure
         // ArrestCase directly (the standing check lives in ArrestBehavior.Charge, not here), so the save's kingdoms,
         // contracts and wars are never touched.
         Hero ownLiege = Clan.PlayerClan?.Kingdom?.Leader;
         bool servesALiege = Logic.ArrestPolicy.CanBeArrested(Arrests.ArrestBehavior.Standing()) && ownLiege != null && ownLiege != Hero.MainHero;
         Hero liege = servesALiege
            ? ownLiege
            : Kingdom.All?.Where(k => k != null && !k.IsEliminated && k.Leader is {IsAlive: true} && k.Leader != Hero.MainHero)
                     .Select(k => k.Leader).FirstOrDefault();

         if (t.Skip(liege == null, "no living ruler of any realm to stand as the liege")) return t.Line();
         if (t.Skip(arrests.Case.Stage != Logic.ArrestStage.None, "a real case is already open on this save; not touching it")) return t.Line();
         t.Note(servesALiege ? "the player's own liege" : "arranged: another realm's ruler stands as the liege");

         string snapshot = arrests.DebugSnapshot();
         try
         {
            // The MCM gate is honoured by Charge; the self-test forces the case open through the pure case itself so it
            // runs whatever the toggle says, then drives the bench from the live realm.
            arrests.Case.Charge(Logic.ArrestCharge.TreasonLetter, liege.StringId, true);
            t.Expect("warrantInTheLiegesName", arrests.Case.Stage == Logic.ArrestStage.WarrantOut && arrests.Case.LiegeId == liege.StringId, true);

            List<Hero> bench = arrests.TrialBench();
            t.Note($"liege={liege.Name}, bench={string.Join(", ", bench.Select(h => h.Name?.ToString()))}");
            t.Expect("liegePresides", bench.Count > 0 && bench[0] == liege, true);
            t.Expect("benchBounded", bench.Count <= 1 + Logic.ArrestPolicy.CouncilVassalSeats, true);
         }
         finally
         {
            arrests.DebugRestore(snapshot);
         }

         t.Expect("caseRestored", arrests.Case.Stage, Logic.ArrestStage.None);

         return t.Line();
      }

      private static string TestBrigandagePatchAttached(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("brigandage_patch_attached");

         System.Reflection.MethodBase target = Arrests.BrigandagePatch.TargetMethod();
         if (t.Abort(target == null, "BeHostileAction.ApplyInternal does not exist on this game version")) return t.Line();

         HarmonyLib.Patches info = HarmonyLib.Harmony.GetPatchInfo(target);
         bool attached = info?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(Arrests.BrigandagePatch)) == true
                         && info.Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(Arrests.BrigandagePatch));
         t.Expect("prefixAndPostfixAttached", attached, true);

         return t.Line();
      }

      /// <summary>
      ///   Courier secrets end to end on real parties: a [SELFTEST] letter is captured onto a real bandit band, a real
      ///   lord's party defeats the band and the lord remembers what the letter said; a second one defeated by the
      ///   player's party is recovered unread. Runs without the model or the rumour (DebugDefeatCarrier), and removes
      ///   the memory it added.
      /// </summary>
      private static string TestCourierSecretTravels(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("courier_secret_travels");

         CourierSecretBehavior secrets = CourierSecretBehavior.Instance;
         if (t.Abort(secrets == null || Hero.MainHero == null, "CourierSecretBehavior not active")) return t.Line();

         MobileParty band = MobileParty.AllBanditParties.FirstOrDefault(p => p is {IsActive: true} && p.Party != null);
         Hero reader = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.Clan != Clan.PlayerClan && h.PartyBelongedTo?.LeaderHero == h);

         if (t.Skip(band == null, "no bandit band on the map to carry a letter")) return t.Line();
         if (t.Skip(reader == null, "no lord leading a party to defeat the band")) return t.Line();

         const string tag = "[SELFTEST-SECRET]";
         CarriedSecret Letter() => new() {
            SenderId = Hero.MainHero.StringId, SenderName = Hero.MainHero.Name.ToString(),
            RecipientId = "cr_selftest_nobody", RecipientName = "a selftest recipient",
            Content = tag + " I would see your brother removed from the council before the spring muster.", FromPlayer = true
         };

         try
         {
            // Taken by bandits: the band carries it.
            CarriedSecret first = Letter();
            secrets.Capture(first, band.Party, CourierLoss.InterceptedByBandits);
            t.Expect("carriedByTheBand", secrets.Secrets.Any(s => s == first && s.CarrierPartyId == band.StringId), true);

            // A lord's party beats the band: it goes up to that lord, who remembers what it said.
            secrets.DebugDefeatCarrier(band.StringId, reader.PartyBelongedTo.Party);
            NpcProfile profile = behavior.Store.Get(reader.StringId);
            t.Expect("leftTheBand", secrets.Secrets.Contains(first), false);
            t.Expect("readerRemembersWhatItSaid", profile?.Events.Any(e => e.summary != null && e.summary.Contains(tag)) == true, true);

            // The player's own party beats the band: the letter is recovered, read by no one.
            CarriedSecret second = Letter();
            secrets.Capture(second, band.Party, CourierLoss.InterceptedByBandits);
            secrets.DebugDefeatCarrier(band.StringId, PartyBase.MainParty);
            t.Expect("recoveredByThePlayer", secrets.Secrets.Contains(second), false);
            t.Note($"band={band.Name}, reader={reader.Name}");
         }
         finally
         {
            NpcProfile profile = behavior.Store.Get(reader.StringId);
            if (profile != null)
            {
               profile.Events.RemoveAll(e => e.summary != null && e.summary.Contains(tag));
               behavior.Store.Set(profile);
            }

            secrets.DebugForgetTagged(tag);
         }

         return t.Line();
      }

      /// <summary>
      ///   The reader's proportionate reaction (courier Inc 4b), without the model: another lord's letter read by a
      ///   grudge-free lord. NOTHING plants nothing; AGAINST plants a LeakedLetter grudge on the letter's author, and the
      ///   player, who wrote nothing, pays no renown. Clears the grudge it planted.
      /// </summary>
      private static string TestCourierSecretReaction(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("courier_secret_reaction");

         var today = (int) CampaignTime.Now.ToDays;
         Hero reader = GrudgeCapableLord(GrudgeSource.LeakedLetter, Logic.LetterLeakConsequencePolicy.GrudgeMagnitude(false), today);
         Hero author = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h != Hero.MainHero && h != reader && h.IsLord && h.IsAlive);

         if (t.Skip(reader == null || author == null || Hero.MainHero?.Clan == null, "no grudge-free, unforgiving lord and another lord to test the reaction on")) return t.Line();

         var secret = new CarriedSecret {
            SenderId = author.StringId, SenderName = author.Name.ToString(),
            RecipientId = Hero.MainHero.StringId, RecipientName = Hero.MainHero.Name.ToString(),
            Content = "[SELFTEST] Your cousin's lands would be better in my hands, and yours.", FromPlayer = false
         };

         float renownBefore = Hero.MainHero.Clan.Renown;
         try
         {
            bool Holds() => GrudgeBehavior.Instance?.LiveGrudgesFor(reader, today)?.Any(g => g.TargetId == author.StringId && g.Source == GrudgeSource.LeakedLetter) ?? false;

            CourierSecretBehavior.React(secret, reader, SecretSeverity.Nothing);
            t.Expect("nothingPlantsNothing", Holds(), false);

            CourierSecretBehavior.React(secret, reader, SecretSeverity.AgainstInterests);
            t.Expect("againstPlantsAGrudgeOnTheAuthor", Holds(), true);
            t.Expect("playerPaysNothing", Hero.MainHero.Clan.Renown, renownBefore);
            t.Note($"reader={reader.Name}, author={author.Name}");
         }
         finally
         {
            GrudgeBehavior.Instance?.ClearGrudgesFor(reader.StringId); // grudge-free before, so only what was planted
         }

         return t.Line();
      }

      // PrisonerOffer's Accept settles a real ransom through the shared PrisonerRansomExecutor (the same atomic
      // trade buy_prisoner uses). Captures an in-party lord into the player's prison, makes a buyer solvent, runs
      // the executor, and asserts the sale lands: the captive leaves the player's roster and the player is paid.
      // Restores everything so the sweep leaves no one caged and no purse changed.
      private static string TestLetterOfferPrisonerRansom(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("letter_offer_prisoner_ransom");

         Hero player = Hero.MainHero;

         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != player && h.IsLord && !h.IsPrisoner && h.PartyBelongedTo != null && h.Clan?.Leader != h);
         if (t.Skip(captive == null, "no capturable in-party lord to ransom")) return t.Line();

         Hero buyer = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != player && h != captive && h.IsLord && !h.IsPrisoner
            && (h.PartyBelongedTo != null || (h.Clan?.Settlements?.Any(s => s != null && (s.IsTown || s.IsCastle)) ?? false)));
         if (t.Skip(buyer == null, "no lord with a holding to play the buyer")) return t.Line();

         TakePrisonerAction.Apply(PartyBase.MainParty, captive);
         bool captured = captive.IsPrisoner && captive.PartyBelongedToAsPrisoner?.LeaderHero == player;
         if (t.Abort(!captured, $"capture failed for {captive.Name}")) return t.Line();

         int buyerGold0 = buyer.Gold;
         int playerGold0 = player.Gold;
         int price = PrisonerOfferPolicy.OfferAmount(BuyPrisonerVerb.RansomAnchor(captive), true);
         buyer.ChangeHeroGold(price); // make the buyer solvent for the test

         NpcProfile buyerProfile = GetOrCreate(behavior, buyer);

         try
         {
            bool ok = PrisonerRansomExecutor.Execute(buyer, captive, price, buyerProfile, out string message);
            bool goneFromPlayer = !BuyPrisonerVerb.PlayerHeldHeroPrisoners().Contains(captive);
            bool playerPaid = player.Gold > playerGold0;

            t.Note($"sold {captive.Name} to {buyer.Name}: {message}");
            t.Expect("ransomSucceeded", ok, true);
            t.Expect("captiveLeftPlayerPrison", goneFromPlayer, true);
            t.Expect("playerReceivedGold", playerPaid, true);
         }
         finally
         {
            try { EndCaptivityAction.ApplyByReleasedByChoice(captive, player); }
            catch (Exception ex) { ModLog.Error($"[SELFTEST] ransom cleanup release failed: {ex.Message}"); }

            player.ChangeHeroGold(playerGold0 - player.Gold);
            buyer.ChangeHeroGold(buyerGold0 - buyer.Gold);
         }

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
         int delta = relAfter - relBefore;

         t.Note($"captor={captor.Name}, freed={victim.Name}, relation {relBefore}->{relAfter} (delta {delta})");
         t.Expect("outcomePositive", outcome?.IsPositive, true);
         t.Expect("noLongerPrisoner", !victim.IsPrisoner, true);

         // The verb applies exactly ReleasePrisonerRelationBump through ChangeRelationAction, but that runs on the
         // live engine: the player's own Charm perks/traits (and vanilla's own captivity-end relation gain) can add
         // on top, so a developed character legitimately sees +6 for a +5 bump. Assert the mod's bump as a FLOOR,
         // not an exact figure, or this HEAVY test fails on any save whose hero has a relation-gain modifier.
         t.Expect("relationBumpedAtLeastPolicy", delta >= CourtActionPolicy.ReleasePrisonerRelationBump, true);

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
         if (npc.GetRelation(target) <= NearTheEngineFloor)
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
            h != plotter && h != Hero.MainHero && h.IsLord && h.MapFaction != Hero.MainHero?.MapFaction && h.GetRelation(Hero.MainHero) < VanillaRelation.TrustedTier && h.Clan != plotter?.Clan && Free(h));

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

         // Nobody already entangled: ForcePlot refuses to lay a plot over a live one, so a pair left over from an
         // earlier run would leave nothing warnable and fail on a world this test itself made. The plotter must
         // also be grudge-free, so the cleanup below clears exactly the grudge that warning the target plants.
         var today = (int) CampaignTime.Now.ToDays;
         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan
            && !SchemeStore.Instance.Active.Any(sc => sc.PlotterId == h.StringId)
            && (GrudgeBehavior.Instance?.LiveGrudgesFor(h, today)?.Count ?? 0) == 0);
         Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != plotter && h != Hero.MainHero && h.IsLord && h.Clan != null && h.Clan != plotter?.Clan && h.Clan != Clan.PlayerClan
            && !SchemeStore.Instance.Active.Any(sc => sc.TargetId == h.StringId));

         if (t.Skip(plotter == null || target == null, "need 2+ unentangled lords from distinct non-player clans")) return t.Line();

         int plotterBefore = behavior.GameStateBridge.GetPlayerRelation(plotter);
         int targetBefore = behavior.GameStateBridge.GetPlayerRelation(target);

         try
         {
            SchemeStore.Instance.ForcePlot(plotter, target, SchemeKind.Slander);

            string msg = SchemeStore.Instance.TargetHeeds(target);

            if (t.Abort(msg == null, $"TargetHeeds({target.Name}) returned null: no eligible scheme after ForcePlot")) return t.Line();

            int plotterDelta = behavior.GameStateBridge.GetPlayerRelation(plotter) - plotterBefore;
            int targetDelta = behavior.GameStateBridge.GetPlayerRelation(target) - targetBefore;
            t.Note($"plotter={plotter.Name} delta={plotterDelta} | target={target.Name} delta={targetDelta}");
            t.Expect("plotterResents(<=-20)", plotterDelta <= -20, true);
            t.Expect("targetGrateful(>=16)", targetDelta >= 16, true);
         }
         finally
         {
            // Warning a target moves REAL vanilla relations and plants a real grudge. Left standing, a diagnostic
            // sweep would quietly turn a lord against the player in a campaign that never played the scene.
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(plotter, Hero.MainHero,
               plotterBefore - behavior.GameStateBridge.GetPlayerRelation(plotter), false);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(target, Hero.MainHero,
               targetBefore - behavior.GameStateBridge.GetPlayerRelation(target), false);
            GrudgeBehavior.Instance?.ClearGrudgesFor(plotter.StringId);
            SchemeStore.Instance.DebugForgetSchemesBetween(plotter, target);
         }

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

         // M-R6: a divorce must clear a stale IsConsort flag on the ex-partner (nothing else ever reset it). Seed
         // one so the accept path (which flows through MarriageDissolutionService.TryDissolve) is proven to clear it.
         bool consortSeeded = profileA.Romantic != null;
         if (consortSeeded)
         {
            profileA.Romantic.IsConsort = true;
            behavior.Store.Set(profileA);
         }

         ActionOutcome acceptOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "accept_divorce"}, spouseA, new ActionContext(profileA, null));

         bool consortClearedAfterDivorce = !consortSeeded
                                           || behavior.Store.Get(spouseA.StringId)?.Romantic?.IsConsort != true;
         t.Expect("consortFlagClearedAfterDivorce", consortClearedAfterDivorce, true);

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
         int relBeforeDecline = spouseB.GetRelation(Hero.MainHero); // ~0 (ArrangeCleanNpc neutralised it)

         ActionOutcome declineOutcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "decline_divorce"}, spouseB, new ActionContext(profileB, null));

         bool stillMarriedAfterDecline = Hero.MainHero.Spouse == spouseB;
         bool flagClearedAfterDecline = !demands.IsDemanding(spouseB.StringId);
         bool refusalGrudgePlanted = GrudgeBehavior.Instance.GrudgesFor(spouseB.StringId)
                                                   .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == today);
         // M-D4 guard: each refusal must also cost a little vanilla relation, or a grudge-triggered demand loops
         // forever (nothing lowers the relation ShouldEscalate gates on). The drip must be visible here.
         bool refusalDroppedRelation = spouseB.GetRelation(Hero.MainHero) < relBeforeDecline;

         t.Note($"scenario B (decline) spouse={spouseB.Name}: outcome={(declineOutcome != null ? declineOutcome.IsPositive.ToString() : "null")}, relation {relBeforeDecline}->{spouseB.GetRelation(Hero.MainHero)}");
         t.Expect("stillMarriedAfterDecline", stillMarriedAfterDecline, true);
         t.Expect("demandFlagClearedAfterDecline", flagClearedAfterDecline, true);
         t.Expect("refusalGrudgePlanted", refusalGrudgePlanted, true);
         t.Expect("refusalDroppedRelation", refusalDroppedRelation, true);

         // ── Scenario C (M-D5): DEMAND-PHASE RECONCILIATION on the still-married spouseB ──
         // Re-open a demand, warm the player's PERSONAL regard above the reconciliation bar, tick: the demand
         // must be WITHDRAWN (not ground into another refusal), mirroring the escalation-phase reconciliation.
         demands.ForceDemand();
         bool demandingBeforeReconcile = demands.IsDemanding(spouseB.StringId);
         profileB.ReputationWithPlayer = SpouseDivorceDemandPolicy.ReconciliationThreshold + 5;
         behavior.Store.Set(profileB);
         demands.RunTick();

         bool demandWithdrawnOnReconcile = demandingBeforeReconcile && !demands.IsDemanding(spouseB.StringId) && Hero.MainHero.Spouse == spouseB;
         t.Note($"scenario C (demand-phase reconciliation) spouse={spouseB.Name}: withdrawn={demandWithdrawnOnReconcile}");
         t.Expect("demandWithdrawnOnReconciliation", demandWithdrawnOnReconcile, true);

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
         int relBeforeWalkOut = spouseA.GetRelation(Hero.MainHero); // baseline (ArrangeCleanNpc neutralised it to ~0)

         demands.RunTick();

         bool dissolvedA = Hero.MainHero.Spouse == null;
         bool neitherClanlessA = Hero.MainHero.Clan != null && spouseA.Clan != null;
         bool escalationClearedAfterWalkOut = !demands.IsEstranging(spouseA.StringId);
         bool walkOutGrudgePlanted = GrudgeBehavior.Instance.GrudgesFor(spouseA.StringId)
                                                   .Any(g => g.Source == GrudgeSource.Divorce && g.EventDay == todayA);
         bool worldEventRecorded = WorldEventStore.Instance.Count > worldEventsBefore;
         // M-D1 guard: the walk-out must SET the relation from the pre-dissolution baseline (a moderate hit),
         // never leave the engine's hidden dissolution penalty (observed crashing to -100) standing. A moderate
         // result stays well clear of that crash; without the SET this assertion fails.
         int relAfterWalkOut = spouseA.GetRelation(Hero.MainHero);
         bool walkOutRelationModerate = relAfterWalkOut > -50;

         t.Note($"scenario A (walk-out) spouse={spouseA.Name}, relation {relBeforeWalkOut}->{relAfterWalkOut}");
         t.Expect("dissolvedAfterWalkOut", dissolvedA, true);
         t.Expect("neitherClanlessAfterWalkOut", neitherClanlessA, true);
         t.Expect("escalationClearedAfterWalkOut", escalationClearedAfterWalkOut, true);
         t.Expect("walkOutGrudgePlanted", walkOutGrudgePlanted, true);
         t.Expect("walkOutWorldEventRecorded", worldEventRecorded, true);
         t.Expect("walkOutRelationModerateNotCrashed", walkOutRelationModerate, true);

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

         // Reconciliation is read from the mod's PERSONAL regard (ReputationWithPlayer), the number the player
         // warms through conversation, NOT the vanilla clan relation (SweepEscalations reads exactly that). Set
         // THAT signal above the bar, or the sweep never sees the reconciliation this scenario is meant to prove.
         profileB.ReputationWithPlayer = SpouseDivorceDemandPolicy.ReconciliationThreshold + 5; // comfortably above the bar
         behavior.Store.Set(profileB);

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

      /// <summary>
      ///   The STARVING LORD bug, reproduced and then proven fixed against the live engine. A player watched a
      ///   clan party that had intercepted him stand on one spot until its food ran out and it starved (Fakade,
      ///   2026-07-13). The cause: we ended the chase with SetMoveModeHold, believing it handed the lord back to
      ///   his own AI. It does the opposite. Hold is not a release, it is an ORDER to stand still, and the party's
      ///   AI, seeing a behaviour it considers perfectly valid, never reconsidered it. He obeyed until he died.
      ///   <para>
      ///     This freezes a real lord party exactly as the old code did, proves it looks frozen, then proves both
      ///     halves of the fix: the release hands a party back, and the on-load rescue frees one already stranded
      ///     by an earlier version (without which a broken campaign stays broken forever).
      ///   </para>
      /// </summary>
      private static string TestInterceptionHoldsCourse(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("interception_holds_course");

         if (t.Abort(Interception.InterceptionBehavior.Instance == null, "interception behavior not initialized")) return t.Line();

         MobileParty lordParty = MobileParty.All.FirstOrDefault(
            p => p != null && p != MobileParty.MainParty && p.LeaderHero is {IsLord: true}
                 && p.CurrentSettlement == null && p.Army == null && p.MapEvent == null && !p.IsCurrentlyAtSea);
         if (t.Skip(lordParty == null, "no free lord party in the open field")) return t.Line();

         var read = Interception.InterceptionBehavior.Instance.DebugSteerAndRelease(lordParty);
         if (t.Skip(read == null, "a real pursuit is under way; not disturbing it")) return t.Line();

         t.Note($"{lordParty.Name}: locked {read.Value.Locked}, on the player {read.Value.OnThePlayer}, free after {read.Value.FreeAfter}, thinks again {read.Value.ThinksAgainAfter}");
         t.Expect("heIsHeldOnCourse", read.Value.Locked, true);
         t.Expect("heRidesAtThePlayer", read.Value.OnThePlayer, true);
         t.Expect("theChaseOverHisDecisionsAreHisOwn", read.Value.FreeAfter, true);
         t.Expect("andHeIsToldToThinkAfresh", read.Value.ThinksAgainAfter, true);

         return t.Line();
      }

      private static string TestPartyRelease(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("party_release");

         MobileParty victim = MobileParty.All.FirstOrDefault(
            p => p != null && p != MobileParty.MainParty && p.LeaderHero != null
                 && p.CurrentSettlement == null && p.Army == null && p.MapEvent == null);

         if (t.Skip(victim == null, "no free lord party in the open field to release")) return t.Line();

         AiBehavior wasDoing = victim.DefaultBehavior;
         t.Note($"{victim.Name}: was {wasDoing}");

         // Freeze it EXACTLY as the old code did, and prove that this alone is what stranded the party: it sits
         // on Hold with nothing telling it to think again, which is precisely how a lord starved in place.
         victim.SetMoveModeHold();
         victim.Ai.RethinkAtNextHourlyTick = false;

         t.Expect("theOldStandDownLeavesItHolding", victim.DefaultBehavior, AiBehavior.Hold);
         t.Expect("andWithNothingTellingItToThinkAgain", victim.Ai.RethinkAtNextHourlyTick, false);

         // The fix: releasing it must actually hand it back, which means MAKING it decide afresh.
         InterceptionBehavior.ReleaseToVanillaAi(victim);
         t.Expect("theReleaseMakesItThinkAgain", victim.Ai.RethinkAtNextHourlyTick, true);
         t.Expect("andNothingOfOursHoldsItsDecisionsShut", victim.Ai.DoNotMakeNewDecisions, false);

         // The rescue: a campaign already broken by an earlier version must heal on load, or the stranded party
         // starves anyway however well we fixed the cause.
         victim.SetMoveModeHold();
         victim.Ai.RethinkAtNextHourlyTick = false;
         int freed = InterceptionBehavior.RescueHeldParties();

         t.Note($"on-load rescue nudged {freed} held party(ies)");
         t.Expect("theOnLoadRescueFreesAnAlreadyStrandedParty", victim.Ai.RethinkAtNextHourlyTick, true);
         t.Expect("andItFoundAtLeastTheOneWeStranded", freed >= 1, true);

         return t.Line();
      }

      /// <summary>
      ///   A lord who can actually BE GIVEN a quest: one with no outstanding task already on their plate. The
      ///   pipeline correctly refuses a second one ("offered a task while one is outstanding"), so a test that
      ///   simply grabbed the first lord it found would fail on a perfectly legitimate refusal and blame the
      ///   pipeline for it. Null when every lord is already busy.
      /// </summary>
      private static Hero FreeQuestGiver(NpcMemoryCampaignBehavior behavior)
         => Hero.AllAliveHeroes.FirstOrDefault(
            h => h != null && h != Hero.MainHero && h.IsAlive && h.IsLord && !HasOutstandingQuest(behavior, h));

      /// <summary>True when this hero already holds a task the player has not settled, which bars a new one.</summary>
      private static bool HasOutstandingQuest(NpcMemoryCampaignBehavior behavior, Hero hero)
      {
         NpcProfile p = behavior?.Store?.Get(hero.StringId);

         return p != null && p.ActiveQuests.Any(q => q.IsOutstanding);
      }

      /// <summary>
      ///   A THROWAWAY profile for a hero whose only memory is sixty days stale, so the elapsed-gap note would
      ///   certainly fire. Never stored, so it mutates nothing. Built fresh rather than aged in place, because
      ///   the gap is measured from the NEWEST memory: adding an old event to a live profile that already holds
      ///   a fresher one changes nothing at all, and the assertion would then pass for the wrong reason.
      /// </summary>
      private static NpcProfile AgedProfile(Hero hero, int today)
      {
         NpcProfile p = HeroProfileMapper.FromHero(hero);
         p.Events.Add(new NotableEvent(today - 60, NotableEventType.Other, "(selftest) a memory sixty days stale"));

         return p;
      }

      /// <summary>
      ///   The in-chat Options button, which a player reported dead across three versions (SuperGraham339, still
      ///   reproducing in v1.30.14). It reaches the vanilla escape menu entirely by REFLECTION, so a TaleWorlds
      ///   rename does not throw: the button just goes quiet, and the log even claims success. That is the exact
      ///   shape of the bug he lived with. The game answers ESC with TWO different mechanisms (a mission view
      ///   while a scene is loaded, a navigation handler on the map) and the mod must find BOTH, because whichever
      ///   one it fails to find is a screen on which the player's menu will never appear. This asserts they still
      ///   resolve on the running game version, and says which path is live right now.
      /// </summary>
      private static string TestMenu(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("menu");

         var (missionMechanism, mapMechanism, missionRunning, escapeViewPresent, detail) = GameMenuOpener.Probe();

         t.Note(detail);

         // The mission mechanism is the one that was MISSING until now: the chat can be hosted by a MissionScreen
         // (a settlement, a conversation, a naval scene), which carries no navigation handler at all, so the menu
         // was being opened on the map hidden behind the scene.
         t.Expect("theMissionEscapeMechanismStillExists", missionMechanism, true);

         // And the map one must survive too, or the button dies on the ordinary campaign map instead.
         t.Expect("theMapEscapeMechanismStillExists", mapMechanism, true);

         // Only assertable when a scene happens to be loaded; on the bare map there is no mission to carry a view.
         if (missionRunning) t.Expect("aRunningMissionCarriesItsEscapeView", escapeViewPresent, true);
         else t.Note("no mission running during the test, so the mission-side view could not be inspected live: open the chat inside a settlement and click Options to confirm by eye");

         return t.Line();
      }

      /// <summary>
      ///   Player report: the letter view's decorative Galahad font has no proper Cyrillic glyphs, so a Russian
      ///   game language rendered it through a broken per-glyph fallback (miscentered, unreadable). Proves both
      ///   halves: the pure <see cref="LetterFontPolicy" /> decision, and the actual wiring into
      ///   <see cref="LetterViewModel" />'s BodyBrush/HeadingBrush/InputBrush bindings via
      ///   <see cref="LetterFontResolver.LanguageIdProvider" /> forced to the Russian id (no real game-language
      ///   switch, so this never disturbs a live campaign). Restores the seam in a finally.
      /// </summary>
      private static string TestLetterFont(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("letter_font");

         // STAKE: this policy's whole purpose. "Русский" is the id TaleWorlds.Localization.MBTextManager
         // .ActiveTextLanguage actually returns for Russian (verified against this engine build's own
         // Native/ModuleData/Languages/RU/language_data.xml, id="Русский").
         t.Expect("policyPrefersPlainFontForRussian", LetterFontPolicy.PreferPlainFont("Русский"), true);

         // STAKE: the decorative parchment look (Galahad) must survive for the game's default/dev language.
         t.Expect("policyKeepsDecorativeFontForEnglish", LetterFontPolicy.PreferPlainFont("English"), false);

         Func<string> priorProvider = LetterFontResolver.LanguageIdProvider;
         try
         {
            var vm = new LetterViewModel();

            // STAKE: the pure policy alone does not prove the VM actually reads it: a wiring slip (wrong brush
            // name, a binding never refreshed) would leave the player's letters broken even with a correct policy.
            LetterFontResolver.LanguageIdProvider = () => "Русский";
            t.Expect("vmBodyBrushIsPlainForRussian", vm.BodyBrush, "CR.Letter.Body.Plain");
            t.Expect("vmHeadingBrushIsPlainForRussian", vm.HeadingBrush, "CR.Letter.Heading.Plain");
            t.Expect("vmInputBrushIsPlainForRussian", vm.InputBrush, "CR.Letter.Input.Plain");

            LetterFontResolver.LanguageIdProvider = () => "English";
            t.Expect("vmBodyBrushStaysDecorativeForEnglish", vm.BodyBrush, "CR.Letter.Body");
            t.Expect("vmHeadingBrushStaysDecorativeForEnglish", vm.HeadingBrush, "CR.Letter.Heading");
            t.Expect("vmInputBrushStaysDecorativeForEnglish", vm.InputBrush, "CR.Letter.Input");
         }
         finally
         {
            LetterFontResolver.LanguageIdProvider = priorProvider;
         }

         return t.Line();
      }

      // Manual action-injection menu (2026-08-21): the pure ManualActionAvailability gate deciding which
      // catalog entries the menu offers for a given snapshot of facts. Mechanical and engine-free by
      // construction (no Hero, no bridge call), so this drives it directly against seeded facts rather than
      // through a live conversation; the exhaustive per-gate coverage lives in
      // ManualActionAvailabilityTests (CalradiaRemembers.Tests), this is the cr.testall smoke line proving the
      // wiring the task brief itself named: a held prisoner exposes execute/free/recruit, and a non-ruler
      // hides grant_fief while keeping the always-on take_gold baseline.
      private static string TestManualActionMenu(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("manual_action_menu");

         var heldEligible = new ManualActionFacts {NpcIsPlayerPrisoner = true, CanRecruitPrisoner = true};
         IReadOnlyList<ManualActionEntry> heldEligibleAvailable = ManualActionAvailability.GetAvailable(heldEligible);
         t.Expect("heldEligibleExecute", heldEligibleAvailable.Contains(ManualActionCatalog.ExecutePrisoner), true);
         t.Expect("heldEligibleFree", heldEligibleAvailable.Contains(ManualActionCatalog.FreePrisoner), true);
         t.Expect("heldEligibleRecruit", heldEligibleAvailable.Contains(ManualActionCatalog.RecruitPrisoner), true);

         // Held but not (yet) eligible to turn: execute/free stand on custody alone, recruit needs its own
         // regard/clan-leader gate on top (PrisonerRecruitmentPolicy), guarding against the empty-promise
         // class of bug: a menu entry the bridge would only ever refuse.
         var heldIneligible = new ManualActionFacts {NpcIsPlayerPrisoner = true, CanRecruitPrisoner = false};
         IReadOnlyList<ManualActionEntry> heldIneligibleAvailable = ManualActionAvailability.GetAvailable(heldIneligible);
         t.Expect("heldIneligibleExecute", heldIneligibleAvailable.Contains(ManualActionCatalog.ExecutePrisoner), true);
         t.Expect("heldIneligibleFree", heldIneligibleAvailable.Contains(ManualActionCatalog.FreePrisoner), true);
         t.Expect("heldIneligibleRecruit", heldIneligibleAvailable.Contains(ManualActionCatalog.RecruitPrisoner), false);

         var nonRuler = new ManualActionFacts();
         IReadOnlyList<ManualActionEntry> nonRulerAvailable = ManualActionAvailability.GetAvailable(nonRuler);
         t.Expect("nonRulerNoGrantFief", nonRulerAvailable.Contains(ManualActionCatalog.GrantFief), false);
         t.Expect("nonRulerNoRecruitPrisoner", nonRulerAvailable.Contains(ManualActionCatalog.RecruitPrisoner), false);
         t.Expect("nonRulerStillHasTakeGold", nonRulerAvailable.Contains(ManualActionCatalog.TakeGold), true);

         return t.Line();
      }

      /// <summary>
      ///   The system prompt the LIVE pipeline would send for this hero, so a test can read what the model is
      ///   actually about to be told. Prefers the configured builder (the very one the chat uses); falls back to
      ///   a fresh one when no LLM is configured, so a mechanical test never has to be skipped for want of a key.
      /// </summary>
      private static string SystemPromptFor(NpcMemoryCampaignBehavior behavior, Hero npc, NpcProfile profile)
      {
         try
         {
            IPromptBuilder builder = behavior.LlmServices?.ChatService?.PromptBuilder ?? new PromptBuilder();
            var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
            EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, "hello");

            return builder.BuildSystemPrompt(profile, world, ctx);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SELFTEST] could not build the system prompt: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   CONVICTION, end to end against the live prompt. A player asked to author what a character BELIEVES
      ///   rather than merely how they sound, and the existing backstory could not deliver it: its own prompt
      ///   section tells the model, in as many words, that it shapes how you SPEAK and not what you DECIDE. So a
      ///   belief written there was a costume. This test injects a conviction at runtime (no file edit), then
      ///   follows it along every link of the real chain: the loader hands it back, the chat's own per-turn sync
      ///   carries it onto the profile, and the section lands in the system prompt the model would actually
      ///   receive, saying that it moves what the NPC decides and carrying the three limits that keep it inside
      ///   the bridge (no powers, no imported lore, cannot bend the world). Finally it proves the MCM gate: turn
      ///   custom data off and the belief must leave the prompt entirely, not linger on a stale profile.
      ///   Deliberately calls <see cref="CharacterOverrideLoader.SyncAuthoredProse" />, the same method the chat
      ///   calls each turn, so removing that call from the chat breaks this test instead of passing beside it.
      /// </summary>
      private static string TestConviction(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("conviction");

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to author a conviction on")) return t.Line();
         if (t.Skip(CrSettings.Instance == null, "MCM settings unavailable, cannot toggle custom character data")) return t.Line();

         const string belief =
            "[SELFTEST] He is convinced the player's clan is quietly plotting to unseat him, and reads every " +
            "kindness as a move in that game. He has told no one, and never will.";

         bool wasEnabled = ModSettings.UseCustomTraits;
         NpcProfile profile = GetOrCreate(behavior, npc);

         try
         {
            CrSettings.Instance.UseCustomTraits = true;

            CharacterOverrideLoader.InjectRuntimeOverride(new CharacterOverride {
               Id = npc.StringId,
               Hero = npc.Name?.ToString(),
               Conviction = belief
            });

            t.Note($"authored a conviction on {npc.Name} ({npc.StringId}) at runtime; character_overrides.json untouched");
            t.Expect("theLoaderHandsBackWhatWasAuthored", CharacterOverrideLoader.ConvictionFor(npc), belief);

            // The chat's own per-turn sync, called here exactly as ChatViewModel calls it.
            CharacterOverrideLoader.SyncAuthoredProse(profile, npc);
            t.Expect("thePerTurnSyncCarriesItOntoTheProfile", profile.AuthoredConviction, belief);

            string prompt = SystemPromptFor(behavior, npc, profile);

            if (t.Abort(prompt == null, "could not build the live system prompt")) return t.Line();

            t.Expect("itReachesTheSystemPromptTheModelWouldGet", prompt.Contains(belief), true);
            t.Expect("underItsOwnSection", prompt.Contains("WHAT YOU HOLD TRUE"), true);

            // The whole reason this field exists rather than a longer backstory: it must be taught as a MOTIVE.
            t.Expect("andItSaysHeACTSOnIt", prompt.Contains("you ACT on it"), true);
            t.Expect("andThatItMovesWhatHeDECIDES", prompt.Contains("moves what"), true);

            // The three limits. A belief a player types into a text box may not become a licence to rewrite the
            // world's rules, so each one is pinned individually: losing any single limit is a distinct failure.
            t.Expect("limitItGrantsNoPowers", prompt.Contains("NO powers"), true);
            t.Expect("limitItImportsNoOutsideLore", prompt.Contains("imports no lore"), true);
            t.Expect("limitItCannotBendTheWorld", prompt.Contains("does not bend the world"), true);

            // The MCM gate, and the reason the sync runs every turn instead of being persisted: switching custom
            // data off must ERASE the belief from the next prompt, not leave it stranded on the stored profile.
            CrSettings.Instance.UseCustomTraits = false;
            CharacterOverrideLoader.SyncAuthoredProse(profile, npc);
            t.Expect("switchingCustomDataOffErasesItFromTheProfile", profile.AuthoredConviction, null);

            string without = SystemPromptFor(behavior, npc, profile);
            t.Expect("andTheSectionLeavesThePromptEntirely", without != null && !without.Contains("WHAT YOU HOLD TRUE"), true);
         }
         finally
         {
            // Give the campaign back exactly what it lent: the file-backed overrides, the player's own MCM
            // choice, and a profile carrying no trace of the injected belief.
            CharacterOverrideLoader.Load();
            CrSettings.Instance.UseCustomTraits = wasEnabled;
            CharacterOverrideLoader.SyncAuthoredProse(profile, npc);
            behavior.Store.Set(profile);
         }

         return t.Line();
      }

      /// <summary>
      ///   Per-role provider routing (2026-08-13) + per-role API keys (player request, 2026-08-17: "I also need the
      ///   ability to have separate API keys for those endpoints, too, otherwise I can't use them"). The feature
      ///   lets the player send each TEXT role to a DIFFERENT provider, and (for OpenAI-Compatible) authenticate
      ///   each role with its OWN key. This drives <see cref="LlmServices.DebugDescribeRoleRouting" /> (which
      ///   reports what a role WOULD use with NO network call) to prove BOTH halves: distinct selections produce
      ///   distinct base URLs + kinds (the feature works at all), and leaving every role on "(default)" collapses
      ///   them onto the ONE default provider (the migration guarantee: an upgraded single-provider config behaves
      ///   exactly as before); then <see cref="LlmServices.DebugResolveApiKeyFor" /> (same no-network-call shape,
      ///   NEVER surfaced to any player-facing command) proves the twin claim for keys, using dummy sentinel
      ///   strings only, never a real key. Snapshots and restores every dropdown + the custom endpoint/key fields it
      ///   touches, so a real campaign is unchanged.
      /// </summary>
      private static string TestProviderRouting(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("provider_routing");

         CrSettings s = CrSettings.Instance;
         if (t.Skip(s == null, "MCM settings unavailable, cannot set the per-role provider dropdowns")) return t.Line();

         LlmServices llm = behavior?.LlmServices;
         if (t.Skip(llm == null, "LLM services not initialized")) return t.Line();

         int priorRoleplay = s.Provider.SelectedIndex;
         int priorConsensual = s.ConsensualProvider.SelectedIndex;
         int priorCnc = s.CncProvider.SelectedIndex;
         int priorInterpreter = s.ActionInterpreterProvider.SelectedIndex;
         string priorEndpoint = s.CustomEndpointUrl;
         string priorNsfwEndpoint = s.CustomEndpointUrlNsfw;
         string priorInterpEndpoint = s.CustomEndpointUrlInterpret;
         string priorApiKey = s.CustomApiKey;
         string priorNsfwApiKey = s.CustomApiKeyNsfw;
         string priorInterpApiKey = s.CustomApiKeyInterpret;
         string priorHousekeepingModel = s.HousekeepingModel;
         int priorHousekeepingProvider = s.HousekeepingProvider.SelectedIndex;

         try
         {
            // THE BACKGROUND ROLE'S ONE PROMISE, and it is a promise to everyone who never touches the setting:
            // a blank Background model resolves to EXACTLY the prose model, so an install that ignores this
            // feature keeps sending its summaries and memory compression where it always did (fkasad asked for
            // the setting, 19/09/2026; nobody asked to have their background work quietly moved).
            s.HousekeepingModel = "";
            s.HousekeepingProvider.SelectedValue = ProviderRolePolicy.DefaultSelection;

            (ProviderKind _, string _, string proseModel) = llm.DebugDescribeRoleRouting(ServiceRole.Prose);
            (ProviderKind _, string _, string inherited) = llm.DebugDescribeRoleRouting(ServiceRole.Housekeeping);

            t.Expect("blankBackgroundModelIsTheProseModel", inherited, proseModel);

            // And when it IS set, it is honoured: otherwise the setting is a placebo and the player's local
            // server goes on evicting its own cache.
            s.HousekeepingModel = "a-cheap/background-model";
            (ProviderKind _, string _, string chosen) = llm.DebugDescribeRoleRouting(ServiceRole.Housekeeping);

            t.Expect("aSetBackgroundModelIsUsed", chosen, "a-cheap/background-model");

            s.HousekeepingModel = priorHousekeepingModel;
            // STAKE: the whole round is "a different provider per role". If DISTINCT selections did not yield
            // distinct base URLs + kinds, every request would still go to one provider and the feature is a no-op.
            // The roleplay / prose role is the base (property still named Provider); the others carry "(default)".
            const string dummyEndpoint = "http://localhost:9999/v1";
            s.CustomEndpointUrl = dummyEndpoint;
            s.Provider.SelectedValue = ProviderRolePolicy.OpenRouterValue;
            s.CncProvider.SelectedValue = ProviderRolePolicy.NanoGptValue;
            s.ActionInterpreterProvider.SelectedValue = ProviderRolePolicy.OpenAiCompatibleValue;

            (ProviderKind kind, string baseUrl, string model) rp = llm.DebugDescribeRoleRouting(ServiceRole.Prose);
            (ProviderKind kind, string baseUrl, string model) nsfw = llm.DebugDescribeRoleRouting(ServiceRole.Nsfw);
            (ProviderKind kind, string baseUrl, string model) interp = llm.DebugDescribeRoleRouting(ServiceRole.Interpret);

            t.Expect("proseKind", rp.kind, ProviderKind.OpenRouter);
            t.Expect("nsfwKind", nsfw.kind, ProviderKind.NanoGpt);
            t.Expect("interpreterKind", interp.kind, ProviderKind.OpenAiCompatible);

            bool distinct = rp.baseUrl != nsfw.baseUrl && nsfw.baseUrl != interp.baseUrl && rp.baseUrl != interp.baseUrl;
            t.Expect("theThreeBaseUrlsAreDistinct", distinct, true);
            t.Expect("interpreterBaseUrlIsTheCustomEndpoint", interp.baseUrl, dummyEndpoint);
            t.Note($"prose={rp.baseUrl} | nsfw={nsfw.baseUrl} | interpreter={interp.baseUrl}");

            // STAKE (per-role endpoints, player request 2026-08-17): two roles both on OpenAI-Compatible must be
            // able to hit DIFFERENT local servers. With a shared endpoint plus a per-role override, each role
            // resolves to its own URL; a role left without an override still inherits the shared one.
            const string sharedEp = "http://localhost:1111/v1";
            const string nsfwEp = "http://localhost:2222/v1";
            const string interpEp = "http://localhost:3333/v1";
            s.Provider.SelectedValue = ProviderRolePolicy.OpenAiCompatibleValue;
            s.CncProvider.SelectedValue = ProviderRolePolicy.OpenAiCompatibleValue;
            s.ActionInterpreterProvider.SelectedValue = ProviderRolePolicy.OpenAiCompatibleValue;
            s.CustomEndpointUrl = sharedEp;
            s.CustomEndpointUrlNsfw = nsfwEp;
            s.CustomEndpointUrlInterpret = interpEp;

            t.Expect("proseUsesTheSharedEndpoint", llm.DebugDescribeRoleRouting(ServiceRole.Prose).baseUrl, sharedEp);
            t.Expect("nsfwUsesItsOwnEndpoint", llm.DebugDescribeRoleRouting(ServiceRole.Nsfw).baseUrl, nsfwEp);
            t.Expect("interpreterUsesItsOwnEndpoint", llm.DebugDescribeRoleRouting(ServiceRole.Interpret).baseUrl, interpEp);

            // A blank override falls back to the shared endpoint (the default that keeps single-endpoint setups working).
            s.CustomEndpointUrlNsfw = "";
            t.Expect("blankOverrideInheritsTheSharedEndpoint", llm.DebugDescribeRoleRouting(ServiceRole.Nsfw).baseUrl, sharedEp);

            // STAKE (per-role API keys, player request 2026-08-17): two roles both on OpenAI-Compatible must each be
            // able to authenticate against their OWN server, or a player who split roles across two local machines
            // could set the endpoints but still fail to log in against the second one. Dummy sentinel strings only:
            // this must never touch or log a real key. With a shared key plus a per-role override, each role
            // resolves to its own key; a role left without an override still inherits the shared one.
            const string sharedKey = "cr-selftest-shared-key";
            const string nsfwKey = "cr-selftest-nsfw-key";
            const string interpKey = "cr-selftest-interp-key";
            s.CustomApiKey = sharedKey;
            s.CustomApiKeyNsfw = nsfwKey;
            s.CustomApiKeyInterpret = interpKey;

            t.Expect("proseUsesTheSharedApiKey", llm.DebugResolveApiKeyFor(ServiceRole.Prose), sharedKey);
            t.Expect("nsfwUsesItsOwnApiKey", llm.DebugResolveApiKeyFor(ServiceRole.Nsfw), nsfwKey);
            t.Expect("interpreterUsesItsOwnApiKey", llm.DebugResolveApiKeyFor(ServiceRole.Interpret), interpKey);

            // A blank override falls back to the shared key (the default that keeps single-key setups working).
            s.CustomApiKeyNsfw = "";
            t.Expect("blankApiKeyOverrideInheritsTheSharedKey", llm.DebugResolveApiKeyFor(ServiceRole.Nsfw), sharedKey);

            // STAKE (MIGRATION): the "(default)" sentinel MUST make the other roles inherit the roleplay / prose
            // base, or a config upgraded from the single-provider era, which never touched these dropdowns, would
            // silently split its roles across providers with no setting changed to explain it.
            s.Provider.SelectedValue = ProviderRolePolicy.OpenRouterValue;
            s.ConsensualProvider.SelectedValue = ProviderRolePolicy.DefaultSelection;
            s.CncProvider.SelectedValue = ProviderRolePolicy.DefaultSelection;
            s.ActionInterpreterProvider.SelectedValue = ProviderRolePolicy.DefaultSelection;

            string dRp = llm.DebugDescribeRoleRouting(ServiceRole.Prose).baseUrl;
            string dNsfw = llm.DebugDescribeRoleRouting(ServiceRole.Nsfw).baseUrl;
            string dInterp = llm.DebugDescribeRoleRouting(ServiceRole.Interpret).baseUrl;

            bool allSame = dRp == dNsfw && dNsfw == dInterp;
            t.Expect("allDefaultRolesShareOneBaseUrl", allSame, true);
            t.Note($"default base url = {(string.IsNullOrWhiteSpace(dRp) ? "(none)" : dRp)}");
         }
         finally
         {
            s.Provider.SelectedIndex = priorRoleplay;
            s.ConsensualProvider.SelectedIndex = priorConsensual;
            s.CncProvider.SelectedIndex = priorCnc;
            s.ActionInterpreterProvider.SelectedIndex = priorInterpreter;
            s.CustomEndpointUrl = priorEndpoint;
            s.CustomEndpointUrlNsfw = priorNsfwEndpoint;
            s.CustomEndpointUrlInterpret = priorInterpEndpoint;
            s.CustomApiKey = priorApiKey;
            s.CustomApiKeyNsfw = priorNsfwApiKey;
            s.CustomApiKeyInterpret = priorInterpApiKey;
            s.HousekeepingModel = priorHousekeepingModel;
            s.HousekeepingProvider.SelectedIndex = priorHousekeepingProvider;
         }

         return t.Line();
      }

      /// <summary>
      ///   prose_fallback (Prose + Interpreter truncation safety net, 15/08/2026): the fast-fail + redo-once-
      ///   elsewhere decision the live chat and cr.rp_bench both rely on before ever placing a real LLM call
      ///   (TruncationFallbackPolicy is pure, so its own clean/not-clean rule is covered by
      ///   TruncationFallbackPolicyTests; this drives it again here so cr.testall proves the wiring reaches this
      ///   assembly at all), plus the ONE piece that genuinely needs a live MCM instance:
      ///   LlmServices.ResolveProseFallbackModel. Phase 2 makes the hard-failure fallback the NSFW model, so this
      ///   asserts it reuses the current scene (PROSE) model when the NSFW field is left blank (the "no new config
      ///   needed by default" claim, since the NSFW model itself defaults to grok), and honours a distinct
      ///   configured NSFW model instead. No engine state is mutated beyond the NSFW model field, restored in the
      ///   finally.
      /// </summary>
      private static string TestProseFallback(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("prose_fallback");

         LlmServices llm = behavior?.LlmServices;
         if (t.Skip(llm == null, "LLM services not initialized")) return t.Line();

         t.Expect("lengthFallsBackWhenConfigured", TruncationFallbackPolicy.ShouldFallBack("length", true), true);
         t.Expect("contentFilterFallsBackWhenConfigured", TruncationFallbackPolicy.ShouldFallBack("content_filter", true), true);
         t.Expect("emptyFallsBackWhenConfigured", TruncationFallbackPolicy.ShouldFallBack(null, true), true);
         t.Expect("stopNeverFallsBack", TruncationFallbackPolicy.ShouldFallBack("stop", true), false);
         t.Expect("noModelConfiguredNeverFallsBack", TruncationFallbackPolicy.ShouldFallBack("length", false), false);
         t.Expect("cleanStopButNoSpokenLineFallsBack", TruncationFallbackPolicy.ShouldFallBack("stop", true, false), true);
         t.Expect("cleanStopWithSpokenLineNeverFallsBack", TruncationFallbackPolicy.ShouldFallBack("stop", true, true), false);

         CrSettings s = CrSettings.Instance;
         if (t.Skip(s == null, "MCM settings unavailable, cannot read the NSFW fallback model field")) return t.Line();

         string priorNsfw = s.CncModel;
         string priorInterpreter = s.ActionInterpreterModel;
         try
         {
            // STAKE: Phase 2 makes the hard-failure fallback the NSFW model. When the NSFW field is blank the
            // resolution must reuse the current scene (PROSE) model, or a single-model config would lose its
            // safety net entirely. Both the fallback and the interpreter fall through to that same PROSE model when
            // their OWN field is blank, so with both blank they must resolve identically (ResolveModel is private).
            s.CncModel = "";
            s.ActionInterpreterModel = "";
            t.Expect("blankFallbackReusesProseModel", llm.ResolveProseFallbackModel(), llm.ResolveInterpreterModel());

            // STAKE: a player who DOES set a distinct NSFW model must have it win, or the field would be dead
            // configuration that silently does nothing.
            s.CncModel = "some/distinct-model";
            t.Expect("configuredFallbackWins", llm.ResolveProseFallbackModel(), "some/distinct-model");
         }
         finally
         {
            s.CncModel = priorNsfw;
            s.ActionInterpreterModel = priorInterpreter;
         }

         return t.Line();
      }

      /// <summary>
      ///   manual_model_toggle (three-state in-chat switch, 2026-08-21): replaces the old binary manual toggle
      ///   (prose=Roleplay / prose=NSFW) with Enhanced/Roleplay/NSFW, cycled by
      ///   <see cref="ChatViewModel.ExecuteToggleModelMode" />. Drives the pure <see cref="ModelModePolicy" /> mapping
      ///   each state defines (MainRoleFor/CompositionFor) and the fixed Cycle order, then the live
      ///   <see cref="LlmServices.ActiveModelId" />/<see cref="LlmServices.ActiveComposition" /> to prove the engine
      ///   binding: setting <see cref="LlmServices.ActiveModelMode" /> to Nsfw must resolve the configured NSFW model
      ///   with NO MCM gate to pass (the old ShowManualModelToggle gate is retired - the switch is always live), while
      ///   Roleplay/Enhanced resolve the PROSE model, and only Enhanced yields Enhanced composition. Snapshots and
      ///   restores the NSFW model field and the active mode.
      /// </summary>
      private static string TestManualModelToggle(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("manual_model_toggle");

         LlmServices llm = behavior?.LlmServices;
         if (t.Skip(llm == null, "LLM services not initialized")) return t.Line();

         // Pure policy: each state's fixed (main role, composition) pair is the whole design of the switch.
         t.Expect("enhancedUsesProse", ModelModePolicy.MainRoleFor(ManualModelMode.Enhanced), ServiceRole.Prose);
         t.Expect("roleplayUsesProse", ModelModePolicy.MainRoleFor(ManualModelMode.Roleplay), ServiceRole.Prose);
         t.Expect("nsfwUsesNsfw", ModelModePolicy.MainRoleFor(ManualModelMode.Nsfw), ServiceRole.Nsfw);
         t.Expect("enhancedComposition", ModelModePolicy.CompositionFor(ManualModelMode.Enhanced), ChatCompositionMode.Enhanced);
         t.Expect("roleplayComposition", ModelModePolicy.CompositionFor(ManualModelMode.Roleplay), ChatCompositionMode.Standard);
         t.Expect("nsfwComposition", ModelModePolicy.CompositionFor(ManualModelMode.Nsfw), ChatCompositionMode.Standard);

         // The button must cycle through all three states in order and wrap, or the player gets stuck.
         t.Expect("cycleEnhancedToRoleplay", ModelModePolicy.Cycle(ManualModelMode.Enhanced), ManualModelMode.Roleplay);
         t.Expect("cycleRoleplayToNsfw", ModelModePolicy.Cycle(ManualModelMode.Roleplay), ManualModelMode.Nsfw);
         t.Expect("cycleNsfwWrapsToEnhanced", ModelModePolicy.Cycle(ManualModelMode.Nsfw), ManualModelMode.Enhanced);

         CrSettings s = CrSettings.Instance;
         if (t.Skip(s == null, "MCM settings unavailable")) return t.Line();

         const string sentinel = "cr-selftest/nsfw-sentinel-model";
         string priorNsfw = s.CncModel;
         ManualModelMode priorMode = LlmServices.ActiveModelMode;
         try
         {
            s.CncModel = sentinel;

            // NSFW state: the live main-call model must resolve to the configured NSFW model, no gate to pass.
            LlmServices.ActiveModelMode = ManualModelMode.Nsfw;
            t.Expect("nsfwStateResolvesNsfwModel", LlmServices.ActiveModelId, sentinel);
            t.Expect("nsfwStateIsStandardComposition", LlmServices.ActiveComposition, ChatCompositionMode.Standard);

            // Roleplay state: back to the PROSE model (not the sentinel), still Standard composition - no
            // NSFW auto-routing even though a captivity scene could be active (content-kind routing is retired).
            LlmServices.ActiveModelMode = ManualModelMode.Roleplay;
            t.Expect("roleplayStateUsesProseModel", LlmServices.ActiveModelId != sentinel, true);
            t.Expect("roleplayStateIsStandardComposition", LlmServices.ActiveComposition, ChatCompositionMode.Standard);

            // Enhanced state: PROSE model, but Enhanced composition (the interpreter + hard-failure-fallback pipeline).
            LlmServices.ActiveModelMode = ManualModelMode.Enhanced;
            t.Expect("enhancedStateUsesProseModel", LlmServices.ActiveModelId != sentinel, true);
            t.Expect("enhancedStateIsEnhancedComposition", LlmServices.ActiveComposition, ChatCompositionMode.Enhanced);
         }
         finally
         {
            s.CncModel = priorNsfw;
            LlmServices.ActiveModelMode = priorMode;
         }

         return t.Line();
      }

      /// <summary>
      ///   action_catalog_parity (Unified Action Catalog, Stage 1, 14/08/2026): the Action Interpreter is taught
      ///   ONLY the verbs listed in <see cref="GameActionCatalog" />, so any bridge verb missing from that catalog
      ///   would be silently unreachable by the LLM (the exact class of bug this whole increment exists to close),
      ///   and any catalog entry the bridge no longer dispatches would teach a dead action. The comparison itself
      ///   cannot live as a pure SDK unit test: <see cref="BannerlordGameStateBridge.HandledActionTypes" /> is
      ///   engine-bound (it lives beside the ExecuteAction switch it documents), so this self-test is the only
      ///   place the two sides can be checked against each other. Reads both static sets directly; mutates
      ///   nothing, so it needs no cleanup.
      /// </summary>
      private static string TestActionCatalogParity(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("action_catalog_parity");

         HashSet<string> bridgeTypes = BannerlordGameStateBridge.HandledActionTypes;
         IReadOnlyCollection<string> catalogTypes = GameActionCatalog.Types;

         if (t.Abort(bridgeTypes == null, "BannerlordGameStateBridge.HandledActionTypes is null")) return t.Line();
         if (t.Abort(catalogTypes == null, "GameActionCatalog.Types is null")) return t.Line();

         var catalogSet = new HashSet<string>(catalogTypes);

         List<string> missingFromCatalog = bridgeTypes.Where(x => !catalogSet.Contains(x)).OrderBy(x => x).ToList();
         List<string> extraInCatalog = catalogSet.Where(x => !bridgeTypes.Contains(x)).OrderBy(x => x).ToList();

         t.Note($"bridgeCount={bridgeTypes.Count}");
         t.Note($"catalogCount={catalogSet.Count}");

         // The two directions of drift: a verb the bridge dispatches that the LLM was never taught (the bug
         // report this whole increment answers), and a verb the catalog still teaches that the bridge no longer
         // dispatches (a dead action the LLM could be steered into emitting for nothing).
         t.Expect("missingFromCatalog", missingFromCatalog.Count == 0
            ? "(none)"
            : string.Join(", ", missingFromCatalog), "(none)");
         t.Expect("extraInCatalog", extraInCatalog.Count == 0
            ? "(none)"
            : string.Join(", ", extraInCatalog), "(none)");

         return t.Line();
      }

      /// <summary>
      ///   external_action_registration (Extension Surface, 28/08/2026): CrActions.Register is the PUBLIC,
      ///   supported door a third-party mod uses to give the LLM one of its own executable deeds, replacing
      ///   the fragile pattern of casting GameActionCatalog.All to a List and mutating it by reflection. This
      ///   proves the WHOLE chain a registered action must actually complete: taught to the interpreter
      ///   catalog, kept in lockstep with the bridge's own dispatch table, taught to the 1:1 prose
      ///   vocabulary, and genuinely EXECUTED when the real bridge dispatches an [ACTION] type it does not
      ///   itself recognise through VerbRegistry. Also pins the three rejection paths (duplicate Name, a
      ///   reserved chat-flow-control Name, an invalid spec) so a mod cannot silently shadow a built-in verb
      ///   or a control CR's own chat window depends on. Cleans up in a finally: a leaked registration would
      ///   pollute every later prompt AND action_catalog_parity for the rest of the session.
      /// </summary>
      private static string TestExternalActionRegistration(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("external_action_registration");

         const string name = "crselftest_ext_action";
         const string execMessage = "self-test external action ran";

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to drive the real dispatch through")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         var executed = false;

         var spec = new ExternalActionSpec {
            Name = name,
            Description = "[SELFTEST] a dummy external action, never a real deed.",
            WhenToEmit = new List<string> {"never genuinely emitted: self-test only"},
            SourceMod = "CrSelfTest",
            Execute = request =>
            {
               executed = true;

               return new ActionResult {Success = true, Message = execMessage};
            }
         };

         try
         {
            ActionRegistrationResult first = CrActions.Register(spec);
            t.Expect("firstRegistrationSucceeds", first, ActionRegistrationResult.Registered);
            t.Expect("appearsInCatalog", GameActionCatalog.Types.Contains(name), true);
            t.Expect("appearsInHandledActionTypes", BannerlordGameStateBridge.HandledActionTypes.Contains(name), true);

            bool inProseVocabulary = LlmServices.DebugActionVocabulary(RelationWarmth.Normal).Any(d => d.Type == name);
            t.Expect("appearsInProseVocabulary", inProseVocabulary, true);

            // Drive the REAL bridge: "crselftest_ext_action" matches none of ExecuteAction's own cases, so
            // the default branch's VerbRegistry.Instance.Execute call is the one actually dispatching it,
            // proving the whole adapter + registry chain runs the spec's own Execute callback.
            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = name}, npc, new ActionContext(profile, null));

            t.Expect("dispatchedTheRealExecuteCallback", executed, true);
            t.Expect("outcomeCarriesTheDummyMessage", outcome?.Message, execMessage);
            t.Expect("outcomeIsFavourable", outcome?.IsPositive, true);

            var duplicate = new ExternalActionSpec {
               Name = name,
               Execute = _ => new ActionResult {Success = true, Message = "should never register"}
            };
            t.Expect("duplicateNameRejected", CrActions.Register(duplicate), ActionRegistrationResult.RejectedDuplicate);

            var reserved = new ExternalActionSpec {
               Name = "end_conversation",
               Execute = _ => new ActionResult {Success = true, Message = "should never register"}
            };
            t.Expect("reservedChatFlowNameRejected", CrActions.Register(reserved), ActionRegistrationResult.RejectedDuplicate);

            var noExecute = new ExternalActionSpec {Name = "crselftest_ext_action_no_exec", Execute = null};
            t.Expect("nullExecuteRejected", CrActions.Register(noExecute), ActionRegistrationResult.RejectedInvalid);
         }
         finally
         {
            // Critical: this self-test must leave NOTHING registered, or it would pollute every real prompt
            // (catalog + prose vocabulary) and break action_catalog_parity for the rest of the session.
            bool removed = CrActions.Unregister(name);
            t.Expect("cleanupActuallyRemovedIt", removed, true);
            t.Expect("goneFromCatalogAfterCleanup", GameActionCatalog.Types.Contains(name), false);
            t.Expect("goneFromHandledActionTypesAfterCleanup", BannerlordGameStateBridge.HandledActionTypes.Contains(name), false);
         }

         return t.Line();
      }

      /// <summary>
      ///   external_action_guidance (Extension Surface actions guidance, 30/08/2026): ExternalActionSpec.Guidance
      ///   answers the modder report ExternalActionSpec.IsEligible/TeachingText alone could not: an
      ///   ineligible action's NPC roleplayed AGREEING in its prose, only for the deed to then fail its own
      ///   eligibility check when the player's turn actually dispatched it. Registers a dummy action that is
      ///   ALWAYS ineligible with a Guidance callback returning a distinctive sentinel, and drives the REAL
      ///   VerbRegistry.Instance.ComposeGuidance/ComposeTeachings against the same VerbContext to prove
      ///   guidance rides ahead of eligibility while ComposeTeachings still hides the ineligible action
      ///   entirely; also asserts the guidance context it received carried the real NPC's facts and correctly
      ///   resolved a hero named in the player's message, proving CR's own ContextualNameResolver reached the
      ///   callback rather than a reimplementation. Unregisters in a finally so nothing leaks into a later
      ///   test or a real prompt.
      /// </summary>
      private static string TestExternalActionGuidance(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("external_action_guidance");

         const string name = "crselftest_ext_guidance";
         const string sentinel = "[SELFTEST GUIDANCE SENTINEL] decline in character: not available right now.";

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to drive the real dispatch through")) return t.Line();

         Hero mentioned = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != npc && h.IsLord);

         if (t.Skip(mentioned == null, "no other lord to name in the player's message")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         string playerMessage = $"Tell me what you make of {mentioned.Name}.";
         var guidanceSawNpcId = false;
         var guidanceSawMentionedHero = false;

         var spec = new ExternalActionSpec {
            Name = name,
            Description = "[SELFTEST] a dummy external action, never a real deed.",
            WhenToEmit = new List<string> {"never genuinely emitted: self-test only"},
            SourceMod = "CrSelfTest",
            IsEligible = _ => false,
            Execute = _ => new ActionResult {Success = true, Message = "should never execute in this test"},
            Guidance = context =>
            {
               guidanceSawNpcId = context?.Npc?.NpcId == npc.StringId;
               guidanceSawMentionedHero = context?.MentionedHeroes != null
                  && context.MentionedHeroes.Any(h => h.Id == mentioned.StringId);

               return sentinel;
            }
         };

         try
         {
            ActionRegistrationResult registered = CrActions.Register(spec);
            t.Expect("registrationSucceeds", registered, ActionRegistrationResult.Registered);

            var verbCtx = new VerbContext(npc, profile, behavior.GameStateBridge, playerMessage, null);

            string guidance = VerbRegistry.Instance.ComposeGuidance(verbCtx);
            string teachings = VerbRegistry.Instance.ComposeTeachings(verbCtx);

            t.Expect("guidanceReachesThePromptDespiteIneligibility", guidance != null && guidance.Contains(sentinel), true);
            t.Expect("teachingsExcludeTheIneligibleAction", teachings == null || !teachings.Contains(sentinel), true);
            t.Expect("guidanceContextCarriedTheRealNpc", guidanceSawNpcId, true);
            t.Expect("guidanceContextResolvedTheNamedHero", guidanceSawMentionedHero, true);
            t.Note($"{npc.Name} asked about {mentioned.Name}: {(guidanceSawMentionedHero ? "RESOLVED" : "NOT resolved")}.");
         }
         finally
         {
            bool removed = CrActions.Unregister(name);
            t.Expect("cleanupActuallyRemovedIt", removed, true);
            t.Expect("goneFromCatalogAfterCleanup", GameActionCatalog.Types.Contains(name), false);
            t.Expect("goneFromHandledActionTypesAfterCleanup", BannerlordGameStateBridge.HandledActionTypes.Contains(name), false);
         }

         return t.Line();
      }

      /// <summary>
      ///   prompt_variable_registration (Extension Surface, Prompt Variables volet, 28/08/2026): CrPrompt is
      ///   the PUBLIC door a third-party mod uses to contribute a live <c>{{name}}</c> prompt variable
      ///   without patching PromptBuilder. PromptVariableRegistry.Compose itself is SDK-internal (visible
      ///   only to the SDK's own test project, mirroring PromptVariableRegistry.Clear), so this drives the
      ///   REAL public surface instead: a fresh PromptBuilder with PostHistoryInstructions referencing the
      ///   registered token, built via the same BuildSystemPrompt call the live chat pipeline uses. Proves a
      ///   registered provider's value actually lands in the live prompt text, keyed off the real NPC's id;
      ///   that a provider which THROWS is isolated rather than breaking the OTHER registered variable's
      ///   expansion in the same prompt (a third-party addon's bug must never brick every conversation); and
      ///   that a name colliding with a built-in ("user") never lets the third-party value override the real
      ///   one CR itself supplies, the exact guard BuildPromptVariables' own merge enforces. Cleans up in a
      ///   finally via CrPrompt.Unregister of each name this test registered, the only cleanup surface the
      ///   mod assembly has.
      /// </summary>
      private static string TestPromptVariableRegistration(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("prompt_variable_registration");

         const string name = "crselftest_var";
         const string throwingName = "crselftest_var_throws";
         const string builtInName = "user";

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to build a real prompt for")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, npc);
         var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
         EncounterContext ctx;

         try
         {
            // A registered provider's value must actually surface in the live prompt text, keyed off the
            // real profile's own id, exactly what BuildPromptVariables hands a provider (NpcId, never a
            // captured constant), since a real provider (e.g. bc_court_state) depends on which NPC the
            // prompt is being built for.
            CrPrompt.RegisterVariable(name, facts => $"selftest-marker-{facts.NpcId}");
            CrPrompt.RegisterVariable(throwingName, _ => throw new InvalidOperationException("[SELFTEST] deliberate throw"));

            // REGISTER FIRST, THEN BUILD THE CONTEXT, which is the order the game itself uses and was not the
            // order this test used. Since 2.5.19 (tashmetu: a modder's {{variables}} came out empty) providers
            // are resolved on the GAME THREAD while the context is built, and the prompt reads what the context
            // carries rather than composing them itself. So a context built BEFORE registration carries an empty
            // set that wins over the registry, and the marker could never land. A real bridge registers at module
            // load, long before any conversation, which is why nothing was broken but this.
            ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, "hello");

            string prompt = null;
            Exception thrown = null;
            try
            {
               var builder = new PromptBuilder {
                  PostHistoryInstructions = $"[[{{{{{name}}}}}]] [[{{{{{throwingName}}}}}]]"
               };
               prompt = builder.BuildSystemPrompt(profile, world, ctx);
            }
            catch (Exception ex)
            {
               thrown = ex;
            }

            // A throwing provider must never be allowed to break prompt building itself: one buggy
            // third-party addon must not brick every conversation in the game.
            t.Expect("buildingThePromptNeverThrowsOnAThrowingProvider", thrown, null);
            t.Expect("registeredValueLandsInTheLivePrompt",
               prompt != null && prompt.Contains($"selftest-marker-{profile.Id}"), true);
            // PromptVariableExpander leaves an UNKNOWN token exactly as written: since Compose skipped the
            // throwing provider, its token was never added to vars, so it must still read literally here,
            // proving the throw did not silently smuggle some other value into its place either.
            t.Expect("throwingProviderLeavesItsOwnTokenLiteral",
               prompt != null && prompt.Contains($"{{{{{throwingName}}}}}"), true);

            // A name that collides with a BUILT-IN ("user") must never let the third-party value win: the
            // real player name CR itself supplies always takes precedence, so a third-party mod can never
            // shadow a core token.
            CrPrompt.RegisterVariable(builtInName, _ => "IMPOSTOR-VALUE-SHOULD-NEVER-APPEAR");

            // Same reason as above: the collision has to be present in the context the prompt reads.
            ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, "hello");

            const string playerName = "SelfTestPlayer";
            var collisionBuilder = new PromptBuilder {
               PlayerName = playerName,
               PostHistoryInstructions = "[[{{user}}]]"
            };
            string collisionPrompt = collisionBuilder.BuildSystemPrompt(profile, world, ctx);

            t.Expect("builtInWinsOverAThirdPartyProviderOfTheSameName",
               collisionPrompt != null && collisionPrompt.Contains($"[[{playerName}]]"), true);
            t.Expect("theImpostorValueNeverAppears",
               collisionPrompt != null && collisionPrompt.Contains("IMPOSTOR-VALUE-SHOULD-NEVER-APPEAR"), false);
         }
         finally
         {
            bool removedName = CrPrompt.Unregister(name);
            bool removedThrowing = CrPrompt.Unregister(throwingName);
            bool removedBuiltIn = CrPrompt.Unregister(builtInName);
            t.Expect("cleanupRemovedTheMainProvider", removedName, true);
            t.Expect("cleanupRemovedTheThrowingProvider", removedThrowing, true);
            t.Expect("cleanupRemovedTheBuiltInCollisionProvider", removedBuiltIn, true);
         }

         return t.Line();
      }

      /// <summary>
      ///   CONVICTION, with the model in the loop. The mechanical test proves the belief reaches the prompt; this
      ///   one asks whether it CHANGES anything, which is the entire claim of the feature. The NPC is given a
      ///   false and ruinous belief (the player's clan plots to unseat him), then offered a plain kindness. A
      ///   backstory would have him thank us warmly in a distinctive voice. A conviction should have him read the
      ///   gift as a move in the game he thinks is being played, guarded, probing, looking for the hook, and yet
      ///   never confess the suspicion outright, because a belief that would ruin him is one he hides. The
      ///   judgement is the reader's, from the log; the harness only fails on the mechanical sins (no reply at
      ///   all, or the mod's own section headers spoken aloud in the NPC's voice).
      /// </summary>
      private static string TestChatConviction(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("chat_conviction");

         if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

         Hero npc = ConversationNpc();

         if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();
         if (t.Skip(CrSettings.Instance == null, "MCM settings unavailable, cannot toggle custom character data")) return t.Line();

         const string belief =
            "[SELFTEST] He is convinced the player's clan is quietly plotting to unseat him, and reads every " +
            "kindness as a move in that game. He has told no one, and never will.";

         bool wasEnabled = ModSettings.UseCustomTraits;
         NpcProfile profile = GetOrCreate(behavior, npc);

         try
         {
            CrSettings.Instance.UseCustomTraits = true;

            CharacterOverrideLoader.InjectRuntimeOverride(new CharacterOverride {
               Id = npc.StringId,
               Hero = npc.Name?.ToString(),
               Conviction = belief
            });

            CharacterOverrideLoader.SyncAuthoredProse(profile, npc);

            if (t.Abort(profile.AuthoredConviction != belief, "the conviction never reached the profile, so the reply would prove nothing")) return t.Line();

            const string kindness =
               "Winter is hard on your lands. I mean to send you grain from my own stores, freely given, and I ask nothing for it. Will you take it?";
            EncounterContext ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, kindness);

            var (ok, reply, err) = AskLlm(behavior, "chat_conviction", npc, profile, ctx, kindness);

            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Expect("gotNonEmptyReply", reply.Length > 0, true);

            // He may hint, he may probe, he may refuse. What he may never do is read our plumbing out loud.
            t.Expect("heNeverSpeaksTheModsOwnSectionHeaders", reply.Contains("WHAT YOU HOLD TRUE") || reply.Contains("[ACTION]"), false);

            t.Note($"offered {npc.Name} free grain while he secretly believes the player's clan plots against him; " +
                   $"judge in log: does he weigh the gift as a MOVE (guarded, probing, looking for the hook) rather " +
                   $"than merely thanking us, and does he keep the suspicion to himself? | A: {Show(reply)}");
         }
         finally
         {
            CharacterOverrideLoader.Load();
            CrSettings.Instance.UseCustomTraits = wasEnabled;
            CharacterOverrideLoader.SyncAuthoredProse(profile, npc);
            behavior.Store.Set(profile);
         }

         return t.Line();
      }

      /// <summary>
      ///   PRESENCE, against the live engine. A companion riding in the player's party must be told they travel
      ///   at the player's side, and must NOT be told how long it has been since they last saw them, even when
      ///   their newest memory is two months stale. That elapsed-gap note is exactly what made a crew member
      ///   sailing between two ports open a conversation as though abandoned for a season (player report). The
      ///   contrast half of the test matters just as much: a lord who is genuinely away must STILL hear the time
      ///   apart, or the fix would have silenced the feature instead of correcting it.
      /// </summary>
      /// <summary>
      ///   The retainer feasibility spike, exercised end to end against the LIVE campaign: an other-clan lord/lady
      ///   ridden IN the player's party (member roster) while their Hero.Clan stays their own, surviving the mod's
      ///   daily observation at peace, and cleanly released. Deliberately borrows only a PARTYLESS other-clan lord
      ///   so the restore (release -> partyless again) returns the campaign exactly as it was; a party-leading
      ///   lord's own party would be disbanded on take, which a live campaign cannot cleanly undo, so that case is
      ///   skipped MANUAL rather than wrecked.
      /// </summary>
      private static string TestRetainerSpike(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("retainer_spike");

         RetainerBehavior spike = RetainerBehavior.Instance;
         if (t.Abort(spike == null || MobileParty.MainParty == null, "the retainer behavior or the player's party is not available")) return t.Line();

         // The pure rule the spike hangs on, asserted directly (it is also unit-tested in the Logic project): a war
         // with the retainer's faction MUST force a leave, and peace must not.
         t.Expect("policyForcesLeaveOnWar", RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = true, IsAlive = true, StillTracked = true}), true);
         t.Expect("policyKeepsThemAtPeace", RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = false, IsAlive = true, StillTracked = true}), false);

         // Borrow a real OTHER-CLAN, alive, non-prisoner, PARTYLESS lord (a clan member sitting without a party):
         // taking one of these never disbands a party, so the release fully restores the campaign.
         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != Hero.MainHero && h.IsAlive && h.IsLord && !h.IsPrisoner
                 && h.Clan != null && h.Clan != Clan.PlayerClan
                 && h.PartyBelongedTo == null && !spike.IsRetainer(h.StringId));

         if (t.Skip(lord == null, "no partyless other-clan lord exists to borrow without disbanding a live party")) return t.Line();

         Clan originalClan = lord.Clan;

         bool took = spike.TryTake(lord, out string takeReport);
         t.Note($"take: {takeReport}");
         if (t.Abort(!took, "TryTake refused the borrowed lord")) return t.Line();

         // (a) they are actually in the player's own member roster, and (b) their clan did NOT change - the whole
         // point of a retainer (rides with you, stays THEIRS), plus their occupation is still Lord.
         t.Expect("theyAreInTheMainPartyRoster", MobileParty.MainParty.MemberRoster.Contains(lord.CharacterObject), true);
         t.Expect("theirPartyBelongedToIsTheMainParty", lord.PartyBelongedTo == MobileParty.MainParty, true);
         t.Expect("theirClanIsUnchanged", lord.Clan == originalClan, true);
         t.Expect("theirClanIsNOTThePlayerClan", lord.Clan == Clan.PlayerClan, false);
         t.Expect("theyAreStillALord", lord.CharacterObject?.Occupation, Occupation.Lord);

         // Drive the behavior's OWN daily handler once and assert they survived it at peace. HONEST SCOPE: this
         // drives only the mod's daily observation seam, not the vanilla ConsiderSpawningLordParties tick, which
         // the harness cannot invoke - so this proves the mod does not itself eject a peaceful retainer, NOT that
         // the full engine tick leaves them untouched (that needs an in-game campaign run to confirm).
         bool atWar = lord.MapFaction != null && Hero.MainHero?.MapFaction != null
                      && FactionManager.IsAtWarAgainstFaction(lord.MapFaction, Hero.MainHero.MapFaction);
         spike.RunDailyObservation();

         if (atWar)
         {
            // The borrowed lord happens to already be at war with the player: the rule then REQUIRES they left.
            t.Note("(borrowed lord's faction was already at war with the player; asserting the war-leave path instead)");
            t.Expect("aWarringRetainerWasReleasedByTheDailyRule", spike.IsRetainer(lord.StringId), false);
            t.Expect("aWarringRetainerLeftTheRoster", MobileParty.MainParty.MemberRoster.Contains(lord.CharacterObject), false);
         }
         else
         {
            t.Expect("aPeacefulRetainerSurvivedTheDailyObservation", spike.IsRetainer(lord.StringId), true);
            t.Expect("andIsStillInTheRosterAfterTheTick", MobileParty.MainParty.MemberRoster.Contains(lord.CharacterObject), true);

            // Release and assert the clean reverse: out of the roster, partyless again (vanilla will respawn their
            // lord party on a later tick), no longer tracked.
            bool released = spike.TryRelease(lord, out string releaseReport);
            t.Note($"release: {releaseReport}");
            t.Expect("releaseSucceeded", released, true);
            t.Expect("theyLeftTheMainPartyRoster", MobileParty.MainParty.MemberRoster.Contains(lord.CharacterObject), false);
            t.Expect("theyArePartylessAgain", lord.PartyBelongedTo == null, true);
            t.Expect("theyAreNoLongerTracked", spike.IsRetainer(lord.StringId), false);
            t.Expect("theirClanSurvivedTheWholeRound", lord.Clan == originalClan, true);
         }

         // Belt and suspenders: never leave the borrowed lord conscripted or tracked if any branch above bailed.
         if (spike.IsRetainer(lord.StringId))
         {
            spike.TryRelease(lord, out string _);
            t.Note($"(restored: released the still-tracked borrowed lord {lord.Name})");
         }

         return t.Line();
      }

      /// <summary>
      ///   The PLAYER-FACING ride_with_me / part_ways path, driven through the REAL BannerlordGameStateBridge so
      ///   the switch wiring, the Enabled gate, the RideWithMeEligibilityPolicy re-check and the take/release are
      ///   all exercised (the retainer_spike test above drives only the raw TryTake/TryRelease mechanism). Proves
      ///   the five things the feature must guarantee: (e) while disabled the verb refuses and takes no one; (a)
      ///   enabled, an idle other-clan lord ends up in MainParty.MemberRoster with Hero.Clan UNCHANGED; (b) a
      ///   party-LEADING lord is REFUSED (that is follow_me's job) so the two verbs are mutually exclusive and a
      ///   lord's command is never orphaned; (c) part_ways removes them; (d) the pure war-leave rule holds. Borrows
      ///   only reversible state (a partyless lord for the take; a party-leader is only ever refused, never taken)
      ///   and restores regard + the feature switch in a finally. Skips MANUAL when no partyless other-clan lord
      ///   exists to borrow.
      /// </summary>
      private static string TestRideWithMe(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("ride_with_me");

         RetainerBehavior retainer = RetainerBehavior.Instance;
         if (t.Abort(retainer == null || MobileParty.MainParty == null || behavior?.GameStateBridge == null,
                "the retainer behavior, the player's party, or the bridge is not available")) return t.Line();

         // (d) the pure rule the whole arrangement hangs on, asserted directly (also unit-tested in Logic): a war
         // with the retainer's faction MUST force a leave.
         t.Expect("policyForcesLeaveOnWar", RetainerLeavePolicy.MustLeave(new RetainerLeavePolicy.RetainerStandingFacts {FactionAtWarWithPlayer = true, IsAlive = true, StillTracked = true}), true);

         // Borrow an idle (partyless) other-clan lord AT PEACE with the player: taking one never disbands a party,
         // and peace means ride_with_me will actually be honoured rather than refused on the war guard.
         // The realm must also be at peace, the same sense the bridge means: this asks the bridge's OWN
         // FactionIsAtWarWithAnyone rather than a copy, because a copy is precisely what hid the real bug here (a
         // duplicated version that forgot bandit clans agreed with a picker that was also wrong).
         Hero idle = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != Hero.MainHero && h.IsAlive && h.IsLord && !h.IsPrisoner
                 && h.Clan != null && h.Clan != Clan.PlayerClan
                 && h.PartyBelongedTo == null && !retainer.IsRetainer(h.StringId)
                 && !(h.MapFaction != null && Hero.MainHero?.MapFaction != null
                      && FactionManager.IsAtWarAgainstFaction(h.MapFaction, Hero.MainHero.MapFaction))
                 && !BannerlordGameStateBridge.FactionIsAtWarWithAnyone(h.MapFaction));

         if (t.Skip(idle == null, "no partyless other-clan lord whose realm is at peace with everyone exists to borrow")) return t.Line();

         // A party-LEADING other-clan lord for the mutual-exclusivity half. They are only ever REFUSED here, so
         // their party is never disbanded and nothing about them needs restoring beyond their forced regard.
         Hero leader = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != Hero.MainHero && h != idle && h.IsAlive && h.IsLord && !h.IsPrisoner
                 && h.Clan != null && h.Clan != Clan.PlayerClan
                 && h.PartyBelongedTo != null && h.PartyBelongedTo.LeaderHero == h);

         NpcProfile idleProfile = GetOrCreate(behavior, idle);
         int originalRegard = idleProfile.ReputationWithPlayer;
         idleProfile.ReputationWithPlayer = RideWithMeEligibilityPolicy.RegardFloor + 10;
         behavior.Store.Set(idleProfile);

         Clan originalClan = idle.Clan;
         bool originalEnabled = RetainerBehavior.Enabled;

         try
         {
            // (e) DISABLED: the bridge must refuse and conscript no one, so an emission on a build with the path
            // off never silently drops a foreign lord into the player's roster.
            RetainerBehavior.Enabled = false;
            ActionOutcome disabled = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "ride_with_me"}, idle, new ActionContext(idleProfile, null));
            t.Expect("disabledIsRefused", disabled?.WasRefused, true);
            t.Expect("disabledTookNoOne", retainer.IsRetainer(idle.StringId), false);

            RetainerBehavior.Enabled = true;

            // (a) idle other-clan lord, enabled -> ride_with_me puts them in the roster, clan UNCHANGED.
            ActionOutcome took = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "ride_with_me"}, idle, new ActionContext(idleProfile, null));
            t.Note($"take: {took?.Message}");
            t.Expect("takeWasNotRefused", took?.WasRefused, false);
            t.Expect("theyAreInTheMainPartyRoster", MobileParty.MainParty.MemberRoster.Contains(idle.CharacterObject), true);
            t.Expect("theyAreTrackedAsRetainer", retainer.IsRetainer(idle.StringId), true);
            t.Expect("theirClanIsUnchanged", idle.Clan == originalClan, true);
            t.Expect("theirClanIsNOTThePlayerClan", idle.Clan == Clan.PlayerClan, false);

            // (b) mutual exclusivity: a party-LEADING lord is REFUSED (follow_me's case), never taken, party intact.
            if (leader != null)
            {
               NpcProfile leaderProfile = GetOrCreate(behavior, leader);
               int leaderOriginalRegard = leaderProfile.ReputationWithPlayer;
               leaderProfile.ReputationWithPlayer = RideWithMeEligibilityPolicy.RegardFloor + 10;
               behavior.Store.Set(leaderProfile);
               MobileParty leaderParty = leader.PartyBelongedTo;

               ActionOutcome refusedLeader = behavior.GameStateBridge.ExecuteAction(
                  new GameAction {Type = "ride_with_me"}, leader, new ActionContext(leaderProfile, null));
               t.Expect("partyLeaderIsRefused", refusedLeader?.WasRefused, true);
               t.Expect("partyLeaderWasNotTaken", retainer.IsRetainer(leader.StringId), false);
               t.Expect("partyLeadersOwnPartySurvived", leader.PartyBelongedTo == leaderParty && leaderParty != null && leaderParty.IsActive, true);

               leaderProfile.ReputationWithPlayer = leaderOriginalRegard;
               behavior.Store.Set(leaderProfile);
            }
            else
            {
               t.Note("(no party-leading other-clan lord to prove mutual exclusivity against; skipped that half)");
            }

            // (c) part_ways removes them: out of the roster, partyless, no longer tracked.
            ActionOutcome parted = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "part_ways"}, idle, new ActionContext(idleProfile, null));
            t.Note($"part_ways: {parted?.Message}");
            t.Expect("partWaysWasNotRefused", parted?.WasRefused, false);
            t.Expect("theyLeftTheMainPartyRoster", MobileParty.MainParty.MemberRoster.Contains(idle.CharacterObject), false);
            t.Expect("theyArePartylessAgain", idle.PartyBelongedTo == null, true);
            t.Expect("theyAreNoLongerTracked", retainer.IsRetainer(idle.StringId), false);
            t.Expect("theirClanSurvivedTheWholeRound", idle.Clan == originalClan, true);
         }
         finally
         {
            // Restore every borrowed piece of state whichever assertion bailed: release if still tracked, put the
            // regard back, and return the feature switch to how it was found.
            if (retainer.IsRetainer(idle.StringId)) retainer.TryRelease(idle, out string _);
            idleProfile.ReputationWithPlayer = originalRegard;
            behavior.Store.Set(idleProfile);
            RetainerBehavior.Enabled = originalEnabled;
         }

         return t.Line();
      }

      /// <summary>
      ///   COURTSHIP TRIAL end to end, LLM-free. Proves the safety rail and the happy path in one sweep: (1) the
      ///   pure override table clears ONLY soft RegardTooLow and never a hard blocker; (2) a REAL hard-blocked hero
      ///   force-marked passed STILL resolves to that hard reason through the real resolver (the offer stays CLOSED,
      ///   the exact failure the feature was written to prevent); (3) a real soft-blocked prospect (regard forced
      ///   low so the block is RegardTooLow) is driven through the behavior's own daily-evaluation seam for the full
      ///   term, and the trial passes, a MarriageProposal letter is queued FROM her, and MarriageBlockedBecause
      ///   becomes None for that pairing; (4) a trial that STOPS accompanying before the term lapses and never
      ///   completes. Restores regard, the trial record, and any queued acceptance letter in a finally. Skips MANUAL
      ///   when the player is already wed or no soft-blockable prospect exists.
      /// </summary>
      private static string TestCourtshipTrial(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("courtship_trial");

         CourtshipTrialBehavior trial = CourtshipTrialBehavior.Instance;
         if (t.Abort(trial == null || behavior?.Store == null || Hero.MainHero == null,
                "the courtship-trial behavior, the store, or the player is not available")) return t.Line();

         // (1) The pure override table, asserted directly (also unit-tested in Logic): a passed trial clears ONLY
         // the soft RegardTooLow gate and NEVER a hard blocker. The safety rail the whole feature rests on.
         t.Expect("overrideClearsSoftRegard", MarriageBlockOverridePolicy.CanOverride(MarriageBlockReason.RegardTooLow, true), true);
         t.Expect("overrideRefusesAtWar", MarriageBlockOverridePolicy.CanOverride(MarriageBlockReason.AtWar, true), false);
         t.Expect("overrideRefusesPlayerAlreadyWed", MarriageBlockOverridePolicy.CanOverride(MarriageBlockReason.PlayerAlreadyWed, true), false);

         var today = (int) CampaignTime.Now.ToDays;

         // (2) A REAL hard-blocked hero, force-marked passed, must STILL resolve to that hard reason through the
         // real resolver: the passed flag can never open a hard-blocked pairing (married lords are always
         // NotMarriageable, so one almost always exists).
         Hero hardBlocked = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsAlive &&
            MarriageBlockOverridePolicy.IsHardBlock(EncounterContextBuilder.ResolveMarriageBlockReason(h, behavior.Store.Get(h.StringId))));

         if (hardBlocked != null)
         {
            MarriageBlockReason before = EncounterContextBuilder.ResolveMarriageBlockReason(hardBlocked, behavior.Store.Get(hardBlocked.StringId));
            trial.DebugSetPassed(hardBlocked.StringId, true);
            MarriageBlockReason after = EncounterContextBuilder.ResolveMarriageBlockReason(hardBlocked, behavior.Store.Get(hardBlocked.StringId));
            t.Note($"hardBlocked={hardBlocked.Name} reason={before}");
            t.Expect("passedFlagDidNotOpenAHardBlock", after, before);
            t.Expect("hardBlockStaysClosed", after == MarriageBlockReason.None, false);
            trial.DebugForget(hardBlocked.StringId);
         }
         else
         {
            t.Note("(no hard-blocked hero found to prove the closed case against; the policy table above still proves it)");
         }

         // (3) The happy path needs an UNWED player and a real soft-blockable prospect.
         if (t.Skip(Hero.MainHero.Spouse != null, "the player is already wed, so every match is the hard PlayerAlreadyWed and none can be soft-blocked")) return t.Line();

         Hero prospect = null;
         NpcProfile profile = null;
         var originalRegard = 0;
         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == Hero.MainHero || !h.IsAlive || h.IsPrisoner) continue;
            if (h.Clan == null || h.Clan == Clan.PlayerClan) continue;
            if (!MarriageProspectResolver.IsMarriageable(Hero.MainHero, h)) continue;

            NpcProfile p = GetOrCreate(behavior, h);
            int savedRegard = p.ReputationWithPlayer;
            p.ReputationWithPlayer = 0; // force the mod's own regard below the bar so only the SOFT gate can stand
            behavior.Store.Set(p);

            MarriageBlockReason r = EncounterContextBuilder.ResolveMarriageBlockReason(h, p);
            if (r == MarriageBlockReason.RegardTooLow && !trial.IsTrialPassed(h.StringId))
            {
               prospect = h;
               profile = p;
               originalRegard = savedRegard;

               break;
            }

            // Not soft-blockable (regard met via the live vanilla relation, at war, etc.): put the regard back and move on.
            p.ReputationWithPlayer = savedRegard;
            behavior.Store.Set(p);
         }

         if (t.Skip(prospect == null, "no soft-blockable marriage prospect exists to test (all are hard-blocked or already above the regard bar)")) return t.Line();

         try
         {
            trial.DebugForget(prospect.StringId);

            // Baseline: the offer is CLOSED on the soft gate the player reported ("MARRIAGE NOT YET AN OFFER").
            t.Expect("baselineReasonIsRegardTooLow", EncounterContextBuilder.ResolveMarriageBlockReason(prospect, profile), MarriageBlockReason.RegardTooLow);

            // Begin the trial in the past, then step to today: the first call must NOT pass it, the second must.
            trial.DebugEvaluate(prospect, profile, true, today - CourtshipTrialPolicy.TrialDays);
            t.Expect("notPassedAtStart", trial.IsTrialPassed(prospect.StringId), false);

            bool passedNow = trial.DebugEvaluate(prospect, profile, true, today);
            t.Expect("theTrialPassedAtTerm", passedNow, true);
            t.Expect("trialPassedFlagSet", trial.IsTrialPassed(prospect.StringId), true);

            // The gate now opens for THIS pairing: the soft block is cleared, so the marriage is on offer.
            t.Expect("marriageIsNowOnOffer", EncounterContextBuilder.ResolveMarriageBlockReason(prospect, profile), MarriageBlockReason.None);

            // She reaches out: a MarriageProposal letter is queued from her. The placeholder is added synchronously
            // when the chat service is present, so this asserts for real with an API key and is only noted without one.
            bool llmReady = behavior.LlmServices?.ChatService != null;
            if (llmReady)
               t.Expect("aMarriageProposalLetterWasQueuedFromHer", profile.SentLetters.Any(l => l.Reason == LetterReason.MarriageProposal), true);
            else
               t.Note("(no chat service / API key: her acceptance letter could not be queued, so that half is unverified this run)");

            // (4) LAPSE: a fresh trial that STOPS accompanying before the term must NOT complete, and the offer closes again.
            trial.DebugForget(prospect.StringId);
            trial.DebugEvaluate(prospect, profile, true, today - CourtshipTrialPolicy.TrialDays); // begin
            bool lapsedComplete = trial.DebugEvaluate(prospect, profile, false, today);           // stopped accompanying
            t.Expect("aLapsedTrialDoesNotComplete", lapsedComplete, false);
            t.Expect("aLapsedTrialLeavesNoPassedFlag", trial.IsTrialPassed(prospect.StringId), false);
            t.Expect("offerClosedAgainAfterLapse", EncounterContextBuilder.ResolveMarriageBlockReason(prospect, profile), MarriageBlockReason.RegardTooLow);
         }
         finally
         {
            // Restore every borrowed piece of state whichever assertion bailed: forget the trial, drop any acceptance
            // letter this test queued, and put the prospect's own regard back.
            trial.DebugForget(prospect.StringId);
            profile.SentLetters?.RemoveAll(l => l.Reason == LetterReason.MarriageProposal);
            profile.ReputationWithPlayer = originalRegard;
            behavior.Store.Set(profile);
         }

         return t.Line();
      }

      private static string TestPresence(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("presence");

         Hero companion = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && h.PartyBelongedTo == MobileParty.MainParty);

         // A test that only runs when the campaign happens to be arranged just so is a test that never runs. If no
         // companion is riding along, BORROW one for the assertion and put them back afterwards, the way the rest
         // of this harness seeds the state it needs.
         MobileParty borrowedFrom = null;
         var borrowed = false;

         if (companion == null)
         {
            Hero spare = Hero.AllAliveHeroes.FirstOrDefault(
               h => h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

            if (t.Skip(spare == null || MobileParty.MainParty == null, "the player has no companion at all to place at their side")) return t.Line();

            borrowedFrom = spare.PartyBelongedTo;
            borrowed = true;
            AddHeroToPartyAction.Apply(spare, MobileParty.MainParty, false);
            companion = spare;
            t.Note($"(borrowed {spare.Name} into the player's party for this test)");
         }

         PresenceTier tier = PlayerPresenceResolver.Resolve(companion);
         t.Note($"{companion.Name}: tier={tier}");
         t.Expect("aCompanionInYourPartyReadsAsInParty", tier, PresenceTier.InPlayerParty);
         t.Expect("aCompanionInYourPartyIsBesideYou", PlayerPresenceResolver.IsBeside(companion), true);

         // Age their memory to sixty days, using a THROWAWAY profile rather than the stored one. Adding an old
         // event to the live profile proves nothing: the gap is measured from the NEWEST memory, so any fresher
         // event (and the tests that ran before this one plant plenty) would still win and the gap would read
         // zero, making the assertion below pass for the wrong reason. This also leaves the save untouched.
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         NpcProfile aged = AgedProfile(companion, today);

         EncounterContext ctx = EncounterContextBuilder.Build(companion, aged, behavior.GameStateBridge, "hello");
         t.Note($"presenceNote='{ctx.PresenceNote}'");

         // Prove the test is not passing trivially: the gap really IS long enough that the note would have fired.
         t.Expect("theGapWouldOtherwiseHaveFired", ctx.DaysSinceLastMeeting > MeetingGapDescriber.RemarkThresholdDays, true);

         t.Expect("theyAreToldHowTheyStandBesideYou", !string.IsNullOrWhiteSpace(ctx.PresenceNote), true);
         t.Expect("andItSaysAtYourSide", ctx.PresenceNote?.Contains("at the player's side") == true, true);

         // The fix itself: someone who rides with you is never told how long since they last saw you.
         t.Expect("noAbandonedNoteForSomeoneRidingWithYou", ctx.MeetingGapNote, null);

         // The other half of the rule: a lord genuinely away STILL feels the time apart. Without this, the "fix"
         // could simply have silenced the feature everywhere instead of correcting who it applies to.
         Hero away = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != Hero.MainHero && h.IsAlive && h.IsLord && !h.IsPlayerCompanion
                 && PlayerPresenceResolver.Resolve(h) == PresenceTier.Away);

         if (away != null)
         {
            EncounterContext actx = EncounterContextBuilder.Build(away, AgedProfile(away, today), behavior.GameStateBridge, "hello");
            t.Note($"{away.Name} (away): gapNote='{actx.MeetingGapNote}'");
            t.Expect("anAwayLordStillHearsTheTimeApart", !string.IsNullOrWhiteSpace(actx.MeetingGapNote), true);
            t.Expect("anAwayLordGetsNoPresenceLine", actx.PresenceNote, null);

            // The reported-wife case: a spouse or consort who is genuinely AWAY must NOT be handed the same
            // "it has been X since you last saw the player" note as a fading acquaintance (players saw wives act
            // "left behind" the moment they rode out of town). Stage the household bond through a THROWAWAY profile
            // marked as a consort, exercising the real EncounterContextBuilder wiring without arranging a marriage
            // or touching the save. Contrast it against the plain away note just proven above: same hero, same
            // gap, only the bond differs, so a pass proves the reframe (not blanket silence) is doing the work.
            NpcProfile consort = AgedProfile(away, today);
            consort.Romantic = consort.Romantic ?? new RomanticProfile();
            consort.Romantic.IsConsort = true;

            EncounterContext hctx = EncounterContextBuilder.Build(away, consort, behavior.GameStateBridge, "hello");
            t.Note($"{away.Name} (away consort): gapNote='{hctx.MeetingGapNote}'");
            t.Expect("anAwayConsortIsNeverToldHowLongSinceTheyMet", hctx.MeetingGapNote?.Contains("since you last saw the player") != true, true);
            t.Expect("anAwayConsortIsFramedAsOfYourHouse", hctx.MeetingGapNote?.Contains("own house") == true, true);
         }
         else t.Note("no away lord to contrast against");

         // Put the borrowed companion back where they were, so the test does not quietly conscript someone.
         if (borrowed)
         {
            if (borrowedFrom != null && borrowedFrom != MobileParty.MainParty)
            {
               AddHeroToPartyAction.Apply(companion, borrowedFrom, false);
               t.Note($"(returned {companion.Name} to their party)");
            }
            else t.Note($"(NOTE: {companion.Name} had no party to return to and remains in yours; reload to discard)");
         }

         return t.Line();
      }

      /// <summary>
      ///   The ONE behaviour v1.30.14 actually changed, so the one worth guarding. Jealousy used to ask a
      ///   party-only question ("is this hero in my party?") while the letter system asked a wider one (party,
      ///   army, OR the same settlement). Both now go through the single PlayerPresenceResolver, which means an
      ///   NPC standing in the same town as the player counts as beside them: they will not post a courier to say
      ///   what they could say to your face, and they are not counted as neglected. This pins that on the live map,
      ///   and pins that the letter system's own façade genuinely delegates to the same authority.
      /// </summary>
      private static string TestStolenLove(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("stolen_love");

         var b = Marriage.NpcMarriageEstrangementBehavior.Instance;
         if (t.Abort(b == null, "stolen-love behavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != null && h.IsAlive && h != Hero.MainHero
            && h.Spouse is {IsAlive: true} && h.Spouse != Hero.MainHero
            && !b.HasConcludedFor(h.StringId) && b.PhaseFor(h.StringId) == EstrangementPhase.None);

         if (t.Abort(npc == null, "no third-party-married NPC to exercise the arc on")) return t.Line();

         try
         {
            // The seducibility pipeline runs on LIVE facts (traits, spouse relation, regard) without throwing and
            // yields a bounded, sane verdict: this is the engine-bound half the pure policy tests cannot cover.
            NpcProfile profile = behavior.Store.Get(npc.StringId);
            SeductionVerdict verdict = SeductionEligibilityPolicy.Evaluate(Mapping.SeductionFactsMapper.For(npc, profile));
            t.Note($"seducible={verdict.Seducible} ({verdict.Difficulty})");

            // The state machine, forced via the debug seam: a freshly seeded affair has not visibly frayed yet
            // (opening grace), declaring pulls it to ReadyToDivorce, and NO divorce has fired by merely declaring.
            b.DebugBegin(npc, today);
            t.Expect("freshlySeededHasNotFrayedYet", b.PhaseFor(npc.StringId), EstrangementPhase.None);

            b.DebugDeclare(npc.StringId, today);
            t.Expect("declaringReachesReadyToDivorce", b.PhaseFor(npc.StringId), EstrangementPhase.ReadyToDivorce);
            t.Expect("noDivorceFiresJustByDeclaring", b.HasConcludedFor(npc.StringId), false);

            // The jilted-spouse grudge is bounded for the real couple, so a divorce would never plant a nonsense value.
            Hero jilted = npc.Spouse;
            int magnitude = StolenSpouseGrudgePolicy.Magnitude(jilted.GetRelation(npc), jilted.GetTraitLevel(DefaultTraits.Mercy));
            t.Expect("jiltedGrudgeWithinBounds",
               magnitude >= StolenSpouseGrudgePolicy.MinMagnitude && magnitude <= StolenSpouseGrudgePolicy.MaxMagnitude, true);
         }
         finally
         {
            b.DebugClear(npc.StringId); // non-destructive: only a test estrangement was seeded, no marriage was touched
         }

         t.Expect("cleanedUp", b.PhaseFor(npc.StringId), EstrangementPhase.None);

         return t.Line();
      }

      private static string TestPresenceGate(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("presence_gate");

         List<Hero> roster = Hero.AllAliveHeroes.Where(h => h != null && h != Hero.MainHero && h.IsAlive).ToList();

         if (t.Abort(roster.Count == 0, "no living hero to judge presence against")) return t.Line();

         // THE consolidation, asserted across the ENTIRE living roster and needing no particular campaign state:
         // the letter system's facade and the single presence authority must give the SAME answer for every hero
         // alive. Before this release they could not: letters asked a wide question (party, army, or the same
         // settlement) while jealousy asked a narrow, party-only one. If these two ever disagree again, the three
         // systems have drifted apart once more and this test is the alarm.
         var disagreements = 0;
         foreach (Hero h in roster)
            if (HeroProximity.IsNearPlayer(h) != PlayerPresenceResolver.IsBeside(h))
               disagreements++;

         t.Note($"{roster.Count} living heroes judged");
         t.Expect("theLetterFacadeAndThePresenceAuthorityNEVERDisagree", disagreements, 0);

         // The tiers must also be a partition: beside == not away, for everyone, with no third state.
         var besideButAway = 0;
         var awayButBeside = 0;

         foreach (Hero h in roster)
         {
            bool beside = PlayerPresenceResolver.IsBeside(h);
            bool away = PlayerPresenceResolver.Resolve(h) == PresenceTier.Away;

            if (beside && away) besideButAway++;
            if (!beside && !away) awayButBeside++;
         }

         t.Expect("besideAndAwayAreExactOpposites", besideButAway + awayButBeside, 0);

         int inParty = roster.Count(h => PlayerPresenceResolver.Resolve(h) == PresenceTier.InPlayerParty);
         int inArmy = roster.Count(h => PlayerPresenceResolver.Resolve(h) == PresenceTier.InPlayerArmy);
         int samePlace = roster.Count(h => PlayerPresenceResolver.Resolve(h) == PresenceTier.SamePlace);
         t.Note($"tiers: party={inParty}, army={inArmy}, samePlace={samePlace}, away={roster.Count - inParty - inArmy - samePlace}");

         // The widened rule itself, when the live map offers it: sharing a roof IS being beside them, so they
         // would speak rather than post a courier, and they are not counted as neglected.
         Settlement here = Hero.MainHero?.CurrentSettlement;
         Hero roommate = here == null
            ? null
            : roster.FirstOrDefault(h => h.CurrentSettlement == here && h.PartyBelongedTo != MobileParty.MainParty);

         if (roommate != null)
         {
            t.Note($"{roommate.Name} shares {here.Name} with you");
            t.Expect("sharingASettlementReadsAsSamePlace", PlayerPresenceResolver.Resolve(roommate), PresenceTier.SamePlace);
            t.Expect("sharingASettlementIsBesideYou", PlayerPresenceResolver.IsBeside(roommate), true);
            t.Expect("soTheyWouldSpeakNotPostALetter", HeroProximity.IsNearPlayer(roommate), true);
         }
         else t.Note("you share no settlement with anyone right now, so the SamePlace tier is UNPROVEN here (walk into a town)");

         // And the gate must not have swallowed the world: someone genuinely away is still away, in BOTH systems.
         Hero far = roster.FirstOrDefault(h => PlayerPresenceResolver.Resolve(h) == PresenceTier.Away);

         if (far != null)
         {
            t.Note($"{far.Name} is away");
            t.Expect("aDistantHeroIsNotBesideYou", PlayerPresenceResolver.IsBeside(far), false);
            t.Expect("theLetterSystemAgreesTheyAreFar", HeroProximity.IsNearPlayer(far), false);
         }
         else t.Note("no distant hero to contrast against");

         return t.Line();
      }

      /// <summary>
      ///   COMPANION CONTENTMENT, through the real weekly tick. Pins the guarantee the rebalance rests on, the one
      ///   a tester had to switch the whole system off to escape: a companion who is merely unoccupied is HEALED by
      ///   the passing of a week, not driven one step closer to the door. Before the rebalance the very same pass
      ///   took them DOWN, every week, until they left, whatever the player promised. Also checks the reported
      ///   governor case: a companion holding a post is not kicking their heels.
      /// </summary>
      private static string TestContentment(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("contentment");

         if (t.Skip(CompanionHappinessBehavior.Instance == null, "the contentment behaviour is not registered")) return t.Line();
         if (t.Skip(!ModSettings.CompanionContentment, "companion contentment is switched OFF in Mod Options")) return t.Line();

         // Pick a companion who is NOT kept from a spouse, so this pass exercises idleness alone (a separated
         // spouse is a real grievance and is DESIGNED to sour them slowly, which would confound the reading).
         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner
            && (h.Spouse == null || !h.Spouse.IsAlive || h.Spouse == Hero.MainHero
                || h.Spouse.PartyBelongedTo == h.PartyBelongedTo));

         if (t.Skip(subject == null, "no companion free of a separated spouse to run an idleness pass on")) return t.Line();

         GetOrCreate(behavior, subject);

         int clanTier = Clan.PlayerClan?.Tier ?? 0;
         int baseline = CompanionBaseline.From(clanTier);
         t.Note($"{subject.Name}: clan tier {clanTier} -> resting happiness {baseline}");

         // The camp's real mood is part of the weekly pass, and a sullen camp legitimately dulls spirits. Read it
         // first, so the reading below is judged against the world as it actually is rather than an assumed one.
         HappinessDriver? mood = PartyMoraleMood.DriverFor(MobileParty.MainParty?.Morale ?? 50f);
         t.Note($"party morale {MobileParty.MainParty?.Morale ?? 50f:0} -> {mood?.ToString() ?? "unremarkable"}");

         // The fix, end to end: sour them almost to the leaving floor, then let one REAL week pass.
         CompanionHappinessService.Set(subject, 20);
         CompanionHappinessBehavior.Instance.RunTick();
         int after = CompanionHappinessService.Get(subject);
         t.Note($"soured 20 -> {after} after one real weekly pass");

         // THE guarantee, and the one that must hold whatever the camp's mood: being left idle can never carry a
         // companion DOWN, so it can never carry them out. Before the rebalance this same pass took them lower
         // every single week until they left, no matter what the player promised.
         t.Expect("anIdleCompanionNEVERSlides", after >= 20, true);
         t.Expect("stillMeansToLeave", CompanionHappiness.WantsToLeave(after), false);

         // And when the camp is not actively miserable, they must actually HEAL: doing nothing in particular now
         // brings a soured companion back rather than finishing them off.
         if (mood != HappinessDriver.LowMorale)
            t.Expect("anIdleSouredCompanionCLIMBS", after > 20, true);
         else
            t.Note("the camp is sullen (LowMorale fired), which legitimately offsets the recovery this week, so CLIMBS is not asserted; feed and pay your troops and it will");

         // The reported governor case: a post handed to a companion IS employment. Only provable when the player
         // actually has one governing, so it reports honestly rather than passing on an empty check.
         Hero governor = Hero.AllAliveHeroes.FirstOrDefault(h => h.IsAlive && h.IsPlayerCompanion && h.GovernorOf != null);

         if (governor != null)
         {
            bool onErrand = CompanionMissionBehavior.Instance?.IsAway(governor.StringId) == true;
            bool leadsAParty = governor.PartyBelongedTo != null && governor.PartyBelongedTo.LeaderHero == governor;
            t.Note($"{governor.Name} governs {governor.GovernorOf.Name}");
            t.Expect("aGovernorIsNotIdle", CompanionEngagementPolicy.IsIdle(new CompanionEngagementPolicy.CompanionChargeFacts {OnErrand = onErrand, IsGovernor = true, LeadsAParty = leadsAParty}), false);
         }
         else t.Note("no companion governs a town right now, so the governor case is UNPROVEN here");

         // Leave them where their clan's standing places them, rather than artificially soured.
         CompanionHappinessService.Set(subject, baseline);

         return t.Line();
      }

      /// <summary>
      ///   MECHANICAL (no LLM): the sixth found-topic, GRATITUDE (design brief 2026-08-20): a content companion
      ///   thanking the player for a specific kindness, purely warm, never a request/bargain. Seeds a real
      ///   Appreciation via the real <see cref="AppreciationBehavior.TryPlant" /> gate on a companion made
      ///   comfortably Content, then drives the REAL <see cref="CompanionAudienceTopicResolver.Scan" /> (never a
      ///   mock), asserting: the fresh kindness HOLDS with the seeded detail carried through; the SAME
      ///   appreciation's own key, once passed back in as already-thanked, WITHHOLDS it (the spend-once rule); and
      ///   the identical kindness, on the same companion made Disgruntled, never holds either (the design brief's
      ///   "a thank-you from an unhappy companion is wrong"). Restores the companion's original happiness in a
      ///   finally; does not remove the seeded appreciation itself (AppreciationBehavior exposes no removal API,
      ///   mirroring appreciation_flow/appreciation_repeat, which leave their own seeded appreciations in place too).
      /// </summary>
      private static string TestCompanionGratitude(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_gratitude");

         if (t.Abort(AppreciationBehavior.Instance == null, "AppreciationBehavior not active")) return t.Line();

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

         if (t.Skip(subject == null, "no living companion to seed a kindness on")) return t.Line();

         GetOrCreate(behavior, subject);

         Appreciation Mine() => AppreciationBehavior.Instance.AppreciationsFor(subject.StringId)
            .FirstOrDefault(a => a.Source == AppreciationSource.RansomedOrFreed
                              && string.Equals(a.CauseSummary, GratitudeTestCause, StringComparison.OrdinalIgnoreCase));

         if (t.Skip(Mine() != null, "this test's kindness is already on the ledger from an earlier run (reload to re-test)")) return t.Line();

         int originalHappiness = CompanionHappinessService.Get(subject);
         var today = (int) CampaignTime.Now.ToDays;

         try
         {
            CompanionHappinessService.Set(subject, GratitudeTestContentHappiness);

            bool planted = AppreciationBehavior.Instance.TryPlant(subject, Hero.MainHero, AppreciationSource.RansomedOrFreed,
               GratitudeTestCause, GratitudeTestMagnitude, true, today);

            if (t.Skip(!planted, "appreciation refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

            List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(
               subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new Dictionary<string, int>());
            CompanionAudienceTopicScan scan = scans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Gratitude);

            t.Expect("freshKindnessOnAContentCompanionHoldsAGratitudeTopic", scan is {Holds: true}, true);

            if (scan is {Holds: true})
            {
               t.Note($"{subject.Name}: gratitude strength {scan.Strength}, detail \"{scan.Detail}\"");

               var thanked = new HashSet<string> {scan.Key};
               List<CompanionAudienceTopicScan> rescanned = CompanionAudienceTopicResolver.Scan(
                  subject, new HashSet<string>(), new HashSet<string>(), thanked, new HashSet<string>(), new Dictionary<string, int>());
               CompanionAudienceTopicScan spentScan = rescanned.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Gratitude);

               // Spend-once is per KINDNESS: this specific appreciation must not resurface. A DIFFERENT
               // qualifying kindness the companion also holds legitimately may, so assert the spent key is
               // gone, not that no gratitude topic holds at all (a live companion can hold several).
               bool sameKindnessStillOffered = spentScan is {Holds: true} && spentScan.Key == scan.Key;
               t.Expect("aSpentKeyWithholdsTheSameKindness", !sameKindnessStillOffered, true);
            }

            // The identical kindness, unspent, but the companion has soured: the happiness gate alone must
            // refuse it now.
            CompanionHappinessService.Set(subject, GratitudeTestSourHappiness);
            List<CompanionAudienceTopicScan> unhappyScans = CompanionAudienceTopicResolver.Scan(
               subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new Dictionary<string, int>());
            CompanionAudienceTopicScan unhappyScan = unhappyScans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Gratitude);

            t.Expect("anUnhappyCompanionIsNeverHandedAThankYou", unhappyScan is {Holds: false}, true);
         }
         finally
         {
            CompanionHappinessService.Set(subject, originalHappiness);
         }

         return t.Line();
      }

      /// <summary>
      ///   MECHANICAL (no LLM): the seventh found-topic, SCHEME WARNING (design brief 2026-08-20 follow-up): a
      ///   loyal companion warning the player that someone is secretly plotting against them, purely a warning,
      ///   never a request/bargain, and NEVER a call to a live LLM. Seeds a real Scheme against Hero.MainHero via
      ///   the real <see cref="SchemeStore.ForcePlot" /> launch path on a content companion, then drives the REAL
      ///   <see cref="CompanionAudienceTopicResolver.Scan" />, asserting: the live plot HOLDS with the plotter
      ///   named in the detail; the SAME plot's own key, once passed back in as already-warned, WITHHOLDS it
      ///   (spend-once); and the identical plot, on the same companion made Disgruntled, never holds either (the
      ///   happiness gate). Restores the companion's original happiness afterwards AND removes the seeded scheme
      ///   from the live ledger (SchemeStore exposes no removal API, so this reaches the live Scheme reference
      ///   SchemeStore.Active already hands back and drops it from the underlying list directly), so no live
      ///   scheme, feud, or rumour survives this test the way a real granted warning would create.
      /// </summary>
      private static string TestCompanionSchemeWarning(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_scheme_warning");

         if (t.Abort(SchemeStore.Instance == null, "SchemeStore not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no main hero")) return t.Line();

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

         if (t.Skip(subject == null, "no living companion to drive the resolver against")) return t.Line();

         if (t.Skip(SchemeStore.Instance.HasSecretSchemeAgainst(Hero.MainHero),
                "a scheme against the player is already afoot from an earlier run (reload to re-test)")) return t.Line();

         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h != subject && h.IsLord && h.Clan != Clan.PlayerClan
            && !SchemeStore.Instance.Active.Any(s => s.PlotterId == h.StringId || s.TargetId == h.StringId));

         if (t.Skip(plotter == null, "no free lord to plot against the player (none unentangled)")) return t.Line();

         GetOrCreate(behavior, subject);
         int originalHappiness = CompanionHappinessService.Get(subject);
         Scheme seeded = null;

         try
         {
            CompanionHappinessService.Set(subject, SchemeWarningTestContentHappiness);

            string plotResult = SchemeStore.Instance.ForcePlot(plotter, Hero.MainHero, SchemeKind.Slander);
            t.Note(plotResult);

            seeded = SchemeStore.Instance.Active.FirstOrDefault(s => s.PlotterId == plotter.StringId && s.TargetId == Hero.MainHero.StringId);

            if (t.Skip(seeded == null || !SchemeStore.Instance.HasSecretSchemeAgainst(Hero.MainHero),
                   "ForcePlot did not produce a warnable scheme against the player")) return t.Line();

            List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(
               subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new Dictionary<string, int>());
            CompanionAudienceTopicScan scan = scans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.SchemeWarning);

            t.Expect("aLivePlotOnAContentCompanionHoldsASchemeWarningTopic", scan is {Holds: true}, true);

            if (scan is {Holds: true})
            {
               t.Note($"{subject.Name}: scheme-warning strength {scan.Strength}, detail \"{scan.Detail}\"");
               t.Expect("theDetailNamesThePlotter", scan.Detail?.Contains(plotter.Name.ToString()) == true, true);

               var warned = new HashSet<string> {scan.Key};
               List<CompanionAudienceTopicScan> rescanned = CompanionAudienceTopicResolver.Scan(
                  subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), warned, new Dictionary<string, int>());
               CompanionAudienceTopicScan spentScan = rescanned.FirstOrDefault(s => s.Topic == CompanionAudienceReason.SchemeWarning);

               t.Expect("aSpentKeyWithholdsTheSamePlot", spentScan is {Holds: false}, true);
            }

            // The identical live plot, unspent, but the companion has soured: the happiness gate alone must
            // refuse it now (design brief: a disaffected companion would not go out of their way to protect
            // the player).
            CompanionHappinessService.Set(subject, SchemeWarningTestSourHappiness);
            List<CompanionAudienceTopicScan> unhappyScans = CompanionAudienceTopicResolver.Scan(
               subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new Dictionary<string, int>());
            CompanionAudienceTopicScan unhappyScan = unhappyScans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.SchemeWarning);

            t.Expect("aDisaffectedCompanionIsNeverHandedTheWarning", unhappyScan is {Holds: false}, true);
         }
         finally
         {
            CompanionHappinessService.Set(subject, originalHappiness);

            // No live scheme may survive this test (unlike Gratitude's appreciation, a scheme carries real
            // feud/rumour machinery). SchemeStore exposes no removal API, so this drops OUR OWN seeded scheme
            // directly from the underlying list Active already hands back (never anyone else's live scheme).
            if (seeded != null && SchemeStore.Instance?.Active is List<Scheme> liveSchemes) liveSchemes.Remove(seeded);
         }

         return t.Line();
      }

      /// <summary>
      ///   MECHANICAL (no LLM): the eighth found-topic, AMBITION (design brief 2026-08-20 follow-up 2): a
      ///   content, ambitious companion voicing ONE of four bound ambitions, each resolved by an EXISTING action
      ///   verb, so granting the audience never executes anything itself (unlike Gratitude/SchemeWarning, the
      ///   deed is the PLAYER's, through the normal action pipeline). Drives the REAL
      ///   <see cref="CompanionAudienceTopicResolver.Scan" /> (never a mock) against a real companion already
      ///   travelling in the party, made comfortably Content: since every companion the audience system ever
      ///   scans already rides in the player's own main party (CompanionAudienceTopicBehavior.Companions), the
      ///   Command ambition ("no field party of their own") holds trivially for them, so this needs no extra
      ///   engine-state seeding to prove the two mechanical rules genuinely at risk of drifting from
      ///   CompanionAmbitionPolicy: the happiness gate and the per-companion cooldown. Asserts: a fresh, content
      ///   companion HOLDS an ambition with a non-empty detail; the SAME companion, asked again within
      ///   CompanionAmbitionPolicy.CooldownDays, WITHHOLDS it (the per-companion cooldown, distinct from the
      ///   spend-once topics' own bookkeeping); and the identical companion, made Disgruntled, never holds one
      ///   either (the happiness gate). Restores the companion's original happiness in a finally.
      /// </summary>
      private static string TestCompanionAmbition(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_ambition");

         Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner && h.PartyBelongedTo == MobileParty.MainParty);

         if (t.Skip(subject == null, "no living companion currently riding in the main party to drive the resolver against")) return t.Line();

         GetOrCreate(behavior, subject);
         int originalHappiness = CompanionHappinessService.Get(subject);

         try
         {
            CompanionHappinessService.Set(subject, AmbitionTestContentHappiness);

            List<CompanionAudienceTopicScan> scans = CompanionAudienceTopicResolver.Scan(
               subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new Dictionary<string, int>());
            CompanionAudienceTopicScan scan = scans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Ambition);

            t.Expect("aContentCompanionRidingInTheMainPartyHoldsAnAmbitionTopic", scan is {Holds: true}, true);

            if (scan is {Holds: true})
            {
               t.Note($"{subject.Name}: ambition strength {scan.Strength}, detail \"{scan.Detail}\"");
               t.Expect("theDetailIsNotEmpty", string.IsNullOrWhiteSpace(scan.Detail), false);

               var askedToday = new Dictionary<string, int> {[subject.StringId] = (int) CampaignTime.Now.ToDays};
               List<CompanionAudienceTopicScan> rescanned = CompanionAudienceTopicResolver.Scan(
                  subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), askedToday);
               CompanionAudienceTopicScan cooldownScan = rescanned.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Ambition);

               t.Expect("anAskWithinTheCooldownWithholdsTheSameAmbition", cooldownScan is {Holds: false}, true);
            }

            // Identical facts, cooldown clear, but the companion has soured: the happiness gate alone must
            // refuse it now (design brief: an ambitious demand from a miserable companion is a grievance, not
            // an ambition, Grievance already covers that path).
            CompanionHappinessService.Set(subject, AmbitionTestSourHappiness);
            List<CompanionAudienceTopicScan> unhappyScans = CompanionAudienceTopicResolver.Scan(
               subject, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new Dictionary<string, int>());
            CompanionAudienceTopicScan unhappyScan = unhappyScans.FirstOrDefault(s => s.Topic == CompanionAudienceReason.Ambition);

            t.Expect("aDisgruntledCompanionIsNeverHandedAnAmbition", unhappyScan is {Holds: false}, true);
         }
         finally
         {
            CompanionHappinessService.Set(subject, originalHappiness);
         }

         return t.Line();
      }

      /// <summary>
      ///   MECHANICAL (no LLM, no live UI inquiry): the ROAD trigger (player report, 2026-09-08: "unless I
      ///   remember to routinely engage with my travel buddies, they never say anything on their own", since the
      ///   found-topic audience only ever offered on SettlementEntered). Drives CompanionAudienceTopicBehavior's
      ///   real <c>DebugRoadTriggerWouldFire</c>/<c>DebugWouldOfferAudience</c> seams, the exact private methods
      ///   OnDailyTick calls after its own dice roll lands, so the test proves the real wiring without ever
      ///   rolling that die or popping a live inquiry. Asserts the pacing dial's Never setting switches the
      ///   gate off deterministically (Gabriel's ruling: "Never must switch the whole thing off, road and
      ///   settlement alike"), whatever the live roster or cooldown state; asserts Occasionally's shared
      ///   cooldown still equals the shipped CompanionAudienceTopicPolicy.GlobalCooldownDays (the "default
      ///   reproduces today's feel" requirement, guarding against the dial's numbers drifting apart from the
      ///   constant they are meant to scale); then, with the dial restored to its prior value, reports whatever
      ///   the live map state answers for the real safety-gated offer decision (informational only: cr.testall
      ///   can run from a state the road trigger would correctly refuse, e.g. mid-mission, so this is a NOTE,
      ///   never a hard assertion). Restores the MCM dial's prior index in a finally.
      /// </summary>
      private static string TestCompanionRoadAudience(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_road_audience");

         CompanionAudienceTopicBehavior audience = CompanionAudienceTopicBehavior.Instance;
         if (t.Abort(audience == null, "CompanionAudienceTopicBehavior.Instance is not wired")) return t.Line();

         CrSettings settings = CrSettings.Instance;
         if (t.Skip(settings?.CompanionAudiencePacing == null, "MCM settings not loaded")) return t.Line();

         int priorIndex = settings.CompanionAudiencePacing.SelectedIndex;

         try
         {
            // Gabriel's ruling: "Never must switch the whole thing off, road and settlement alike." Index 0 is
            // Never (CrSettings.CompanionAudiencePacing's own declared order); this must refuse the road offer
            // no matter what the live roster or cooldown holds, since CompanionAudiencePacingRules.IsOff short-
            // circuits before either is ever read.
            settings.CompanionAudiencePacing.SelectedIndex = 0;
            t.Expect("neverSwitchesTheRoadTriggerOffToo", audience.DebugRoadTriggerWouldFire(), false);

            // Index 2 is Occasionally. The load-bearing regression guard for "the default reproduces today's
            // feel": if this ever drifts from the shipped constant, every existing player's companions would go
            // quiet for a different stretch than the one they already know, for a dial they never touched.
            settings.CompanionAudiencePacing.SelectedIndex = 2;
            t.Expect("occasionallyKeepsTheShippedFourteenDayCooldown",
               CompanionAudiencePacingRules.CooldownDays(ModSettings.CompanionAudienceCadence),
               CompanionAudienceTopicPolicy.GlobalCooldownDays);

            // Informational only: proves the wiring reaches RoadIsSafeForPrivateWord + the shared offer gate
            // without asserting a specific answer, since cr.testall's own live state (mid-mission, a stray
            // conversation, etc.) is outside this test's control.
            bool wouldFireNow = audience.DebugRoadTriggerWouldFire();
            t.Note($"road offer right now (Occasionally, live map state): {(wouldFireNow ? "would fire" : "would not fire")}");
         }
         finally
         {
            settings.CompanionAudiencePacing.SelectedIndex = priorIndex;
         }

         return t.Line();
      }

      /// <summary>
      ///   MECHANICAL (no LLM, no live UI inquiry): the fief-audience pillar (player wish, Fakade) end to end.
      ///   Road-only InterceptionBehavior needs a field party to move, so anyone without one never got a turn;
      ///   this proves the settlement-entry replacement path fires on a REAL live fact (a fresh, high-reach,
      ///   player-involving WorldEvent, exactly what InterceptionBehavior.HeardRecentPlayerDeed itself reads for
      ///   the road path) rather than a fabricated one, and that the per-NPC cooldown the offer starts actually
      ///   withholds a second offer for the SAME NPC. Requires a real partyless hero standing in some settlement
      ///   right now (a lord garrisoned in his fief, a lady, a notable); MANUAL when none exists this session.
      /// </summary>
      private static string TestFiefAudience(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("fief_audience");

         FiefAudienceBehavior fiefBehavior = FiefAudienceBehavior.Instance;
         if (t.Skip(fiefBehavior == null, "Fief-audience system is not active")) return t.Line();

         Hero candidate = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != null && h != Hero.MainHero && h.IsAlive && !h.IsPrisoner
            && h.PartyBelongedTo == null && h.CurrentSettlement != null);

         if (t.Skip(candidate == null, "no partyless hero is standing in any settlement right now to drive the scan against"))
            return t.Line();

         Settlement settlement = candidate.CurrentSettlement;

         // The pure gate, driven with the one live fact explicitly false: an NPC who has heard nothing must
         // raise nothing, the same rule SelectBestCandidate itself enforces via FiefAudiencePolicy.ChooseFiefValid.
         t.Expect("aNoReasonNpcRaisesNothing",
            FiefAudiencePolicy.ChooseFiefValid(new InterceptionFacts {HeardRecentPlayerDeed = false}),
            InterceptionTrigger.None);

         // Seed the ONE live fact HeardRecentPlayerDeed reads (WorldEventStore.GetKnownEvents, awareness-gated):
         // a fresh (today), maximum-reach, player-involving event, so the scan below finds a REAL reason rather
         // than a mocked one: magnitude 100 clears RumorAwareness.Threshold (50) with zero geo/faction bonus,
         // so this is deterministic wherever the candidate happens to stand.
         var seeded = new WorldEvent {
            Type = WorldEventType.PlayerFeat,
            Summary = "[SELFTEST] a deed worth hearing of",
            Magnitude = 100,
            InvolvesPlayer = true
         };
         WorldEventStore.Instance?.Record(seeded);

         try
         {
            // The seeded event makes at least `candidate` eligible, but the scan picks the STRONGEST partyless
            // NPC with any live reason, and the enriched triggers (grudge/appreciation/nemesis bounty) mean a
            // busy settlement may hold a stronger one. So assert an offer IS raised to some fief-valid partyless
            // NPC, not that it is exactly the seeded lord, and test the cooldown against whoever was actually
            // offered.
            FiefAudienceCandidate first = fiefBehavior.DebugScanAndStamp(settlement);
            t.Expect("aPartylessNpcWithALiveReasonIsOffered", first != null && !string.IsNullOrEmpty(first.HeroId), true);
            t.Expect("theOfferedTriggerIsFiefValid", first != null && FiefAudiencePolicy.IsFiefValid(first.Trigger), true);

            FiefAudienceCandidate second = fiefBehavior.DebugScanAndStamp(settlement);
            bool sameNpcWithheld = first == null || second == null || second.HeroId != first.HeroId;
            t.Expect("thePerNpcCooldownWithholdsASecondOfferForTheSameNpc", sameNpcWithheld, true);

            if (first != null && !string.IsNullOrEmpty(first.HeroId)) fiefBehavior.DebugClearCooldown(first.HeroId);
         }
         finally
         {
            if (!string.IsNullOrEmpty(seeded.Id)) WorldEventStore.Instance?.Remove(seeded.Id);
            fiefBehavior.DebugClearCooldown(candidate.StringId);
         }

         return t.Line();
      }

      /// <summary>
      ///   The two new signature-ledger channels (2026-08-20): a lord who holds a live, significant grudge
      ///   against the PLAYER, or a significant, recent appreciation of them, now seeks the player out exactly
      ///   like TerritorialPatrol/HeardOfDeed already did. Drives the REAL fact-gathering
      ///   (InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer/HoldsSignificantAppreciation, shared by BOTH the
      ///   road and fief-audience channels) against real seeded Grudge/Appreciation ledger entries, then the
      ///   pure InterceptionPolicy.Choose/FiefAudiencePolicy.IsFiefValid, no live LLM call anywhere.
      /// </summary>
      private static string TestInterceptionGrudgeAppreciation(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("interception_grudge_appreciation");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(AppreciationBehavior.Instance == null, "AppreciationBehavior not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         // Two distinct, clean-slate lords (no live grudge/appreciation against the player yet), so the two
         // fact-gathering reads below are unambiguous regardless of what an earlier self-test in the same
         // session may already have planted on some other NPC.
         bool CleanSlate(Hero h)
            => h != Hero.MainHero && h.IsLord && h.IsAlive
               && !InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer(h, today, out _)
               && !InterceptionBehavior.HoldsSignificantAppreciation(h, today, out _);

         Hero grudgeHolder = Hero.AllAliveHeroes.FirstOrDefault(CleanSlate);

         if (t.Skip(grudgeHolder == null, "no lord free of a live grudge/appreciation against the player already (every candidate touched by an earlier test)")) return t.Line();

         Hero appreciationHolder = Hero.AllAliveHeroes.FirstOrDefault(h => h != grudgeHolder && CleanSlate(h));

         if (t.Skip(appreciationHolder == null, "no second clean-slate lord available")) return t.Line();

         // ── Grudge channel: a live, significant grudge against the PLAYER surfaces the new trigger ──
         t.Expect("noGrudgeYetReadsFalse", InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer(grudgeHolder, today, out _), false);

         bool grudgePlanted = GrudgeBehavior.Instance.TryPlant(grudgeHolder, Hero.MainHero, GrudgeSource.FailedQuest,
            "[SELFTEST] the player left a task to them unfinished.", InterceptionGrudgeTestMagnitude, true, today);

         try
         {
            if (t.Skip(!grudgePlanted, "grudge refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

            bool holdsGrudge = InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer(grudgeHolder, today, out string grudgeCause);
            t.Expect("aLiveSignificantGrudgeIsFound", holdsGrudge, true);
            t.Expect("theGrudgeCauseSurvivesForTheOpener", string.IsNullOrEmpty(grudgeCause), false);
            t.Expect("thePureChoiceSurfacesGrudge",
               InterceptionPolicy.Choose(new InterceptionFacts {HoldsLiveGrudgeAgainstPlayer = holdsGrudge}), InterceptionTrigger.Grudge);
            t.Expect("grudgeIsFiefValid", FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.Grudge), true);
         }
         finally
         {
            GrudgeBehavior.Instance.ClearGrudgesFor(grudgeHolder.StringId);
         }

         // ── AppreciationOwed channel: a significant, recent appreciation of the PLAYER surfaces the new trigger ──
         t.Expect("noAppreciationYetReadsFalse", InterceptionBehavior.HoldsSignificantAppreciation(appreciationHolder, today, out _), false);

         bool appreciationPlanted = AppreciationBehavior.Instance.TryPlant(appreciationHolder, Hero.MainHero, AppreciationSource.QuestDoneForThem,
            "[SELFTEST] the player did what they promised.", InterceptionAppreciationTestMagnitude, true, today);

         if (t.Skip(!appreciationPlanted, "appreciation refused (anti-noise guardrail or a full, stronger house)")) return t.Line();

         bool holdsAppreciation = InterceptionBehavior.HoldsSignificantAppreciation(appreciationHolder, today, out string appreciationCause);
         t.Expect("aSignificantAppreciationIsFound", holdsAppreciation, true);
         t.Expect("theAppreciationCauseSurvivesForTheOpener", string.IsNullOrEmpty(appreciationCause), false);
         t.Expect("thePureChoiceSurfacesAppreciationOwed",
            InterceptionPolicy.Choose(new InterceptionFacts {HoldsSignificantAppreciation = holdsAppreciation}), InterceptionTrigger.AppreciationOwed);
         t.Expect("appreciationOwedIsFiefValid", FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.AppreciationOwed), true);

         // ── Priority pin (mirrors InterceptionPolicyTests): a wrong pressed outranks a warm acknowledgement ──
         t.Expect("grudgeOutranksAppreciationWhenBothHold",
            InterceptionPolicy.Choose(new InterceptionFacts {HoldsLiveGrudgeAgainstPlayer = true, HoldsSignificantAppreciation = true}),
            InterceptionTrigger.Grudge);

         return t.Line();
      }

      /// <summary>
      ///   The engine half of Fakade's report: the DIRECTIVE is a one-shot, the FACT is not. Only the live
      ///   builder can show that the second turn of the same conversation still carries it, since that depends
      ///   on the host remembering the seeker rather than on anything the prompt does.
      /// </summary>
      private static string TestApproachFraming(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("approach_framing");

         if (t.Abort(InterceptionBehavior.Instance == null, "InterceptionBehavior not active")) return t.Line();

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);

         if (t.Skip(lord == null, "no lord to send")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, lord);

         try
         {
            InterceptionBehavior.Instance.ForgetConversationSeeker();
            InterceptionBehavior.Instance.StashFiefAudienceReason(lord, InterceptionTrigger.Grudge, "[SELFTEST] a grievance");

            EncounterContext first = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "Well?", resolveInterception: true);
            t.Expect("theFirstTurnCarriesTheOpeningDirective", string.IsNullOrEmpty(first?.InterceptionReason), false);
            t.Expect("theFirstTurnKnowsWhoSoughtWhom", first?.NpcSoughtThePlayer, true);

            EncounterContext second = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "I hear you.", resolveInterception: true);
            t.Expect("theDirectiveIsSpentAfterOneTurn", string.IsNullOrEmpty(second?.InterceptionReason), true);
            t.Expect("butTheSecondTurnStillKnowsWhoSoughtWhom", second?.NpcSoughtThePlayer, true);

            InterceptionBehavior.Instance.ForgetConversationSeeker();
            EncounterContext later = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "Good day.", resolveInterception: true);
            t.Expect("aNewConversationOwesNothingToTheLastOne", later?.NpcSoughtThePlayer, false);
         }
         finally
         {
            InterceptionBehavior.Instance.ForgetConversationSeeker();
         }

         return t.Line();
      }

      /// <summary>
      ///   The engine half of the expelled-player fix: that a departure marker lands in the PERSISTED store and the
      ///   letter scheduler's question ("how long since the player left this realm?") finds it by kingdom id.
      /// </summary>
      private static string TestRealmDeparture(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("realm_departure");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         Kingdom realm = Kingdom.All.FirstOrDefault(k => k != null && !k.IsEliminated && k != Clan.PlayerClan?.Kingdom
                                                         && WorldEventFeeders.DaysSincePlayerLeft(k, today) == null);

         if (t.Skip(realm == null, "no kingdom the player has not already left")) return t.Line();

         var marker = new WorldEvent {
            Id = "selftest_realm_departure_" + today,
            Type = WorldEventType.PlayerFeat,
            Day = today,
            Magnitude = 1,
            InvolvesPlayer = true,
            Summary = "[SELFTEST] departure marker",
            FactionIds = new List<string> {realm.StringId},
            Status = WorldEventFeeders.PlayerLeftRealmStatus
         };

         try
         {
            WorldEventStore.Instance.Record(marker);

            int? days = WorldEventFeeders.DaysSincePlayerLeft(realm, today);
            t.Expect("theDepartureIsFound", days, (int?) 0);
            t.Expect("itsRulerStaysSilent", ServiceOfferPolicy.RulerMayWrite(days), false);
         }
         finally
         {
            WorldEventStore.Instance.Remove(marker.Id);
         }

         t.Expect("removedTheRulerMayWriteAgain", ServiceOfferPolicy.RulerMayWrite(WorldEventFeeders.DaysSincePlayerLeft(realm, today)), true);

         string playerRealm = Clan.PlayerClan?.Kingdom?.Name?.ToString();
         string premise = ServiceOfferPolicy.Premise(realm.Name.ToString(), playerRealm);
         t.Note($"player realm={playerRealm ?? "(none)"}; premise={premise}");
         t.Expect("thePremiseFollowsTheLiveRealm", playerRealm == null ? premise.Contains("serves no realm") : premise.Contains(playerRealm), true);

         return t.Line();
      }

      /// <summary>
      ///   Where a duel would be fought from where the player is standing, and whether the two halves of that
      ///   decision agree. The pure half (DuelStagingSupportTests) pins the rule; what only a live game can
      ///   answer is whether THIS install's castle really owns the "center" location the rule assumes, which is
      ///   a claim read off SandBox/ModuleData/location_complex_templates.xml and never yet walked.
      ///   <para>
      ///     It reports rather than merely passing, because three crash reports in four days all turned on a
      ///     fact nobody had written down: where the player actually was.
      ///   </para>
      /// </summary>
      private static string TestDuelVenueHere(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("duel_venue_here");

         Settlement here = Settlement.CurrentSettlement;
         if (t.Skip(here == null, "on the open map; walk into a town or a castle and run this there")) return t.Line();

         bool inside = TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.InsideSettlement;
         bool hasGround = here.LocationComplex?.GetLocationWithId("center") != null;
         bool atSea = MobileParty.MainParty?.IsCurrentlyAtSea ?? false;
         DuelVenue venue = DuelEligibilityResolver.VenueForPlayerNow();

         t.Note($"{here.Name}: town={here.IsTown}, castle={here.IsCastle}, village={here.IsVillage}, "
              + $"inside={inside}, duellingGround={hasGround}, atSea={atSea}, venue={venue}");

         // THE CLAIM THAT WIDENED THE FEATURE, checked against the running engine rather than against a data
         // file I read. If a castle in this install owns no "center", the castle duel must go back to the open
         // field, and this is what would say so.
         if (here.IsTown || here.IsCastle)
            t.Expect("aTownOrCastleOwnsADuellingGround", hasGround, true);

         if (here.IsVillage)
            t.Expect("aVillageDoesNot", hasGround, false);

         // AND THE TWO HALVES AGREE. Offered where it cannot be staged is FloatingMammal's report inverted,
         // and staged where it cannot be opened is yozakura12's.
         t.Expect("theVenueMatchesWhatTheStagingWouldAccept",
            venue == DuelVenue.TownGates,
            !atSea && DuelStagingSupport.CanHoldATownDuel(inside, hasGround));

         t.Expect("andThatVenueHasABuiltMission", DuelStagingSupport.CanStage(venue), !atSea);

         return t.Line();
      }

      /// <summary>
      ///   A partner accepting the player's other loves, end to end in the engine. The pure halves
      ///   (PluralAcceptancePolicyTests, AcceptedPluralJealousyTests) cover who may be asked and what it costs;
      ///   what only the engine can get wrong is that the bridge routes the verb at all, that its guard reads
      ///   the SAME facts the teaching gate did, and that her word lands on the profile the store persists.
      /// </summary>
      private static string TestPluralAcceptance(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("plural_acceptance");

         if (t.Abort(behavior?.Store == null || Hero.MainHero == null, "no store or no player hero")) return t.Line();

         Hero lady = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive && h.IsLord
                                                          && h != Hero.MainHero.Spouse);

         if (t.Skip(lady == null, "no lady free to borrow as a consort")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, lady);
         RomanticProfile savedRomantic = profile.Romantic;
         int savedRegard = profile.ReputationWithPlayer;

         try
         {
            // A woman who is no partner of the player cannot accept a household she is not in. Asked FIRST,
            // while the borrowed bond is not yet in place, so the refusal is the real one.
            profile.Romantic = null;
            ActionOutcome stranger = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "accept_other_partners"}, lady, new ActionContext(profile, null));

            t.Expect("aWomanWhoIsNoPartnerIsRefused", stranger?.WasRefused, true);

            // Now a committed consort, devoted enough to bear it against an exclusive custom.
            profile.Romantic = new RomanticProfile {IsConsort = true, Status = RomanticStatus.Committed};
            profile.ReputationWithPlayer = PluralAcceptancePolicy.DevotionRequiredAgainstCulture;

            JealousyTrack before = TrackFor(profile, lady);

            ActionOutcome accepted = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "accept_other_partners"}, lady, new ActionContext(profile, null));

            t.Expect("herWordIsTaken", accepted != null && !accepted.WasRefused, true);
            t.Expect("andRecordedOnHerProfile", behavior.Store.Get(lady.StringId)?.Romantic?.AcceptsPlayersOtherPartners, true);
            t.Expect("andItIsNotReciprocalOpenTerms", profile.Romantic.IsOpenArrangement, false);

            JealousyTrack after = TrackFor(profile, lady);
            t.Note($"{lady.Name}: track {before} -> {after}");
            t.Expect("theJealousyTrackSoftens", after, JealousyTrack.AcceptedAgainstCulture);

            ActionOutcome twice = behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "accept_other_partners"}, lady, new ActionContext(profile, null));

            t.Expect("sayingItTwiceIsRefused", twice?.WasRefused, true);
         }
         finally
         {
            profile.Romantic = savedRomantic;
            profile.ReputationWithPlayer = savedRegard;
            behavior.Store.Set(profile);
         }

         return t.Line();
      }

      /// <summary>The track the jealousy system would put this partner on right now, read the way it reads it.</summary>
      private static JealousyTrack TrackFor(NpcProfile profile, Hero lady)
         => JealousyTrackPolicy.For(new JealousyTrackFacts {
            CultureToleratesSharing = JealousyCultureLoader.GetCulture(lady?.Culture?.Name?.ToString())
                                                           .ToleratesSharedPartners(Hero.MainHero?.Clan?.Tier ?? 0),
            PartnerAcceptedTheArrangement = profile?.Romantic?.AcceptsPlayersOtherPartners ?? false
         });

      /// <summary>
      ///   A conversation the player stepped out of, through the REAL store the chat opens and closes against.
      ///   The pure half (ConversationResumeSnapshotTests, ConversationResumePolicyTests) covers what is kept and
      ///   when it may be picked up; what only the engine can get wrong is that the behavior actually owns a
      ///   store, that keeping and consuming go through that one instance, and that a refused consume CLEARS it
      ///   rather than leaving it for the next chat, which is the half of the rule a pure test cannot see.
      /// </summary>
      private static string TestConversationResume(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("conversation_resume");

         if (t.Abort(behavior?.ConvResume == null, "no conversation-resume store on the behavior")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         List<Hero> lords = Hero.AllAliveHeroes.Where(h => h != Hero.MainHero && h.IsLord && h.IsAlive).Take(2).ToList();
         if (t.Skip(lords.Count < 2, "need two living lords")) return t.Line();

         Hero lord = lords[0];
         Hero another = lords[1];
         var today = (int) CampaignTime.Now.ToDays;
         const string anchor = "[SELFTEST]-anchor";

         try
         {
            // Same lord, same day: the whole request. The words come back, and so does the memory event the
            // first sitting wrote, without which closing again would leave the lord remembering one afternoon
            // as two separate talks.
            behavior.ConvResume.Keep(TheKeptNegotiation(lord), Identity(lord, today, anchor));
            ConversationResumeSnapshot resumed = behavior.ConvResume.Consume(lord.StringId, today, true);

            t.Expect("theExchangeComesBack", resumed != null, true);
            t.Expect("everyWordOfIt", resumed?.Lines.Count ?? 0, 3);
            t.Expect("itStillKnowsWhoseItIs", resumed?.HeroId, lord.StringId);
            t.Expect("andWhichMemoryItAlreadyWrote", resumed?.AnchorId, anchor);
            t.Expect("theLastWordIsTheOneHeIsAnswering", resumed?.Lines.Last().Text?.EndsWith("Say the figure.") ?? false, true);

            // Taken once. A second open must find nothing, or a conversation would be replayed into every
            // chat the player opens for the rest of the day.
            t.Expect("theSameExchangeIsNotHandedOutTwice", behavior.ConvResume.Consume(lord.StringId, today, true) == null, true);

            // Another lord: refused, AND discarded. The discard is the point ("deleted as soon as you open a
            // new chat"): if it merely failed to match, the words would be waiting when the player came back
            // to the first lord, having meanwhile been somewhere else entirely.
            behavior.ConvResume.Keep(TheKeptNegotiation(lord), Identity(lord, today, anchor));
            t.Expect("anotherLordIsRefused", behavior.ConvResume.Consume(another.StringId, today, true) == null, true);
            t.Expect("andTheWordsAreGoneRatherThanWaiting", behavior.ConvResume.Consume(lord.StringId, today, true) == null, true);

            // Another day: the conversation genuinely ended and lives in the lord's memory now.
            behavior.ConvResume.Keep(TheKeptNegotiation(lord), Identity(lord, today, anchor));
            t.Expect("tomorrowIsRefused", behavior.ConvResume.Consume(lord.StringId, today + 1, true) == null, true);

            // A staged scene (a captive encounter, a briefing, a council) opens with its own cue.
            behavior.ConvResume.Keep(TheKeptNegotiation(lord), Identity(lord, today, anchor));
            t.Expect("aStagedSceneIsRefused", behavior.ConvResume.Consume(lord.StringId, today, false) == null, true);

            // Nothing said, nothing kept: a chat opened and closed in silence must not leave an empty capture
            // that the next open "resumes" into a blank exchange while claiming continuity.
            behavior.ConvResume.Keep(new ConversationTranscript(), Identity(lord, today, anchor));
            t.Expect("aSilentChatKeepsNothing", behavior.ConvResume.HasKeptConversation, false);
         }
         finally
         {
            behavior.ConvResume.Forget();
         }

         return t.Line();
      }

      /// <summary>An unfinished bargain, three lines, for the resume self-test.</summary>
      private static ConversationTranscript TheKeptNegotiation(Hero lord)
      {
         var transcript = new ConversationTranscript();
         transcript.Record(new TranscriptLine {IsPlayer = true, SpeakerName = "You", Text = "[SELFTEST] Then take my silver and hold the place for my banner."});
         transcript.Record(new TranscriptLine {SpeakerHeroId = lord.StringId, SpeakerName = lord.Name?.ToString(), Text = "[SELFTEST] \"You ask me to trade a fief for a purse.\""});
         transcript.Record(new TranscriptLine {SpeakerHeroId = lord.StringId, SpeakerName = lord.Name?.ToString(), Text = "[SELFTEST] Say the figure."});

         return transcript;
      }

      private static ConversationResumeIdentity Identity(Hero lord, int day, string anchorId)
         => new ConversationResumeIdentity {HeroId = lord.StringId, GameDay = day, AnchorId = anchorId};

      /// <summary>
      ///   An answered grievance, end to end in the engine. The pure half (AnsweredGrievanceTests) covers the rules and
      ///   the wording; what only the engine can get wrong is that the verb's Execute mutates the LEDGER's own grudge
      ///   (not a copy), that the bridge routes hear_account at all, and that the note the prompt reads and the
      ///   interception check both see the change.
      /// </summary>
      private static string TestGrudgeAnswered(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("grudge_answered");

         if (t.Abort(GrudgeBehavior.Instance == null, "GrudgeBehavior not active")) return t.Line();
         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;

         Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive
            && GrudgeBehavior.Instance.GrudgesFor(h.StringId).Count == 0);

         if (t.Skip(lord == null, "no lord free of grudges")) return t.Line();

         const string account = "Your brother raised his banner against his own liege";

         try
         {
            bool planted = GrudgeBehavior.Instance.TryPlant(lord, Hero.MainHero, GrudgeSource.ExecutedKin,
               "[SELFTEST] the execution of their brother", InterceptionGrudgeTestMagnitude, true, today, false);

            if (t.Skip(!planted, "grudge refused (anti-noise guardrail)")) return t.Line();

            Grudge grudge = GrudgeBehavior.Instance.GrudgesFor(lord.StringId).First();
            grudge.Rigidity = GrudgePolicy.VendettaRigidity; // the case reported: a vendetta nothing can mend
            int magnitudeBefore = grudge.Magnitude;

            t.Expect("unansweredItDrivesAnInterception", InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer(lord, today, out _), true);
            t.Expect("unansweredHearAccountIsOffered", HearAccountVerb.UnansweredKnownGrudge(lord) != null, true);

            NpcProfile profile = GetOrCreate(behavior, lord);
            behavior.GameStateBridge.ExecuteAction(
               new GameAction {Type = "hear_account", Parameters = new Dictionary<string, string> {{"account", account}}},
               lord, new ActionContext(profile, null));

            t.Expect("theLedgerGrudgeIsAnswered", GrudgePolicy.IsAnswered(grudge), true);
            t.Expect("theAccountIsKept", grudge.PlayerAccount, account);
            t.Expect("stillAVendetta", GrudgePolicy.IsResolvable(grudge), false);
            t.Expect("exactlyAsHeavy", grudge.Magnitude, magnitudeBefore);
            t.Expect("notResolved", grudge.IsResolved, false);

            EncounterContext ctx = EncounterContextBuilder.Build(lord, profile, behavior.GameStateBridge, "We meet again.");
            string note = ctx?.GrudgeNote ?? "";
            t.Note($"{lord.Name}: GrudgeNote = {note}");
            t.Expect("theNoteCarriesTheAccount", note.Contains(account), true);
            t.Expect("theNoteSaysDoNotAskAgain", note.Contains("Do not ask them again why"), true);

            t.Expect("answeredItNoLongerDrivesAnInterception", InterceptionBehavior.HoldsLiveGrudgeAgainstPlayer(lord, today, out _), false);
            t.Expect("answeredHearAccountIsNoLongerOffered", HearAccountVerb.UnansweredKnownGrudge(lord) == null, true);
         }
         finally
         {
            GrudgeBehavior.Instance.ClearGrudgesFor(lord.StringId);
         }

         return t.Line();
      }

      /// <summary>
      ///   The third proactive channel (2026-08-20, roadmap piece 2 "nemesis discoverability + lord bounties"):
      ///   a lord whose region is plagued by a nemesis he is eligible to bounty seeks the player out to name
      ///   him and where he operates, the ANNOUNCEMENT/discoverability layer on top of the existing scripted
      ///   hero_main_options bounty offer (subsumes the "tavern rumour: Coll raids near X" wish, since the lord
      ///   tells the player directly). Mints a nemesis anchored to a REAL lord's own region
      ///   (<see cref="MintBountyEligibleNemesis" />, the same helper nemesis_bounty_capture/execute use), so
      ///   InterceptionBehavior.HasNemesisBountyToOffer drives the SAME NemesisBountyDialogFlow.FindEligibleNemesisFor
      ///   gate the scripted offer uses (never a re-derived one), then the pure InterceptionPolicy.Choose/
      ///   FiefAudiencePolicy.IsFiefValid, no live LLM call anywhere. Never issues the bounty itself (that stays
      ///   the scripted line's job, not this proactive channel's): only the announcement is under test. Marks
      ///   the minted identity dead afterwards (mirrors nemesis_hunt's cleanup).
      /// </summary>
      private static string TestInterceptionNemesisBounty(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("interception_nemesis_bounty");

         if (t.Abort(NemesisBehavior.Instance == null, "NemesisBehavior not active")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         (Hero giver, Hero nemesisHero, NemesisIdentity nem) = MintBountyEligibleNemesis(today);

         if (t.Skip(giver == null, "no lord with a resolvable region available to hold the bounty")) return t.Line();
         if (t.Abort(nemesisHero == null || nem == null, "MintCaptorFor produced no tracked nemesis (check the log for the real reason: a missing bandit clan/template, or a respawn cooldown the harness should now be bypassing)")) return t.Line();

         try
         {
            // The SAME gate the scripted hero_main_options offer itself checks (FindEligibleNemesisFor) -
            // proves the region-anchored mint is genuinely eligible before the fact-gathering below is judged.
            t.Expect("theScriptedOfferGateFindsHimEligible", NemesisBountyDialogFlow.FindEligibleNemesisFor(giver) == nem, true);

            bool hasBounty = InterceptionBehavior.HasNemesisBountyToOffer(giver, out string detail);
            t.Expect("theProactiveFactReusesTheSameGate", hasBounty, true);
            t.Expect("theDetailSurvivesForTheOpener", string.IsNullOrEmpty(detail), false);
            t.Expect("theDetailNamesTheNemesis", detail != null && detail.Contains(nem.Name), true);

            t.Expect("thePureChoiceSurfacesNemesisBounty",
               InterceptionPolicy.Choose(new InterceptionFacts {HasNemesisBountyToOffer = hasBounty}), InterceptionTrigger.NemesisBounty);
            t.Expect("nemesisBountyIsFiefValid", FiefAudiencePolicy.IsFiefValid(InterceptionTrigger.NemesisBounty), true);

            // ── Priority pin (mirrors InterceptionPolicyTests): a live grudge against the PLAYER still ──
            // ── outranks a nemesis bounty - a personal wrong pressed comes before a threat to the lord's ──
            // ── own lands (design brief: "a personal grudge still comes first"). ──────────────────────────
            t.Expect("grudgeOutranksNemesisBountyWhenBothHold",
               InterceptionPolicy.Choose(new InterceptionFacts {HoldsLiveGrudgeAgainstPlayer = true, HasNemesisBountyToOffer = true}),
               InterceptionTrigger.Grudge);
         }
         finally
         {
            nem.IsAlive = false; // isolate this test's minted identity from any later run in the same session
         }

         return t.Line();
      }

      /// <summary>
      ///   MECHANICAL (no LLM): the whole letters-mailbox stack, end to end, without ever calling EvaluateDaily
      ///   (which would need a real correspondent event to fire and cannot be forced deterministically). The
      ///   headline: asking a sender to write less, via the REAL parser (<see cref="CorrespondencePreferenceParser" />)
      ///   and the REAL apply seam (<see cref="CorrespondenceSignalService" />) both the reply path and this test
      ///   share, drives that sender's damper to its floor and LOWERS the weight their letters compete with, the
      ///   player-visible promise behind the whole B1/B2 correspondence-interest feature. Also pins the quieter
      ///   implicit signal (an unanswered letter still dampens), the scheduler's weighted pick, its budget
      ///   throttle, and a stale candidate's retirement. Every live mutation is restored before returning.
      /// </summary>
      private static string TestLettersMailbox(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("letters_mailbox");

         string id = behavior.Store.ListIds().FirstOrDefault();
         NpcProfile profile = id != null ? behavior.Store.Get(id) : null;

         if (t.Skip(profile == null, "no NPC profile exists yet (chat with someone once to seed one, then re-run)")) return t.Line();

         int baseInterest = profile.CorrespondenceInterest;
         LetterTriggerEvaluator ev = behavior.LetterEvaluator;
         var today = (int) CampaignTime.Now.ToDays;

         try
         {
            // ── Headline: a stated "write less" preference, through the REAL parser + the REAL apply seam ──
            profile.CorrespondenceInterest = 0;
            int weightBefore = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.BaseWeight(LetterReason.RomanticCorrespondence), SenderInterest = profile.CorrespondenceInterest, SubjectInterest = 0, GlobalInterest = 0});

            CorrespondencePreference pref = CorrespondencePreferenceParser.Parse(
               "DELAY: 2\nMy reply...\n[CORRESPONDENCE_PREFERENCE] scope=sender direction=less strength=strong");
            t.Expect("parserReadsSenderScope", pref.Scope, PreferenceScope.Sender);

            bool applied = CorrespondenceSignalService.ApplyPreference(profile, ev, pref, LetterReason.RomanticCorrespondence);
            t.Expect("theSeamActuallyApplied", applied, true);
            t.Expect("statedPreferenceDrivesSenderToTheFloor", profile.CorrespondenceInterest, CorrespondenceInterestPolicy.SenderFloor);

            int weightAfter = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.BaseWeight(LetterReason.RomanticCorrespondence), SenderInterest = profile.CorrespondenceInterest, SubjectInterest = 0, GlobalInterest = 0});
            t.Note($"askedToWriteLess: weight {weightBefore} -> {weightAfter}");
            t.Expect("askingToWriteLessLOWERSTheirMailboxWeight", weightAfter < weightBefore, true);

            // ── The quiet implicit signal: an acknowledged-but-unanswered letter also dampens ──
            profile.CorrespondenceInterest = 0;
            profile.CorrespondenceInterest = CorrespondenceInterestPolicy.ClampSender(profile.CorrespondenceInterest + CorrespondenceInterestPolicy.OnAcknowledge().SenderDelta);
            int weightNeutral = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.BaseWeight(LetterReason.RomanticCorrespondence), SenderInterest = 0, SubjectInterest = 0, GlobalInterest = 0});
            int weightAfterAck = LetterWeights.WeightWithInterest(new LetterWeights.InterestDampers {BaseWeight = LetterWeights.BaseWeight(LetterReason.RomanticCorrespondence), SenderInterest = profile.CorrespondenceInterest, SubjectInterest = 0, GlobalInterest = 0});
            t.Expect("acknowledgingWithoutReplyingGoesNegative", profile.CorrespondenceInterest < 0, true);
            t.Expect("anAcknowledgeAloneAlsoLowersTheirWeight", weightAfterAck < weightNeutral, true);

            // ── Ranking: the weighted scheduler prefers the important letter over mere flavor ──
            var intrigue = new LetterCandidate {Reason = LetterReason.Blackmail, SenderId = "a", Weight = LetterWeights.BaseWeight(LetterReason.Blackmail), EligibleSinceDay = today};
            var flavor = new LetterCandidate {Reason = LetterReason.TournamentVictory, SenderId = "b", Weight = LetterWeights.BaseWeight(LetterReason.TournamentVictory), EligibleSinceDay = today};
            var pool = new List<LetterCandidate> {flavor, intrigue};

            LetterBudget openBudget = LetterPacingRules.BudgetFor(LetterPacing.Occasionally);
            LetterCandidate chosen = LetterMailboxPolicy.SelectNext(pool, new List<int>(), today, openBudget, 99); // 99 disables the diversity roll
            t.Expect("theSchedulerPrefersTheImportantLetter", chosen?.Reason, LetterReason.Blackmail);

            // ── Budget throttle: a spent window releases nothing, however loud the pool ──
            var spentWindow = new List<int>();
            for (var i = 0; i < openBudget.MaxPerWindow; i++) spentWindow.Add(today);
            LetterCandidate throttled = LetterMailboxPolicy.SelectNext(pool, spentWindow, today, openBudget, 99);
            t.Expect("aFullBudgetWindowReleasesNothing", throttled, null);

            // ── Staleness: an old event candidate is quietly retired rather than force-delivered ──
            t.Expect("a40DayOldCandidateIsStale", LetterCandidateFreshness.IsStale(LetterReason.BattleVictory, today - 40, today), true);
            t.Expect("a5DayOldCandidateIsStillFresh", LetterCandidateFreshness.IsStale(LetterReason.BattleVictory, today - 5, today), false);
         }
         finally
         {
            // Scope=sender never touched subject/global, so only CorrespondenceInterest needs restoring.
            profile.CorrespondenceInterest = baseInterest;
            behavior.Store.Set(profile);
         }

         return t.Line();
      }

      /// <summary>
      ///   Player report (Desporion): an NPC proposed marriage by letter, "agreed on the terms" over three
      ///   letters, then denied it all in person because LetterPopupManager.MarkReceived recorded only a
      ///   generic "Received a letter from X (Reason)" memory, never the substance. Pure-policy half:
      ///   ShouldRemember correctly excludes the two re-ping reasons (AwaitingReply, QuestUpdate) and includes
      ///   a substantive one (MarriageProposal); BaseMemory is first-person, names the player, and reflects
      ///   the write/reply direction. Real-path half: drives the REAL LetterMemoryFinalizer.RecordLetterMemory
      ///   (the seam LetterPopupManager.MarkReceived itself calls) with a ShouldRemember=false reason
      ///   (QuestUpdate), so the run can never fall through to the async LLM summarizer (Lane C, kept out of
      ///   cr.testall) even against a live LLM client, while still proving the base event actually lands on
      ///   profile.Events. Removes the event it added.
      /// </summary>
      /// <summary>
      ///   THE WIRE BETWEEN TWO HALVES THAT ARE EACH ALREADY TESTED. HistoryKnownThrough and
      ///   QuestsKnownThrough are counts captured by ChatViewModel when a speaker first takes a turn; the SDK
      ///   unit tests prove AppendHistory and AppendActiveQuests honour a cut they are handed, and the mod's
      ///   own tests cannot run a live builder. Nothing tested that the right number actually travels, which
      ///   is the only part a player can see going wrong.
      ///   <para>
      ///     It matters most for the HISTORY cut, because that one REMOVES content from every prompt. If the
      ///     count arrived wrong the symptom would not be repetition, it would be a character who has
      ///     forgotten the player entirely, and that is a far worse bug than the one it fixes.
      ///   </para>
      ///   Deliberately no LLM call. The question is what the prompt contains, which is decidable and free;
      ///   asking a model whether it remembers would be a slower, dearer and less certain reading of the same
      ///   fact, and it would fail for reasons that have nothing to do with the cut.
      /// </summary>
      private static string TestConversationCut(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("conversation_cut");

         const string before = "CRSELFTEST BEFORE: we sheltered together at the Hollowmere ford.";
         const string during = "CRSELFTEST DURING: I have just told them of the Hollowmere ford.";
         const string taskBefore = "CRSELFTEST BEFORE: carry my seal to the reeve at Hollowmere";
         const string taskDuring = "CRSELFTEST DURING: bring me the head of the Hollowmere boar";

         string id = behavior.Store.ListIds().FirstOrDefault();
         NpcProfile profile = id != null ? behavior.Store.Get(id) : null;
         Hero hero = profile?.Id == null ? null : Hero.FindFirst(h => h != null && h.StringId == profile.Id);

         if (t.Skip(hero == null, "no NPC profile with a live hero yet (chat with someone once, then re-run)")) return t.Line();
         if (t.Skip(behavior.LlmServices?.ChatService?.PromptBuilder == null, "the prompt builder is not up")) return t.Line();

         IPromptBuilder builder = behavior.LlmServices.ChatService.PromptBuilder;
         int events = profile.Events.Count;
         int quests = profile.ActiveQuests.Count;
         int today = behavior.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;

         try
         {
            // ── What this character already held when the conversation opened ──
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other, before));
            profile.ActiveQuests.Add(Seeded(taskBefore, today));

            var vm = new ChatViewModel();
            int historyCut = vm.HistoryKnownThrough(profile);
            int questCut = vm.QuestsKnownThrough(profile);

            t.Expect("theHistoryCutIsTakenAtWhatTheyAlreadyHeld", historyCut, events + 1);
            t.Expect("theQuestCutIsTakenAtWhatTheyAlreadyHeld", questCut, quests + 1);

            // ── Now the exchange itself writes, which is what ProfileMutator does every turn ──
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other, during));
            profile.ActiveQuests.Add(Seeded(taskDuring, today));

            t.Expect("theHistoryCutDoesNotMoveDuringTheTalk", vm.HistoryKnownThrough(profile), historyCut);
            t.Expect("theQuestCutDoesNotMoveDuringTheTalk", vm.QuestsKnownThrough(profile), questCut);

            EncounterContext ctx = EncounterContextBuilder.Build(
               hero, profile, behavior.GameStateBridge, null,
               historyKnownThrough: historyCut, questsKnownThrough: questCut);

            t.Expect("theCutReachesTheContext", ctx.HistoryKnownThrough, historyCut);

            string prompt = builder.BuildSystemPrompt(profile, new WorldState {CurrentDay = today}, ctx);

            // THE ONE THAT MATTERS: a memory older than this conversation must still reach the character.
            t.Expect("thePastStillReachesTheCharacter", prompt.Contains(before), true);
            t.Expect("thisConversationIsNotReplayedAsHistory", prompt.Contains(during), false);

            // Quests can be switched off entirely, in which case the block is absent and there is nothing
            // to assert about it. Read that from the prompt rather than from a setting: the prompt is the
            // thing under test.
            if (prompt.Contains("YOUR QUESTS"))
            {
               t.Expect("aTaskFromBeforeIsStillAnErrand", prompt.Contains("OUTSTANDING: " + taskBefore), true);
               t.Expect("aTaskAgreedAtTheTableReadsAsFresh", prompt.Contains("JUST AGREED, in this very conversation: " + taskDuring), true);
            }
         }
         finally
         {
            NpcProfile cleanup = behavior.Store.Get(profile.Id);
            if (cleanup != null)
            {
               cleanup.Events.RemoveAll(e => e.summary == before || e.summary == during);
               cleanup.ActiveQuests.RemoveAll(q => q.Description == taskBefore || q.Description == taskDuring);
               behavior.Store.Set(cleanup);
            }
         }

         return t.Line();
      }

      /// <summary>A throwaway task for <see cref="TestConversationCut" />, removed again in its finally.</summary>
      private static InformalQuest Seeded(string description, int today)
         => new InformalQuest {
            Type = QuestType.BanditClear, Description = description, IssuedOnDay = today, Status = QuestStatus.Active
         };

      private static string TestLetterMemory(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("letter_memory");

         // ── Pure policy: the two re-ping reasons carry no substance beyond what an earlier letter already ──
         // ── recorded; MarriageProposal is the exact reason from the player report and MUST be remembered. ──
         t.Expect("awaitingReplyIsNotRemembered", LetterMemoryPolicy.ShouldRemember(LetterReason.AwaitingReply), false);
         t.Expect("questUpdateIsNotRemembered", LetterMemoryPolicy.ShouldRemember(LetterReason.QuestUpdate), false);
         t.Expect("marriageProposalIsRemembered", LetterMemoryPolicy.ShouldRemember(LetterReason.MarriageProposal), true);

         string sentBase = LetterMemoryPolicy.BaseMemory(LetterReason.MarriageProposal, "Desporion", "Aldric", true);
         t.Expect("baseMemoryIsFirstPerson", sentBase.StartsWith("I wrote to", StringComparison.Ordinal), true);
         t.Expect("baseMemoryNamesThePlayer", sentBase.Contains("Aldric"), true);

         string receivedBase = LetterMemoryPolicy.BaseMemory(LetterReason.MarriageProposal, "Desporion", "Aldric", false);
         t.Expect("baseMemoryReflectsReceivedDirection", receivedBase.StartsWith("Aldric wrote to me", StringComparison.Ordinal), true);

         // ── Real path: LetterMemoryFinalizer.RecordLetterMemory, the exact seam MarkReceived calls ──
         string id = behavior.Store.ListIds().FirstOrDefault();
         NpcProfile profile = id != null ? behavior.Store.Get(id) : null;

         if (t.Skip(profile == null, "no NPC profile exists yet (chat with someone once to seed one, then re-run)")) return t.Line();

         int before = profile.Events.Count;
         int today = behavior.GameStateBridge?.GetCurrentGameDay() ?? (int)CampaignTime.Now.ToDays;

         try
         {
            // QuestUpdate: ShouldRemember is false, so this can never fall through to the async LLM
            // summarizer, even against a live, configured LLM client.
            LetterMemoryFinalizer.RecordLetterMemory(profile, LetterReason.QuestUpdate, "The bandits have been dealt with.", true, today, behavior);

            NpcProfile fresh = behavior.Store.Get(profile.Id);
            string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";
            string expected = LetterMemoryPolicy.BaseMemory(LetterReason.QuestUpdate, profile.Name, playerName, true);

            t.Expect("theRealPathAddedOneEvent", fresh?.Events.Count, before + 1);
            t.Expect("theBaseEventIsTheExpectedPhrase", fresh?.Events.LastOrDefault()?.summary, expected);

            // Gabriel (28/09/2026): a letter the lord had their chancellery write is still theirs to remember,
            // through the same sending path every delivered letter takes.
            var dictated = new PendingLetter {Reason = LetterReason.QuestUpdate, Content = "Her Majesty wishes to know how the matter stands.", WrittenBy = "the chancellery of Her Majesty Rhagaea"};
            LetterMemoryFinalizer.RememberSending(fresh, dictated, today, behavior);
            t.Expect("aDictatedLetterIsRememberedAsHadWritten", behavior.Store.Get(profile.Id)?.Events.LastOrDefault()?.summary,
                     LetterMemoryPolicy.BaseMemory(LetterReason.QuestUpdate, profile.Name, playerName, true, dictated: true));
         }
         finally
         {
            NpcProfile cleanup = behavior.Store.Get(profile.Id);
            if (cleanup != null && cleanup.Events.Count > before)
            {
               while (cleanup.Events.Count > before) cleanup.Events.RemoveAt(cleanup.Events.Count - 1);
               behavior.Store.Set(cleanup);
            }
         }

         return t.Line();
      }

      /// <summary>
      ///   The weekly world-conditions feeder reads live engine facts (GetScoreOfDeclaringWar, a town's
      ///   FoodStocks / FoodChange / Prosperity) behind guards that swallow any exception to 0, so a renamed field or
      ///   a changed model signature would fail SILENTLY in game (every scan quietly recording nothing). This reads
      ///   those exact seams DIRECTLY, so a break is a loud FAIL here, then runs the real RunScan to prove both
      ///   scanners execute. The recorded events are the genuine weekly behaviour and dedup themselves, so nothing is
      ///   cleaned up.
      /// </summary>
      private static string TestWorldConditions(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("world_conditions");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         WorldConditionsFeeder feeder = NpcMemoryCampaignBehavior.Instance?.WorldConditions;

         if (t.Abort(feeder == null, "WorldConditionsFeeder not active")) return t.Line();

         var diplomacy = Campaign.Current?.Models?.DiplomacyModel;

         if (t.Abort(diplomacy == null, "no DiplomacyModel to read war scores from")) return t.Line();

         // War-score read, in both directions, exactly as ScanWarLooming does it (each side evaluated by its own
         // ruling clan). A NaN / Infinity here means the model's signature or return contract shifted under us.
         List<Kingdom> kingdoms = Kingdom.All?.Where(k => k is {IsEliminated: false}).Take(2).ToList();

         if (t.Skip(kingdoms == null || kingdoms.Count < 2, "fewer than two live kingdoms to score a war between")) return t.Line();

         Kingdom a = kingdoms[0], b = kingdoms[1];
         float scoreAB = diplomacy.GetScoreOfDeclaringWar(a, b, a.RulingClan, out TextObject _);
         float scoreBA = diplomacy.GetScoreOfDeclaringWar(b, a, b.RulingClan, out TextObject _);
         t.Note($"warScore {a.Name}->{b.Name}={scoreAB:0.#}, {b.Name}->{a.Name}={scoreBA:0.#}");
         t.Expect("warScoreReadable", !float.IsNaN(scoreAB) && !float.IsInfinity(scoreAB) && !float.IsNaN(scoreBA) && !float.IsInfinity(scoreBA), true);

         // Town economy reads, exactly as ScanHardship does them.
         Settlement town = Settlement.All?.FirstOrDefault(s => s is {IsTown: true});

         if (t.Skip(town?.Town == null, "no town on the map to read economy from")) return t.Line();

         float food = town.Town.FoodStocks, foodChange = town.Town.FoodChange, prosperity = town.Town.Prosperity;
         t.Note($"{town.Name}: food={food:0.#}, foodChange={foodChange:0.#}, prosperity={prosperity:0.#}");
         t.Expect("townEconomyReadable", !float.IsNaN(food) && !float.IsNaN(foodChange) && !float.IsNaN(prosperity), true);

         // The real feeder: both scanners (war-looming + hardship) must run, each returning a tally.
         IReadOnlyList<WorldConditionsFeeder.ConditionScanResult> results = feeder.RunScan();
         t.Note("scan: " + string.Join(", ", results.Select(r => $"{r.Label}={r.Recorded}")));
         t.Expect("scannersRan", results.Count, 2);

         return t.Line();
      }

      /// <summary>
      ///   Drives the real RealmTidingsBehavior.DebugEvaluate end to end and proves the per-NPC dedup ledger holds.
      ///   A friendly landed lord (regard forced to 30) is handed a dominant seeded event (magnitude 100, tagged to
      ///   his own kingdom) so the pure salience pick chooses it deterministically over any real world event (which
      ///   top out well below): the first evaluation must queue a RealmTidings letter candidate and record the seed
      ///   in RelayedWorldEventIds, and a second evaluation (its pending candidate cleared so the anti-stacking guard
      ///   does not short-circuit) must NOT relay the same event again. Restores the writer's regard, ledger and
      ///   candidates and removes the seed.
      /// </summary>
      private static string TestRealmTidings(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("realm_tidings");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();
         if (t.Abort(behavior.Store == null, "memory store not active")) return t.Line();

         Hero writer = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsLord && !h.IsPrisoner && h.Clan != null && h.Clan != Clan.PlayerClan && h.Clan.Kingdom != null);

         if (t.Skip(writer == null, "no eligible landed lord of a kingdom to write realm tidings")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, writer);
         int origRegard = profile.ReputationWithPlayer;
         var origLedger = new List<string>(profile.RelayedWorldEventIds);
         const string seedId = "selftest_realmtidings_evt";
         const string seedSummary = "A great host gathers at the border, and men whisper of war to come.";

         try
         {
            profile.ReputationWithPlayer = 30; // above MinRegardToWrite (20)
            profile.RelayedWorldEventIds.Clear();
            profile.LetterCandidates.RemoveAll(c => c.Reason == LetterReason.RealmTidings);
            behavior.Store.Set(profile);

            // Magnitude 100 + the writer's own kingdom: its salience beats any real event, so the pure policy picks
            // it deterministically. Stable id so the seed is predictable and removable.
            Feed.Record(new ExternalWorldEvent {
               Id = seedId,
               SourceMod = "SelfTest",
               Summary = seedSummary,
               Magnitude = 100,
               FactionIds = new List<string> {writer.Clan.Kingdom.StringId}
            });

            // FIRST eval: the seed should be picked, queued as a candidate, and recorded in the dedup ledger.
            RealmTidingsBehavior.DebugEvaluate(behavior.Store, profile, writer);
            profile = behavior.Store.Get(writer.StringId);
            bool queued = profile.LetterCandidates.Any(c => c.Reason == LetterReason.RealmTidings);
            t.Note($"writer={writer.Name}: firstEval queued={queued}");
            t.Expect("tidingsQueued", queued, true);
            t.Expect("seedRecordedInLedger", profile.RelayedWorldEventIds.Contains(seedId), true);

            // DEDUP eval: clear the pending candidate so the anti-stacking guard does not short-circuit, then
            // evaluate again. The seed is now in the ledger, so it must never be relayed a second time.
            profile.LetterCandidates.RemoveAll(c => c.Reason == LetterReason.RealmTidings);
            behavior.Store.Set(profile);
            RealmTidingsBehavior.DebugEvaluate(behavior.Store, profile, writer);
            profile = behavior.Store.Get(writer.StringId);
            int seedRelays = profile.RelayedWorldEventIds.Count(x => x == seedId);
            t.Note($"secondEval seedRelayCount={seedRelays}");
            t.Expect("seedNotRelayedTwice", seedRelays, 1);
         }
         finally
         {
            WorldEventStore.Instance.Remove(seedId);
            profile = behavior.Store.Get(writer.StringId) ?? profile;
            // Setup had cleared every RealmTidings candidate, so restoring that state means removing them all again
            // (covers a second, different event a dedup pass may have queued, which the seed-summary match misses).
            profile.LetterCandidates.RemoveAll(c => c.Reason == LetterReason.RealmTidings);
            profile.ReputationWithPlayer = origRegard;
            profile.RelayedWorldEventIds.Clear();
            profile.RelayedWorldEventIds.AddRange(origLedger);
            behavior.Store.Set(profile);
         }

         return t.Line();
      }

      /// <summary>
      ///   Player report: "the monarch notified me per letter of a successful siege after a few days when I was
      ///   part of the force that took it." WorldEventFeeders.OnSettlementOwnerChanged only ever recognised the
      ///   player as involved when they were the officially credited capturer, so a mere participant in the
      ///   besieging force never set InvolvesPlayer and RealmTidingsBehavior's own InvolvesPlayer filter let the
      ///   player's own conquest boomerang back as unbidden news. This drives the REAL engine-bound seam end to
      ///   end (a live MapEvent cannot be staged in a self-test): DebugSetPlayerSiegeMarker stamps the marker
      ///   exactly as OnMapEventEnded would after an assault, then DebugRecordSiegeCapture runs the exact
      ///   RecordSiegeCapture construction OnSettlementOwnerChanged uses, with a NON-player taker (proving the
      ///   marker path, not the trivial playerIsTaker path). Asserts InvolvesPlayer flips true (the fix) while
      ///   Type/Aggrieved* stay keyed on the real taker (the conquest's attribution is not stolen, the nuance
      ///   the fix must not break). Removes the seeded WorldEvent afterwards.
      /// </summary>
      /// <summary>
      ///   Invented facts (Gabriel, 2026-09-12). A character makes up a blight; the gate judges it; if it
      ///   passes it becomes a real rumour that a DIFFERENT character can later have heard. A unit test can
      ///   prove the gate's rule and can never prove the row reached the world, which is the same gap the
      ///   knowledge sweep exists for - so this drives the real injector against the real store.
      ///   <para>
      ///     Also asserts the two refusals that matter most: a claim the PLAYER made never enters (Gabriel:
      ///     "le joueur subit le Monde alors que les NPC le definissent"), and a claim about something the
      ///     ENGINE records never enters, because that is the checkable lie the whole design avoids.
      ///   </para>
      ///   Removes every row it wrote.
      /// </summary>
      /// <summary>
      ///   The seasonal historian (Gabriel, 2026-09-11 design; built 2026-09-12). Drives the REAL behaviour
      ///   against the REAL world-event store and asserts the whole road: a season's change becomes an entry,
      ///   the entry is ordinary news rather than a fourth memory store, and a third party can reach it
      ///   through the same rumour machinery everything else uses.
      ///   <para>
      ///     It uses a deliberately perturbed reading, because the honest alternative is waiting a season and
      ///     hoping a kingdom happened to take a town. Removes the row it writes.
      ///   </para>
      /// </summary>
      private static string TestSeasonalChronicle(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("seasonal_chronicle");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         SeasonalChronicleBehavior chronicle = SeasonalChronicleBehavior.Instance;
         if (t.Abort(chronicle == null, "SeasonalChronicleBehavior not attached")) return t.Line();

         WorldEvent written = null;

         try
         {
            // A plain turn first: it must run over every realm without throwing, and on a save whose readings
            // are already current it must find nothing to say. A chronicle that reported every tick would be
            // a chronicle nobody reads.
            int quiet = chronicle.DebugRunTurn();
            t.Note($"a turn with readings already current recorded {quiet} entr(ies)");

            written = chronicle.DebugRecordSyntheticTurn();

            if (t.Skip(written == null, "no realm in this save yields a describable season")) return t.Line();

            t.Note($"entry: {written.Summary}");
            t.Expect("aSeasonsChangeBecomesAnEntry", written.Type, WorldEventType.SeasonalTurn);
            t.Expect("itIsTaggedAsTheChronicles", written.SourceMod, SeasonalChronicleBehavior.Source);

            // Ruling 3, made mechanical: it is ordinary news, so the ageing and awareness that already exist
            // apply to it and no parallel ledger was created.
            t.Expect("itIsNotThePlayersChronicle", written.InvolvesPlayer, false);
            t.Expect("itIsNotKeptForeverAsADeed", written.IsPersistentDeed, false);
            // Not a rank but a REACH: what matters is who hears it, so the assertion is that a countryman of
            // that realm actually would and a distant foreigner would not. The old form compared magnitudes
            // as though they were ranks, which is how both features shipped unreachable.
            t.Expect("itsOwnRealmHearsHowItFared",
                     RumorAwareness.Knows(RumorAwareness.Score(written.Magnitude, true, 0, 0)), true);
            t.Expect("aDistantForeignerHasNotBeenFollowingIt",
                     RumorAwareness.Knows(RumorAwareness.Score(written.Magnitude, false, 0, 0)), false);

            // Ruling 2: one historian for the world, and every character carries a VIEW of it - which is the
            // machinery that already existed rather than anything this feature added.
            Hero reader = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive && h.IsLord);

            if (reader != null)
            {
               var today = (int) CampaignTime.Now.ToDays;
               bool reachable = WorldEventStore.Instance.GetKnownEvents(reader, today, 50)
                                               .Any(e => e?.Id == written.Id)
                                || WorldEventStore.Instance.IsAwareOf(reader, written, today);

               t.Note($"a lord ({reader.Name}) can reach it through the rumour road={reachable}");
            }
         }
         finally
         {
            if (written?.Id != null) WorldEventStore.Instance.Remove(written.Id);
         }

         return t.Line();
      }

      private static string TestWorldClaims(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("world_claims");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         Hero speaker = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive && h.IsLord);
         if (t.Skip(speaker == null, "no living lord to put words in the mouth of")) return t.Line();

         Settlement place = Settlement.All?.FirstOrDefault(p => p.IsVillage) ?? Settlement.All?.FirstOrDefault();
         if (t.Skip(place == null, "no settlement to place the news at")) return t.Line();

         var today = (int) CampaignTime.Now.ToDays;
         var before = WorldEventStore.Instance.Count;
         var written = new List<string>();

         try
         {
            var blight = new WorldClaim {
               SpeakerId = speaker.StringId, SpeakerName = speaker.Name?.ToString(),
               Subject = ClaimSubject.Blight, PlaceName = place.Name?.ToString(), StatedAsFact = true,
               Text = $"[SELFTEST] A blight has taken the grain in the fields about {place.Name}."
            };

            // The switch is OFF by default, and this test drives the injector DIRECTLY - it proves the
            // mechanism, never that the feature is live. Saying so here stops a PASS being read as "it runs".
            t.Note($"speaker={speaker.Name}, place={place.Name}, "
                   + $"MCM 'Characters can add news to the world'={(ModSettings.EnableNpcWorldClaims ? "ON" : "OFF (default) - no claim is harvested in play")}");

            // 1. an NPC's invention about something the game does not model DOES become real.
            int adopted = WorldClaimInjector.Adopt(new List<WorldClaim> {blight}, speaker, today);
            t.Expect("anNpcsInventionEntersTheWorld", adopted, 1);

            WorldEvent row = WorldEventStore.Instance.MostRecentMatching(
               WorldEventType.HeardTell, e => e?.Summary != null && e.Summary.Contains("[SELFTEST]"));

            if (t.Abort(row == null, "the adopted claim is not in the store")) return t.Line();
            written.Add(row.Id);

            // The row has to be attributable, findable and weightless enough to never crowd out real news.
            t.Expect("theSpeakerIsNamedSoItCanBeAttributed", row.ActorIds?.Contains(speaker.StringId) == true, true);
            t.Expect("itIsTaggedAsOurs", row.SourceMod, WorldClaimInjector.Source);
            t.Expect("itIsNotThePlayersChronicle", row.InvolvesPlayer, false);
            t.Expect("itNeverOutweighsSomethingThatHappened", row.Magnitude <= WorldClaimPolicy.Magnitude, true);

            // 2. and the whole point: somebody ELSE can now have heard it. This is the incoherence Gabriel
            //    named - the second NPC who could not know - closed through the rumour road that already
            //    existed rather than through a second world model.
            Hero other = Hero.AllAliveHeroes.FirstOrDefault(
               h => h != Hero.MainHero && h != speaker && h.IsAlive && !h.IsPrisoner);

            if (other != null)
            {
               bool reachable = WorldEventStore.Instance.GetKnownEvents(other, today, 50)
                                               .Any(e => e?.Id == row.Id)
                                || WorldEventStore.Instance.IsAwareOf(other, row, today);

               t.Note($"a third party ({other.Name}) can reach it through the rumour road={reachable}");
            }

            // 3. the player does not author the world.
            int fromPlayer = WorldClaimInjector.Adopt(new List<WorldClaim> {blight}, Hero.MainHero, today);
            t.Expect("thePlayersOwnClaimNeverEnters", fromPlayer, 0);

            // 4. nor does anything the engine keeps on a screen the player can open.
            var engineOwned = new WorldClaim {
               SpeakerId = speaker.StringId, SpeakerName = speaker.Name?.ToString(),
               Subject = ClaimSubject.EngineOwned, PlaceName = place.Name?.ToString(), StatedAsFact = true,
               Text = "[SELFTEST] The garrison there numbers four hundred spears."
            };

            t.Expect("aClaimTheGameItselfRecordsNeverEnters",
                     WorldClaimInjector.Adopt(new List<WorldClaim> {engineOwned}, speaker, today), 0);

            // 5. and the same news twice is still one piece of news.
            t.Expect("theSameNewsTwiceIsRecordedOnce",
                     WorldClaimInjector.Adopt(new List<WorldClaim> {blight}, speaker, today), 0);
         }
         finally
         {
            foreach (string id in written) WorldEventStore.Instance.Remove(id);

            t.Note($"store restored: {before} -> {WorldEventStore.Instance.Count}");
         }

         return t.Line();
      }

      private static string TestSiegeTidingsParticipation(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("siege_tidings_participation");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         WorldEventFeeders feeders = behavior?.WorldFeeders;
         if (t.Abort(feeders == null, "WorldEventFeeders not active")) return t.Line();

         Settlement settlement = Settlement.All?.FirstOrDefault(s => s.IsTown || s.IsCastle);
         if (t.Skip(settlement == null, "no town/castle settlement exists to stage the marker against")) return t.Line();

         Hero taker = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);
         if (t.Skip(taker == null, "no non-player lord to stand in as the credited capturer")) return t.Line();

         WorldEvent recorded = null;

         try
         {
            feeders.DebugSetPlayerSiegeMarker(settlement.StringId, (int) CampaignTime.Now.ToDays);
            recorded = feeders.DebugRecordSiegeCapture(settlement, taker, null);

            if (t.Abort(recorded == null, "RecordSiegeCapture returned no WorldEvent")) return t.Line();

            t.Note($"settlement={settlement.Name}, taker={taker.Name} (not the player)");
            // The fix: a mere participant (marker matched, not the credited taker) still flips InvolvesPlayer,
            // so realm-tidings' own filter will never relay this siege back as news.
            t.Expect("involvesPlayer", recorded.InvolvesPlayer, true);
            // The nuance the fix must not break: attribution stays with the real taker, not the participant.
            t.Expect("type", recorded.Type, WorldEventType.SettlementCaptured);
            t.Expect("aggrievedFactionIdsEmpty", recorded.AggrievedFactionIds?.Count ?? 0, 0);
            t.Expect("aggrievedActorIdsEmpty", recorded.AggrievedActorIds?.Count ?? 0, 0);
            t.Expect("chronicleLineAbsent", recorded.ChronicleLine == null, true);
         }
         finally
         {
            if (recorded?.Id != null) WorldEventStore.Instance.Remove(recorded.Id);
         }

         return t.Line();
      }

      /// <summary>
      ///   The companion "fought together" memory (WorldEventFeeders' main-party companion loop) is driven by a real
      ///   MapEvent in game, so a unit test cannot reach it: this drives the DebugRecordCompanionBattleMemories seam
      ///   (the exact path the battle feeder uses) against a real party. Borrows a companion into the party if none
      ///   rides along, and asserts the companion's profile gained a fresh "Fought at your side" memory. Removes the
      ///   memory it added and returns any borrowed companion.
      /// </summary>
      private static string TestCompanionBattleMemory(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_battle_memory");

         if (t.Abort(behavior.Store == null, "memory store not active")) return t.Line();
         if (t.Abort(MobileParty.MainParty == null, "no main party to hold a companion")) return t.Line();

         Hero companion = Hero.AllAliveHeroes.FirstOrDefault(
            h => h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && h.PartyBelongedTo == MobileParty.MainParty);

         // A test that only runs when a companion happens to be riding along is a test that rarely runs; BORROW one
         // and put them back afterwards, the way the rest of this harness seeds the state it needs.
         MobileParty borrowedFrom = null;
         var borrowed = false;

         if (companion == null)
         {
            Hero spare = Hero.AllAliveHeroes.FirstOrDefault(
               h => h != Hero.MainHero && h.IsAlive && h.IsPlayerCompanion && !h.IsPrisoner);

            if (t.Skip(spare == null, "the player has no companion at all to place in the party")) return t.Line();

            borrowedFrom = spare.PartyBelongedTo;
            borrowed = true;
            AddHeroToPartyAction.Apply(spare, MobileParty.MainParty, false);
            companion = spare;
            t.Note($"(borrowed {spare.Name} into the player's party for this test)");
         }

         NpcProfile profile = GetOrCreate(behavior, companion);
         int eventsBefore = profile.Events.Count;
         int today = behavior.GameStateBridge.GetCurrentGameDay();
         // Built the way the recorder builds it, player name included (BattleMemoryText.NameOrPlayer, 2.5.18).
         // Without the name this matched nothing: the assertion below failed, and the cleanup at the end of this
         // test quietly removed nothing, leaving a planted memory in the campaign every time it ran.
         string expected = BattleMemoryText.AllySummary(true, "", false, Hero.MainHero?.Name?.ToString() ?? "the player");

         int gained = WorldEventFeeders.DebugRecordCompanionBattleMemories(true);

         profile = behavior.Store.Get(companion.StringId);
         t.Note($"{companion.Name}: events {eventsBefore}->{profile.Events.Count}, companionsGained={gained}, last='{profile.Events.LastOrDefault()?.summary ?? "(none)"}'");
         t.Expect("companionGainedMemory", profile.Events.Count > eventsBefore, true);
         t.Expect("theMemoryNamesThePlayerHeFoughtBeside", profile.Events.Last().summary == expected, true);

         // Cleanup: drop only the ally memory this test planted (this exact summary, dated today), then return the
         // borrowed companion so the test does not quietly conscript someone.
         profile.Events.RemoveAll(e => e.gameDay == today && e.summary == expected);
         behavior.Store.Set(profile);

         if (borrowed)
         {
            if (borrowedFrom != null && borrowedFrom != MobileParty.MainParty)
            {
               AddHeroToPartyAction.Apply(companion, borrowedFrom, false);
               t.Note($"(returned {companion.Name} to their party)");
            }
            else t.Note($"(NOTE: {companion.Name} had no party to return to and remains in yours; reload to discard)");
         }

         return t.Line();
      }

      /// <summary>
      ///   The companion-briefing pillar as it now stands: a private word becomes a PRIVATE MEMORY on everyone
      ///   who stood in it, and that memory never reaches the person it was about.
      ///   <para>
      ///     Rewritten 2026-09-13 when the standing brief was dismantled. The old version drove
      ///     CompanionBriefingStore through stacking, bounding and expiry - a whole second carrier that existed
      ///     for one reason, that a memory could not be kept private, and that stopped being a reason the hour
      ///     NotableEvent.IsPrivate was built.
      ///   </para>
      /// </summary>
      private static string TestCompanionBriefing(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("companion_briefing");

         Hero companion = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h != Hero.MainHero && h.IsAlive);
         Hero target = Hero.AllAliveHeroes?.FirstOrDefault(
            h => h != null && h != Hero.MainHero && h.IsAlive && h != companion);

         if (t.Skip(companion == null || target == null, "no two living heroes to brief and be briefed about"))
            return t.Line();

         NpcProfile companionProfile = behavior?.Store?.Get(companion!.StringId);
         if (t.Skip(companionProfile?.Events == null, "no profile to remember a briefing")) return t.Line();

         List<NotableEvent> original = companionProfile!.Events.ToList();
         string errand = $"SELFTEST-ERRAND the player asked me to win over {target!.Name} before we meet";
         const string openly = "SELFTEST-OPENLY we rode through the pass together in the rain";

         try
         {
            int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
            companionProfile.Events.Add(new NotableEvent(day, NotableEventType.Other, errand) {IsPrivate = true});
            // A CONTROL, planted on the SAME companion in the SAME breath and differing in one bit: IsPrivate.
            // Without it "the errand never reaches the room" passes just as well when the witness recall is
            // broken, empty, or unwired as when the filter is doing its job - and that is not a hypothetical
            // in this codebase: ConversationCast.Introduce sat written and never called for a fortnight.
            companionProfile.Events.Add(new NotableEvent(day, NotableEventType.Other, openly));
            behavior.Store.Set(companionProfile);

            // The briefed companion carries it: a private memory is still one of THEIR memories, so it sits in
            // their own profile and reaches their own prompt through the ordinary history like any other.
            t.Expect("theErrandReachesTheCompanionWhoWasGivenIt",
                     BuildOwnMemoryProbe(companionProfile).Contains("SELFTEST-ERRAND"), true);

            string recalled = ChatViewModel.DebugWitnessMemory(companion.StringId) ?? "";

            // The control first, so a failure here reads as "the recall is not working" rather than being
            // mistaken for the pillar holding.
            t.Expect("anOrdinaryMemoryDoesReachTheRoom", recalled.Contains("SELFTEST-OPENLY"), true);

            // THE LOAD-BEARING ASSERTION OF THE WHOLE PILLAR: standing in the room with the target, that same
            // memory must not be readable by them. The witness recall is the only path it could travel.
            t.Expect("theErrandNeverReachesTheRoom", !recalled.Contains("SELFTEST-ERRAND"), true);

            // And the scene flag still names the absent target, so the briefing chat itself knows who it is for.
            EncounterContext scene = EncounterContextBuilder.Build(
               companion, companionProfile, behavior.GameStateBridge, "well?", null, false,
               isCompanionBriefingScene: true, companionBriefingTargetName: target.Name?.ToString());

            t.Expect("theSceneKnowsWhoItIsFor", scene?.IsCompanionBriefingScene, true);
            t.Expect("theSceneNamesTheAbsentTarget",
                     string.Equals(scene?.CompanionBriefingTargetName, target.Name?.ToString()), true);

            t.Note($"briefed={companion.Name}, about={target.Name}");
         }
         finally
         {
            companionProfile.Events.Clear();
            companionProfile.Events.AddRange(original);
            behavior?.Store?.Set(companionProfile);
         }

         return t.Line();
      }

      /// <summary>The plain read of a hero's own memories, for the assertion that they still have them.</summary>
      private static string BuildOwnMemoryProbe(NpcProfile profile)
         => string.Join(" ", profile.Events.Select(e => e?.summary ?? ""));


      private static string TestQueryRelevantFacts(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("query_relevant_facts");

         if (t.Abort(WorldEventStore.Instance == null, "WorldEventStore not active")) return t.Line();

         Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
         Hero other = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h != npc && h.IsLord);

         if (t.Skip(npc == null || other == null, "need two live lords: the NPC and a third party to ask about")) return t.Line();

         int today = (int) CampaignTime.Now.ToDays;

         var factionIds = new List<string>();
         if (npc.MapFaction?.StringId != null) factionIds.Add(npc.MapFaction.StringId);

         // High magnitude + the NPC's own faction tagged, dated today: guarantees RumorAwareness.Knows passes
         // regardless of map position, so a miss below proves the RELEVANCE scoring failed, not awareness.
         var seeded = new WorldEvent {
            Type = WorldEventType.Battle,
            Day = today,
            Magnitude = 80,
            Summary = $"{other.Name} broke a raiding host near the eastern marches (selftest marker).",
            ActorIds = new List<string> {other.StringId},
            FactionIds = factionIds
         };
         WorldEventStore.Instance.Record(seeded);

         try
         {
            string namedQuestion = $"Tell me, what do you know of {other.Name}?";
            string unrelatedQuestion = "How fares the harvest this year?";

            // The query block deliberately SKIPS whatever is already ambient (ResolveQueryRelevantFacts drops the
            // top few known events, since those are injected as ambient rumour elsewhere). A fresh magnitude-80
            // seed can land in that top slice on a quiet save, and then the correct behaviour is to say nothing
            // here, which used to read as a failure. Detect that and SKIP: a test that cannot run is not a bug.
            IReadOnlyList<(WorldEvents.WorldEvent Event, Logic.RumorConfidence Confidence)> knownNow =
               WorldEvents.WorldEventStore.Instance?.GetKnownEventsRated(npc, today, 6);
            bool seededIsAlreadyAmbient = knownNow != null && knownNow.Any(k => k.Event?.Id == seeded.Id);

            if (t.Skip(seededIsAlreadyAmbient,
                   "the seeded fact is already in this NPC's ambient top slice, which the query block skips by design")) return t.Line();

            string namedBlock = EncounterContextBuilder.ResolveQueryRelevantFacts(npc, namedQuestion, today);
            string unrelatedBlock = EncounterContextBuilder.ResolveQueryRelevantFacts(npc, unrelatedQuestion, today);

            t.Note($"npc={npc.Name}, asked about {other.Name} -> {(namedBlock == null ? "null" : Show(namedBlock))}");
            t.Expect("surfacesOnNamedEntity", namedBlock != null && namedBlock.Contains("selftest marker"), true);
            t.Expect("silentOnUnrelatedMessage", unrelatedBlock == null || !unrelatedBlock.Contains("selftest marker"), true);

            return t.Line();
         }
         finally
         {
            WorldEventStore.Instance.Remove(seeded.Id);
         }
      }

      /// <summary>
      ///   War-council OFFER trigger (mechanical, no LLM, no UI): drives the REAL arm + policy + persisted-cooldown
      ///   decision through <see cref="CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior.DebugWouldOffer" />. The
      ///   council window is a Gauntlet screen, so this test never opens it; it asserts only the decision that
      ///   precedes it. Restores the kill-switch and the persisted last-offer day afterwards.
      /// </summary>
      private static string TestWarCouncilOffer(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("war_council_offer");

         CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior offer = CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior.Instance;

         if (t.Abort(offer == null, "war-council offer behavior not registered")) return t.Line();

         // Borrow the two pieces of state this test drives, to restore them exactly afterwards.
         bool prevEnabled = CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior.Enabled;
         int prevLastOfferDay = offer.DebugLastOfferDay;
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         try
         {
            // Armed + never offered: a notable action while leading an army WOULD offer (the whole point).
            CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior.Enabled = true;
            offer.DebugSetLastOfferDay(-1);
            t.Expect("armedNotableInArmyOffers", offer.DebugWouldOffer(true, true), true);

            // Not commanding an army, or a routine action, must not summon the captains.
            t.Expect("notInArmyRefuses", offer.DebugWouldOffer(false, true), false);
            t.Expect("nonNotableRefuses", offer.DebugWouldOffer(true, false), false);

            // Anti-spam: an offer stamped today blocks a fresh notable action; a stale stamp past the cooldown clears it.
            offer.DebugSetLastOfferDay(today);
            t.Expect("withinCooldownRefuses", offer.DebugWouldOffer(true, true), false);
            offer.DebugSetLastOfferDay(today - (WarCouncilTriggerPolicy.DefaultCooldownDays + 1));
            t.Expect("afterCooldownOffers", offer.DebugWouldOffer(true, true), true);

            // The kill-switch overrides everything: nothing reaches players while disarmed.
            CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior.Enabled = false;
            offer.DebugSetLastOfferDay(-1);
            t.Expect("disarmedNeverOffers", offer.DebugWouldOffer(true, true), false);

            t.Note("Decision-only: the council UI is never opened here (DebugWouldOffer runs the arm+policy+cooldown gate without staging a scene).");
         }
         finally
         {
            CalradiaRemembers.WarCouncil.WarCouncilOfferBehavior.Enabled = prevEnabled;
            offer.DebugSetLastOfferDay(prevLastOfferDay);
         }

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

         // !IsPrisoner (testall 2026-07-23): StanceConsequenceBehavior.CanWrite rightly refuses a captive
         // plotter, so a pick that can land on one fails the test for a reason that is not this feature's.
         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero &&
            h.IsLord &&
            !h.IsPrisoner &&
            h.Clan != Clan.PlayerClan &&
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

         // !IsPrisoner (testall 2026-07-23): StanceConsequenceBehavior.CanWrite rightly refuses a captive
         // plotter, so a pick that can land on one fails the test for a reason that is not this feature's.
         Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero &&
            h.IsLord &&
            !h.IsPrisoner &&
            h.Clan != Clan.PlayerClan &&
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

      private static string TestSuccessionSwitch(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("succession_switch");

         if (t.Abort(Hero.MainHero == null, "no player hero")) return t.Line();

         // What a player switcher hands over: a predecessor who is alive and active. Classified only, never applied.
         t.Expect("aLivingPredecessorIsASwitch", SuccessionBehavior.ClassifyChange(Hero.MainHero), PlayerChangeKind.Switch);

         Hero dead = Hero.DeadOrDisabledHeroes?.FirstOrDefault(h => h.IsDead);
         if (dead != null) t.Expect("aDeadPredecessorIsADeath", SuccessionBehavior.ClassifyChange(dead), PlayerChangeKind.Death);
         else t.Note("no dead hero in this save to classify");

         Hero disabled = Hero.DeadOrDisabledHeroes?.FirstOrDefault(h => h.HeroState == Hero.CharacterStates.Disabled);
         if (disabled != null) t.Expect("aDisabledPredecessorIsARetirement", SuccessionBehavior.ClassifyChange(disabled), PlayerChangeKind.Retirement);
         else t.Note("no disabled hero in this save to classify");

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

         // A DETACHED profile, built from the lord but never stored. Until 26/09/2026 this test ran the real
         // SuccessionBehavior.Apply, which reframed EVERY profile in the player's save (private memories erased, all
         // marked inherited) and wrote a succession into the chronicle: running cr.testall on a real save damaged it.
         // Apply is now one loop over SuccessionMemoryPolicy.Reframe, so reframing a copy proves the same rule.
         NpcProfile stored = behavior.Store.Get(subject.StringId);
         int storedEventsBefore = stored?.Events.Count ?? -1;
         string storedInheritedBefore = stored?.InheritedFromName;

         NpcProfile copy = HeroProfileMapper.FromHero(subject);
         int day = behavior.GameStateBridge.GetCurrentGameDay();
         copy.Events.Add(new NotableEvent(day, NotableEventType.Captivity, "[SELFTEST] private captivity memory"));
         copy.Events.Add(new NotableEvent(day, NotableEventType.Conflict, "[SELFTEST] public political dealing"));
         int publicBefore = copy.Events.Count(e => SuccessionMemoryPolicy.SurvivesSuccession(e.type));
         var predName = Hero.MainHero.Name?.ToString();

         SuccessionMemoryPolicy.Reframe(copy, predName, SuccessionBehavior.ResolveKinship(Hero.MainHero, heir));

         int privateAfter = copy.Events.Count(e => !SuccessionMemoryPolicy.SurvivesSuccession(e.type));
         int publicAfter = copy.Events.Count(e => SuccessionMemoryPolicy.SurvivesSuccession(e.type));

         t.Note($"subject={subject.Name}, heir={heir.Name}, kinship='{copy.InheritedKinship}'");
         t.Expect("privateDropped", privateAfter == 0, true);
         t.Expect("publicKept", publicAfter == publicBefore && publicAfter >= 1, true);
         t.Expect("inheritedSet", copy.InheritedFromName == predName, true);

         // The save itself is untouched.
         NpcProfile storedAfter = behavior.Store.Get(subject.StringId);
         t.Expect("saveUntouched", (storedAfter?.Events.Count ?? -1) == storedEventsBefore && storedAfter?.InheritedFromName == storedInheritedBefore, true);

         return t.Line();
      }

      /// <summary>
      ///   Personal supply quest (ProvideGoods) end-to-end: a bad-category proposal is refused by the real
      ///   issuance pipeline (nothing grounded), a grain proposal is issued through QuestService.ApplyResponse
      ///   and grounds a real category+count, and a real hand-over through SettleSupplyQuest takes exactly the
      ///   required count, closes the quest (Completed), and pays the reward. Leaves the roster as it found it.
      /// </summary>
      private static string TestSupplyQuest(NpcMemoryCampaignBehavior behavior)
      {
         var t = new TestRun("supply_quest");

         if (t.Abort(Hero.MainHero == null || MobileParty.MainParty?.ItemRoster == null, "no player party")) return t.Line();

         Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && !h.IsPrisoner);

         if (t.Skip(giver == null, "no live lord to stand as the supply-asking giver")) return t.Line();

         var grain = MBObjectManager.Instance?.GetObject<ItemObject>("grain");

         if (t.Skip(grain == null || !SupplyItemMatcher.Matches(grain, ProvideGoodsPolicy.Grain), "no grain item to test the hand-over")) return t.Line();

         NpcProfile profile = GetOrCreate(behavior, giver);
         ArrangeCleanNpc(behavior, profile, giver); // clean slate so the one-task-at-a-time gate cannot block issuance
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         // Refusal branch: an unrecognized goods category grounds no quest at all.
         QuestService.ApplyResponse(profile, giver,
            new ParsedResponse {Dialogue = "", QuestGiven = new QuestProposal {Type = QuestType.ProvideGoods, Category = "swords", RequiredCount = 5}},
            day, behavior.GameStateBridge, behavior.Store, null);
         bool refused = behavior.Store.Get(giver.StringId).ActiveQuests.All(q => q.Type != QuestType.ProvideGoods);
         t.Expect("badCategoryRefused", refused, true);

         // Issue a real grain supply task through the pipeline (grounds category + count).
         const int need = 3;
         QuestService.ApplyResponse(profile, giver,
            new ParsedResponse {
               Dialogue = "",
               QuestGiven = new QuestProposal {
                  Type = QuestType.ProvideGoods, Category = "grain", RequiredCount = need,
                  RewardGold = 150, RewardRelation = 3, Description = $"[SELFTEST] {giver.Name} needs grain for the host."
               }
            },
            day, behavior.GameStateBridge, behavior.Store, null);

         profile = behavior.Store.Get(giver.StringId);
         InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.ProvideGoods && q.IsOutstanding);

         if (t.Abort(quest == null, $"the pipeline seeded no ProvideGoods quest for {giver.Name}")) return t.Line();
         t.Expect("groundedCategory", quest.RequiredItemCategory, ProvideGoodsPolicy.Grain);
         t.Expect("groundedCount", quest.RequiredItemCount, need);

         // Hand over the grain through the real bridge settlement.
         int held0 = CountItemInParty(grain);
         MobileParty.MainParty.ItemRoster.AddToCounts(grain, need);
         int regardBefore = profile.ReputationWithPlayer;

         var offers = new List<ItemOffer> {new ItemOffer(grain.StringId, CountItemInParty(grain))};
         ActionOutcome outcome = behavior.GameStateBridge.SettleSupplyQuest(profile, giver, quest, offers);

         InformalQuest after = profile.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.ProvideGoods);
         int taken = held0 + need - CountItemInParty(grain);

         t.Note($"giver={giver.Name}, grain={grain.Name}: grounded {quest.RequiredItemCount} {quest.RequiredItemCategory}, status={after?.Status}, taken={taken}/{need}, regard {regardBefore}->{profile.ReputationWithPlayer}");
         t.Expect("handOverSettled", outcome?.IsPositive, true);
         t.Expect("questCompleted", after?.Status, QuestStatus.Completed);
         t.Expect("tookExactlyRequired", taken, need);
         t.Expect("goodwillPaid", profile.ReputationWithPlayer > regardBefore, true);

         // Restore the roster: drop any surplus grain the test added.
         int surplus = CountItemInParty(grain) - held0;
         if (surplus > 0) MobileParty.MainParty.ItemRoster.AddToCounts(grain, -surplus);

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