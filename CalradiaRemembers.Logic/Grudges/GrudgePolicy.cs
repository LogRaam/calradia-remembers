// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: formation guardrails (anti-noise), rigidity (the "regard sets how
// hard resolution is" master rule from the ROADMAP), decay, the per-NPC surfacing cap, and resolution.
// Every constant here is a tuning knob; changing one is a one-line, test-covered decision.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   Every fact <see cref="GrudgePolicy.ShouldForm" /> and <see cref="GrudgePolicy.RigidityAt" />
    ///   need about a candidate grudge, before it becomes a persisted <see cref="Grudge" />.
    /// </summary>
    public sealed class GrudgeCandidateFacts
    {
        /// <summary>Which system is proposing this grudge.</summary>
        public GrudgeSource Source { get; set; }

        /// <summary>The proposed intensity, 0..100, before any trait amplification.</summary>
        public int Magnitude { get; set; }

        /// <summary>Anti-noise guardrail: true only when the cause is clearly attributable to the target. An unattributable event never forms a grudge, however large.</summary>
        public bool IsAttributable { get; set; }

        /// <summary>True when the holder's romantic preferences (Possessive, monogamous-strict) apply — amplifies <see cref="GrudgeSource.Jealousy" /> grudges only.</summary>
        public bool PossessiveAmplifier { get; set; }

        /// <summary>The holder's trait levels.</summary>
        public TraitFacts Traits { get; set; }
    }

    /// <summary>Pure rules governing whether a grudge forms, how rigid it is, how it decays, which ones surface, and how it resolves.</summary>
    public static class GrudgePolicy
    {
        // ── Formation (anti-noise guardrails) ───────────────────────────────

        /// <summary>Tuning: the base magnitude floor below which an event is too minor to grudge over.</summary>
        public const int MagnitudeFloor = 15;

        /// <summary>Tuning: each positive Mercy level raises the formation floor (more forgiving, harder to offend into a lasting grudge).</summary>
        public const int MercyFormationFloorPerLevel = 5;

        /// <summary>Tuning: each positive Calculating level lowers the formation floor (tallies everything).</summary>
        public const int CalculatingFormationFloorPerLevel = 5;

        /// <summary>Tuning: the flat bonus added to a Jealousy candidate's magnitude when <see cref="GrudgeCandidateFacts.PossessiveAmplifier" /> is set.</summary>
        public const int PossessiveJealousyAmplifierBonus = 10;

        /// <summary>
        ///   The candidate's magnitude after applying the possessive/jealousy amplifier. Exposed so
        ///   migration (and any future caller) stores the SAME amplified value on the resulting
        ///   <see cref="Grudge.Magnitude" /> that <see cref="ShouldForm" /> evaluated against.
        /// </summary>
        public static int EffectiveFormationMagnitude(GrudgeCandidateFacts facts)
        {
            if (facts == null) return 0;

            int magnitude = facts.Magnitude;

            if (facts.PossessiveAmplifier && facts.Source == GrudgeSource.Jealousy)
                magnitude += PossessiveJealousyAmplifierBonus;

            return magnitude;
        }

        /// <summary>
        ///   True when a candidate clears every anti-noise guardrail: attributable, and its (possibly
        ///   amplified) magnitude meets the trait-modulated floor. High Mercy raises the floor
        ///   (forgiving); high Calculating lowers it (tallies everything).
        /// </summary>
        public static bool ShouldForm(GrudgeCandidateFacts facts)
        {
            if (facts == null || !facts.IsAttributable) return false;

            int magnitude = EffectiveFormationMagnitude(facts);
            int floor = FormationFloor(facts.Traits);

            return magnitude >= floor;
        }

        private static int FormationFloor(TraitFacts traits)
        {
            if (traits == null) return MagnitudeFloor;

            int floor = MagnitudeFloor
                + traits.Mercy * MercyFormationFloorPerLevel
                - traits.Calculating * CalculatingFormationFloorPerLevel;

            return floor < 0 ? 0 : floor;
        }

        // ── Rigidity (the master rule: regard sets how hard resolution is) ──

        /// <summary>Tuning: rigidity at exactly neutral (zero) regard for the target, before trait modulation.</summary>
        public const int BaseRigidityAtNeutralRegard = 50;

        /// <summary>Tuning: how many rigidity points one point of regard softens (positive regard) or hardens (negative regard).</summary>
        public const double RigidityPerRegardPoint = 0.6;

        /// <summary>Tuning: each positive Calculating level raises rigidity (resolution wants payment or power).</summary>
        public const int CalculatingRigidityPerLevel = 6;

        /// <summary>Tuning: each positive Mercy level lowers rigidity (a sincere amend suffices).</summary>
        public const int MercyRigidityReliefPerLevel = 6;

        /// <summary>The rigidity value marking a vendetta: unforgivable, never decays, never resolves.</summary>
        public const int VendettaRigidity = 100;

        /// <summary>
        ///   The hardest a grudge can become through REGARD AND TRAITS alone: one short of a vendetta.
        ///   <para>
        ///     A vendetta is something you earn by a DEED (putting someone's kin to death), not by being
        ///     disliked enough. The two were indistinguishable while the clamp ceiling and
        ///     <see cref="VendettaRigidity" /> were the same number: any grudge that SATURATED became
        ///     unforgivable, and six separate rules downstream read rigidity 100 as "vendetta" (never
        ///     resolvable, never decays, narrated as unforgivable, every gesture rejected, the greeting
        ///     forced cold). It went unnoticed only because rigidity used to be computed from the vanilla
        ///     clan relation, where the required regard (about -84 at neutral traits) is nearly unreachable.
        ///     Once it read the mod's own ledger, whose captive floor is -100, an NPC at -80 turned every
        ///     grudge they held into a permanent one: the whole resolution pillar became unreachable for
        ///     precisely the people worth reconciling with.
        ///   </para>
        /// </summary>
        public const int MaxEarnedRigidity = VendettaRigidity - 1;

        /// <summary>
        ///   Rigidity at formation: high regard for the target softens it (reconcilable), negative
        ///   regard hardens it. <see cref="GrudgeSource.ExecutedKin" /> is always maximum rigidity
        ///   regardless of regard or traits (a vendetta), and is the ONLY way to reach it. Calculating
        ///   pushes rigidity up; Mercy pulls it down. Clamped 0..<see cref="MaxEarnedRigidity" />.
        /// </summary>
        public static int RigidityAt(int regardForTarget, GrudgeCandidateFacts facts)
        {
            if (facts != null && facts.Source == GrudgeSource.ExecutedKin) return VendettaRigidity;

            double rigidity = BaseRigidityAtNeutralRegard - regardForTarget * RigidityPerRegardPoint;

            TraitFacts traits = facts?.Traits;
            int calculating = traits?.Calculating ?? 0;
            int mercy = traits?.Mercy ?? 0;

            rigidity += calculating * CalculatingRigidityPerLevel;
            rigidity -= mercy * MercyRigidityReliefPerLevel;

            return Clamp((int) Math.Round(rigidity), 0, MaxEarnedRigidity);
        }

        /// <summary>
        ///   Save migration for the saturation bug above: a grudge that is NOT a true vendetta but was
        ///   stamped at <see cref="VendettaRigidity" /> is frozen forever (unresolvable and non-decaying),
        ///   so old saves would carry the defect for the rest of the campaign however well it is fixed at
        ///   formation. Returns the rigidity this grudge should now hold. Idempotent: a value already at or
        ///   below <see cref="MaxEarnedRigidity" /> is returned untouched, and a real ExecutedKin vendetta
        ///   keeps its 100.
        /// </summary>
        public static int HealSaturatedRigidity(GrudgeSource source, int rigidity)
            => source != GrudgeSource.ExecutedKin && rigidity >= VendettaRigidity
                ? MaxEarnedRigidity
                : rigidity;

        // ── Decay ────────────────────────────────────────────────────────────

        /// <summary>Tuning: magnitude points softened per in-game day at baseline (no Mercy/Calculating pull).</summary>
        public const double BaseDecayPerDay = 0.4;

        /// <summary>Tuning: additional decay per day, per positive Mercy level (forgives faster).</summary>
        public const double MercyDecayBonusPerLevel = 0.3;

        /// <summary>Tuning: decay slowed per day, per positive Calculating level (tallies harder, forgets slower).</summary>
        public const double CalculatingDecaySlowdownPerLevel = 0.15;

        /// <summary>Tuning: the decay rate never drops below this floor (even a high-Calculating holder still drifts, unless the freeze threshold below applies).</summary>
        public const double MinDecayPerDay = 0.05;

        /// <summary>Tuning: at or above this Calculating level, a grudge NEVER decays (tallies everything, forgets nothing).</summary>
        public const int NeverDecayCalculatingLevel = 2;

        /// <summary>
        ///   The grudge's live intensity today: resolved grudges read as zero (never surface again); a
        ///   vendetta (<see cref="VendettaRigidity" />) or a holder at/above
        ///   <see cref="NeverDecayCalculatingLevel" /> Calculating never softens; everyone else decays
        ///   linearly from <see cref="Grudge.EventDay" />, faster with higher Mercy, slower with higher
        ///   Calculating (down to <see cref="MinDecayPerDay" />, never fully frozen short of the vendetta
        ///   or never-decay case). The stored <see cref="Grudge.Magnitude" /> itself is never mutated;
        ///   this is a live, idempotent read.
        /// </summary>
        public static int EffectiveMagnitude(Grudge grudge, int today, TraitFacts traits)
        {
            if (grudge == null || grudge.IsResolved) return 0;
            if (grudge.Rigidity >= VendettaRigidity) return Clamp(grudge.Magnitude, 0, 100);
            if (traits != null && traits.Calculating >= NeverDecayCalculatingLevel) return Clamp(grudge.Magnitude, 0, 100);

            int daysElapsed = today - grudge.EventDay;
            if (daysElapsed <= 0) return Clamp(grudge.Magnitude, 0, 100);

            double mercy = traits?.Mercy ?? 0;
            double calculating = traits?.Calculating ?? 0;

            double decayPerDay = BaseDecayPerDay
                + PositivePart(mercy) * MercyDecayBonusPerLevel
                - PositivePart(calculating) * CalculatingDecaySlowdownPerLevel;

            if (decayPerDay < MinDecayPerDay) decayPerDay = MinDecayPerDay;

            double effective = grudge.Magnitude - decayPerDay * daysElapsed;

            return effective <= 0 ? 0 : (int) Math.Floor(effective);
        }

        // ── Per-NPC cap (anti-noise) ─────────────────────────────────────────

        /// <summary>Tuning: the maximum number of a single holder's live grudges that ever surface at once.</summary>
        public const int PerNpcCap = 3;

        /// <summary>
        ///   The dominant grudges among one holder's grievances: resolved and fully-decayed (effective
        ///   magnitude zero) entries never surface; the rest are ranked by live effective magnitude and
        ///   truncated to <see cref="PerNpcCap" />, so the system never reads as a bookkeeper's ledger.
        /// </summary>
        public static IEnumerable<Grudge> SelectDominant(IEnumerable<Grudge> grudges, int today, TraitFacts traits)
        {
            if (grudges == null) return Enumerable.Empty<Grudge>();

            return grudges
                .Where(g => g != null && !g.IsResolved && EffectiveMagnitude(g, today, traits) > 0)
                .OrderByDescending(g => EffectiveMagnitude(g, today, traits))
                .Take(PerNpcCap)
                .ToList();
        }

        /// <summary>
        ///   The per-holder admission rule a live planting call (<c>GrudgeBehavior.TryPlant</c>) enforces
        ///   BEFORE forming a new grudge: below <see cref="PerNpcCap" /> live grudges, the newcomer is
        ///   always admitted (<paramref name="toEvict" /> null). At the cap, a full house only yields to a
        ///   STRICTLY stronger newcomer — the single weakest live grudge (by today's effective magnitude) is
        ///   named via <paramref name="toEvict" /> for the caller to remove; a newcomer no stronger than the
        ///   weakest is refused outright (it would just add noise, the exact anti-noise guardrail the
        ///   ROADMAP's per-NPC cap exists for).
        /// </summary>
        public static bool TryAdmit(IReadOnlyList<Grudge> liveGrudges, int newcomerMagnitude, int today, TraitFacts traits, out Grudge toEvict)
        {
            toEvict = null;

            if (liveGrudges == null) return true;

            List<Grudge> live = liveGrudges.Where(g => g != null && !g.IsResolved && EffectiveMagnitude(g, today, traits) > 0).ToList();

            if (live.Count < PerNpcCap) return true;

            Grudge weakest = live.OrderBy(g => EffectiveMagnitude(g, today, traits)).First();
            int weakestMagnitude = EffectiveMagnitude(weakest, today, traits);

            if (newcomerMagnitude <= weakestMagnitude) return false;

            toEvict = weakest;

            return true;
        }

        // ── Resolution ───────────────────────────────────────────────────────

        /// <summary>Tuning: the rigidity ceiling below which a resolution is considered SOFT (a lighter path, e.g. Amends/Gift over Deed/Time).</summary>
        public const int SoftRigidityCeiling = 35;

        /// <summary>True when a grudge can, in principle, be mended: not already resolved, and not a vendetta.</summary>
        public static bool IsResolvable(Grudge grudge)
            => grudge != null && !grudge.IsResolved && grudge.Rigidity < VendettaRigidity;

        /// <summary>
        ///   A sensible source-to-resolution-kind mapping, tiered by rigidity (soft below
        ///   <see cref="SoftRigidityCeiling" />, harder at/above it). <see cref="GrudgeSource.ExecutedKin" />
        ///   or any grudge that reached <see cref="VendettaRigidity" /> is always Unforgivable, whatever
        ///   its source.
        /// </summary>
        public static GrudgeResolutionKind ResolutionKindFor(GrudgeSource source, int rigidity)
        {
            if (source == GrudgeSource.ExecutedKin || rigidity >= VendettaRigidity) return GrudgeResolutionKind.Unforgivable;

            bool soft = rigidity < SoftRigidityCeiling;

            switch (source)
            {
                case GrudgeSource.Jealousy:          return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Time;
                case GrudgeSource.SchemeWarned:       return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                case GrudgeSource.SchemeAbetted:      return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                case GrudgeSource.FailedQuest:        return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                case GrudgeSource.Divorce:            return soft ? GrudgeResolutionKind.Gift : GrudgeResolutionKind.Amends;
                case GrudgeSource.BastardClaim:       return soft ? GrudgeResolutionKind.Gift : GrudgeResolutionKind.Deed;
                case GrudgeSource.BastardBlackmail:   return GrudgeResolutionKind.Gift;
                case GrudgeSource.DeliveredPrisoner:  return GrudgeResolutionKind.Deed;
                case GrudgeSource.CaptiveAbuse:       return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                case GrudgeSource.SwayExposed:        return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Time;
                case GrudgeSource.UnblessedMarriage:  return GrudgeResolutionKind.Time;
                case GrudgeSource.CaptorCruelty:      return GrudgeResolutionKind.Deed;
                case GrudgeSource.PrisonerRescued:    return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                case GrudgeSource.External:           return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                case GrudgeSource.FiefRevoked:        return GrudgeResolutionKind.Deed;
                case GrudgeSource.Expelled:           return GrudgeResolutionKind.Deed;
                // BrokenOath (R7-light's swear_oath, COUNCIL_ACTIONS.md's Partie 8) mirrors FailedQuest's own
                // treatment: both are a broken WORD rather than a seized possession or a severed bond, so a
                // still-soft grudge can be talked down (Amends), but once it has hardened only a real deed
                // (making good on the substance of the oath, or an equivalent gesture) will mend it.
                case GrudgeSource.BrokenOath:         return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                // PactBroken (the council's own non_aggression_pact, COUNCIL_ACTIONS.md's Partie 8) mirrors
                // BrokenOath's own treatment exactly: both are a broken WORD (a promise of restraint) rather than
                // a seized possession or a severed bond, so a still-soft grudge can be talked down (Amends), but
                // once it has hardened only a real deed will mend it.
                case GrudgeSource.PactBroken:         return soft ? GrudgeResolutionKind.Amends : GrudgeResolutionKind.Deed;
                default:                              return GrudgeResolutionKind.Amends;
            }
        }

        /// <summary>
        ///   Marks a grudge resolved on <paramref name="today" />. Refuses (returns false, leaves the
        ///   grudge untouched) when it is already resolved or is a vendetta (<see cref="IsResolvable" />
        ///   is false) — the caller should gate on <see cref="IsResolvable" /> before offering a
        ///   resolution path in the first place, but this guard keeps the mutation itself safe either way.
        /// </summary>
        public static bool Resolve(Grudge grudge, int today)
        {
            if (!IsResolvable(grudge)) return false;

            grudge.IsResolved = true;
            grudge.ResolvedDay = today;

            return true;
        }

        // ── Answered, and hardened ───────────────────────────────────────────

        /// <summary>The longest account kept on a grudge. One line of the holder's own summary, not a transcript.</summary>
        public const int MaxAccountChars = 200;

        /// <summary>True once the player has given their account of this grievance. See <see cref="Grudge.AnsweredDay" />.</summary>
        public static bool IsAnswered(Grudge grudge) => grudge != null && grudge.AnsweredDay >= 0;

        /// <summary>
        ///   Records that the player has given their account, and what it was. It changes nothing about how heavy
        ///   the grievance is, how rigid, or whether it can ever be mended: Gabriel's ruling (19/09/2026) is that a
        ///   vendetta stays unforgivable however well it is answered, and that an explanation is not amends. What it
        ///   changes is that the holder has HEARD it. A grievance answered is also, by definition, one the player
        ///   knows of. Refused for a resolved grudge and for an empty account: an answer that says nothing is not one.
        ///   A later account replaces an earlier one, since the latest thing the player said is what the holder has.
        /// </summary>
        public static bool RecordAnswer(Grudge grudge, string account, int today)
        {
            if (grudge == null || grudge.IsResolved) return false;

            string said = (account ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (said.Length == 0) return false;
            if (said.Length > MaxAccountChars) said = said.Substring(0, MaxAccountChars).TrimEnd() + "...";

            grudge.AnsweredDay = today;
            grudge.PlayerAccount = said;
            grudge.KnownToPlayer = true;

            return true;
        }

        /// <summary>
        ///   Whether this grievance still sends its holder out to confront the player. Once answered it does not:
        ///   the confrontation has happened (Gabriel's ruling, 19/09/2026). The holder stays as cold as the grudge
        ///   makes them at every meeting; they simply stop riding out to ask a question already answered.
        /// </summary>
        public static bool StillDrivesConfrontation(Grudge grudge)
            => grudge != null && !grudge.IsResolved && !IsAnswered(grudge);

        /// <summary>
        ///   Deepens a live grudge by <paramref name="amount" />, capped at 100. The counterpart
        ///   <see cref="Resolve" /> always had: a duel ducked over a grievance was meant to make it fester
        ///   (DuelOutcomePolicy says so), and the only reason it never did is that nothing could raise a grudge
        ///   once formed. Rigidity is left alone on purpose: a grievance made worse is not thereby made
        ///   unforgivable, and turning a mendable grudge into a vendetta is not a thing a single refusal should do.
        /// </summary>
        public static bool Harden(Grudge grudge, int amount)
        {
            if (grudge == null || grudge.IsResolved || amount <= 0) return false;

            grudge.Magnitude = Clamp(grudge.Magnitude + amount, 0, 100);

            return true;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static double PositivePart(double value) => value > 0 ? value : 0;

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;

            return value > max ? max : value;
        }
    }
}
