// Code written by Gabriel Mailhot, 28/10/2026.
// LeaveHouseWording words the window where the player chooses a road to a ruler's hand, and teaches her what each road
// gives. RaveN017Mod (Nexus, 28/10/2026) gave Rhagaea everything expecting to join House Pethros: "I specifically chose
// the second option: to give her everything and join her." On that road the player stays head of their own house, sworn
// to her realm. If this is wrong, the window or the empress promises a place in her house the game never gives, and the
// player learns it only after an irreversible choice.

#region

using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LeaveHouseWordingTests
   {
      private static readonly LeaveHouseWordingFacts Rhagaea = new() {
         RulerName = "Rhagaea", RulerHouse = "Pethros", RulerIsFemale = true, PlayerHouse = "Ravens"
      };

      // STAKE: RaveN017Mod's report: the give-her-all road must say, before the choice, that the player stays head of
      // their own house and does not join hers.
      [Test]
      public void GIVEN_the_give_her_all_road_WHEN_offered_THEN_the_player_reads_they_stay_head_of_their_house_and_never_join_hers()
      {
         LeaveHouseWording.GiveHerAllStanding(Rhagaea).Should().Be("You remain head of Ravens, now sworn to her realm; you do not join Pethros.");
      }

      // STAKE: the window titled "Leave Ravens for Rhagaea?" with a "Leave the house" button promised what one of its two
      // roads does not do; neither may claim the player leaves the house.
      [Test]
      public void GIVEN_the_choice_window_WHEN_shown_THEN_neither_its_title_nor_its_button_promises_leaving_the_house()
      {
         LeaveHouseWording.InquiryTitle(Rhagaea).Should().Be("Wed Rhagaea: choose your road");
         LeaveHouseWording.ConfirmButton.Should().NotContainEquivalentOf("leave");
      }

      // STAKE: the empress must not tell the player they will join her house on that road, or the conversation makes the
      // promise the window no longer makes.
      [Test]
      public void GIVEN_the_give_her_all_road_WHEN_the_ruler_is_taught_it_THEN_she_is_told_never_to_promise_her_house()
      {
         LeaveHouseWording.GiveHerAllTeaching(Rhagaea).Should().Contain("stay head of Ravens").And.Contain("never join Pethros");
      }

      // STAKE: the note after the choice names what was chosen: giving her all does not leave the house.
      [Test]
      public void GIVEN_a_road_chosen_WHEN_noted_THEN_the_note_names_that_road()
      {
         LeaveHouseWording.ChosenNotice(LeaveHouseRoad.GiveHerAll, Rhagaea).Should().StartWith("You will give Rhagaea your lands and gold and wed her");
         LeaveHouseWording.ChosenNotice(LeaveHouseRoad.ThroughTheHeir, Rhagaea).Should().StartWith("You will hand Ravens to your heir and wed Rhagaea");
      }
   }
}
