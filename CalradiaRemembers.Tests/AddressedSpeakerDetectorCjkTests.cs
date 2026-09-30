// Code written by Gabriel Mailhot, 30/09/2026.
// yozakura12 (Nexus, 24/09/2026, on 2.5.23) added the companion カリー as a witness to a conversation with ベラドンナ
// and wrote 「カリー、聞かせてくれるか？」 ("Karry, will you tell me?"). The turn stayed with ベラドンナ, who told カリー
// to answer, and カリー never got a turn. The detector found an address only by splitting on SPACES and reading a LATIN
// comma: a Japanese line has neither, so the whole message was one "word" and no witness was ever addressed. These
// tests pin Japanese (and other unspaced CJK) addresses, and that a name merely MENTIONED still steals nothing, which is
// the rule the English tests in AddressedSpeakerDetectorTests guard.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AddressedSpeakerDetectorCjkTests
    {
        private const string MainNpcId = "npc_belladonna";
        private const string WitnessId = "npc_karry";

        private static List<AddressedSpeakerDetector.Participant> Participants(string witnessName = "カリー")
            => new() {
                new AddressedSpeakerDetector.Participant(
                    MainNpcId, AddressedSpeakerDetector.DistinctiveNameTokens("ベラドンナ")),
                new AddressedSpeakerDetector.Participant(
                    WitnessId, AddressedSpeakerDetector.DistinctiveNameTokens(witnessName))
            };

        // yozakura12's exact line: without this the witness is present, addressed by name, and never speaks.
        [Test]
        public void GIVEN_a_japanese_line_opening_on_the_witness_name_and_an_ideographic_comma_WHEN_detected_THEN_the_witness_answers()
        {
            AddressedSpeakerDetector.Detect("「カリー、聞かせてくれるか？」", Participants()).Should().Be(WitnessId);
        }

        // Many players type dialogue without the corner brackets; the address must not depend on them.
        [Test]
        public void GIVEN_the_same_address_without_quotation_brackets_WHEN_detected_THEN_the_witness_answers()
        {
            AddressedSpeakerDetector.Detect("カリー、聞かせてくれるか？", Participants()).Should().Be(WitnessId);
        }

        // A name used as the subject or object of a sentence (here with the particle は) is talk ABOUT that person;
        // routing it would hand ベラドンナ's turn to カリー, the impersonation bug the English rules exist to prevent.
        [Test]
        public void GIVEN_the_witness_named_as_the_subject_of_a_question_WHEN_detected_THEN_no_one_is_addressed()
        {
            AddressedSpeakerDetector.Detect("「カリーは何か知っているのか？」", Participants()).Should().BeNull();
        }

        // The player addresses ベラドンナ and asks about カリー: the one addressed answers, not the one mentioned.
        [Test]
        public void GIVEN_the_main_character_addressed_and_the_witness_mentioned_WHEN_detected_THEN_the_main_character_answers()
        {
            AddressedSpeakerDetector.Detect("「ベラドンナ、カリーのことをどう思う？」", Participants()).Should().Be(MainNpcId);
        }

        // The Japanese counterpart of "What say you, Martira?": the name closes the line after a comma.
        [Test]
        public void GIVEN_the_witness_name_closing_the_line_after_an_ideographic_comma_WHEN_detected_THEN_the_witness_answers()
        {
            AddressedSpeakerDetector.Detect("「そうだろう、カリー？」", Participants()).Should().Be(WitnessId);
        }

        // Calling someone by name alone ("Karry!") is the plainest address there is.
        [Test]
        public void GIVEN_only_the_witness_name_called_out_WHEN_detected_THEN_the_witness_answers()
        {
            AddressedSpeakerDetector.Detect("「カリー！」", Participants()).Should().Be(WitnessId);
        }

        // Many Japanese and Chinese given names are two characters (エマ, リン, 李明); the three-letter floor meant for
        // Latin connectors like "of" and "al" made them impossible to address at all.
        [Test]
        public void GIVEN_a_two_character_japanese_name_WHEN_addressed_THEN_that_witness_answers()
        {
            AddressedSpeakerDetector.Detect("「エマ、来てくれ」", Participants("エマ")).Should().Be(WitnessId);
        }

        // A full name written with a middle dot (カリー・ロス) is still addressed by the given name alone.
        [Test]
        public void GIVEN_a_witness_whose_full_name_has_a_middle_dot_WHEN_addressed_by_the_given_name_THEN_the_witness_answers()
        {
            AddressedSpeakerDetector.Detect("カリー、どう思う？", Participants("カリー・ロス")).Should().Be(WitnessId);
        }

        // The workaround offered to yozakura12 before the fix (a Latin comma after the name) keeps working, bracket and all.
        [Test]
        public void GIVEN_a_latin_comma_after_the_japanese_name_inside_brackets_WHEN_detected_THEN_the_witness_answers()
        {
            AddressedSpeakerDetector.Detect("「カリー, 聞かせてくれるか？」", Participants()).Should().Be(WitnessId);
        }
    }
}
