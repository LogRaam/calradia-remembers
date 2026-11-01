// Code written by Gabriel Mailhot, 01/11/2026.
// ExtortionPolicy decides who the player has a real hold over, whether they give in, what they pay and what it costs (Gabriel,
// 23/10/2026; his "go", 01/11/2026). If it is wrong, the model's bluff empties a lord's purse, one purse can be milked every
// day, or an extortion costs nothing; the ledger, if wrong, loses the bargain a payment for lands made.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Extortion;
using CalradiaRemembers.Logic.Trauma;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class ExtortionPolicyTests
   {
      private static ExtortionResistanceFacts Secret(int honor = 0, int tiers = 0, bool ruler = false, int roguery = 0)
         => new() {Lever = ExtortionLever.Secret, Honor = honor, TiersAbove = tiers, IsRuler = ruler, PlayerRoguery = roguery};

      [Test]
      public void GIVEN_a_secret_the_player_really_knows_WHEN_the_hold_is_read_THEN_it_needs_no_army()
      {
         // STAKE: the lever on an affair works from a single rider: the hold is the knowledge, not the men.
         ExtortionPolicy.Lever(new ExtortionLeverFacts {SecretIsKnown = true, PlayerMen = 1, TheirMen = 500}).Should().Be(ExtortionLever.Secret);
      }

      [Test]
      public void GIVEN_nothing_verifiable_WHEN_the_hold_is_read_THEN_there_is_none()
      {
         // STAKE: Gabriel's "go" on a hold the game verifies: without it the verb is not even taught, so the model cannot invent a secret.
         ExtortionPolicy.Lever(new ExtortionLeverFacts {PlayerMen = 500, TheirMen = 1}).Should().Be(ExtortionLever.None);
      }

      [Test]
      public void GIVEN_a_village_in_reach_WHEN_the_player_outnumbers_them_THEN_the_threat_is_real()
      {
         // STAKE: "pay, or I burn your villages" means something only from men who could do it.
         ExtortionPolicy.Lever(new ExtortionLeverFacts {VillageInReach = true, PlayerMen = 100, TheirMen = 50}).Should().Be(ExtortionLever.Lands);
         ExtortionPolicy.Lever(new ExtortionLeverFacts {VillageInReach = true, PlayerMen = 99, TheirMen = 50}).Should().Be(ExtortionLever.None);
      }

      [Test]
      public void GIVEN_a_handful_of_men_WHEN_the_target_has_none_THEN_a_handful_burns_nothing()
      {
         // STAKE: two riders against a lord alone in a field are not a threat to his lands.
         ExtortionPolicy.Lever(new ExtortionLeverFacts {VillageInReach = true, PlayerMen = ExtortionPolicy.FewestMen - 1, TheirMen = 0}).Should().Be(ExtortionLever.None);
      }

      [Test]
      public void GIVEN_a_purse_WHEN_the_price_is_read_THEN_it_is_a_share_of_it()
      {
         // STAKE: Gabriel accepted a ceiling on a share of the purse: an extortion hurts and never strips them bare, unlike a robbery.
         ExtortionPolicy.MostTaken(10000).Should().Be(10000 * ExtortionPolicy.PurseSharePercent / 100);
         ExtortionPolicy.MostTaken(-5).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_huge_purse_WHEN_the_price_is_read_THEN_the_transfer_limit_holds()
      {
         // STAKE: no coin verb may pass the one limit every other transfer keeps.
         ExtortionPolicy.MostTaken(5000000).Should().Be(GoldTransferLimit.MaxSingleTransfer);
      }

      [Test]
      public void GIVEN_a_sum_past_the_ceiling_WHEN_it_is_paid_THEN_the_ceiling_decides()
      {
         // STAKE: the model names a figure; the game decides what moves.
         ExtortionPolicy.Take(9000, 10000).Should().Be(2000);
         ExtortionPolicy.Take(500, 10000).Should().Be(500);
         ExtortionPolicy.Take(null, 10000).Should().Be(2000);
      }

      [Test]
      public void GIVEN_a_poor_purse_WHEN_it_is_weighed_THEN_it_is_not_worth_a_threat()
      {
         // STAKE: a verb taught over a purse of a few denars would only cost the player a grudge for nothing.
         ExtortionPolicy.IsWorthAsking(ExtortionPolicy.FewestDenars * 100 / ExtortionPolicy.PurseSharePercent - 5).Should().BeFalse();
         ExtortionPolicy.IsWorthAsking(ExtortionPolicy.FewestDenars * 100 / ExtortionPolicy.PurseSharePercent).Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_attempt_this_month_WHEN_they_could_be_pressed_again_THEN_they_cannot_yet()
      {
         // STAKE: without this, a player talks to the same lord every day and drains a fifth of his purse each time.
         ExtortionPolicy.MayPressAgain(100, 100 + ExtortionPolicy.DaysBetweenAttempts - 1).Should().BeFalse();
         ExtortionPolicy.MayPressAgain(100, 100 + ExtortionPolicy.DaysBetweenAttempts).Should().BeTrue();
         ExtortionPolicy.MayPressAgain(null, 1).Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_honest_house_WHEN_pressed_THEN_it_gives_in_less()
      {
         // STAKE: Gabriel accepted honor and rank against the player: a blackmailer does not bend a man of honour as he bends a rogue.
         ExtortionPolicy.ChanceToYield(Secret(honor: 2)).Should().BeLessThan(ExtortionPolicy.ChanceToYield(Secret()));
         ExtortionPolicy.ChanceToYield(Secret(honor: -2)).Should().BeGreaterThan(ExtortionPolicy.ChanceToYield(Secret()));
      }

      [Test]
      public void GIVEN_a_crowned_head_of_a_great_house_WHEN_pressed_THEN_rank_stands_against_the_player()
      {
         // STAKE: same ruling: rank weighs, and a ruler least of all bows to a threat.
         ExtortionPolicy.ChanceToYield(Secret(tiers: 3, ruler: true)).Should().BeLessThan(ExtortionPolicy.ChanceToYield(Secret(tiers: 3)));
         ExtortionPolicy.ChanceToYield(Secret(tiers: 3)).Should().BeLessThan(ExtortionPolicy.ChanceToYield(Secret()));
      }

      [Test]
      public void GIVEN_a_house_below_the_players_WHEN_pressed_THEN_rank_adds_nothing()
      {
         // STAKE: rank only ever protects: the penalty never turns into a bonus.
         ExtortionPolicy.ChanceToYield(Secret(tiers: -3)).Should().Be(ExtortionPolicy.ChanceToYield(Secret()));
      }

      [Test]
      public void GIVEN_a_rogue_WHEN_pressing_THEN_roguery_helps_within_the_robbers_cap()
      {
         // STAKE: Gabriel accepted "la Roguerie aide, avec le meme plafond": craft never replaces the hold.
         ExtortionPolicy.ChanceToYield(Secret(roguery: 100)).Should().Be(ExtortionPolicy.ChanceToYield(Secret()) + 20);
         ExtortionPolicy.ChanceToYield(Secret(roguery: 500)).Should().Be(ExtortionPolicy.ChanceToYield(Secret()) + 20);
      }

      [Test]
      public void GIVEN_a_much_larger_host_WHEN_threatening_lands_THEN_the_gap_weighs()
      {
         // STAKE: Gabriel accepted a threat to lands that weighs by the gap in strength.
         int atTheBar = ExtortionPolicy.ChanceToYield(new ExtortionResistanceFacts {Lever = ExtortionLever.Lands, PlayerMen = 100, TheirMen = 50});
         int farPast = ExtortionPolicy.ChanceToYield(new ExtortionResistanceFacts {Lever = ExtortionLever.Lands, PlayerMen = 400, TheirMen = 50});

         atTheBar.Should().Be(ExtortionPolicy.EvenChance);
         farPast.Should().Be(ExtortionPolicy.EvenChance + ExtortionPolicy.MenBonusCap);
      }

      [Test]
      public void GIVEN_the_worst_of_everything_WHEN_pressed_THEN_nothing_is_ever_certain()
      {
         // STAKE: the same floor and ceiling as every violent attempt.
         ExtortionPolicy.ChanceToYield(Secret(honor: 2, tiers: 6, ruler: true)).Should().BeGreaterThanOrEqualTo(5);
         ExtortionPolicy.ChanceToYield(Secret(honor: -2, roguery: 300)).Should().BeLessThanOrEqualTo(95);
      }

      [Test]
      public void GIVEN_a_payment_WHEN_it_is_charged_THEN_regard_falls_and_a_grudge_stays()
      {
         // STAKE: Gabriel accepted that the victim pays and keeps a grudge strong enough for the grudge system to answer on its own.
         ExtortionCost cost = ExtortionPolicy.Cost(ExtortionLever.Secret, true, 0);

         cost.RelationHit.Should().Be(DeedRegardPolicy.ExtortedVictim);
         cost.GrudgeMagnitude.Should().Be(ExtortionPolicy.PaidGrudge);
      }

      [Test]
      public void GIVEN_a_refusal_WHEN_it_is_charged_THEN_it_still_costs_a_share()
      {
         // STAKE: Gabriel accepted 60%, as for a failed robbery: they remember that the player tried.
         ExtortionCost cost = ExtortionPolicy.Cost(ExtortionLever.Lands, false, 0);

         cost.RelationHit.Should().Be(-15);
         cost.GrudgeMagnitude.Should().Be(18);
         cost.CrimeRating.Should().Be(3);
      }

      [Test]
      public void GIVEN_the_same_victim_again_WHEN_it_is_charged_THEN_each_time_costs_more()
      {
         // STAKE: Gabriel accepted "chaque nouvelle fois coute plus cher": a purse is not a rent.
         ExtortionPolicy.Cost(ExtortionLever.Secret, true, 2).RelationHit.Should().Be(DeedRegardPolicy.ExtortedVictim * 2);
         ExtortionPolicy.Cost(ExtortionLever.Secret, true, 2).GrudgeMagnitude.Should().Be(ExtortionPolicy.PaidGrudge + 2 * ExtortionPolicy.RepeatGrudge);
         ExtortionPolicy.Cost(ExtortionLever.Secret, true, 50).GrudgeMagnitude.Should().Be(ExtortionPolicy.MostGrudge);
      }

      [Test]
      public void GIVEN_a_secret_WHEN_it_is_charged_THEN_no_crime_is_laid()
      {
         // STAKE: Gabriel accepted it: the victim cannot complain without telling what they are paying to hide.
         ExtortionPolicy.Cost(ExtortionLever.Secret, true, 0).CrimeRating.Should().Be(0);
         ExtortionPolicy.Cost(ExtortionLever.Lands, true, 0).CrimeRating.Should().Be(ExtortionPolicy.LandsCrime);
      }

      [Test]
      public void GIVEN_coin_taken_for_lands_WHEN_a_raid_comes_within_the_month_THEN_a_bargain_stands_to_be_broken()
      {
         // STAKE: Gabriel's "go" on the kept word: without it, the promise that earned the coin is worth nothing.
         var ledger = new ExtortionLedger();
         ledger.Attempts.Add(new ExtortionRecord {VictimId = "lucon", ClanId = "house", Lever = ExtortionLever.Lands, Day = 10, Amount = 500});

         ledger.BargainOver("house", 10 + ExtortionPolicy.DaysOfTheBargain).Should().NotBeNull();
         ledger.BargainOver("house", 10 + ExtortionPolicy.DaysOfTheBargain + 1).Should().BeNull();
         ledger.BargainOver("another", 11).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_refusal_or_a_secret_WHEN_lands_are_raided_THEN_no_word_was_given()
      {
         // STAKE: a refused threat promised nothing, nor did coin paid for a secret: the page must not call a raid a broken word.
         var ledger = new ExtortionLedger();
         ledger.Attempts.Add(new ExtortionRecord {VictimId = "a", ClanId = "house", Lever = ExtortionLever.Lands, Day = 10});
         ledger.Attempts.Add(new ExtortionRecord {VictimId = "b", ClanId = "house", Lever = ExtortionLever.Secret, Day = 10, Amount = 500});

         ledger.BargainOver("house", 11).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_bargain_already_broken_WHEN_more_is_raided_THEN_it_breaks_only_once()
      {
         // STAKE: three villages raided are one word broken, not three grudges for the same coin.
         var ledger = new ExtortionLedger();
         ledger.Attempts.Add(new ExtortionRecord {ClanId = "house", Lever = ExtortionLever.Lands, Day = 10, Amount = 500, BargainBroken = true});

         ledger.BargainOver("house", 11).Should().BeNull();
      }

      [Test]
      public void GIVEN_refused_attempts_WHEN_counted_THEN_they_spend_the_month_and_count_as_times()
      {
         // STAKE: a refusal is an attempt: otherwise a player re-rolls the same lord until he gives in.
         var ledger = new ExtortionLedger();
         ledger.Attempts.Add(new ExtortionRecord {VictimId = "lucon", Day = 10});
         ledger.Attempts.Add(new ExtortionRecord {VictimId = "lucon", Day = 50, Amount = 700});

         ledger.TimesPressed("lucon").Should().Be(2);
         ledger.LastDay("lucon").Should().Be(50);
         ledger.LastDay("nobody").Should().BeNull();
      }

      [Test]
      public void GIVEN_a_ledger_WHEN_saved_and_read_THEN_nothing_is_lost()
      {
         // STAKE: a reload must neither reopen a purse nor forget a bargain.
         var ledger = new ExtortionLedger();
         ledger.Attempts.Add(new ExtortionRecord {VictimId = "lucon", ClanId = "house", Lever = ExtortionLever.Lands, Day = 10, Amount = 500, BargainBroken = true});

         ExtortionRecord read = ExtortionLedger.Deserialize(ledger.Serialize()).Attempts[0];

         read.Lever.Should().Be(ExtortionLever.Lands);
         read.Amount.Should().Be(500);
         read.BargainBroken.Should().BeTrue();
      }

      [TestCase(null)]
      [TestCase("")]
      [TestCase("{broken")]
      public void GIVEN_an_old_or_broken_save_WHEN_read_THEN_no_attempt_is_held(string json)
      {
         // STAKE: the save-migration rule: a save from before this feature never bricks a campaign.
         ExtortionLedger.Deserialize(json).Attempts.Should().BeEmpty();
      }

      [Test]
      public void GIVEN_an_extortion_paid_WHEN_the_page_tells_it_THEN_it_counts_the_times_as_Gabriels_wish_reads()
      {
         // STAKE: the wish itself: the page tells that the player extorted this person, and how often.
         var deed = new MisdeedFacts {VictimId = "lucon", Kind = MisdeedKind.Extorted, Days = new[] {10, 50}};

         MisdeedsPagePolicy.Line(deed, "Lucon", false, d => $"day {d}").Should().StartWith("You extorted Lucon, twice");
      }

      [Test]
      public void GIVEN_a_bargain_broken_WHEN_the_page_tells_it_THEN_it_says_what_was_broken()
      {
         // STAKE: BrokeWord reads "on a task he gave you", which would be false here.
         var deed = new MisdeedFacts {VictimId = "lucon", Kind = MisdeedKind.BrokeExtortionBargain, Days = new[] {10}};

         MisdeedsPagePolicy.Line(deed, "Lucon", true, d => $"day {d}").Should().StartWith("You took Lucon's coin to spare her lands, then raided them");
      }

      [Test]
      public void GIVEN_any_adult_level_WHEN_the_page_is_read_THEN_extortion_is_on_it()
      {
         // STAKE: Gabriel's roadmap entry: told "at every adult level", unlike the captive deeds Hardcore keeps.
         MisdeedsPagePolicy.NeedsHardcore(MisdeedKind.Extorted).Should().BeFalse();
         MisdeedsPagePolicy.NeedsHardcore(MisdeedKind.BrokeExtortionBargain).Should().BeFalse();
      }
   }
}
