// Code written by Gabriel Mailhot, 12/08/2026.
// Pins the wording and the diagnostic block for an [ACTION] the bridge refused because its type is not a real
// mod verb. This is the feedback funnel: the player sees an immersive, deed-scoped line (never "nothing
// happened", which contradicted a triumphant scene where a +1 regard and a quest DID land that turn), and the
// invented verb is always logged with its context so the author can add it later. What each channel says is
// what these tests hold in place.
//
// Each test below names the fault it guards.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class UnknownActionNoticeTests
   {
      // The whole reason this class exists: the base line must be scoped to the DEED and must never claim the
      // turn was inert. The player report was a triumphant scene where the old "(That cannot be done here.
      // Nothing happened.)" read as contradicting the story because other real effects landed the same turn.
      [Test]
      public void GIVEN_the_base_message_WHEN_read_THEN_it_is_deed_scoped_and_never_says_nothing_happened()
      {
         UnknownActionNotice.BaseMessage.Should().Be("(That deed finds no purchase in the world here.)");
         UnknownActionNotice.BaseMessage.ToLowerInvariant().Should().NotContain("nothing happened");
      }

      // With the MCM toggle OFF (the default), the player must see only the immersive line: no diagnostic
      // aside, nothing to break the scene for someone who never asked to help report bugs.
      [Test]
      public void GIVEN_reporting_is_off_WHEN_composing_THEN_only_the_base_message_shows()
      {
         UnknownActionNotice.Compose(false).Should().Be(UnknownActionNotice.BaseMessage);
      }

      // With the toggle ON, the base line still shows first (immersion preserved) and the sober report hint is
      // appended. The hint must never name a person in-fiction: it points at the log and the mod page only.
      [Test]
      public void GIVEN_reporting_is_on_WHEN_composing_THEN_the_base_message_carries_a_person_free_report_hint()
      {
         string composed = UnknownActionNotice.Compose(true);

         composed.Should().StartWith(UnknownActionNotice.BaseMessage);
         composed.Should().Contain("logged");
         composed.Should().Contain("mod page");
         composed.Length.Should().BeGreaterThan(UnknownActionNotice.BaseMessage.Length);
      }

      // The funnel's payload: the author must be able to see WHAT the model tried. The block has to carry the
      // invented verb, every parameter it rode with, who attempted it, and the two surrounding lines, so the
      // author can decide whether to support the verb in a future update.
      [Test]
      public void GIVEN_a_full_context_WHEN_formatting_the_log_entry_THEN_it_captures_the_verb_params_npc_and_lines()
      {
         var parameters = new Dictionary<string, string> {
            {"fief", "Pravend"},
            {"recipient", "player"}
         };

         string entry = UnknownActionNotice.FormatLogEntry(new UnknownActionNotice.UnknownActionFacts {ActionType = "grant_fief_to_player", Parameters = parameters, NpcLabel = "Derthert of dey Meroc (Vlandian)", NpcLine = "Then let the deed be done: Pravend is yours.", PlayerLine = "Grant me a fief for my service."});

         entry.Should().Contain("grant_fief_to_player");
         entry.Should().Contain("fief");
         entry.Should().Contain("Pravend");
         entry.Should().Contain("recipient");
         entry.Should().Contain("player");
         entry.Should().Contain("Derthert of dey Meroc (Vlandian)");
         entry.Should().Contain("Then let the deed be done: Pravend is yours.");
         entry.Should().Contain("Grant me a fief for my service.");
      }

      // Robustness: the caller passes null for any field it cannot cleanly reach (rather than plumbing new
      // parameters through many layers), and a parameter may carry a blank value. Blank fields must be omitted
      // and the call must never throw, or a diagnostic aid would itself crash a live turn.
      [Test]
      public void GIVEN_nulls_and_blanks_WHEN_formatting_the_log_entry_THEN_blank_fields_are_omitted_and_it_never_throws()
      {
         var parameters = new Dictionary<string, string> {
            {"amount", "500"},
            {"note", "   "}
         };

         string entry = UnknownActionNotice.FormatLogEntry(new UnknownActionNotice.UnknownActionFacts {ActionType = "bless_marriage", Parameters = parameters, NpcLabel = null, NpcLine = null, PlayerLine = "Bless our union."});

         entry.Should().Contain("bless_marriage");
         entry.Should().Contain("amount");
         entry.Should().Contain("500");
         entry.Should().NotContain("note");
         entry.Should().Contain("Bless our union.");
         entry.Should().NotContain("NPC:");
         entry.Should().NotContain("NPC line:");
      }
   }
}
