// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar: console diagnostics. Mirrors ModCheats.Grudges.cs's shape (cr.grudges ->
// cr.appreciations, a read-only dev-truth dump showing EVERYTHING regardless of player knowledge, unlike
// the encyclopedia which only ever shows what the player has earned).

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Grudges;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>Test-appreciation magnitude for cr.appreciation_test — comfortably above every formation floor.</summary>
        private const int TestAppreciationMagnitude = 40;

        /// <summary>
        ///   cr.appreciations [name] — dev-truth dump of every appreciation this NPC has ever felt (live and
        ///   fully-faded alike), regardless of what the player has actually learned. Source, cause, day,
        ///   stored vs TODAY's effective magnitude (and, when a live grudge shadows it, the TARNISHED value
        ///   too), known/hidden. Read-only.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("appreciations", "cr")]
        public static string Appreciations_List(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (AppreciationBehavior.Instance == null) return "Appreciation system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            IReadOnlyList<Appreciation> all = AppreciationBehavior.Instance.AppreciationsFor(npc.StringId);
            if (all.Count == 0) return $"{npc.Name} feels no appreciation, live or faded.";

            var today = (int) CampaignTime.Now.ToDays;
            CalradiaRemembers.Logic.Appreciations.TraitFacts traits = AppreciationBridge.BuildTraitFacts(npc);
            int activeGrudgeMagnitude = ResolveDominantLiveGrudgeMagnitude(npc, today);

            var sb = new StringBuilder();
            sb.AppendLine($"{npc.Name} — {all.Count} appreciation(s) on record (dominant live grudge magnitude today: {activeGrudgeMagnitude}):");
            foreach (Appreciation a in all.OrderByDescending(a => a.EventDay))
            {
                int effective = AppreciationPolicy.EffectiveMagnitude(a, today, traits);
                int tarnished = AppreciationPolicy.TarnishedMagnitude(effective, activeGrudgeMagnitude);
                sb.AppendLine($"  [{a.Source}] day {a.EventDay}: \"{a.CauseSummary}\"");
                sb.AppendLine($"    magnitude {a.Magnitude} -> effective today {effective} -> tarnished {tarnished} ({AppreciationNarrator.WarmthWord(tarnished)})");
                sb.AppendLine($"    known={a.KnownToPlayer}");
            }

            return sb.ToString();
        }

        /// <summary>
        ///   cr.appreciation_test [name] — plants a synthetic QuestDoneForThem appreciation on the NPC
        ///   (bypassing the real source pipelines) so the prompt/console/encyclopedia loop can be validated
        ///   without seeding a real gift, payment, quest, or divorce. Still goes through the real TryPlant
        ///   gate (anti-noise guardrail, the per-holder cap), so it can legitimately refuse.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("appreciation_test", "cr")]
        public static string Appreciations_Test(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (AppreciationBehavior.Instance == null) return "Appreciation system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            var today = (int) CampaignTime.Now.ToDays;
            bool planted = AppreciationBehavior.Instance.TryPlant(
                npc, Hero.MainHero, AppreciationSource.QuestDoneForThem,
                "[SELFTEST] The player did what they promised.", TestAppreciationMagnitude, knownToPlayer: true, today);

            return planted
                ? $"{npc.Name} now feels a test appreciation (QuestDoneForThem, magnitude {TestAppreciationMagnitude}). Run 'cr.appreciations {npc.Name}' to inspect it, or open chat to see it colour the prompt."
                : $"{npc.Name} refused the test appreciation (anti-noise guardrail or a full, stronger house — run 'cr.appreciations {npc.Name}' to see why).";
        }

        /// <summary>
        ///   cr.appreciation_backfill — force-reruns the one-shot migration scan regardless of the stamped
        ///   version, reporting how many appreciations formed vs were skipped. Idempotent: running it again
        ///   in the same session forms nothing new (the deterministic holder+source+day id dedups), which
        ///   this command calls out explicitly so a second run's zero-formed result reads as SUCCESS, not a
        ///   failure to find anything.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("appreciation_backfill", "cr")]
        public static string Appreciations_Backfill(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
            if (AppreciationBehavior.Instance == null) return "Appreciation system not active.";

            (int formed, int skipped) = AppreciationBehavior.Instance.DebugForceBackfill(behavior.Store);

            return $"[APPRECIATION] backfill: {formed} formed, {skipped} skipped. " +
                   "Idempotent by design — run this again right now and expect 0 formed (every marker already migrated).";
        }

        /// <summary>The holder's dominant live grudge's TODAY's effective magnitude (0 when none) — the same lookup the prompt/encyclopedia tarnish reads.</summary>
        private static int ResolveDominantLiveGrudgeMagnitude(Hero hero, int today)
        {
            IReadOnlyList<Grudge> live = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);
            if (live == null || live.Count == 0) return 0;

            CalradiaRemembers.Logic.Grudges.TraitFacts grudgeTraits = GrudgeBridge.BuildTraitFacts(hero);

            return GrudgePolicy.EffectiveMagnitude(live[0], today, grudgeTraits);
        }
    }
}
