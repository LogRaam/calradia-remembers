// Code written by Gabriel Mailhot, 15/11/2026.
// What ExecutionerlessDeathPolicy decides: whether Bannerlord 1.5.7's blood-feud bookkeeping may count a death as an
// execution. Gabriel's play test of 15/11/2026 crashed the game in ExecutionCampaignBehavior.OnHeroKilled: a hero with an
// Executed death mark and no executioner was killed by the daily tick, and the bookkeeping read the missing killer's clan.
// If this breaks, that hero ends the game again; if it narrows, a real execution no longer starts the feud the game
// promises.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ExecutionerlessDeathPolicyTests
   {
      // THE REPORT: an execution with nobody who executed crashed the game on the next daily tick.
      [Test]
      public void GIVEN_an_execution_with_no_executioner_WHEN_the_feud_ledger_would_count_it_THEN_it_is_not_counted()
      {
         ExecutionerlessDeathPolicy.KeptAsExecution(true, false).Should().BeFalse();
      }

      // STAKE: a lord who has a prisoner executed still answers for it, in the feud 1.5.7 starts.
      [Test]
      public void GIVEN_an_execution_by_someone_WHEN_the_feud_ledger_counts_it_THEN_it_counts_as_an_execution()
      {
         ExecutionerlessDeathPolicy.KeptAsExecution(true, true).Should().BeTrue();
      }

      // STAKE: a death that was never an execution (old age, battle) is never made one.
      [Test]
      public void GIVEN_a_death_that_is_no_execution_WHEN_the_feud_ledger_looks_THEN_it_is_never_an_execution()
      {
         ExecutionerlessDeathPolicy.KeptAsExecution(false, true).Should().BeFalse();
      }
   }
}
