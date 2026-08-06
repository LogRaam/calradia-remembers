// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 companion hardening. AwaitingReunionPolicy sizes the timeout CompanionMissionBehavior.Advance reads in
// its AwaitingReunion phase. Before this, that phase resolved ONLY when the companion was free AND back in the
// player's party; a companion who was ransomed, escaped to a wanderer state, or whose errand party dissolved
// never re-entered the party on their own and stayed IsAway == true FOREVER, blocking re-dispatch, expel, and
// their journal, with no notice. These tests pin the timeout boundary that force-closes the stranded errand.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AwaitingReunionPolicyTests
   {
      // A fresh capture must NOT be force-closed: the normal escape/ransom/rescue path needs room to resolve it
      // first, or every captured companion would be yanked home the moment they are taken.
      [Test]
      public void GIVEN_a_companion_just_entered_awaiting_reunion_WHEN_asked_to_force_close_THEN_no()
      {
         AwaitingReunionPolicy.ShouldForceClose(0).Should().BeFalse();
      }

      // One day short of the timeout still waits: the boundary must not fire early and rob a companion who is
      // about to be ransomed on the very next day.
      [Test]
      public void GIVEN_a_wait_one_day_short_of_the_timeout_WHEN_asked_to_force_close_THEN_no()
      {
         AwaitingReunionPolicy.ShouldForceClose(AwaitingReunionPolicy.TimeoutDays - 1).Should().BeFalse();
      }

      // The core anti-strand fix: at exactly the timeout the errand must close, clearing the away-status so the
      // companion can be re-dispatched / expelled again. Inclusive boundary, so a stuck errand never lingers past it.
      [Test]
      public void GIVEN_a_wait_of_exactly_the_timeout_WHEN_asked_to_force_close_THEN_yes()
      {
         AwaitingReunionPolicy.ShouldForceClose(AwaitingReunionPolicy.TimeoutDays).Should().BeTrue();
      }

      // Well past the timeout (e.g. a save loaded with a long-stranded companion) force-closes too, so an old
      // stuck errand is cleaned up rather than persisting for the rest of the campaign.
      [Test]
      public void GIVEN_a_wait_well_past_the_timeout_WHEN_asked_to_force_close_THEN_yes()
      {
         AwaitingReunionPolicy.ShouldForceClose(AwaitingReunionPolicy.TimeoutDays + 100).Should().BeTrue();
      }
   }
}
