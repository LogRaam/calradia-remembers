// Code written by Gabriel Mailhot, 25/09/2026.
// Documents the memory a new spouse keeps of the wedding. zhenja1, 25/09/2026: after a vanilla marriage offer, his bride
// spoke at their first word as though she had always been part of his house. Nothing had been written on her at the
// wedding, and an empty history makes the prompt say "you are already married and share a life". What this line says
// is what the model will build her whole manner on, so it must say how new the marriage is, and never less.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WeddingMemoryTextTests
   {
      // The report itself: a vanilla offer between two houses, the couple having never exchanged a word. She must
      // remember a stranger she has just married, not a life she has shared.
      [Test]
      public void GIVEN_an_arranged_match_with_someone_never_met_WHEN_she_remembers_the_wedding_THEN_she_knows_they_are_still_strangers()
      {
         string line = WeddingMemoryText.For(new WeddingFacts {PlayerName = "Arwa", HaveMet = false, FromAnotherHouse = true});

         line.Should().Be("Were wed to Arwa in a match agreed between your two houses, before the two of you had ever spoken. "
                        + "You are newlyweds and still strangers to each other.");
      }

      // They crossed paths in the game (vanilla says they met) but never talked in CR: newlyweds, not intimates.
      [Test]
      public void GIVEN_an_arranged_match_with_someone_met_once_WHEN_she_remembers_the_wedding_THEN_she_barely_knows_him()
      {
         string line = WeddingMemoryText.For(new WeddingFacts {PlayerName = "Arwa", HaveMet = true, FromAnotherHouse = true});

         line.Should().Contain("You had met, but you hardly know each other yet: you are newlyweds.");
      }

      // A courtship played out in conversation already lives in her other memories; the wedding line must not
      // contradict it by calling them strangers.
      [Test]
      public void GIVEN_a_couple_who_courted_in_conversation_WHEN_she_remembers_the_wedding_THEN_it_is_simply_the_wedding()
      {
         string line = WeddingMemoryText.For(new WeddingFacts {PlayerName = "Arwa", HaveSharedHistory = true, HaveMet = true, FromAnotherHouse = true});

         line.Should().Be("Were wed to Arwa.");
      }

      // A spouse with no house of her own (a wanderer) was not given by a family; "your two houses" would be false.
      [Test]
      public void GIVEN_a_spouse_with_no_house_of_her_own_WHEN_the_match_is_described_THEN_no_houses_are_named()
      {
         string line = WeddingMemoryText.For(new WeddingFacts {PlayerName = "Arwa", HaveMet = true, FromAnotherHouse = false});

         line.Should().NotContain("houses");
      }
   }
}
