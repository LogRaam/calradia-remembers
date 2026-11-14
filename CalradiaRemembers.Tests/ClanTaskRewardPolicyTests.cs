// Code written by Gabriel Mailhot, 13/11/2026.
// What ClanTaskRewardPolicy decides: a task from the player's own clan pays no coin; anyone else's pays what was promised.
// Aporo (Discord, 13/11/2026): "I don't need that 100 denars for bringing 20 grain when it's my money either way";
// Gabriel: "tout le clan". If this breaks, a wife pays her husband from their shared purse again, or a lord of another
// house stops paying for the work he asked.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ClanTaskRewardPolicyTests
   {
      // THE REPORT: 100 denars from his wife for 20 grain, his own money either way.
      [Test]
      public void GIVEN_a_task_from_the_players_own_clan_WHEN_its_coin_is_set_THEN_it_pays_none()
      {
         ClanTaskRewardPolicy.GoldFor(true, 100).Should().Be(0);
      }

      // STAKE: a lord of another house still pays for the work he asked.
      [Test]
      public void GIVEN_a_task_from_another_clan_WHEN_its_coin_is_set_THEN_it_pays_what_was_promised()
      {
         ClanTaskRewardPolicy.GoldFor(false, 100).Should().Be(100);
      }
   }
}
