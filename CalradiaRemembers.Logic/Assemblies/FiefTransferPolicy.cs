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
        ///   player, not merely a member or an independent clan's own head) AND the recipient's clan is a vassal
        ///   of THAT SAME kingdom (a crown fief passes only to a lord who owes this crown fealty, never a
        ///   foreign or enemy house) AND the named fief is a town or castle the player's OWN clan currently owns
        ///   (a genuine crown fief to give away, never a fief another vassal already holds, which would not be
        ///   the player's to give). The <paramref name="recipientInPlayerKingdom" /> fact is the v1.34.1 hotfix
        ///   invariant: without it a hallucinated or coaxed grant_fief token could hand a crown fief to an enemy.
        /// </summary>
        public static bool CanGrant(bool playerIsKingdomLeader, bool recipientInPlayerKingdom, bool fiefOwnedByPlayerClan)
            => playerIsKingdomLeader && recipientInPlayerKingdom && fiefOwnedByPlayerClan;

        /// <summary>
        ///   revoke_fief's gate: legal only when the player actually RULES a kingdom AND the target's clan is a
        ///   vassal of THAT SAME kingdom (the crown confiscates only from its own sworn lords, never seizes an
        ///   enemy's fief mid-war without a siege) AND the named fief is a town or castle the SEATED vassal's own
        ///   clan currently owns (a real fief to strip; naming a fief the vassal never held would confiscate
        ///   nothing, an empty punishment the table never actually paid). The <paramref name="targetInPlayerKingdom" />
        ///   fact is the v1.34.1 hotfix invariant: without it a hallucinated revoke_fief token could annex an
        ///   enemy holding by decree.
        /// </summary>
        public static bool CanRevoke(bool playerIsKingdomLeader, bool targetInPlayerKingdom, bool fiefOwnedBySpoliatedClan)
            => playerIsKingdomLeader && targetInPlayerKingdom && fiefOwnedBySpoliatedClan;

        /// <summary>
        ///   swap_fiefs' gate (COUNCIL_ACTIONS.md Partie 8, Kimi review): legal only when the player actually
        ///   RULES a kingdom AND their own clan currently owns the fief they are giving up AND the seated
        ///   vassal's own clan currently owns the fief offered in exchange - the literal AND of
        ///   <see cref="CanGrant" />'s and <see cref="CanRevoke" />'s own two conditions, since a swap is
        ///   mechanically a grant and a revoke that must both succeed or neither happens. This is deliberately
        ///   the SAME sovereignty bar grant/revoke already enforce, not a separate rule: a swap moves crown land
        ///   exactly like either half alone would, so it needs no laxer (or stricter) gate of its own.
        ///   CouncilLift.SettleSwapFiefs calls <see cref="TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction.ApplyByKingDecision" />
        ///   for EITHER side only after this whole gate (both halves) has already passed: the atomicity lives in
        ///   the executor's own validate-then-apply ordering, this method only says whether the trade is legal at
        ///   all.
        /// </summary>
        public static bool CanSwap(bool playerIsKingdomLeader, bool playerFiefOwnedByPlayerClan, bool vassalFiefOwnedBySeatedVassalClan)
            => playerIsKingdomLeader && playerFiefOwnedByPlayerClan && vassalFiefOwnedBySeatedVassalClan;

        /// <summary>
        ///   grant_fief's 1:1 conversation TEACHING gate (2026-08-05, the suzerain-command batch brought to BOTH the
        ///   council and 1:1 pipelines). It reduces the SAME eligibility the council's own offering proves before it
        ///   teaches grant_fief at the table to the three facts the mod's EncounterContextBuilder reads about the ONE
        ///   conversation NPC: the player must actually RULE a kingdom (only a suzerain grants a crown fief), the
        ///   conversation NPC must be a VASSAL of a separate house in that same kingdom (a fief is granted ACROSS
        ///   houses, never to one's own clan, and only to a lord who owes the crown fealty), and the player's own
        ///   clan must hold at least one grantable town or castle. Pure and total, mirroring
        ///   <see cref="GovernorAppointmentPolicy.CanOfferAppointment" />: the bridge re-checks every one of these
        ///   again at commit time (<c>CourtActionResolver.TryGrantFief</c>), so a true answer here only ever OFFERS a
        ///   teaching, it never seals a deed.
        /// </summary>
        public static bool CanOfferGrant(bool playerIsKingdomLeader, bool npcIsVassalInPlayerKingdom, bool hasGrantableFief)
            => playerIsKingdomLeader && npcIsVassalInPlayerKingdom && hasGrantableFief;

        /// <summary>
        ///   revoke_fief's 1:1 conversation TEACHING gate (2026-08-05, the suzerain-command batch). It reduces the
        ///   council's own revoke offering eligibility to the three facts the mod reads about the ONE conversation
        ///   NPC: the player must actually RULE a kingdom (only a suzerain revokes a crown fief), the conversation NPC
        ///   must be a VASSAL of a separate house in that same kingdom (a clan's own fiefs already belong to it, so
        ///   there is nothing to revoke from one's own clan), and that vassal's own clan must currently hold at least
        ///   one revocable town or castle. Pure and total, mirroring <see cref="CanOfferGrant" />; the bridge
        ///   re-checks every fact again at commit time (<c>CourtActionResolver.TryRevokeFief</c>), so a true answer
        ///   here only ever OFFERS a teaching.
        /// </summary>
        public static bool CanOfferRevoke(bool playerIsKingdomLeader, bool npcIsVassalInPlayerKingdom, bool npcHoldsRevocableFief)
            => playerIsKingdomLeader && npcIsVassalInPlayerKingdom && npcHoldsRevocableFief;
    }
}
