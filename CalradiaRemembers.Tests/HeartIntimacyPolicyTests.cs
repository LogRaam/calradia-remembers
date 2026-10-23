// Code written by Gabriel Mailhot, 23/10/2026.
// The Heart section's intimate lines (Gabriel, 23/10/2026): at Hardcore only, the nights the player shared with a character
// show on both pages, sober, "du genre de quelque chose que le joueur aurait pu ecrire dans son journal intime". Heart is
// "une section sur l'amour et le desir": what the player took by force is told in the Misdeeds section instead
// (MisdeedsPagePolicyTests). If this is wrong, a player below Hardcore reads what they never opted into, a night is counted
// twice, or a forced scene is told as a night of love.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class HeartIntimacyPolicyTests
   {
      private static string When(int day) => day switch {100 => "spring 1091", 200 => "summer 1092", 300 => "autumn 1093", _ => $"day {day}"};

      // STAKE: Gabriel, 23/10/2026: "En mode Hardcore uniquement." A player on Explicit or below never opted into this.
      [TestCase(AdultContentLevel.Off)]
      [TestCase(AdultContentLevel.Mature)]
      [TestCase(AdultContentLevel.Explicit)]
      public void GIVEN_a_level_below_hardcore_WHEN_a_page_opens_THEN_no_intimate_line_shows(AdultContentLevel level)
      {
         HeartIntimacyPolicy.Shows(level).Should().BeFalse();
      }

      // STAKE: the other half of the same ruling: at Hardcore the lines show.
      [Test]
      public void GIVEN_hardcore_WHEN_a_page_opens_THEN_the_intimate_lines_show()
      {
         HeartIntimacyPolicy.Shows(AdultContentLevel.Hardcore).Should().BeTrue();
      }

      // STAKE: a single night is told as one, dated, in the page's second person.
      [Test]
      public void GIVEN_one_night_together_WHEN_her_page_opens_THEN_it_tells_that_night_and_when()
      {
         var f = new HeartNightsFacts {Name = "Ira", NightDays = new[] {100}};

         HeartIntimacyPolicy.NightsLine(f, When).Should().Be("You shared a night together in spring 1091.");
      }

      // STAKE: sober as a diary: how many, the first and the last, never the content of a night.
      [Test]
      public void GIVEN_several_nights_WHEN_her_page_opens_THEN_it_counts_them_with_the_first_and_the_last()
      {
         var f = new HeartNightsFacts {Name = "Ira", NightDays = new[] {300, 100, 200}};

         HeartIntimacyPolicy.NightsLine(f, When).Should().Be("You have shared three nights together, the first in spring 1091, the last in autumn 1093.");
      }

      // STAKE: a conversation can leave two intimate memories on one day; the page would otherwise count a night twice.
      [Test]
      public void GIVEN_two_intimate_memories_on_one_day_WHEN_counted_THEN_they_are_one_night()
      {
         var f = new HeartNightsFacts {Name = "Ira", NightDays = new[] {100, 100}};

         HeartIntimacyPolicy.NightsLine(f, When).Should().Be("You shared a night together in spring 1091.");
      }

      // STAKE: nights all in one season are not told as "the first in spring 1091, the last in spring 1091".
      [Test]
      public void GIVEN_nights_all_in_one_season_WHEN_her_page_opens_THEN_the_season_is_said_once()
      {
         var f = new HeartNightsFacts {Name = "Ira", NightDays = new[] {100, 101}};

         HeartIntimacyPolicy.NightsLine(f, d => "spring 1091").Should().Be("You have shared two nights together, in spring 1091.");
      }

      // STAKE: a character the player never lay with has no such line.
      [Test]
      public void GIVEN_no_night_WHEN_a_page_opens_THEN_there_is_no_line()
      {
         HeartIntimacyPolicy.NightsLine(new HeartNightsFacts {Name = "Ira"}, When).Should().BeNull();
         HeartIntimacyPolicy.NightsRecapLine(new HeartNightsFacts {Name = "Ira"}, When).Should().BeNull();
      }

      // STAKE: Gabriel: "Les scenes intimes consentantes apparaissent sur la page du joueur", one line per partner.
      [Test]
      public void GIVEN_nights_with_a_partner_WHEN_the_players_page_opens_THEN_a_line_names_the_partner_and_counts_them()
      {
         var one = new HeartNightsFacts {Name = "Ira", NightDays = new[] {100}};
         var many = new HeartNightsFacts {Name = "Ajin the Hawk", NightDays = new[] {100, 200}};

         HeartIntimacyPolicy.NightsRecapLine(one, When).Should().Be("A night with Ira, in spring 1091.");
         HeartIntimacyPolicy.NightsRecapLine(many, When).Should().Be("Two nights with Ajin the Hawk, the first in spring 1091, the last in summer 1092.");
      }

      // STAKE: a forced captive scene must never be counted as a night of love: the romantic memory such a scene would leave
      // is dropped before it is written (CoerciveSceneEventPolicy), so the nights line only ever reads consensual nights.
      [Test]
      public void GIVEN_a_forced_captive_scene_WHEN_the_model_tags_it_intimacy_THEN_no_night_is_written()
      {
         CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Intimacy, true).Should().BeTrue();
         CoerciveSceneEventPolicy.ShouldDropRomanticEvent(NotableEventType.Intimacy, false).Should().BeFalse();
      }
   }
}
