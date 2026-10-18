// Code written by Gabriel Mailhot, 17/10/2026.
// CompanionRumourFreshnessPolicy decides whether a companion travelling with the player still comes to bring a piece of
// news. LapitaPolata (Nexus, 17/10/2026): a companion told of a castle being besieged after it had fallen, and of a war
// breaking out while the player was already riding to a siege in it, "like they were not in my team witnessing
// everything with me, but were calling from the other side of the continent". If this is too loose, that comes back; if
// too strict, a companion never brings word of the world at all.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionRumourFreshnessPolicyTests
   {
      // STAKE: fresh news of a distant realm is what this topic is for; it must still come.
      [Test]
      public void GIVEN_fresh_news_of_a_distant_realm_WHEN_weighed_THEN_the_companion_may_bring_it()
      {
         CompanionRumourFreshnessPolicy.WhyNotNews(new CompanionRumourFreshnessFacts {AgeDays = 2}).Should().BeNull();
      }

      // STAKE: LapitaPolata's war: his own realm's war is no news to him; he was already riding to a siege in it.
      [Test]
      public void GIVEN_a_war_of_the_players_own_realm_WHEN_weighed_THEN_it_is_not_brought_as_news()
      {
         CompanionRumourFreshnessPolicy.WhyNotNews(new CompanionRumourFreshnessFacts {AgeDays = 0, IsThePlayersOwnWarOrPeace = true}).Should().NotBeNull();
      }

      // STAKE: LapitaPolata's castle: the siege fight is not told once the place has fallen since.
      [Test]
      public void GIVEN_a_siege_fight_at_a_place_that_has_fallen_since_WHEN_weighed_THEN_it_is_not_brought()
      {
         CompanionRumourFreshnessPolicy.WhyNotNews(new CompanionRumourFreshnessFacts {AgeDays = 1, OvertakenSince = true}).Should().NotBeNull();
      }

      // STAKE: the companion rode with the player; what the player lived is not news to bring them.
      [Test]
      public void GIVEN_an_event_the_player_took_part_in_WHEN_weighed_THEN_it_is_not_brought()
      {
         CompanionRumourFreshnessPolicy.WhyNotNews(new CompanionRumourFreshnessFacts {InvolvesThePlayer = true}).Should().NotBeNull();
      }

      // STAKE: a war since made peace is not announced as a war breaking out.
      [Test]
      public void GIVEN_a_war_since_made_peace_WHEN_weighed_THEN_it_is_not_brought()
      {
         CompanionRumourFreshnessPolicy.WhyNotNews(new CompanionRumourFreshnessFacts {AgeDays = 3, NoLongerTrue = true}).Should().NotBeNull();
      }

      // STAKE: travelling together, news older than a week would have reached the player too.
      [Test]
      public void GIVEN_news_older_than_a_week_WHEN_weighed_THEN_it_is_not_brought()
      {
         CompanionRumourFreshnessPolicy.WhyNotNews(new CompanionRumourFreshnessFacts {AgeDays = CompanionRumourFreshnessPolicy.MaxAgeDays}).Should().BeNull();
         CompanionRumourFreshnessPolicy.WhyNotNews(new CompanionRumourFreshnessFacts {AgeDays = CompanionRumourFreshnessPolicy.MaxAgeDays + 1}).Should().NotBeNull();
      }

      // STAKE: the age is said, so the news is told as past, not as something under way.
      [Test]
      public void GIVEN_an_age_WHEN_worded_THEN_it_reads_as_past()
      {
         CompanionRumourFreshnessPolicy.AgeWords(0).Should().Be("today");
         CompanionRumourFreshnessPolicy.AgeWords(1).Should().Be("yesterday");
         CompanionRumourFreshnessPolicy.AgeWords(4).Should().Be("4 days ago");
      }
   }
}
