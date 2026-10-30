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

      // STAKE: a companion close to leaving, or warning of a plot, is heard before one with counsel or a kindness to share.
      [Test]
      public void GIVEN_a_companions_reason_WHEN_ranked_THEN_leaving_and_plots_press_and_the_rest_can_wait()
      {
         InitiativeRanks.RankOf(NpcMemoryService.Core.Models.CompanionAudienceReason.Grievance).Should().Be(InitiativeRank.Pressing);
         InitiativeRanks.RankOf(NpcMemoryService.Core.Models.CompanionAudienceReason.Retirement).Should().Be(InitiativeRank.Pressing);
         InitiativeRanks.RankOf(NpcMemoryService.Core.Models.CompanionAudienceReason.SchemeWarning).Should().Be(InitiativeRank.Pressing);
         InitiativeRanks.RankOf(NpcMemoryService.Core.Models.CompanionAudienceReason.SecretAffection).Should().Be(InitiativeRank.Ordinary);
         InitiativeRanks.RankOf(NpcMemoryService.Core.Models.CompanionAudienceReason.Gratitude).Should().Be(InitiativeRank.Ordinary);
      }

      // STAKE: a lord's own aim is weighed with the aims, so the player's "Lords' Aims" weight reaches it.
      [Test]
      public void GIVEN_a_lords_aim_WHEN_its_family_is_read_THEN_it_is_the_aims_not_the_lords()
      {
         InitiativeRanks.FamilyOf(InterceptionTrigger.StandingAim).Should().Be(InitiativeFamily.Aims);
         InitiativeRanks.FamilyOf(InterceptionTrigger.Grudge).Should().Be(InitiativeFamily.Lords);
      }

      // STAKE: Gabriel's unhappy wife (28/10/2026): what the household writes is the household's, so its weight and its
      // fair turn in the month reach her; a lord's news stays with the letters.
      [Test]
      public void GIVEN_letters_from_the_household_and_from_a_lord_WHEN_sorted_THEN_the_household_has_its_own()
      {
         InitiativeRanks.FamilyOf(NpcMemoryService.Core.Models.LetterReason.SpouseCorrespondence).Should().Be(InitiativeFamily.Household);
         InitiativeRanks.FamilyOf(NpcMemoryService.Core.Models.LetterReason.SpouseDivorceDemand).Should().Be(InitiativeFamily.Household);
         InitiativeRanks.FamilyOf(NpcMemoryService.Core.Models.LetterReason.ChildSupportRequest).Should().Be(InitiativeFamily.Household);
         InitiativeRanks.FamilyOf(NpcMemoryService.Core.Models.LetterReason.RealmTidings).Should().Be(InitiativeFamily.Letters);
      }
   }
}
