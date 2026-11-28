// Code written by Gabriel Mailhot, 28/11/2026.
// The composed intro's two calls (Stage 3 of the group-scene plan; Gabriel, 28/11/2026: the composer "chooses among true
// facts and stages them; it invents nothing the game tracks"). The composer writes, the check reads it against the facts,
// and anything short of a plain "ok" falls back to today's opener. If this is wrong: an invented raid reaches the screen, a
// failed or slow call leaves the player with nothing, or the check never sees what the composer wrote.

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
   public sealed class ComposedOpeningComposerTests
   {
      private const string Composed = "doing: She reads the casualty lists.\nsetting: Smoke hangs in the hall of Poros.\nconcern: You fear for the wounded.\ntension: Arwa took the spoils.";

      private sealed class Canned : ILlmClient
      {
         private readonly Func<LlmRequest, LlmResponse> _answer;
         public readonly List<LlmRequest> Seen = new();

         public Canned(Func<LlmRequest, LlmResponse> answer) => _answer = answer;

         public Canned(string content) : this(_ => new LlmResponse {Content = content, IsSuccess = true}) { }

         public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
         {
            Seen.Add(request);

            return Task.FromResult(_answer(request));
         }
      }

      private static readonly ComposedOpeningSheet Sheet = new() {NpcName = "Alympia", PlayerName = "Arwa", Place = "the hall of Poros", Season = "spring"};

      private static ComposedOpeningResult Run(ILlmClient composer, ILlmClient checker, CancellationToken ct = default)
         => ComposedOpeningComposer.ComposeAsync(Sheet, composer, checker, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), ct).GetAwaiter().GetResult();

      // STAKE: Gabriel's case: a composition that invents nothing opens the chat, its concern and tension kept.
      [Test]
      public void GIVEN_a_composition_the_check_passes_WHEN_composed_THEN_it_is_delivered_and_the_check_read_it()
      {
         var checker = new Canned("ok");

         ComposedOpeningResult result = Run(new Canned(Composed), checker);

         result.Delivered.Should().BeTrue();
         result.Reply.Setting.Should().Be("Smoke hangs in the hall of Poros.");
         result.Reply.Concern.Should().Be("You fear for the wounded.");
         checker.Seen.Should().ContainSingle().Which.Messages[0].Content.Should().Contain("setting: Smoke hangs in the hall of Poros.");
      }

      // STAKE: the simulator's invented raid: caught by the check, the opening falls back and says why.
      [Test]
      public void GIVEN_a_check_that_finds_an_invention_WHEN_composed_THEN_it_falls_back_naming_it()
      {
         ComposedOpeningResult result = Run(new Canned(Composed), new Canned("invents a raid on Varcheg"));

         result.Delivered.Should().BeFalse();
         result.Why.Should().Be("the check found it invents a raid on Varcheg");
      }

      // STAKE: a failed composer never leaves the player without an opening, and the check is not paid for.
      [Test]
      public void GIVEN_a_failed_composer_WHEN_composed_THEN_it_falls_back_without_a_check()
      {
         var checker = new Canned("ok");

         ComposedOpeningResult result = Run(new Canned(_ => new LlmResponse {Content = "", IsSuccess = false, ErrorMessage = "429"}), checker);

         result.Delivered.Should().BeFalse();
         result.Why.Should().Contain("the composer failed");
         checker.Seen.Should().BeEmpty();
      }

      // STAKE: a refusal shown as a scene would be worse than the canned greeting.
      [Test]
      public void GIVEN_a_refused_composition_WHEN_composed_THEN_it_falls_back()
      {
         Run(new Canned("setting: I'm sorry, but I can't help with that request."), new Canned("ok")).Delivered.Should().BeFalse();
      }

      // STAKE: a check that failed is no check: fail closed.
      [Test]
      public void GIVEN_a_failed_check_WHEN_composed_THEN_it_falls_back()
      {
         Run(new Canned(Composed), new Canned(_ => new LlmResponse {Content = "", IsSuccess = false, ErrorMessage = "timeout"})).Delivered.Should().BeFalse();
      }

      // STAKE: the player who spoke first owns the turn: a cut wait is reported as set aside, not as a failure to redo.
      [Test]
      public void GIVEN_the_wait_cut_by_the_caller_WHEN_composed_THEN_it_is_cancelled()
      {
         using var cts = new CancellationTokenSource();
         cts.Cancel();

         Run(new Canned(_ => throw new OperationCanceledException()), new Canned("ok"), cts.Token).Cancelled.Should().BeTrue();
      }

      // STAKE: a composer that hangs past its limit falls back instead of holding the opening.
      [Test]
      public void GIVEN_a_composer_that_times_out_WHEN_composed_THEN_it_falls_back()
      {
         var slow = new Canned(_ => throw new OperationCanceledException());

         ComposedOpeningResult result = Run(slow, new Canned("ok"));

         result.Delivered.Should().BeFalse();
         result.Cancelled.Should().BeFalse();
         result.Why.Should().Be("the composer timed out");
      }
   }
}
