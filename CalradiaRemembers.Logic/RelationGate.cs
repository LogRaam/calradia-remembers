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
        public static int Resolve(
            int proposed,
            NpcProfile profile,
            NotableEventType? emittedEvent,
            double currentHour,
            bool npcIsCaptive,
            bool intimacyAllowed,
            RelationWarmth warmth = RelationWarmth.Normal)
        {
            if (proposed == 0 || profile == null) return 0;

            if (proposed > 0)
                return ResolvePositive(proposed, profile, emittedEvent, currentHour, warmth, intimacyAllowed);

            // ── Negative: not rate-limited; depth bounded by verifiable context ──
            // Warmth never softens negatives — insults and betrayals cost the same at every level.
            int floor = npcIsCaptive ? CaptiveFloor : ConversationalFloor;
            return proposed > floor ? proposed : floor;
        }

        #region private

        private static int ResolvePositive(
            int proposed,
            NpcProfile profile,
            NotableEventType? emittedEvent,
            double currentHour,
            RelationWarmth warmth,
            bool intimacyAllowed)
        {
            // C1: the intimacy bonus is a GUARDED door. It counts as significant (bigger cap, routine cooldown
            // bypassed) ONLY when the caller vouched the moment is legitimate AND its own dedicated cooldown has
            // elapsed. A tag that fails either test is demoted to a routine gain, so it can never farm +3 per turn.
            bool isSignificant = emittedEvent == NotableEventType.Intimacy
                                 && intimacyAllowed
                                 && (!profile.LastIntimacyGainHour.HasValue
                                     || currentHour - profile.LastIntimacyGainHour.Value >= IntimacyCooldownHours);

            // Routine gains are rate-limited (window depends on warmth); a significant
            // moment bypasses that routine cooldown entirely.
            if (!isSignificant
                && profile.LastRelationGainHour.HasValue
                && currentHour - profile.LastRelationGainHour.Value < CooldownHoursFor(warmth))
                return 0;

            int cap = isSignificant ? IntimacyCap : RoutineCapFor(warmth);
            int granted = proposed < cap ? proposed : cap;

            if (granted > 0)
            {
                profile.LastRelationGainHour = currentHour;
                if (isSignificant) profile.LastIntimacyGainHour = currentHour;
            }

            return granted;
        }

        #endregion
    }
}
