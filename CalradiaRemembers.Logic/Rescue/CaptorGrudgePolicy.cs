// Code written by Gabriel Mailhot, 11/07/2026.

#region

#endregion

namespace CalradiaRemembers.Logic.Rescue
{
    /// <summary>
    ///   Decides whether the captor who held a now-freed prisoner DISCOVERS the player was responsible, and
    ///   how hard they resent it. A public act (storming a castle) is both more likely to be traced to the
    ///   player and something the player can expect the captor to resent openly; a quiet release is less
    ///   likely to be discovered and, if it is, stays a private grudge the player is not told about.
    /// </summary>
    public static class CaptorGrudgePolicy
    {
        // TUNE: first-pass values, not yet play-tested.
        public const double PublicDiscoveryChance = 0.60;

        // TUNE: first-pass values, not yet play-tested.
        public const double PrivateDiscoveryChance = 0.35;

        // TUNE: first-pass values, not yet play-tested.
        public const int PublicGrudgeMagnitude = 30;

        // TUNE: first-pass values, not yet play-tested.
        public const int PrivateGrudgeMagnitude = 20;

        /// <summary>The chance the captor traces the release back to the player, given whether the act was public.</summary>
        public static double DiscoveryChance(bool publicAction) => publicAction ? PublicDiscoveryChance : PrivateDiscoveryChance;

        /// <summary>
        ///   True when a uniform [0,1) <paramref name="roll01"/> supplied by the caller lands under the
        ///   applicable <see cref="DiscoveryChance"/>.
        /// </summary>
        public static bool Discovers(double roll01, bool publicAction) => roll01 < DiscoveryChance(publicAction);

        /// <summary>How hard the captor's grudge bites once they discover the player's hand in it.</summary>
        public static int GrudgeMagnitude(bool publicAction) => publicAction ? PublicGrudgeMagnitude : PrivateGrudgeMagnitude;

        /// <summary>True when the resulting grudge is known to the player (a public act is never a secret grudge).</summary>
        public static bool KnownToPlayer(bool publicAction) => publicAction;
    }
}
