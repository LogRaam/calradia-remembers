// Code written by Gabriel Mailhot, 17/11/2026.
// What a character hears of a fed event's full text (EventDetailRecall). The Bellum Civile / AI Rebel Relay request
// (17/11/2026) asked for "full record -> relevance filtering -> compact summary -> full details when genuinely needed", and
// "not ... every full-length event into every conversation". The host hands only events the character already knows; this
// decides which passages of their full texts reach the prompt. If it is wrong, either a 20,000-character digest swells every
// prompt, or a pointed question about a known event gets an invented answer while the true one sits unread.

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
   public sealed class EventDetailRecallTests
   {
      private const string Digest =
         "The council of Vlandia met at Pravend in the spring.\n" +
         "Count Aldric demanded the march of Ocs Hall for his son, and was refused.\n" +
         "The harvest in the south failed, and the crown bought grain from the Empire at a ruinous price.\n" +
         "Lady Ira left the court in anger and took her riders home to Sargot.";

      private static EventDetailSource Vlandia(string hedge = "") => new() {Summary = "A digest of Vlandia's spring.", Details = Digest, Hedge = hedge};

      // STAKE: asked about the grain, the character must answer from what the digest says, not invent.
      [Test]
      public void GIVEN_a_question_on_one_matter_WHEN_recalled_THEN_the_passage_on_it_comes_and_not_the_others()
      {
         IReadOnlyList<EventDetailPassage> found = EventDetailRecall.Find(new EventDetailRecallFacts {
            Events = new[] {Vlandia()}, Query = "Why is the grain so dear this year? Was it the harvest?"
         });

         found.Should().HaveCount(1);
         found[0].Passages.Should().ContainSingle().Which.Should().Contain("bought grain from the Empire");
      }

      // STAKE: the request was explicit: no full-length event in every conversation. Talk on another matter brings none.
      [Test]
      public void GIVEN_talk_on_another_matter_WHEN_recalled_THEN_nothing_comes()
      {
         EventDetailRecall.Find(new EventDetailRecallFacts {Events = new[] {Vlandia()}, Query = "Shall we ride to the river and fish?"})
                          .Should().BeEmpty();
      }

      // STAKE: a long digest must never swell the prompt past its budget, however many passages match.
      [Test]
      public void GIVEN_many_matching_passages_WHEN_recalled_THEN_the_budget_holds()
      {
         string longDigest = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"Report {i}: the council argued over grain and the harvest again."));

         IReadOnlyList<EventDetailPassage> found = EventDetailRecall.Find(new EventDetailRecallFacts {
            Events = new[] {new EventDetailSource {Summary = "Many reports.", Details = longDigest}}, Query = "the grain and the harvest", BudgetChars = 500
         });

         found.SelectMany(p => p.Passages).Sum(p => p.Length).Should().BeLessThanOrEqualTo(500);
         found.SelectMany(p => p.Passages).Should().NotBeEmpty();
      }

      // STAKE: two passages of one text are told in the text's order, so the story does not run backwards.
      [Test]
      public void GIVEN_two_passages_of_one_text_WHEN_recalled_THEN_they_keep_the_texts_order()
      {
         IReadOnlyList<EventDetailPassage> found = EventDetailRecall.Find(new EventDetailRecallFacts {
            Events = new[] {Vlandia()}, Query = "What of Aldric and of Lady Ira?"
         });

         found[0].Passages.Should().HaveCount(2);
         found[0].Passages[0].Should().Contain("Aldric");
         found[0].Passages[1].Should().Contain("Ira");
      }

      // STAKE: something heard secondhand stays hedged when its detail is told, or the character speaks of it as certain.
      [Test]
      public void GIVEN_an_event_heard_secondhand_WHEN_rendered_THEN_the_hedge_leads_the_line()
      {
         IReadOnlyList<EventDetailPassage> found = EventDetailRecall.Find(new EventDetailRecallFacts {
            Events = new[] {Vlandia("(heard secondhand) ")}, Query = "Tell me of Lady Ira"
         });

         EventDetailRecall.Render(found).Should().StartWith("- (heard secondhand) Of A digest of Vlandia's spring, word has brought you more:");
      }

      // STAKE: one enormous paragraph is still cut into passages, so a question gets the part that answers it, not all of it.
      [Test]
      public void GIVEN_one_huge_paragraph_WHEN_recalled_THEN_only_a_passage_of_it_comes()
      {
         string wall = string.Join(" ", Enumerable.Range(0, 80).Select(i => i == 40 ? "The bridge at Varcheg was burned by night." : $"Nothing of note happened on day {i}."));

         IReadOnlyList<EventDetailPassage> found = EventDetailRecall.Find(new EventDetailRecallFacts {
            Events = new[] {new EventDetailSource {Summary = "A chronicle.", Details = wall}}, Query = "Who burned the bridge at Varcheg?"
         });

         string told = string.Join(" ", found.SelectMany(p => p.Passages));
         told.Should().Contain("bridge at Varcheg was burned");
         told.Length.Should().BeLessThanOrEqualTo(EventDetailRecall.PassageChars);
      }
   }
}
