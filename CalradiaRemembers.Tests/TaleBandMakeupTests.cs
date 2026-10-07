// Code written by Gabriel Mailhot, 07/10/2026.
// What a tale's band is made of. fkasad (Nexus, 07/10/2026), after the Wolf of the Wood: "A party of the lowest ranked
// bandits available in the game ... 65 looters vs 110 veterans Dawi. I could have auto resolved it at no cost." The
// band had grown with his men (2.7.3) but stayed looters, the weakest troop in the game. Gabriel (07/10/2026) ratified
// a "local" band: the bandits of the region, with a share of seasoned men that follows how seasoned the player's own
// troops are. If this breaks, the climax is a formality again for any veteran company.

#region

using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleBandMakeupTests
   {
      // A company of recruits meets plain bandits: no seasoned men.
      [Test]
      public void GIVEN_a_player_leading_recruits_WHEN_the_band_is_made_THEN_it_is_all_plain_bandits()
      {
         AuthoredBandMakeupPolicy.Split(new AuthoredBandMakeupFacts {Size = 20, PlayerAverageTier = 1.2, UpgradeSteps = 2})
                                 .Should().Equal(20, 0, 0);
      }

      // fkasad's veterans: a good share of the band is seasoned, some of it twice over, and the whole still adds up.
      [Test]
      public void GIVEN_a_player_leading_veterans_WHEN_the_band_is_made_THEN_a_share_is_seasoned_and_the_count_is_kept()
      {
         int[] split = AuthoredBandMakeupPolicy.Split(new AuthoredBandMakeupFacts {Size = 65, PlayerAverageTier = 5, UpgradeSteps = 2});

         split.Sum().Should().Be(65);
         split[1].Should().BeGreaterThan(0);
         split[2].Should().BeGreaterThan(0);
         (split[1] + split[2]).Should().BeLessThanOrEqualTo((int) (65 * AuthoredBandMakeupPolicy.MaxSeasonedShare + 0.5));
      }

      // Bandits whose troop never improves (looters in some setups) stay one kind: nothing to upgrade them into.
      [Test]
      public void GIVEN_bandits_with_no_better_troop_WHEN_the_band_is_made_THEN_every_man_is_that_troop()
      {
         AuthoredBandMakeupPolicy.Split(new AuthoredBandMakeupFacts {Size = 30, PlayerAverageTier = 5, UpgradeSteps = 0})
                                 .Should().Equal(30);
      }

      // "local" is a template the loader accepts, and the wolf's slavers use it.
      [Test]
      public void GIVEN_the_wolf_tale_WHEN_loaded_THEN_its_slavers_are_the_regions_bandits()
      {
         AuthoredQuest q = AuthoredTales.Wolf();

         q.Stages.Select(s => s.Objective?.Spawn).Where(sp => sp != null).Should().NotBeEmpty()
          .And.OnlyContain(sp => sp.Template == AuthoredBandMakeupPolicy.LocalTemplate);
      }
   }
}
