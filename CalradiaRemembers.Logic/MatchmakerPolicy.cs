// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "Playing wingman for a guy trying hard to get a girl married?" Gabriel ruled the same
// day that the task ends in a REAL marriage, earned in two steps: the player wins the bride over (a real sway toward
// the suitor in her own conversation), then wins her family's consent, and the game weds them. No forced marriage on
// the family's word alone: she must hold the suitor a friend first (the game's own Friend line, VanillaRelation).
//
// The family weighs the match as it weighs one the player arranges for his own kin (ArrangedMatchPolicy, unchanged):
// enough regard for the one who asks, or a match that raises their daughter's standing. The house she would marry
// into is the SUITOR's, so his clan's tier is what is weighed, not the player's.

#region

using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What decides a match the player brokers between two other people.</summary>
    public sealed class MatchFacts
    {
        /// <summary>The bride's own relation with the suitor (the game's relation): she must hold him a friend.</summary>
        public int BrideRelationToSuitor { get; set; }

        /// <summary>The bride's house and the suitor's are different houses.</summary>
        public bool OfAnotherHouse { get; set; }

        /// <summary>The bride's family head's personal regard for the player who brokers it (CR's own ledger).</summary>
        public int FamilyRegardForThePlayer { get; set; }

        /// <summary>The suitor's clan tier: the house she would marry into.</summary>
        public int SuitorClanTier { get; set; }

        /// <summary>The bride's clan tier.</summary>
        public int BrideClanTier { get; set; }
    }

    /// <summary>The answer to a brokered match.</summary>
    public enum MatchVerdict
    {
        /// <summary>They may be wed.</summary>
        Wed,

        /// <summary>She does not yet hold him a friend: win her over first.</summary>
        NotWonOver,

        /// <summary>They are of one house.</summary>
        SameHouse,

        /// <summary>Her family will not give her: not enough regard for the broker, and no gain in standing.</summary>
        FamilyRefuses
    }

    /// <summary>The rules of a match the player brokers (arrange_match). Pure.</summary>
    public static class MatchmakerPolicy
    {
        /// <summary>Her heart first, then her family's consent, the latter weighed as for any arranged match.</summary>
        public static MatchVerdict Decide(MatchFacts f)
        {
            if (f == null) return MatchVerdict.FamilyRefuses;
            if (!new VanillaRelation(f.BrideRelationToSuitor).IsFriend) return MatchVerdict.NotWonOver;

            switch (ArrangedMatchPolicy.Decide(new ArrangedMatchFacts {
                       TargetIsOfAnotherClan = f.OfAnotherHouse, TargetFamilyRegard = f.FamilyRegardForThePlayer,
                       PlayerClanTier = f.SuitorClanTier, TargetClanTier = f.BrideClanTier
                    }))
            {
                case ArrangedMatchConsent.NotCrossClan: return MatchVerdict.SameHouse;
                case ArrangedMatchConsent.RefusedStanding: return MatchVerdict.FamilyRefuses;
                default: return MatchVerdict.Wed;
            }
        }
    }
}
