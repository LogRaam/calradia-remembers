// Code written by Gabriel Mailhot, 11/07/2026.
// Covers the round-table controller's pure decision state (RoundTableGate), which the mod's RoundTableController
// composes. RoundTableController itself and its RunAsync lap live in the net472 mod project (engine-bound:
// ChatSpeaker/Hero, NpcMemoryCampaignBehavior, EncounterContextBuilder, ChatManager), which this net10 test
// project cannot reference, so the lap sequencing is exercised in game rather than here. The gates below use the
// real RoundTableIntentDetector.

#region

using CalradiaRemembers.Logic.Assemblies;
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

        // ComputeVisible backs the mod's IsRoundTableVisible-bound "Round Table" button: with a group present in
        // a live, non-captive conversation, the button must actually show or the player has no way to trigger it.
        [Test]
        public void GIVEN_a_group_present_and_a_live_conversation_WHEN_computing_visibility_THEN_the_button_shows()
        {
            var gate = new RoundTableGate();

            gate.ComputeVisible(1, false, false).Should().BeTrue();
            gate.ComputeVisible(3, false, false).Should().BeTrue();
        }

        // Three independent reasons the button must stay hidden: nobody to go round with, a captive scene
        // (round-table group discussion has no place in a CNC encounter), or a conversation already over.
        [Test]
        public void GIVEN_no_witness_or_a_captive_scene_or_an_ended_talk_WHEN_computing_visibility_THEN_it_is_hidden()
        {
            var gate = new RoundTableGate();

            gate.ComputeVisible(0, false, false).Should().BeFalse(); // no group to go round
            gate.ComputeVisible(2, true, false).Should().BeFalse();  // captive encounter
            gate.ComputeVisible(2, false, true).Should().BeFalse();  // conversation already ended
        }

        // TryConsumeForMainReply clears the flag it returns true for, so the same free-text opener must not
        // re-trigger a second round-table burst on a later, unrelated main-NPC reply.
        [Test]
        public void GIVEN_a_group_opener_WHEN_the_main_npc_replies_THEN_the_floor_opens_exactly_once()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, false);

            // Consumed on the main NPC's reply, then never again for the same request.
            gate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = false, ConversationEnded = false}).Should().BeTrue();
            gate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = false, ConversationEnded = false}).Should().BeFalse();
        }

        // Only the main NPC's own reply can open the floor; a witness turn in between (speakerIsMain false)
        // must not consume the pending request, or the burst could start mid-witness-exchange.
        [Test]
        public void GIVEN_a_group_opener_WHEN_the_speaker_is_not_the_main_npc_THEN_the_floor_does_not_open()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, false);

            gate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = false, IsCaptiveEncounter = false, ConversationEnded = false}).Should().BeFalse();
        }

        // The captive and ended-conversation guards are re-checked at consume time too, not only at send time:
        // a scene can turn captive, or end, in the gap between the player's message and the NPC's reply.
        [Test]
        public void GIVEN_a_group_opener_WHEN_the_scene_is_captive_or_the_talk_ended_THEN_the_floor_does_not_open()
        {
            var captiveGate = new RoundTableGate();
            captiveGate.OnPlayerSend(GroupOpener, 2, false);
            captiveGate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = true, ConversationEnded = false}).Should().BeFalse();

            var endedGate = new RoundTableGate();
            endedGate.OnPlayerSend(GroupOpener, 2, false);
            endedGate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = false, ConversationEnded = true}).Should().BeFalse();
        }

        // False-positive guard: an ordinary line must never accidentally spawn a group burst nobody asked for.
        [Test]
        public void GIVEN_an_ordinary_message_WHEN_the_main_npc_replies_THEN_the_floor_stays_closed()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(PlainMessage, 2, false);

            gate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = false, ConversationEnded = false}).Should().BeFalse();
        }

        // Opening the floor to an empty room is meaningless: with no witnesses present, OnPlayerSend must not
        // arm the request even when the text itself reads as a group opener.
        [Test]
        public void GIVEN_a_group_opener_but_no_witnesses_present_WHEN_the_main_npc_replies_THEN_the_floor_stays_closed()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 0, false);

            gate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = false, ConversationEnded = false}).Should().BeFalse();
        }

        // Defense in depth alongside the consume-time captive check above: OnPlayerSend itself must refuse to
        // arm the request while the scene is already captive, so a captive round-table can never even queue up.
        [Test]
        public void GIVEN_a_group_opener_inside_a_captive_scene_WHEN_the_main_npc_replies_THEN_the_floor_stays_closed()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, true); // captive at send time never arms the request

            gate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = false, ConversationEnded = false}).Should().BeFalse();
        }

        // OnPlayerSend overwrites, it does not accumulate: only the player's LATEST message should decide
        // whether the floor opens, so a stray earlier "everyone, thoughts?" cannot resurface after the player
        // has moved the conversation on to something else.
        [Test]
        public void GIVEN_a_later_ordinary_message_WHEN_it_follows_a_group_opener_THEN_it_disarms_the_pending_request()
        {
            var gate = new RoundTableGate();
            gate.OnPlayerSend(GroupOpener, 2, false); // armed
            gate.OnPlayerSend(PlainMessage, 2, false); // overwrites: no longer a group opener

            gate.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = true, IsCaptiveEncounter = false, ConversationEnded = false}).Should().BeFalse();
        }

        // Plain property behavior: Start is the round-robin cursor RoundTableController reads and advances each
        // lap (RoundTablePolicy.NextStartIndex). Not derivable beyond that it must simply hold what it is given.
        [Test]
        public void GIVEN_the_rotation_start_WHEN_set_THEN_it_round_trips()
        {
            var gate = new RoundTableGate { Start = 2 };

            gate.Start.Should().Be(2);
        }
    }
}
