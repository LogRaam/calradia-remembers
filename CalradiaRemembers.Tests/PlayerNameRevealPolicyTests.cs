// Code written by Gabriel Mailhot, 19/07/2026.
// A brigand never knows a noble's name from a register (Gabriel's rule), so the only way a bandit captor
// learns it is the prisoner giving it up in the conversation. This detects that moment, so the mod can write
// it onto the captor's memory and a recurring nemesis keeps knowing it. Whole-word, case-insensitive.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerNameRevealPolicyTests
   {
      // The plain case: the prisoner names themselves. This is the trigger for the captor to remember it.
      [Test]
      public void GIVEN_the_player_states_their_name_WHEN_checking_THEN_it_is_a_reveal()
      {
         PlayerNameRevealPolicy.MentionsOwnName("I am Arwa, and my clan will come for me.", "Arwa of Thais")
                               .Should().BeTrue();
      }

      // Case-insensitive: a name shouted or lowercased still counts.
      [TestCase("arwa is my name")]
      [TestCase("ARWA. Remember it.")]
      public void GIVEN_the_name_in_any_case_WHEN_checking_THEN_it_is_a_reveal(string message)
      {
         PlayerNameRevealPolicy.MentionsOwnName(message, "Arwa").Should().BeTrue();
      }

      // Whole-word only: a name buried inside a longer word is NOT a reveal, or common syllables would
      // trip it constantly.
      [Test]
      public void GIVEN_the_name_only_inside_a_longer_word_WHEN_checking_THEN_it_is_not_a_reveal()
      {
         PlayerNameRevealPolicy.MentionsOwnName("the marwater runs cold here", "Arwa").Should().BeFalse();
      }

      // The prisoner who never names themselves stays unnamed to the captor. This is the norm, and the
      // whole point: silence keeps the name secret.
      [Test]
      public void GIVEN_a_message_without_the_name_WHEN_checking_THEN_it_is_not_a_reveal()
      {
         PlayerNameRevealPolicy.MentionsOwnName("You will hang for this, you filthy dog.", "Arwa")
                               .Should().BeFalse();
      }

      // A short connector in the full name ("of") must not be the thing that matches, or "speak of it"
      // would read as revealing "Arwa of Thais".
      [Test]
      public void GIVEN_only_a_short_connector_matches_WHEN_checking_THEN_it_is_not_a_reveal()
      {
         PlayerNameRevealPolicy.MentionsOwnName("speak of it plainly", "Arwa of Thais").Should().BeFalse();
      }

      // The family name alone also identifies: a prisoner who says "I am a Thais" has given the captor
      // something to know them by.
      [Test]
      public void GIVEN_a_family_name_part_WHEN_checking_THEN_it_is_a_reveal()
      {
         PlayerNameRevealPolicy.MentionsOwnName("I am a Thais, and we do not forget.", "Arwa of Thais")
                               .Should().BeTrue();
      }

      // Fail safe: blank message or blank name never matches.
      [TestCase("", "Arwa")]
      [TestCase("   ", "Arwa")]
      [TestCase("I am Arwa", "")]
      [TestCase("I am Arwa", null)]
      public void GIVEN_blank_input_WHEN_checking_THEN_it_is_not_a_reveal(string message, string name)
      {
         PlayerNameRevealPolicy.MentionsOwnName(message, name).Should().BeFalse();
      }
   }
}
