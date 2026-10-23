// Code written by Gabriel Mailhot, 23/10/2026.
// The Misdeeds section on the player's own page (Gabriel, 23/10/2026): "Heart doit etre une section sur l'amour et le desir.
// L'evenement 'take by force' devrait etre une autre section avec des actions reprehensibles." What the player did of their
// own will to a character, from the Trauma ledger, at Hardcore only; "les scenes forcees subies par le joueur ne sont pas
// affichees sur la page du joueur." If this is wrong, a player below Hardcore reads what they never opted into, what a captor
// did or forced the player to do is written as the player's own deed, or a deed is told twice.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Trauma;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MisdeedsPagePolicyTests
   {
      private static string When(int day) => day switch {100 => "spring 1091", 200 => "summer 1092", 300 => "autumn 1093", _ => $"day {day}"};

      // STAKE: Hardcore only, as the Trauma section it mirrors; a player on Explicit or below never opted into this.
      [TestCase(AdultContentLevel.Off)]
      [TestCase(AdultContentLevel.Mature)]
      [TestCase(AdultContentLevel.Explicit)]
      public void GIVEN_a_level_below_hardcore_WHEN_the_players_page_opens_THEN_no_misdeed_shows(AdultContentLevel level)
      {
         MisdeedsPagePolicy.Shows(level, true).Should().BeFalse();
      }

      // STAKE: the section is the player's own; a character's page tells what they lived through in its Trauma section.
      [Test]
      public void GIVEN_hardcore_WHEN_a_page_opens_THEN_only_the_players_page_has_the_section()
      {
         MisdeedsPagePolicy.Shows(AdultContentLevel.Hardcore, true).Should().BeTrue();
         MisdeedsPagePolicy.Shows(AdultContentLevel.Hardcore, false).Should().BeFalse();
      }

      // STAKE: Gabriel: "Les actes que le joueur a fait subir peuvent apparaitre": a prisoner the player took by force is
      // named, once per victim, counted and dated.
      [Test]
      public void GIVEN_the_player_took_a_prisoner_by_force_twice_WHEN_the_players_page_opens_THEN_one_line_tells_it()
      {
         var ledger = new List<TraumaRecord>
         {
            new TraumaRecord {VictimId = "phenoria", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.Player, Day = 100},
            new TraumaRecord {VictimId = "phenoria", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.Player, Day = 200}
         };

         IReadOnlyList<MisdeedFacts> deeds = MisdeedsPagePolicy.Of(ledger);

         deeds.Should().HaveCount(1);
         MisdeedsPagePolicy.Line(deeds[0], "Phenoria", true, When)
                           .Should().Be("You took Phenoria by force while she was your prisoner, twice, the first in spring 1091, the last in summer 1092.");
      }

      // STAKE: one deed reads as one, dated.
      [Test]
      public void GIVEN_one_forced_scene_WHEN_the_players_page_opens_THEN_it_is_told_once_with_its_season()
      {
         var deed = new MisdeedFacts {VictimId = "lucon", Kind = TraumaKind.Assaulted, Days = new[] {200}};

         MisdeedsPagePolicy.Line(deed, "Lucon", false, When).Should().Be("You took Lucon by force while he was your prisoner, in summer 1092.");
      }

      // STAKE: Gabriel: "une autre section avec des actions reprehensibles": the other deeds the ledger holds of the player
      // belong there too, each in plain words.
      [TestCase(TraumaKind.Tortured, "You tortured Ira while she was your prisoner, in spring 1091.")]
      [TestCase(TraumaKind.Interrogated, "You interrogated Ira under duress while she was your prisoner, in spring 1091.")]
      [TestCase(TraumaKind.Tormented, "You tormented Ira while she was your prisoner, in spring 1091.")]
      [TestCase(TraumaKind.Beaten, "You beat Ira, in spring 1091.")]
      [TestCase(TraumaKind.Robbed, "You robbed Ira by force, in spring 1091.")]
      public void GIVEN_each_kind_of_deed_WHEN_its_line_is_written_THEN_it_says_what_the_player_did(TraumaKind kind, string expected)
      {
         MisdeedsPagePolicy.Line(new MisdeedFacts {VictimId = "ira", Kind = kind, Days = new[] {100}}, "Ira", true, When).Should().Be(expected);
      }

      // STAKE: two different deeds to one character are two lines, the oldest first, each with its own count.
      [Test]
      public void GIVEN_a_prisoner_the_player_robbed_then_took_by_force_WHEN_gathered_THEN_two_entries_the_oldest_first()
      {
         var ledger = new List<TraumaRecord>
         {
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.Player, Day = 200},
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Robbed, Author = TraumaAuthor.Player, Day = 100}
         };

         MisdeedsPagePolicy.Of(ledger).Select(d => d.Kind).Should().Equal(TraumaKind.Robbed, TraumaKind.Assaulted);
      }

      // STAKE: Gabriel: "Les scenes forcees subies par le joueur ne sont pas affichees sur la page du joueur." A scene where
      // the captor forced the player's own hands is also the player's ordeal, not the player's act.
      [Test]
      public void GIVEN_what_a_captor_did_or_forced_the_player_to_do_WHEN_the_players_page_opens_THEN_none_of_it_shows()
      {
         var ledger = new List<TraumaRecord>
         {
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.Captor, Day = 100},
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Tortured, Author = TraumaAuthor.CaptorsMen, Day = 100},
            new TraumaRecord {VictimId = "ira", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.PlayerCoerced, Day = 100}
         };

         MisdeedsPagePolicy.Of(ledger).Should().BeEmpty();
      }
   }
}
