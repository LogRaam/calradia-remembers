// Code written by Gabriel Mailhot, 01/11/2026.
// What this decides: the most a character hands the player for nothing in return, read by the give_gold action and told to
// the model before it names a sum.
//
// RaveN017Mod (Nexus, 01/11/2026): Rhagaea offered 20,000 denars, nothing happened, then she sent 1,000. Gabriel, same day:
// the problem is "l'incoherence entre ce que le LLM dit et ce que le joueur recoit" and players extracting "trop d'or au LLM
// par rapport a la valeur des choses"; "part de la bourse selon le regard". If this is wrong, a player talks a rich lord out
// of a fortune, drains a purse by asking again, or a sworn debt cannot be paid.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class GiftCeilingPolicyTests
   {
      // STAKE: Gabriel's rule: warmer regard, larger gift. A stranger talked into generosity gets little from a rich lord.
      [TestCase(0, 1000)]
      [TestCase(20, 3000)]
      [TestCase(30, 5000)]
      [TestCase(50, 10000)]
      [TestCase(60, 20000)]
      public void GIVEN_a_purse_of_100000_WHEN_regard_rises_THEN_more_is_given(int regard, int expected)
      {
         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 100000, Regard = regard}).Should().Be(expected);
      }

      // STAKE: the old flat 10,000 made no difference between a stranger and a spouse; a beloved character with a great
      // purse may now give more than it ever allowed, which is what lifting that ceiling was for.
      [Test]
      public void GIVEN_a_devoted_character_with_a_great_purse_WHEN_they_give_THEN_the_old_10000_no_longer_holds_them()
      {
         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 1000000, Regard = 80}).Should().BeGreaterThan(GoldTransferLimit.MaxSingleTransfer);
      }

      // STAKE: someone who despises the player hands over nothing, however the player words it.
      [Test]
      public void GIVEN_a_hostile_character_WHEN_they_give_THEN_nothing_changes_hands()
      {
         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 100000, Regard = -30}).Should().Be(0);
      }

      // STAKE: a thrown purse of coin for a beggar or a messenger should not read as refused because the giver is poor:
      // a few coins stay possible for anyone not hostile.
      [Test]
      public void GIVEN_a_thin_purse_WHEN_they_give_THEN_a_few_coins_are_still_possible()
      {
         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 500, Regard = 0}).Should().Be(GiftCeilingPolicy.SmallCoin);
      }

      // STAKE: the exploit Gabriel named: asking again must not take the same share of what is left until the purse is gone.
      [Test]
      public void GIVEN_a_gift_already_made_WHEN_the_player_asks_again_inside_the_window_THEN_nothing_more_comes()
      {
         var first = new GiftFacts {Purse = 100000, Regard = 30};
         int given = GiftCeilingPolicy.Ceiling(first);

         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 100000 - given, Regard = 30, GivenInWindow = given}).Should().Be(0);
      }

      // STAKE: the same limit, spent in small asks: the total stays the share, whatever the number of times.
      [Test]
      public void GIVEN_part_of_the_share_given_WHEN_the_player_asks_again_THEN_only_the_rest_of_the_share_remains()
      {
         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 98000, Regard = 30, GivenInWindow = 2000}).Should().Be(3000);
      }

      // STAKE: a character's generosity is not spent forever: a month later they may give again.
      [Test]
      public void GIVEN_a_month_gone_by_WHEN_the_window_is_read_THEN_past_gifts_no_longer_count()
      {
         GiftCeilingPolicy.GivenStill(100, 5000, 100 + GiftCeilingPolicy.WindowDays).Should().Be(0);
         GiftCeilingPolicy.GivenStill(100, 5000, 100 + GiftCeilingPolicy.WindowDays - 1).Should().Be(5000);
      }

      // STAKE: an old save has no window; nothing was given as far as anyone knows.
      [Test]
      public void GIVEN_no_window_on_record_WHEN_it_is_read_THEN_nothing_counts()
      {
         GiftCeilingPolicy.GivenStill(null, 0, 100).Should().Be(0);
      }

      // STAKE: a sworn pay_gold oath is verified by this same action (CommitmentBehavior); a ceiling on it would make a
      // real promise unpayable and break the character's word for them.
      [Test]
      public void GIVEN_a_sworn_debt_WHEN_they_pay_THEN_the_debt_is_not_held_back_as_a_gift()
      {
         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 100000, Regard = 0, OwedByOath = 40000}).Should().Be(40000);
      }

      // STAKE: no rule conjures coin: a debt or a share stops at the purse.
      [Test]
      public void GIVEN_a_debt_beyond_the_purse_WHEN_they_pay_THEN_it_stops_at_the_purse()
      {
         GiftCeilingPolicy.Ceiling(new GiftFacts {Purse = 300, Regard = 0, OwedByOath = 40000}).Should().Be(300);
      }

      // STAKE: the report's second half: 1,000 arrived with no word about the 20,000. The player and the model both read why.
      [Test]
      public void GIVEN_less_was_given_than_named_WHEN_willingness_held_it_back_THEN_the_notice_says_so()
      {
         GiftCeilingPolicy.ReducedNote(new GiftOutcome {Asked = 20000, Given = 1000, GiverName = "Rhagaea"})
                          .Should().Contain("not the 20000 named").And.Contain("will part with freely");
      }

      // STAKE: the two reasons differ for the player: an empty purse is no slight.
      [Test]
      public void GIVEN_less_was_given_than_named_WHEN_the_purse_held_it_back_THEN_the_notice_says_that_instead()
      {
         GiftCeilingPolicy.ReducedNote(new GiftOutcome {Asked = 20000, Given = 1000, LimitedByPurse = true, GiverName = "Rhagaea"})
                          .Should().Contain("all Rhagaea holds");
      }

      // STAKE: when the sum named is the sum paid there is nothing to explain.
      [Test]
      public void GIVEN_the_sum_named_was_given_WHEN_the_notice_is_written_THEN_nothing_is_added()
      {
         GiftCeilingPolicy.ReducedNote(new GiftOutcome {Asked = 500, Given = 500, GiverName = "Rhagaea"}).Should().BeEmpty();
      }
   }
}
