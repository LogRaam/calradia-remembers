// Code written by Gabriel Mailhot, 03/10/2026.
// EnjoyingHydra (Nexus, 03/10/2026, 2.6.11 with Bellum Civile and AOC): "Whenever I try to confirm to talking to [a
// companion] ... It closes the notification but the game just continues running, no conversation ... Eventually had
// the Companion leave because he took me being unable to talk to him about the problems as grievances." An audience
// was counted as granted the moment the player said yes, before any window opened: the refusal count was reset, the
// one-shot topics spent, a scheme warning heeded, though nothing was ever said. Now what a granted audience changes is
// promised at the yes and kept only when the conversation is really on screen. If this rule is wrong, a companion's
// unheard word is spent as if heard, or a word that was heard is never counted.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AudiencePromiseLedgerTests
   {
      // The conversation opened: what the yes promised happens, once.
      [Test]
      public void GIVEN_an_audience_granted_WHEN_the_conversation_opens_THEN_what_it_promised_happens_once()
      {
         var ledger = new AudiencePromiseLedger();
         var spent = 0;
         ledger.Promise("comp_ira", () => spent++);

         ledger.Keep("comp_ira").Should().Be(1);
         ledger.Keep("comp_ira").Should().Be(0);
         spent.Should().Be(1);
      }

      // EnjoyingHydra's case: the window never opened; nothing is spent, so the companion will ask again.
      [Test]
      public void GIVEN_an_audience_granted_WHEN_it_never_opens_THEN_nothing_it_promised_happens()
      {
         var ledger = new AudiencePromiseLedger();
         var spent = 0;
         ledger.Promise("comp_ira", () => spent++);

         ledger.Break("comp_ira").Should().Be(1);
         ledger.Keep("comp_ira").Should().Be(0);
         spent.Should().Be(0);
      }

      // Opening a conversation with someone else keeps nobody else's promise.
      [Test]
      public void GIVEN_one_companions_audience_pending_WHEN_another_conversation_opens_THEN_it_stays_pending()
      {
         var ledger = new AudiencePromiseLedger();
         var spent = 0;
         ledger.Promise("comp_ira", () => spent++);

         ledger.Keep("lord_garios");

         ledger.IsPending("comp_ira").Should().BeTrue();
         spent.Should().Be(0);
      }

      // A grant both resets the grievance count and spends a topic: both happen together when the word is heard.
      [Test]
      public void GIVEN_two_promises_for_one_audience_WHEN_it_opens_THEN_both_are_kept()
      {
         var ledger = new AudiencePromiseLedger();
         var spent = 0;
         ledger.Promise("comp_ira", () => spent++);
         ledger.Promise("comp_ira", () => spent++);

         ledger.Keep("comp_ira").Should().Be(2);
         spent.Should().Be(2);
      }

      // One promise that throws must not cost the others.
      [Test]
      public void GIVEN_a_promise_that_fails_WHEN_kept_THEN_the_others_still_happen()
      {
         var ledger = new AudiencePromiseLedger();
         var spent = 0;
         ledger.Promise("comp_ira", () => throw new System.InvalidOperationException("boom"));
         ledger.Promise("comp_ira", () => spent++);

         ledger.Keep("comp_ira");

         spent.Should().Be(1);
      }
   }
}
