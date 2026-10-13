// Code written by Gabriel Mailhot, 13/10/2026.
// What these pin: the "Who is this?" button of an audience invitation never loses the invitation.
//
// Lordfadooboo (Nexus, 13/10/2026): "an ability to open their encyclopedia page from that screen would be helpful".
// The game's popup is modal over every screen, so the button closes it, opens the encyclopedia, and raises the same
// invitation again when the encyclopedia closes. An audience starts its cooldown when it is offered: an invitation that
// does not come back is an audience the player cannot get back for days, which is worse than having no button.

#region

using CalradiaRemembers.Logic.Interception;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Interception
{
   [TestFixture]
   public sealed class EncyclopediaDetourTests
   {
      // While the player reads the page, the invitation stays away (it would sit over the page otherwise).
      [Test]
      public void GIVEN_the_encyclopedia_open_WHEN_a_frame_passes_THEN_the_invitation_waits()
      {
         EncyclopediaDetour.Next(new EncyclopediaDetourFacts {SeenOpen = true, IsOpenNow = true, SecondsWaited = 60})
                           .Should().Be(EncyclopediaDetourStep.Wait);
      }

      // The player closes the page: the invitation is back, to accept or decline.
      [Test]
      public void GIVEN_the_encyclopedia_closed_after_being_read_WHEN_a_frame_passes_THEN_the_invitation_comes_back()
      {
         EncyclopediaDetour.Next(new EncyclopediaDetourFacts {SeenOpen = true, IsOpenNow = false, SecondsWaited = 30})
                           .Should().Be(EncyclopediaDetourStep.RaiseAgain);
      }

      // The page takes a frame or two to appear: that is not the player closing it.
      [Test]
      public void GIVEN_the_page_not_open_yet_WHEN_just_pressed_THEN_the_invitation_waits_for_it()
      {
         EncyclopediaDetour.Next(new EncyclopediaDetourFacts {SeenOpen = false, IsOpenNow = false, SecondsWaited = 0.2f})
                           .Should().Be(EncyclopediaDetourStep.Wait);
      }

      // A page that never opens must not swallow the audience: after a short wait the invitation returns anyway.
      [Test]
      public void GIVEN_a_page_that_never_opens_WHEN_the_wait_runs_out_THEN_the_invitation_comes_back_anyway()
      {
         EncyclopediaDetour.Next(new EncyclopediaDetourFacts {SeenOpen = false, IsOpenNow = false, SecondsWaited = EncyclopediaDetour.SecondsToOpen})
                           .Should().Be(EncyclopediaDetourStep.RaiseAgain);
      }
   }
}
