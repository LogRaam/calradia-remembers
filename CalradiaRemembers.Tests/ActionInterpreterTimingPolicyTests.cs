// Code written by Gabriel Mailhot, 13/08/2026.
// Documents how the "Actions Interpreter Timing" MCM dropdown string maps to the ActionInterpreterTiming enum.
// This decides, in Prose + Interpreter composition mode, whether the action interpreter's tags apply in-turn
// (Synchronous) or a moment after the reply is shown (Asynchronous).
//
// WHY IT MATTERS: Synchronous is the safe default (flow-control tags such as end_conversation and an OOC refusal
// note still land before control returns to the player, exactly as the integrated path does). Anything the parser
// does NOT firmly recognise as "Asynchronous" must degrade to Synchronous, so a stale or hand-edited config
// never silently defers state changes off the turn. The dropdown itself is built from the same label constants
// this parser matches, so the two can never drift.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ActionInterpreterTimingPolicyTests
    {
        // The shipped default is the in-turn path; a config that never touched this setting must keep it.
        [Test]
        public void GIVEN_the_synchronous_label_WHEN_parsing_THEN_synchronous_is_returned()
        {
            ActionInterpreterTimingPolicy.Parse(ActionInterpreterTimingPolicy.SynchronousLabel)
                .Should().Be(ActionInterpreterTiming.Synchronous);
        }

        // The only value that may defer tag application off the turn is the exact opt-in label; if this stops
        // matching, a player who chose Asynchronous would silently keep the synchronous path.
        [Test]
        public void GIVEN_the_asynchronous_label_WHEN_parsing_THEN_asynchronous_is_returned()
        {
            ActionInterpreterTimingPolicy.Parse(ActionInterpreterTimingPolicy.AsynchronousLabel)
                .Should().Be(ActionInterpreterTiming.Asynchronous);
        }

        // An MCM value can arrive with stray casing/spacing across MCM versions; the opt-in must survive that
        // rather than silently reverting to Synchronous behind the player's back.
        [Test]
        public void GIVEN_a_padded_or_recased_asynchronous_value_WHEN_parsing_THEN_asynchronous_is_still_returned()
        {
            ActionInterpreterTimingPolicy.Parse("  asynchronous  ").Should().Be(ActionInterpreterTiming.Asynchronous);
        }

        // A blank, null, or unrecognised value must NEVER defer state changes off the turn; it degrades to
        // Synchronous, where end_conversation and refusal notes still apply in-turn like the integrated path.
        [Test]
        public void GIVEN_a_blank_null_or_unknown_value_WHEN_parsing_THEN_it_degrades_to_synchronous()
        {
            ActionInterpreterTimingPolicy.Parse(null).Should().Be(ActionInterpreterTiming.Synchronous);
            ActionInterpreterTimingPolicy.Parse("").Should().Be(ActionInterpreterTiming.Synchronous);
            ActionInterpreterTimingPolicy.Parse("   ").Should().Be(ActionInterpreterTiming.Synchronous);
            ActionInterpreterTimingPolicy.Parse("later please").Should().Be(ActionInterpreterTiming.Synchronous);
        }
    }
}
