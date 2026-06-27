// Code written by Gabriel Mailhot, 27/06/2026.
// Documents the mission-registration-door rules: title/description bounded to the blob-safe length (and an
// empty one rejected), reward gold/relation clamped to CR's own payable caps (the external mod cannot promise
// a fortune or a flood of regard), an optional deadline normalised, and the id+source identity required.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ExternalMissionRulesTests
    {
        [Test]
        public void GIVEN_a_negative_gold_reward_WHEN_clamping_THEN_it_floors_at_zero()
        {
            ExternalMissionRules.ClampRewardGold(-200).Should().Be(0);
        }

        [Test]
        public void GIVEN_an_overlarge_gold_reward_WHEN_clamping_THEN_it_caps_at_the_max()
        {
            ExternalMissionRules.ClampRewardGold(int.MaxValue).Should().Be(ExternalMissionRules.MaxRewardGold);
        }

        [Test]
        public void GIVEN_a_relation_reward_above_the_gated_cap_WHEN_clamping_THEN_it_caps_at_the_quest_reward_limit()
        {
            ExternalMissionRules.ClampRewardRelation(99).Should().Be(ExternalMissionRules.MaxRewardRelation);
            ExternalMissionRules.ClampRewardRelation(-5).Should().Be(0);
            ExternalMissionRules.ClampRewardRelation(7).Should().Be(7);
        }

        [Test]
        public void GIVEN_an_empty_or_whitespace_title_WHEN_bounding_THEN_it_is_rejected_as_null()
        {
            ExternalMissionRules.BoundTitle(null).Should().BeNull();
            ExternalMissionRules.BoundTitle("   ").Should().BeNull();
        }

        [Test]
        public void GIVEN_an_overlong_description_WHEN_bounding_THEN_it_is_truncated_to_the_blob_guard()
        {
            string huge = new string('x', ExternalMissionRules.MaxDescriptionChars + 200);

            ExternalMissionRules.BoundDescription(huge).Length.Should().Be(ExternalMissionRules.MaxDescriptionChars);
        }

        [Test]
        public void GIVEN_a_padded_title_WHEN_bounding_THEN_it_is_trimmed()
        {
            ExternalMissionRules.BoundTitle("  Win the autumn race  ").Should().Be("Win the autumn race");
        }

        [Test]
        public void GIVEN_no_deadline_WHEN_normalising_THEN_it_stays_open_ended()
        {
            ExternalMissionRules.NormalizeDeadlineDays(null).Should().BeNull();
            ExternalMissionRules.NormalizeDeadlineDays(0).Should().BeNull();
            ExternalMissionRules.NormalizeDeadlineDays(-3).Should().BeNull();
        }

        [Test]
        public void GIVEN_an_overlong_deadline_WHEN_normalising_THEN_it_caps_at_a_year()
        {
            ExternalMissionRules.NormalizeDeadlineDays(10000).Should().Be(ExternalMissionRules.MaxDeadlineDays);
        }

        [Test]
        public void GIVEN_a_deadline_in_range_WHEN_normalising_THEN_it_is_unchanged()
        {
            ExternalMissionRules.NormalizeDeadlineDays(30).Should().Be(30);
        }

        [Test]
        public void GIVEN_a_missing_id_or_source_WHEN_checking_identity_THEN_it_is_not_registerable()
        {
            ExternalMissionRules.HasRequiredIdentity("", "GrandRaces").Should().BeFalse();
            ExternalMissionRules.HasRequiredIdentity("race_42", "  ").Should().BeFalse();
            ExternalMissionRules.HasRequiredIdentity("race_42", "GrandRaces").Should().BeTrue();
        }
    }
}
