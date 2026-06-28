// Code written by Gabriel Mailhot, 25/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Bastards;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Scenarios;
using CalradiaRemembers.Stance;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

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
        /// <summary>How costly a test's mutations are to undo — drives the run warning and `cr.test list`.</summary>
        private enum Severity
        {
            Light,        // relations / profiles only
            Heavy,        // clans / prisoners / pregnancy
            Irreversible  // declares wars / seals one-shot scenarios
        }

        /// <summary>The registry. To add a test: write a method, then add one line here.</summary>
        private static readonly List<SelfTest> Tests = new List<SelfTest>
        {
            new SelfTest("providegold",      Severity.Light,        "seed ProvideGold -> take_gold -> assert stamped",                  TestProvideGold),
            new SelfTest("scheme_warn",      Severity.Light,        "ForcePlot -> TargetHeeds -> assert plotter/target relation deltas", TestSchemeWarn),
            new SelfTest("succession",       Severity.Light,        "seed private+public events -> Apply -> assert private dropped",     TestSuccession),
            new SelfTest("recruit",          Severity.Heavy,        "eligible lord -> join_clan -> assert he moved into your clan",      TestRecruit),
            new SelfTest("deliver_prisoner", Severity.Heavy,        "capture -> give_prisoner -> assert transfer + deed stamp",         TestDeliverPrisoner),
            new SelfTest("free_prisoner",    Severity.Heavy,        "capture -> free_prisoner -> assert released",                      TestFreePrisoner),
            new SelfTest("give_item",        Severity.Light,        "pick a player item -> give_item -> assert it left the inventory",   TestGiveItem),
            new SelfTest("stance_deed",      Severity.Light,        "a gift deed -> assert Trust rose and persisted",                   TestStanceDeed),
            new SelfTest("stance_letter",    Severity.Heavy,        "hostile stance -> weekly eval -> assert a cold StanceHostility letter is queued", TestStanceLetter),
            new SelfTest("scheme_assist",    Severity.Heavy,        "plot + regard -> PlayerAssists -> assert the plot advanced",       TestSchemeAssist),
            new SelfTest("pregnancy",        Severity.Heavy,        "fertile female -> TryConceive(force) -> assert pregnant",          TestPregnancy),
            new SelfTest("companion_mission",Severity.Heavy,        "dispatch a companion -> assert away -> resolve -> assert home",    TestCompanionMission),
            new SelfTest("bastard_discovery",Severity.Heavy,        "tag a bastard -> Discover -> assert flag + a Scandal world-event",  TestBastardDiscovery),
            new SelfTest("bastard_claim",    Severity.Heavy,        "force a grown bastard's claim -> assert hostility + claim world-event", TestBastardClaim),
            new SelfTest("bastard_scandal_culture", Severity.Heavy, "discover a bastard -> assert the scandal magnitude is culture-scaled", TestBastardScandalCulture),
            new SelfTest("feed_ingest",      Severity.Light,        "Feed.Record an external event -> assert stored + a re-send is deduped", TestFeedIngest),
            new SelfTest("feed_update",      Severity.Light,        "Feed.Update upserts a fed fact by id, Feed.Retract removes it (mutable entries)", TestFeedUpdate),
            new SelfTest("mission_register", Severity.Heavy,        "MissionBoard.Register an external mission -> assert issued + dedup + Complete stamps it", TestMissionRegister),
            new SelfTest("mission_reward",   Severity.Heavy,        "MissionBoard FULL LOOP -> register -> Complete -> player reports to giver -> assert reward PAID + quest Completed", TestMissionRewardLoop),
            new SelfTest("declare_war",      Severity.Irreversible, "seed deed -> declare war -> assert WarDeclared stamps it",         TestDeclareWar),
            new SelfTest("scenario",         Severity.Irreversible, "fire ON_DEATH:Rhagaea x2 -> assert a rule fires then seals",       TestScenario),
            new SelfTest("llm_dialogue",     Severity.Light,        "LLM: a plain greeting -> log the reply (relevance judged from the log)",          TestLlmDialogue,        callsLlm: true),
            new SelfTest("chat_memory",      Severity.Light,        "LLM: seed a shared history -> ask about it -> does the reply reference it?",       TestChatMemory,         callsLlm: true),
            new SelfTest("chat_stance",      Severity.Light,        "LLM: set a distinctive stance (fear/distrust) -> ask -> does the tone match?",    TestChatStance,         callsLlm: true),
            new SelfTest("chat_hearsay",     Severity.Light,        "LLM: ask about another lord -> does the NPC answer from their own vantage?",       TestChatHearsay,        callsLlm: true),
            new SelfTest("chat_bastard_mother", Severity.Light,     "LLM: force a mercenary bastard-mother context -> does she raise/extort the child?", TestChatBastardMother, callsLlm: true),
            new SelfTest("chat_worldevent",  Severity.Light,        "LLM: Feed a fresh event -> ask for news -> does the NPC surface it?",             TestChatWorldEvent,     callsLlm: true),
            new SelfTest("chat_stance_assassin", Severity.Light,    "LLM: force a secret-killer stance hint -> does the NPC stay smooth and hide it?", TestChatStanceAssassin, callsLlm: true),
            new SelfTest("chat_stance_favor",    Severity.Light,    "LLM: force a friendly stance hint -> does the NPC offer aid / warmth?",           TestChatStanceFavor,    callsLlm: true),
        };

        // ── Public API (cr.testall / cr.test) ─────────────────────────────────

        /// <summary>
        ///   The routine sweep (cr.testall): every test EXCEPT the irreversible ones (declaring war,
        ///   sealing a one-shot scenario). Those mutate the LIVE campaign in ways only a reload undoes —
        ///   and, running before the conversation tests, they poison every reply that follows (an actual
        ///   war declaration had all six NPCs harping on "your house declared war"). So they are opt-in,
        ///   one at a time, via <c>cr.test &lt;name&gt;</c> — or <c>cr.test full</c> on a throwaway save.
        /// </summary>
        public static string RunAll(NpcMemoryCampaignBehavior behavior)
            => RunGroup(Tests.Where(t => t.Severity != Severity.Irreversible), behavior);

        /// <summary>Runs EVERY test, including the irreversible ones (declares war / seals scenarios). Throwaway save only.</summary>
        public static string RunEverything(NpcMemoryCampaignBehavior behavior) => RunGroup(Tests, behavior);

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

        /// <summary>Runs ONLY the mechanical tests (no LLM call, no API cost) — the fast, free sweep.</summary>
        public static string RunMechanical(NpcMemoryCampaignBehavior behavior)
            => RunGroup(Tests.Where(t => !t.CallsLlm), behavior);

        private static string RunGroup(IEnumerable<SelfTest> group, NpcMemoryCampaignBehavior behavior)
        {
            List<SelfTest> tests = group.ToList();
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
                sb.AppendLine($"[SELFTEST] *** includes {llm} conversation test(s) — {llm} real LLM call(s) + a brief freeze each; relevance is judged from the log ***");
                // Stamp the model so logs can be compared across LLMs ("which one wrote better replies?").
                sb.AppendLine($"[SELFTEST] *** LLM model: {ModelLabel(behavior)} ***");
            }

            int pass = 0, fail = 0, manual = 0;
            foreach (SelfTest test in tests)
            {
                string line = SafeRun(test, behavior);
                sb.AppendLine(line);
                if      (line.Contains(": PASS")) pass++;
                else if (line.Contains(": FAIL")) fail++;
                else                              manual++;
            }

            sb.AppendLine($"[SELFTEST] SUMMARY: {pass} PASS, {fail} FAIL, {manual} MANUAL");
            string report = sb.ToString();
            ModLog.Info(report);
            return report;
        }

        /// <summary>Runs one named test; returns its result line.</summary>
        public static string Run(string testName, NpcMemoryCampaignBehavior behavior)
        {
            SelfTest test = Tests.FirstOrDefault(t => string.Equals(t.Name, testName, StringComparison.OrdinalIgnoreCase));
            if (test == null)
                return $"[SELFTEST] Unknown test '{testName}'. Available: {string.Join(", ", Tests.Select(t => t.Name))}";

            // A single conversation test is run to be read — show its full, untruncated reply.
            _fullReplies = test.CallsLlm;
            string result;
            try { result = SafeRun(test, behavior); }
            finally { _fullReplies = false; }
            ModLog.Info(result);
            return result;
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
                        Type        = QuestType.ProvideGold,
                        Description = $"[SELFTEST] {giver.Name} asks you for 500 denars.",
                        DeadlineDays   = 30,
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
                    Parameters = new Dictionary<string, string> { { "amount", "500" } }
                },
                giver, null);

            profile = behavior.Store.Get(giver.StringId);
            bool stillOpen = profile.ActiveQuests.Any(q => q.Type == QuestType.ProvideGold && q.IsOutstanding);
            t.Note($"giver={giver.Name}");
            t.Expect("stamped", !stillOpen, true);
            return t.Line();
        }

        private static string TestSchemeWarn(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("scheme_warn");
            if (t.Abort(SchemeStore.Instance == null, "SchemeStore not active")) return t.Line();

            Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan);
            Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != plotter && h != Hero.MainHero && h.IsLord
                && h.Clan != null && h.Clan != plotter?.Clan && h.Clan != Clan.PlayerClan);
            if (t.Skip(plotter == null || target == null, "need 2+ lords from distinct non-player clans")) return t.Line();

            SchemeStore.Instance.ForcePlot(plotter, target, SchemeKind.Slander);
            int plotterBefore = behavior.GameStateBridge.GetPlayerRelation(plotter);
            int targetBefore  = behavior.GameStateBridge.GetPlayerRelation(target);

            string msg = SchemeStore.Instance.TargetHeeds(target);
            if (t.Abort(msg == null, $"TargetHeeds({target.Name}) returned null — no eligible scheme after ForcePlot")) return t.Line();

            int plotterDelta = behavior.GameStateBridge.GetPlayerRelation(plotter) - plotterBefore;
            int targetDelta  = behavior.GameStateBridge.GetPlayerRelation(target)  - targetBefore;
            t.Note($"plotter={plotter.Name} delta={plotterDelta} | target={target.Name} delta={targetDelta}");
            t.Expect("plotterResents(<=-20)", plotterDelta <= -20, true);
            t.Expect("targetGrateful(>=16)",  targetDelta  >= 16,  true);
            return t.Line();
        }

        private static string TestSuccession(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("succession");
            if (t.Abort(SuccessionBehavior.Instance == null, "SuccessionBehavior not active")) return t.Line();

            // Prefer a clan member as heir; fall back to any alive lord
            Hero heir = Clan.PlayerClan?.Heroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive)
                     ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
            if (t.Skip(heir == null, "no heir candidate — join a clan or ensure a lord is alive")) return t.Line();

            Hero subject = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
            if (t.Skip(subject == null, "no lord to seed a profile on")) return t.Line();

            // Seed a deterministic profile: one PRIVATE event (dropped by succession) + one PUBLIC event (kept)
            NpcProfile profile = GetOrCreate(behavior, subject);
            int day = behavior.GameStateBridge.GetCurrentGameDay();
            profile.Events.Add(new NotableEvent(day, NotableEventType.Captivity, "[SELFTEST] private captivity memory"));
            profile.Events.Add(new NotableEvent(day, NotableEventType.Conflict,  "[SELFTEST] public political dealing"));
            behavior.Store.Set(profile);

            profile = behavior.Store.Get(subject.StringId);
            int publicBefore = profile.Events.Count(e => SuccessionMemoryPolicy.SurvivesSuccession(e.type));
            string predName = Hero.MainHero.Name?.ToString();

            // Apply — mutates ALL profiles in the store (heir inherits standing, not private history)
            int touched = SuccessionBehavior.Instance.Apply(Hero.MainHero, heir);

            NpcProfile after = behavior.Store.Get(subject.StringId);
            int privateAfter = after.Events.Count(e => !SuccessionMemoryPolicy.SurvivesSuccession(e.type));
            int publicAfter  = after.Events.Count(e =>  SuccessionMemoryPolicy.SurvivesSuccession(e.type));

            t.Note($"subject={subject.Name}, heir={heir.Name}, {touched} profiles, inherited='{after.InheritedFromName}'");
            t.Expect("privateDropped", privateAfter == 0, true);
            t.Expect("publicKept", publicAfter == publicBefore && publicAfter >= 1, true);
            t.Expect("inheritedSet", after.InheritedFromName == predName, true);
            return t.Line();
        }

        private static string TestRecruit(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("recruit");

            Clan playerClan = Clan.PlayerClan;
            if (t.Abort(playerClan == null, "no player clan")) return t.Line();

            // Pick a lord the recruitment gate would actually accept, so a real move is assertable
            Hero lord = Hero.AllAliveHeroes.FirstOrDefault(h =>
            {
                if (h == Hero.MainHero || !h.IsLord || h.Clan == playerClan) return false;
                var facts = LordRecruitmentFactsBuilder.Build(h);
                return facts != null && LordRecruitment.IsEligible(facts);
            });
            if (t.Skip(lord == null, "no eligible lord to recruit (need a landless/broken/estranged lord of tier <= yours)")) return t.Line();

            string oldClan = lord.Clan?.Name?.ToString() ?? "—";

            // The bridge re-validates the gate ("the prompt is advice, the bridge is law")
            behavior.GameStateBridge.ExecuteAction(new GameAction { Type = "join_clan" }, lord, null);

            t.Note($"{lord.Name}: {oldClan} -> {lord.Clan?.Name?.ToString() ?? "—"}");
            t.Expect("joinedYourClan", lord.Clan == playerClan, true);
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
                Type           = QuestType.DeliverPrisoner,
                Description    = $"[SELFTEST] Hand {captive.Name} to {receiver.Name}.",
                TargetHeroId   = captive.StringId,
                TargetHeroName = captive.Name?.ToString(),
                IssuedOnDay    = behavior.GameStateBridge.GetCurrentGameDay(),
                RewardGold     = 500,
                RewardRelation = 5,
                Status         = QuestStatus.Active
            };
            profile.ActiveQuests.Add(quest);
            behavior.Store.Set(profile);

            behavior.GameStateBridge.ExecuteAction(
                new GameAction { Type = "give_prisoner" }, receiver, new ActionContext(profile, null));

            bool stillHeld = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()
                                        .Any(el => el.Character?.HeroObject == captive) == true;
            InformalQuest after = behavior.Store.Get(receiver.StringId)?.ActiveQuests
                                          .FirstOrDefault(q => q.Type == QuestType.DeliverPrisoner && q.TargetHeroId == captive.StringId);
            bool stamped = after != null && !after.IsOutstanding;

            t.Note($"captive={captive.Name} -> {receiver.Name}");
            t.Expect("released", !stillHeld, true);
            t.Expect("stamped", stamped, true);
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

            behavior.GameStateBridge.ExecuteAction(new GameAction { Type = "free_prisoner" }, captive, null);

            t.Note($"{captive.Name}");
            t.Expect("freed", !captive.IsPrisoner, true);
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
                Type              = QuestType.DeclareWar,
                Description       = $"[SELFTEST] Declare war on {target.Name}.",
                TargetFactionId   = target.StringId,
                TargetFactionName = target.Name?.ToString(),
                IssuedOnDay       = behavior.GameStateBridge.GetCurrentGameDay(),
                RewardGold        = 1000,
                RewardRelation    = 8,
                Status            = QuestStatus.Active
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

        private static string TestScenario(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("scenario");

            ScenarioRuntime runtime = ScenarioBehavior.Instance?.Runtime;
            if (t.Abort(runtime == null, "scenario engine not ready")) return t.Line();

            Hero rhagaea = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h.Name != null && h.Name.ToString().IndexOf("Rhagaea", StringComparison.OrdinalIgnoreCase) >= 0);
            if (t.Skip(rhagaea == null, "Rhagaea not alive in this campaign — cannot fire the sample ON_DEATH rule")) return t.Line();

            var heroes = new List<Hero> { rhagaea };
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

        private static string TestGiveItem(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("give_item");

            Hero receiver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
            if (t.Skip(receiver == null, "no lord to receive a gift")) return t.Line();

            var roster = MobileParty.MainParty?.ItemRoster;
            if (t.Abort(roster == null, "no player item roster")) return t.Line();
            if (t.Skip(roster.Count == 0, "player carries no items to give")) return t.Line();

            var item = roster.GetItemAtIndex(0);
            int before = roster.GetItemNumber(item);

            // Drive give_item the bag-picked way (by StringId via OfferedItemId), the LLM-proof path
            var ctx = new ActionContext(GetOrCreate(behavior, receiver), null) { OfferedItemId = item.StringId };
            behavior.GameStateBridge.ExecuteAction(new GameAction { Type = "give_item" }, receiver, ctx);

            int after = roster.GetItemNumber(item);
            t.Note($"item={item.Name}, {before}->{after}, to={receiver.Name}");
            t.Expect("removedOne", after == before - 1, true);
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

        private static string TestSchemeAssist(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("scheme_assist");
            if (t.Abort(SchemeStore.Instance == null, "SchemeStore not active")) return t.Line();

            // Pick actors NOT already entangled in an active scheme, so an earlier test's plot
            // (e.g. scheme_warn leaving one Discovered) can't taint the precondition here.
            bool Free(Hero h) => !SchemeStore.Instance.Active.Any(s => s.PlotterId == h.StringId || s.TargetId == h.StringId);

            Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan && Free(h));
            // The target must NOT be allied with the player (relation < 20, different faction)
            Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != plotter && h != Hero.MainHero && h.IsLord
                && h.MapFaction != Hero.MainHero?.MapFaction
                && h.GetRelation(Hero.MainHero) < 20
                && h.Clan != plotter?.Clan && Free(h));
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

        private static string TestCompanionMission(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("companion_mission");

            CompanionMissionBehavior missions = CompanionMissionBehavior.Instance;
            if (t.Abort(missions == null, "companion-mission system not active")) return t.Line();

            Hero companion = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != Hero.MainHero && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan)
                && h.PartyBelongedTo == MobileParty.MainParty && !missions.IsAway(h.StringId));
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
            BastardBehavior.Instance.DebugClaim(subject, pressesClaim: true);

            int relAfter = behavior.GameStateBridge.GetPlayerRelation(subject);
            int eventsAfter = WorldEventStore.Instance.Count;
            t.Note($"subject={subject.Name}, rel {relBefore}->{relAfter}, events {eventsBefore}->{eventsAfter}");
            t.Expect("turnedHostile", relAfter < relBefore, true);
            t.Expect("claimRecorded", eventsAfter > eventsBefore, true);
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

        private static string TestStanceLetter(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("stance_letter");

            Hero npc = ConversationNpc();
            if (t.Skip(npc == null, "no NPC to take the stance")) return t.Line();
            if (t.Skip(npc.Clan == Clan.PlayerClan, "NPC is in the player's own clan — no stance letters")) return t.Line();

            NpcProfile profile = GetOrCreate(behavior, npc);
            int before = profile.SentLetters.Count(l => l.Reason == LetterReason.StanceHostility);
            profile.ReputationWithPlayer = -50;   // real hostility (above lethal) -> an open-enmity Threat letter
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
            var ack = new ParsedResponse { Dialogue = "", QuestCompleted = new QuestCompletionClaim { Type = QuestType.External } };
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

        // ── Conversation smoke tests (each = 1 real LLM call; relevance judged from the log) ──

        private static string TestLlmDialogue(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("llm_dialogue");
            if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured — set an API key + model, or an OpenAI-compatible endpoint")) return t.Line();

            Hero npc = ConversationNpc();
            if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

            NpcProfile profile = GetOrCreate(behavior, npc);
            const string question = "Greetings. Tell me plainly — what is the mood of the realm these days, and how do you regard me?";
            var ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

            var (ok, reply, err) = AskLlm(behavior, "llm_dialogue", npc, profile, ctx, question);
            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Note($"{npc.Name} replied {reply.Length} chars | A: {Show(reply)}");
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
            var ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

            var (ok, reply, err) = AskLlm(behavior, "chat_memory", npc, profile, ctx, question);
            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Note($"judge in log: does {npc.Name} reference Pravend + the quarrel (not invented history)? | A: {Show(reply)}");
            t.Expect("gotNonEmptyReply", reply.Length > 0, true);
            return t.Line();
        }

        private static string TestChatStance(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("chat_stance");
            if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

            Hero npc = ConversationNpc();
            if (t.Skip(npc == null, "no NPC to converse with")) return t.Line();

            NpcProfile profile = GetOrCreate(behavior, npc);
            profile.StanceTrust = -40;  // distrusts the player's word
            profile.StanceRespect = 30; // grudging respect for their prowess
            profile.StanceFear = 70;    // and is plainly intimidated
            behavior.Store.Set(profile);

            const string question = "Look me in the eye and tell me — how do you truly regard me?";
            var ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

            var (ok, reply, err) = AskLlm(behavior, "chat_stance", npc, profile, ctx, question);
            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Note($"set trust-40/respect+30/fear+70; judge in log: wary + intimidated yet not trusting? | A: {Show(reply)}");
            t.Expect("gotNonEmptyReply", reply.Length > 0, true);
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
            string question = $"What do you make of {other.Name}? Tell me true.";
            var ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

            var (ok, reply, err) = AskLlm(behavior, "chat_hearsay", npc, profile, ctx, question);
            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Note($"asked about {other.Name}; judge in log: answers from a vantage (friend/rival/stranger), never invents whereabouts | A: {Show(reply)}");
            t.Expect("gotNonEmptyReply", reply.Length > 0, true);
            return t.Line();
        }

        private static string TestChatBastardMother(NpcMemoryCampaignBehavior behavior)
        {
            var t = new TestRun("chat_bastard_mother");
            if (t.Skip(behavior.LlmServices is not {IsAvailable: true}, "LLM not configured")) return t.Line();

            // Prefer an UNMARRIED woman: a strictly-monogamous wife's fidelity persona contradicts (and
            // tends to suppress) the "you bore the player a bastard" secret, sabotaging the read.
            Hero npc = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && h.IsLord && h.Spouse == null)
                    ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && h.Spouse == null)
                    ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale && h.IsLord)
                    ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsFemale);
            if (t.Skip(npc == null, "no female NPC to play the mother")) return t.Line();

            NpcProfile profile = GetOrCreate(behavior, npc);
            // Seed the shared past a real bastard-mother would carry, so the profile does not read as a
            // stranger and contradict the secret (the bug behind her once denying they had ever met).
            int day = behavior.GameStateBridge.GetCurrentGameDay();
            profile.Events.Add(new NotableEvent(day - 900, NotableEventType.Intimacy, "[SELFTEST] We were lovers for a season, years ago, before duty pulled us apart."));
            behavior.Store.Set(profile);

            // Force the prompt path directly: a mercenary bastard-mother holding the secret. (Needs Adult >= Mature.)
            var ctx = new EncounterContext { BastardMotherTone = "mercenary", BastardBlackmailDemand = 2000 };

            const string question = "It has been some time. It is good to see you again.";
            var (ok, reply, err) = AskLlm(behavior, "chat_bastard_mother", npc, profile, ctx, question);
            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Note($"{npc.Name} as a mercenary mother; judge in log: does she raise the hidden child + press for coin (Adult must be >= Mature)? | A: {Show(reply)}");
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
            var ctx = EncounterContextBuilder.Build(npc, profile, behavior.GameStateBridge, question);

            var (ok, reply, err) = AskLlm(behavior, "chat_worldevent", npc, profile, ctx, question);
            if (t.Abort(!ok, $"LLM call failed: {err}")) return t.Line();

            t.Note($"fed the Pravend granary fire; judge in log: does {npc.Name} mention it (as hearsay)? | A: {Show(reply)}");
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

        private static Hero ConversationNpc()
            => Hero.OneToOneConversationHero ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

        /// <summary>Runs one question through the real chat pipeline, logs the full Q&amp;A, and returns the reply.</summary>
        private static (bool ok, string reply, string error) AskLlm(
            NpcMemoryCampaignBehavior behavior, string label, Hero npc, NpcProfile profile, EncounterContext ctx, string question)
        {
            var services = behavior.LlmServices;
            if (services is not {IsAvailable: true}) return (false, null, "LLM not configured");

            var world = new WorldState { CurrentDay = behavior.GameStateBridge.GetCurrentGameDay() };
            var session = new ChatSession();

            // Synchronous wait on the real pipeline (Task.Run avoids any main-thread sync-context capture).
            var result = Task.Run(() => services.ChatService.ChatAsync(profile, world, session, question, ctx)).GetAwaiter().GetResult();
            if (result is not {IsSuccess: true}) return (false, null, result?.ErrorMessage ?? "no result");

            string reply = result.Response?.Dialogue ?? "";
            ModLog.Info($"[SELFTEST] {label} [model: {ModelLabel(behavior)}] — Q to {npc.Name}: {question}");
            ModLog.Info($"[SELFTEST] {label} — A from {npc.Name}: {reply}");

            return (true, reply, null);
        }

        /// <summary>The active LLM model id (e.g. "x-ai/grok-4.20"), so test logs can be compared across LLMs.</summary>
        private static string ModelLabel(NpcMemoryCampaignBehavior behavior)
        {
            string model = behavior?.LlmServices?.ModelId;
            return string.IsNullOrWhiteSpace(model) ? "unknown" : model;
        }

        // Set by RunChat ("test mode"): show the whole reply in the report instead of a 180-char preview.
        private static bool _fullReplies;

        /// <summary>The reply as shown in the report: full text in test mode (cr.test chat), else a short preview.</summary>
        private static string Show(string s) => _fullReplies ? (s ?? "") : Preview(s);

        private static string Preview(string s)
            => string.IsNullOrEmpty(s)
                ? ""
                : s.Length > 180
                    ? s.Substring(0, 180) + "..."
                    : s;

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string SafeRun(SelfTest test, NpcMemoryCampaignBehavior behavior)
        {
            try { return test.Run(behavior); }
            catch (Exception ex)
            {
                string msg = $"[SELFTEST] {test.Name}: FAIL — threw {ex.GetType().Name}: {ex.Message}";
                ModLog.Error(msg);
                return msg;
            }
        }

        private static NpcProfile GetOrCreate(NpcMemoryCampaignBehavior behavior, Hero hero)
        {
            NpcProfile p = behavior.Store.Get(hero.StringId);
            if (p != null) return p;
            p = HeroProfileMapper.FromHero(hero);
            behavior.Store.Set(p);
            return p;
        }

        // ── Types ─────────────────────────────────────────────────────────────

        /// <summary>A registered test: its name, how costly its mutations are, a one-line description, and the body.</summary>
        private sealed class SelfTest
        {
            public readonly string Name;
            public readonly Severity Severity;
            public readonly string Description;
            public readonly Func<NpcMemoryCampaignBehavior, string> Run;

            /// <summary>True when the test makes a real LLM call (a conversation smoke test) — costs an API call + a freeze.</summary>
            public readonly bool CallsLlm;

            public SelfTest(string name, Severity severity, string description, Func<NpcMemoryCampaignBehavior, string> run, bool callsLlm = false)
            {
                Name = name;
                Severity = severity;
                Description = description;
                Run = run;
                CallsLlm = callsLlm;
            }
        }

        /// <summary>
        ///   Builds a test's result line. <see cref="Skip" /> short-circuits to MANUAL (precondition unmet),
        ///   <see cref="Abort" /> to FAIL (setup failed); <see cref="Expect" /> records labelled assertions and
        ///   <see cref="Note" /> adds free-form detail. <see cref="Line" /> renders the verdict + detail.
        /// </summary>
        private sealed class TestRun
        {
            private readonly StringBuilder _detail = new StringBuilder();
            private readonly string _name;
            private bool _failed;
            private bool _manual;
            private string _manualReason;

            public TestRun(string name) => _name = name;

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

            /// <summary>Add a free-form detail note (does not affect the verdict).</summary>
            public void Note(string text) => Append(text);

            /// <summary>Render the final [SELFTEST] line.</summary>
            public string Line()
            {
                if (_manual) return $"[SELFTEST] {_name}: MANUAL — {_manualReason}";

                string d = _detail.ToString();
                if (_failed) return $"[SELFTEST] {_name}: FAIL — {d}";
                return d.Length > 0
                    ? $"[SELFTEST] {_name}: PASS — {d}"
                    : $"[SELFTEST] {_name}: PASS";
            }

            private void Append(string text)
                => _detail.Append(_detail.Length > 0 ? " | " : "").Append(text);
        }
    }
}
