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
                                                  bool mission = false)
            => new ResolutionWorldFacts(inParty, available, vacantFief, canAfford, mission);

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
        // spare but no fief and no mission, only the two motions those facts satisfy are offered, and the
        // out-of-scope GrantFief never appears whatever the facts.
        [Test]
        public void GIVEN_a_companions_council_and_partial_facts_WHEN_offered_THEN_only_the_satisfied_motions_appear()
        {
            ResolutionWorldFacts facts = Facts(inParty: true, available: true, canAfford: true,
                                               vacantFief: false, mission: false);

            var offered = ResolutionEligibilityPolicy.OffersFor(CouncilScope.Companions, facts)
                .Select(d => d.Kind).ToList();

            offered.Should().BeEquivalentTo(new[] {ResolutionKind.AssignPartyRole, ResolutionKind.GrantStipend});
            offered.Should().NotContain(ResolutionKind.AppointGovernor); // no vacant fief
            offered.Should().NotContain(ResolutionKind.DispatchOnMission); // no mission available
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
