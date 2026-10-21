// Code written by Gabriel Mailhot, 20/10/2026.
// The encyclopedia's Trauma section (Gabriel, 20/10/2026): at Hardcore, the aggressions a character lived through, "uniquement
// ceux des PNJ, pas ceux du joueur car on ne decide pas a la place du joueur ce qui est traumatisant pour lui." If this is
// wrong, the player's own page judges what was traumatic for them, a lower adult level shows it, the page names the player
// as the author of what a captor did, or a scene closed twice is listed twice.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Trauma;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TraumaPagePolicyTests
   {
      private static string When(int day) => day == 100 ? "spring 1092" : $"day {day}";

      // STAKE: Gabriel's ruling: never on the player's page, whatever the level; only at Hardcore on a character's.
      [Test]
      public void GIVEN_the_adult_level_and_whose_page_it_is_WHEN_the_section_is_weighed_THEN_only_a_characters_page_at_hardcore_shows_it()
      {
         TraumaPagePolicy.Shows(AdultContentLevel.Hardcore, isThePlayersPage: false).Should().BeTrue();
         TraumaPagePolicy.Shows(AdultContentLevel.Hardcore, isThePlayersPage: true).Should().BeFalse();
         TraumaPagePolicy.Shows(AdultContentLevel.Explicit, isThePlayersPage: false).Should().BeFalse();
      }

      // STAKE: what a captor did is his, not the player's: the player who was made to watch is never named its author.
      [Test]
      public void GIVEN_a_companion_broken_by_a_captor_WHEN_her_page_opens_THEN_the_captor_is_named_and_the_player_only_saw_it()
      {
         var r = new TraumaRecord {VictimId = "c", CaptorId = "lord_g", Kind = TraumaKind.Tortured, Author = TraumaAuthor.Captor, Day = 100};

         TraumaPagePolicy.Line(r, victimIsFemale: true, "Garios", When).Should().Be("She was tortured by Garios while held captive, before your eyes, in spring 1092.");
      }

      // STAKE: the coerced player is told it was forced on them; a faceless captor is "her captors", and a captor's sex is
      // never guessed.
      [Test]
      public void GIVEN_the_player_forced_to_hurt_a_companion_by_faceless_captors_WHEN_her_page_opens_THEN_it_says_it_was_forced_on_the_player()
      {
         var r = new TraumaRecord {VictimId = "c", Kind = TraumaKind.Assaulted, Author = TraumaAuthor.PlayerCoerced, Day = 100};

         TraumaPagePolicy.Line(r, victimIsFemale: true, null, When)
                         .Should().Be("She was assaulted by your own hands, forced on you by her captors, while you were both held captive, in spring 1092.");
      }

      // STAKE: what the player did of their own will is said plainly, to them.
      [Test]
      public void GIVEN_a_prisoner_tortured_by_the_player_and_a_man_beaten_by_him_WHEN_their_pages_open_THEN_each_says_it_was_the_player()
      {
         TraumaPagePolicy.Line(new TraumaRecord {Kind = TraumaKind.Tortured, Author = TraumaAuthor.Player, Day = 100}, false, null, When)
                         .Should().Be("He was tortured by you while your prisoner, in spring 1092.");
         TraumaPagePolicy.Line(new TraumaRecord {Kind = TraumaKind.Beaten, Author = TraumaAuthor.Player, Day = 100}, false, null, When)
                         .Should().Be("He was beaten down by you, in spring 1092.");
      }

      // STAKE: Gabriel (20/10/2026): "oui, ajoute le vol". A purse taken by force is an aggression the victim lived through,
      // told as the player's own deed, never as something done while a prisoner.
      [Test]
      public void GIVEN_a_woman_the_player_robbed_by_force_WHEN_her_page_opens_THEN_it_says_the_player_robbed_her()
      {
         TraumaPagePolicy.Line(new TraumaRecord {Kind = TraumaKind.Robbed, Author = TraumaAuthor.Player, Day = 100}, true, null, When)
                         .Should().Be("She was robbed at swordpoint by you, in spring 1092.");
      }

      // STAKE: the words follow what the scene was; a scene of force reads as an assault, never as something gentler.
      [Test]
      public void GIVEN_each_captive_scene_intent_WHEN_its_kind_is_read_THEN_torture_and_force_and_interrogation_keep_their_names()
      {
         TraumaPagePolicy.KindOf(CaptiveSceneIntent.Torture).Should().Be(TraumaKind.Tortured);
         TraumaPagePolicy.KindOf(CaptiveSceneIntent.Domination).Should().Be(TraumaKind.Assaulted);
         TraumaPagePolicy.KindOf(CaptiveSceneIntent.Training).Should().Be(TraumaKind.Assaulted);
         TraumaPagePolicy.KindOf(CaptiveSceneIntent.PersonalDesire).Should().Be(TraumaKind.Assaulted);
         TraumaPagePolicy.KindOf(CaptiveSceneIntent.Interrogation).Should().Be(TraumaKind.Interrogated);
      }

      // STAKE: a scene closed twice is one aggression, listed once.
      [Test]
      public void GIVEN_an_aggression_already_in_the_ledger_WHEN_the_same_is_offered_again_THEN_the_ledger_already_holds_it()
      {
         var r = new TraumaRecord {VictimId = "c", Kind = TraumaKind.Tortured, Author = TraumaAuthor.Captor, Day = 100};

         TraumaPagePolicy.Holds(new List<TraumaRecord> {r}, new TraumaRecord {VictimId = "c", Kind = TraumaKind.Tortured, Author = TraumaAuthor.Captor, Day = 100})
                         .Should().BeTrue();
         TraumaPagePolicy.Holds(new List<TraumaRecord> {r}, new TraumaRecord {VictimId = "c", Kind = TraumaKind.Tortured, Author = TraumaAuthor.Captor, Day = 140})
                         .Should().BeFalse();
      }

      // STAKE: the ledger survives a save and a load, and a line a later version wrote never breaks the load.
      [Test]
      public void GIVEN_a_saved_ledger_with_a_line_from_a_later_version_WHEN_read_back_THEN_every_known_record_returns_and_the_unknown_is_skipped()
      {
         var ledger = new List<TraumaRecord>
         {
            new() {VictimId = "c", CaptorId = "lord_g", Kind = TraumaKind.Tortured, Author = TraumaAuthor.Captor, Day = 100},
            new() {VictimId = "d", Kind = TraumaKind.Beaten, Author = TraumaAuthor.Player, Day = 140}
         };

         List<TraumaRecord> back = TraumaPagePolicy.Read(TraumaPagePolicy.Write(ledger) + "\ne||Drowned|Player|150");

         back.Should().BeEquivalentTo(ledger);
      }
   }
}
