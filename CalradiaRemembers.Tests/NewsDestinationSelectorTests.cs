// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the news-errand destination scoping — which town a "bring me news about X" ride targets, and
// the settlement > faction > culture precedence, with nearest-first tie-breaking and the empty fallback.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class NewsDestinationSelectorTests
   {
      private static NewsDestinationCandidate Town(string name, string faction, string culture)
         => new NewsDestinationCandidate(name, faction, culture);

      [Test]
      public void GIVEN_no_topic_WHEN_choosing_THEN_it_defers_to_the_host_nearest_fallback()
      {
         //Arrange
         var towns = new List<NewsDestinationCandidate> { Town("Sibir", "Sturgia", "Sturgian") };

         //Act/Assert — -1 means "no scoped match; pick the nearest".
         NewsDestinationSelector.Choose("", towns).Should().Be(-1);
         NewsDestinationSelector.Choose(null, towns).Should().Be(-1);
      }

      [Test]
      public void GIVEN_a_faction_topic_WHEN_choosing_THEN_it_picks_a_town_of_that_faction()
      {
         //Arrange
         var towns = new List<NewsDestinationCandidate>
         {
            Town("Pravend", "Vlandia", "Vlandian"),
            Town("Sibir",   "Sturgia", "Sturgian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Sturgia", towns).Should().Be(1);
      }

      [Test]
      public void GIVEN_a_culture_topic_WHEN_no_faction_matches_THEN_it_falls_to_a_culture_match()
      {
         //Arrange — "Sturgian" is the culture, not the faction spelling.
         var towns = new List<NewsDestinationCandidate>
         {
            Town("Pravend", "Vlandia", "Vlandian"),
            Town("Sibir",   "Sturgia", "Sturgian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Sturgian", towns).Should().Be(1);
      }

      [Test]
      public void GIVEN_a_named_settlement_WHEN_it_also_partly_matches_a_faction_THEN_the_settlement_wins()
      {
         //Arrange — a request for "Pravend" must beat a mere faction match elsewhere.
         var towns = new List<NewsDestinationCandidate>
         {
            Town("Sibir",   "Sturgia",  "Sturgian"),
            Town("Pravend", "Vlandia",  "Vlandian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Pravend", towns).Should().Be(1);
      }

      [Test]
      public void GIVEN_two_towns_of_the_same_faction_WHEN_choosing_THEN_the_first_host_ordered_one_wins()
      {
         //Arrange — host orders by nearest; a tie must resolve to the nearest (index 0).
         var towns = new List<NewsDestinationCandidate>
         {
            Town("Balgard", "Sturgia", "Sturgian"),
            Town("Sibir",   "Sturgia", "Sturgian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Sturgia", towns).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_topic_that_matches_nothing_WHEN_choosing_THEN_it_returns_no_match()
      {
         //Arrange
         var towns = new List<NewsDestinationCandidate> { Town("Pravend", "Vlandia", "Vlandian") };

         //Act/Assert — host falls back to the nearest town.
         NewsDestinationSelector.Choose("Aserai", towns).Should().Be(-1);
      }
   }
}
