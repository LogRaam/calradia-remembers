// Code written by Gabriel Mailhot, 28/10/2026.
// How a lord's reason to seek the player out ranks with the world's initiative. Gabriel (28/10/2026): coherent priorities,
// and the war duty among the four that always pass. If this is wrong, a kindness to acknowledge crowds out a grudge, or
// the liege's summons waits behind a gossip.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Initiative;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Initiative
{
   [TestFixture]
   public sealed class InitiativeRanksTests
   {
      // STAKE: Gabriel's four: the liege's war summons always passes.
      [Test]
      public void GIVEN_the_war_duty_summons_WHEN_ranked_THEN_it_is_imposed()
      {
         InitiativeRanks.RankOf(InterceptionTrigger.WarDutySummons).Should().Be(InitiativeRank.Imposed);
      }

      // STAKE: a grudge, fresh news, a nemesis, a patrol press; a kindness and a lord's own aim can wait.
      [Test]
      public void GIVEN_each_reason_WHEN_ranked_THEN_what_presses_comes_before_what_can_wait()
      {
         InitiativeRanks.RankOf(InterceptionTrigger.Grudge).Should().Be(InitiativeRank.Pressing);
         InitiativeRanks.RankOf(InterceptionTrigger.HeardOfDeed).Should().Be(InitiativeRank.Pressing);
         InitiativeRanks.RankOf(InterceptionTrigger.NemesisBounty).Should().Be(InitiativeRank.Pressing);
         InitiativeRanks.RankOf(InterceptionTrigger.TerritorialPatrol).Should().Be(InitiativeRank.Pressing);
         InitiativeRanks.RankOf(InterceptionTrigger.AppreciationOwed).Should().Be(InitiativeRank.Ordinary);
         InitiativeRanks.RankOf(InterceptionTrigger.StandingAim).Should().Be(InitiativeRank.Ordinary);
      }

      // STAKE: a lord's own aim is weighed with the aims, so the player's "Lords' Aims" weight reaches it.
      [Test]
      public void GIVEN_a_lords_aim_WHEN_its_family_is_read_THEN_it_is_the_aims_not_the_lords()
      {
         InitiativeRanks.FamilyOf(InterceptionTrigger.StandingAim).Should().Be(InitiativeFamily.Aims);
         InitiativeRanks.FamilyOf(InterceptionTrigger.Grudge).Should().Be(InitiativeFamily.Lords);
      }
   }
}
