// Code written by Gabriel Mailhot, 01/11/2026.
// HelpPleaPolicy decides who may call on the player for help (DepaHN, Nexus, 27/10/2026), what they ask and what the outcome
// leaves behind. If it is wrong, strangers beg, a player is punished for what they could not do or never received, or
// relationships carry no consequence after all: "a brother under siege whose letter goes unanswered remembers it".

#region

using CalradiaRemembers.Logic.HelpPleas;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.HelpPleas
{
   [TestFixture]
   public class HelpPleaPolicyTests
   {
      private static HelpWriterFacts Writer(int regard = 40, bool kin = false, bool ally = false, bool met = true, bool clan = false, bool war = false)
         => new HelpWriterFacts {HasMet = met, Regard = regard, IsKin = kin, IsWarAlly = ally, InThePlayersClan = clan, AtWarWithThePlayer = war};

      [Test]
      public void GIVEN_a_true_friend_WHEN_a_crisis_comes_THEN_they_may_write()
      {
         // STAKE: DepaHN's wish itself: friends write when they need help.
         HelpPleaPolicy.MayWrite(Writer(HelpPleaPolicy.FriendFloor)).Should().BeTrue();
      }

      [Test]
      public void GIVEN_someone_merely_civil_WHEN_a_crisis_comes_THEN_they_do_not_write()
      {
         // STAKE: DepaHN, 26/10/2026, complained of letters from strangers; a plea is a claim on a bond, so it needs one.
         HelpPleaPolicy.MayWrite(Writer(HelpPleaPolicy.FriendFloor - 1)).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_war_ally_WHEN_their_regard_is_only_friendly_THEN_they_may_write()
      {
         // STAKE: the wish names allies; the shared war is the bond, a lower regard is enough.
         HelpPleaPolicy.MayWrite(Writer(HelpPleaPolicy.AllyFloor, ally: true)).Should().BeTrue();
      }

      [Test]
      public void GIVEN_kin_WHEN_their_regard_is_cool_but_not_sour_THEN_they_may_write()
      {
         // STAKE: family asks where a friend could not; only a kinsman who truly soured stays silent.
         HelpPleaPolicy.MayWrite(Writer(HelpPleaPolicy.KinFloor, kin: true)).Should().BeTrue();
         HelpPleaPolicy.MayWrite(Writer(HelpPleaPolicy.KinFloor - 1, kin: true)).Should().BeFalse();
      }

      [Test]
      public void GIVEN_someone_never_met_WHEN_their_regard_is_high_THEN_they_do_not_write()
      {
         // STAKE: DepaHN, 26/10/2026: regard can rise for a man never met; only someone the player knows writes unbidden.
         HelpPleaPolicy.MayWrite(Writer(met: false)).Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_players_own_clan_or_an_enemy_WHEN_a_crisis_comes_THEN_nobody_writes()
      {
         // STAKE: the player's own clan's trouble is already the player's; an enemy could not be helped without treason.
         HelpPleaPolicy.MayWrite(Writer(clan: true)).Should().BeFalse();
         HelpPleaPolicy.MayWrite(Writer(war: true)).Should().BeFalse();
      }

      [Test]
      public void GIVEN_kin_and_a_warmer_friend_WHEN_one_must_write_THEN_the_kinsman_does()
      {
         // STAKE: one crisis, one letter: a house's whole family must not write at once, and blood comes first.
         HelpPleaPolicy.WriterRank(Writer(0, kin: true)).Should().BeGreaterThan(HelpPleaPolicy.WriterRank(Writer(90)));
      }

      [Test]
      public void GIVEN_a_crisis_already_pleaded_WHEN_a_plea_would_open_THEN_it_does_not()
      {
         // STAKE: the roadmap's "never a flood of pleas": one crisis asks once.
         HelpPleaPolicy.MayOpen(new HelpOpeningFacts {Today = 100, CrisisAlreadyPleaded = true}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_writer_who_asked_recently_WHEN_a_new_crisis_comes_THEN_they_wait()
      {
         // STAKE: a friend who always wants something is a bore, and the wait keeps each letter an event.
         HelpPleaPolicy.MayOpen(new HelpOpeningFacts {Today = 100, LastPleaDay = 100 - HelpPleaPolicy.DaysBetweenPleas + 1}).Should().BeFalse();
         HelpPleaPolicy.MayOpen(new HelpOpeningFacts {Today = 100, LastPleaDay = 100 - HelpPleaPolicy.DaysBetweenPleas}).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_most_pleas_already_open_WHEN_another_would_open_THEN_it_waits()
      {
         // STAKE: in a great war many friends suffer at once; the player must not be buried under them.
         HelpPleaPolicy.MayOpen(new HelpOpeningFacts {Today = 100, OpenPleas = HelpPleaPolicy.MaxOpenPleas}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_each_crisis_WHEN_asked_what_it_needs_THEN_it_asks_what_fits()
      {
         // STAKE: the wish: "depending on their situation". Coin does not lift a siege.
         HelpPleaPolicy.AskFor(HelpCrisis.Besieged).Should().Be(HelpAsk.Relief);
         HelpPleaPolicy.AskFor(HelpCrisis.Starving).Should().Be(HelpAsk.Food);
         HelpPleaPolicy.AskFor(HelpCrisis.Ruined).Should().Be(HelpAsk.Coin);
         HelpPleaPolicy.AskFor(HelpCrisis.BattleLost).Should().Be(HelpAsk.Coin);
         HelpPleaPolicy.AskFor(HelpCrisis.FiefLost).Should().Be(HelpAsk.Coin);
      }

      [Test]
      public void GIVEN_a_rich_house_WHEN_it_lost_a_fight_THEN_it_does_not_beg()
      {
         // STAKE: a lord with a fortune asking the player for 500 denars reads as a bug, not as distress.
         HelpPleaPolicy.HouseIsInNeed(HelpCrisis.BattleLost, HelpPleaPolicy.NeedyGold).Should().BeFalse();
         HelpPleaPolicy.HouseIsInNeed(HelpCrisis.BattleLost, HelpPleaPolicy.NeedyGold - 1).Should().BeTrue();
      }

      [TestCase(2000, 0)]
      [TestCase(2500, 500)]
      [TestCase(12340, 2400)]
      [TestCase(900000, HelpPleaPolicy.MaxCoin)]
      public void GIVEN_the_players_wealth_WHEN_a_sum_is_asked_THEN_it_fits_what_they_have(int gold, int asked)
      {
         // STAKE: the wish: "what the player has". Nobody asks a pauper, and the sum stays a friend's, not a tax.
         HelpPleaPolicy.CoinAsked(gold).Should().Be(asked);
      }

      [Test]
      public void GIVEN_a_captive_player_WHEN_any_help_is_asked_THEN_they_cannot()
      {
         // STAKE: Gabriel's default: "I cannot" costs nothing when the game can see it is so.
         HelpPleaPolicy.IncapacityOf(new HelpMeansFacts {Ask = HelpAsk.Food, IsCaptive = true}).Should().Be(HelpIncapacity.Captive);
      }

      [Test]
      public void GIVEN_the_players_own_walls_under_siege_or_too_few_men_WHEN_relief_is_asked_THEN_they_cannot()
      {
         // STAKE: nobody blames a lord for defending his own, or a man with a dozen riders for not lifting a siege.
         HelpPleaPolicy.IncapacityOf(new HelpMeansFacts {Ask = HelpAsk.Relief, OwnFiefBesieged = true, PartyMen = 100}).Should().Be(HelpIncapacity.OwnFiefBesieged);
         HelpPleaPolicy.IncapacityOf(new HelpMeansFacts {Ask = HelpAsk.Relief, PartyMen = HelpPleaPolicy.MinMenToRide - 1}).Should().Be(HelpIncapacity.TooFewMen);
         HelpPleaPolicy.IncapacityOf(new HelpMeansFacts {Ask = HelpAsk.Relief, PartyMen = HelpPleaPolicy.MinMenToRide}).Should().Be(HelpIncapacity.None);
      }

      [Test]
      public void GIVEN_too_little_gold_WHEN_coin_is_asked_THEN_they_cannot()
      {
         // STAKE: the sum was fair when sealed, but the purse may have emptied since; the Send button must not overdraw it.
         HelpPleaPolicy.IncapacityOf(new HelpMeansFacts {Ask = HelpAsk.Coin, Gold = 499, Amount = 500}).Should().Be(HelpIncapacity.TooPoor);
      }

      [Test]
      public void GIVEN_a_letter_that_never_arrived_WHEN_the_crisis_closes_THEN_nothing_is_held_against_the_player()
      {
         // STAKE: a grudge for a letter the player never saw is the mod punishing them for a courier's road.
         HelpVerdict verdict = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = false, Disaster = true});

         verdict.Feeling.Should().Be(HelpFeeling.Nothing);
         verdict.RegardDelta.Should().Be(0);
         verdict.GrudgeMagnitude.Should().Be(0);
      }

      [Test]
      public void GIVEN_help_that_came_WHEN_nothing_was_answered_THEN_the_deed_counts()
      {
         // STAKE: Gabriel, 01/11/2026: the game judges the deed. A player who throws the letter and rides anyway is thanked.
         HelpVerdict verdict = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true, HelpCame = true});

         verdict.Feeling.Should().Be(HelpFeeling.Thankful);
         verdict.RegardDelta.Should().BePositive();
         verdict.KindnessMagnitude.Should().BePositive();
      }

      [Test]
      public void GIVEN_a_letter_thrown_away_WHEN_the_place_fell_THEN_the_grievance_is_heavy()
      {
         // STAKE: Gabriel's default: "the abandonment happened, even if you did not read it", and it weighs what followed.
         HelpVerdict fell = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true, Disaster = true});
         HelpVerdict held = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true});

         fell.Feeling.Should().Be(HelpFeeling.Abandoned);
         fell.GrudgeMagnitude.Should().BeGreaterThan(held.GrudgeMagnitude);
         held.GrudgeMagnitude.Should().BeGreaterThanOrEqualTo(15, "GrudgePolicy.MagnitudeFloor would drop anything smaller");
      }

      [Test]
      public void GIVEN_a_refusal_the_player_could_have_avoided_WHEN_the_crisis_closes_THEN_it_is_a_small_disappointment()
      {
         // STAKE: the design: "if you refuse saying so, a small disappointment": no lasting grievance for an honest no.
         HelpVerdict verdict = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true, Answer = HelpAnswer.Refused});

         verdict.Feeling.Should().Be(HelpFeeling.Disappointed);
         verdict.RegardDelta.Should().BeNegative();
         verdict.GrudgeMagnitude.Should().Be(0);
      }

      [TestCase(HelpAnswer.Refused)]
      [TestCase(HelpAnswer.None)]
      public void GIVEN_a_player_who_truly_could_not_WHEN_the_crisis_closes_THEN_it_costs_nothing(HelpAnswer answer)
      {
         // STAKE: Gabriel's default: a prisoner is not to blame for a castle that fell, answered or not.
         HelpVerdict verdict = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true, Answer = answer, Incapacity = HelpIncapacity.Captive, Disaster = true});

         verdict.Feeling.Should().Be(HelpFeeling.Nothing);
         verdict.RegardDelta.Should().Be(0);
      }

      [Test]
      public void GIVEN_a_promise_with_no_ride_WHEN_the_place_fell_THEN_it_is_the_worst_outcome()
      {
         // STAKE: a broken word in the hour of need must cost more than silence, or promising would always be the safe click.
         HelpVerdict betrayed = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true, Answer = HelpAnswer.Promised, Disaster = true});
         HelpVerdict silent = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true, Disaster = true});

         betrayed.Feeling.Should().Be(HelpFeeling.Betrayed);
         betrayed.GrudgeMagnitude.Should().BeGreaterThan(silent.GrudgeMagnitude);
         betrayed.RegardDelta.Should().BeLessThan(silent.RegardDelta);
      }

      [Test]
      public void GIVEN_a_promise_and_a_ride_WHEN_the_place_fell_first_THEN_the_word_is_not_broken()
      {
         // STAKE: a siege can end in days; a player who set out at once and came too late kept faith.
         HelpVerdict verdict = HelpPleaPolicy.Judge(new HelpCloseFacts {LetterReached = true, Answer = HelpAnswer.Promised, WasOnTheWay = true, Disaster = true});

         verdict.Feeling.Should().Be(HelpFeeling.TooLate);
         verdict.GrudgeMagnitude.Should().Be(0);
         verdict.RegardDelta.Should().Be(0);
      }
   }

   [TestFixture]
   public class HelpPleaLedgerTests
   {
      [Test]
      public void GIVEN_a_plea_WHEN_its_letter_arrives_THEN_the_deadline_counts_from_the_arrival()
      {
         // STAKE: a rider's road across the map must not eat the days the player has to reach the siege.
         var ledger = new HelpPleaLedger();
         HelpPlea plea = ledger.Add(new HelpPlea {SenderId = "a", Ask = HelpAsk.Relief}, "k", 10);

         ledger.MarkReceived(plea, 17);

         plea.DeadlineDay.Should().Be(17 + HelpPleaPolicy.DaysToReachTheSiege);
      }

      [Test]
      public void GIVEN_a_ledger_WHEN_saved_and_read_THEN_nothing_is_lost()
      {
         // STAKE: a save and reload in the middle of a siege must not forget the promise or the crisis already pleaded.
         var ledger = new HelpPleaLedger();
         ledger.Add(new HelpPlea {SenderId = "a", Ask = HelpAsk.Coin, Amount = 700, Answer = HelpAnswer.Refused}, "siege:x:3", 10);

         HelpPleaLedger read = HelpPleaLedger.Deserialize(ledger.Serialize());

         read.From("a").Amount.Should().Be(700);
         read.From("a").Answer.Should().Be(HelpAnswer.Refused);
         read.PleadedCrises.Should().ContainKey("siege:x:3");
         read.LastPleaDay["a"].Should().Be(10);
         read.NextId.Should().Be(2);
      }

      [TestCase(null)]
      [TestCase("")]
      [TestCase("{broken")]
      public void GIVEN_an_old_or_broken_save_WHEN_read_THEN_the_ledger_is_empty(string json)
      {
         // STAKE: the save-migration rule: a save from before this feature, or a damaged key, never bricks a campaign.
         HelpPleaLedger.Deserialize(json).Open.Should().BeEmpty();
      }

      [Test]
      public void GIVEN_an_old_crisis_WHEN_pruned_THEN_it_is_forgotten_and_a_recent_one_stays()
      {
         // STAKE: the ledger is saved with the campaign; it must not grow for as long as the campaign runs.
         var ledger = new HelpPleaLedger();
         ledger.Add(new HelpPlea {SenderId = "a"}, "old", 0);
         ledger.Add(new HelpPlea {SenderId = "b"}, "new", 500);

         ledger.Prune(500);

         ledger.PleadedCrises.Should().ContainKey("new").And.NotContainKey("old");
      }

      [Test]
      public void GIVEN_a_capable_player_WHEN_the_note_is_read_THEN_they_know_what_silence_costs()
      {
         // STAKE: a grievance the player was never warned of feels like a bug; the price is told before the choice.
         HelpPleaWording.Note(HelpAsk.Coin, HelpIncapacity.None, 9).Should().Contain("9 days").And.Contain("remembered");
         HelpPleaWording.Note(HelpAsk.Relief, HelpIncapacity.TooFewMen, 9).Should().Contain("Nobody will hold it against you");
      }

      [Test]
      public void GIVEN_a_player_who_cannot_WHEN_the_button_is_read_THEN_it_says_so_and_only_then()
      {
         // STAKE: "I cannot" is the free answer; offering it to a player who can would be a lie by the game.
         HelpPleaWording.DeclineButton(HelpIncapacity.TooPoor).Should().Be("I cannot");
         HelpPleaWording.DeclineButton(HelpIncapacity.None).Should().Be("I refuse");
      }
   }
}
