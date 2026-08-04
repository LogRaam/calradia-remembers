// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 2 (COUNCIL_ACTIONS.md): the pure gate behind grant_fief/revoke_fief,
// the first two GOVERNANCE kinds to gain a real executor. A fief belongs to the CLAN, not a hero (vanilla's own
// ChangeOwnerOfSettlementAction), so both are cross-clan by nature: grant_fief moves a crown fief FROM the
// player's own clan TO a seated WarCouncil vassal's, revoke_fief moves one the OTHER way. Gabriel's own
// condition (COUNCIL_ACTIONS.md): "on ne veut pas que ce soit facile" for a grant, and revoke is legal ONLY for
// the actual kingdom sovereign (corrected by the Kimi review from "chef de clan": a clan leader cannot revoke a
// fief from a clanmate, since a clan's own fiefs already belong to that same clan). Isolated here so the AND of
// sovereignty and ownership stays unit-tested, the same discipline GoldPledgePolicy's clamp already follows.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for grant_fief/revoke_fief: whether a fief transfer between the crown and a vassal is currently legal.</summary>
    public static class FiefTransferPolicy
    {
        /// <summary>
        ///   Tuning: the relation hit revoke_fief lands on the spoliated lord (COUNCIL_ACTIONS.md's Kimi review
        ///   suggests -40 to -60 with the clan head; this sits at the middle of that range). Named here, not as
        ///   a magic number in the lift executor, so the figure is documented and testable in one place.
        /// </summary>
        public const int RevokeRelationHit = -50;

        /// <summary>
        ///   Tuning: the grudge magnitude revoke_fief plants on the spoliated lord. Gabriel's own words
        ///   (COUNCIL_ACTIONS.md): "C'est une des pires punitions qui puisse être faite" - maximum intensity,
        ///   the same ceiling GrudgeCandidateFacts.Magnitude accepts before trait amplification.
        /// </summary>
        public const int RevokeGrudgeMagnitude = 100;

        /// <summary>
        ///   Tuning: the player's own clan influence cost of granting a fief away (Gabriel's own condition,
        ///   COUNCIL_ACTIONS.md: "on ne veut pas que ce soit facile"; the Kimi review proposes 100-300, caled on
        ///   vanilla's own KingdomDecision costs; this sits at the middle of that range). A FIXED, unconditional
        ///   cost, not an affordability gate: the grant is a sovereign's OWN prerogative, never blocked by a
        ///   thin treasury, but it still costs something real every time, unlike a free relation-only favour.
        /// </summary>
        public const int GrantInfluenceCost = 150;

        /// <summary>
        ///   grant_fief's gate: legal only when the player actually RULES a kingdom (Kingdom.Leader == the
        ///   player, not merely a member or an independent clan's own head) AND the named fief is a town or
        ///   castle the player's OWN clan currently owns (a genuine crown fief to give away, never a fief
        ///   another vassal already holds, which would not be the player's to give).
        /// </summary>
        public static bool CanGrant(bool playerIsKingdomLeader, bool fiefOwnedByPlayerClan)
            => playerIsKingdomLeader && fiefOwnedByPlayerClan;

        /// <summary>
        ///   revoke_fief's gate: legal only when the player actually RULES a kingdom AND the named fief is a
        ///   town or castle the SEATED vassal's own clan currently owns (a real fief to strip; naming a fief
        ///   the vassal never held would confiscate nothing, an empty punishment the table never actually paid).
        /// </summary>
        public static bool CanRevoke(bool playerIsKingdomLeader, bool fiefOwnedBySpoliatedClan)
            => playerIsKingdomLeader && fiefOwnedBySpoliatedClan;
    }
}
