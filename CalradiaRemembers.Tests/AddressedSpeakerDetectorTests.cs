// Code written by Gabriel Mailhot, 03/07/2026.
// Regression coverage for the witness-impersonation bug: a bare MENTION of a co-located witness was
// being treated as a direct ADDRESS, routing the whole turn (and the LLM's voice) to them while the
// chat window still showed the main NPC's name. These tests pin the tightened vocative/stage-direction
// rules that keep a bare mention ABOUT a witness from stealing the turn, while still letting the player
// genuinely address a witness directly (the Sprint 15B feature).

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AddressedSpeakerDetectorTests
    {
        private const string MainNpcId = "npc_alympia";
        private const string WitnessId = "npc_martira";

        private static List<AddressedSpeakerDetector.Participant> Participants()
            => new() {
                new AddressedSpeakerDetector.Participant(
                    MainNpcId, AddressedSpeakerDetector.DistinctiveNameTokens("Alympia")),
                new AddressedSpeakerDetector.Participant(
                    WitnessId, AddressedSpeakerDetector.DistinctiveNameTokens("Martira"))
            };

        // ── Must still route: real vocative / stage-direction addresses ────────

        [Test]
        public void GIVEN_a_leading_vocative_comma_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Martira, what do you think?", Participants())
                .Should().Be(WitnessId);
        }

        [Test]
        public void GIVEN_a_comma_flanked_vocative_mid_sentence_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Tell me, Martira, what happened.", Participants())
                .Should().Be(WitnessId);
        }

        [Test]
        public void GIVEN_a_vocative_comma_before_a_sentence_final_name_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("What say you, Martira?", Participants())
                .Should().Be(WitnessId);
        }

        [Test]
        public void GIVEN_the_name_alone_as_the_whole_message_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Martira?", Participants()).Should().Be(WitnessId);
            AddressedSpeakerDetector.Detect("Martira!", Participants()).Should().Be(WitnessId);
        }

        [Test]
        public void GIVEN_the_name_immediately_followed_by_you_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Martira you saw it yourself", Participants())
                .Should().Be(WitnessId);
        }

        [Test]
        public void GIVEN_a_stage_direction_that_turns_to_the_witness_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("*turns to Martira* and what of the harvest?", Participants())
                .Should().Be(WitnessId);
        }

        [Test]
        public void GIVEN_a_stage_direction_that_speaks_or_says_to_the_witness_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("*speaks to Martira* and you?", Participants())
                .Should().Be(WitnessId);
            AddressedSpeakerDetector.Detect("*says to Martira* well?", Participants())
                .Should().Be(WitnessId);
        }

        [Test]
        public void GIVEN_a_stage_direction_that_explicitly_addresses_asks_or_faces_the_witness_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("*addresses Martira* well then?", Participants())
                .Should().Be(WitnessId);
            AddressedSpeakerDetector.Detect("*asks Martira* what say you?", Participants())
                .Should().Be(WitnessId);
            AddressedSpeakerDetector.Detect("*faces Martira* and waits", Participants())
                .Should().Be(WitnessId);
        }

        // ── Must NOT route: bare mentions ABOUT the witness ────────────────────

        [Test]
        public void GIVEN_a_question_that_merely_ends_with_the_name_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("What do you make of Martira?", Participants())
                .Should().BeNull();
        }

        [Test]
        public void GIVEN_the_name_as_a_bare_first_word_report_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("Martira has been raiding my villages", Participants())
                .Should().BeNull();
        }

        [Test]
        public void GIVEN_a_stage_direction_that_only_glances_at_or_points_at_the_witness_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("*glances at Martira*", Participants()).Should().BeNull();
            AddressedSpeakerDetector.Detect("*points at Martira*", Participants()).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_bare_stage_direction_naming_the_witness_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("*Martira smiles*", Participants()).Should().BeNull();
        }

        // ── Safety / stickiness ─────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_vocative_address_to_the_main_npc_WHEN_detected_THEN_it_resolves_to_the_main_npc_harmlessly()
        {
            AddressedSpeakerDetector.Detect("Alympia, what do you think?", Participants())
                .Should().Be(MainNpcId);
        }

        [Test]
        public void GIVEN_a_message_with_no_participant_names_WHEN_detected_THEN_no_addressee_is_resolved_so_the_sticky_speaker_is_kept()
        {
            AddressedSpeakerDetector.Detect("What will you do about the bandits in the hills?", Participants())
                .Should().BeNull();
        }
    }
}
