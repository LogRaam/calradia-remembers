// Code written by Gabriel Mailhot, 28/11/2026.
// The Dramaturge mode's scene keeper (Stage 6 of the group-scene plan), ported from the simulator's Inspect and Keep: one
// cheap reading of each reply before it is shown, for a lie (the mod's rule: NPCs never lie), a repeat within this chat, a
// contradiction, or an invention of a fact the game tracks; one rewrite with the reason, read once more, and the rewrite is
// shown. If this is wrong: a cleared line is rewritten because the keeper's reasoning said "problem" before its verdict, a
// slow keeper holds the reply, a failed rewrite blanks a good reply, or the rewrite is never told why.

#region

using System;
using System.Collections.Generic;
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
   public sealed class DramaturgeKeeperTests
   {
      private const string Lie = "detail: She says she was never in Lageta, but her memories put her there last spring.\nfacts: none\nobjects: none\nrevealed: none\nproblem: lie";
      private const string Pass = "detail: none\nfacts: Alympia hands Arwa the ledger\nobjects: the ledger: in Arwa's hands\nrevealed: none\nproblem: none";

      private sealed class Canned : ILlmClient
      {
         private readonly Queue<Func<LlmRequest, CancellationToken, Task<LlmResponse>>> _answers = new();
         public readonly List<LlmRequest> Seen = new();

         public Canned(params string[] contents)
         {
            foreach (string c in contents) _answers.Enqueue((_, _) => Task.FromResult(new LlmResponse {Content = c, IsSuccess = true}));
         }

         public Canned(Func<LlmRequest, CancellationToken, Task<LlmResponse>> answer) => _answers.Enqueue(answer);

         public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
         {
            Seen.Add(request);

            return _answers.Count > 0 ? _answers.Dequeue()(request, ct) : Task.FromResult(new LlmResponse {Content = Pass, IsSuccess = true});
         }
      }

      private static readonly DramaturgeSheet Sheet = new() {NpcName = "Alympia", PlayerName = "Arwa", Knows = "Alympia rode through Lageta last spring."};

      private static DramaturgeKeeperResult Keep(ILlmClient keeper, Func<string, Task<string>> rewrite, DramaturgeRecord record = null, double seconds = 5)
         => DramaturgeTurnComposer.KeepAsync(keeper, Sheet, null, record ?? new DramaturgeRecord(), "Arwa: Were you in Lageta?", "I was never in Lageta.", rewrite,
                                             TimeSpan.FromSeconds(seconds), CancellationToken.None).GetAwaiter().GetResult();

      // STAKE: a review of 27/11/2026: the keeper wrote "problem: lie" first, then cleared the line in its reasoning, and the
      // line was rewritten all the same. The LAST verdict line decides.
      [Test]
      public void GIVEN_a_reply_that_says_problem_in_its_reasoning_WHEN_parsed_THEN_the_last_verdict_line_decides()
      {
         KeeperVerdict v = DramaturgeKeeper.Parse("problem: lie\ndetail: on reflection she may well have seen it\nfacts: none\nobjects: none\nproblem: none");

         v.Problem.Should().Be(KeeperProblem.None);
         v.HadVerdict.Should().BeTrue();
      }

      // STAKE: each fault is named for the rewrite and the log; "none" in a field is no fact.
      [Test]
      public void GIVEN_each_verdict_WHEN_parsed_THEN_problem_detail_facts_and_objects_are_read()
      {
         DramaturgeKeeper.Parse(Lie).Problem.Should().Be(KeeperProblem.Lie);
         DramaturgeKeeper.Parse("detail: x\nproblem: Repeat").Problem.Should().Be(KeeperProblem.Repeat);
         DramaturgeKeeper.Parse("detail: x\nproblem: contradiction").Problem.Should().Be(KeeperProblem.Contradiction);
         DramaturgeKeeper.Parse("detail: x\n**problem**: invention").Problem.Should().Be(KeeperProblem.Invention);

         KeeperVerdict pass = DramaturgeKeeper.Parse(Pass);
         pass.Problem.Should().Be(KeeperProblem.None);
         pass.Detail.Should().BeEmpty();
         pass.Facts.Should().Equal("Alympia hands Arwa the ledger");
         pass.Objects.Should().Be("the ledger: in Arwa's hands");
      }

      // STAKE: Gabriel's chat with Monchug (28/11/2026): a keeper that wrote its detail under "detail:" left the rewrite told
      // nothing and the log read "found a invention: ;". The block up to the next field is the detail.
      [Test]
      public void GIVEN_a_detail_written_on_the_next_lines_WHEN_parsed_THEN_it_is_read_and_logged()
      {
         KeeperVerdict v = DramaturgeKeeper.Parse("detail:\nHe names a Vlandian king's war on the Aserai,\nwhich no fact holds.\nfacts: none\nobjects: none\nproblem: invention");

         v.Problem.Should().Be(KeeperProblem.Invention);
         v.Detail.Should().Be("He names a Vlandian king's war on the Aserai, which no fact holds.");
         DramaturgeKeeper.DetailForLog(new KeeperVerdict()).Should().Be("it gave no detail");
      }

      // STAKE: a keeper whose reply was cut before its verdict must not rewrite a line nobody judged; it passes, and says so.
      [Test]
      public void GIVEN_a_reply_without_a_verdict_WHEN_parsed_THEN_it_passes_marked_as_unjudged()
      {
         KeeperVerdict v = DramaturgeKeeper.Parse("detail: she claims");

         v.Problem.Should().Be(KeeperProblem.None);
         v.HadVerdict.Should().BeFalse();
      }

      // STAKE: the clarified definition (the third review of 27/11/2026: the keeper read the absence of proof as a lie): an
      // unproven claim is not a lie, and private life is never an invention.
      [Test]
      public void GIVEN_the_keeper_instructions_WHEN_read_THEN_an_unproven_claim_is_no_lie_and_private_life_is_free()
      {
         string system = DramaturgeKeeper.System("Alympia");

         system.Should().Contain("An unproven claim is not a lie");
         system.Should().Contain("Private life is free and is NOT an invention");
         system.Should().Contain("decide the problem LAST");
      }

      // STAKE: the mod's rule (NPCs never lie): a lie caught is written once more, told why, and the rewrite is shown.
      [Test]
      public void GIVEN_a_lie_WHEN_kept_THEN_one_rewrite_is_asked_with_the_reason_read_again_and_shown()
      {
         var keeper = new Canned(Lie, Pass);
         string asked = null;
         int rewrites = 0;

         DramaturgeKeeperResult result = Keep(keeper, reason =>
         {
            rewrites++;
            asked = reason;

            return Task.FromResult("I will not speak of Lageta.");
         });

         rewrites.Should().Be(1);
         asked.Should().Contain("You said something you know is false").And.Contain("never in Lageta");
         keeper.Seen.Should().HaveCount(2);
         keeper.Seen[1].Messages[0].Content.Should().Contain("I will not speak of Lageta.");
         result.Text.Should().Be("I will not speak of Lageta.");
         result.Rewritten.Should().BeTrue();
      }

      // STAKE: one rewrite, never a loop: a rewrite the keeper still faults is shown all the same, and the log says so.
      [Test]
      public void GIVEN_a_rewrite_that_still_fails_WHEN_kept_THEN_it_is_shown_and_no_third_draft_is_asked()
      {
         var keeper = new Canned(Lie, Lie);
         int rewrites = 0;

         DramaturgeKeeperResult result = Keep(keeper, _ =>
         {
            rewrites++;

            return Task.FromResult("Lageta? Never.");
         });

         rewrites.Should().Be(1);
         result.Text.Should().Be("Lageta? Never.");
         result.Why.Should().Contain(w => w.Contains("flagged again (lie)"));
      }

      // STAKE: a rewrite that failed (or was a refusal the chat would not show) must not blank the reply the player waits for.
      [Test]
      public void GIVEN_a_rewrite_that_did_not_come_WHEN_kept_THEN_the_first_reply_is_shown()
      {
         DramaturgeKeeperResult result = Keep(new Canned(Lie), _ => Task.FromResult<string>(null));

         result.Text.Should().Be("I was never in Lageta.");
         result.Rewritten.Should().BeFalse();
      }

      // STAKE: latency: a keeper that fails or hangs past its short limit lets the reply through as it came.
      [Test]
      public void GIVEN_a_keeper_that_fails_or_hangs_WHEN_kept_THEN_the_reply_is_shown_as_it_came()
      {
         Keep(new Canned((_, _) => Task.FromResult(new LlmResponse {Content = "", IsSuccess = false, ErrorMessage = "429"})), _ => Task.FromResult("x")).Text.Should().Be("I was never in Lageta.");

         DramaturgeKeeperResult hung = Keep(new Canned((_, _) => new TaskCompletionSource<LlmResponse>().Task), _ => Task.FromResult("x"), seconds: 0.2);
         hung.Text.Should().Be("I was never in Lageta.");
         hung.First.Should().BeNull();
      }

      // STAKE: the keeper keeps the record of the chat: what a passed reply made true is there for the next reading.
      [Test]
      public void GIVEN_a_passed_reply_WHEN_kept_THEN_the_record_holds_what_it_established()
      {
         var record = new DramaturgeRecord();

         Keep(new Canned(Pass), _ => Task.FromResult("x"), record);

         record.Text().Should().Contain("- ledger: in Arwa's hands").And.Contain("- Alympia hands Arwa the ledger");
      }

      // STAKE: the inner-life reader: a reply updates the state; a failure keeps the old one (the turn never waits on it).
      [Test]
      public void GIVEN_the_inner_life_reader_WHEN_it_answers_or_fails_THEN_the_state_is_updated_or_kept()
      {
         var before = new DramaturgeInnerState {Feeling = "calm", Aim = "trade"};

         DramaturgeMindResult ok = DramaturgeTurnComposer.UpdateMindAsync(new Canned("feeling: angry\nlevel: fluctuation\naim: trade"), Sheet, before, "Arwa: you cheat",
                                                                           TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();
         ok.Updated.Should().BeTrue();
         ok.State.Feeling.Should().Be("angry");

         DramaturgeMindResult failed = DramaturgeTurnComposer.UpdateMindAsync(new Canned((_, _) => Task.FromResult(new LlmResponse {Content = "", IsSuccess = false})), Sheet, before, "x",
                                                                               TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();
         failed.Updated.Should().BeFalse();
         failed.State.Should().BeSameAs(before);
      }
   }
}
