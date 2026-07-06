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

        /// <summary>Test sway magnitude for cr.grudge_sway_test: a plausible mid-range value on the same scale as CourtActionPolicy.SwayAgainstDelta.</summary>
        private const int TestSwayMagnitude = 20;

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
        ///   cr.grudge_sway_test [name]: plants a synthetic SwayExposed grudge on the NPC (bypassing the
        ///   real sway_opinion + rumour-reveal pipeline) via the real Tier2GrudgeRules.SwayExposedGrudge
        ///   rule and TryPlant gate, mirroring cr.grudge_test's shape. To exercise the REAL delayed reveal
        ///   pipeline instead (sway_opinion -> Rivalry rumour -> SwayExposureBehavior -> awareness ->
        ///   plant), sway a real target in conversation (stance=against) then run cr.grudge_sway_reveal.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_sway_test", "cr")]
        public static string Grudges_SwayTest(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            var today = (int) CampaignTime.Now.ToDays;
            var facts = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = TestSwayMagnitude};
            GrudgeProposal proposal = Tier2GrudgeRules.SwayExposedGrudge(facts);

            bool planted = GrudgeBehavior.Instance.TryPlant(
                npc, Hero.MainHero, GrudgeSource.SwayExposed,
                "[SELFTEST] Word reached them that the player had turned someone's opinion against them.",
                proposal.Magnitude, proposal.KnownToPlayer, today);

            return planted
                ? $"{npc.Name} now nurses a test SwayExposed grudge (hidden, magnitude {proposal.Magnitude}). Run 'cr.grudges {npc.Name}' to inspect it."
                : $"{npc.Name} refused the test grudge (anti-noise guardrail or a full, stronger house).";
        }

        /// <summary>
        ///   cr.grudge_sway_reveal [name]: forces the REAL pending SwayExposureBehavior watch for this
        ///   NPC (queued by a real in-conversation sway_opinion against them) to reveal now, bypassing only
        ///   the awareness TIMING (never the SwayExposedGrudge rule nor TryPlant's own guardrails). Reports
        ///   when no watch is pending.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_sway_reveal", "cr")]
        public static string Grudges_SwayReveal(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (SwayExposureBehavior.Instance == null) return "Sway exposure system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            return SwayExposureBehavior.Instance.DebugForceReveal(npc);
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
        ///   cr.grudge_resolve [amends|gift &lt;value&gt;|deed] [name] — runs <see cref="GrudgeResolutionService.TryResolveDominant" />
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
            else if (nameTokens.Count > 0 && nameTokens[0].Equals("deed", System.StringComparison.OrdinalIgnoreCase))
            {
                gesture = GrudgeGesture.Deed;
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

        /// <summary>
        ///   cr.grudge_unblessed_test [name]: plants a synthetic UnblessedMarriage grudge on the NPC
        ///   (bypassing the real MarriageApprovalBehavior wedding pipeline) via the real
        ///   Tier2GrudgeRules.UnblessedMarriageGrudge rule and TryPlant gate, mirroring cr.grudge_sway_test's
        ///   shape. To exercise the REAL plant instead, revoke or never grant a blessing (cr.marriage_revoke)
        ///   then marry the family's kin via cr.marry, and check with cr.grudges.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_unblessed_test", "cr")]
        public static string Grudges_UnblessedMarriageTest(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            var today = (int) CampaignTime.Now.ToDays;
            var facts = new UnblessedMarriageFacts {MarriageIsUnblessed = true, StillMarried = true};
            GrudgeProposal proposal = Tier2GrudgeRules.UnblessedMarriageGrudge(facts);

            bool planted = GrudgeBehavior.Instance.TryPlant(
                npc, Hero.MainHero, GrudgeSource.UnblessedMarriage,
                "[SELFTEST] The player wed into their family without earning its blessing.",
                proposal.Magnitude, proposal.KnownToPlayer, today);

            return planted
                ? $"{npc.Name} now nurses a test UnblessedMarriage grudge (known, magnitude {proposal.Magnitude}). " +
                  $"Run 'cr.grudges {npc.Name}' to inspect it, or 'cr.grudge_unblessed_clear' to test the divorce-time clear."
                : $"{npc.Name} refused the test grudge (anti-noise guardrail or a full, stronger house).";
        }

        /// <summary>
        ///   cr.grudge_unblessed_clear: runs the exact GrudgeBehavior.ResolveUnblessedMarriageGrudgesAgainst
        ///   seam DivorceProcessBehavior and SpouseDivorceDemandBehavior call the moment the player's own
        ///   marriage dissolves, clearing every live UnblessedMarriage grudge against the player regardless
        ///   of holder. No NPC argument: this source is only ever aimed at the player, so it resolves
        ///   everything at once, exactly as a real divorce would.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_unblessed_clear", "cr")]
        public static string Grudges_UnblessedMarriageClear(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";
            if (Hero.MainHero == null) return "No player hero.";

            var today = (int) CampaignTime.Now.ToDays;
            int resolved = GrudgeBehavior.Instance.ResolveUnblessedMarriageGrudgesAgainst(Hero.MainHero.StringId, today);

            return resolved > 0
                ? $"{resolved} UnblessedMarriage grudge(s) against the player resolved, as at a real divorce. Run 'cr.grudges <name>' to confirm."
                : "No live UnblessedMarriage grudge against the player found to clear.";
        }

        /// <summary>
        ///   cr.grudge_execute_kin [name] - force-runs the exact ExecutedKinGrudgeBehavior plant loop
        ///   against the named hero's close living kin (spouse/parents/children/siblings), as if the
        ///   player had just had them executed, without actually killing anyone. Goes through the real
        ///   TryPlant gate for every kin considered, so it can legitimately plant fewer than it considers
        ///   (anti-noise guardrail, or a full, stronger house).
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("grudge_execute_kin", "cr")]
        public static string Grudges_ExecuteKin(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (GrudgeBehavior.Instance == null) return "Grudge system not active.";

            Hero npc = ResolveHero(args);
            if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";
            if (npc == Hero.MainHero) return "Pick an NPC, not the player.";

            var today = (int) CampaignTime.Now.ToDays;
            string cause = $"[SELFTEST] {Hero.MainHero.Name} had {npc.Name} put to death.";

            (int considered, int planted) = ExecutedKinGrudgeBehavior.PlantOnKinOf(npc, cause, today);

            return considered == 0
                ? $"{npc.Name} has no close living kin (spouse/parent/adult child/adult sibling) to grudge the player."
                : $"{npc.Name}'s kin: {considered} considered, {planted} vendetta grudge(s) planted. Run 'cr.grudges <kin name>' to inspect.";
        }
    }
}
