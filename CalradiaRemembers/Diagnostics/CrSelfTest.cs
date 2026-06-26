// Code written by Gabriel Mailhot, 25/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            new SelfTest("scheme_assist",    Severity.Heavy,        "plot + regard -> PlayerAssists -> assert the plot advanced",       TestSchemeAssist),
            new SelfTest("pregnancy",        Severity.Heavy,        "fertile female -> TryConceive(force) -> assert pregnant",          TestPregnancy),
            new SelfTest("companion_mission",Severity.Heavy,        "dispatch a companion -> assert away -> resolve -> assert home",    TestCompanionMission),
            new SelfTest("declare_war",      Severity.Irreversible, "seed deed -> declare war -> assert WarDeclared stamps it",         TestDeclareWar),
            new SelfTest("scenario",         Severity.Irreversible, "fire ON_DEATH:Rhagaea x2 -> assert a rule fires then seals",       TestScenario),
        };

        // ── Public API (cr.testall / cr.test) ─────────────────────────────────

        /// <summary>Runs all registered tests; returns a multi-line report ending with a summary line.</summary>
        public static string RunAll(NpcMemoryCampaignBehavior behavior)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SELFTEST] *** WARNING: tests mutate campaign state — reload or use a copied save ***");
            if (Tests.Any(t => t.Severity == Severity.Irreversible))
                sb.AppendLine("[SELFTEST] *** includes IRREVERSIBLE tests (declare war / seal scenarios) — reload is mandatory ***");

            int pass = 0, fail = 0, manual = 0;
            foreach (SelfTest test in Tests)
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

            string result = SafeRun(test, behavior);
            ModLog.Info(result);
            return result;
        }

        /// <summary>Lists the registered tests (name + severity + description) for cr.test list.</summary>
        public static string List()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[SELFTEST] {Tests.Count} registered test(s):");
            foreach (SelfTest t in Tests)
                sb.AppendLine($"  {t.Name}  [{t.Severity}]  — {t.Description}");
            sb.AppendLine("Run all: cr.testall   |   one: cr.test <name>");
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

            public SelfTest(string name, Severity severity, string description, Func<NpcMemoryCampaignBehavior, string> run)
            {
                Name = name;
                Severity = severity;
                Description = description;
                Run = run;
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
