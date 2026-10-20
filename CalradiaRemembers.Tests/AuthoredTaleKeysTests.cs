// Code written by Gabriel Mailhot, 20/10/2026.
// Four keys tashmetu asked for while writing new tales (20/10/2026), approved by Gabriel the same day:
// requires.player.gender ("like the giver's"), a notable giver's trade and a town as the bound place ("players keep asking
// for tales from town merchants"), and gold the player pays in an ending ("a fine, a bribe, a child's keep"). If these are
// wrong, a tale written for a woman is offered to a man, a merchant's tale is told by a headman, a tale bound to a town
// cannot load, or a player is offered a bribe he cannot pay and the tale tells him he paid it.

#region

using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredTaleKeysTests
   {
      private static string WithRequires(string requires) => AuthoredTales.Fork.Replace("\"binds\":", "\"requires\": " + requires + ",\n        \"binds\":");

      // STAKE: "requires.player.gender, like the giver's": a tale told to a woman is never offered to a man, nor the reverse.
      [Test]
      public void GIVEN_a_tale_told_to_a_woman_WHEN_the_player_is_a_man_or_a_woman_THEN_only_the_woman_is_offered_it()
      {
         var asked = new PlayerRequirement {Gender = "female"};

         AuthoredPlayerPolicy.WhyNot(asked, new AuthoredPlayerFacts {IsFemale = false}).Should().NotBeNull();
         AuthoredPlayerPolicy.WhyNot(asked, new AuthoredPlayerFacts {IsFemale = true}).Should().BeNull();
      }

      // STAKE: a tale that names no sex for the player is offered to everyone, as every tale written before this key was;
      // and the clan tier it asks for still holds.
      [Test]
      public void GIVEN_a_tale_naming_no_sex_for_the_player_WHEN_weighed_THEN_either_player_fits_and_the_tier_still_counts()
      {
         AuthoredPlayerPolicy.WhyNot(new PlayerRequirement(), new AuthoredPlayerFacts {IsFemale = true}).Should().BeNull();
         AuthoredPlayerPolicy.WhyNot(new PlayerRequirement {MinClanTier = 3}, new AuthoredPlayerFacts {ClanTier = 2}).Should().Contain("tier");
      }

      // STAKE: the author writes the key as for the giver; a misspelt sex must not load as "either" and reach everyone.
      [Test]
      public void GIVEN_a_misspelt_player_sex_WHEN_the_tale_is_read_THEN_the_loader_refuses_it()
      {
         AuthoredTales.Read(WithRequires("{ \"player\": { \"gender\": \"woman\" } }")).Problems.Should().Contain(p => p.Contains("requires.player.gender"));
         AuthoredTales.Load(WithRequires("{ \"player\": { \"gender\": \"female\" } }")).Requires.Player.Gender.Should().Be("female");
      }

      // STAKE: a merchant's tale is told by a merchant, never by the village headman; a list allows several trades.
      [Test]
      public void GIVEN_a_tale_for_merchants_WHEN_notables_of_different_trades_are_weighed_THEN_only_the_merchant_fits()
      {
         var g = new GiverRequirement {Role = "notable", Trade = {"merchant"}};

         AuthoredGiverPolicy.WhyNot(g, new GiverFacts {IsNotable = true, Trade = "merchant"}).Should().BeNull();
         AuthoredGiverPolicy.WhyNot(g, new GiverFacts {IsNotable = true, Trade = "headman"}).Should().Contain("merchant");
         AuthoredGiverPolicy.WhyNot(new GiverRequirement {Role = "notable", Trade = {"merchant", "artisan"}}, new GiverFacts {IsNotable = true, Trade = "artisan"})
            .Should().BeNull();
      }

      // STAKE: only a notable plies a trade; a tale asking a lord for one could never be offered, so it does not load.
      [Test]
      public void GIVEN_a_trade_asked_of_a_lord_or_an_unknown_trade_WHEN_the_tale_is_read_THEN_the_loader_refuses_it()
      {
         AuthoredTales.Read(WithRequires("{ \"giver\": { \"role\": \"lord\", \"trade\": \"merchant\" } }")).Problems.Should().Contain(p => p.Contains("only a notable"));
         AuthoredTales.Read(WithRequires("{ \"giver\": { \"role\": \"notable\", \"trade\": \"baker\" } }")).Problems.Should().Contain(p => p.Contains("no trade"));
         AuthoredTales.Load(WithRequires("{ \"giver\": { \"role\": \"notable\", \"trade\": [\"merchant\", \"artisan\"] } }")).Requires.Giver.Trade
            .Should().Equal("merchant", "artisan");
      }

      // STAKE: a town may be the bound place; anything else the game cannot bind is refused rather than left unbound.
      [Test]
      public void GIVEN_a_place_of_a_kind_WHEN_the_tale_is_read_THEN_a_village_or_a_town_loads_and_a_castle_does_not()
      {
         AuthoredTales.Load(WithRequires("{ \"village\": { \"type\": \"town\" } }")).Requires.Village.Type.Should().Be("town");
         AuthoredTales.Read(WithRequires("{ \"village\": { \"type\": \"castle\" } }")).Problems.Should().Contain(p => p.Contains("requires.village.type"));
      }

      // STAKE: gating before declaration. A player without the gold is never offered the way to an ending that makes him pay;
      // with it, he is. A way to a stage costs nothing yet.
      [Test]
      public void GIVEN_an_ending_the_player_pays_for_WHEN_the_choices_are_listed_THEN_the_way_there_is_offered_only_with_the_gold()
      {
         AuthoredQuest q = AuthoredTales.Load(AuthoredTales.Fork.Replace("\"abandoned\": { \"regard\": -4,", "\"abandoned\": { \"regard\": -4, \"gold\": -50,"));
         AuthoredQuestRun run = AuthoredTales.At(q, "the_road");
         System.Func<int> keep = AuthoredQuestEngine.PlayerGold;
         try
         {
            AuthoredQuestEngine.PlayerGold = () => 10;
            AuthoredQuestEngine.OpenChoices(run, q).Select(c => c.Id).Should().Equal("ride_out");

            AuthoredQuestEngine.PlayerGold = () => 50;
            AuthoredQuestEngine.OpenChoices(run, q).Select(c => c.Id).Should().Equal("ride_out", "give_up", "let_it_be");
         }
         finally
         {
            AuthoredQuestEngine.PlayerGold = keep;
         }
      }

      // STAKE: an ending that pays the player, or a way to a stage, never closes a choice, whatever the purse.
      [Test]
      public void GIVEN_a_paid_reward_or_a_way_to_a_stage_WHEN_costed_THEN_it_costs_the_player_nothing()
      {
         AuthoredQuest q = AuthoredTales.Load();
         AuthoredStage road = q.Stages.Single(s => s.Id == "the_road");

         AuthoredQuestEngine.CostOf(road.Choices.Single(c => c.Id == "ride_out"), q).Should().Be(0);
         AuthoredQuestEngine.CostOf(new AuthoredChoice {Next = "end:rescued"}, q).Should().Be(0);
         AuthoredQuestEngine.CanPay(0, 0).Should().BeTrue();
      }
   }
}
