// Code written by Gabriel Mailhot, 26/10/2026.
// LeaveHouseForRulerPolicy decides whether the player may leave their house for a ruler who heads hers (the heir takes
// it up and becomes the played character, the old character weds her), and the order the hand-over runs in. Gabriel,
// 26/10/2026: "Jamais elle laissera son trone [...] le joueur aura le choix soit d'abandonner son personnage et
// continuer avec l'heritier". It cannot be undone: if this is wrong, a player gives up their character and house for a
// wedding the game then refuses, or the wedding pulls the empress out of her own house and leaves her empire headless.

#region

using System.Linq;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LeaveHouseForRulerPolicyTests
   {
      private static LeaveHouseFacts Open() => new() {
         PlayerIsWed = false, TwoHeadsOfHouses = true, WarOrCaptivity = false, HerHeartIsWon = true, HasAnHeir = true,
         HeirRidesWithThePlayer = true, ArmyOrBattle = false, TheGameWouldWedThemAfter = true
      };

      // STAKE: the road Gabriel asked for must open when everything is in place, or the empress only ever says no.
      [Test]
      public void GIVEN_her_heart_won_and_an_heir_at_the_players_side_WHEN_asked_THEN_the_road_is_open()
      {
         LeaveHouseForRulerPolicy.WhyNot(Open()).Should().Be(LeaveHouseBar.None);
      }

      // STAKE: she would never be a second wife, and the wife's own road is not built yet.
      [Test]
      public void GIVEN_the_player_is_wed_WHEN_asked_THEN_it_is_barred_first()
      {
         var f = new LeaveHouseFacts {PlayerIsWed = true, TwoHeadsOfHouses = true, HerHeartIsWon = true, HasAnHeir = true, HeirRidesWithThePlayer = true, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.PlayerIsWed);
      }

      // STAKE: giving up one's house for a lady who does not want you would be a hollow, irreversible sacrifice.
      [Test]
      public void GIVEN_her_heart_is_not_won_WHEN_asked_THEN_it_is_barred()
      {
         var f = new LeaveHouseFacts {TwoHeadsOfHouses = true, HerHeartIsWon = false, HasAnHeir = true, HeirRidesWithThePlayer = true, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.HerHeartIsNotWon);
      }

      // STAKE: with no heir the game ends the campaign when it loses its player; the house must have someone to take it.
      [Test]
      public void GIVEN_no_heir_of_age_WHEN_asked_THEN_it_is_barred()
      {
         var f = new LeaveHouseFacts {TwoHeadsOfHouses = true, HerHeartIsWon = true, HasAnHeir = false, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.NoHeir);
      }

      // STAKE: the hand-over keeps the player's party only if the heir is in it; elsewhere the wedding would disband it.
      [Test]
      public void GIVEN_the_heir_rides_elsewhere_WHEN_asked_THEN_the_heir_must_first_come_to_the_players_side()
      {
         var f = new LeaveHouseFacts {TwoHeadsOfHouses = true, HerHeartIsWon = true, HasAnHeir = true, HeirRidesWithThePlayer = false, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.HeirIsNotAtTheirSide);
      }

      // STAKE: a bar the game raises only after the hand-over would strand the player without house or bride.
      [Test]
      public void GIVEN_the_game_would_refuse_them_even_after_the_hand_over_WHEN_asked_THEN_nothing_is_handed_over()
      {
         var f = new LeaveHouseFacts {TwoHeadsOfHouses = true, HerHeartIsWon = true, HasAnHeir = true, HeirRidesWithThePlayer = true, TheGameWouldWedThemAfter = false};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.TheGameWouldRefuse);
      }

      // STAKE: the game's own wedding refuses an army or a battle; asked before, not discovered after.
      [Test]
      public void GIVEN_an_army_or_a_battle_WHEN_asked_THEN_it_is_barred()
      {
         var f = new LeaveHouseFacts {TwoHeadsOfHouses = true, HerHeartIsWon = true, HasAnHeir = true, HeirRidesWithThePlayer = true, ArmyOrBattle = true, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.ArmyOrBattle);
      }

      // STAKE: an ordinary lady (not heading a house) is wed the ordinary way; this road is not for her.
      [Test]
      public void GIVEN_she_heads_no_house_WHEN_asked_THEN_this_road_does_not_apply()
      {
         var f = new LeaveHouseFacts {TwoHeadsOfHouses = false, HerHeartIsWon = true, HasAnHeir = true, HeirRidesWithThePlayer = true, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.NotTwoHeadsOfHouses);
      }

      // STAKE: wed while still the player, the game pulls the empress into the player's clan and her empire loses its
      // ruler. The house and the player must pass to the heir first.
      [Test]
      public void GIVEN_the_hand_over_WHEN_ordered_THEN_the_house_and_the_player_pass_to_the_heir_before_the_wedding()
      {
         var steps = LeaveHouseForRulerPolicy.Steps.ToList();

         steps.IndexOf(LeaveHouseStep.HandOverTheHouse).Should().BeLessThan(steps.IndexOf(LeaveHouseStep.HandOverThePlayer));
         steps.IndexOf(LeaveHouseStep.HandOverThePlayer).Should().BeLessThan(steps.IndexOf(LeaveHouseStep.Wed));
         steps.IndexOf(LeaveHouseStep.Wed).Should().BeLessThan(steps.IndexOf(LeaveHouseStep.JoinHer));
      }

      // STAKE: the old character's workshops pass while he is still head (vanilla's retirement does it in that order).
      [Test]
      public void GIVEN_the_hand_over_WHEN_ordered_THEN_property_passes_before_the_house_does()
      {
         var steps = LeaveHouseForRulerPolicy.Steps.ToList();

         steps.IndexOf(LeaveHouseStep.HandOverWorkshops).Should().BeLessThan(steps.IndexOf(LeaveHouseStep.HandOverTheHouse));
         steps.IndexOf(LeaveHouseStep.HandOverCaravans).Should().BeLessThan(steps.IndexOf(LeaveHouseStep.HandOverTheHouse));
      }
   }
}
