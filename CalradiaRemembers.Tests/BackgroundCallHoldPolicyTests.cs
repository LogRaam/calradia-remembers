// Code written by Gabriel Mailhot, 15/10/2026.
// What BackgroundCallHoldPolicy decides: whether a background model call (summary, memory compaction, letter,
// letter memory, courier secret) waits for the open conversation to close. fkasad (Nexus, 15/10/2026) runs small
// and medium models on a local server; background work slipping in between two replies made the server swap its
// KV cache away from the conversation and back, and replies timed out. He asked for the background work to wait
// for the conversation's end, on the local path only. If this is wrong one way, local replies stall again; the
// other way, cloud players' letters and summaries are delayed for nothing, or a stuck chat flag parks them forever.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BackgroundCallHoldPolicyTests
    {
        // STAKE: fkasad's report. A summary or a letter sent to the local server mid-conversation evicts the
        // conversation's KV cache and the next reply times out.
        [Test]
        public void GIVEN_a_conversation_open_on_a_local_server_WHEN_background_work_is_ready_THEN_it_waits()
        {
            BackgroundCallHoldPolicy.ShouldHold(new BackgroundCallHoldFacts {ConversationOpen = true, GoesToLocalServer = true})
                .Should().BeTrue();
        }

        // STAKE: once the conversation closes, the held summary or letter must go, or the NPC never remembers it.
        [Test]
        public void GIVEN_no_conversation_open_WHEN_background_work_is_ready_THEN_it_goes_now()
        {
            BackgroundCallHoldPolicy.ShouldHold(new BackgroundCallHoldFacts {ConversationOpen = false, GoesToLocalServer = true})
                .Should().BeFalse();
        }

        // STAKE: fkasad asked for the local path only. A cloud provider serves requests side by side, so holding
        // a cloud player's letters and summaries would only delay them.
        [Test]
        public void GIVEN_a_cloud_provider_WHEN_a_conversation_is_open_THEN_background_work_goes_now()
        {
            BackgroundCallHoldPolicy.ShouldHold(new BackgroundCallHoldFacts {ConversationOpen = true, GoesToLocalServer = false})
                .Should().BeFalse();
        }

        // STAKE: the chat's host can lose its screen without its close running. A flag stuck "open" must not park
        // every letter and summary for the rest of the session, so the wait ends at the cap.
        [Test]
        public void GIVEN_a_call_held_for_the_whole_cap_WHEN_the_conversation_still_reads_open_THEN_it_goes_anyway()
        {
            BackgroundCallHoldPolicy.ShouldHold(new BackgroundCallHoldFacts {
                ConversationOpen = true, GoesToLocalServer = true, HeldSeconds = BackgroundCallHoldPolicy.MaxHoldSeconds
            }).Should().BeFalse();
        }

        // STAKE: a long conversation, short of the cap, still keeps the local server to itself.
        [Test]
        public void GIVEN_a_call_held_for_a_few_minutes_WHEN_the_conversation_is_still_open_THEN_it_keeps_waiting()
        {
            BackgroundCallHoldPolicy.ShouldHold(new BackgroundCallHoldFacts {ConversationOpen = true, GoesToLocalServer = true, HeldSeconds = 300})
                .Should().BeTrue();
        }
    }
}
