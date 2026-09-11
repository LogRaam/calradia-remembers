// Code written by Gabriel Mailhot, 04/08/2026.
// Documents give_hostage's eligibility gate: COUNCIL_ACTIONS.md's own five CONDITIONS ("vivant, libre, majeur,
// pas chef de clan, pas heritier unique"), each pinned by its own test so a future change to the predicate
// cannot silently drop one of Gabriel's own guards. The STAKE across every test here: an ineligible relative
// (a minor, an imprisoned kinsman, a clan chief, or the pledger's clan's own sole remaining heir) offered as a
// hostage would either be mechanically wrong (a chief has no business residing at a foreign court) or would
// gamble away a clan's own succession, exactly the "enjeu de succession vanilla trop lourd" Gabriel's own
// condition warns against.
#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class HostageEligibilityPolicyTests
    {
        private static HostageCandidateFacts Eligible() => new() {IsAlive = true, IsFree = true, IsAdult = true, IsClanChief = false, IsSoleHeir = false};

        // Baseline: every condition met must pass, or the policy would refuse a textbook-eligible relative
        // (an adult, free, living, non-chief, non-sole-heir kinsman) for no documented reason.
        [Test]
        public void GIVEN_every_condition_met_WHEN_asked_if_eligible_THEN_it_is()
        {
            HostageEligibilityPolicy.IsEligible(Eligible()).Should().BeTrue();
        }

        // A dead relative cannot be moved anywhere: COUNCIL_ACTIONS.md's own "vivant".
        [Test]
        public void GIVEN_the_relative_is_dead_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new HostageCandidateFacts {IsAlive = false, IsFree = true, IsAdult = true, IsClanChief = false, IsSoleHeir = false};

            HostageEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // A relative already held captive (by anyone) cannot be handed over as a SEPARATE hostage: COUNCIL_ACTIONS.md's
        // own "libre". Mixing custody chains would be mechanically incoherent (who releases them at term's end?).
        [Test]
        public void GIVEN_the_relative_is_not_free_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new HostageCandidateFacts {IsAlive = true, IsFree = false, IsAdult = true, IsClanChief = false, IsSoleHeir = false};

            HostageEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // A minor cannot be offered: COUNCIL_ACTIONS.md's own "majeur", the explicit "not a minor" guard Kimi's
        // review calls out by name (a child hostage is a different, far darker mechanic this build refuses).
        [Test]
        public void GIVEN_the_relative_is_a_minor_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new HostageCandidateFacts {IsAlive = true, IsFree = true, IsAdult = false, IsClanChief = false, IsSoleHeir = false};

            HostageEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // A clan chief cannot be given away: COUNCIL_ACTIONS.md's own "pas chef de clan" (a ruling lord residing
        // at a foreign court is a different, much heavier diplomatic act than this v1 "guest of honour" design).
        [Test]
        public void GIVEN_the_relative_is_a_clan_chief_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new HostageCandidateFacts {IsAlive = true, IsFree = true, IsAdult = true, IsClanChief = true, IsSoleHeir = false};

            HostageEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // The pledger's clan's own sole remaining heir cannot be given away: COUNCIL_ACTIONS.md's own "pas
        // heritier unique (sinon enjeu de succession vanilla trop lourd)". Removing the clan's only other adult
        // member would leave it a single death away from extinction, a stake this build refuses to gamble.
        [Test]
        public void GIVEN_the_relative_is_the_clans_sole_heir_WHEN_asked_if_eligible_THEN_it_is_refused()
        {
            var facts = new HostageCandidateFacts {IsAlive = true, IsFree = true, IsAdult = true, IsClanChief = false, IsSoleHeir = true};

            HostageEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }

        // Every failing condition must independently refuse, never cancel another out: two simultaneous failures
        // (a dead, underage candidate) must still read as ineligible, not somehow pass by double negative.
        [Test]
        public void GIVEN_multiple_conditions_fail_at_once_WHEN_asked_if_eligible_THEN_it_is_still_refused()
        {
            var facts = new HostageCandidateFacts {IsAlive = false, IsFree = true, IsAdult = false, IsClanChief = false, IsSoleHeir = false};

            HostageEligibilityPolicy.IsEligible(facts).Should().BeFalse();
        }
    }
}
