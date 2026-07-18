// Code written by Gabriel Mailhot, 17/07/2026.
// This policy is the bounded retry loop that guarantees a committed letter (today only the child-support pension
// due after a birth) actually gets written, without the three failure modes that make scheduled generation hard:
// losing it to a single ill-timed LLM outage, queuing a duplicate while one is already being written, and
// re-queuing a hopeless one every day forever. Each test pins one of those boundaries. The order of precedence
// matters as much as each rule: a letter already written must be recognised as done even past its due day, and an
// in-flight generation must suppress a second attempt even when attempts remain.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScheduledLetterPolicyTests
    {
        // The due-day gate: a pension committed a week out must not be written the day it is scheduled, or the
        // "sent a week after the birth" pacing collapses and the letter arrives immediately.
        [Test]
        public void GIVEN_the_due_day_has_not_arrived_WHEN_deciding_THEN_it_waits()
        {
            ScheduledLetterPolicy.Decide(sendDay: 107, today: 100, attempts: 0, fulfilled: false, generationInFlight: false)
                .Should().Be(ScheduledLetterAction.Wait);
        }

        // The core happy path: on the due day, with nothing written or in flight and attempts to spare, the letter
        // is generated. This is the tick that actually produces the pension.
        [Test]
        public void GIVEN_it_is_due_and_nothing_is_in_flight_WHEN_deciding_THEN_it_generates()
        {
            ScheduledLetterPolicy.Decide(sendDay: 100, today: 100, attempts: 0, fulfilled: false, generationInFlight: false)
                .Should().Be(ScheduledLetterAction.Generate);
        }

        // The due-day boundary is INCLUSIVE: the letter is written on exactly the scheduled day, not one day later,
        // so the pension is not silently delayed by a day.
        [Test]
        public void GIVEN_today_is_exactly_the_due_day_WHEN_deciding_THEN_it_generates()
        {
            ScheduledLetterPolicy.Decide(sendDay: 100, today: 100, attempts: 0, fulfilled: false, generationInFlight: false)
                .Should().Be(ScheduledLetterAction.Generate);
        }

        // No-duplicate guard: while a blank placeholder of this reason is mid-generation, a second attempt must be
        // suppressed even though the day is due and attempts remain, or the mother sends two pension letters.
        [Test]
        public void GIVEN_a_generation_is_already_in_flight_WHEN_deciding_THEN_it_waits()
        {
            ScheduledLetterPolicy.Decide(sendDay: 100, today: 105, attempts: 1, fulfilled: false, generationInFlight: true)
                .Should().Be(ScheduledLetterAction.Wait);
        }

        // The whole reason the pillar exists: a letter written on an earlier attempt (fulfilled) means the intent
        // is done and must be dropped, so the same pension is never generated twice across days.
        [Test]
        public void GIVEN_the_letter_has_already_been_written_WHEN_deciding_THEN_it_is_fulfilled()
        {
            ScheduledLetterPolicy.Decide(sendDay: 100, today: 108, attempts: 1, fulfilled: true, generationInFlight: false)
                .Should().Be(ScheduledLetterAction.Fulfilled);
        }

        // Fulfilled OUTRANKS the attempt bound: even a letter that took every attempt but did land must read as
        // done, not abandoned, so a slow-but-eventual success is not wrongly discarded.
        [Test]
        public void GIVEN_it_is_fulfilled_at_the_attempt_limit_WHEN_deciding_THEN_fulfilled_wins_over_give_up()
        {
            ScheduledLetterPolicy.Decide(sendDay: 100, today: 120, attempts: ScheduledLetterPolicy.MaxAttempts,
                    fulfilled: true, generationInFlight: false)
                .Should().Be(ScheduledLetterAction.Fulfilled);
        }

        // The unbounded-retry guard: once the attempt limit is hit with nothing written, the intent is abandoned
        // rather than re-queued every single day, so a persistently-broken LLM cannot spam generation forever.
        [Test]
        public void GIVEN_the_attempt_limit_is_reached_and_nothing_written_WHEN_deciding_THEN_it_gives_up()
        {
            ScheduledLetterPolicy.Decide(sendDay: 100, today: 130, attempts: ScheduledLetterPolicy.MaxAttempts,
                    fulfilled: false, generationInFlight: false)
                .Should().Be(ScheduledLetterAction.GiveUp);
        }

        // Confirms the bound is a ceiling reached only after real retries: one attempt short of the limit still
        // retries, so the loop uses its full retry budget before giving up on a recoverable outage.
        [Test]
        public void GIVEN_one_attempt_below_the_limit_WHEN_deciding_THEN_it_still_generates()
        {
            ScheduledLetterPolicy.Decide(sendDay: 100, today: 130, attempts: ScheduledLetterPolicy.MaxAttempts - 1,
                    fulfilled: false, generationInFlight: false)
                .Should().Be(ScheduledLetterAction.Generate);
        }
    }
}
