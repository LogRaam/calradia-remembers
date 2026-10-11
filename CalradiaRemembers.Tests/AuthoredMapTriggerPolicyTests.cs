// Code written by Gabriel Mailhot, 11/10/2026.
// A tale that begins by itself on the map (Gabriel, 10/10/2026: the party waits in the wife's home region, by day, and
// there is a chance the tale begins). If these rules are wrong, the scene by the waterfall begins at night, at sea, in
// the middle of a siege or inside a town, the moment the party pauses for a breath, or anywhere but her people's land.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredMapTriggerPolicyTests
   {
      private static AuthoredMapTrigger Waterfall() => new() {StillHours = 3, ByDay = true, InGiversLands = true, ChancePerDay = 0.15};

      private static MapTriggerFacts Camped(int still = 5, bool day = true, bool lands = true, bool settlement = false, bool battle = false, bool sea = false)
         => new() {StillHours = still, IsDay = day, InGiversLands = lands, InSettlement = settlement, InBattle = battle, AtSea = sea, LandsCulture = "Vlandia"};

      // Gabriel's case: married to a Khuzait, waiting in the steppe by day, long enough.
      [Test]
      public void GIVEN_the_party_camped_by_day_in_the_wifes_lands_WHEN_checked_THEN_the_tale_may_begin()
      {
         AuthoredMapTriggerPolicy.WhyNot(Waterfall(), Camped()).Should().BeNull();
      }

      // Gabriel: "il faudrait aussi que ca se passe de jour". She is found bathing in daylight.
      [Test]
      public void GIVEN_night_WHEN_the_tale_begins_by_day_THEN_it_waits()
      {
         AuthoredMapTriggerPolicy.WhyNot(Waterfall(), Camped(day: false)).Should().Contain("night");
      }

      // A party that only paused on its road has not made camp: the tale waits for the hours it names.
      [Test]
      public void GIVEN_a_party_still_for_less_than_the_tale_waits_WHEN_checked_THEN_it_waits()
      {
         AuthoredMapTriggerPolicy.WhyNot(Waterfall(), Camped(still: 2)).Should().Contain("2 hours of the 3");
      }

      // Gabriel: "dans la region d'origine de sa femme". Camped in Vlandia, a Khuzait wife's tale does not begin.
      [Test]
      public void GIVEN_the_party_outside_the_wifes_lands_WHEN_the_tale_asks_for_them_THEN_it_waits_and_names_where_it_is()
      {
         AuthoredMapTriggerPolicy.WhyNot(Waterfall(), Camped(lands: false)).Should().Contain("Vlandia");
      }

      // She is found in nature: never inside a town, in a siege or at sea.
      [Test]
      public void GIVEN_a_town_a_battle_or_the_sea_WHEN_checked_THEN_the_tale_never_begins_there()
      {
         AuthoredMapTriggerPolicy.WhyNot(Waterfall(), Camped(settlement: true)).Should().Contain("settlement");
         AuthoredMapTriggerPolicy.WhyNot(Waterfall(), Camped(battle: true)).Should().Contain("battle");
         AuthoredMapTriggerPolicy.WhyNot(Waterfall(), Camped(sea: true)).Should().Contain("sea");
      }

      // A tale that asks neither day nor lands begins wherever the party has stood still long enough.
      [Test]
      public void GIVEN_a_tale_that_asks_neither_day_nor_lands_WHEN_at_night_abroad_THEN_it_may_begin()
      {
         var anywhere = new AuthoredMapTrigger {StillHours = 0, ByDay = false, InGiversLands = false};

         AuthoredMapTriggerPolicy.WhyNot(anywhere, Camped(still: 0, day: false, lands: false)).Should().BeNull();
      }

      // Gabriel: 15% a day. The roll under the chance begins it; at or over it, not today.
      [Test]
      public void GIVEN_the_days_roll_WHEN_under_or_over_the_chance_THEN_it_begins_or_not()
      {
         AuthoredMapTriggerPolicy.Begins(0.15, 0.14).Should().BeTrue();
         AuthoredMapTriggerPolicy.Begins(0.15, 0.15).Should().BeFalse();
      }

      // The defaults are Gabriel's settings (11/10/2026): 3 hours still, by day, in her lands, 15% a day.
      [Test]
      public void GIVEN_a_tale_that_writes_only_on_map_WHEN_read_THEN_it_takes_gabriels_settings()
      {
         var t = new AuthoredMapTrigger();

         t.StillHours.Should().Be(3);
         t.ByDay.Should().BeTrue();
         t.InGiversLands.Should().BeTrue();
         t.ChancePerDay.Should().Be(0.15);
      }
   }
}
