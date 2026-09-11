// Code written by Gabriel Mailhot, 05/08/2026.
// Documents sponsor_ward's eligibility gate: alive, free, within the young-adult age band, and not already the
// protege of another live bond (COUNCIL_ACTIONS.md's own "un protege n'a qu'un mentor a la fois"). The STAKE
// across every test here: an ineligible companion (dead, imprisoned, too old, or already sponsored) taken on as
// a ward would either be mechanically wrong (a dead companion cannot travel with anyone) or would silently
// double-book a protege between two mentors, exactly the kind of drift the eligibility gate exists to prevent.
#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class WardEligibilityPolicyTests
    {
        private static WardCandidateFacts Eligible() => new() {IsAlive = true, IsFree = true, IsYoungAdult = true, IsAlreadySponsored = false};

        // Baseline: every condition met must pass, or the policy would refuse a textbook-eligible companion for
        // no documented reason.
        [Test]
        public void GIVEN_every_condition_met_WHEN_asked_if_eligible_THEN_it_is()
        {
            WardEligibilityPolicy.IsEligible(Eligible()).Should().BeTrue();
        }

        // A dead companion cannot be mentored: the daily tick's own "both alive" condition would never fire.
        [Test]
        public void GIVEN_the_companion_is_dead_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new WardCandidateFacts {IsAlive = false, IsFree = true, IsYoungAdult = true, IsAlreadySponsored = false};

            WardEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // A companion already held prisoner (by anyone) cannot begin a mentorship that requires travelling
        // alongside the mentor.
        [Test]
        public void GIVEN_the_companion_is_not_free_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new WardCandidateFacts {IsAlive = true, IsFree = false, IsYoungAdult = true, IsAlreadySponsored = false};

            WardEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // Outside the young-adult age band (WardPolicy.IsWithinAgeBand): this task's own "young companion"
        // wording, never a grizzled veteran or a minor.
        [Test]
        public void GIVEN_the_companion_is_outside_the_age_band_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new WardCandidateFacts {IsAlive = true, IsFree = true, IsYoungAdult = false, IsAlreadySponsored = false};

            WardEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // A companion already sponsored by another mentor cannot be double-booked: COUNCIL_ACTIONS.md's own
        // "un protege n'a qu'un mentor a la fois", the one rule this eligibility gate exists to enforce beyond
        // the ordinary alive/free/age checks every other policy in this codebase already has.
        [Test]
        public void GIVEN_the_companion_is_already_sponsored_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new WardCandidateFacts {IsAlive = true, IsFree = true, IsYoungAdult = true, IsAlreadySponsored = true};

            WardEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // Every failing condition must independently refuse, never cancel another out: two simultaneous failures
        // (a dead, already-sponsored candidate) must still read as ineligible, not somehow pass by double negative.
        [Test]
        public void GIVEN_multiple_conditions_fail_at_once_WHEN_asked_if_eligible_THEN_it_is_still_refused()
        {
            var facts = new WardCandidateFacts {IsAlive = false, IsFree = true, IsYoungAdult = true, IsAlreadySponsored = true};

            WardEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }
    }
}
