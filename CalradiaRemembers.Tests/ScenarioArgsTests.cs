// Code written by Gabriel Mailhot, 24/06/2026.
// Documents the target:delta payload grammar used by consequence keywords like CHANGE_RELATION:Rhagaea:+5.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScenarioArgsTests
    {
        [Test]
        public void GIVEN_a_target_and_a_positive_delta_WHEN_parsing_THEN_both_are_returned()
        {
            ScenarioArgs.TryParseTargetDelta("Rhagaea:+5", out string target, out int delta).Should().BeTrue();
            target.Should().Be("Rhagaea");
            delta.Should().Be(5);
        }

        [Test]
        public void GIVEN_a_negative_delta_WHEN_parsing_THEN_the_sign_is_kept()
        {
            ScenarioArgs.TryParseTargetDelta("Derthert:-3", out _, out int delta).Should().BeTrue();
            delta.Should().Be(-3);
        }

        [Test]
        public void GIVEN_a_zero_delta_WHEN_parsing_THEN_it_parses()
        {
            ScenarioArgs.TryParseTargetDelta("Hero:0", out string target, out int delta).Should().BeTrue();
            target.Should().Be("Hero");
            delta.Should().Be(0);
        }

        [Test]
        public void GIVEN_no_colon_WHEN_parsing_THEN_it_fails()
        {
            ScenarioArgs.TryParseTargetDelta("Rhagaea", out _, out _).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_trailing_colon_or_a_non_integer_WHEN_parsing_THEN_it_fails()
        {
            ScenarioArgs.TryParseTargetDelta("Hero:", out _, out _).Should().BeFalse();
            ScenarioArgs.TryParseTargetDelta("Hero:abc", out _, out _).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_target_and_free_text_WHEN_parsing_target_rest_THEN_the_first_colon_splits()
        {
            ScenarioArgs.TryParseTargetRest("Rhagaea:she wed the player", out string target, out string rest).Should().BeTrue();
            target.Should().Be("Rhagaea");
            rest.Should().Be("she wed the player");
        }

        [Test]
        public void GIVEN_free_text_with_further_colons_WHEN_parsing_target_rest_THEN_only_the_first_colon_splits()
        {
            ScenarioArgs.TryParseTargetRest("Ref:a: b: c", out string target, out string rest).Should().BeTrue();
            target.Should().Be("Ref");
            rest.Should().Be("a: b: c");
        }

        [Test]
        public void GIVEN_no_colon_or_empty_rest_WHEN_parsing_target_rest_THEN_it_fails()
        {
            ScenarioArgs.TryParseTargetRest("Rhagaea", out _, out _).Should().BeFalse();
            ScenarioArgs.TryParseTargetRest("Rhagaea:", out _, out _).Should().BeFalse();
        }

        [Test]
        public void GIVEN_signed_integers_WHEN_parsing_THEN_a_leading_plus_is_accepted()
        {
            ScenarioArgs.TryParseSignedInt("+7", out int v).Should().BeTrue();
            v.Should().Be(7);
            ScenarioArgs.TryParseSignedInt("-2", out v).Should().BeTrue();
            v.Should().Be(-2);
            ScenarioArgs.TryParseSignedInt("x", out _).Should().BeFalse();
        }
    }
}
