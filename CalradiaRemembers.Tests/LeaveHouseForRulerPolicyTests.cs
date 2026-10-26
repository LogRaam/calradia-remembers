// Code written by Gabriel Mailhot, 26/10/2026.
// LeaveHouseForRulerPolicy decides whether the player may leave their house for a ruler who heads hers (the heir takes
// it up and becomes the played character, the old character weds her), and the order the hand-over runs in. Gabriel,
// 26/10/2026: "Jamais elle laissera son trone [...] le joueur aura le choix soit d'abandonner son personnage et
// continuer avec l'heritier". It cannot be undone: if this is wrong, a player gives up their character and house for a
// wedding the game then refuses, or the wedding pulls the empress out of her own house and leaves her empire headless.

#region

using System.Linq;
using CalradiaRemembers.Logic.Marriage;
using NpcMemoryService.Core.Models;
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

      // STAKE: Gabriel (26/10/2026): giving her all is "aussi l'option si aucun heritier". Without an heir that road must
      // stay open, or a player with no child of age has no road to her hand at all.
      [Test]
      public void GIVEN_no_heir_WHEN_the_player_would_give_her_all_THEN_the_road_is_open()
      {
         var f = new LeaveHouseFacts {Road = LeaveHouseRoad.GiveHerAll, TwoHeadsOfHouses = true, HerHeartIsWon = true, HasAnHeir = false, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.None);
      }

      // STAKE: giving her all still asks for her heart: a ruler takes no husband she does not want, however rich his gift.
      [Test]
      public void GIVEN_her_heart_not_won_WHEN_the_player_would_give_her_all_THEN_it_is_barred()
      {
         var f = new LeaveHouseFacts {Road = LeaveHouseRoad.GiveHerAll, TwoHeadsOfHouses = true, HerHeartIsWon = false, TheGameWouldWedThemAfter = true};

         LeaveHouseForRulerPolicy.WhyNot(f).Should().Be(LeaveHouseBar.HerHeartIsNotWon);
      }

      // STAKE: a house that leaves its realm before giving its fiefs away hands them all to that realm's ruler, and the
      // empress receives nothing. The fiefs, and the realm's other houses, must be hers before the player's house moves.
      [Test]
      public void GIVEN_giving_her_all_WHEN_ordered_THEN_the_fiefs_and_the_realm_are_hers_before_the_house_joins_her_realm()
      {
         var steps = LeaveHouseForRulerPolicy.GiveHerAllSteps.ToList();

         steps.IndexOf(GiveHerAllStep.GiveTheFiefs).Should().BeLessThan(steps.IndexOf(GiveHerAllStep.JoinHerRealm));
         steps.IndexOf(GiveHerAllStep.AbsorbTheRealm).Should().BeLessThan(steps.IndexOf(GiveHerAllStep.JoinHerRealm));
      }

      // STAKE: wed while still a king, he would read as one and Rhagaea's "two crowns" union would fire over a crown he
      // has just given her; and the influence the change of realm wipes is given back after it, not before.
      [Test]
      public void GIVEN_giving_her_all_WHEN_ordered_THEN_the_wedding_follows_the_change_of_realm_and_the_influence_is_given_back_after_it()
      {
         var steps = LeaveHouseForRulerPolicy.GiveHerAllSteps.ToList();

         steps.IndexOf(GiveHerAllStep.JoinHerRealm).Should().BeLessThan(steps.IndexOf(GiveHerAllStep.Wed));
         steps.IndexOf(GiveHerAllStep.JoinHerRealm).Should().BeLessThan(steps.IndexOf(GiveHerAllStep.RestoreTheInfluence));
      }

      // STAKE: Gabriel's season of wages keeps his house's troops paid while it has no fiefs left: sixty days of what the
      // whole house pays each day.
      [Test]
      public void GIVEN_the_house_pays_its_troops_daily_WHEN_she_gives_a_season_THEN_it_covers_sixty_days()
      {
         LeaveHouseForRulerPolicy.SeasonOfWages(150).Should().Be(9000);
         LeaveHouseForRulerPolicy.SeasonOfWages(0).Should().Be(0);
      }

      // STAKE: Gabriel: the war "depends" on what the player brings. A realm handed over, or a defection from a rival
      // empire, is near certain to be answered.
      [Test]
      public void GIVEN_a_realm_or_a_defection_from_a_rival_WHEN_the_union_is_sealed_THEN_war_is_near_certain()
      {
         LeaveHouseForRulerPolicy.WarChance(new UnionFacts {BringsARealm = true}).Should().BeGreaterThanOrEqualTo(90);
         LeaveHouseForRulerPolicy.WarChance(new UnionFacts {DefectsFromARival = true}).Should().BeGreaterThanOrEqualTo(90);
      }

      // STAKE: a landless house alarms nobody; each fief it brings makes the rivals likelier to answer, never certain.
      [Test]
      public void GIVEN_only_fiefs_WHEN_the_union_is_sealed_THEN_war_grows_with_them_but_a_landless_house_brings_none()
      {
         LeaveHouseForRulerPolicy.WarChance(new UnionFacts {FiefsBrought = 0}).Should().Be(0);
         LeaveHouseForRulerPolicy.WarChance(new UnionFacts {FiefsBrought = 2}).Should().BeGreaterThan(LeaveHouseForRulerPolicy.WarChance(new UnionFacts {FiefsBrought = 1}));
         LeaveHouseForRulerPolicy.WarChance(new UnionFacts {FiefsBrought = 20}).Should().BeLessThan(90);
      }

      // STAKE: Gabriel: the children of the union are "dans sa lignee". The game puts every child of the player in the
      // player's house, so without this the empress's heirs would be born outside her own house.
      [Test]
      public void GIVEN_a_wife_who_kept_her_own_house_WHEN_she_gives_birth_THEN_the_child_belongs_to_her_line()
      {
         LeaveHouseForRulerPolicy.ChildBelongsToHerHouse(new UnionChildFacts {MotherIsThePlayersWife = true, MotherKeepsHerOwnHouse = true}).Should().BeTrue();
      }

      // STAKE: an ordinary wife lives in the player's house; her children stay the player's, as the game already makes them.
      [Test]
      public void GIVEN_a_wife_in_the_players_house_WHEN_she_gives_birth_THEN_the_child_stays_in_the_players_house()
      {
         LeaveHouseForRulerPolicy.ChildBelongsToHerHouse(new UnionChildFacts {MotherIsThePlayersWife = true, MotherKeepsHerOwnHouse = false}).Should().BeFalse();
      }

      // STAKE: the Ira report's lesson on her road: a player who resolves to leave their house for her while her heart is not
      // won, or while a war or a campaign stands between them, must hear what is missing rather than silence.
      [Test]
      public void GIVEN_a_bar_the_player_could_mend_WHEN_she_speaks_THEN_she_names_it()
      {
         LeaveHouseForRulerPolicy.ObstacleToName(LeaveHouseBar.HerHeartIsNotWon).Should().Be(LeaveHouseObstacle.HerHeartIsNotWon);
         LeaveHouseForRulerPolicy.ObstacleToName(LeaveHouseBar.WarOrCaptivity).Should().Be(LeaveHouseObstacle.WarOrCaptivity);
         LeaveHouseForRulerPolicy.ObstacleToName(LeaveHouseBar.ArmyOrBattle).Should().Be(LeaveHouseObstacle.ArmyOrBattle);
      }

      // STAKE: an open road needs no excuse, and a rule of the game's own she has no words for stays unsaid.
      [Test]
      public void GIVEN_the_road_open_or_a_bar_she_has_no_words_for_WHEN_she_speaks_THEN_she_names_nothing()
      {
         LeaveHouseForRulerPolicy.ObstacleToName(LeaveHouseBar.None).Should().Be(LeaveHouseObstacle.None);
         LeaveHouseForRulerPolicy.ObstacleToName(LeaveHouseBar.TheGameWouldRefuse).Should().Be(LeaveHouseObstacle.None);
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
