// Code written by Gabriel Mailhot, 31/10/2026.
// A rider bound for the player has to reach a player who keeps moving. kurushimi00 (Nexus, 31/10/2026): riders "all requiring
// me to keep my party immobile to let them actually deliver the letters". The rider was steered once a game hour to where
// the player then stood, and its arrival was read on that same hour, so anyone riding at more than 2 units an hour was
// never within reach of the check. Gabriel, 31/10/2026: "il faudrait qu'un cavalier courrier puisse rattrapper le joueur en
// mouvement. La lettre pourrait etre delivree au joueur lorsque le cavalier est assez pres de lui sans avoir a le toucher
// forcement." This decides when a rider is steered again and how fast it must ride to close the gap.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>What a rider's chase is read from, in map units.</summary>
    public sealed class RiderChaseFacts
    {
        /// <summary>How far the rider is from the player now.</summary>
        public float DistanceToPlayer { get; init; }

        /// <summary>How far the player has moved from the point the rider is riding to.</summary>
        public float TargetDrift { get; init; }
    }

    /// <summary>When a rider bound for the player is steered again, and the pace that lets it catch them. Pure.</summary>
    public static class ReplyRiderPolicy
    {
        /// <summary>TUNE: the drift always worth a new order: under this, the rider is already riding at the player.</summary>
        public const float MinTargetDrift = 0.5f;

        /// <summary>TUNE: the share of the distance the player may drift before the rider turns: a far rider needs no fine aim.</summary>
        public const float DriftShareOfDistance = 0.1f;

        /// <summary>TUNE: how much faster than the player a rider rides, in map units an hour, so the gap always closes.</summary>
        public const float RiderLead = 1f;

        /// <summary>True when the player has moved enough from the rider's point for a new order to matter.</summary>
        public static bool ShouldRefreshTarget(RiderChaseFacts facts)
        {
            if (facts == null) return false;

            return facts.TargetDrift >= Math.Max(MinTargetDrift, facts.DistanceToPlayer * DriftShareOfDistance);
        }

        /// <summary>
        ///   The pace a rider holds: never under the couriers' own <paramref name="floor" />, and faster than a player who
        ///   outrides it (a mounted party with the right perks passes the floor).
        /// </summary>
        public static float PaceFor(float playerSpeed, float floor) => Math.Max(floor, playerSpeed + RiderLead);
    }
}
