// Code written by Gabriel Mailhot, 03/07/2026.

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
      [Test]
      public void GIVEN_a_contract_with_an_eligibility_callback_WHEN_invoking_it_with_facts_THEN_the_callback_runs_with_those_facts()
      {
         var facts = new VerbFacts {NpcId = "hero_1", RelationToPlayer = 25, IsLord = true};
         VerbFacts received = null;

         var contract = new ExternalVerbContract {
            Name = "external_verb",
            IsEligible = f =>
            {
               received = f;

               return f.RelationToPlayer >= 20;
            }
         };

         bool eligible = contract.IsEligible(facts);

         eligible.Should().BeTrue();
         received.Should().BeSameAs(facts);
      }

      [Test]
      public void GIVEN_a_contract_with_a_teaching_callback_WHEN_invoking_it_THEN_it_returns_that_callbacks_text()
      {
         var contract = new ExternalVerbContract {
            Name = "external_verb",
            TeachingText = f => $"You could do something for {f.NpcId}."
         };

         string text = contract.TeachingText(new VerbFacts {NpcId = "hero_2"});

         text.Should().Be("You could do something for hero_2.");
      }

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

         ActionResult result = contract.Execute(request);

         result.Success.Should().BeTrue();
         result.Message.Should().Be("external_verb executed for hero_3.");
      }

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
