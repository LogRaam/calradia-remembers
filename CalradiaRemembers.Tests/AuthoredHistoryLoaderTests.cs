// Code written by Gabriel Mailhot, 17/10/2026.
// Authored history (docs/design/AUTHORED_HISTORY.md). Gabriel, 17/10/2026: a file of the world's past lets him "mettre
// en scene des evenements du passe qui ne se sont pas produits dans le temps de jeu", one file per campaign, edited on a
// timeline page. AuthoredHistoryLoader decides which of the author's events the game takes: each is kept, kept with a
// warning, or set aside, and the problems are said in plain words. If it is too loose, a lord the encyclopedia shows
// alive is remembered as executed, or the past is dated inside the campaign the game itself recorded; too strict, and
// a whole history is lost to one typo.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredHistory;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredHistoryLoaderTests
   {
      private const int DaysInSeason = 21;
      private const int SeasonsInYear = 4;

      // The vanilla start: summer 1084.
      private static readonly int Start = AuthoredHistoryLoader.DayOf(1084, "summer", DaysInSeason, SeasonsInYear);

      private static HistoryWorldFacts World() => new() {
         DaysInSeason = DaysInSeason, SeasonsInYear = SeasonsInYear, CampaignStartDay = Start, PlayerId = "main_hero",
         Heroes = new List<HistoryHeroFacts> {
            new() {Id = "main_hero", Name = "Fercread", BirthDay = AuthoredHistoryLoader.DayOf(1060, null, DaysInSeason, SeasonsInYear)},
            new() {Id = "lord_derthert", Name = "Derthert", BirthDay = AuthoredHistoryLoader.DayOf(1040, null, DaysInSeason, SeasonsInYear), SpouseId = "lady_a"},
            new() {Id = "lord_young", Name = "Young", BirthDay = AuthoredHistoryLoader.DayOf(1080, null, DaysInSeason, SeasonsInYear)},
            new() {Id = "lord_dead", Name = "Old Dead", BirthDay = AuthoredHistoryLoader.DayOf(1020, null, DaysInSeason, SeasonsInYear), IsDead = true},
            new() {Id = "lady_a", Name = "Ada", SpouseId = "lord_derthert"},
            new() {Id = "twin_1", Name = "Twin"},
            new() {Id = "twin_2", Name = "Twin"}
         },
         PlaceIds = new HashSet<string> {"town_EN1"},
         RealmIds = new HashSet<string> {"vlandia"}
      };

      private static AuthoredHistoryEvent Feud(string id = "feud", int year = 1075) => new() {
         Id = id, Year = year, Season = "spring", Kind = "feud", Summary = "Derthert and Ada quarrelled.", Actors = new List<string> {"lord_derthert"},
         Harmed = new List<string> {"lady_a"}
      };

      private static AuthoredHistoryLoadResult Load(params AuthoredHistoryEvent[] events)
         => AuthoredHistoryLoader.Validate(new AuthoredHistoryDocument {Events = events.ToList()}, World());

      // STAKE: the date the timeline shows is the date the game files it under; off by a season, an author's spring
      // betrayal reads as a summer one, and the "before the campaign" line moves.
      [Test]
      public void GIVEN_a_year_and_a_season_WHEN_dated_THEN_it_is_the_first_day_of_that_season_on_the_game_calendar()
      {
         AuthoredHistoryLoader.DayOf(1084, "summer", 21, 4).Should().Be(1084 * 84 + 21);
         AuthoredHistoryLoader.DayOf(1084, null, 21, 4).Should().Be(1084 * 84, "no season reads as spring");
      }

      // STAKE: Gabriel's own example of the file's purpose: an event of the past, kept with its people, place and date.
      [Test]
      public void GIVEN_a_sound_event_of_the_past_WHEN_loaded_THEN_it_is_kept_resolved_and_dated()
      {
         AuthoredHistoryLoadResult result = Load(Feud());

         result.Problems.Should().BeEmpty();
         result.Events.Should().ContainSingle().Which.Day.Should().Be(AuthoredHistoryLoader.DayOf(1075, "spring", DaysInSeason, SeasonsInYear));
      }

      // STAKE: Gabriel: events "qui ne se sont pas produits dans le temps de jeu". Inside the campaign, the game owns
      // the time; an authored battle last week would sit beside what really happened then.
      [Test]
      public void GIVEN_an_event_dated_inside_the_campaign_WHEN_loaded_THEN_it_is_set_aside()
      {
         AuthoredHistoryLoadResult result = Load(Feud(year: 1085));

         result.Events.Should().BeEmpty();
         result.Problems.Should().ContainSingle().Which.Should().Contain("not before the campaign began");
      }

      // STAKE: never fake what the player can verify: the encyclopedia shows the lord alive.
      [Test]
      public void GIVEN_an_execution_of_a_lord_alive_today_WHEN_loaded_THEN_it_is_set_aside()
      {
         AuthoredHistoryEvent e = Feud();
         e.Kind = "execution";
         e.Harmed = new List<string> {"lord_derthert"};

         AuthoredHistoryLoadResult result = Load(e);

         result.Events.Should().BeEmpty();
         result.Problems.Single().Should().Contain("is alive today");
      }

      // STAKE: a lord dead since is still someone the past can name; set aside, every history of an old war would lose
      // the men who died in it.
      [Test]
      public void GIVEN_a_fall_in_battle_of_a_lord_dead_today_WHEN_loaded_THEN_it_is_kept()
      {
         AuthoredHistoryEvent e = Feud();
         e.Kind = "fell_in_battle";
         e.Harmed = new List<string> {"lord_dead"};

         Load(e).Events.Should().ContainSingle();
      }

      // STAKE: a boy of four cannot have betrayed a city; the page would show a child as a turncoat.
      [Test]
      public void GIVEN_a_hero_dated_before_his_birth_WHEN_loaded_THEN_it_is_set_aside()
      {
         AuthoredHistoryEvent e = Feud();
         e.Actors = new List<string> {"lord_young"};

         Load(e).Problems.Single().Should().Contain("not yet born");
      }

      // STAKE: namesakes are different people (HeroLabel's lesson); a guess would give one twin the other's past.
      [Test]
      public void GIVEN_a_name_two_heroes_bear_WHEN_loaded_THEN_it_is_set_aside_and_the_id_is_asked_for()
      {
         AuthoredHistoryEvent e = Feud();
         e.Actors = new List<string> {"Twin"};

         Load(e).Problems.Single().Should().Contain("use the id");
      }

      // STAKE: a file edited by hand can use a name, when only one hero bears it.
      [Test]
      public void GIVEN_a_name_only_one_hero_bears_WHEN_loaded_THEN_it_resolves_to_his_id()
      {
         AuthoredHistoryEvent e = Feud();
         e.Actors = new List<string> {"derthert"};

         Load(e).Events.Single().ActorIds.Should().Equal("lord_derthert");
      }

      // STAKE: a marriage the encyclopedia does not show is plausible history (a divorce, a widowing): kept, but said.
      [Test]
      public void GIVEN_a_marriage_of_two_heroes_not_wed_today_WHEN_loaded_THEN_it_is_kept_with_a_warning()
      {
         AuthoredHistoryEvent e = Feud();
         e.Kind = "marriage";
         e.Harmed = new List<string> {"lord_dead"};

         AuthoredHistoryLoadResult result = Load(e);

         result.Events.Should().ContainSingle();
         result.Problems.Single().Should().Contain("not married today");
      }

      // STAKE: the id is how an edit updates an event; two with one id, and the second would overwrite the first.
      [Test]
      public void GIVEN_two_events_with_one_id_WHEN_loaded_THEN_the_second_is_set_aside()
      {
         AuthoredHistoryLoadResult result = Load(Feud("same"), Feud("same"));

         result.Events.Should().ContainSingle();
         result.Problems.Single().Should().Contain("already has this id");
      }

      // STAKE: one bad relation must not cost the author the event it hangs on.
      [Test]
      public void GIVEN_a_relation_naming_one_hero_twice_or_both_modes_WHEN_loaded_THEN_only_the_relation_is_dropped()
      {
         AuthoredHistoryEvent e = Feud();
         e.Relations = new List<AuthoredRelation> {
            new() {A = "lord_derthert", B = "lord_derthert", Change = -20},
            new() {A = "lord_derthert", B = "lady_a", Change = -20, Set = -50},
            new() {A = "lord_derthert", B = "lady_a", Change = -300}
         };

         AuthoredHistoryLoadResult result = Load(e);

         result.Events.Single().Relations.Should().ContainSingle().Which.Change.Should().Be(-100, "a change is clamped to vanilla's bounds");
         result.Problems.Should().HaveCount(2);
      }

      // STAKE: an NPC's feeling toward the player comes from the mod's personal regard; a relation that names the
      // player is marked so it writes that regard too, or "you betrayed him" would barely show in conversation.
      [Test]
      public void GIVEN_a_relation_with_the_player_WHEN_loaded_THEN_it_is_marked_and_the_event_is_the_players_past()
      {
         AuthoredHistoryEvent e = Feud();
         e.Actors = new List<string> {"main_hero"};
         e.Relations = new List<AuthoredRelation> {new() {A = "main_hero", B = "lady_a", Set = -60}};

         AcceptedHistoryEvent kept = Load(e).Events.Single();

         kept.InvolvesPlayer.Should().BeTrue();
         kept.Relations.Single().TouchesPlayer.Should().BeTrue();
      }

      // STAKE: a broken file says so and loads nothing, rather than crashing the load or half-reading.
      [Test]
      public void GIVEN_text_that_is_not_json_WHEN_parsed_THEN_nothing_loads_and_the_problem_is_said()
      {
         var problems = new List<string>();

         AuthoredHistoryLoader.Parse("{ not json", problems).Should().BeNull();
         problems.Should().ContainSingle().Which.Should().Contain("not valid JSON");
      }

      // STAKE: the page writes this shape; the loader must read it field for field.
      [Test]
      public void GIVEN_the_format_the_page_writes_WHEN_parsed_THEN_every_field_is_read()
      {
         const string json = "{\"format\":1,\"campaign\":\"c1\",\"title\":\"T\",\"events\":[{\"id\":\"x\",\"year\":1078,\"season\":\"autumn\",\"kind\":\"betrayal\","
                             + "\"summary\":\"S\",\"actors\":[\"a\"],\"harmed\":[\"b\"],\"place\":\"town_EN1\",\"realms\":[\"vlandia\"],\"importance\":60,"
                             + "\"secret\":true,\"knownBy\":[\"c\"],\"relations\":[{\"a\":\"a\",\"b\":\"b\",\"set\":-60}]}]}";

         AuthoredHistoryEvent e = AuthoredHistoryLoader.Parse(json, new List<string>()).Events.Single();

         e.Season.Should().Be("autumn");
         e.Importance.Should().Be(60);
         e.Secret.Should().BeTrue();
         e.KnownBy.Should().Equal("c");
         e.Relations.Single().Set.Should().Be(-60);
         e.Relations.Single().Change.Should().BeNull();
      }
   }
}
