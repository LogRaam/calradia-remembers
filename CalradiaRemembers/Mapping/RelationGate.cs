// Code written by Gabriel Mailhot, 30/05/2026. Sprint 10.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Mapping
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
        /// <summary>Minimum in-game hours between two routine positive gains.</summary>
        public const double CooldownHours = 8.0;

        /// <summary>Cap for a routine "the NPC warms to you" gain.</summary>
        private const int DefaultPositiveCap = 1;

        /// <summary>Cap for a self-evident significant moment (intimacy). Bypasses cooldown.</summary>
        private const int IntimacyCap = 3;

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
        public static int Resolve(
            int proposed,
            NpcProfile profile,
            NotableEventType? emittedEvent,
            double currentHour,
            bool npcIsCaptive)
        {
            if (proposed == 0 || profile == null) return 0;

            if (proposed > 0)
                return ResolvePositive(proposed, profile, emittedEvent, currentHour);

            // ── Negative: not rate-limited; depth bounded by verifiable context ──
            int floor = npcIsCaptive ? CaptiveFloor : ConversationalFloor;
            return proposed > floor ? proposed : floor;
        }

        #region private

        private static int ResolvePositive(
            int proposed,
            NpcProfile profile,
            NotableEventType? emittedEvent,
            double currentHour)
        {
            bool isSignificant = emittedEvent == NotableEventType.Intimacy;

            // Routine gains are rate-limited; a significant moment bypasses the cooldown.
            if (!isSignificant
                && profile.LastRelationGainHour.HasValue
                && currentHour - profile.LastRelationGainHour.Value < CooldownHours)
                return 0;

            int cap = isSignificant ? IntimacyCap : DefaultPositiveCap;
            int granted = proposed < cap ? proposed : cap;

            if (granted > 0) profile.LastRelationGainHour = currentHour;
            return granted;
        }

        #endregion
    }
}
