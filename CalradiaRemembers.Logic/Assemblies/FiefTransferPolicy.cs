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
        ///   What makes a crown fief the player's to give, and this lord's to receive. Three booleans in a row,
        ///   and the middle one is the v1.34.1 hotfix invariant: without it a hallucinated grant_fief token
        ///   could hand a crown fief to an enemy house.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class GrantFacts
        {
           /// <summary>
           ///   The player rules a kingdom outright: Kingdom.Leader is the player, not merely a member,
           ///   and not an independent clan's own head.
           /// </summary>
           public bool PlayerIsKingdomLeader { get; set; }

           /// <summary>
           ///   The recipient's clan is a vassal of THAT SAME kingdom. A crown fief passes only to a lord who
           ///   owes this crown fealty, never to a foreign or enemy house.
           /// </summary>
           public bool RecipientInPlayerKingdom { get; set; }

           /// <summary>
           ///   The named town or castle is one the player's OWN clan holds right now. A fief another vassal
           ///   already holds was never the player's to give.
           /// </summary>
           public bool FiefOwnedByPlayerClan { get; set; }
        }

        /// <summary>
        ///   grant_fief's gate: legal only when the player actually RULES a kingdom (Kingdom.Leader == the
        ///   player, not merely a member or an independent clan's own head) AND the recipient's clan is a vassal
        ///   of THAT SAME kingdom (a crown fief passes only to a lord who owes this crown fealty, never a
        ///   foreign or enemy house) AND the named fief is a town or castle the player's OWN clan currently owns
        ///   (a genuine crown fief to give away, never a fief another vassal already holds, which would not be
        ///   the player's to give). The <c>RecipientInPlayerKingdom</c> fact is the v1.34.1 hotfix
        ///   invariant: without it a hallucinated or coaxed grant_fief token could hand a crown fief to an enemy.
        /// </summary>
        public static bool CanGrant(GrantFacts facts)
        {
           if (facts == null) return false;

           bool playerIsKingdomLeader = facts.PlayerIsKingdomLeader;
           bool recipientInPlayerKingdom = facts.RecipientInPlayerKingdom;
           bool fiefOwnedByPlayerClan = facts.FiefOwnedByPlayerClan;

           return playerIsKingdomLeader && recipientInPlayerKingdom && fiefOwnedByPlayerClan;
        }

        /// <summary>
        ///   What makes a fief the crown's to strip. Three booleans in a row, and the middle one is the same
        ///   v1.34.1 invariant grant carries: without it a hallucinated revoke_fief token could annex an
        ///   enemy holding by decree rather than by siege.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class RevokeFacts
        {
           /// <summary>
           ///   The player rules a kingdom outright: Kingdom.Leader is the player, not merely a member,
           ///   and not an independent clan's own head.
           /// </summary>
           public bool PlayerIsKingdomLeader { get; set; }

           /// <summary>
           ///   The target's clan is a vassal of THAT SAME kingdom. A crown confiscates only from its own sworn
           ///   lords; it does not seize an enemy's fief mid-war without a siege.
           /// </summary>
           public bool TargetInPlayerKingdom { get; set; }

           /// <summary>
           ///   The named fief is one the seated vassal's own clan holds. Naming a fief they never held would
           ///   confiscate nothing: an empty punishment the table never actually paid.
           /// </summary>
           public bool FiefOwnedBySpoliatedClan { get; set; }
        }

        /// <summary>
        ///   revoke_fief's gate: legal only when the player actually RULES a kingdom AND the target's clan is a
        ///   vassal of THAT SAME kingdom (the crown confiscates only from its own sworn lords, never seizes an
        ///   enemy's fief mid-war without a siege) AND the named fief is a town or castle the SEATED vassal's own
        ///   clan currently owns (a real fief to strip; naming a fief the vassal never held would confiscate
        ///   nothing, an empty punishment the table never actually paid). The <c>TargetInPlayerKingdom</c>
        ///   fact is the v1.34.1 hotfix invariant: without it a hallucinated revoke_fief token could annex an
        ///   enemy holding by decree.
        /// </summary>
        public static bool CanRevoke(RevokeFacts facts)
        {
           if (facts == null) return false;

           bool playerIsKingdomLeader = facts.PlayerIsKingdomLeader;
           bool targetInPlayerKingdom = facts.TargetInPlayerKingdom;
           bool fiefOwnedBySpoliatedClan = facts.FiefOwnedBySpoliatedClan;

           return playerIsKingdomLeader && targetInPlayerKingdom && fiefOwnedBySpoliatedClan;
        }

        /// <summary>
        ///   A swap is mechanically a grant and a revoke that must both succeed or neither happens, so it asks
        ///   the sovereignty question once and the ownership question twice, of two different clans. Those two
        ///   ownership booleans are the pair a swap cannot afford to have crossed.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class SwapFacts
        {
           /// <summary>
           ///   The player rules a kingdom outright: Kingdom.Leader is the player, not merely a member,
           ///   and not an independent clan's own head.
           /// </summary>
           public bool PlayerIsKingdomLeader { get; set; }

           /// <summary>The fief the player gives up is one their own clan holds.</summary>
           public bool PlayerFiefOwnedByPlayerClan { get; set; }

           /// <summary>The fief offered in exchange is one the seated vassal's clan holds.</summary>
           public bool VassalFiefOwnedBySeatedVassalClan { get; set; }
        }

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
        public static bool CanSwap(SwapFacts facts)
        {
           if (facts == null) return false;

           bool playerIsKingdomLeader = facts.PlayerIsKingdomLeader;
           bool playerFiefOwnedByPlayerClan = facts.PlayerFiefOwnedByPlayerClan;
           bool vassalFiefOwnedBySeatedVassalClan = facts.VassalFiefOwnedBySeatedVassalClan;

           return playerIsKingdomLeader && playerFiefOwnedByPlayerClan && vassalFiefOwnedBySeatedVassalClan;
        }

        /// <summary>
        ///   The same sovereignty question as <see cref="CanGrant" />, reduced to what can be read about the ONE
        ///   conversation NPC before the LLM is shown grant_fief at all. The bridge re-checks every fact at
        ///   commit, so a true answer here only ever OFFERS a teaching.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class GrantOfferFacts
        {
           /// <summary>
           ///   The player rules a kingdom outright: Kingdom.Leader is the player, not merely a member,
           ///   and not an independent clan's own head.
           /// </summary>
           public bool PlayerIsKingdomLeader { get; set; }

           /// <summary>
           ///   The NPC is a vassal of a SEPARATE house in that same kingdom. A fief is granted across houses,
           ///   never to one's own clan.
           /// </summary>
           public bool NpcIsVassalInPlayerKingdom { get; set; }

           /// <summary>The player's clan holds at least one town or castle to give.</summary>
           public bool HasGrantableFief { get; set; }
        }

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
        public static bool CanOfferGrant(GrantOfferFacts facts)
        {
           if (facts == null) return false;

           bool playerIsKingdomLeader = facts.PlayerIsKingdomLeader;
           bool npcIsVassalInPlayerKingdom = facts.NpcIsVassalInPlayerKingdom;
           bool hasGrantableFief = facts.HasGrantableFief;

           return playerIsKingdomLeader && npcIsVassalInPlayerKingdom && hasGrantableFief;
        }

        /// <summary>
        ///   The teaching gate for revoke_fief, mirroring <see cref="CanOfferGrant" />. Re-checked in full at
        ///   commit, so a true answer here only ever OFFERS.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class RevokeOfferFacts
        {
           /// <summary>
           ///   The player rules a kingdom outright: Kingdom.Leader is the player, not merely a member,
           ///   and not an independent clan's own head.
           /// </summary>
           public bool PlayerIsKingdomLeader { get; set; }

           /// <summary>
           ///   The NPC is a vassal of a separate house in that same kingdom. A clan's own fiefs already belong
           ///   to it, so there is nothing to revoke from oneself.
           /// </summary>
           public bool NpcIsVassalInPlayerKingdom { get; set; }

           /// <summary>That vassal's clan holds at least one town or castle.</summary>
           public bool NpcHoldsRevocableFief { get; set; }
        }

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
        public static bool CanOfferRevoke(RevokeOfferFacts facts)
        {
           if (facts == null) return false;

           bool playerIsKingdomLeader = facts.PlayerIsKingdomLeader;
           bool npcIsVassalInPlayerKingdom = facts.NpcIsVassalInPlayerKingdom;
           bool npcHoldsRevocableFief = facts.NpcHoldsRevocableFief;

           return playerIsKingdomLeader && npcIsVassalInPlayerKingdom && npcHoldsRevocableFief;
        }
    }
}
