// Code written by Gabriel Mailhot, 20/07/2026.
// Documents which catalogue motions a council may actually offer given the world. The rule is AND: every
// condition a motion requires must hold. The stake is the empty-promise guard, if a motion is offered while a
// condition is unmet (no vacant fief, no gold), the council could pass a resolution the bridge cannot apply,
// which the whole verification design exists to prevent.

#region

using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class ResolutionEligibilityPolicyTests
    {
        private static ResolutionWorldFacts Facts(bool inParty = false,
                                                  bool available = false,
                                                  bool vacantFief = false,
                                                  bool canAfford = false,
                                                  bool mission = false,
                                                  bool outOfParty = false,
                                                  bool canDeclareWar = false,
                                                  bool canMakePeace = false,
                                                  bool memberHasGold = false,
                                                  bool memberCanGrantInfluence = false,
                                                  bool playerIsKingdomLeader = false,
                                                  bool playerHasGiveableFief = false,
                                                  bool seatedVassalOwnsFief = false,
                                                  bool anySeatedCompanionExpellable = false,
                                                  bool anySeatedMemberCanPledgeAgainst = false,
                                                  bool anySeatedAllyHasMarriageableKin = false,
                                                  bool anySeatedTributePayerAvailable = false)
            => new ResolutionWorldFacts(inParty, available, vacantFief, canAfford, mission, outOfParty, canDeclareWar, canMakePeace, memberHasGold, memberCanGrantInfluence, playerIsKingdomLeader, playerHasGiveableFief, seatedVassalOwnsFief, anySeatedCompanionExpellable, anySeatedMemberCanPledgeAgainst, anySeatedAllyHasMarriageableKin, anySeatedTributePayerAvailable);

        // A single-condition motion: assigning a party role needs only that the companion travels with the
        // player. Present it when he does, withhold it when he does not (a governor away in a town has no party
        // seat to fill).
        [Test]
        public void GIVEN_assign_party_role_WHEN_the_target_is_or_is_not_in_the_party_THEN_offer_follows()
        {
            ResolutionDefinition assign = ResolutionCatalog.Get(ResolutionKind.AssignPartyRole);

            ResolutionEligibilityPolicy.CanOffer(assign, Facts(inParty: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(assign, Facts(inParty: false)).Should().BeFalse();
        }

        // The fourth executable motion: dispatch_mission REUSES assign_party_role's own TargetInPlayerParty fact
        // (ResolutionCatalog's DispatchOnMission entry docs the reasoning), so a seated companion who travels
        // with the player may be sent out the same as they may be assigned a role, and withheld the same way
        // when nobody present rides in the party.
        [Test]
        public void GIVEN_dispatch_mission_WHEN_the_target_is_or_is_not_in_the_party_THEN_offer_follows()
        {
            ResolutionDefinition dispatch = ResolutionCatalog.Get(ResolutionKind.DispatchOnMission);

            ResolutionEligibilityPolicy.CanOffer(dispatch, Facts(inParty: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(dispatch, Facts(inParty: false)).Should().BeFalse();
        }

        // Bug d, the symmetric inverse: rejoining the party needs the OPPOSITE fact of assign_party_role, a
        // seated companion who is NOT in the party. Offering it while nobody is out (or withholding it while
        // someone is) would either promise a rejoin the lift finds nothing to settle, or hide the one deed that
        // closes the "companion pledges to rejoin, nothing happens" report.
        [Test]
        public void GIVEN_rejoin_party_WHEN_the_target_is_or_is_not_out_of_the_party_THEN_offer_follows()
        {
            ResolutionDefinition rejoin = ResolutionCatalog.Get(ResolutionKind.RejoinParty);

            ResolutionEligibilityPolicy.CanOffer(rejoin, Facts(outOfParty: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(rejoin, Facts(outOfParty: false)).Should().BeFalse();
        }

        // A two-condition motion is AND, not OR: appointing a governor needs BOTH an available companion AND a
        // vacant fief. Missing either must withhold it, so the council never resolves to seat a governor in a
        // fief that does not exist or send a companion who cannot go.
        [Test]
        public void GIVEN_appoint_governor_WHEN_either_condition_is_missing_THEN_it_is_not_offered()
        {
            ResolutionDefinition appoint = ResolutionCatalog.Get(ResolutionKind.AppointGovernor);

            ResolutionEligibilityPolicy.CanOffer(appoint, Facts(available: true, vacantFief: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(appoint, Facts(available: true, vacantFief: false)).Should().BeFalse();
            ResolutionEligibilityPolicy.CanOffer(appoint, Facts(available: false, vacantFief: true)).Should().BeFalse();
        }

        // The affordability gate: a stipend cannot be moved unless the player can fund it, so the council never
        // promises gold the treasury does not hold.
        [Test]
        public void GIVEN_grant_stipend_WHEN_the_player_cannot_afford_it_THEN_it_is_not_offered()
        {
            ResolutionDefinition stipend = ResolutionCatalog.Get(ResolutionKind.GrantStipend);

            ResolutionEligibilityPolicy.CanOffer(stipend, Facts(canAfford: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(stipend, Facts(canAfford: false)).Should().BeFalse();
        }

        // The whole-council query composes scope and eligibility: with a companion in the party and gold to
        // spare but no fief, the motions those facts satisfy are offered (assign_party_role and dispatch_mission
        // share the same in-party fact, see ResolutionCatalog's DispatchOnMission entry), and the out-of-scope
        // GrantFief never appears whatever the facts.
        [Test]
        public void GIVEN_a_companions_council_and_partial_facts_WHEN_offered_THEN_only_the_satisfied_motions_appear()
        {
            ResolutionWorldFacts facts = Facts(inParty: true, available: true, canAfford: true,
                                               vacantFief: false, mission: false);

            var offered = ResolutionEligibilityPolicy.OffersFor(CouncilScope.Companions, facts)
                .Select(d => d.Kind).ToList();

            offered.Should().BeEquivalentTo(new[]
            {
                ResolutionKind.AssignPartyRole, ResolutionKind.DispatchOnMission, ResolutionKind.GrantStipend
            });
            offered.Should().NotContain(ResolutionKind.AppointGovernor); // no vacant fief
        }

        // The prompt-offering feature (2026-08-03): appoint_governor must be TAUGHT to the LLM only when both
        // its conditions hold, or the model promises a governorship the lift then has no fief/companion to
        // grant (CouncilLift.SettleAppointGovernor's own re-check would fail it, wasting the player's turn on
        // a pledge that was never possible).
        [Test]
        public void GIVEN_a_vacant_fief_and_an_available_member_WHEN_offered_keys_are_asked_THEN_appoint_governor_is_included()
        {
            ResolutionWorldFacts facts = Facts(available: true, vacantFief: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts)
                .Should().Contain("appoint_governor");
        }

        // Missing either half of the requirement must withhold the key from the OFFERED set, the same AND the
        // definition-level CanOffer check already enforces; this test pins that the scope-level aggregation
        // (what the prompt actually reads) does not silently relax that AND.
        [Test]
        public void GIVEN_no_vacant_fief_OR_no_available_member_WHEN_offered_keys_are_asked_THEN_appoint_governor_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(available: true, vacantFief: false))
                .Should().NotContain("appoint_governor");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(available: false, vacantFief: true))
                .Should().NotContain("appoint_governor");
        }

        // The offering wire (bug d): rejoin_party must be TAUGHT to the LLM only when a seated companion is
        // actually out of the party, or the model promises a rejoin CouncilLift.SettleRejoinParty then finds
        // nobody eligible to bring back (wasting the player's turn on a pledge that was never possible).
        [Test]
        public void GIVEN_a_seated_companion_out_of_the_party_WHEN_offered_keys_are_asked_THEN_rejoin_party_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(outOfParty: true))
                .Should().Contain("rejoin_party");
        }

        // Absent that fact (every seated companion already rides along, or none is seated) the key must be
        // withheld, or the LLM could promise a rejoin for someone with nothing to rejoin from.
        [Test]
        public void GIVEN_no_seated_companion_out_of_the_party_WHEN_offered_keys_are_asked_THEN_rejoin_party_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(outOfParty: false))
                .Should().NotContain("rejoin_party");
        }

        // The offering wire, mirroring assign_party_role's own: dispatch_mission must be TAUGHT to the LLM only
        // when a seated companion actually travels with the player, or the model promises an errand
        // CouncilLift.SettleDispatchMission then finds nobody eligible to send on.
        [Test]
        public void GIVEN_a_seated_member_in_the_party_WHEN_offered_keys_are_asked_THEN_dispatch_mission_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(inParty: true))
                .Should().Contain("dispatch_mission");
        }

        // Absent that fact (no seated companion rides with the player) the key must be withheld, or the LLM
        // could promise a dispatch for a companion who is not there to send.
        [Test]
        public void GIVEN_no_seated_member_in_the_party_WHEN_offered_keys_are_asked_THEN_dispatch_mission_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(inParty: false))
                .Should().NotContain("dispatch_mission");
        }

        // The offering wire, the fifth executable motion and the first that touches persisted save state:
        // grant_stipend must be TAUGHT to the LLM only when the player can afford at least the minimum tranche,
        // or the model promises a purse CouncilLift.SettleGrantStipend (CouncilStipendBehavior.TryStart) then
        // finds the player's coffers cannot fund at all.
        [Test]
        public void GIVEN_the_player_can_afford_the_floor_tranche_WHEN_offered_keys_are_asked_THEN_grant_stipend_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(canAfford: true))
                .Should().Contain("grant_stipend");
        }

        // Absent that fact (the player cannot even cover the cheapest possible tranche) the key must be withheld,
        // or the LLM could promise a stipend the lift has already proven the treasury cannot fund at all.
        [Test]
        public void GIVEN_the_player_cannot_afford_the_floor_tranche_WHEN_offered_keys_are_asked_THEN_grant_stipend_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(canAfford: false))
                .Should().NotContain("grant_stipend");
        }

        // Quest carries no ResolutionDefinition (it is universal, not a scoped catalogue motion), so it must
        // never be filtered by facts: a family council with zero motions of its own must still surface "quest",
        // or that scope could seat and debate yet never record a single deed.
        [Test]
        public void GIVEN_any_scope_and_empty_facts_WHEN_offered_keys_are_asked_THEN_quest_is_always_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, Facts())
                .Should().Contain("quest");
        }

        // The WarCouncil's own gate: declare_war must be TAUGHT to the LLM only when the mod's offering has
        // proven the player actually leads a faction that could declare a new war, or the model promises a
        // decision CouncilLift.SettleDeclareWar's own guard would refuse outright (bridge is law).
        [Test]
        public void GIVEN_the_player_can_declare_war_WHEN_offered_keys_are_asked_THEN_declare_war_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, Facts(canDeclareWar: true))
                .Should().Contain("declare_war");
        }

        // Absent that fact (the player leads no faction able to declare war, e.g. not the kingdom's ruler) the
        // key must be withheld, or the table could offer a war declaration nobody present has the standing to make.
        [Test]
        public void GIVEN_the_player_cannot_declare_war_WHEN_offered_keys_are_asked_THEN_declare_war_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, Facts(canDeclareWar: false))
                .Should().NotContain("declare_war");
        }

        // The Parley's own gate, declare_war's symmetric counterpart: make_peace must be TAUGHT to the LLM only
        // when the mod's offering has proven the player leads their own faction AND it is at war with the
        // parley's target, or the model promises a peace CouncilLift.SettleMakePeace's own guard would refuse.
        [Test]
        public void GIVEN_the_player_can_make_peace_WHEN_offered_keys_are_asked_THEN_make_peace_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, Facts(canMakePeace: true))
                .Should().Contain("make_peace");
        }

        // Absent that fact (the player does not lead their own faction, or is somehow not at war with the
        // parley's target) the key must be withheld, or the table could offer a peace nobody present has the
        // standing to make.
        [Test]
        public void GIVEN_the_player_cannot_make_peace_WHEN_offered_keys_are_asked_THEN_make_peace_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, Facts(canMakePeace: false))
                .Should().NotContain("make_peace");
        }

        // Partie 1 (bringing the existing 1:1 resource verbs to the council): give_gold needs only that a seated
        // member holds gold to give. Present it when one does, withhold it when nobody at the table has any coin
        // of their own to pledge.
        [Test]
        public void GIVEN_give_gold_WHEN_a_member_does_or_does_not_hold_gold_THEN_offer_follows()
        {
            ResolutionDefinition giveGold = ResolutionCatalog.Get(ResolutionKind.GiveGold);

            ResolutionEligibilityPolicy.CanOffer(giveGold, Facts(memberHasGold: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(giveGold, Facts(memberHasGold: false)).Should().BeFalse();
        }

        // The offering wire: give_gold must be TAUGHT to the LLM only when a seated member (ClanLords) actually
        // holds gold, or the model promises a pledge CouncilLift.SettleGiveGold then finds nobody present could
        // actually pay (GoldPledgePolicy.ClampPayable would clamp it to zero and the lift would refuse it).
        [Test]
        public void GIVEN_a_seated_clan_lord_holds_gold_WHEN_offered_keys_are_asked_THEN_give_gold_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, Facts(memberHasGold: true))
                .Should().Contain("give_gold");
        }

        // The WarCouncil gets the identical gate: a war council with no moneyed lord seated must not see
        // give_gold offered either.
        [Test]
        public void GIVEN_no_seated_war_council_member_holds_gold_WHEN_offered_keys_are_asked_THEN_give_gold_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, Facts(memberHasGold: false))
                .Should().NotContain("give_gold");
        }

        // The empty-promise guard, the other direction: Companions and Family carry no GiveGold catalogue entry
        // at all, so the key must never surface there even when the fact is (nonsensically) true, proving the
        // scope gate, not merely the fact, is what keeps a hired hand or dynastic kin from pledging a lord's purse.
        [Test]
        public void GIVEN_a_companions_or_family_council_WHEN_offered_keys_are_asked_THEN_give_gold_is_never_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, Facts(memberHasGold: true))
                .Should().NotContain("give_gold");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, Facts(memberHasGold: true))
                .Should().NotContain("give_gold");
        }

        // Partie 1 (bringing the existing 1:1 resource verbs to the council): give_influence needs only that a
        // seated ally's own clan could grant it. Present it when one can, withhold it when nobody at the table
        // has a clan able to spare any influence.
        [Test]
        public void GIVEN_give_influence_WHEN_a_member_can_or_cannot_grant_influence_THEN_offer_follows()
        {
            ResolutionDefinition giveInfluence = ResolutionCatalog.Get(ResolutionKind.GiveInfluence);

            ResolutionEligibilityPolicy.CanOffer(giveInfluence, Facts(memberCanGrantInfluence: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(giveInfluence, Facts(memberCanGrantInfluence: false)).Should().BeFalse();
        }

        // The offering wire: give_influence must be TAUGHT to the LLM only when a seated ally's clan actually
        // could grant it, or the model promises a pledge CouncilLift.SettleGiveInfluence then finds the giver's
        // own clan cannot afford (reserve buffer) or is not owed the standing (relation floor) to spare.
        [Test]
        public void GIVEN_a_seated_ally_can_grant_influence_WHEN_offered_keys_are_asked_THEN_give_influence_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, Facts(memberCanGrantInfluence: true))
                .Should().Contain("give_influence");
        }

        // Absent that fact (no seated ally's clan could currently spare any influence) the key must be
        // withheld, or the table could offer a pledge nobody present could actually keep.
        [Test]
        public void GIVEN_no_seated_ally_can_grant_influence_WHEN_offered_keys_are_asked_THEN_give_influence_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, Facts(memberCanGrantInfluence: false))
                .Should().NotContain("give_influence");
        }

        // The empty-promise guard, the other direction: ClanLords carries no GiveInfluence catalogue entry at
        // all (within the player's own clan the transfer is clan-to-itself, the exact vacant-promise case
        // COUNCIL_ACTIONS.md's Kimi review flags), so the key must never surface there even when the fact is
        // (nonsensically) true, proving the scope gate, not merely the fact, keeps the council from teaching a
        // pledge that would lift to nothing.
        [Test]
        public void GIVEN_a_clan_lords_council_WHEN_offered_keys_are_asked_THEN_give_influence_is_never_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, Facts(memberCanGrantInfluence: true))
                .Should().NotContain("give_influence");
        }

        // Partie 2 (COUNCIL_ACTIONS.md): grant_fief needs BOTH the player's own sovereignty AND a giveable
        // crown fief. Missing either must withhold it, so the table never resolves to grant a fief the player
        // has no standing to give away, or a fief that does not exist to give.
        [Test]
        public void GIVEN_grant_fief_WHEN_either_condition_is_missing_THEN_it_is_not_offered()
        {
            ResolutionDefinition grantFief = ResolutionCatalog.Get(ResolutionKind.GrantFief);

            ResolutionEligibilityPolicy.CanOffer(grantFief, Facts(playerIsKingdomLeader: true, playerHasGiveableFief: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(grantFief, Facts(playerIsKingdomLeader: true, playerHasGiveableFief: false)).Should().BeFalse();
            ResolutionEligibilityPolicy.CanOffer(grantFief, Facts(playerIsKingdomLeader: false, playerHasGiveableFief: true)).Should().BeFalse();
        }

        // The offering wire: grant_fief must be TAUGHT to the LLM only when both conditions hold, or the model
        // promises a fief CouncilLift.SettleGrantFief's own re-check (FiefTransferPolicy.CanGrant) would refuse
        // outright (bridge is law).
        [Test]
        public void GIVEN_the_player_can_grant_a_fief_WHEN_offered_keys_are_asked_THEN_grant_fief_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil,
                    Facts(playerIsKingdomLeader: true, playerHasGiveableFief: true))
                .Should().Contain("grant_fief");
        }

        // Absent either fact (no kingdom to rule, or no crown fief to give) the key must be withheld, or the
        // table could offer a grant the lift has already proven the player has no standing or nothing to give.
        [Test]
        public void GIVEN_the_player_cannot_grant_a_fief_WHEN_offered_keys_are_asked_THEN_grant_fief_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil,
                    Facts(playerIsKingdomLeader: false, playerHasGiveableFief: true))
                .Should().NotContain("grant_fief");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil,
                    Facts(playerIsKingdomLeader: true, playerHasGiveableFief: false))
                .Should().NotContain("grant_fief");
        }

        // The empty-promise guard, the other direction: ClanLords, Companions and Family carry no GrantFief
        // catalogue entry at all (a fief is clan-owned, so a transfer within the player's own clan or a hired
        // hand's council is mechanically vacant), so the key must never surface there even when both facts are
        // (nonsensically) true.
        [Test]
        public void GIVEN_a_clan_lords_companions_or_family_council_WHEN_offered_keys_are_asked_THEN_grant_fief_is_never_included()
        {
            ResolutionWorldFacts facts = Facts(playerIsKingdomLeader: true, playerHasGiveableFief: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts).Should().NotContain("grant_fief");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts).Should().NotContain("grant_fief");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, facts).Should().NotContain("grant_fief");
        }

        // Partie 2 (COUNCIL_ACTIONS.md): revoke_fief needs BOTH the player's own sovereignty AND a seated
        // vassal actually holding a fief to strip. Missing either must withhold it, so the table never resolves
        // to revoke a fief nobody present holds, or that the player has no standing to seize.
        [Test]
        public void GIVEN_revoke_fief_WHEN_either_condition_is_missing_THEN_it_is_not_offered()
        {
            ResolutionDefinition revokeFief = ResolutionCatalog.Get(ResolutionKind.RevokeFief);

            ResolutionEligibilityPolicy.CanOffer(revokeFief, Facts(playerIsKingdomLeader: true, seatedVassalOwnsFief: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(revokeFief, Facts(playerIsKingdomLeader: true, seatedVassalOwnsFief: false)).Should().BeFalse();
            ResolutionEligibilityPolicy.CanOffer(revokeFief, Facts(playerIsKingdomLeader: false, seatedVassalOwnsFief: true)).Should().BeFalse();
        }

        // The offering wire: revoke_fief must be TAUGHT to the LLM only when both conditions hold, or the model
        // threatens a punishment CouncilLift.SettleRevokeFief's own re-check (FiefTransferPolicy.CanRevoke)
        // would refuse outright, a hollow threat the table never actually carries out.
        [Test]
        public void GIVEN_the_player_can_revoke_a_fief_WHEN_offered_keys_are_asked_THEN_revoke_fief_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil,
                    Facts(playerIsKingdomLeader: true, seatedVassalOwnsFief: true))
                .Should().Contain("revoke_fief");
        }

        // Absent either fact (no kingdom to rule, or no seated vassal holds a fief at all) the key must be
        // withheld, or the table could threaten a confiscation the lift has already proven impossible.
        [Test]
        public void GIVEN_the_player_cannot_revoke_a_fief_WHEN_offered_keys_are_asked_THEN_revoke_fief_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil,
                    Facts(playerIsKingdomLeader: false, seatedVassalOwnsFief: true))
                .Should().NotContain("revoke_fief");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil,
                    Facts(playerIsKingdomLeader: true, seatedVassalOwnsFief: false))
                .Should().NotContain("revoke_fief");
        }

        // The empty-promise guard, the other direction: ClanLords, Companions and Family carry no RevokeFief
        // catalogue entry at all, so the key must never surface there even when both facts are (nonsensically) true.
        [Test]
        public void GIVEN_a_clan_lords_companions_or_family_council_WHEN_offered_keys_are_asked_THEN_revoke_fief_is_never_included()
        {
            ResolutionWorldFacts facts = Facts(playerIsKingdomLeader: true, seatedVassalOwnsFief: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts).Should().NotContain("revoke_fief");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts).Should().NotContain("revoke_fief");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, facts).Should().NotContain("revoke_fief");
        }

        // Partie 8 (COUNCIL_ACTIONS.md's Kimi review): expel_from_clan needs the single AnySeatedCompanionExpellable
        // fact (itself already the AND of player-leads-clan / target-is-companion / not-spouse / alive, see
        // ExpulsionPolicy.CanExpel). Missing it must withhold the motion entirely.
        [Test]
        public void GIVEN_expel_from_clan_WHEN_no_seated_companion_is_expellable_THEN_it_is_not_offered()
        {
            ResolutionDefinition expel = ResolutionCatalog.Get(ResolutionKind.ExpelFromClan);

            ResolutionEligibilityPolicy.CanOffer(expel, Facts(anySeatedCompanionExpellable: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(expel, Facts(anySeatedCompanionExpellable: false)).Should().BeFalse();
        }

        // The offering wire: expel_from_clan must be TAUGHT to the LLM only when the fact holds, or the model
        // threatens a permanent break CouncilLift.SettleExpelFromClan's own re-check (ExpulsionPolicy.CanExpel)
        // would refuse outright, a hollow threat that would still cost the table nothing to voice.
        [Test]
        public void GIVEN_a_companion_is_expellable_WHEN_offered_keys_are_asked_THEN_expel_from_clan_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions,
                    Facts(anySeatedCompanionExpellable: true))
                .Should().Contain("expel_from_clan");
        }

        // Absent the fact (no leader, no companion, only the spouse, or nobody alive to expel) the key must be
        // withheld, or the table could threaten an expulsion the lift has already proven impossible.
        [Test]
        public void GIVEN_no_companion_is_expellable_WHEN_offered_keys_are_asked_THEN_expel_from_clan_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions,
                    Facts(anySeatedCompanionExpellable: false))
                .Should().NotContain("expel_from_clan");
        }

        // The empty-promise guard, the other direction: Family, ClanLords, WarCouncil and Parley carry no
        // ExpelFromClan catalogue entry at all (this build's own scoping call, see ExpulsionPolicy's own
        // header), so the key must never surface there even when the fact is (nonsensically) true.
        [Test]
        public void GIVEN_a_family_clan_lords_war_council_or_parley_WHEN_offered_keys_are_asked_THEN_expel_from_clan_is_never_included()
        {
            ResolutionWorldFacts facts = Facts(anySeatedCompanionExpellable: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, facts).Should().NotContain("expel_from_clan");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts).Should().NotContain("expel_from_clan");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, facts).Should().NotContain("expel_from_clan");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, facts).Should().NotContain("expel_from_clan");
        }

        // Partie 1 (COUNCIL_ACTIONS.md's schemes block): pledge_against needs only the single, approximated
        // AnySeatedMemberCanPledgeAgainst fact (no pledge already outstanding on some seated member). Missing it
        // must withhold the motion entirely, or the table could offer a scheme the lift has already proven
        // nobody present is free to launch.
        [Test]
        public void GIVEN_pledge_against_WHEN_no_seated_member_can_pledge_THEN_it_is_not_offered()
        {
            ResolutionDefinition pledgeAgainst = ResolutionCatalog.Get(ResolutionKind.PledgeAgainst);

            ResolutionEligibilityPolicy.CanOffer(pledgeAgainst, Facts(anySeatedMemberCanPledgeAgainst: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(pledgeAgainst, Facts(anySeatedMemberCanPledgeAgainst: false)).Should().BeFalse();
        }

        // The offering wire: pledge_against must be TAUGHT to the LLM only when the fact holds, or the model
        // proposes a scheme CouncilLift.SettlePledgeAgainst's own re-check (CommitmentBehavior.HasActive,
        // CourtActionResolver.ResolvePledgeTarget) would refuse outright, a hollow promise the table never
        // actually carries out.
        [Test]
        public void GIVEN_a_seated_member_can_pledge_against_a_rival_WHEN_offered_keys_are_asked_THEN_pledge_against_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, Facts(anySeatedMemberCanPledgeAgainst: true))
                .Should().Contain("pledge_against");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, Facts(anySeatedMemberCanPledgeAgainst: true))
                .Should().Contain("pledge_against");
        }

        // Absent that fact (every seated member already carries a pledge, or none is seated) the key must be
        // withheld, or the table could offer a scheme the lift has already proven impossible.
        [Test]
        public void GIVEN_no_seated_member_can_pledge_against_a_rival_WHEN_offered_keys_are_asked_THEN_pledge_against_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, Facts(anySeatedMemberCanPledgeAgainst: false))
                .Should().NotContain("pledge_against");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, Facts(anySeatedMemberCanPledgeAgainst: false))
                .Should().NotContain("pledge_against");
        }

        // The empty-promise guard, the other direction: Companions, WarCouncil and Parley carry no PledgeAgainst
        // catalogue entry at all (a hired hand's own rivalry has no lord's standing behind it, and WarCouncil
        // already carries its own faction-scale motions), so the key must never surface there even when the fact
        // is (nonsensically) true.
        [Test]
        public void GIVEN_a_companions_war_council_or_parley_council_WHEN_offered_keys_are_asked_THEN_pledge_against_is_never_included()
        {
            ResolutionWorldFacts facts = Facts(anySeatedMemberCanPledgeAgainst: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts).Should().NotContain("pledge_against");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, facts).Should().NotContain("pledge_against");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, facts).Should().NotContain("pledge_against");
        }

        // Partie 1 (COUNCIL_ACTIONS.md, "juste arrange_mariage qui est valide"): arrange_marriage needs only the
        // single, approximated AnySeatedAllyHasMarriageableKin fact (both sides offer an unwed kin). Missing it
        // must withhold the motion entirely, or the table could pledge a match nobody present could actually make.
        [Test]
        public void GIVEN_arrange_marriage_WHEN_no_seated_ally_offers_a_marriageable_kin_THEN_it_is_not_offered()
        {
            ResolutionDefinition arrangeMarriage = ResolutionCatalog.Get(ResolutionKind.ArrangeMarriage);

            ResolutionEligibilityPolicy.CanOffer(arrangeMarriage, Facts(anySeatedAllyHasMarriageableKin: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(arrangeMarriage, Facts(anySeatedAllyHasMarriageableKin: false)).Should().BeFalse();
        }

        // The offering wire: arrange_marriage must be TAUGHT to the LLM only when the fact holds, or the model
        // proposes a match CourtActionResolver.TryArrangeMarriage's own re-check would refuse outright (no unwed
        // kin, no authority, or the family's own standing refuses it), a hollow promise the table never actually
        // carries out.
        [Test]
        public void GIVEN_a_seated_ally_offers_a_marriageable_kin_WHEN_offered_keys_are_asked_THEN_arrange_marriage_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, Facts(anySeatedAllyHasMarriageableKin: true))
                .Should().Contain("arrange_marriage");
        }

        // Absent that fact (nobody present, on either side, has an unwed kin to offer) the key must be withheld,
        // or the table could pledge a marriage the lift has already proven impossible.
        [Test]
        public void GIVEN_no_seated_ally_offers_a_marriageable_kin_WHEN_offered_keys_are_asked_THEN_arrange_marriage_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, Facts(anySeatedAllyHasMarriageableKin: false))
                .Should().NotContain("arrange_marriage");
        }

        // The empty-promise guard, the other direction: ClanLords, Companions and Family carry no ArrangeMarriage
        // catalogue entry at all (no genuine cross-clan house to marry into within the player's own clan or a
        // hired hand's council), so the key must never surface there even when the fact is (nonsensically) true.
        [Test]
        public void GIVEN_a_clan_lords_companions_or_family_council_WHEN_offered_keys_are_asked_THEN_arrange_marriage_is_never_included()
        {
            ResolutionWorldFacts facts = Facts(anySeatedAllyHasMarriageableKin: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts).Should().NotContain("arrange_marriage");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts).Should().NotContain("arrange_marriage");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, facts).Should().NotContain("arrange_marriage");
        }

        // Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): tribute needs only the single, approximated
        // AnySeatedTributePayerAvailable fact (some seated Parley counterpart leads a clan of their own). Missing
        // it must withhold the motion entirely, or the table could bind an enemy to a daily payment the lift has
        // already proven nobody present could actually owe.
        [Test]
        public void GIVEN_tribute_WHEN_no_seated_counterpart_can_pay_THEN_it_is_not_offered()
        {
            ResolutionDefinition tribute = ResolutionCatalog.Get(ResolutionKind.Tribute);

            ResolutionEligibilityPolicy.CanOffer(tribute, Facts(anySeatedTributePayerAvailable: true)).Should().BeTrue();
            ResolutionEligibilityPolicy.CanOffer(tribute, Facts(anySeatedTributePayerAvailable: false)).Should().BeFalse();
        }

        // The offering wire: tribute must be TAUGHT to the LLM only when the fact holds, or the model proposes a
        // pledge CouncilLift.SettleTribute's own re-check (the payer must lead a clan distinct from the player's)
        // would refuse outright, a hollow promise the table never actually carries out.
        [Test]
        public void GIVEN_a_seated_counterpart_can_pay_tribute_WHEN_offered_keys_are_asked_THEN_tribute_is_included()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, Facts(anySeatedTributePayerAvailable: true))
                .Should().Contain("tribute");
        }

        // Absent that fact (nobody seated leads a clan of their own to pay from) the key must be withheld, or the
        // table could bind an enemy to a tribute the lift has already proven impossible.
        [Test]
        public void GIVEN_no_seated_counterpart_can_pay_tribute_WHEN_offered_keys_are_asked_THEN_tribute_is_withheld()
        {
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Parley, Facts(anySeatedTributePayerAvailable: false))
                .Should().NotContain("tribute");
        }

        // The empty-promise guard, the other direction: Companions, Family, ClanLords and WarCouncil carry no
        // Tribute catalogue entry at all (only a Parley ever seats an enemy faction leader to pay tribute from),
        // so the key must never surface there even when the fact is (nonsensically) true.
        [Test]
        public void GIVEN_a_companions_family_clan_lords_or_war_council_WHEN_offered_keys_are_asked_THEN_tribute_is_never_included()
        {
            ResolutionWorldFacts facts = Facts(anySeatedTributePayerAvailable: true);

            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Companions, facts).Should().NotContain("tribute");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.Family, facts).Should().NotContain("tribute");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.ClanLords, facts).Should().NotContain("tribute");
            ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(CouncilScope.WarCouncil, facts).Should().NotContain("tribute");
        }
    }
}
