// Code written by Gabriel Mailhot, 03/07/2026.
// ExternalVerbContract is the exact shape a future public Actions.Register(...) API will hand back and
// forth with a third-party mod, wrapped mod-side by ExternalVerbAdapter into the SAME VerbRegistry CR's own
// conversation verbs run through. If Name routing, or any of the three Func slots (IsEligible/TeachingText/
// Execute), misbehaves, or an unset callback throws instead of simply reading as "absent", an external
// mod's verb could silently vanish from what the LLM is taught, or crash the conversation it is plugged
// into. These tests pin the plain-Func plumbing before any engine coupling is added on top of it.

#region

using System.Collections.Generic;
using FluentAssertions;
using NpcMemoryService.Core.Extension;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   /// <summary>
   ///   Exercises the SDK's <see cref="ExternalVerbContract" /> (Extension Surface, Actions volet spike):
   ///   the plain Func-based shape the mod's CalradiaRemembers.Verbs.ExternalVerbAdapter wraps as an
   ///   IConversationVerb. The adapter itself is engine-coupled (it projects a Hero-carrying VerbContext)
   ///   and cannot be unit-tested here; these tests prove the callback plumbing it delegates through works
   ///   as an external mod would rely on it to.
   /// </summary>
   [TestFixture]
   public sealed class ExternalVerbContractTests
   {
      // Proves the exact VerbFacts instance CR projects reaches the external mod's callback unchanged
      // (BeSameAs, not just equal): if the host ever cloned or partially copied facts before calling out,
      // a verb reading extra data off that instance would silently see stale or missing information.
      [Test]
      public void GIVEN_a_contract_with_an_eligibility_callback_WHEN_invoking_it_with_facts_THEN_the_callback_runs_with_those_facts()
      {
         var facts = new VerbFacts {NpcId = "hero_1", RelationToPlayer = 25, IsLord = true};
         VerbFacts? received = null;

         var contract = new ExternalVerbContract {
            Name = "external_verb",
            IsEligible = f =>
            {
               received = f;

               return f.RelationToPlayer >= 20;
            }
         };

         bool eligible = contract.IsEligible!(facts);

         eligible.Should().BeTrue();
         received.Should().BeSameAs(facts);
      }

      // The teaching text an external mod's own callback returns is what VerbRegistryCore composes straight
      // into the LLM's system prompt: it must pass through unaltered, or the model is taught something the
      // external mod never actually wrote.
      [Test]
      public void GIVEN_a_contract_with_a_teaching_callback_WHEN_invoking_it_THEN_it_returns_that_callbacks_text()
      {
         var contract = new ExternalVerbContract {
            Name = "external_verb",
            TeachingText = f => $"You could do something for {f.NpcId}."
         };

         string text = contract.TeachingText!(new VerbFacts {NpcId = "hero_2"});

         text.Should().Be("You could do something for hero_2.");
      }

      // ExternalVerbAdapter.Execute maps this ActionRequest/ActionResult round-trip directly into the host's
      // own chat-line ActionOutcome the player sees: the verb name, the LLM's raw [ACTION] parameters and
      // the facts must reach Execute intact, and its Message/Success must come back exactly as returned.
      [Test]
      public void GIVEN_a_contract_with_an_execute_callback_WHEN_invoking_it_with_a_request_THEN_it_returns_that_callbacks_result()
      {
         var contract = new ExternalVerbContract {
            Name = "external_verb",
            Execute = req => new ActionResult {
               Message = $"{req.VerbName} executed for {req.Facts.NpcId}.",
               Success = true
            }
         };

         var request = new ActionRequest {
            VerbName = "external_verb",
            Parameters = new Dictionary<string, string> {["target"] = "someone"},
            Facts = new VerbFacts {NpcId = "hero_3"}
         };

         ActionResult result = contract.Execute!(request);

         result.Success.Should().BeTrue();
         result.Message.Should().Be("external_verb executed for hero_3.");
      }

      // An external mod is free to implement only some hooks (a teaching-only verb with no side effect, say):
      // ExternalVerbAdapter null-checks each Func before invoking it, so an unset callback must read back as
      // plain null, never throw, or a partially-implemented third-party verb would crash the registry the
      // moment CR tried to use it.
      [Test]
      public void GIVEN_a_contract_with_no_callbacks_set_WHEN_reading_them_THEN_they_are_null_rather_than_throwing()
      {
         var contract = new ExternalVerbContract {Name = "bare_verb"};

         contract.IsEligible.Should().BeNull();
         contract.TeachingText.Should().BeNull();
         contract.Execute.Should().BeNull();
      }
   }
}
