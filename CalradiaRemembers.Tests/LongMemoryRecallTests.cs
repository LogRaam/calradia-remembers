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

      // -- Threads (Gabriel, 17/11/2026: "je peux parler de ma mission a un NPC a plusieurs reprises dans differentes
      //    conversations, et la memoire devrait lier ces conversations") --

      private static StoryArchiveRecord On(StoryArchiveRecord line, params string[] threads)
      {
         return new StoryArchiveRecord {
            Id = line.Id, Kind = line.Kind, EpisodeId = line.EpisodeId, Time = line.Time, Text = line.Text, IsPlayer = line.IsPlayer,
            SpeakerId = line.SpeakerId, Audience = line.Audience, Threads = threads.ToList()
         };
      }

      private static List<StoryArchiveRecord> Mission()
         => new List<StoryArchiveRecord> {
            Line("first", "Small talk about the road.", time: 10), On(Line("first", "Clear the bandits camped near Varcheg and I will pay you well.", time: 10), "place:varcheg"),
            Line("first", "More small talk.", time: 10),
            Line("second", "How is your sister?", time: 20), On(Line("second", "The Varcheg lot moved up into the hills, mind.", time: 20), "place:varcheg"),
            Line("second", "Safe roads.", time: 20),
            Line("third", "Cold today.", time: 30), On(Line("third", "For Varcheg I can now offer two thousand, not one.", time: 30), "place:varcheg"),
            Line("third", "Go well.", time: 30),
            Line("other", "We spoke of bandits once, near Pravend, long ago.", time: 5)
         };

      // STAKE: the mission told over three conversations comes back as one story, not as the single line whose words match.
      [Test]
      public void GIVEN_a_mission_spoken_of_in_three_conversations_WHEN_the_player_asks_about_the_bandits_THEN_all_three_are_recalled_in_order()
      {
         IReadOnlyList<LongMemoryPassage> found = Find(Mission(), "How goes it with those bandits?");

         found.Where(p => p.ThreadKeys.Contains("place:varcheg")).Select(p => p.EpisodeId).Should().Equal("first", "second", "third");
      }

      // STAKE: terms changed in the third talk; the character must know which passage says how things stand now.
      [Test]
      public void GIVEN_a_thread_over_several_conversations_WHEN_recalled_THEN_only_the_newest_is_marked_latest()
      {
         IReadOnlyList<LongMemoryPassage> found = Find(Mission(), "How goes it with those bandits?");

         found.Where(p => p.IsLatestOnThread).Select(p => p.EpisodeId).Should().Equal("third");
      }

      // STAKE: the player names the place without any word the old lines share: the thread alone finds them.
      [Test]
      public void GIVEN_no_word_in_common_WHEN_the_players_line_names_the_threads_subject_THEN_the_thread_is_recalled()
      {
         IReadOnlyList<LongMemoryPassage> found = LongMemoryRecall.Find(new LongMemoryRecallFacts {
            Records = Mission(), CharacterId = Her, Query = "Any news?", QueryThreads = new[] {"place:varcheg"}, CurrentEpisodeId = "now"
         });

         found.Select(p => p.EpisodeId).Should().Contain(new[] {"first", "second", "third"});
      }

      // STAKE: the memories a reply spoke of ([DISCUSSED]) link a conversation whose lines carry no key at all.
      [Test]
      public void GIVEN_a_thread_learned_after_the_lines_WHEN_recalled_THEN_that_conversation_joins_the_thread()
      {
         var records = new List<StoryArchiveRecord> {
            On(Line("a", "The ford, that night, the arrow in my shoulder.", time: 3), "memory:comp_ira:m2"),
            Line("b", "I still feel it when the weather turns.", time: 9),
            new StoryArchiveRecord {Id = "t", Kind = StoryArchiveKind.Thread, EpisodeId = "b", Threads = new List<string> {"memory:comp_ira:m2"}}
         };

         Find(records, "Tell me again about the ford.").Select(p => p.EpisodeId).Should().Contain("b");
      }

      // STAKE: a thread never hands a character a conversation they did not hear.
      [Test]
      public void GIVEN_a_conversation_on_the_thread_she_did_not_hear_WHEN_recalled_THEN_it_stays_out()
      {
         List<StoryArchiveRecord> records = Mission();
         records.Add(new StoryArchiveRecord {
            Id = "x", Kind = StoryArchiveKind.Line, EpisodeId = "elsewhere", Time = 25, Text = "Varcheg is mine to clear.", SpeakerId = "lord_x",
            Audience = new List<string> {"lord_x"}, Threads = new List<string> {"place:varcheg"}
         });

         Find(records, "How goes it with those bandits?").Select(p => p.EpisodeId).Should().NotContain("elsewhere");
      }

      // STAKE: threads spend the same budget; a long thread never swells the prompt past it.
      [Test]
      public void GIVEN_a_long_thread_WHEN_recalled_THEN_the_budget_holds()
      {
         var records = new List<StoryArchiveRecord>();
         for (var e = 0; e < 30; e++)
         {
            records.Add(Line($"ep{e}", $"plain words {e}", time: e));
            records.Add(On(Line($"ep{e}", $"the Varcheg matter, part {e}, and more words to make it long enough to count", time: e), "place:varcheg"));
         }

         Find(records, "Varcheg?", budget: 800).SelectMany(p => p.Lines).Sum(l => l.Text.Length + 12).Should().BeLessThanOrEqualTo(800);
      }
   }
}
