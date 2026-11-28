// Code written by Gabriel Mailhot, 28/11/2026.
// Decides whether a closed chat's summary job also updates what the character carries away from the player (Stage 2 of the
// group-scene plan, "what a meeting leaves"; Gabriel, 28/11/2026: "go pour tout le système ... je testerai demain").
// If it is wrong: the switch off still spends a background call per chat, a witness has the addressed character's record
// written as his own, or an empty conversation is sent to the model.

#region

using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MeetingMemoryPolicyTests
    {
        // STAKE: the plan's rule for the night, "with every new switch off, the game behaves exactly as today": no call.
        [Test]
        public void GIVEN_the_switch_off_WHEN_a_chat_closes_THEN_no_update_is_made()
        {
            MeetingMemoryPolicy.ShouldUpdate(new MeetingMemoryFacts {SwitchOn = false, HasTranscript = true}).Should().BeFalse();
        }

        // STAKE: the feature itself: the one the player spoke with remembers what changed between them.
        [Test]
        public void GIVEN_the_switch_on_and_the_one_spoken_with_WHEN_a_chat_closes_THEN_the_record_is_updated()
        {
            MeetingMemoryPolicy.ShouldUpdate(new MeetingMemoryFacts {SwitchOn = true, HasTranscript = true}).Should().BeTrue();
        }

        // STAKE (lpsalce, 26/11/2026, the same mix-up for memories): a witness was handed the addressed character's memory.
        [Test]
        public void GIVEN_a_witness_job_WHEN_a_chat_closes_THEN_no_update_is_made()
        {
            MeetingMemoryPolicy.ShouldUpdate(new MeetingMemoryFacts {SwitchOn = true, IsWitness = true, HasTranscript = true}).Should().BeFalse();
        }

        // STAKE: with no words there is nothing to remember, and a call would only invent.
        [Test]
        public void GIVEN_no_transcript_WHEN_a_chat_closes_THEN_no_update_is_made()
        {
            MeetingMemoryPolicy.ShouldUpdate(new MeetingMemoryFacts {SwitchOn = true, HasTranscript = false}).Should().BeFalse();
            MeetingMemoryPolicy.ShouldUpdate(null).Should().BeFalse();
        }
    }
}
