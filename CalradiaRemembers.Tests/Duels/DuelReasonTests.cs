// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A: DuelReason is a bare enum, but its two values must exist and stay distinct, since the
// host switches consequence policy (DuelOutcomePolicy alone vs DuelOutcomePolicy plus CourtshipDuelPolicy) on
// which one is carried. A silently merged or renamed value would misroute a courtship duel's consequence onto
// the wrong hero, or an honour duel into moving a romance no one is pursuing.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelReasonTests
   {
      // Both reasons must exist as distinct values, or the host would have nothing to switch on between the
      // generic-only path and the courtship-layered one.
      [Test]
      public void GIVEN_the_two_duel_reasons_WHEN_compared_THEN_they_are_distinct()
      {
         DuelReason.HonourAffront.Should().NotBe(DuelReason.CourtshipRivalry);
      }
   }
}
