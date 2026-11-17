// Code written by Gabriel Mailhot, 17/11/2026.
// LongMemoryRecall picks, before a reply, the passages of earlier conversations a character may quote word for word
// (Goeth, Discord 17/11/2026: "a 150-line night in a tavern ... The next time we talked she remembered it in two lines").
// Its answer goes into the prompt under "WORDS FROM EARLIER TALKS". If it is wrong, a character quotes words said where they
// were not, quotes what the model already reads, splices two nights into one passage, or swells the prompt past the budget
// the player set.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.StoryArchive;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LongMemoryRecallTests
   {
      private const string Her = "comp_ira";

      private static StoryArchiveRecord Line(string episode, string text, bool player = false, double time = 10, params string[] audience)
         => new StoryArchiveRecord {
            Id = System.Guid.NewGuid().ToString("N"), Kind = StoryArchiveKind.Line, EpisodeId = episode, Time = time, Text = text, IsPlayer = player,
            SpeakerId = player ? "main_hero" : Her, Audience = (audience.Length == 0 ? new[] {Her} : audience).ToList()
         };

      private static List<StoryArchiveRecord> TavernNight()
      {
         var lines = new List<StoryArchiveRecord>();
         for (var i = 0; i < 12; i++) lines.Add(Line("tavern", $"small talk number {i} about ale and weather", i % 2 == 0));
         lines.Insert(6, Line("tavern", "You promised you would teach me the longsword before winter.", false));
         return lines;
      }

      private static IReadOnlyList<LongMemoryPassage> Find(IReadOnlyList<StoryArchiveRecord> records, string query, string current = "now", int budget = 4000, ISet<string> already = null)
         => LongMemoryRecall.Find(new LongMemoryRecallFacts {
            Records = records, CharacterId = Her, Query = query, CurrentEpisodeId = current, BudgetChars = budget, AlreadyInContext = already
         });

      // STAKE: the thread the player picks up ("the longsword") brings back the very promise, with the lines around it.
      [Test]
      public void GIVEN_a_promise_in_an_earlier_night_WHEN_the_player_mentions_it_THEN_the_passage_around_it_is_recalled()
      {
         IReadOnlyList<LongMemoryPassage> found = Find(TavernNight(), "So, about that longsword lesson?");

         found.Should().HaveCount(1);
         found[0].Lines.Should().Contain(l => l.Text.Contains("teach me the longsword"));
         found[0].Lines.Count.Should().Be(2 * LongMemoryRecall.Radius + 1);
      }

      // STAKE (A/B, 17/11/2026): "about what you promised me that night" recalled the goat story, not the promise,
      // because the night said "promise" and the player said "promised".
      [Test]
      public void GIVEN_the_player_says_promised_WHEN_the_night_said_promise_THEN_the_promise_is_recalled()
      {
         var records = new List<StoryArchiveRecord> {
            Line("night", "One night the goat ate his boots."), Line("night", "filler one"), Line("night", "filler two"),
            Line("night", "filler three"), Line("night", "filler four"), Line("night", "filler five"),
            Line("night", "I will teach you the longsword. That is a promise.")
         };

         Find(records, "So, about what you promised me that night...").SelectMany(p => p.Lines).Should().Contain(l => l.Text.Contains("longsword"));
      }

      // STAKE: a character never quotes words said where they were not.
      [Test]
      public void GIVEN_words_said_without_her_WHEN_recalled_THEN_she_does_not_have_them()
      {
         var records = new List<StoryArchiveRecord> {
            new StoryArchiveRecord {Id = "x", Kind = StoryArchiveKind.Line, EpisodeId = "council", Text = "The longsword is the king's gift.", SpeakerId = "lord_x", Audience = new List<string> {"lord_x"}}
         };

         Find(records, "longsword").Should().BeEmpty();
      }

      // STAKE: the conversation under way is already in the prompt; quoting it again is noise.
      [Test]
      public void GIVEN_the_match_is_in_the_current_conversation_WHEN_recalled_THEN_it_is_left_out()
      {
         Find(TavernNight(), "longsword", current: "tavern").Should().BeEmpty();
      }

      // STAKE: a resumed talk is in the history already; its lines are not recalled twice.
      [Test]
      public void GIVEN_lines_already_in_context_WHEN_recalled_THEN_they_are_left_out()
      {
         var already = new HashSet<string> {"You promised you would teach me the longsword before winter."};

         Find(TavernNight(), "longsword", already: already).SelectMany(p => p.Lines).Should().NotContain(l => l.Text.Contains("longsword"));
      }

      // STAKE: two nights are never spliced into one passage: a window stops at its own conversation.
      [Test]
      public void GIVEN_a_match_at_the_edge_of_a_conversation_WHEN_recalled_THEN_the_passage_never_crosses_into_another()
      {
         var records = new List<StoryArchiveRecord> {
            Line("first", "opening of the first night", time: 5), Line("first", "the longsword, at last", time: 5),
            Line("second", "a different evening entirely", time: 8), Line("second", "more of the second evening", time: 8)
         };

         IReadOnlyList<LongMemoryPassage> found = Find(records, "longsword");

         found.Should().ContainSingle();
         found[0].Lines.Should().OnlyContain(l => l.EpisodeId == "first");
      }

      // STAKE: the player's budget holds: the recall never writes more than the slider allows.
      [Test]
      public void GIVEN_many_matches_WHEN_recalled_THEN_the_budget_holds()
      {
         var records = new List<StoryArchiveRecord>();
         for (var e = 0; e < 20; e++)
            for (var i = 0; i < 5; i++)
               records.Add(Line($"ep{e}", $"the longsword again and again, evening {e}, line {i}", time: e));

         IReadOnlyList<LongMemoryPassage> found = Find(records, "longsword", budget: 600);

         found.SelectMany(p => p.Lines).Sum(l => l.Text.Length + 12).Should().BeLessThanOrEqualTo(600);
         found.Should().NotBeEmpty();
      }

      // STAKE: nothing that bears on this turn means nothing is added: no prompt swells with random old talk.
      [Test]
      public void GIVEN_no_word_in_common_WHEN_recalled_THEN_nothing_is_added()
      {
         Find(TavernNight(), "Greetings").Should().BeEmpty();
         Find(TavernNight(), "").Should().BeEmpty();
      }

      // STAKE: the passage says who else heard it, so the character knows the words were not a secret.
      [Test]
      public void GIVEN_others_heard_it_WHEN_recalled_THEN_they_are_named_as_present()
      {
         var records = new List<StoryArchiveRecord> {Line("camp", "Mind the longsword, it bites.", false, 3, Her, "comp_martira")};

         Find(records, "longsword")[0].AlsoPresentIds.Should().Equal("comp_martira");
      }
   }
}
