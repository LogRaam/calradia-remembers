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

        // The comma is the strongest ordinary-English address marker; must route so a direct
        // "Name, ..." turn goes to that witness's own profile and voice instead of the main NPC
        // silently answering for them (the impersonation bug this whole file guards).
        [Test]
        public void GIVEN_a_leading_vocative_comma_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Martira, what do you think?", Participants())
                .Should().Be(WitnessId);
        }

        // A vocative can sit mid-sentence, flanked by commas on both sides ("Tell me, Martira, ...").
        // Missing this shape would fall back to treating the line as ABOUT Martira instead of TO her.
        [Test]
        public void GIVEN_a_comma_flanked_vocative_mid_sentence_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Tell me, Martira, what happened.", Participants())
                .Should().Be(WitnessId);
        }

        // The trailing case: a comma right before a name that ends the sentence ("What say you,
        // Martira?"). Without this shape the most natural way to close a question at someone would
        // be missed and left as a bare mention.
        [Test]
        public void GIVEN_a_vocative_comma_before_a_sentence_final_name_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("What say you, Martira?", Participants())
                .Should().Be(WitnessId);
        }

        // A bare name (or name+punctuation) as the ENTIRE message is unambiguous even without a
        // vocative comma at all ("Martira?" / "Martira!"); wholeMessageIsOneWord exists specifically
        // for this shortest, comma-less form of address.
        [Test]
        public void GIVEN_the_name_alone_as_the_whole_message_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Martira?", Participants()).Should().Be(WitnessId);
            AddressedSpeakerDetector.Detect("Martira!", Participants()).Should().Be(WitnessId);
        }

        // "Martira you saw it yourself" has no comma at all; the word right after the name being
        // "you" is the cue that a comma was simply dropped in casual typing, not that Martira is
        // merely being discussed in the third person.
        [Test]
        public void GIVEN_the_name_immediately_followed_by_you_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("Martira you saw it yourself", Participants())
                .Should().Be(WitnessId);
        }

        // A stage direction is checked before any vocative (strongest address signal), but only when
        // it carries an explicit directed-speech cue like "turns to" (StageDirectionCues), never on
        // the name alone, or every scripted stage direction naming a witness would hijack the turn.
        [Test]
        public void GIVEN_a_stage_direction_that_turns_to_the_witness_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("*turns to Martira* and what of the harvest?", Participants())
                .Should().Be(WitnessId);
        }

        // Two more phrasings of the same directed-speech cue (speaks to / says to); each must qualify
        // independently since players will not standardize on one verb when they type a stage direction.
        [Test]
        public void GIVEN_a_stage_direction_that_speaks_or_says_to_the_witness_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("*speaks to Martira* and you?", Participants())
                .Should().Be(WitnessId);
            AddressedSpeakerDetector.Detect("*says to Martira* well?", Participants())
                .Should().Be(WitnessId);
        }

        // Rounds out the StageDirectionCues set (addresses/asks/faces); the cue list was kept narrow
        // and explicit on purpose so that unlisted verbs of mere observation ("glances at", "points
        // at", tested below as non-routing) never falsely qualify as a directed address.
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

        // ── Comma-dropped tag vocative (player report: "Isn't that right Tezzeret?") ──

        // The exact player report that drove this fix (CHANGELOG v1.30.11): "Isn't that right,
        // Martira?" typed with the comma dropped was staying with the main NPC instead of handing
        // the turn to the witness under her own portrait. VocativeTagWords ("right", "aye", ...)
        // recovers the address when casual typing loses the comma.
        [Test]
        public void GIVEN_a_tag_vocative_with_the_comma_dropped_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect(
                    "Those Sturgians don't know what's coming for 'em. Isn't that right Martira?", Participants())
                .Should().Be(WitnessId);
        }

        // A second tag word beyond "right" ("aye"), proving the fix generalizes across the whole
        // VocativeTagWords set rather than being hard-coded to the single word from the bug report.
        [Test]
        public void GIVEN_another_tag_word_before_the_name_WHEN_detected_THEN_routes_to_the_named_witness()
        {
            AddressedSpeakerDetector.Detect("We ride at dawn aye Martira?", Participants())
                .Should().Be(WitnessId);
        }

        // VocativeTagWords is deliberately narrow: a verb of mention like "seen" must NEVER qualify
        // as a tag word, or "Have you seen Martira?" (a question ABOUT her) would wrongly steal the
        // turn to her voice, undoing the very distinction this file exists to protect.
        [Test]
        public void GIVEN_a_verb_of_mention_before_the_name_not_a_tag_WHEN_detected_THEN_does_not_route()
        {
            // A preposition or verb of mention before the name is never a tag, so this stays a mention.
            AddressedSpeakerDetector.Detect("Have you seen Martira?", Participants())
                .Should().BeNull();
        }

        // ── Must NOT route: bare mentions ABOUT the witness ────────────────────

        // The core false positive this whole file exists to prevent: a question merely ENDING in a
        // name ("What do you make of Martira?") is about the witness, not addressed to her, and must
        // never hijack the turn to her voice.
        [Test]
        public void GIVEN_a_question_that_merely_ends_with_the_name_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("What do you make of Martira?", Participants())
                .Should().BeNull();
        }

        // A report that happens to open with the witness's name ("Martira has been raiding my
        // villages") carries none of the address cues (no comma, no "you", not the whole message);
        // must stay a mention or every sentence that starts with a name would misfire.
        [Test]
        public void GIVEN_the_name_as_a_bare_first_word_report_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("Martira has been raiding my villages", Participants())
                .Should().BeNull();
        }

        // "at" is intentionally absent from StageDirectionCues: glancing or pointing AT someone is not
        // the same as speaking TO them, so these stage directions must resolve to nothing rather than
        // steal the turn to a witness who was only gestured at.
        [Test]
        public void GIVEN_a_stage_direction_that_only_glances_at_or_points_at_the_witness_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("*glances at Martira*", Participants()).Should().BeNull();
            AddressedSpeakerDetector.Detect("*points at Martira*", Participants()).Should().BeNull();
        }

        // A stage direction that is nothing but the witness's own action ("*Martira smiles*") carries
        // no address cue at all; must not route, or every scripted witness action would wrongly hand
        // the turn to them.
        [Test]
        public void GIVEN_a_bare_stage_direction_naming_the_witness_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("*Martira smiles*", Participants()).Should().BeNull();
        }

        // ── Appositive reference, not a vocative (player report 2026-07-27) ────

        // The reported bug: "I wanted to ask about your friend, Martira." switched the active speaker to
        // Martira, even though she is only being referred to in the third person. The comma before her
        // name made the old rule read it as a trailing vocative. "friend" is a person-descriptor, so the
        // comma here joins an appositive ("your friend" IS Martira), a mention, and must not steal the turn.
        [Test]
        public void GIVEN_a_name_in_a_possessive_appositive_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("I wanted to ask about your friend, Martira.", Participants())
                .Should().BeNull();
        }

        // The second reported phrasing ("Do you know my sister, Aitheen?"), here with the witness's name,
        // proving the fix is the descriptor noun before the comma ("sister"), not the specific word "friend".
        [Test]
        public void GIVEN_a_kinship_appositive_before_the_name_WHEN_detected_THEN_does_not_route()
        {
            AddressedSpeakerDetector.Detect("Do you know my sister, Martira?", Participants())
                .Should().BeNull();
        }

        // The precision line the fix must not cross: a possessive before the comma is NOT enough to make an
        // appositive. "I trust your judgment, Martira." addresses Martira (judgment cannot rename a person),
        // so it must STILL route. Suppressing on the possessive alone would have wrongly killed this vocative.
        [Test]
        public void GIVEN_a_possessive_object_that_is_not_a_person_before_the_name_WHEN_detected_THEN_still_routes()
        {
            AddressedSpeakerDetector.Detect("I trust your judgment, Martira.", Participants())
                .Should().Be(WitnessId);
        }

        // ── Safety / stickiness ─────────────────────────────────────────────────

        // Addressing the main NPC by name must resolve to their own id, not null or a crash; the
        // caller (ChatViewModel.ResolveAddressedSpeaker) treats a detected id equal to the main NPC's
        // as "no witness override", so ordinary conversation keeps flowing normally.
        [Test]
        public void GIVEN_a_vocative_address_to_the_main_npc_WHEN_detected_THEN_it_resolves_to_the_main_npc_harmlessly()
        {
            AddressedSpeakerDetector.Detect("Alympia, what do you think?", Participants())
                .Should().Be(MainNpcId);
        }

        // When nothing in the line names anyone, Detect must return null so the caller KEEPS the
        // previous sticky speaker rather than snapping back to the main NPC mid-exchange with a
        // witness (the Sprint 15B addressed-witness feature this class supports).
        [Test]
        public void GIVEN_a_message_with_no_participant_names_WHEN_detected_THEN_no_addressee_is_resolved_so_the_sticky_speaker_is_kept()
        {
            AddressedSpeakerDetector.Detect("What will you do about the bandits in the hills?", Participants())
                .Should().BeNull();
        }
    }
}
