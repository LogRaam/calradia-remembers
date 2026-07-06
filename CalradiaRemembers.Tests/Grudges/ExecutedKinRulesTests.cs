// Code written by Gabriel Mailhot, 06/07/2026.
// Grudges pillar, Tier 3 (the vendetta end of the spectrum): one behaviour per test, Gabriel reads
// these as the design documentation for ExecutedKin formation. GrudgePolicy.RigidityAt already fixes
// this source's rigidity to VendettaRigidity regardless of the magnitude this rule proposes, so these
// tests only cover Forms/Magnitude/KnownToPlayer, never rigidity itself (that is GrudgePolicyTests's job).

#region

using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
    [TestFixture]
    public sealed class ExecutedKinRulesTests
    {
        [TestCase(ExecutedKinKind.Spouse)]
        [TestCase(ExecutedKinKind.Child)]
        [TestCase(ExecutedKinKind.Parent)]
        [TestCase(ExecutedKinKind.Sibling)]
        public void GIVEN_any_close_kin_of_an_executed_lord_WHEN_evaluating_THEN_a_grudge_always_forms_known_to_the_player(ExecutedKinKind kinship)
        {
            var facts = new ExecutedKinFacts {Kinship = kinship, KinRegardForPlayer = 0};

            GrudgeProposal proposal = ExecutedKinRules.ExecutedKinGrudge(facts);

            proposal.Forms.Should().BeTrue("an execution is public and always wounds a close kin, there is no benign case");
            proposal.KnownToPlayer.Should().BeTrue("the kin witnesses or plainly learns of the execution, there is nothing to conceal");
        }

        [Test]
        public void GIVEN_a_spouse_or_child_WHEN_evaluating_THEN_the_magnitude_is_the_heaviest_tier()
        {
            int spouseMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Spouse, KinRegardForPlayer = 0}).Magnitude;
            int childMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Child, KinRegardForPlayer = 0}).Magnitude;

            spouseMagnitude.Should().Be(ExecutedKinRules.ChildOrSpouseMagnitude);
            childMagnitude.Should().Be(ExecutedKinRules.ChildOrSpouseMagnitude);
        }

        [Test]
        public void GIVEN_a_parent_or_sibling_WHEN_evaluating_THEN_the_magnitude_is_lighter_than_spouse_or_child_but_still_a_vendetta()
        {
            int parentMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Parent, KinRegardForPlayer = 0}).Magnitude;
            int siblingMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Sibling, KinRegardForPlayer = 0}).Magnitude;

            parentMagnitude.Should().Be(ExecutedKinRules.SiblingOrParentMagnitude);
            siblingMagnitude.Should().Be(ExecutedKinRules.SiblingOrParentMagnitude);
            parentMagnitude.Should().BeLessThan(ExecutedKinRules.ChildOrSpouseMagnitude);
        }

        [Test]
        public void GIVEN_kin_who_already_resented_the_player_WHEN_evaluating_THEN_the_magnitude_is_bumped()
        {
            var neutralRegard = new ExecutedKinFacts {Kinship = ExecutedKinKind.Sibling, KinRegardForPlayer = 0};
            var lowRegard = new ExecutedKinFacts {Kinship = ExecutedKinKind.Sibling, KinRegardForPlayer = ExecutedKinRules.LowRegardThreshold};

            int neutralMagnitude = ExecutedKinRules.ExecutedKinGrudge(neutralRegard).Magnitude;
            int lowRegardMagnitude = ExecutedKinRules.ExecutedKinGrudge(lowRegard).Magnitude;

            lowRegardMagnitude.Should().Be(neutralMagnitude + ExecutedKinRules.LowRegardBonus);
        }

        [Test]
        public void GIVEN_a_bump_that_would_exceed_the_100_scale_WHEN_evaluating_THEN_the_magnitude_is_clamped_to_100()
        {
            var facts = new ExecutedKinFacts {Kinship = ExecutedKinKind.Spouse, KinRegardForPlayer = ExecutedKinRules.LowRegardThreshold};

            ExecutedKinRules.ExecutedKinGrudge(facts).Magnitude.Should().Be(100);
        }

        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_executed_kin_THEN_nothing_forms()
        {
            ExecutedKinRules.ExecutedKinGrudge(null).Forms.Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_lightest_forming_case_WHEN_evaluating_THEN_the_magnitude_clears_the_formation_floor_and_the_grudge_is_a_true_vendetta_regardless_of_regard()
        {
            var facts = new ExecutedKinFacts {Kinship = ExecutedKinKind.Parent, KinRegardForPlayer = 100};

            GrudgeProposal proposal = ExecutedKinRules.ExecutedKinGrudge(facts);

            proposal.Magnitude.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);

            // The rule itself never sets rigidity: GrudgePolicy.RigidityAt hard-codes ExecutedKin to
            // VendettaRigidity regardless of the target's regard for the holder, proven here even at
            // the friendliest regard imaginable.
            var candidate = new GrudgeCandidateFacts {Source = GrudgeSource.ExecutedKin, Magnitude = proposal.Magnitude, IsAttributable = true};
            GrudgePolicy.RigidityAt(regardForTarget: 100, candidate).Should().Be(GrudgePolicy.VendettaRigidity);
        }
    }
}
