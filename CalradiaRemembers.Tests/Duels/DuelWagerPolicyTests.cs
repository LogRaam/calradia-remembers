// Code written by Gabriel Mailhot, 18/11/2026.

// DuelWagerPolicy decides what gold rides on a duel or a brawl. Aporo (Discord, 18/11/2026) bet 30,000 on a duel and the
// game held no wager at all, so the bet was words. If the policy is wrong, a fight pays a sum one side never had, a
// hallucinated figure empties a purse past the one-transfer ceiling, or a character is told it may stake coin it lacks.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelWagerPolicyTests
   {
      // STAKE: a model writes "30,000 denars" or "none"; misread, the wager is a different sum than the one agreed.
      [Test]
      public void GIVEN_a_wager_field_as_a_model_writes_it_WHEN_read_THEN_only_its_digits_count()
      {
         DuelWagerPolicy.Read("30,000 denars").Should().Be(30000);
         DuelWagerPolicy.Read("500").Should().Be(500);
         DuelWagerPolicy.Read("none").Should().Be(0);
         DuelWagerPolicy.Read(null).Should().Be(0);
      }

      // STAKE: the character may only agree to stake coin it has, and never beyond what one transfer may move.
      [Test]
      public void GIVEN_the_characters_purse_WHEN_the_prompt_cap_is_set_THEN_it_is_that_purse_rounded_down_and_held_at_the_ceiling()
      {
         DuelWagerPolicy.PromptCap(2_345).Should().Be(2_300);
         DuelWagerPolicy.PromptCap(99).Should().Be(0);
         DuelWagerPolicy.PromptCap(0).Should().Be(0);
         DuelWagerPolicy.PromptCap(250_000).Should().Be(GoldTransferLimit.MaxSingleTransfer);
      }

      // STAKE: a wager rides only when both can pay it; the player hears first when it is their purse that falls short.
      [Test]
      public void GIVEN_a_named_wager_WHEN_held_against_the_two_purses_THEN_it_rides_only_when_both_cover_it()
      {
         DuelWagerPolicy.Hold(new DuelWagerFacts {Asked = 500, NpcPurse = 600, PlayerPurse = 500}).Should().Be((500, DuelWagerRefusal.None));
         DuelWagerPolicy.Hold(new DuelWagerFacts {Asked = 500, NpcPurse = 100, PlayerPurse = 100}).Should().Be((0, DuelWagerRefusal.PlayerCannotCover));
         DuelWagerPolicy.Hold(new DuelWagerFacts {Asked = 500, NpcPurse = 100, PlayerPurse = 900}).Should().Be((0, DuelWagerRefusal.NpcCannotCover));
         DuelWagerPolicy.Hold(new DuelWagerFacts {Asked = 0, NpcPurse = 100, PlayerPurse = 900}).Should().Be((0, DuelWagerRefusal.None));
      }

      // STAKE: Aporo's 30,000 is above what one transfer may move (no fortune extracted in roleplay); it rides at the ceiling
      // and says so, rather than paying all or nothing.
      [Test]
      public void GIVEN_a_wager_above_the_one_transfer_ceiling_WHEN_held_THEN_it_rides_at_the_ceiling()
      {
         DuelWagerPolicy.Hold(new DuelWagerFacts {Asked = 30_000, NpcPurse = 50_000, PlayerPurse = 40_000})
            .Should().Be((GoldTransferLimit.MaxSingleTransfer, DuelWagerRefusal.HeldAtCeiling));
      }

      // STAKE: a loser whose purse shrank pays what they hold, never a negative purse.
      [Test]
      public void GIVEN_a_loser_purse_WHEN_the_wager_is_paid_THEN_no_more_than_the_purse_moves()
      {
         DuelWagerPolicy.Payable(500, 2_000).Should().Be(500);
         DuelWagerPolicy.Payable(500, 300).Should().Be(300);
         DuelWagerPolicy.Payable(0, 300).Should().Be(0);
      }
   }
}
