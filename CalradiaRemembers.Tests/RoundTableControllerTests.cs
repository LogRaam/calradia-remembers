// Code written by Gabriel Mailhot, 11/07/2026.
// Covers the round-table controller's pure decision state (RoundTableGate), which the mod's RoundTableController
// composes. RoundTableController itself and its RunAsync lap live in the net472 mod project (engine-bound:
// ChatSpeaker/Hero, NpcMemoryCampaignBehavior, EncounterContextBuilder, ChatManager), which this net10 test
// project cannot reference, so the lap sequencing is exercised in game rather than here. The gates below use the
// real RoundTableIntentDetector.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RoundTableControllerTests
    {
        // A message the real detector reads as opening the floor to the whole group.
        private const string GroupOpener = "What do you all think?";

        // An ordinary message that names no group and solicits nothing.
        private const string PlainMessage = "Good day, my lord.";

        [Test]
        public void GIVEN_a_group_present_and_a_live_conversation_WHEN_computing_visibility_THEN_the_button_shows()
        {
            var gate = new RoundTableGate();

            gate.ComputeVisible(1, false, false).Should().BeTrue();
            gate.ComputeVisible(3, false, false).Should().BeTrue();
        }

        [Test]
        public void GIVEN_no_witness_or_a_captive_scene_or_an_ended_talk_WHEN_computing_visibility_THEN_it_is_hidden()
        {
            var gate = new RoundTableGate();

            gate.ComputeVisible(0, false, false).Should().BeFalse(); // no group to go round
            gate.ComputeVisible(2, true, false).Should().BeFalse();  // captive encounter
            gate.ComputeVisible(2, false, true).Should().BeFalse();  // conversation already ended
        }

        [Test]
        public void GIVEN_a_group_opener_WHEN_the_main_npc_replies_THEN_the_floor_opens_exactly_once()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, false);

            // Consumed on the main NPC's reply, then never again for the same request.
            gate.TryConsumeForMainReply(true, false, false).Should().BeTrue();
            gate.TryConsumeForMainReply(true, false, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_group_opener_WHEN_the_speaker_is_not_the_main_npc_THEN_the_floor_does_not_open()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, false);

            gate.TryConsumeForMainReply(false, false, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_group_opener_WHEN_the_scene_is_captive_or_the_talk_ended_THEN_the_floor_does_not_open()
        {
            var captiveGate = new RoundTableGate();
            captiveGate.OnPlayerSend(GroupOpener, 2, false);
            captiveGate.TryConsumeForMainReply(true, true, false).Should().BeFalse();

            var endedGate = new RoundTableGate();
            endedGate.OnPlayerSend(GroupOpener, 2, false);
            endedGate.TryConsumeForMainReply(true, false, true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_ordinary_message_WHEN_the_main_npc_replies_THEN_the_floor_stays_closed()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(PlainMessage, 2, false);

            gate.TryConsumeForMainReply(true, false, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_group_opener_but_no_witnesses_present_WHEN_the_main_npc_replies_THEN_the_floor_stays_closed()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 0, false);

            gate.TryConsumeForMainReply(true, false, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_group_opener_inside_a_captive_scene_WHEN_the_main_npc_replies_THEN_the_floor_stays_closed()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, true); // captive at send time never arms the request

            gate.TryConsumeForMainReply(true, false, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_later_ordinary_message_WHEN_it_follows_a_group_opener_THEN_it_disarms_the_pending_request()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, false); // armed
            gate.OnPlayerSend(PlainMessage, 2, false); // overwrites: no longer a group opener

            gate.TryConsumeForMainReply(true, false, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_rotation_start_WHEN_set_THEN_it_round_trips()
        {
            var gate = new RoundTableGate { Start = 2 };

            gate.Start.Should().Be(2);
        }
    }
}
