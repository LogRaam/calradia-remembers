// Code written by Gabriel Mailhot, 30/05/2026. Sprint 10.
// Moved into CalradiaRemembers.Logic 20/06/2026 (was Mapping/RelationGate.cs) and put under test:
// this is the mod's core anti-exploit rule — a regression would let players talk their way to max
// relation, the very failure it exists to prevent. It breaks silently, so it is protected here.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Single source of truth for how a LLM-proposed relation change is allowed
    ///   to move the player↔NPC relationship. Enforces the design pillars agreed for
    ///   Calradia Remembers:
    ///
    ///   - Trust is gained SLOWLY: routine warmth is capped at +1 and rate-limited
    ///     to one gain per <see cref="CooldownHours"/> window.
    ///   - A genuinely significant, self-evident moment (physical intimacy) grants a
    ///     larger gain (<see cref="IntimacyCap"/>) and BYPASSES the cooldown — a big
    ///     beat always counts.
    ///   - Trust is lost QUICKLY: negatives are not rate-limited and may run deep.
    ///     A normal conversation can sink to <see cref="ConversationalFloor"/>; only a
    ///     verifiable extreme (the NPC is the player's captive) unlocks the full
    ///     <see cref="CaptiveFloor"/>.
    ///
    ///   Magnitude is bounded by what the game can actually verify, never by the
    ///   LLM's unfettered proposal — this is what prevents talking one's way to max
    ///   relation, the core failure of comparable mods.
    /// </summary>
    public static class RelationGate
    {
        /// <summary>
        ///   Minimum in-game hours between two routine positive gains at the default
        ///   (Normal) warmth. Easier/Very-Easy shorten this — see <see cref="CooldownHoursFor"/>.
        /// </summary>
        public const double CooldownHours = 8.0;

        /// <summary>Cap for a routine "the NPC warms to you" gain.</summary>
        private const int DefaultPositiveCap = 1;

        /// <summary>
        ///   Cooldown window for the given warmth level. Only positive gains are throttled;
        ///   Very Easy removes the throttle entirely so most pleasant exchanges can land.
        /// </summary>
        public static double CooldownHoursFor(RelationWarmth warmth)
        {
            switch (warmth)
            {
                case RelationWarmth.VeryEasy: return 0.0;
                case RelationWarmth.Easier:   return 3.0;
                default:                      return CooldownHours; // Normal
            }
        }

        /// <summary>Routine positive cap for the given warmth level (Very Easy allows a +2 beat).</summary>
        public static int RoutineCapFor(RelationWarmth warmth)
            => warmth == RelationWarmth.VeryEasy ? 2 : DefaultPositiveCap;

        /// <summary>Cap for a self-evident significant moment (intimacy). Bypasses the ROUTINE cooldown.</summary>
        private const int IntimacyCap = 3;

        /// <summary>
        ///   The intimacy bonus's OWN cooldown (regard audit C1). It bypasses the routine throttle, but not this:
        ///   without a dedicated window a model tagging every reply "intimacy" farmed +3 per turn (+100 in ~34
        ///   turns), the very "talk your way to max relation" exploit the gate exists to stop.
        /// </summary>
        public const double IntimacyCooldownHours = 24.0;

        /// <summary>Deepest a single negative may go in ordinary conversation.</summary>
        private const int ConversationalFloor = -15;

        /// <summary>Deepest a negative may go when the NPC is the player's captive (cruelty).</summary>
        private const int CaptiveFloor = -100;

        /// <summary>
        ///   Resolves the LLM's proposed delta into the delta the game will actually
        ///   apply. On a granted positive gain, stamps
        ///   <see cref="NpcProfile.LastRelationGainHour"/> so the cooldown can be
        ///   enforced on subsequent turns. Returns 0 when the change is fully gated out.
        /// </summary>
        /// <param name="proposed">The raw delta the LLM emitted.</param>
        /// <param name="profile">The NPC profile (its cooldown stamp may be updated).</param>
        /// <param name="emittedEvent">The event type emitted this exchange, if any.</param>
        /// <param name="currentHour"><c>CampaignTime.Now.ToHours</c> at this moment.</param>
        /// <param name="npcIsCaptive">True if the NPC is currently the player's captive.</param>
        /// <param name="intimacyAllowed">
        ///   Regard audit C1: whether the caller has confirmed an intimacy [EVENT] is legitimate this turn (adult
        ///   content enabled, orientation-compatible, an active romance underway). Only then may an intimacy tag
        ///   earn the larger, cooldown-bypassing bonus; otherwise it is treated as a routine gain, so the tag can
        ///   never be spammed to farm regard. The prompt is advice, this gate is the law.
        /// </param>
        /// <summary>Default per-tier decay period (in-game DAYS) for the progressive-regard model; the MCM setting overrides it.</summary>
        public const double DefaultDecayDays = 7.0;

        public static int Resolve(
            int proposed,
            NpcProfile profile,
            NotableEventType? emittedEvent,
            double currentHour,
            bool npcIsCaptive,
            bool intimacyAllowed,
            RelationWarmth warmth = RelationWarmth.Normal,
            double decayDays = DefaultDecayDays)
        {
            if (proposed == 0 || profile == null) return 0;

            if (proposed > 0)
                return ResolvePositive(proposed, profile, emittedEvent, currentHour, intimacyAllowed, decayDays);

            // ── Negative: not rate-limited; depth bounded by verifiable context ──
            // Warmth never softens negatives — insults and betrayals cost the same at every level.
            int floor = npcIsCaptive ? CaptiveFloor : ConversationalFloor;
            return proposed > floor ? proposed : floor;
        }

        /// <summary>
        ///   Progressive-regard model (2026-09-05, replacing the flat one-gain-per-8h routine throttle): the moments
        ///   the NPC demands for the NEXT +1 rise by one each time one is granted (tier 0 -> 1 moment, tier 1 -> 2,
        ///   tier 2 -> 3, ...), and a tier decays back down after <paramref name="decayDays" /> in-game days. So a
        ///   long, genuinely warm conversation can earn several points (its moments accrue while game time is frozen),
        ///   a drive-by earns one then hits the wall, and the wall cannot be farmed by skipping 8 hours and returning
        ///   (the tier persists for days). Pure so the curve is unit-tested; the engine only supplies the clock, the
        ///   profile's carried tier/accrual, and the player's chosen decay period.
        ///   <para>Returns the moments-required for the next +1 at the given tier (tier + 1); exposed for tests/UI.</para>
        /// </summary>
        public static int MomentsRequiredForTier(int tier) => (tier < 0 ? 0 : tier) + 1;

        #region private

        private static int ResolvePositive(
            int proposed,
            NpcProfile profile,
            NotableEventType? emittedEvent,
            double currentHour,
            bool intimacyAllowed,
            double decayDays)
        {
            // C1: the intimacy bonus is a GUARDED door, unchanged and OUTSIDE the progressive model. It counts as a
            // significant moment (bigger cap, its own dedicated cooldown) ONLY when the caller vouched the moment is
            // legitimate AND its own cooldown has elapsed; a tag failing either test falls through to the routine
            // progressive path below, so it can never farm +3 per turn.
            bool isSignificant = emittedEvent == NotableEventType.Intimacy
                                 && intimacyAllowed
                                 && (!profile.LastIntimacyGainHour.HasValue
                                     || currentHour - profile.LastIntimacyGainHour.Value >= IntimacyCooldownHours);

            if (isSignificant)
            {
                int grantedIntimacy = proposed < IntimacyCap ? proposed : IntimacyCap;
                if (grantedIntimacy > 0)
                {
                    profile.LastIntimacyGainHour = currentHour;
                    profile.LastRelationGainHour = currentHour;
                }

                return grantedIntimacy;
            }

            // ── Routine progressive path ──────────────────────────────────────
            // First let the resistance cool for any time elapsed since it last moved (leaving and returning does not
            // reset it; only real time does). Then this exchange is one warm moment: grant a +1 only once the
            // accrued moments reach the current tier's requirement, and raising the tier makes the NEXT +1 dearer.
            DecayResistance(profile, currentHour, decayDays);

            profile.RegardWarmthAccrued += 1;
            if (profile.RegardWarmthAccrued < MomentsRequiredForTier(profile.RegardResistanceTier))
                return 0; // progress banked toward the next +1, but not enough of it yet

            profile.RegardWarmthAccrued = 0;
            profile.RegardResistanceTier += 1;      // deep bonds get harder to deepen
            profile.RegardResistanceStampHour = currentHour; // the new, higher tier starts its decay window now
            profile.LastRelationGainHour = currentHour;      // keep the legacy stamp fresh (diagnostics / save-compat)

            // The grant is a conservative +1 per cleared tier: the LLM's proposed magnitude is never trusted to
            // size a routine gain (the "talk your way to max" exploit the whole gate exists to prevent).
            return 1;
        }

        /// <summary>
        ///   Cools the resistance for the whole in-game days elapsed since it last moved: one tier down per
        ///   <paramref name="decayDays" /> days, carrying the remainder so a long absence walks it all the way back
        ///   down. Drops any partial accrual once a tier falls (a neglected bond does not keep half-progress).
        ///   <para>
        ///     A decayDays of 0 means the player turned the cooldown OFF, and the resistance relaxes completely
        ///     every time it is consulted: warmth never becomes dearer, so a warm exchange is worth its +1 today
        ///     as much as it was on the first day. It does NOT mean the tier freezes where it stands, which is
        ///     what this used to do (2026-09-10). That reading made the setting the exact opposite of what the
        ///     v2.5.1 changelog promised a player it would do, "set it to 0 to turn the cooldown off": frozen
        ///     resistance makes affection permanently HARDER, not free. Nobody was ever bitten by it only because
        ///     the MCM control clamped its own minimum to 1, so the value was unreachable. FloatingMammal asked
        ///     for the 0 he had been promised (2026-09-10), which is what sent anyone to read this line at all.
        ///   </para>
        ///   <para>
        ///     Off is not unlimited: an exchange still has to bank one warm moment for a tier-0 grant, so the
        ///     ceiling is +1 per warm exchange, which is exactly the flat behaviour the progressive model
        ///     replaced. That is a pacing choice a player may want, not an exploit they can run away with.
        ///   </para>
        /// </summary>
        private static void DecayResistance(NpcProfile profile, double currentHour, double decayDays)
        {
            if (profile.RegardResistanceTier <= 0 || !profile.RegardResistanceStampHour.HasValue)
            {
                if (profile.RegardResistanceTier <= 0)
                {
                    profile.RegardResistanceTier = 0;
                    profile.RegardResistanceStampHour = null; // nothing built up: measure the first tier from when it forms
                }

                return;
            }

            double windowHours = decayDays * 24.0;

            // The player turned the cooldown off: relax it entirely rather than freezing it. See this method's
            // own note for why the old "return" here was the opposite of the promise on the tin.
            if (windowHours <= 0.0)
            {
                profile.RegardResistanceTier = 0;
                profile.RegardWarmthAccrued = 0;
                profile.RegardResistanceStampHour = null;

                return;
            }

            double elapsed = currentHour - profile.RegardResistanceStampHour.Value;
            if (elapsed < windowHours) return;

            var tiersDown = (int) (elapsed / windowHours);
            int newTier = profile.RegardResistanceTier - tiersDown;
            if (newTier < 0) newTier = 0;

            profile.RegardResistanceTier = newTier;
            profile.RegardWarmthAccrued = 0;
            profile.RegardResistanceStampHour = newTier > 0
                ? profile.RegardResistanceStampHour.Value + tiersDown * windowHours
                : (double?) null;
        }

        #endregion
    }
}
