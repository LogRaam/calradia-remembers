// Code written by Gabriel Mailhot, 28/08/2026.
// Modder request (Heroes Voices): a TTS/subtitle addon groups CrConversation.LineSpoken events by SessionId
// and orders them by LineIndex, including the multi-participant SPOTLIGHT ensemble conversations 2.3.1
// introduced. ConversationLineSequencer is the pure logic under test here: it hands out the monotonic
// LineIndex and tracks which SessionId is current. Get the reset wrong and a second conversation's lines
// would collide with (or continue past) the first's, so the addon groups or orders lines incorrectly and
// plays the wrong audio against the wrong line.

#region

using CalradiaRemembers.Logic.Extension;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Extension
{
    [TestFixture]
    public sealed class ConversationLineSequencerTests
    {
        // STAKE: a Heroes Voices addon groups LineSpoken by SessionId and orders by LineIndex; if the index
        // did not increment within a session, every line would report the same position and the addon could
        // not tell which line came first.
        [Test]
        public void GIVEN_a_started_session_WHEN_NextIndex_is_called_repeatedly_THEN_it_yields_0_1_2()
        {
            var sequencer = new ConversationLineSequencer();
            sequencer.StartSession("session-a");

            sequencer.NextIndex().Should().Be(0);
            sequencer.NextIndex().Should().Be(1);
            sequencer.NextIndex().Should().Be(2);
        }

        // STAKE: a Heroes Voices addon groups LineSpoken by SessionId and orders by LineIndex; if the index
        // did not reset per conversation the second exchange's first line would collide with the first
        // exchange's later lines and the wrong audio would play.
        [Test]
        public void GIVEN_a_session_already_advanced_WHEN_a_new_session_starts_THEN_the_counter_resets_to_0()
        {
            var sequencer = new ConversationLineSequencer();
            sequencer.StartSession("session-a");
            sequencer.NextIndex();
            sequencer.NextIndex();

            sequencer.StartSession("session-b");

            sequencer.NextIndex().Should().Be(0);
        }

        // STAKE: SessionId is the field a listener keys its grouping on; it must always reflect the most
        // recent StartSession call, never a stale prior session's id, or lines from two different
        // conversations would be reported under the same session.
        [Test]
        public void GIVEN_two_sessions_started_in_sequence_WHEN_reading_SessionId_THEN_it_reflects_the_last_StartSession_call()
        {
            var sequencer = new ConversationLineSequencer();
            sequencer.StartSession("session-a");

            sequencer.StartSession("session-b");

            sequencer.SessionId.Should().Be("session-b");
        }

        // STAKE: before any conversation has opened there is no session to report; a listener (or CR itself)
        // must see null rather than a stale or fabricated id.
        [Test]
        public void GIVEN_a_fresh_sequencer_WHEN_no_StartSession_has_been_called_THEN_SessionId_is_null()
        {
            var sequencer = new ConversationLineSequencer();

            sequencer.SessionId.Should().BeNull();
        }
    }
}
