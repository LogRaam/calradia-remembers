// Code written by Gabriel Mailhot, 24/06/2026.
// Documents the target:delta payload grammar used by consequence keywords like CHANGE_RELATION:Rhagaea:+5,
// and the target:rest grammar used by INJECT_MEMORY:Ref:text. These strings come straight out of a modder's
// hand-typed ModuleData/scenarios/*.json (or the composer app's export); ScenarioConsequenceExecutor calls
// straight into these parsers before ever touching game state. A wrong split silently no-ops an authored
// consequence (logged "bad payload", never applied); a parser that throws instead of returning false would
// turn a modder's typo into a campaign-breaking exception. So the grammar's edge cases are pinned here.

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
        // The baseline shape every CHANGE_RELATION/CHANGE_INFLUENCE keyword in a pack relies on: get this
        // split wrong and every authored positive-delta consequence in the game silently no-ops.
        [Test]
        public void GIVEN_a_target_and_a_positive_delta_WHEN_parsing_THEN_both_are_returned()
        {
            ScenarioArgs.TryParseTargetDelta("Rhagaea:+5", out string target, out int delta).Should().BeTrue();
            target.Should().Be("Rhagaea");
            delta.Should().Be(5);
        }

        // Scripted consequences must be able to punish, not just reward. Losing the minus sign here would
        // turn every authored negative consequence into a positive one, silently inverting a modder's intent.
        [Test]
        public void GIVEN_a_negative_delta_WHEN_parsing_THEN_the_sign_is_kept()
        {
            ScenarioArgs.TryParseTargetDelta("Derthert:-3", out _, out int delta).Should().BeTrue();
            delta.Should().Be(-3);
        }

        // ScenarioConsequenceExecutor treats delta==0 as its own no-op downstream (skips the relation/
        // influence call). "0" must parse successfully here so it is rejected there as a deliberate no-op,
        // not rejected here as malformed input.
        [Test]
        public void GIVEN_a_zero_delta_WHEN_parsing_THEN_it_parses()
        {
            ScenarioArgs.TryParseTargetDelta("Hero:0", out string target, out int delta).Should().BeTrue();
            target.Should().Be("Hero");
            delta.Should().Be(0);
        }

        // A modder's typo (or a hostile/malformed file) must fail parsing cleanly, never throw, so the
        // executor's log-and-skip path runs instead of an exception reaching the campaign.
        [Test]
        public void GIVEN_no_colon_WHEN_parsing_THEN_it_fails()
        {
            ScenarioArgs.TryParseTargetDelta("Rhagaea", out _, out _).Should().BeFalse();
        }

        // Two more malformed shapes (a dangling colon, a non-numeric tail) must fail the same clean way as a
        // missing colon, not throw or silently coerce to a default value.
        [Test]
        public void GIVEN_a_trailing_colon_or_a_non_integer_WHEN_parsing_THEN_it_fails()
        {
            ScenarioArgs.TryParseTargetDelta("Hero:", out _, out _).Should().BeFalse();
            ScenarioArgs.TryParseTargetDelta("Hero:abc", out _, out _).Should().BeFalse();
        }

        // The baseline shape for INJECT_MEMORY:Ref:text: splitting on the FIRST colon (unlike TargetDelta's
        // last-colon split) is what lets the free-text half carry its own punctuation.
        [Test]
        public void GIVEN_a_target_and_free_text_WHEN_parsing_target_rest_THEN_the_first_colon_splits()
        {
            ScenarioArgs.TryParseTargetRest("Rhagaea:she wed the player", out string target, out string rest).Should().BeTrue();
            target.Should().Be("Rhagaea");
            rest.Should().Be("she wed the player");
        }

        // An authored memory is free prose and will often contain its own colons (dialogue, lists, times).
        // Splitting on the wrong colon would truncate or corrupt the injected memory text.
        [Test]
        public void GIVEN_free_text_with_further_colons_WHEN_parsing_target_rest_THEN_only_the_first_colon_splits()
        {
            ScenarioArgs.TryParseTargetRest("Ref:a: b: c", out string target, out string rest).Should().BeTrue();
            target.Should().Be("Ref");
            rest.Should().Be("a: b: c");
        }

        // Mirrors TargetDelta's malformed-input contract for the rest-grammar: a memory keyword authored with
        // no target or no text must fail cleanly, not plant a blank or mistargeted memory on some NPC.
        [Test]
        public void GIVEN_no_colon_or_empty_rest_WHEN_parsing_target_rest_THEN_it_fails()
        {
            ScenarioArgs.TryParseTargetRest("Rhagaea", out _, out _).Should().BeFalse();
            ScenarioArgs.TryParseTargetRest("Rhagaea:", out _, out _).Should().BeFalse();
        }

        // The shared low-level integer parser both delta grammars rely on. The DSL convention always signs a
        // positive delta explicitly ("+5"), so "+7" must parse the same as "7" or every authored positive
        // consequence would fail to parse.
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
