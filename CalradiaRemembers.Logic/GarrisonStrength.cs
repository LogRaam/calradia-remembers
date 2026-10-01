// Code written by Gabriel Mailhot, 01/10/2026.
// How strong a garrison is, against the engine's own party-size limit for the place. These shares were written
// inline in EncounterContextBuilder.ReadGarrison (the seat-standing pack, 2026-09-12) and are moved here unchanged on
// 01/10/2026, when a second reader arrived: a governor asking the player to reinforce an under-strength garrison
// (fkasad, Nexus, 01/10/2026: "Works for garrison reinforcement too"). One home, so what a lord SAYS of his walls
// and what he ASKS for them can never disagree.
//
// fkasad's request also covers the hand-over: troops given in conversation go straight into the garrison, and the
// rules for how many may be asked live in ReinforceGarrisonPolicy below.

#region

using NpcMemoryService.Core.Knowledge;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>A garrison's strength band. Pure.</summary>
    public static class GarrisonStrength
    {
        /// <summary>Below this share of its limit a garrison is a skeleton: a handful of men on the walls.</summary>
        public const float SkeletonShare = 0.15f;

        /// <summary>Below this share it is under strength.</summary>
        public const float UnderStrengthShare = 0.50f;

        /// <summary>Below this share it is adequate; at or above, strong.</summary>
        public const float AdequateShare = 0.85f;

        /// <summary>The band for this many healthy men against this limit; Unknown when the limit is unreadable.</summary>
        public static GarrisonBand BandOf(int healthy, int limit)
        {
            if (limit <= 0) return GarrisonBand.Unknown;

            float share = healthy / (float) limit;

            if (share < SkeletonShare) return GarrisonBand.Skeleton;
            if (share < UnderStrengthShare) return GarrisonBand.UnderStrength;
            if (share < AdequateShare) return GarrisonBand.Adequate;

            return GarrisonBand.Strong;
        }
    }

    /// <summary>The rules of a reinforce_garrison task. Pure.</summary>
    public static class ReinforceGarrisonPolicy
    {
        /// <summary>The fewest men worth asking for: fewer is no reinforcement anyone would send a messenger about.</summary>
        public const int MinTroops = 5;

        /// <summary>The most a lord asks of one company of men.</summary>
        public const int MaxTroops = 60;

        /// <summary>Only a skeleton or an under-strength garrison is reinforced; an adequate one has no need.</summary>
        public static bool MayAsk(GarrisonBand band)
            => band == GarrisonBand.Skeleton || band == GarrisonBand.UnderStrength;

        /// <summary>
        ///   How many men to ask for: the proposed number within [MinTroops, MaxTroops] and never past the room left
        ///   on the walls; null when there is no room for even the fewest.
        /// </summary>
        public static int? TroopsToAsk(int proposed, int roomLeft)
        {
            int ceiling = roomLeft < MaxTroops ? roomLeft : MaxTroops;
            if (ceiling < MinTroops) return null;

            return proposed < MinTroops ? MinTroops : proposed > ceiling ? ceiling : proposed;
        }

        /// <summary>A hand-over is all the men asked or nothing: a short delivery is refused, never faked.</summary>
        public static bool CanHandOver(int playerHealthyRegulars, int required)
            => required > 0 && playerHealthyRegulars >= required;
    }
}
