// Code written by Gabriel Mailhot, 25/10/2026.
// Documents when the action interpreter is asked a second time. Gabriel (25/10/2026), on a failed interpreter call that
// used to lose the reply's deeds with only a log line: "reessaie une fois l'interpreteur en ajoutant un log qui le
// specifie."
//
// WHY IT MATTERS: without the second try a timeout or a garbled reply silently drops what the character did; with more
// than one retry a slow provider would keep the player waiting turn after turn; and a reading that simply found no deed
// must not be retried, or every quiet reply would cost two calls.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class InterpreterRetryPolicyTests
    {
        // STAKE: a failed, empty or unparsable first reading would lose the reply's deeds; it is asked once more.
        [Test]
        public void GIVEN_a_first_call_that_brought_back_nothing_usable_WHEN_deciding_THEN_the_interpreter_is_asked_again()
        {
            foreach (InterpreterAttemptOutcome outcome in new[] {InterpreterAttemptOutcome.CallFailed, InterpreterAttemptOutcome.Empty, InterpreterAttemptOutcome.Unparsable})
                InterpreterRetryPolicy.ShouldRetry(outcome, 1).Should().BeTrue();
        }

        // STAKE: "une fois": a second failure is not tried a third time, so the player never waits more than two calls.
        [Test]
        public void GIVEN_a_second_call_that_failed_too_WHEN_deciding_THEN_it_is_not_asked_a_third_time()
        {
            InterpreterRetryPolicy.ShouldRetry(InterpreterAttemptOutcome.CallFailed, 2).Should().BeFalse();
        }

        // STAKE: a reading that found no deed is an answer; retrying it would double the cost of every quiet reply.
        [Test]
        public void GIVEN_a_reading_that_parsed_WHEN_deciding_THEN_it_is_never_retried_even_with_no_deed_in_it()
        {
            InterpreterRetryPolicy.ShouldRetry(InterpreterAttemptOutcome.Read, 1).Should().BeFalse();
        }
    }
}
