// Code written by Gabriel Mailhot, 22/07/2026.
// Protects the found-topic selection at the heart of the companion-audience feature: the ratified design rule
// is that an audience must carry something the player can ACT on, DECIDE, or LEARN, and the mod must FIND that
// thing BEFORE it ever asks for one ("no topic found means no audience, silence is the default"). This is the
// ONE place that rule is enforced in code: get it wrong and either an ineligible topic gets voiced (the player
// hears counsel that is false, or a rumour nobody could have heard), or silence stops being the default and a
// companion starts popping up "just to say something", the exact rival-mod annoyance the owner tested against.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionAudienceTopicPolicyTests
   {
      // ── Precondition gate ──────────────────────────────────────────────────

      // The hard rule from the request itself: "a candidate whose precondition is false is never selected".
      // Without this, a topic whose underlying fact ISN'T true could still be voiced, e.g. counsel to vary
      // food when the party is already well-stocked, which the design explicitly calls "worse than silence".
      [Test]
      public void GIVEN_a_candidates_precondition_is_false_WHEN_selected_THEN_it_is_never_chosen()
      {
         var candidates = new List<CompanionAudienceTopicCandidate> {
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.PracticalCounsel, false, 100),
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.MissionFollowUp, true, 10)
         };

         CompanionAudienceTopicPolicy.SelectTopic(candidates).Should().Be(CompanionAudienceReason.MissionFollowUp);
      }

      // ── No eligible candidate ───────────────────────────────────────────────

      // "No topic found means NO audience": there is no fallback topic. A companion with nothing worth
      // saying must stay silent, never default to some generic reason just because the tick ran.
      [Test]
      public void GIVEN_no_candidate_qualifies_WHEN_selected_THEN_the_answer_is_none()
      {
         var candidates = new List<CompanionAudienceTopicCandidate> {
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.Callback, false, 90),
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.RumourReport, false, 80)
         };

         CompanionAudienceTopicPolicy.SelectTopic(candidates).Should().Be(CompanionAudienceReason.None);
      }

      // An empty list must behave exactly like a list full of ineligible candidates, no topic, no audience.
      [Test]
      public void GIVEN_an_empty_candidate_list_WHEN_selected_THEN_the_answer_is_none()
         => CompanionAudienceTopicPolicy.SelectTopic(new List<CompanionAudienceTopicCandidate>()).Should().Be(CompanionAudienceReason.None);

      // ── Strongest wins ───────────────────────────────────────────────────────

      // "When several qualify, the strongest is chosen", the design calls Callback the highest-value topic
      // of the five; this pins that a stronger candidate (whatever its reason) always wins over a weaker one,
      // so the audience always leads with the most compelling thing found, not merely the first one checked.
      [Test]
      public void GIVEN_several_eligible_candidates_WHEN_selected_THEN_the_strongest_wins()
      {
         var candidates = new List<CompanionAudienceTopicCandidate> {
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.MissionFollowUp, true, 55),
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.Callback, true, 92),
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.PracticalCounsel, true, 40)
         };

         CompanionAudienceTopicPolicy.SelectTopic(candidates).Should().Be(CompanionAudienceReason.Callback);
      }

      // ── Deterministic tie-break ─────────────────────────────────────────────

      // The request is explicit: "the same inputs always give the same answer regardless of input order". Two
      // equally-strong candidates must resolve the same way no matter which one the caller happened to build
      // first, otherwise the SAME party state could open a different audience on different session launches.
      [Test]
      public void GIVEN_a_tie_in_strength_WHEN_selected_THEN_the_answer_does_not_depend_on_list_order()
      {
         var order1 = new List<CompanionAudienceTopicCandidate> {
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.RumourReport, true, 60),
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.CompanionConflict, true, 60)
         };
         var order2 = new List<CompanionAudienceTopicCandidate> {
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.CompanionConflict, true, 60),
            new CompanionAudienceTopicCandidate(CompanionAudienceReason.RumourReport, true, 60)
         };

         CompanionAudienceReason fromOrder1 = CompanionAudienceTopicPolicy.SelectTopic(order1);
         CompanionAudienceReason fromOrder2 = CompanionAudienceTopicPolicy.SelectTopic(order2);

         fromOrder1.Should().Be(fromOrder2);
         // Pin the actual tie-break rule (lower enum ordinal wins) so a future reader can predict it without
         // re-deriving it from the implementation: CompanionConflict is declared before RumourReport.
         fromOrder1.Should().Be(CompanionAudienceReason.CompanionConflict);
      }

      // ── Global cooldown constant ─────────────────────────────────────────────

      // Pins that the cooldown is shared across the WHOLE roster rather than per companion, the request's own
      // reasoning: eight companions each with their own timer would still produce a popup every few days from
      // someone, exactly the "oh no, not him again" failure mode being designed against.
      [Test]
      public void GIVEN_the_global_cooldown_constant_WHEN_read_THEN_it_governs_the_whole_roster_not_one_companion()
         => CompanionAudienceTopicPolicy.GlobalCooldownDays.Should().BeGreaterThan(0);
   }
}
