// Code written by Gabriel Mailhot, 17/11/2026.
// The story archive (Gabriel, 17/11/2026: the Story Log replaced by a long-term record of every episode, written always)
// keeps the player's story outside the save. These pin the three things it cannot get wrong: a reloaded save reads only its
// own past (StoryArchiveBranch), a record survives being written and read back, a torn line never breaks the file, and the
// Story Log still reads as it always did (StoryArchiveNovel). If they break, a character remembers a future that never
// happened, the player's story is lost to one bad line, or the Story Log the player reads changes under them.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.StoryArchive;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class StoryArchiveTests
   {
      private static StoryArchiveRecord R(string id, string parent, string text = "x")
         => new StoryArchiveRecord {Id = id, ParentId = parent, Kind = StoryArchiveKind.Line, Text = text};

      // STAKE: a save reloaded before a long talk must not remember that talk: the abandoned branch is unreachable.
      [Test]
      public void GIVEN_a_branch_abandoned_by_a_reload_WHEN_the_save_head_is_walked_THEN_only_that_saves_past_is_read()
      {
         // a -> b (saved here) -> c -> d, then the player reloads b and plays e -> f
         Dictionary<string, StoryArchiveRecord> byId = StoryArchiveBranch.Index(new[] {
            R("a", null), R("b", "a"), R("c", "b"), R("d", "c"), R("e", "b"), R("f", "e")
         });

         StoryArchiveBranch.Walk(byId, "f").Select(r => r.Id).Should().Equal("a", "b", "e", "f");
         StoryArchiveBranch.Walk(byId, "d").Select(r => r.Id).Should().Equal("a", "b", "c", "d");
      }

      // STAKE: an older save (no head) or a head the file no longer has reads an empty past, never another branch's.
      [Test]
      public void GIVEN_no_head_or_an_unknown_head_WHEN_walked_THEN_nothing_is_read()
      {
         Dictionary<string, StoryArchiveRecord> byId = StoryArchiveBranch.Index(new[] {R("a", null)});

         StoryArchiveBranch.Walk(byId, null).Should().BeEmpty();
         StoryArchiveBranch.Walk(byId, "zz").Should().BeEmpty();
      }

      // STAKE: a hand-edited file with a loop must not hang the game on load.
      [Test]
      public void GIVEN_a_cycle_WHEN_walked_THEN_it_stops()
      {
         Dictionary<string, StoryArchiveRecord> byId = StoryArchiveBranch.Index(new[] {R("a", "b"), R("b", "a")});

         StoryArchiveBranch.Walk(byId, "a").Should().HaveCount(2);
      }

      // STAKE: who heard a line is what later decides who may remember it; it must come back exactly.
      [Test]
      public void GIVEN_a_line_with_its_audience_WHEN_written_and_read_back_THEN_nothing_is_lost()
      {
         var line = new StoryArchiveRecord {
            Id = "r1", ParentId = "r0", Kind = StoryArchiveKind.Line, EpisodeId = "s1", SceneKind = "conversation", Time = 812.25,
            SpeakerId = "lord_1_1", SpeakerName = "Lucon", Text = "You said you would teach me.", Audience = new List<string> {"lord_1_1", "comp_3"},
            Tags = new List<string> {"lord_1_1"}
         };

         StoryArchiveRecord back = StoryArchiveRecord.Deserialize(line.Serialize());

         back.Should().BeEquivalentTo(line);
      }

      // STAKE: a crash mid-write leaves a torn last line; it is skipped, the rest of the story stays readable.
      [TestCase("")]
      [TestCase("{\"Id\":\"r1\",\"Kind\":\"Li")]
      [TestCase("{\"Kind\":\"Line\"}")]
      public void GIVEN_a_torn_or_idless_line_WHEN_read_THEN_it_is_skipped(string line)
      {
         StoryArchiveRecord.Deserialize(line).Should().BeNull();
      }

      // STAKE: the Story Log the player reads keeps its novel form: the meeting, then the lines.
      [Test]
      public void GIVEN_an_episode_and_its_lines_WHEN_told_THEN_the_story_log_reads_as_before()
      {
         string nl = Environment.NewLine;
         var start = new StoryArchiveRecord {
            Kind = StoryArchiveKind.EpisodeStart, Time = 812.4, PlaceName = "the castle of Poros",
            Participants = new List<StoryArchiveParticipant> {new StoryArchiveParticipant {Name = "Alympia"}, new StoryArchiveParticipant {Name = "Martira"}}
         };

         StoryArchiveNovel.Render(start).Should().Be($"{nl}[Day 812]{nl}{nl}You meet Alympia and Martira in the castle of Poros.{nl}");
         StoryArchiveNovel.Render(new StoryArchiveRecord {Kind = StoryArchiveKind.Line, IsPlayer = true, Text = "Well met."}).Should().Be($"{nl}You: Well met.{nl}");
         StoryArchiveNovel.Render(new StoryArchiveRecord {Kind = StoryArchiveKind.Line, SpeakerName = "Alympia", Text = "Is it?"}).Should().Be($"{nl}Alympia: Is it?{nl}");
      }

      // STAKE: the novel never tells a deed the game refused, nor an out-of-character note, as if it had happened.
      [Test]
      public void GIVEN_a_refused_deed_or_a_note_WHEN_told_THEN_the_novel_shows_nothing()
      {
         StoryArchiveNovel.Render(new StoryArchiveRecord {Kind = StoryArchiveKind.Action, Text = "Garios joins your clan.", Refused = true}).Should().BeNull();
         StoryArchiveNovel.Render(new StoryArchiveRecord {Kind = StoryArchiveKind.Note, Text = "(OOC: the marriage was refused)"}).Should().BeNull();
         StoryArchiveNovel.Render(new StoryArchiveRecord {Kind = StoryArchiveKind.Action, Text = "Garios joins your clan."}).Should().Contain("Garios joins your clan.");
      }

      // STAKE: the coin and the regard are told as the game applied them, in the Story Log's own words.
      [Test]
      public void GIVEN_coin_and_regard_the_game_applied_WHEN_told_THEN_the_sentences_say_so()
      {
         StoryArchiveNovel.CoinSentence("Lucon", 500, true).Should().Be("Lucon hands you 500 denars.");
         StoryArchiveNovel.CoinSentence("Lucon", 500, false).Should().Be("You hand Lucon 500 denars.");
         StoryArchiveNovel.CoinSentence("Lucon", 0, false).Should().BeNull();
         StoryArchiveNovel.RegardSentence("Lucon", 2).Should().Be("Lucon seems to appreciate your words.");
         StoryArchiveNovel.RegardSentence("Lucon", -9).Should().Be("A cold distance settles over Lucon.");
         StoryArchiveNovel.RegardSentence("Lucon", 0).Should().BeNull();
      }

      // STAKE: the archive keeps what the game did: the coin actually moved, not what the model proposed.
      [Test]
      public void GIVEN_a_gold_deed_WHEN_entered_THEN_the_sentence_carries_the_coin_the_game_moved()
      {
         StoryArchiveDeed.Entry(new StoryArchiveDeedFacts {ActionType = "give_gold", ActorName = "Lucon", AppliedCoin = 300, Message = "Lucon gives you 9999."})
                         .Text.Should().Be("Lucon hands you 300 denars.");
      }

      // STAKE: a regard change gated to nothing, or a verb the model invented, did not happen: no record.
      [Test]
      public void GIVEN_a_deed_that_did_not_happen_WHEN_entered_THEN_nothing_is_kept()
      {
         StoryArchiveDeed.Entry(new StoryArchiveDeedFacts {ActionType = "change_relation", ActorName = "Lucon", AppliedRegard = 0}).Should().BeNull();
         StoryArchiveDeed.Entry(new StoryArchiveDeedFacts {ActionType = "change_relation", ActorName = "Lucon"}).Should().BeNull();
         StoryArchiveDeed.Entry(new StoryArchiveDeedFacts {ActionType = "summon_dragon", UnknownVerb = true, Message = "?"}).Should().BeNull();
      }

      // STAKE: a refused deed is kept as refused (the character was asked), so the novel and later memory never take it as done.
      [Test]
      public void GIVEN_a_refused_deed_WHEN_entered_THEN_it_is_kept_marked_refused()
      {
         StoryArchiveDeedEntry entry = StoryArchiveDeed.Entry(new StoryArchiveDeedFacts {ActionType = "join_clan", Message = "Garios cannot join: you are at war.", Refused = true});

         entry.Refused.Should().BeTrue();
         entry.Text.Should().Be("Garios cannot join: you are at war.");
      }

      // STAKE: any other deed is told in the line the game already showed the player.
      [Test]
      public void GIVEN_another_deed_WHEN_entered_THEN_the_games_own_line_tells_it()
      {
         StoryArchiveDeed.Entry(new StoryArchiveDeedFacts {ActionType = "join_clan", Message = "Garios joins your clan."}).Text.Should().Be("Garios joins your clan.");
      }

      // STAKE: an out-of-character note is not a line the player said in the story.
      [Test]
      public void GIVEN_transcript_lines_WHEN_classified_THEN_notes_and_narration_are_told_apart()
      {
         StoryArchiveDeed.KindOfTranscriptLine(true, false, "(Out of character: the game refused that.)").Should().Be(StoryArchiveKind.Note);
         StoryArchiveDeed.KindOfTranscriptLine(true, false, "Well met.").Should().Be(StoryArchiveKind.Line);
         StoryArchiveDeed.KindOfTranscriptLine(false, true, "Rain.").Should().Be(StoryArchiveKind.Narration);
      }

      // STAKE: letters, which the Story Log carried, are not lost in the move.
      [Test]
      public void GIVEN_a_letter_received_WHEN_told_THEN_it_keeps_its_heading()
      {
         StoryArchiveNovel.Render(new StoryArchiveRecord {Kind = StoryArchiveKind.Letter, SpeakerName = "Lucon", Time = 30, Text = "Come at once."})
                          .Should().Contain("--- Letter from Lucon (Day 30) ---").And.Contain("Come at once.");
      }
   }
}
