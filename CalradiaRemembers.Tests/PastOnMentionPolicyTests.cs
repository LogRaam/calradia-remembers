// Code written by Gabriel Mailhot, 17/10/2026.
// PastOnMentionPolicy decides which past events a speaker brings to a turn in which the player named a person, a place
// or a realm, and how each is worded. Gabriel (17/10/2026): the long-term memory "pourrait parfois etre utilise pour des
// conversations entre joueur et NPC sur le passe", and the authored history file is there to "mettre en scene des
// evenements du passe". If the pick is wrong, a lord asked about his own father's execution draws a blank, or a
// passing word drags up a battle of ten years ago. If the wording is wrong, he tells a betrayal of before the campaign
// as fresh news, or as hearsay when he was the betrayer.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PastOnMentionPolicyTests
   {
      private const int Year = 84;
      private const int Today = 1084 * Year;

      private static PastMentionRequest Asked(params string[] named) => new() {NamedIds = named, Today = Today, DaysInYear = Year};

      private static PastCandidateFacts Event(string id, int yearsAgo, params string[] entities) => new() {
         Id = id, EntityIds = entities, Summary = "Something befell " + string.Join(" and ", entities) + ".", Day = Today - yearsAgo * Year,
         Magnitude = 40, Tie = PastTie.Realm
      };

      // STAKE: the whole point: asked about a lord by name, the speaker brings what he knows of that lord's past.
      [Test]
      public void GIVEN_the_player_names_a_lord_WHEN_the_past_is_weighed_THEN_an_event_naming_him_is_brought_up()
      {
         string block = PastOnMentionPolicy.Block(new[] {Event("e1", 9, "lord_garios")}, Asked("lord_garios"));

         block.Should().Contain("lord_garios");
      }

      // STAKE: a keyword alone never brings up the past ("how went the battle yesterday?" must not raise a battle of
      // ten years ago); only a name the player said does.
      [Test]
      public void GIVEN_the_player_names_no_one_in_the_event_WHEN_the_past_is_weighed_THEN_nothing_is_brought_up()
      {
         PastOnMentionPolicy.Block(new[] {Event("e1", 9, "lord_garios")}, Asked("lord_other")).Should().BeNull();
         PastOnMentionPolicy.Block(new[] {Event("e1", 9, "lord_garios")}, Asked()).Should().BeNull();
      }

      // STAKE: an authored event names those it harmed and its place too; asking about the victim or the town must
      // find it, not only asking about the actor.
      [Test]
      public void GIVEN_the_player_names_the_place_of_an_event_WHEN_the_past_is_weighed_THEN_the_event_is_brought_up()
      {
         PastOnMentionPolicy.Pick(new[] {Event("e1", 9, "lord_garios", "town_pravend")}, Asked("town_pravend")).Should().HaveCount(1);
      }

      // STAKE: naming a whole realm matches dozens of events; the speaker keeps to the design's one to three, or the
      // turn drowns in history.
      [Test]
      public void GIVEN_many_events_of_a_named_realm_WHEN_the_past_is_weighed_THEN_at_most_three_are_brought_up()
      {
         IEnumerable<PastCandidateFacts> many = Enumerable.Range(0, 10).Select(i => Event("e" + i, i + 1, "vlandia"));

         PastOnMentionPolicy.Pick(many, Asked("vlandia")).Should().HaveCount(PastOnMentionPolicy.MaxLines);
      }

      // STAKE: among the matches, the greatest is told first, then the one nearest to the speaker, then the latest; a
      // fixed order so the same question gets the same answer.
      [Test]
      public void GIVEN_matches_of_different_weight_WHEN_the_past_is_weighed_THEN_the_greatest_then_the_nearest_comes_first()
      {
         PastCandidateFacts small = new() {Id = "small", EntityIds = new[] {"x"}, Summary = "s", Magnitude = 20, Tie = PastTie.Actor, Day = Today};
         PastCandidateFacts great = new() {Id = "great", EntityIds = new[] {"x"}, Summary = "g", Magnitude = 60, Tie = PastTie.Realm, Day = 1};
         PastCandidateFacts nearer = new() {Id = "nearer", EntityIds = new[] {"x"}, Summary = "n", Magnitude = 20, Tie = PastTie.Kin, Day = Today};
         PastCandidateFacts farther = new() {Id = "farther", EntityIds = new[] {"x"}, Summary = "f", Magnitude = 20, Tie = PastTie.Realm, Day = Today};

         PastOnMentionPolicy.Pick(new[] {farther, nearer, small, great}, Asked("x")).Select(c => c.Id)
                            .Should().Equal("great", "small", "nearer");
      }

      // STAKE: an event the recent-news block already shows this turn is not told twice under two headings.
      [Test]
      public void GIVEN_an_event_already_shown_this_turn_WHEN_the_past_is_weighed_THEN_it_is_not_repeated()
      {
         var r = new PastMentionRequest {NamedIds = new[] {"x"}, Today = Today, DaysInYear = Year, AlreadyShownIds = new[] {"e1"}};

         PastOnMentionPolicy.Pick(new[] {Event("e1", 2, "x")}, r).Should().BeEmpty();
      }

      // STAKE: without its age, a betrayal of before the campaign is told as this week's news.
      [Test]
      public void GIVEN_an_event_nine_years_old_WHEN_it_is_worded_THEN_the_line_says_nine_years_ago()
      {
         PastOnMentionPolicy.Line(Event("e1", 9, "x"), Asked("x")).Should().EndWith("(9 years ago).");
         PastOnMentionPolicy.Line(Event("e1", 1, "x"), Asked("x")).Should().EndWith("(a year ago).");
         PastOnMentionPolicy.Line(Event("e1", 0, "x"), Asked("x")).Should().EndWith("(within the past year).");
      }

      // STAKE: the speaker who did it does not tell it as something he heard.
      [Test]
      public void GIVEN_the_speaker_was_in_the_event_WHEN_it_is_worded_THEN_the_line_says_he_was_part_of_it()
      {
         var lived = new PastCandidateFacts {Id = "e1", EntityIds = new[] {"x"}, Summary = "He betrayed his liege.", Day = Today - 5 * Year, Tie = PastTie.Actor};

         PastOnMentionPolicy.Line(lived, Asked("x")).Should().Contain(PastOnMentionPolicy.LivedItTag);
      }

      // STAKE: a young lord speaks of his grandfather's war as a story he was told, not as a memory.
      [Test]
      public void GIVEN_an_event_older_than_the_speaker_WHEN_it_is_worded_THEN_the_line_says_he_knows_it_as_told()
      {
         var r = new PastMentionRequest {NamedIds = new[] {"x"}, Today = Today, DaysInYear = Year, SpeakerBirthDay = Today - 20 * Year};

         PastOnMentionPolicy.Line(Event("e1", 30, "x"), r).Should().Contain(PastOnMentionPolicy.BeforeBirthTag);
         PastOnMentionPolicy.Line(Event("e1", 10, "x"), r).Should().NotContain(PastOnMentionPolicy.BeforeBirthTag);
      }

      // STAKE: 67 settlements end in "Castle"; "did you hold the castle?" naming them all would drag up every old siege,
      // the very thing only-a-name was meant to prevent.
      [Test]
      public void GIVEN_a_word_shared_by_many_place_names_WHEN_the_message_uses_it_THEN_it_names_none_of_them()
      {
         var places = new[] {
            new KeyValuePair<string, string>("castle_1", "Ortongard Castle"), new KeyValuePair<string, string>("castle_2", "Druimmor Castle"),
            new KeyValuePair<string, string>("castle_3", "Hertogea Castle"), new KeyValuePair<string, string>("town_1", "Pravend")
         };

         PastOnMentionPolicy.IdsNamedIn(places, new[] {"castle"}, s => s.ToLowerInvariant(), 3).Should().BeEmpty();
         PastOnMentionPolicy.IdsNamedIn(places, new[] {"ortongard"}, s => s.ToLowerInvariant(), 3).Should().Equal("castle_1");
         PastOnMentionPolicy.IdsNamedIn(places, new[] {"pravend"}, s => s.ToLowerInvariant(), 3).Should().Equal("town_1");
      }

      // STAKE: Gabriel 17/10/2026, "secret event devrait etre tagged secret": a secret from the past keeps its tag, so
      // the speaker speaks of it only in confidence.
      [Test]
      public void GIVEN_a_secret_event_WHEN_it_is_worded_THEN_the_line_carries_the_secret_tag()
      {
         var secret = new PastCandidateFacts {Id = "e1", EntityIds = new[] {"x"}, Summary = "A secret.", Day = Today, Secret = true};

         PastOnMentionPolicy.Line(secret, Asked("x")).Should().StartWith("- " + RumorDiscretion.SecretTag);
      }
   }
}
