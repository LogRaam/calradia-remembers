// Code written by Gabriel Mailhot, 23/10/2026.
// The Heart section's intimate lines (Gabriel, 23/10/2026): at Hardcore only, the nights the player shared with a character
// show on both pages, sober, "du genre de quelque chose que le joueur aurait pu ecrire dans son journal intime"; of the
// forced captive scenes, "les actes que le joueur a fait subir peuvent apparaitre" on the player's page, and "les scenes
// forcees subies par le joueur ne sont pas affichees sur la page du joueur". If this is wrong, a player below Hardcore reads
// what they never opted into, a night is counted twice, a forced scene is told as a night of love, or what was done to the
// player is written on the player's own page.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.Logic.Trauma;
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

      // STAKE: Gabriel: "Les actes que le joueur a fait subir peuvent apparaitre": a prisoner the player took by force is
      // named on the player's page, once per victim, counted and dated.
      [Test]
      public void GIVEN_the_player_took_a_prisoner_by_force_twice_WHEN_the_players_page_opens_THEN_one_line_tells_it()
      {
         var ledger = new List<TraumaRecord>
         {
            new TraumaRecord {VictimId = "phenoria", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.Player, Day = 100},
            new TraumaRecord {VictimId = "phenoria", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.Player, Day = 200}
         };

         IReadOnlyList<HeartForcedActs> acts = HeartIntimacyPolicy.ForcedByThePlayer(ledger);

         acts.Should().HaveCount(1);
         HeartIntimacyPolicy.ForcedLine(acts[0], "Phenoria", true, When)
                            .Should().Be("You took Phenoria by force while she was your prisoner, twice, the first in spring 1091, the last in summer 1092.");
      }

      // STAKE: one forced scene reads as one, dated.
      [Test]
      public void GIVEN_one_forced_scene_WHEN_the_players_page_opens_THEN_it_is_told_once_with_its_season()
      {
         var acts = new HeartForcedActs {VictimId = "lucon", Days = new[] {200}};

         HeartIntimacyPolicy.ForcedLine(acts, "Lucon", false, When).Should().Be("You took Lucon by force while he was your prisoner, in summer 1092.");
      }

      // STAKE: Gabriel: "Les scenes forcees subies par le joueur ne sont pas affichees sur la page du joueur." A scene where
      // the captor forced the player's own hands is also the player's ordeal, not the player's act.
      [Test]
      public void GIVEN_what_a_captor_did_or_forced_the_player_to_do_WHEN_the_players_page_opens_THEN_none_of_it_shows()
      {
         var ledger = new List<TraumaRecord>
         {
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.Captor, Day = 100},
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.CaptorsMen, Day = 100},
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.PlayerCoerced, Day = 100}
         };

         HeartIntimacyPolicy.ForcedByThePlayer(ledger).Should().BeEmpty();
      }

      // STAKE: the Heart section is about intimacy; a prisoner the player tortured or robbed belongs to her Trauma section,
      // not to the player's love life.
      [Test]
      public void GIVEN_a_prisoner_the_player_tortured_WHEN_the_players_page_opens_THEN_the_heart_does_not_tell_it()
      {
         var ledger = new List<TraumaRecord>
         {
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Tortured, Author = TraumaAuthor.Player, Day = 100},
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Robbed, Author = TraumaAuthor.Player, Day = 100}
         };

         HeartIntimacyPolicy.ForcedByThePlayer(ledger).Should().BeEmpty();
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
