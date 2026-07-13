// Code written by Gabriel Mailhot, 23/06/2026.
// Chooses which town a companion's "go gather news about X" errand rides to
// (CompanionMissionBehavior.ResolveDestination): a named settlement beats a faction (kingdom) beats a
// culture, and -1 tells the host to fall back to the nearest town. Get the precedence or the tie-break
// wrong and the companion rides to a town that has nothing to do with what the player actually asked
// for, or two equally-ranked candidates resolve unpredictably instead of to the intended nearest one.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class NewsDestinationSelectorTests
   {
      // Without the culture fallback tier, a perfectly reasonable culture-worded request ("news about the
      // Sturgians") would score nothing at all and the companion would just ride to the nearest town
      // regardless of what was actually asked.
      [Test]
      public void GIVEN_a_culture_topic_WHEN_no_faction_matches_THEN_it_falls_to_a_culture_match()
      {
         //Arrange — "Sturgian" is the culture, not the faction spelling.
         var towns = new List<NewsDestinationCandidate> {
            Town("Pravend", "Vlandia", "Vlandian"),
            Town("Sibir", "Sturgia", "Sturgian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Sturgian", towns).Should().Be(1);
      }

      // Pins that a kingdom name alone, with no settlement named, is enough to steer the errand to the
      // right realm, not merely to whichever town happens to be nearest.
      [Test]
      public void GIVEN_a_faction_topic_WHEN_choosing_THEN_it_picks_a_town_of_that_faction()
      {
         //Arrange
         var towns = new List<NewsDestinationCandidate> {
            Town("Pravend", "Vlandia", "Vlandian"),
            Town("Sibir", "Sturgia", "Sturgian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Sturgia", towns).Should().Be(1);
      }

      // Pins the precedence order itself (settlement over faction over culture): without it, a naive
      // first-match scan (or a reversed order) could send the companion to the wrong town whenever a
      // settlement name happens to overlap a faction or culture substring.
      [Test]
      public void GIVEN_a_named_settlement_WHEN_it_also_partly_matches_a_faction_THEN_the_settlement_wins()
      {
         //Arrange — a request for "Pravend" must beat a mere faction match elsewhere.
         var towns = new List<NewsDestinationCandidate> {
            Town("Sibir", "Sturgia", "Sturgian"),
            Town("Pravend", "Vlandia", "Vlandian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Pravend", towns).Should().Be(1);
      }

      // An unrecognized topic must return "no scoped match" (-1), not default to index 0: defaulting
      // would silently send the companion to a real but unrelated town, which the player would mistake
      // for a genuine match on their request.
      [Test]
      public void GIVEN_a_topic_that_matches_nothing_WHEN_choosing_THEN_it_returns_no_match()
      {
         //Arrange
         var towns = new List<NewsDestinationCandidate> {Town("Pravend", "Vlandia", "Vlandian")};

         //Act/Assert — host falls back to the nearest town.
         NewsDestinationSelector.Choose("Aserai", towns).Should().Be(-1);
      }

      // Guards that both empty-string and null topic are treated identically: free-text parsing on the
      // host side could plausibly produce either, and only one being handled would be an easy miss.
      [Test]
      public void GIVEN_no_topic_WHEN_choosing_THEN_it_defers_to_the_host_nearest_fallback()
      {
         //Arrange
         var towns = new List<NewsDestinationCandidate> {Town("Sibir", "Sturgia", "Sturgian")};

         //Act/Assert — -1 means "no scoped match; pick the nearest".
         NewsDestinationSelector.Choose("", towns).Should().Be(-1);
         NewsDestinationSelector.Choose(null, towns).Should().Be(-1);
      }

      // Pins the tie-break as "keep the first," not "keep the last" or unstable: since candidates are
      // pre-sorted nearest-first by the host, this is what actually guarantees the companion rides to the
      // NEAREST of several equally-valid towns instead of a farther one.
      [Test]
      public void GIVEN_two_towns_of_the_same_faction_WHEN_choosing_THEN_the_first_host_ordered_one_wins()
      {
         //Arrange — host orders by nearest; a tie must resolve to the nearest (index 0).
         var towns = new List<NewsDestinationCandidate> {
            Town("Balgard", "Sturgia", "Sturgian"),
            Town("Sibir", "Sturgia", "Sturgian")
         };

         //Act/Assert
         NewsDestinationSelector.Choose("Sturgia", towns).Should().Be(0);
      }

      #region private

      private static NewsDestinationCandidate Town(string name, string faction, string culture) => new(name, faction, culture);

      #endregion
   }
}