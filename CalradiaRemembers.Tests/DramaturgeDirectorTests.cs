// Code written by Gabriel Mailhot, 28/11/2026.
// The dramaturge of the Dramaturge mode (Stage 7 of the group-scene plan; Gabriel, 28/11/2026, "DRAMA+PROSE+INTERPRET+NSFW"),
// ported from the simulator's prose-model Dramaturge. It decides, after a beat, whether the world brings one of the events the
// composer drew from true facts, or whether the scene has said what it had to say. NOT A/B-validated.
//
// What the policy decides and what breaks if it is wrong: an event arrives before the scene has set its question, two land in
// one beat, an event is brought that the player cannot see (Scene Narration off) or that was never checked, the scene "ends"
// after one exchange, or a prose call is paid for a reading that can change nothing. The composer side: the events never
// leak into the hidden tension, the stage 3 prompts do not move for anyone outside the Dramaturge mode, and only an event
// the check plainly passed is kept.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DramaturgeDirectorTests
   {
      private sealed class Canned : ILlmClient
      {
         private readonly Func<LlmRequest, string> _answer;
         public readonly List<LlmRequest> Seen = new();

         public Canned(Func<LlmRequest, string> answer) => _answer = answer;

         public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
         {
            lock (Seen) Seen.Add(request);

            return Task.FromResult(new LlmResponse {Content = _answer(request), IsSuccess = true});
         }
      }

      private static DramaturgeDecisionFacts Facts(int beats, int lastEventBeat = -1, bool narration = true, params int[] used)
         => new() {BeatsPlayed = beats, LastEventBeat = lastEventBeat, EventCount = 3, UsedEvents = used, SceneNarrationOn = narration};

      private static DramaturgeReading Event(int n) => new() {Move = DramaturgeMove.Event, EventNumber = n, HadMove = true};

      private static readonly DramaturgeReading End = new() {Move = DramaturgeMove.End, HadMove = true};

      // STAKE: a prose call after the first beat can change nothing (no event before two beats): it would only cost the player.
      [Test]
      public void GIVEN_one_beat_played_WHEN_asked_whether_to_read_THEN_no_and_from_two_beats_yes()
      {
         DramaturgeScenePolicy.ShouldRead(new DramaturgeReadFacts {DramaturgeTurn = true, BeatsPlayed = 1}).Should().BeFalse();
         DramaturgeScenePolicy.ShouldRead(new DramaturgeReadFacts {DramaturgeTurn = true, BeatsPlayed = 2}).Should().BeTrue();
      }

      // STAKE: Standard and Enhanced must not change: a beat that did not run the Dramaturge machinery never reads; nor a scene
      // already ended, nor a second reading on top of one still out.
      [Test]
      public void GIVEN_no_dramaturge_turn_an_ended_scene_or_a_reading_out_WHEN_asked_whether_to_read_THEN_no()
      {
         DramaturgeScenePolicy.ShouldRead(new DramaturgeReadFacts {DramaturgeTurn = false, BeatsPlayed = 5}).Should().BeFalse();
         DramaturgeScenePolicy.ShouldRead(new DramaturgeReadFacts {DramaturgeTurn = true, BeatsPlayed = 5, SceneEnded = true}).Should().BeFalse();
         DramaturgeScenePolicy.ShouldRead(new DramaturgeReadFacts {DramaturgeTurn = true, BeatsPlayed = 5, ReadInFlight = true}).Should().BeFalse();
      }

      // STAKE: the simulator's gate: a scene needs room to set its question before the world breaks in.
      [Test]
      public void GIVEN_an_event_asked_after_one_beat_WHEN_decided_THEN_it_is_not_brought_and_after_two_it_is()
      {
         DramaturgeScenePolicy.Decide(Event(1), Facts(1)).EventNumber.Should().Be(0);
         DramaturgeScenePolicy.Decide(Event(1), Facts(2)).EventNumber.Should().Be(1);
      }

      // STAKE: two events in one beat, or the same one twice, read as the world piling on.
      [Test]
      public void GIVEN_an_event_already_brought_this_beat_or_before_WHEN_another_is_asked_THEN_it_is_not_brought()
      {
         DramaturgeScenePolicy.Decide(Event(2), Facts(3, lastEventBeat: 3)).EventNumber.Should().Be(0);
         DramaturgeScenePolicy.Decide(Event(1), Facts(4, 3, true, 1)).EventNumber.Should().Be(0);
         DramaturgeScenePolicy.Decide(Event(2), Facts(4, 3, true, 1)).EventNumber.Should().Be(2);
      }

      // STAKE: Gabriel's rule, it chooses among true facts: a number the composer never listed brings nothing.
      [Test]
      public void GIVEN_an_event_number_not_listed_WHEN_decided_THEN_nothing_is_brought()
      {
         DramaturgeScenePolicy.Decide(Event(4), Facts(3)).EventNumber.Should().Be(0);
         DramaturgeScenePolicy.Decide(Event(0), Facts(3)).EventNumber.Should().Be(0);
      }

      // STAKE: 1Enfant turned the purple narration off: a character reacting to an event the player never saw is a mystery.
      [Test]
      public void GIVEN_scene_narration_off_WHEN_an_event_is_asked_THEN_it_is_not_brought()
      {
         DramaturgeDecision d = DramaturgeScenePolicy.Decide(Event(1), Facts(3, narration: false));

         d.EventNumber.Should().Be(0);
         d.Why.Should().Contain("Scene Narration is off");
      }

      // STAKE: the simulator's intimate scene ended at beat 8 just before its point; here "end" never comes before three beats.
      [Test]
      public void GIVEN_end_asked_after_two_beats_WHEN_decided_THEN_the_scene_goes_on_and_after_three_it_ends()
      {
         DramaturgeScenePolicy.Decide(End, Facts(2)).End.Should().BeFalse();
         DramaturgeScenePolicy.Decide(End, Facts(3)).End.Should().BeTrue();
      }

      // STAKE: an unreadable reply changes nothing (no event out of nowhere, no ending).
      [Test]
      public void GIVEN_a_reply_with_no_move_line_WHEN_read_and_decided_THEN_nothing_changes()
      {
         DramaturgeReading r = DramaturgeDirector.Parse("I think the scene is fine.");

         r.HadMove.Should().BeFalse();
         DramaturgeDecision d = DramaturgeScenePolicy.Decide(r, Facts(5));
         d.EventNumber.Should().Be(0);
         d.End.Should().BeFalse();
      }

      // STAKE: the four lines read whatever the case or decoration, so a chosen event is not lost to formatting.
      [Test]
      public void GIVEN_the_four_lines_WHEN_read_THEN_progress_and_the_move_are_kept()
      {
         DramaturgeReading r = DramaturgeDirector.Parse("Progress: nothing\n- repeating: Alympia: deflects | none\nuntapped: the debt\n**Move**: Event 2.");
         DramaturgeReading e = DramaturgeDirector.Parse("progress: she confessed\nrepeating: none\nuntapped: none\nmove: end");

         r.Progress.Should().Be("nothing");
         r.Move.Should().Be(DramaturgeMove.Event);
         r.EventNumber.Should().Be(2);
         e.Move.Should().Be(DramaturgeMove.End);
      }

      // STAKE: Gabriel's chat with Rancid Thylea (28/11/2026): the dramaturge kept the template's brackets ("progress: <nothing>"),
      // and the log and the next reading carried them.
      [Test]
      public void GIVEN_fields_wrapped_in_the_templates_brackets_WHEN_read_THEN_the_brackets_are_dropped()
      {
         DramaturgeReading r = DramaturgeDirector.Parse("progress: <nothing>\nrepeating: <Thylea: probing the story>\nuntapped: <her trade>\nmove: <event 1>");

         r.Progress.Should().Be("nothing");
         r.Repeating.Should().Be("Thylea: probing the story");
         r.Move.Should().Be(DramaturgeMove.Event);
         r.EventNumber.Should().Be(1);
      }

      // STAKE: an event is brought once; the dramaturge is shown only those still waiting, under their own numbers.
      [Test]
      public void GIVEN_three_events_WHEN_one_is_taken_THEN_it_is_gone_from_the_waiting_list_and_cannot_be_taken_again()
      {
         var events = new DramaturgeEvents();
         events.Reset(new[] {"A rider comes in.", " ", "A servant drops a tray.", "Bells ring."});

         events.Count.Should().Be(3);
         events.Take(2).Should().Be("A servant drops a tray.");
         events.Take(2).Should().BeNull();
         events.Waiting().Select(w => w.Key).Should().Equal(1, 3);
         events.Used.Should().Equal(2);
      }

      // STAKE: with the composed opening off there is nothing true to bring: the dramaturge is told none is left.
      [Test]
      public void GIVEN_no_events_WHEN_the_dramaturge_prompt_is_written_THEN_it_says_none_left_and_carries_the_inner_life()
      {
         string user = DramaturgeDirector.User(new DramaturgeSheet {NpcName = "Alympia", PlayerName = "Arwa"}, new DramaturgeInnerState {Feeling = "wary", Aim = "keep the debt hidden"},
                                               "Arwa: Where were you?", "", new KeyValuePair<int, string>[0], 3);

         user.Should().Contain("(none left)");
         user.Should().Contain("feels wary; wants keep the debt hidden");
         user.Should().Contain("This is the end of beat 3.");
      }

      // STAKE: the reply after an event takes it as true; the reply after "end" may close the scene but is never ordered to.
      [Test]
      public void GIVEN_an_event_or_the_end_WHEN_the_note_is_written_THEN_the_event_is_a_fact_and_the_end_a_cue()
      {
         DramaturgeDirector.EventNote("A rider comes in.").Should().StartWith("This has just happened in the scene, and it is true: A rider comes in.");
         DramaturgeDirector.EndNote.Should().Contain("You may bring it to a close in character");
         DramaturgeDirector.EndNote.Should().NotContain("end_conversation");
      }

      // STAKE: a hung or failed dramaturge leaves the scene as it is.
      [Test]
      public void GIVEN_a_dramaturge_that_fails_WHEN_the_scene_is_read_THEN_there_is_no_reading()
      {
         var failing = new Canned(_ => throw new InvalidOperationException("down"));

         DramaturgeReading r = DramaturgeTurnComposer.ReadSceneAsync(failing, new DramaturgeSheet {NpcName = "Alympia"}, null, "", "", null, 3,
                                                                      TimeSpan.FromSeconds(2), CancellationToken.None).GetAwaiter().GetResult();

         r.Should().BeNull();
      }

      // ---- the composer's events (Stage 3 extended) ----

      private static readonly ComposedOpeningSheet Plain = new() {NpcName = "Alympia", PlayerName = "Arwa", Place = "the hall of Poros"};

      private static readonly ComposedOpeningSheet WithEvents = new() {NpcName = "Alympia", PlayerName = "Arwa", Place = "the hall of Poros", ProposeEvents = true};

      // STAKE: the rule for the night's build: outside the Dramaturge mode the composer's prompt is the stage 3 one, byte for byte.
      [Test]
      public void GIVEN_events_not_asked_WHEN_the_composer_prompt_is_written_THEN_it_asks_for_no_event()
      {
         string plain = ComposedOpeningPrompt.ComposerSystem(Plain);

         plain.Should().Contain("Answer exactly these four labelled lines");
         plain.Should().NotContain("event:");
         ComposedOpeningPrompt.ComposerSystem(WithEvents).Should().Contain("\nevent: <");
      }

      // STAKE: an event line must never glue onto the tension, which rides hidden in every turn's prompt.
      [Test]
      public void GIVEN_a_reply_with_event_lines_WHEN_parsed_THEN_the_tension_stays_clean_and_the_events_are_listed()
      {
         ComposedOpeningReply r = ComposedOpeningPrompt.Parse("doing: reads\nsetting: Smoke.\nconcern: The wounded.\ntension: The spoils.\nevent: *A rider comes in*\nwith dust on his cloak.\nevent 2: A servant drops a tray.\nevent: none");

         r.Tension.Should().Be("The spoils.");
         r.Events.Should().Equal("A rider comes in with dust on his cloak.", "A servant drops a tray.");
      }

      // STAKE: only an event the check plainly passed is kept; "ok" for one does not pass another left unanswered.
      [Test]
      public void GIVEN_event_verdicts_WHEN_read_THEN_only_the_plain_oks_are_kept()
      {
         ComposedOpeningPrompt.ReadEventVerdicts("1: ok\n2: invents a raid on Varcheg\n4: ok", 3).Should().Equal(true, false, false);
         ComposedOpeningPrompt.ReadEventVerdicts("event 1: ok, nothing invented here\n2. OK", 2).Should().Equal(true, true);
         ComposedOpeningPrompt.ReadEventVerdicts("1: ok but it invents a fief", 1).Should().Equal(false);
      }

      // STAKE: one inventing event must not sink the opening, and only the passed events reach the chat.
      [Test]
      public void GIVEN_one_event_that_invents_WHEN_composed_THEN_the_opening_is_delivered_with_the_others()
      {
         var composer = new Canned(_ => "doing: reads\nsetting: Smoke.\nconcern: c\ntension: t\nevent: A rider comes in.\nevent: Raiders burn the mill.\nevent: Bells ring.");
         var checker = new Canned(r => r.Subject == "composed opening events check" ? "1: ok\n2: invents a raid\n3: ok" : "ok");

         ComposedOpeningResult result = ComposedOpeningComposer.ComposeAsync(WithEvents, composer, checker, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), CancellationToken.None)
                                                               .GetAwaiter().GetResult();

         result.Delivered.Should().BeTrue();
         result.Reply.Events.Should().Equal("A rider comes in.", "Bells ring.");
         checker.Seen.Should().HaveCount(2);
      }

      // STAKE: outside the Dramaturge mode no events check is paid and none is kept, even if the model wrote some.
      [Test]
      public void GIVEN_events_not_asked_WHEN_the_model_writes_some_anyway_THEN_none_is_checked_or_kept()
      {
         var composer = new Canned(_ => "doing: reads\nsetting: Smoke.\nconcern: c\ntension: t\nevent: A rider comes in.");
         var checker = new Canned(_ => "ok");

         ComposedOpeningResult result = ComposedOpeningComposer.ComposeAsync(Plain, composer, checker, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), CancellationToken.None)
                                                               .GetAwaiter().GetResult();

         result.Delivered.Should().BeTrue();
         result.Reply.Events.Should().BeEmpty();
         checker.Seen.Should().ContainSingle();
      }
   }
}
