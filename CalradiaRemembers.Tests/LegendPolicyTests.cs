// Code written by Gabriel Mailhot, 01/11/2026.
// LegendPolicy decides which battles pass into legend, who has heard of them, and what each character carries into a talk (DepaHN,
// Nexus, 27/10/2026; Gabriel's "go", 01/11/2026). If it is wrong, every scrap is a legend and none means anything, a looter rout
// is sung in the halls, a stranger knows of a deed on the day it happened, or a teller changes who won, which NPCs never do.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Legends;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class LegendPolicyTests
   {
      private static LegendBattleFacts Battle(int winners, int losers, int losses = 0, bool garrison = false)
         => new() {WinnerMen = winners, LoserMen = losers, WinnerLosses = losses, GarrisonWon = garrison, LordsOnBothSides = true};

      private static Legend Tale(int day, string name = "The Holding of Rovalt", LegendKind kind = LegendKind.SiegeHeld)
         => new() {Kind = kind, Name = name, Day = day, HeroName = "Derthert", PlaceName = "Rovalt", OpposingRealmName = "Sturgia", FewMen = 120, ManyMen = 450, Toll = 200};

      [Test]
      public void GIVEN_a_field_won_at_one_against_three_WHEN_it_ends_THEN_it_is_a_legend()
      {
         // STAKE: DepaHN's wish, and the bar Gabriel accepted: a victory against the odds.
         LegendPolicy.Classify(Battle(100, LegendPolicy.OddsAgainst * 100)).Should().Be(LegendKind.VictoryAgainstTheOdds);
      }

      [Test]
      public void GIVEN_a_close_fight_WHEN_it_ends_THEN_it_is_an_ordinary_battle()
      {
         // STAKE: a legend must stay extraordinary; a hard win at two against three is what a war is made of.
         LegendPolicy.Classify(Battle(100, LegendPolicy.OddsAgainst * 100 - 1)).Should().BeNull();
      }

      [Test]
      public void GIVEN_garrison_holding_an_assault_at_those_odds_WHEN_it_ends_THEN_a_siege_was_held()
      {
         // STAKE: DepaHN: "a siege won against the odds".
         LegendPolicy.Classify(Battle(150, 500, garrison: true)).Should().Be(LegendKind.SiegeHeld);
      }

      [Test]
      public void GIVEN_an_overwhelmed_side_that_cost_the_enemy_dearly_WHEN_it_ends_THEN_it_is_a_last_stand()
      {
         // STAKE: DepaHN: "a heroic last stand": a beaten side can be the one remembered.
         LegendPolicy.Classify(Battle(600, 100, 150)).Should().Be(LegendKind.LastStand);
      }

      [Test]
      public void GIVEN_an_overwhelmed_side_that_cost_little_WHEN_it_ends_THEN_it_is_merely_a_defeat()
      {
         // STAKE: being crushed is no legend; only the price paid makes it one.
         LegendPolicy.Classify(Battle(600, 100, 149)).Should().BeNull();
      }

      [Test]
      public void GIVEN_too_few_men_WHEN_the_odds_are_long_THEN_it_is_a_skirmish()
      {
         // STAKE: ten men beating thirty would otherwise be sung across Calradia.
         LegendPolicy.Classify(Battle(LegendPolicy.FewestMen - 1, 300)).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_side_no_lord_led_WHEN_the_odds_are_long_THEN_nobody_sings_of_it()
      {
         // STAKE: Gabriel accepted "against a lord, never against looters".
         LegendPolicy.Classify(new LegendBattleFacts {WinnerMen = 100, LoserMen = 400, LordsOnBothSides = false}).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_legend_this_season_WHEN_a_lords_deed_comes_THEN_it_waits_and_the_players_does_not()
      {
         // STAKE: at most one a season keeps it rare; but a lord's battle across the map must not swallow what the player just did.
         LegendPolicy.MayRaise(100, 100 + LegendPolicy.DaysBetweenLegends - 1, false).Should().BeFalse();
         LegendPolicy.MayRaise(100, 100 + LegendPolicy.DaysBetweenLegends, false).Should().BeTrue();
         LegendPolicy.MayRaise(100, 101, true).Should().BeTrue();
         LegendPolicy.MayRaise(null, 1, false).Should().BeTrue();
      }

      [TestCase(LegendKind.SiegeHeld, "The Holding of Rovalt")]
      [TestCase(LegendKind.VictoryAgainstTheOdds, "The Day of Rovalt")]
      [TestCase(LegendKind.LastStand, "The Last Stand of Derthert")]
      public void GIVEN_a_deed_WHEN_it_is_named_THEN_the_name_follows_the_kind(LegendKind kind, string expected)
      {
         // STAKE: the name is the game's and never changes, so every teller speaks of the same thing.
         LegendPolicy.Name(kind, "Rovalt", "Derthert").Should().Be(expected);
      }

      [Test]
      public void GIVEN_a_field_no_settlement_names_WHEN_it_is_named_THEN_the_hero_carries_it()
      {
         // STAKE: a field far from any town must not read "The Day of ".
         LegendPolicy.Name(LegendKind.VictoryAgainstTheOdds, null, "Derthert").Should().Be("The Day of Derthert");
      }

      [Test]
      public void GIVEN_several_ties_WHEN_the_bond_is_read_THEN_the_closest_counts()
      {
         // STAKE: DepaHN: told "differently depending on their connection": a veteran of his realm speaks as a veteran.
         LegendPolicy.BondOf(new LegendTellerFacts {FoughtBeside = true, SameRealm = true}).Should().Be(LegendBond.FoughtBeside);
         LegendPolicy.BondOf(new LegendTellerFacts {IsTheHero = true, FoughtBeside = true}).Should().Be(LegendBond.OwnDeed);
         LegendPolicy.BondOf(new LegendTellerFacts()).Should().Be(LegendBond.Stranger);
      }

      [Test]
      public void GIVEN_a_fresh_legend_WHEN_word_travels_THEN_it_reaches_the_realms_and_then_everyone()
      {
         // STAKE: Gabriel accepted the spread: those who were there, then the realm, then all Calradia.
         LegendPolicy.HasHeard(LegendBond.FoughtBeside, 0).Should().BeTrue();
         LegendPolicy.HasHeard(LegendBond.SameRealm, LegendPolicy.DaysToReachTheRealms - 1).Should().BeFalse();
         LegendPolicy.HasHeard(LegendBond.SameRealm, LegendPolicy.DaysToReachTheRealms).Should().BeTrue();
         LegendPolicy.HasHeard(LegendBond.Stranger, LegendPolicy.DaysToReachCalradia - 1).Should().BeFalse();
         LegendPolicy.HasHeard(LegendBond.Stranger, LegendPolicy.DaysToReachCalradia).Should().BeTrue();
      }

      [Test]
      public void GIVEN_many_legends_WHEN_a_character_speaks_THEN_they_carry_the_one_closest_to_them()
      {
         // STAKE: Gabriel accepted one legend per character: several would make every talk a recital.
         var mine = Tale(10, "Mine");
         var newer = Tale(50, "Newer");
         Legend chosen = LegendPolicy.Closest(new[] {newer, mine}, l => l == mine ? LegendBond.FoughtBeside : LegendBond.Stranger, 100);

         chosen.Should().BeSameAs(mine);
      }

      [Test]
      public void GIVEN_two_legends_as_close_WHEN_a_character_speaks_THEN_the_newest_is_told()
      {
         // STAKE: this is the fixed tie-break, so a character does not flip between two tales from one talk to the next.
         var old = Tale(10, "Old");
         var recent = Tale(50, "Recent");

         LegendPolicy.Closest(new[] {old, recent}, l => LegendBond.Stranger, 100).Should().BeSameAs(recent);
      }

      [Test]
      public void GIVEN_only_a_legend_not_yet_heard_WHEN_a_character_speaks_THEN_they_carry_none()
      {
         // STAKE: a stranger must not know on the day of the battle what a courier takes weeks to carry.
         LegendPolicy.Closest(new[] {Tale(99)}, l => LegendBond.Stranger, 100).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_teller_WHEN_the_prompt_is_read_THEN_the_facts_are_fixed_and_the_stance_is_theirs()
      {
         // STAKE: Gabriel's ruling: tone, stress and believed reasons may differ, never who won, where, or how many.
         string beaten = LegendPolicy.PromptLine(Tale(10), LegendBond.FoughtAgainst, "spring 1091");
         string proud = LegendPolicy.PromptLine(Tale(10), LegendBond.FoughtBeside, "spring 1091");

         beaten.Should().Contain("Derthert held Rovalt with 120 men against 450 of Sturgia");
         proud.Should().Contain("Derthert held Rovalt with 120 men against 450 of Sturgia");
         beaten.Should().Contain("rankles").And.Contain("never change who won, where, or the numbers");
         proud.Should().Contain("with pride");
      }

      [Test]
      public void GIVEN_a_last_stand_WHEN_it_is_told_THEN_the_field_lost_is_part_of_it()
      {
         // STAKE: a last stand told as a victory would be a lie the prompt put in a character's mouth.
         LegendPolicy.Deed(Tale(10, kind: LegendKind.LastStand)).Should().Contain("lost the field").And.Contain("200 men");
      }

      [Test]
      public void GIVEN_a_ledger_WHEN_saved_and_read_THEN_the_legends_keep_their_names_and_witnesses()
      {
         // STAKE: DepaHN's legends outlive those in them: names must survive a hero's death or a realm's fall, and a reload.
         var ledger = new LegendLedger();
         Legend tale = Tale(10);
         tale.BesideIds = new List<string> {"witness"};
         ledger.Legends.Add(tale);

         LegendLedger read = LegendLedger.Deserialize(ledger.Serialize());

         read.Legends.Should().ContainSingle(l => l.Name == "The Holding of Rovalt" && l.HeroName == "Derthert" && l.Kind == LegendKind.SiegeHeld);
         read.Legends[0].BesideIds.Should().Contain("witness");
         read.LastDay.Should().Be(10);
      }

      [TestCase(null)]
      [TestCase("")]
      [TestCase("{broken")]
      public void GIVEN_an_old_or_broken_save_WHEN_read_THEN_there_is_no_legend_yet(string json)
      {
         // STAKE: the save-migration rule: a save from before this feature never bricks a campaign.
         LegendLedger.Deserialize(json).Legends.Should().BeEmpty();
      }
   }
}
