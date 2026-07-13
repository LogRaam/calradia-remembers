// Code written by Gabriel Mailhot, 06/07/2026.
// Grudges pillar, Tier 3 (the vendetta end of the spectrum): one behaviour per test, Gabriel reads
// these as the design documentation for ExecutedKin formation. GrudgePolicy.RigidityAt already fixes
// this source's rigidity to VendettaRigidity regardless of the magnitude this rule proposes, so these
// tests only cover Forms/Magnitude/KnownToPlayer, never rigidity itself (that is GrudgePolicyTests's job).
//
// WHY IT MATTERS: this rule fires from ExecutedKinGrudgeBehavior whenever the PLAYER personally executes
// a lord, planting a vendetta on that lord's close living kin. It decides three things that all feed the
// plant straight into GrudgeBehavior: whether a grudge forms at all (Forms), how deep the wound starts
// (Magnitude), and whether the kin's resentment is out in the open (KnownToPlayer). Because
// GrudgeSource.ExecutedKin is hard-wired to VendettaRigidity, ANY grudge this rule proposes is
// unforgivable and never decays, so a wrong Forms=true would saddle an unrelated hero with a permanent
// vendetta, and a wrong Magnitude/KnownToPlayer would misrepresent how an execution's kin actually reacts.

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
        // No benign-execution gate exists for any recognized kinship: unlike CaptiveAbuse (Severity 0 is
        // benign), an execution always wounds and is always witnessed or plainly learned of, so all four
        // kin kinds must form and must be KNOWN to the player, never a hidden grudge.
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

        // Spouse and child are the tightest bonds, so ChildOrSpouseMagnitude (90) must be the heaviest tier
        // this rule ever proposes: losing them to the player's own hand should wound deeper than losing a
        // parent or sibling.
        [Test]
        public void GIVEN_a_spouse_or_child_WHEN_evaluating_THEN_the_magnitude_is_the_heaviest_tier()
        {
            int spouseMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Spouse, KinRegardForPlayer = 0}).Magnitude;
            int childMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Child, KinRegardForPlayer = 0}).Magnitude;

            spouseMagnitude.Should().Be(ExecutedKinRules.ChildOrSpouseMagnitude);
            childMagnitude.Should().Be(ExecutedKinRules.ChildOrSpouseMagnitude);
        }

        // Pins the second tier: a parent or sibling still nurses a real vendetta (SiblingOrParentMagnitude,
        // 75), just a step lighter than the tightest bonds, so the two tiers never collapse to the same
        // number or invert their ordering.
        [Test]
        public void GIVEN_a_parent_or_sibling_WHEN_evaluating_THEN_the_magnitude_is_lighter_than_spouse_or_child_but_still_a_vendetta()
        {
            int parentMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Parent, KinRegardForPlayer = 0}).Magnitude;
            int siblingMagnitude = ExecutedKinRules.ExecutedKinGrudge(new ExecutedKinFacts {Kinship = ExecutedKinKind.Sibling, KinRegardForPlayer = 0}).Magnitude;

            parentMagnitude.Should().Be(ExecutedKinRules.SiblingOrParentMagnitude);
            siblingMagnitude.Should().Be(ExecutedKinRules.SiblingOrParentMagnitude);
            parentMagnitude.Should().BeLessThan(ExecutedKinRules.ChildOrSpouseMagnitude);
        }

        // Mirrors DeliveredPrisonerLowRegardBonus's framing (per ExecutedKinFacts.KinRegardForPlayer's own
        // doc): a kin who already resented the player takes the killing as confirmation, not a shock, and
        // must nurse the vendetta harder from day one, not identically to a kin who had no prior grievance.
        [Test]
        public void GIVEN_kin_who_already_resented_the_player_WHEN_evaluating_THEN_the_magnitude_is_bumped()
        {
            var neutralRegard = new ExecutedKinFacts {Kinship = ExecutedKinKind.Sibling, KinRegardForPlayer = 0};
            var lowRegard = new ExecutedKinFacts {Kinship = ExecutedKinKind.Sibling, KinRegardForPlayer = ExecutedKinRules.LowRegardThreshold};

            int neutralMagnitude = ExecutedKinRules.ExecutedKinGrudge(neutralRegard).Magnitude;
            int lowRegardMagnitude = ExecutedKinRules.ExecutedKinGrudge(lowRegard).Magnitude;

            lowRegardMagnitude.Should().Be(neutralMagnitude + ExecutedKinRules.LowRegardBonus);
        }

        // Every magnitude in the pillar shares a 0..100 scale (GrudgePolicy.EffectiveMagnitude clamps to
        // it too): an unclamped spouse/child magnitude plus the low-regard bump would overshoot it, so this
        // pins the ceiling that keeps ExecutedKin's numbers meaningful next to every other source's.
        [Test]
        public void GIVEN_a_bump_that_would_exceed_the_100_scale_WHEN_evaluating_THEN_the_magnitude_is_clamped_to_100()
        {
            var facts = new ExecutedKinFacts {Kinship = ExecutedKinKind.Spouse, KinRegardForPlayer = ExecutedKinRules.LowRegardThreshold};

            ExecutedKinRules.ExecutedKinGrudge(facts).Magnitude.Should().Be(100);
        }

        // Fail safe, not fail open: a missing or malformed ExecutedKinFacts must never be misread as a
        // reason to plant an unforgivable vendetta on some unintended hero.
        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_executed_kin_THEN_nothing_forms()
        {
            ExecutedKinRules.ExecutedKinGrudge(null).Forms.Should().BeFalse();
        }

        // Even the lightest tier (Parent) at the friendliest regard imaginable (100) must still count as a
        // full, unresolvable vendetta: unlike an ordinary grudge, where high regard for the target softens
        // rigidity (GrudgePolicy.RigidityAt), ExecutedKin is hard-fixed to VendettaRigidity regardless of
        // how much the kin liked the player beforehand, so no amount of prior goodwill ever talks this one back.
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
