// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar: console diagnostics. Mirrors ModCheats.Divorce.cs's shape (cr.divorce_info -> cr.grudges,
// a read-only dev-truth dump showing EVERYTHING regardless of player knowledge, unlike the encyclopedia
// which only ever shows what the player has earned).

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Verbs;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>Test-grudge magnitude for cr.grudge_test — comfortably above every formation floor.</summary>
        private const int TestGrudgeMagnitude = 40;

        /// <summary>
        ///   cr.grudges [name] — dev-truth dump of every grudge this NPC has ever nursed (live and
        ///   resolved), regardless of what the player has actually learned. Source, cause, day, stored vs
        ///   TODAY's effective magnitude, rigidity + the narrator's qualitative word, known/hidden, and the
        ///   resolution kind. Read-only.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudges", "cr")]
        public static string Grudges_List(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            IReadOnlyList<Grudge> all = GrudgeBehavior.Instance.GrudgesFor(npc.StringId);
            if (all.Count == 0) return $"{npc.Name} nurses no grudge, live or resolved.";

            var today = (int) CampaignTime.Now.ToDays;
            TraitFacts traits = GrudgeBridge.BuildTraitFacts(npc);

            var sb = new StringBuilder();
            sb.AppendLine($"{npc.Name} — {all.Count} grudge(s) on record:");
            foreach (Grudge g in all.OrderByDescending(g => g.EventDay))
            {
                int effective = GrudgePolicy.EffectiveMagnitude(g, today, traits);
                sb.AppendLine($"  [{g.Source}] day {g.EventDay}: \"{g.CauseSummary}\"");
                sb.AppendLine($"    magnitude {g.Magnitude} -> effective today {effective} | rigidity {g.Rigidity} ({GrudgeNarrator.RigidityWord(g.Rigidity)})");
                sb.AppendLine($"    known={g.KnownToPlayer} hintDiscovered={g.ResolutionHintDiscovered} resolvable={GrudgePolicy.IsResolvable(g)} " +
                              $"resolutionKind={g.ResolutionKind} {(g.IsResolved ? $"RESOLVED day {g.ResolvedDay}" : "unresolved")}");
            }

            return sb.ToString();
        }

        /// <summary>
        ///   cr.grudge_test [name] — plants a synthetic FailedQuest grudge on the NPC (bypassing the real
        ///   source pipelines) so the prompt/console loop can be validated without seeding a real quest,
        ///   scheme, or divorce. Still goes through the real TryPlant gate (anti-noise guardrails, the
        ///   per-holder cap), so it can legitimately refuse.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_test", "cr")]
        public static string Grudges_Test(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            var today = (int) CampaignTime.Now.ToDays;
            bool planted = GrudgeBehavior.Instance.TryPlant(
                npc, Hero.MainHero, GrudgeSource.FailedQuest,
                "[SELFTEST] The player left a task to them unfinished.", TestGrudgeMagnitude, knownToPlayer: true, today);

            return planted
                ? $"{npc.Name} now nurses a test grudge (FailedQuest, magnitude {TestGrudgeMagnitude}). Run 'cr.grudges {npc.Name}' to inspect it, or open chat to see it colour the prompt."
                : $"{npc.Name} refused the test grudge (anti-noise guardrail or a full, stronger house — run 'cr.grudges {npc.Name}' to see why).";
        }

        /// <summary>
        ///   cr.grudge_backfill — force-reruns the one-shot migration scan regardless of the stamped
        ///   version, reporting how many grudges formed vs were skipped. Idempotent: running it again in
        ///   the same session forms nothing new (the deterministic holder+source+day id dedups), which
        ///   this command calls out explicitly so a second run's zero-formed result reads as SUCCESS, not
        ///   a failure to find anything.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_backfill", "cr")]
        public static string Grudges_Backfill(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            (int formed, int skipped) = GrudgeBehavior.Instance.DebugForceBackfill(behavior.Store);

            return $"[GRUDGE] backfill: {formed} formed, {skipped} skipped. " +
                   "Idempotent by design — run this again right now and expect 0 formed (every marker already migrated).";
        }

        /// <summary>
        ///   cr.grudge_resolve [amends|gift &lt;value&gt;] [name] — runs <see cref="GrudgeResolutionService.TryResolveDominant" />
        ///   against the NPC's dominant resolvable live grudge with the given gesture, no LLM involved.
        ///   Reports the verdict plus the grudge's before/after magnitude, so the resolution engine can be
        ///   validated deterministically. Defaults to the Amends gesture (giftValue 0) when the gesture
        ///   argument is omitted or unrecognized.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_resolve", "cr")]
        public static string Grudges_Resolve(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            args ??= new List<string>();
            GrudgeGesture gesture = GrudgeGesture.Amends;
            var giftValue = 0;
            var nameTokens = new List<string>(args);

            if (nameTokens.Count > 0 && nameTokens[0].Equals("gift", System.StringComparison.OrdinalIgnoreCase))
            {
                gesture = GrudgeGesture.Gift;
                nameTokens.RemoveAt(0);
                if (nameTokens.Count > 0 && int.TryParse(nameTokens[0], out int parsedValue))
                {
                    giftValue = parsedValue;
                    nameTokens.RemoveAt(0);
                }
            }
            else if (nameTokens.Count > 0 && nameTokens[0].Equals("amends", System.StringComparison.OrdinalIgnoreCase))
            {
                nameTokens.RemoveAt(0);
            }

            Hero npc = ResolveHero(nameTokens);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            var today = (int) CampaignTime.Now.ToDays;
            TraitFacts traits = GrudgeBridge.BuildTraitFacts(npc);

            Grudge before = GrudgeBehavior.Instance.LiveGrudgesFor(npc, today)
                .FirstOrDefault(g => GrudgePolicy.IsResolvable(g) && g.TargetId == Hero.MainHero.StringId);
            if (before == null) return $"{npc.Name} nurses no resolvable live grudge against the player.";

            int magnitudeBefore = before.Magnitude;
            int effectiveBefore = GrudgePolicy.EffectiveMagnitude(before, today, traits);

            GrudgeResolutionAttemptResult result = GrudgeResolutionService.TryResolveDominant(npc, gesture, giftValue, today);

            return $"{npc.Name} [{before.Source}]: gesture={gesture}" + (gesture == GrudgeGesture.Gift ? $"({giftValue})" : "") +
                   $" -> {result.Outcome} | magnitude {magnitudeBefore} (effective {effectiveBefore}) -> {result.Grudge?.Magnitude ?? magnitudeBefore}" +
                   $" | resolved={result.Grudge?.IsResolved ?? false}";
        }

        /// <summary>
        ///   cr.grudge_terms [name] — runs the grudge_terms reveal deterministically (no LLM), via the exact
        ///   same <see cref="GrudgeTermsVerb.Execute" /> path an in-conversation [ACTION] grudge_terms would
        ///   take, against the NPC's dominant known, resolvable grudge whose resolution hint is not yet
        ///   discovered. Reports which grudge's hint was revealed, or that none qualified (already
        ///   discovered, unresolvable, unknown to the player, or no grudge at all).
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_terms", "cr")]
        public static string Grudges_Terms(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            var today = (int) CampaignTime.Now.ToDays;
            Grudge before = GrudgeBehavior.Instance.KnownGrudgesFor(npc.StringId, today)
                .FirstOrDefault(g => !g.ResolutionHintDiscovered && GrudgePolicy.IsResolvable(g) && g.TargetId == Hero.MainHero.StringId);

            if (before == null)
                return $"{npc.Name} has nothing new to reveal (no known, resolvable grudge with an undiscovered hint).";

            var ctx = new VerbContext(npc, null, null, null, null);
            ActionOutcome outcome = new GrudgeTermsVerb().Execute(null, ctx);

            return outcome != null
                ? $"{npc.Name} [{before.Source}]: {outcome.Message}"
                : $"{npc.Name}: grudge_terms produced no outcome (unexpected).";
        }
    }
}
