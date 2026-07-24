// Code written by Gabriel Mailhot, 22/07/2026.
// Protects the CALLBACK found-topic, flagged in the request as the highest-value topic of the five: the mod
// already stores every NotableEvent but nothing ever pays one off, and this decides which stored event (if
// any) is old and strong enough to resurface unprompted. Get MinDaysAgo wrong and a "callback" reads as the
// mod nagging about last week; get IsSignificant wrong and a throwaway Farewell or FirstMeeting could headline
// an audience where a real Betrayal or Intimacy should have.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionCallbackPolicyTests
   {
      // ── Significance set ─────────────────────────────────────────────────────

      // Mirrors LlmMemoryCompressor's own protected-from-compression set (its header: never drop a
      // Betrayal/Intimacy/Confrontation/Agreement/Captivity/Jealousy), the one place this codebase already
      // judges an event durable. A callback headlined by anything less durable would ring hollow.
      [Test]
      public void GIVEN_a_compressor_protected_event_type_WHEN_asked_THEN_it_is_significant()
      {
         CompanionCallbackPolicy.IsSignificant(NotableEventType.Betrayal).Should().BeTrue();
         CompanionCallbackPolicy.IsSignificant(NotableEventType.Intimacy).Should().BeTrue();
         CompanionCallbackPolicy.IsSignificant(NotableEventType.Confrontation).Should().BeTrue();
         CompanionCallbackPolicy.IsSignificant(NotableEventType.Agreement).Should().BeTrue();
         CompanionCallbackPolicy.IsSignificant(NotableEventType.Captivity).Should().BeTrue();
         CompanionCallbackPolicy.IsSignificant(NotableEventType.Jealousy).Should().BeTrue();
      }

      // A shared feat pulled off together is exactly the kind of thing worth calling back to, even though the
      // compressor itself does not protect it from being thinned out over time.
      [Test]
      public void GIVEN_a_collaboration_event_WHEN_asked_THEN_it_is_also_significant()
         => CompanionCallbackPolicy.IsSignificant(NotableEventType.Collaboration).Should().BeTrue();

      // A first meeting is an introduction, not a confidence to resurface; Flirt is not even compressor-
      // protected. Neither should ever headline an unprompted callback.
      [Test]
      public void GIVEN_a_first_meeting_or_flirt_event_WHEN_asked_THEN_it_is_not_significant()
      {
         CompanionCallbackPolicy.IsSignificant(NotableEventType.FirstMeeting).Should().BeFalse();
         CompanionCallbackPolicy.IsSignificant(NotableEventType.Flirt).Should().BeFalse();
      }

      // ── Age floor ────────────────────────────────────────────────────────────

      // Below MinDaysAgo an event is not "old" yet: resurfacing it unprompted would read as the mod nagging
      // about something from last week rather than a genuine memory finally coming back.
      [Test]
      public void GIVEN_a_significant_event_that_is_too_recent_WHEN_scored_THEN_it_does_not_qualify()
         => CompanionCallbackPolicy.Strength(NotableEventType.Betrayal, CompanionCallbackPolicy.MinDaysAgo - 1).Should().Be(0);

      [Test]
      public void GIVEN_a_significant_event_at_the_age_floor_WHEN_scored_THEN_it_qualifies()
         => CompanionCallbackPolicy.Strength(NotableEventType.Betrayal, CompanionCallbackPolicy.MinDaysAgo).Should().BeGreaterThan(0);

      // ── Insignificant events never qualify, whatever their age ──────────────

      // Age alone must never make an ordinary event callback-worthy, only a significant one may resurface,
      // however ancient. Otherwise every stale FirstMeeting in the log would eventually become "callback bait".
      [Test]
      public void GIVEN_an_insignificant_event_however_old_WHEN_scored_THEN_it_never_qualifies()
         => CompanionCallbackPolicy.Strength(NotableEventType.FirstMeeting, 10000).Should().Be(0);

      // ── Prefer old and significant over recent ──────────────────────────────

      // The design rule in plain words: "Prefer old and significant over recent." Two equally significant
      // events must rank the older one higher, so the audience always leads with the OLDEST strong memory
      // available, not merely the latest one that happens to qualify.
      [Test]
      public void GIVEN_two_significant_events_of_different_ages_WHEN_scored_THEN_the_older_one_scores_higher()
      {
         int older = CompanionCallbackPolicy.Strength(NotableEventType.Betrayal, 150);
         int newer = CompanionCallbackPolicy.Strength(NotableEventType.Betrayal, 30);

         older.Should().BeGreaterThan(newer);
      }

      // ── Age bonus plateaus ───────────────────────────────────────────────────

      // An event from years ago must not score meaningfully higher than one already well past the ratified
      // "old enough" mark, otherwise a single ancient event would dominate every strength comparison forever,
      // and the callback topic could never rotate through a companion's other old memories.
      [Test]
      public void GIVEN_an_event_far_beyond_the_age_bonus_ceiling_WHEN_scored_THEN_it_scores_the_same_as_at_the_ceiling()
      {
         int atCeiling = CompanionCallbackPolicy.Strength(NotableEventType.Betrayal, 200);
         int wayBeyond = CompanionCallbackPolicy.Strength(NotableEventType.Betrayal, 2000);

         wayBeyond.Should().Be(atCeiling);
      }

      // ── Qualifies mirrors Strength ───────────────────────────────────────────

      [Test]
      public void GIVEN_a_qualifying_event_WHEN_asked_via_qualifies_THEN_it_agrees_with_strength()
      {
         CompanionCallbackPolicy.Qualifies(NotableEventType.Confrontation, 60).Should().BeTrue();
         CompanionCallbackPolicy.Qualifies(NotableEventType.Flirt, 60).Should().BeFalse();
      }
   }
}
