// Code written by Gabriel Mailhot, 15/09/2026.
// What these pin: that saying the name of the person in front of you does not summon their namesakes.
//
// fkasad (Nexus, 15/09/2026) was talking to a companion named Erevan. He wrote "Erevan". The prompt
// answered with two other Erevans he had never met and told the NPC to ask which one was meant - so the
// man being addressed was asked to identify himself.
//
// The test that matters most is the second one: one extra word of evidence and the namesake is a real
// reference again, which is what keeps this from silencing genuine questions about a third party who
// happens to share a first name.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SpeakerNamesakePolicyTests
    {
        private static readonly string[] Erevan = {"erevan"};

        // THE REPORT. "Erevan the Brewer" found only by the word "erevan", from a man already named Erevan.
        [Test]
        public void GIVEN_a_match_made_only_of_the_speakers_own_name_WHEN_judged_THEN_it_is_a_namesake()
        {
            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"erevan"}, Erevan).Should().BeTrue();
        }

        // THE GUARD-RAIL, and the reason the rule is about evidence rather than names: the player who really
        // does mean the Brewer says so, and that word is not part of the speaker's name.
        [Test]
        public void GIVEN_a_match_carrying_one_word_the_speaker_does_not_have_WHEN_judged_THEN_it_stands()
        {
            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"erevan", "brewer"}, Erevan).Should().BeFalse();
        }

        // An unrelated third party is untouched by any of this.
        [Test]
        public void GIVEN_a_match_sharing_nothing_with_the_speaker_WHEN_judged_THEN_it_stands()
        {
            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"martira"}, Erevan).Should().BeFalse();
        }

        // A speaker with a two-part name shadows both parts, but only when the match uses nothing else.
        [Test]
        public void GIVEN_a_two_part_speaker_name_WHEN_the_match_uses_only_those_parts_THEN_it_is_a_namesake()
        {
            string[] speaker = {"erevan", "talmerion"};

            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"erevan", "talmerion"}, speaker).Should().BeTrue();
            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"erevan", "brewer"}, speaker).Should().BeFalse();
        }

        // Case must not decide it: the folded tokens arrive from two different code paths.
        [Test]
        public void GIVEN_a_match_in_another_case_WHEN_judged_THEN_it_is_still_a_namesake()
        {
            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"EREVAN"}, Erevan).Should().BeTrue();
        }

        // No evidence either way drops nothing. An unnamed speaker or an empty match must never be read as
        // "everything is a namesake", which would silence the hint block entirely.
        [Test]
        public void GIVEN_nothing_to_judge_on_WHEN_judged_THEN_nothing_is_dropped()
        {
            SpeakerNamesakePolicy.IsMerelyANamesake(new string[0], Erevan).Should().BeFalse();
            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"erevan"}, new string[0]).Should().BeFalse();
            SpeakerNamesakePolicy.IsMerelyANamesake(null, Erevan).Should().BeFalse();
            SpeakerNamesakePolicy.IsMerelyANamesake(new[] {"erevan"}, null).Should().BeFalse();
        }
    }
}
