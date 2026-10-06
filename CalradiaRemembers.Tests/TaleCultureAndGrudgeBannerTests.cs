// Code written by Gabriel Mailhot, 06/10/2026.
// Two asks from tashmetu (Nexus, 06/10/2026) on written tales. First: a tale about Battanian woods should be offered by
// a Battanian lord about a Battanian village, so the giver and the village may each name a culture, one or several.
// Second: when an ending sets two lords at odds, the banner still said "<lord> now holds a grievance against you",
// telling the player they had made an enemy they had not. If this breaks, a tale lands in the wrong country, or the
// player is told of a quarrel with them that does not exist.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleCultureAndGrudgeBannerTests
   {
      // A tale that names no culture is offered anywhere, as before.
      [Test]
      public void GIVEN_no_culture_asked_WHEN_any_culture_is_checked_THEN_it_matches()
      {
         AuthoredCulturePolicy.Matches(new List<string>(), "vlandia", "Vlandia").Should().BeTrue();
         AuthoredCulturePolicy.Matches(null, null, null).Should().BeTrue();
      }

      // tashmetu's case: "battania" matches the Battanians by id or by name, whatever the case, and nobody else.
      [Test]
      public void GIVEN_a_tale_for_battania_WHEN_cultures_are_checked_THEN_only_battania_matches()
      {
         var wanted = new List<string> {"battania"};

         AuthoredCulturePolicy.Matches(wanted, "battania", "Battania").Should().BeTrue();
         AuthoredCulturePolicy.Matches(new List<string> {"Battania"}, "battania", "Battania").Should().BeTrue();
         AuthoredCulturePolicy.Matches(wanted, "vlandia", "Vlandia").Should().BeFalse();
         AuthoredCulturePolicy.Matches(wanted, null, null).Should().BeFalse("a place with no culture is not Battanian");
      }

      // A list is any of them.
      [Test]
      public void GIVEN_a_tale_for_several_cultures_WHEN_checked_THEN_any_of_them_matches()
      {
         var wanted = new List<string> {"battania", "sturgia"};

         AuthoredCulturePolicy.Matches(wanted, "sturgia", "Sturgia").Should().BeTrue();
         AuthoredCulturePolicy.Matches(wanted, "aserai", "Aserai").Should().BeFalse();
      }

      // The file may write one culture or a list, on the giver and on the village alike.
      [Test]
      public void GIVEN_a_file_with_one_culture_or_a_list_WHEN_loaded_THEN_both_read_as_lists()
      {
         var giver = JsonConvert.DeserializeObject<GiverRequirement>("{\"role\":\"lord\",\"culture\":\"battania\"}");
         var village = JsonConvert.DeserializeObject<PlaceRequirement>("{\"type\":\"village\",\"culture\":[\"battania\",\"sturgia\"]}");

         giver.Culture.Should().Equal("battania");
         village.Culture.Should().Equal("battania", "sturgia");
      }

      // The player is the target: the banner speaks to them, as before.
      [Test]
      public void GIVEN_a_grudge_against_the_player_WHEN_the_banner_is_written_THEN_it_says_against_you()
      {
         GrudgeBannerText.Compose("Lucon", "Crotor", targetIsPlayer: true).Should().Be("Lucon now holds a grievance against you.");
      }

      // tashmetu's case: two lords at odds. The banner names the one it is held against.
      [Test]
      public void GIVEN_a_grudge_between_two_lords_WHEN_the_banner_is_written_THEN_it_names_the_target()
      {
         GrudgeBannerText.Compose("Lucon", "Crotor", targetIsPlayer: false).Should().Be("Lucon now holds a grievance against Crotor.");
      }
   }
}
