// Code written by Gabriel Mailhot, 12/09/2026.
// The seasonal historian. What these pin: that a season which changed nothing is not recorded, and that one
// which did is described the way a person would remark on it rather than the way a ledger would.
//
// Why it exists, in Gabriel's own question: a lord knows how many men he has NOW; asked how many he had last
// year, he invents. A historian that fixed that with a time series would be expensive AND wrong - nobody
// remembers a number, and a character with a perfect recall of figures breaks the illusion from the other
// side. His design answers it without one: a seasonal snapshot, summarised as a chronicler would.
//
// His first ruling is the one this file exists to keep: COMPUTE IT, do not have the LLM write it. "Vlandia
// grew rich" IS a delta description, so it costs nothing, cannot fail halfway, and has no language to get
// wrong. The chronicler stays alive because a CHARACTER tells it, not a background job.
//
// If these fail, either the chronicle has started recording ripples nobody would mention, or it has stopped
// noticing a realm losing a town.

#region

using CalradiaRemembers.Logic.WorldAlmanac;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.WorldAlmanac
{
   [TestFixture]
   public sealed class SeasonalTurnPolicyTests
   {
      private static KingdomSeasonSnapshot Vlandia(int holdings = 10, long wealth = 100000,
                                                   double field = 5000, double prosperity = 20000)
         => new() {
            KingdomId = "vlandia", KingdomName = "Vlandia",
            Holdings = holdings, Wealth = wealth, FieldStrength = field, Prosperity = prosperity
         };

      // A quiet season is the common case, and it must cost nothing: a chronicle that records every ripple is
      // a chronicle nobody reads, and these lines are spoken aloud by characters who should sound like they
      // are remarking on something.
      [Test]
      public void GIVEN_a_season_in_which_nothing_much_moved_WHEN_summarised_THEN_nothing_is_recorded()
      {
         SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(10, 105000, 5100, 20500), "Autumn").Should().BeNull();
         SeasonalTurnPolicy.IsNotable(Vlandia(), Vlandia(10, 105000, 5100, 20500)).Should().BeFalse();
      }

      // The thing everyone actually talks about. A single stronghold changing hands is history whatever the
      // arithmetic says elsewhere, so it has no threshold at all.
      [Test]
      public void GIVEN_a_stronghold_changed_hands_WHEN_summarised_THEN_it_is_history_whatever_else_held_steady()
      {
         SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(11), "Autumn")
                           .Should().Be("Vlandia took a stronghold this past autumn.");

         SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(9), "Winter")
                           .Should().Be("Vlandia lost a stronghold this past winter.");
      }

      // Gabriel's own example, and the phrase he used for it: "Vlandia s'est enrichit".
      [Test]
      public void GIVEN_a_realm_that_grew_rich_WHEN_summarised_THEN_that_is_what_it_says()
      {
         SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(10, 200000), "Summer")
                           .Should().Contain("grew richer");
      }

      // Both directions, because a chronicle that only recorded triumphs would be a chronicle nobody believed.
      [Test]
      public void GIVEN_a_realm_in_decline_WHEN_summarised_THEN_the_decline_is_recorded_too()
      {
         string said = SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(10, 40000, 2000, 12000), "Winter");

         said.Should().Contain("coffers thin").And.Contain("armies thin").And.Contain("towns suffer");
      }

      // BY SHARE, not by amount. A thousand denars is a fortune to one realm and a rounding error to another,
      // and a chronicle that only ever remarked on the largest kingdom would be a chronicle about one kingdom.
      [Test]
      public void GIVEN_a_small_realm_and_a_great_one_WHEN_each_gains_the_same_coin_THEN_only_the_small_one_is_news()
      {
         var smallBefore = new KingdomSeasonSnapshot {KingdomName = "Battania", Wealth = 10000};
         var smallAfter = new KingdomSeasonSnapshot {KingdomName = "Battania", Wealth = 20000};

         var greatBefore = new KingdomSeasonSnapshot {KingdomName = "Vlandia", Wealth = 1000000};
         var greatAfter = new KingdomSeasonSnapshot {KingdomName = "Vlandia", Wealth = 1010000};

         SeasonalTurnPolicy.IsNotable(smallBefore, smallAfter).Should().BeTrue();
         SeasonalTurnPolicy.IsNotable(greatBefore, greatAfter).Should().BeFalse();
      }

      // It reads as a sentence, not an inventory. These are spoken by people.
      [Test]
      public void GIVEN_several_things_moved_WHEN_summarised_THEN_it_reads_as_a_chronicler_would_write_it()
      {
         string said = SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(12, 200000), "Spring");

         said.Should().Be("Vlandia took 2 strongholds and grew richer this past spring.");
      }

      // A realm with nothing to compare against says nothing rather than announcing itself into existence -
      // the first season after a save is loaded, or a kingdom newly founded.
      [Test]
      public void GIVEN_no_previous_standing_WHEN_summarised_THEN_nothing_is_claimed()
      {
         SeasonalTurnPolicy.Describe(null, Vlandia(), "Autumn").Should().BeNull();

         var fromNothing = new KingdomSeasonSnapshot {KingdomName = "Vlandia"};
         SeasonalTurnPolicy.Describe(fromNothing, Vlandia(0, 5000, 100, 900), "Autumn").Should().BeNull();
      }

      // The season is named because the whole point is that facts acquire a DATE - and in seasons rather than
      // days, which is what made dating affordable at all.
      [Test]
      public void GIVEN_any_recorded_turn_WHEN_summarised_THEN_it_says_when()
      {
         SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(11), "Autumn").Should().EndWith("this past autumn.");
         SeasonalTurnPolicy.Describe(Vlandia(), Vlandia(11), null).Should().EndWith("this past season.");
      }
   }
}
