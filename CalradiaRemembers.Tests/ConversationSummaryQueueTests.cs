// Code written by Gabriel Mailhot, 24/07/2026.
// Increment B: the pure logic behind the durable pending-summary queue. When a conversation ends, the mod
// records a base memory event ("Spoke with X.") and fires an async LLM call meant to replace it with a rich
// summary; if the game closes or crashes before that call returns, the enrichment plan is lost and the NPC's
// memory keeps the bare placeholder forever. This queue IS that plan, captured as data durable enough to
// survive a save/quit: a background worker (a later increment) drains it to actually call the summarizer, and
// a load-time replay re-enqueues whatever survived the save.
//
// WHAT BREAKS IF THIS IS WRONG: a lost dedup lets the same base event get queued and processed twice; a broken
// cap either grows the queue forever (a save-file/memory leak) or drops the wrong job when full; a broken retry
// bound either lets one permanently-failing endpoint retry a dead job forever, burning tokens on every tick, or
// drops a job too early, before it had a fair chance to recover from a transient failure; and a Deserialize
// that throws on a corrupt or pre-feature save blob would brick the whole game load, instead of just starting
// the session with no pending jobs.

#region

using System;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ConversationSummaryQueueTests
    {
        private static PendingSummaryJob MakeJob(
            string profileId = "npc_1",
            int anchorDay = 10,
            string anchorSummary = "Spoke with X.",
            string transcript = "Player: Hi\nX: Hello.",
            string playerName = "Player",
            string replyLanguage = null!,
            int enqueuedDay = 10,
            int attempts = 0,
            string anchorId = null!)
        {
            return new PendingSummaryJob
            {
                AnchorId = anchorId,
                ProfileId = profileId,
                AnchorDay = anchorDay,
                AnchorType = "Conversation",
                AnchorSummary = anchorSummary,
                Transcript = transcript,
                PlayerName = playerName,
                ReplyLanguage = replyLanguage,
                EnqueuedDay = enqueuedDay,
                Attempts = attempts
            };
        }

        // ── FIFO order ───────────────────────────────────────────────────────

        // STAKE: a worker drains this queue in order; if FIFO broke, an old conversation's summary could be
        // processed after a newer one, or, worse, never processed at all while newer jobs keep cutting in line.
        [Test]
        public void GIVEN_two_jobs_enqueued_WHEN_draining_THEN_they_come_out_oldest_first()
        {
            var queue = new ConversationSummaryQueue();
            PendingSummaryJob first = MakeJob(profileId: "npc_1", anchorDay: 10, anchorSummary: "Spoke with X.");
            PendingSummaryJob second = MakeJob(profileId: "npc_2", anchorDay: 11, anchorSummary: "Spoke with Y.");

            queue.Enqueue(first);
            queue.Enqueue(second);

            queue.NextPending().Should().BeSameAs(first);
            queue.Complete(first);
            queue.NextPending().Should().BeSameAs(second);
        }

        // ── Dedup ────────────────────────────────────────────────────────────

        // STAKE: the anchor key (ProfileId, AnchorDay, AnchorSummary) identifies ONE base event. Without dedup,
        // a duplicate enqueue call would queue the same enrichment twice and the worker would burn an extra LLM
        // call rewriting a memory that is about to be rewritten again anyway.
        [Test]
        public void GIVEN_the_same_anchor_key_enqueued_twice_WHEN_enqueuing_THEN_only_one_copy_is_kept()
        {
            var queue = new ConversationSummaryQueue();
            PendingSummaryJob job = MakeJob(profileId: "npc_1", anchorDay: 10, anchorSummary: "Spoke with X.");
            PendingSummaryJob duplicate = MakeJob(profileId: "npc_1", anchorDay: 10, anchorSummary: "Spoke with X.", transcript: "a different transcript");

            SummaryEnqueueOutcome first = queue.Enqueue(job);
            SummaryEnqueueOutcome second = queue.Enqueue(duplicate);

            first.Should().Be(SummaryEnqueueOutcome.Added);
            second.Should().Be(SummaryEnqueueOutcome.Deduplicated);
            queue.Count.Should().Be(1);
        }

        // ── Cap ──────────────────────────────────────────────────────────────

        // STAKE: without a cap, a stuck worker (or a burst of conversations right before a crash) would grow
        // this queue without bound, bloating every future save file. Dropping the OLDEST job on overflow is the
        // deliberate choice: the newest jobs are the ones most likely to still matter to the player.
        [Test]
        public void GIVEN_the_queue_is_at_capacity_WHEN_enqueuing_one_more_THEN_the_oldest_job_is_dropped_and_size_stays_capped()
        {
            var queue = new ConversationSummaryQueue(maxJobs: 3);
            PendingSummaryJob oldest = MakeJob(profileId: "npc_1", anchorDay: 1, anchorSummary: "Spoke with A.");
            queue.Enqueue(oldest);
            queue.Enqueue(MakeJob(profileId: "npc_2", anchorDay: 2, anchorSummary: "Spoke with B."));
            queue.Enqueue(MakeJob(profileId: "npc_3", anchorDay: 3, anchorSummary: "Spoke with C."));

            SummaryEnqueueOutcome outcome = queue.Enqueue(MakeJob(profileId: "npc_4", anchorDay: 4, anchorSummary: "Spoke with D."));

            outcome.Should().Be(SummaryEnqueueOutcome.AddedOldestDropped);
            queue.Count.Should().Be(3);
            queue.Pending.Should().NotContain(oldest);
        }

        // ── Complete ─────────────────────────────────────────────────────────

        // STAKE: once the worker successfully writes the rich summary, the job MUST leave the queue, or the
        // same enrichment would be redone, and re-billed, on every subsequent drain or load-time replay.
        [Test]
        public void GIVEN_a_queued_job_WHEN_completed_THEN_it_is_removed_from_the_queue()
        {
            var queue = new ConversationSummaryQueue();
            PendingSummaryJob job = MakeJob();
            queue.Enqueue(job);

            queue.Complete(job).Should().BeTrue();
            queue.Count.Should().Be(0);
        }

        // ── Fail / retry bound ───────────────────────────────────────────────

        // STAKE: a transient failure (the LLM endpoint hiccups once) must not permanently lose the job; the
        // first two failures have to keep it in the queue, incrementing Attempts, so the worker still gets a
        // fair retry instead of the plan being thrown away over one bad call.
        [Test]
        public void GIVEN_a_job_fails_WHEN_below_MaxAttempts_THEN_Attempts_increments_and_the_job_is_kept()
        {
            var queue = new ConversationSummaryQueue();
            PendingSummaryJob job = MakeJob();
            queue.Enqueue(job);

            bool droppedAfterFirstFail = queue.Fail(job);
            bool droppedAfterSecondFail = queue.Fail(job);

            droppedAfterFirstFail.Should().BeFalse();
            droppedAfterSecondFail.Should().BeFalse();
            job.Attempts.Should().Be(2);
            queue.Count.Should().Be(1);
        }

        // STAKE: the flip side of the guarantee above: a permanently-failing endpoint (a revoked key, a dead
        // model route) must not retry the same job forever, burning a call every tick with no chance of
        // success. The 3rd failure, reaching MaxAttempts, has to drop it for good.
        [Test]
        public void GIVEN_a_job_fails_a_third_time_WHEN_that_reaches_MaxAttempts_THEN_the_job_is_dropped()
        {
            var queue = new ConversationSummaryQueue();
            PendingSummaryJob job = MakeJob();
            queue.Enqueue(job);

            queue.Fail(job);
            queue.Fail(job);
            bool droppedAfterThirdFail = queue.Fail(job);

            droppedAfterThirdFail.Should().BeTrue();
            queue.Count.Should().Be(0);
        }

        // ── Serialization ────────────────────────────────────────────────────

        // STAKE: this is the whole point of the durable queue: Serialize/Deserialize is what lets a pending job
        // survive a save/quit at all. Every field a load-time replay needs, including the transcript text and a
        // possibly-null ReplyLanguage, must round-trip faithfully, or the replayed job would be missing input
        // the summarizer needs, or crash trying to read a value that was silently dropped.
        [Test]
        public void GIVEN_a_queue_with_a_job_WHEN_serializing_and_deserializing_THEN_every_field_round_trips()
        {
            var queue = new ConversationSummaryQueue();
            PendingSummaryJob job = MakeJob(
                profileId: "npc_1",
                anchorDay: 10,
                anchorSummary: "Spoke with X.",
                transcript: "Player: Hi\nX: Hello there.",
                playerName: "Aria",
                replyLanguage: null!,
                enqueuedDay: 9,
                attempts: 1);
            queue.Enqueue(job);
            queue.Fail(job); // bump Attempts to 2, prove Attempts survives the round-trip too

            string json = queue.Serialize();
            ConversationSummaryQueue reloaded = ConversationSummaryQueue.Deserialize(json);

            reloaded.Count.Should().Be(1);
            PendingSummaryJob restored = reloaded.NextPending();
            restored.ProfileId.Should().Be("npc_1");
            restored.AnchorDay.Should().Be(10);
            restored.AnchorType.Should().Be("Conversation");
            restored.AnchorSummary.Should().Be("Spoke with X.");
            restored.Transcript.Should().Be("Player: Hi\nX: Hello there.");
            restored.PlayerName.Should().Be("Aria");
            restored.ReplyLanguage.Should().BeNull();
            restored.EnqueuedDay.Should().Be(9);
            restored.Attempts.Should().Be(2);
        }

        // ── Corrupt/missing input degrades to empty ─────────────────────────

        // STAKE: an old save simply predates this feature and carries no pending-summary data at all; a corrupt
        // blob is any other kind of damage. Either way this MUST NOT throw: a throwing Deserialize would brick
        // the whole game load over a single missing or malformed field in an otherwise-fine save.
        [Test]
        public void GIVEN_null_input_WHEN_deserializing_THEN_the_result_is_an_empty_queue()
        {
            ConversationSummaryQueue queue = ConversationSummaryQueue.Deserialize(null);
            queue.Count.Should().Be(0);
        }

        [Test]
        public void GIVEN_empty_string_input_WHEN_deserializing_THEN_the_result_is_an_empty_queue()
        {
            ConversationSummaryQueue queue = ConversationSummaryQueue.Deserialize("");
            queue.Count.Should().Be(0);
        }

        [Test]
        public void GIVEN_garbage_corrupt_input_WHEN_deserializing_THEN_it_does_not_throw_and_yields_an_empty_queue()
        {
            ConversationSummaryQueue? queue = null;
            Action act = () => queue = ConversationSummaryQueue.Deserialize("{not valid json at all!!");

            act.Should().NotThrow();
            queue!.Count.Should().Be(0);
        }

        // THE REPORTED DEFECT (audit, 15/09/2026). The anchor of a conversation is always "Spoke with X." on
        // today's date, so a SECOND sitting with the same person on the same day was byte-identical to the
        // first and was deduplicated away as a repeat. That conversation was never enriched, and the first
        // one's summary was written onto its event. Two sittings are two jobs.
        [Test]
        public void GIVEN_a_second_conversation_the_same_day_WHEN_enqueued_THEN_it_is_not_mistaken_for_the_first()
        {
            var queue = new ConversationSummaryQueue();

            queue.Enqueue(MakeJob(anchorId: "first-sitting"));

            queue.Enqueue(MakeJob(anchorId: "second-sitting")).Should().Be(SummaryEnqueueOutcome.Added);
            queue.Count.Should().Be(2);
        }

        // The SAME job offered twice is still a repeat: identity must not turn deduplication off, only make
        // it correct. A queue that accepts duplicates pays the model twice for one conversation.
        [Test]
        public void GIVEN_the_same_job_twice_WHEN_enqueued_THEN_the_repeat_is_still_refused()
        {
            var queue = new ConversationSummaryQueue();

            queue.Enqueue(MakeJob(anchorId: "one-sitting"));

            queue.Enqueue(MakeJob(anchorId: "one-sitting")).Should().Be(SummaryEnqueueOutcome.Deduplicated);
            queue.Count.Should().Be(1);
        }

        // OLDER SAVES replay jobs that carry no identity, and those must keep the behaviour they have: matched
        // by value, deduplicated by value. Anything else would drop a job a player is already waiting on.
        [Test]
        public void GIVEN_jobs_from_an_older_save_WHEN_enqueued_THEN_they_are_still_matched_by_value()
        {
            var queue = new ConversationSummaryQueue();

            queue.Enqueue(MakeJob());

            queue.Enqueue(MakeJob()).Should().Be(SummaryEnqueueOutcome.Deduplicated);
        }
    }
}
