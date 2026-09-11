// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 4 (support_claimant, Logic + self-test slice): the DECISION behind SettleSupportClaimant, lifted
// out of the executor into a pure, unit-tested policy. The real vanilla "cast a vote in a live KingdomDecision"
// path is confirmed intractable without a fragile Harmony patch on private election internals (KingdomElection
// recomputes every AI clan's SupportWeight and skips the player's own supporter), so v1 stays: a seated vassal of
// the player's OWN kingdom throws their house's political weight behind the player as a real clan-influence
// transfer. This policy pins WHO may back the player and HOW MUCH, so the gates and the amount are tested here
// rather than eyeballed inside the engine-facing executor. The executor keeps ownership of the real
// ChangeClanInfluenceAction and the player-facing reason strings; this only decides.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>The verdict of <see cref="ClaimantSupportPolicy" />: which gate stopped the backing, or Applied with the amount.</summary>
    public enum ClaimantSupportOutcome
    {
        /// <summary>The backer is dead, not a lord, or a prisoner: no one to lend backing.</summary>
        NotFound,

        /// <summary>The backer has no clan of their own, or their clan IS the player's: there is no separate house to back with.</summary>
        BackerIsPlayerHouse,

        /// <summary>The backer's clan holds no seat in the player's own kingdom: a support pledge only makes sense within that court.</summary>
        NotInPlayerKingdom,

        /// <summary>The computed backing is zero (a cold vassal never appears to back the player) or more than the clan can spare.</summary>
        CannotAfford,

        /// <summary>The backing stands: move <see cref="ClaimantSupportResult.Amount" /> influence from the backer's clan to the player's.</summary>
        Applied
    }

    /// <summary>A support-claimant verdict: the outcome, and the influence amount (meaningful for Applied, and carried on CannotAfford for reporting).</summary>
    public readonly struct ClaimantSupportResult
    {
        public ClaimantSupportResult(ClaimantSupportOutcome outcome, int amount)
        {
            Outcome = outcome;
            Amount = amount;
        }

        public ClaimantSupportOutcome Outcome { get; }
        public int Amount { get; }
    }

    /// <summary>Pure decision for support_claimant: who may throw their house's weight behind the player, and how much.</summary>
    public static class ClaimantSupportPolicy
    {
        /// <summary>
        ///   Decides the backing, in the SAME gate order SettleSupportClaimant enforced inline (bridge is law):
        ///   the backer must be a live free lord; they must lead a clan of their own that is not the player's;
        ///   that clan must sit in the player's own kingdom; and the relation-scaled grant
        ///   (<see cref="Logic.CourtActionPolicy.InfluenceGrant" />) must be positive and affordable
        ///   (<see cref="Logic.CourtActionPolicy.CanAffordInfluenceGrant" />). A zero or negative relation yields a
        ///   zero grant, so a hostile vassal can never appear to back the player.
        /// </summary>
        /// <summary>
        ///   What is known about the lord being asked to back a claimant, read off the campaign by the caller.
        ///   Named rather than positional (2026-09-10): SIX booleans in a row, where "is of the player's own
        ///   house" and "is in the player's kingdom" are both just true and mean opposite things to the answer.
        /// </summary>
        public sealed class ClaimantBackerFacts
        {
            /// <summary>The backer is alive.</summary>
            public bool IsAlive { get; set; }

            /// <summary>The backer is a lord, which is who can spend influence at court.</summary>
            public bool IsLord { get; set; }

            /// <summary>The backer is somebody's prisoner.</summary>
            public bool IsPrisoner { get; set; }

            /// <summary>The backer has a clan of their own to spend from.</summary>
            public bool HasClan { get; set; }

            /// <summary>The backer belongs to the player's OWN house, whose influence is the player's to spend.</summary>
            public bool IsPlayerClan { get; set; }

            /// <summary>The backer sits in the player's kingdom, where the claim is actually being decided.</summary>
            public bool IsInPlayerKingdom { get; set; }

            /// <summary>The backer's relation with the player, which sizes what they would give.</summary>
            public int Relation { get; set; }

            /// <summary>The backer's clan influence, which sets what they can actually afford.</summary>
            public float Influence { get; set; }
        }

        /// <summary>Whether this lord will back the claim, and with how much. Unknown facts find no backer.</summary>
        public static ClaimantSupportResult Evaluate(ClaimantBackerFacts facts)
        {
            if (facts == null) return new ClaimantSupportResult(ClaimantSupportOutcome.NotFound, 0);

            bool backerAlive = facts.IsAlive;
            bool backerIsLord = facts.IsLord;
            bool backerIsPrisoner = facts.IsPrisoner;
            bool backerHasClan = facts.HasClan;
            bool backerIsPlayerClan = facts.IsPlayerClan;
            bool backerInPlayerKingdom = facts.IsInPlayerKingdom;
            int relation = facts.Relation;
            float backerInfluence = facts.Influence;

            if (!backerAlive || !backerIsLord || backerIsPrisoner)
                return new ClaimantSupportResult(ClaimantSupportOutcome.NotFound, 0);

            if (!backerHasClan || backerIsPlayerClan)
                return new ClaimantSupportResult(ClaimantSupportOutcome.BackerIsPlayerHouse, 0);

            if (!backerInPlayerKingdom)
                return new ClaimantSupportResult(ClaimantSupportOutcome.NotInPlayerKingdom, 0);

            int amount = CalradiaRemembers.Logic.CourtActionPolicy.InfluenceGrant(relation);

            if (amount <= 0 || !CalradiaRemembers.Logic.CourtActionPolicy.CanAffordInfluenceGrant(backerInfluence, amount))
                return new ClaimantSupportResult(ClaimantSupportOutcome.CannotAfford, amount);

            return new ClaimantSupportResult(ClaimantSupportOutcome.Applied, amount);
        }
    }
}
