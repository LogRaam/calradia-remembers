// Code written by Gabriel Mailhot, 10/07/2026.

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

        [TestCase("Everyone knows the roads are dangerous.")]
        [TestCase("What do you think, Rhagaea?")]
        [TestCase("Tell me about the weather.")]
        public void GIVEN_a_message_addressing_one_person_or_lacking_a_cue_WHEN_detecting_THEN_it_is_not_a_round_table(string message)
        {
            RoundTableIntentDetector.Detects(message).Should().BeFalse();
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void GIVEN_a_null_empty_or_whitespace_message_WHEN_detecting_THEN_it_is_not_a_round_table(string message)
        {
            RoundTableIntentDetector.Detects(message).Should().BeFalse();
        }
    }
}
