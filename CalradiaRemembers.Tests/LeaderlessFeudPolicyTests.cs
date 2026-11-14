// Code written by Gabriel Mailhot, 13/11/2026.
// What LeaderlessFeudPolicy decides: Bannerlord 1.5.7's blood feud starts only with a clan that has a leader.
// Gabriel's cr.test all of 13/11/2026 on 1.5.7: executing a nemesis (a bandit clan, no leader) threw inside the engine's
// feud, after the kill. If this breaks, executing a nemesis throws again; if it widens, a real lord's execution no
// longer starts the feud the game now promises.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LeaderlessFeudPolicyTests
   {
      // THE REPORT: a nemesis's band of looters has no head to feud with, and the engine threw on the missing one.
      [Test]
      public void GIVEN_an_executed_hero_whose_clan_has_no_leader_WHEN_the_feud_would_start_THEN_it_does_not()
      {
         LeaderlessFeudPolicy.MayStartFeud(false).Should().BeFalse();
      }

      // STAKE: executing a lord of a real house still starts the blood feud of 1.5.7.
      [Test]
      public void GIVEN_an_executed_hero_whose_clan_has_a_leader_WHEN_the_feud_would_start_THEN_it_starts()
      {
         LeaderlessFeudPolicy.MayStartFeud(true).Should().BeTrue();
      }
   }
}
