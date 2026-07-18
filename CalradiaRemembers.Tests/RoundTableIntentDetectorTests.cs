// Code written by Gabriel Mailhot, 10/07/2026.
// Documents the free-text trigger for opening the floor to the whole group, as opposed to addressing one
// person (which routes through AddressedSpeakerDetector instead). A false positive here would derail an
// ordinary one-on-one line (or one naming a witness) into a group burst nobody asked for; a false negative
// means the player's explicit "what do you all think?" silently does nothing, since RoundTableGate.OnPlayerSend
// only arms on a Detects() hit.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RoundTableIntentDetectorTests
    {
        // Both the explicit-phrase path (an unambiguous "round table" cue) and the group-token + solicitation
        // path (e.g. "everyone" + "think") must fire, in English and French, so the trigger is not tied to one
        // exact phrasing the player has to guess.
        [TestCase("Let's have a round table on this.")]
        [TestCase("What do you all think?")]
        [TestCase("I'd like to hear from everyone.")]
        [TestCase("Everyone, share your thoughts.")]
        [TestCase("Qu'en pensez-vous, vous tous ?")]
        [TestCase("Tout le monde, donnez votre avis.")]
        [TestCase("Open the floor, I want to hear you all.")]
        public void GIVEN_a_message_opening_the_floor_to_the_group_WHEN_detecting_THEN_it_is_a_round_table(string message)
        {
            RoundTableIntentDetector.Detects(message).Should().BeTrue();
        }

        // The group-token requires a solicitation cue alongside it (a mention of "everyone" alone is not
        // enough), and naming one person ("Rhagaea") must never be mistaken for opening the floor to all: that
        // is the addressed-speaker path, a different feature entirely.
        [TestCase("Everyone knows the roads are dangerous.")]
        [TestCase("What do you think, Rhagaea?")]
        [TestCase("Tell me about the weather.")]
        public void GIVEN_a_message_addressing_one_person_or_lacking_a_cue_WHEN_detecting_THEN_it_is_not_a_round_table(string message)
        {
            RoundTableIntentDetector.Detects(message).Should().BeFalse();
        }

        // Round-table audit D8: a cue hidden INSIDE another word used to fire the detector, and every false
        // positive costs a burst of up to three extra LLM calls for a remark that opened no floor at all.
        // "Everyone says..." matched the group token plus "say" buried in "says"; in French "la direction du
        // camp" matched "dire" buried in "direction". These are the exact reported shapes.
        [TestCase("Everyone says the roads are dangerous.")]
        [TestCase("Tout le monde connait la direction du camp.")]
        [TestCase("Everyone is heading in that direction.")]
        public void GIVEN_a_cue_buried_inside_a_longer_word_WHEN_detecting_THEN_it_is_not_a_round_table(string message)
        {
            RoundTableIntentDetector.Detects(message).Should().BeFalse();
        }

        // The other side of the boundary rule: the same cues as WHOLE words must still open the floor, or
        // tightening the match would simply break the feature it was meant to protect.
        [TestCase("Everyone, say what you think.")]
        [TestCase("Tout le monde, dites ce que vous en pensez.")]
        public void GIVEN_the_same_cues_as_whole_words_WHEN_detecting_THEN_the_floor_still_opens(string message)
        {
            RoundTableIntentDetector.Detects(message).Should().BeTrue();
        }

        // Defensive: Detects is read every player send (RoundTableGate.OnPlayerSend); a null, empty, or
        // whitespace-only message must not throw and must never accidentally arm a pending request.
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void GIVEN_a_null_empty_or_whitespace_message_WHEN_detecting_THEN_it_is_not_a_round_table(string message)
        {
            RoundTableIntentDetector.Detects(message).Should().BeFalse();
        }
    }
}
