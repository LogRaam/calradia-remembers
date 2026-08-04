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
                                                  bool outOfParty = false)
            => new ResolutionWorldFacts(inParty, available, vacantFief, canAfford, mission, outOfParty);

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
    }
}
