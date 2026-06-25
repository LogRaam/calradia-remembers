// Code written by Gabriel Mailhot, 25/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Diagnostics
{
    /// <summary>
    ///   End-to-end self-test harness. Each test seeds state via the real service/bridge,
    ///   asserts what happened, and logs a <c>[SELFTEST]</c> PASS/FAIL/MANUAL line.
    ///   <para>
    ///     WARNING: tests mutate live campaign state (relations, quests, profiles).
    ///     Copy your save before running, or reload when done to discard mutations.
    ///   </para>
    ///   Access via <c>cr.testall</c> (all) or <c>cr.test_*</c> (individual).
    /// </summary>
    public static class CrSelfTest
    {
        private static readonly Dictionary<string, Func<NpcMemoryCampaignBehavior, string>> Registry
            = new Dictionary<string, Func<NpcMemoryCampaignBehavior, string>>
            {
                ["providegold"] = TestProvideGold,
                ["scheme_warn"] = TestSchemeWarn,
                ["succession"]  = TestSuccession,
            };

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Runs all registered tests; returns a multi-line report ending with a summary line.</summary>
        public static string RunAll(NpcMemoryCampaignBehavior behavior)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SELFTEST] *** WARNING: tests mutate campaign state — reload or use a copied save ***");

            int pass = 0, fail = 0, manual = 0;
            foreach (KeyValuePair<string, Func<NpcMemoryCampaignBehavior, string>> kv in Registry)
            {
                string line = SafeRun(kv.Key, kv.Value, behavior);
                sb.AppendLine(line);
                if      (line.Contains(": PASS"))  pass++;
                else if (line.Contains(": FAIL"))  fail++;
                else                               manual++;
            }

            sb.AppendLine($"[SELFTEST] SUMMARY: {pass} PASS, {fail} FAIL, {manual} MANUAL");
            string report = sb.ToString();
            ModLog.Info(report);
            return report;
        }

        /// <summary>Runs one named test; returns its result line.</summary>
        public static string Run(string testName, NpcMemoryCampaignBehavior behavior)
        {
            string key = testName?.ToLowerInvariant();
            if (key == null || !Registry.TryGetValue(key, out Func<NpcMemoryCampaignBehavior, string> test))
                return $"[SELFTEST] Unknown test '{testName}'. Available: {string.Join(", ", Registry.Keys)}";

            string result = SafeRun(testName, test, behavior);
            ModLog.Info(result);
            return result;
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        private static string TestProvideGold(NpcMemoryCampaignBehavior behavior)
        {
            const string T = "providegold";

            Hero giver = Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
            if (giver == null) return Manual(T, "no live lords in this campaign");

            // 1. Seed a ProvideGold quest directly via the real quest service
            NpcProfile profile = GetOrCreate(behavior, giver);
            QuestService.ApplyResponse(
                profile, giver,
                new ParsedResponse {
                    Dialogue = "",
                    QuestGiven = new QuestProposal {
                        Type        = QuestType.ProvideGold,
                        Description = $"[SELFTEST] {giver.Name} asks you for 500 denars.",
                        DeadlineDays  = 30,
                        RewardRelation = 5
                    }
                },
                behavior.GameStateBridge.GetCurrentGameDay(),
                behavior.GameStateBridge, behavior.Store, null);

            profile = behavior.Store.Get(giver.StringId);
            int seeded = profile.ActiveQuests.Count(q => q.Type == QuestType.ProvideGold && q.IsOutstanding);
            if (seeded == 0)
                return Fail(T, $"quest not seeded for {giver.Name} (QuestService.ApplyResponse did not create it)");

            // 2. Satisfy via take_gold — same code path as the Give Gold button
            behavior.GameStateBridge.ExecuteAction(
                new GameAction {
                    Type = "take_gold",
                    Parameters = new Dictionary<string, string> { { "amount", "500" } }
                },
                giver, null);

            profile = behavior.Store.Get(giver.StringId);
            bool stillOpen = profile.ActiveQuests.Any(q => q.Type == QuestType.ProvideGold && q.IsOutstanding);
            return stillOpen
                ? Fail(T, $"take_gold did not stamp ProvideGold satisfied (giver={giver.Name})")
                : Pass(T, $"seeded+stamped for {giver.Name}");
        }

        private static string TestSchemeWarn(NpcMemoryCampaignBehavior behavior)
        {
            const string T = "scheme_warn";

            if (SchemeStore.Instance == null) return Fail(T, "SchemeStore not active");

            Hero plotter = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != Hero.MainHero && h.IsLord && h.Clan != Clan.PlayerClan);
            Hero target = Hero.AllAliveHeroes.FirstOrDefault(h =>
                h != plotter && h != Hero.MainHero && h.IsLord
                && h.Clan != null && h.Clan != plotter?.Clan && h.Clan != Clan.PlayerClan);

            if (plotter == null || target == null)
                return Manual(T, "need 2+ lords from distinct non-player clans");

            // Plant scheme so TargetHeeds has something eligible to expose
            SchemeStore.Instance.ForcePlot(plotter, target, SchemeKind.Slander);

            int plotterBefore = behavior.GameStateBridge.GetPlayerRelation(plotter);
            int targetBefore  = behavior.GameStateBridge.GetPlayerRelation(target);

            string msg = SchemeStore.Instance.TargetHeeds(target);
            if (msg == null)
                return Fail(T, $"TargetHeeds({target.Name}) returned null — no eligible scheme after ForcePlot");

            int plotterDelta = behavior.GameStateBridge.GetPlayerRelation(plotter) - plotterBefore;
            int targetDelta  = behavior.GameStateBridge.GetPlayerRelation(target)  - targetBefore;

            bool plotterResents = plotterDelta <= -20;
            bool targetGrateful = targetDelta  >= 16;
            string detail = $"plotter={plotter.Name} Δ{plotterDelta:+#;-#;0} (want ≤-20) | " +
                            $"target={target.Name} Δ{targetDelta:+#;-#;0} (want ≥+16)";

            return (plotterResents && targetGrateful) ? Pass(T, detail) : Fail(T, detail);
        }

        private static string TestSuccession(NpcMemoryCampaignBehavior behavior)
        {
            const string T = "succession";

            if (SuccessionBehavior.Instance == null) return Fail(T, "SuccessionBehavior not active");

            // Prefer a clan member; fall back to any alive lord
            Hero heir = Clan.PlayerClan?.Heroes?.FirstOrDefault(h => h != Hero.MainHero && h.IsAlive)
                     ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);
            if (heir == null) return Manual(T, "no heir candidate — join a clan or ensure a lord is alive");

            // Find a profile that has at least one private event (will be dropped by succession)
            string targetId     = null;
            int    privateBefore = 0, publicBefore = 0;
            foreach (string id in behavior.Store.ListIds())
            {
                NpcProfile p = behavior.Store.Get(id);
                if (p == null) continue;
                int priv = p.Events.Count(e => !SuccessionMemoryPolicy.SurvivesSuccession(e.type));
                if (priv <= 0) continue;
                targetId      = id;
                privateBefore = priv;
                publicBefore  = p.Events.Count(e => SuccessionMemoryPolicy.SurvivesSuccession(e.type));
                break;
            }

            if (targetId == null)
                return Manual(T, "no profile has private events yet — open a captive or intimate scene first, then re-run");

            string predName = Hero.MainHero.Name?.ToString();

            // Apply — mutates ALL profiles in the store (heir inherits standing, not private history)
            int touched = SuccessionBehavior.Instance.Apply(Hero.MainHero, heir);

            NpcProfile after = behavior.Store.Get(targetId);
            int privateAfter = after.Events.Count(e => !SuccessionMemoryPolicy.SurvivesSuccession(e.type));
            int publicAfter  = after.Events.Count(e =>  SuccessionMemoryPolicy.SurvivesSuccession(e.type));

            bool privateDropped = privateAfter   == 0;
            bool publicKept     = publicAfter    == publicBefore;
            bool inheritedSet   = after.InheritedFromName == predName;

            string detail = $"private {privateBefore}→{privateAfter} (want 0) | " +
                            $"public {publicBefore}→{publicAfter} (want same) | " +
                            $"inherited='{after.InheritedFromName}' | heir={heir.Name} ({touched} profiles)";

            return (privateDropped && publicKept && inheritedSet) ? Pass(T, detail) : Fail(T, detail);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string SafeRun(
            string name,
            Func<NpcMemoryCampaignBehavior, string> test,
            NpcMemoryCampaignBehavior behavior)
        {
            try { return test(behavior); }
            catch (Exception ex)
            {
                string msg = Fail(name, $"threw {ex.GetType().Name}: {ex.Message}");
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

        private static string Pass(string name, string detail = null)
            => detail == null
                ? $"[SELFTEST] {name}: PASS"
                : $"[SELFTEST] {name}: PASS — {detail}";

        private static string Fail(string name, string detail)
            => $"[SELFTEST] {name}: FAIL — {detail}";

        private static string Manual(string name, string reason)
            => $"[SELFTEST] {name}: MANUAL — {reason}";
    }
}
