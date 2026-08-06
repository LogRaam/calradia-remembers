// Code written by Gabriel Mailhot, 03/08/2026.
// Protects the FIRST governance executor's own refusal rule (CouncilLift.SettleAppointGovernor). Author's
// condition: appointing a companion or clan lord as governor is a real pledge the lift may carry out, and the
// prospective governor MAY refuse it - but only MECHANICALLY, from their own standing toward the player, never
// as a narrated refusal invented after ChangeGovernorAction already ran. That is exactly the phantom-deed class
// this project eliminates elsewhere (a quest "completed" with no verified deed, a relation gain with no gate):
// here the failure mode would be a governor appointed in the save data while the story tells the player they
// were turned down, or the reverse. Getting the boundary wrong in either direction reopens that class of bug.
//
// The standing floor (20) deliberately mirrors StanceGatePolicy.DelegatedRiskTrustFloor, the codebase's existing
// threshold for "the NPC risks exposing themselves on the player's word" (used for lending troops/influence):
// governing a fief is the same kind of exposure, so this reuses that established number rather than inventing
// a second one for the same class of trust.
//
// CanOfferAppointment is the 1:1 conversation TEACHING gate added when appoint_governor was brought to the 1:1
// pipeline (2026-08-05), alongside the council. It is the conjunction the mod's EncounterContextBuilder feeds
// from live engine facts to decide whether the LLM is even shown the action this turn. What it guards is the
// empty-promise class the whole conditional-teaching contract exists to prevent: teach the verb only when the
// bridge's own re-check (CourtActionResolver.TryAppointGovernor) could actually honour it, so an NPC can never
// "agree" to a governorship the game then silently declines. Each fact below removes one way that could happen.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class GovernorAppointmentPolicyTests
    {
        // The ordinary accept case: standing well above the floor must accept, or a trusted, long-proven
        // companion could still be turned away at the lift for no discoverable reason.
        [Test]
        public void GIVEN_standing_well_above_the_floor_WHEN_deciding_THEN_the_post_is_accepted()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(standingTowardPlayer: 50);

            verdict.Accepted.Should().BeTrue();
            verdict.RefusalReason.Should().BeNull();
        }

        // The ordinary refuse case: standing well below the floor must refuse, or the mechanical-refusal
        // condition would be dead code, and every appointment would silently succeed regardless of trust.
        [Test]
        public void GIVEN_standing_well_below_the_floor_WHEN_deciding_THEN_the_post_is_refused()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(standingTowardPlayer: 0);

            verdict.Accepted.Should().BeFalse();
            verdict.RefusalReason.Should().NotBeNullOrWhiteSpace();
        }

        // StandingFloor is inclusive (>=): pins that standing sitting EXACTLY on the tuning value already
        // accepts, so a future retune of the constant can't silently flip this boundary case to a refusal.
        [Test]
        public void GIVEN_standing_exactly_at_the_floor_WHEN_deciding_THEN_the_post_is_accepted()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(GovernorAppointmentPolicy.StandingFloor);

            verdict.Accepted.Should().BeTrue();
        }

        // The far side of the same boundary: one point under the floor must refuse, confirming the policy
        // compares by ordering (>=) rather than by equality with the floor alone.
        [Test]
        public void GIVEN_standing_one_point_below_the_floor_WHEN_deciding_THEN_the_post_is_refused()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(GovernorAppointmentPolicy.StandingFloor - 1);

            verdict.Accepted.Should().BeFalse();
        }

        // A negative standing (an NPC who actively dislikes the player) must refuse just as plainly as a
        // neutral one: this is the class of NPC most likely to be tested first in-game, so it must not
        // accidentally fall through to acceptance via an unguarded arithmetic edge case.
        [Test]
        public void GIVEN_negative_standing_WHEN_deciding_THEN_the_post_is_refused()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(standingTowardPlayer: -30);

            verdict.Accepted.Should().BeFalse();
        }

        // The all-clear case: player leads the clan, the NPC is an available member of it, and a fief sits vacant.
        // If this did not offer, appoint_governor would never be taught in 1:1 at all, and the verb would stay
        // council-only, the exact regression this whole slice exists to fix.
        [Test]
        public void GIVEN_every_teaching_fact_holds_WHEN_offering_THEN_the_action_is_taught()
        {
            bool offered = GovernorAppointmentPolicy.CanOfferAppointment(
                playerLeadsClan: true, appointeeInPlayerClan: true, appointeeAvailable: true, hasVacantGovernableFief: true,
                appointeeNotGoverningElsewhere: true, appointeeNotAway: true);

            offered.Should().BeTrue();
        }

        // Only the clan's head hands out its governorships: a player who does not lead their clan cannot seat a
        // governor at all, so teaching the verb would promise a deed the bridge (which re-checks leadership) then
        // refuses. Without this fact an NPC could "accept" a post the player has no standing to grant.
        [Test]
        public void GIVEN_the_player_does_not_lead_the_clan_WHEN_offering_THEN_the_action_is_withheld()
        {
            bool offered = GovernorAppointmentPolicy.CanOfferAppointment(
                playerLeadsClan: false, appointeeInPlayerClan: true, appointeeAvailable: true, hasVacantGovernableFief: true,
                appointeeNotGoverningElsewhere: true, appointeeNotAway: true);

            offered.Should().BeFalse();
        }

        // A governor holds one of the clan's OWN fiefs, so the appointee must belong to the player's clan (a
        // companion or clan lord). Teaching an outsider the verb would let them "agree" to govern a fief they can
        // never lawfully hold, and the bridge's own clan-membership grounding would then decline it.
        [Test]
        public void GIVEN_the_appointee_is_not_of_the_players_clan_WHEN_offering_THEN_the_action_is_withheld()
        {
            bool offered = GovernorAppointmentPolicy.CanOfferAppointment(
                playerLeadsClan: true, appointeeInPlayerClan: false, appointeeAvailable: true, hasVacantGovernableFief: true,
                appointeeNotGoverningElsewhere: true, appointeeNotAway: true);

            offered.Should().BeFalse();
        }

        // A dead, missing, or imprisoned member cannot take a post (ChangeGovernorAction itself will not seat a
        // prisoner). Teaching the verb to an unavailable appointee would surface an offer the bridge cannot honour.
        [Test]
        public void GIVEN_the_appointee_is_unavailable_WHEN_offering_THEN_the_action_is_withheld()
        {
            bool offered = GovernorAppointmentPolicy.CanOfferAppointment(
                playerLeadsClan: true, appointeeInPlayerClan: true, appointeeAvailable: false, hasVacantGovernableFief: true,
                appointeeNotGoverningElsewhere: true, appointeeNotAway: true);

            offered.Should().BeFalse();
        }

        // With no town or castle held without a governor, there is nothing to grant: teaching the verb here is the
        // textbook empty promise (a governorship offered over a fief that does not exist or is already governed),
        // which is why the offering mirrors the council's own HasVacantGovernableFief gate.
        [Test]
        public void GIVEN_no_vacant_governable_fief_WHEN_offering_THEN_the_action_is_withheld()
        {
            bool offered = GovernorAppointmentPolicy.CanOfferAppointment(
                playerLeadsClan: true, appointeeInPlayerClan: true, appointeeAvailable: true, hasVacantGovernableFief: false,
                appointeeNotGoverningElsewhere: true, appointeeNotAway: true);

            offered.Should().BeFalse();
        }

        // STAKE (v1.34.2 governance hardening, finding 2): one governor, one seat. An appointee who already governs
        // another fief must NOT be offered a second post, or the bridge (which re-checks GovernorOf at commit) would
        // refuse the very deed the model was taught to promise, reopening the empty-promise class. Withhold the offer.
        [Test]
        public void GIVEN_the_appointee_already_governs_another_fief_WHEN_offering_THEN_the_action_is_withheld()
        {
            bool offered = GovernorAppointmentPolicy.CanOfferAppointment(
                playerLeadsClan: true, appointeeInPlayerClan: true, appointeeAvailable: true, hasVacantGovernableFief: true,
                appointeeNotGoverningElsewhere: false, appointeeNotAway: true);

            offered.Should().BeFalse();
        }

        // STAKE (v1.34.2 governance hardening, finding 2): a companion away on a companion-mission errand cannot be
        // seated in absentia (the bridge re-checks CompanionMissionBehavior.IsAway at commit and refuses). Teaching
        // the verb to an away appointee would surface an offer the bridge cannot honour, the empty-promise class.
        [Test]
        public void GIVEN_the_appointee_is_away_on_an_errand_WHEN_offering_THEN_the_action_is_withheld()
        {
            bool offered = GovernorAppointmentPolicy.CanOfferAppointment(
                playerLeadsClan: true, appointeeInPlayerClan: true, appointeeAvailable: true, hasVacantGovernableFief: true,
                appointeeNotGoverningElsewhere: true, appointeeNotAway: false);

            offered.Should().BeFalse();
        }
    }
}
