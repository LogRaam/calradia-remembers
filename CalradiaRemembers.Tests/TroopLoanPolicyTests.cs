// Code written by Gabriel Mailhot, 18/07/2026.
// Troop-loan pillar, increment 0 (passive). These rules decide when soldiers an NPC lent the player march
// home again, and how many are left to march.
//
// The gap that started it: Mina offered to send men along with the hideout quest she had just given, and
// nothing happened. lend_troops was never eligible (relation >= 20, she was a stranger at 0), so the offer
// was pure fiction. Scoping the fix found the deeper fault: lend_troops moves soldiers ONE WAY with no
// ledger at all, so what the verb calls a loan has always been a permanent gift.
//
// Every boundary below is one of Gabriel's calls (2026-07-18), so each test names which one it pins.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TroopLoanPolicyTests
   {
      // THE CALL THAT DEFINES THE PILLAR: the loan ends when the DEED is done (the hideout falls), not when
      // the player walks back to collect the reward. In quest terms that is IsOutstanding going false, and
      // the men are already marching home while the reward conversation is still days away.
      [Test]
      public void GIVEN_the_deed_is_done_WHEN_deciding_THEN_the_soldiers_go_home()
      {
         TroopLoanPolicy.ShouldReturn(OpenLoan(), questIsOutstanding: false).Should().BeTrue();
      }

      // The other side of it: while the task is still outstanding the men stay. This is what makes the loan
      // useful at all, and it is why the trigger is the quest and not a timer.
      [Test]
      public void GIVEN_the_task_is_still_outstanding_WHEN_deciding_THEN_the_soldiers_stay()
      {
         TroopLoanPolicy.ShouldReturn(OpenLoan(), questIsOutstanding: true).Should().BeFalse();
      }

      // Gabriel's ruling on failure, and the reason it is not optional: if the men only went home on
      // success, FAILING a quest would be the way to keep them permanently. Same flag, so expired,
      // abandoned and cancelled all bring them back, with no penalty attached.
      [Test]
      public void GIVEN_the_task_ended_in_failure_WHEN_deciding_THEN_the_soldiers_still_go_home()
      {
         // A failed quest is no longer outstanding, exactly like a satisfied one.
         TroopLoanPolicy.ShouldReturn(OpenLoan(), questIsOutstanding: false).Should().BeTrue();
      }

      // Idempotency, and it protects the player's own troops: a resolution seen twice (a re-entered
      // settlement, a reloaded save, two subscribers on one event) must not claw back a second batch of
      // soldiers the lender never lent.
      [Test]
      public void GIVEN_the_loan_was_already_returned_WHEN_deciding_THEN_nothing_is_taken_again()
      {
         var loan = new TroopLoan {QuestKey = "cr_quest_mina_1_12", ReturnedOnDay = 20};

         TroopLoanPolicy.ShouldReturn(loan, questIsOutstanding: false).Should().BeFalse();
      }

      // Casualties carry NO consequence (Gabriel: "pas de conséquences même si ils meurent tous"). Whatever
      // survives goes back, the shortfall is simply the dead, and nothing here counts or reports it: the
      // quest's own outcome is the consequence.
      [Test]
      public void GIVEN_some_of_the_lent_men_died_WHEN_returning_THEN_only_the_survivors_march_home()
      {
         IReadOnlyDictionary<string, int> going = TroopLoanPolicy.ReturnableCounts(
            lent: new Dictionary<string, int> {["battanian_clanwarrior"] = 20},
            stillInPlayerParty: new Dictionary<string, int> {["battanian_clanwarrior"] = 12});

         going["battanian_clanwarrior"].Should().Be(12);
      }

      // The extreme of the same call, stated on its own because it is the one a player will actually hit:
      // the whole detachment is wiped out. Nothing goes back, and that is a complete, non-exceptional
      // outcome, not an error to report.
      [Test]
      public void GIVEN_every_lent_man_died_WHEN_returning_THEN_nothing_marches_home_and_that_is_fine()
      {
         TroopLoanPolicy.ReturnableCounts(
                            new Dictionary<string, int> {["battanian_clanwarrior"] = 20},
                            new Dictionary<string, int>())
                        .Should().BeEmpty();
      }

      // The boundary in the other direction, which guards the LENDER: the player recruited nine more of the
      // same troop type during the quest. Only what was lent goes back. Without this the loan would quietly
      // confiscate soldiers the player paid for.
      [Test]
      public void GIVEN_the_player_recruited_more_of_the_same_type_WHEN_returning_THEN_only_the_lent_number_leaves()
      {
         TroopLoanPolicy.ReturnableCounts(
                            new Dictionary<string, int> {["battanian_clanwarrior"] = 20},
                            new Dictionary<string, int> {["battanian_clanwarrior"] = 29})
                        ["battanian_clanwarrior"].Should().Be(20);
      }

      // A mixed detachment must be settled type by type: the spearmen can be wiped out while the archers
      // come home intact, and one shared total would mis-credit both.
      [Test]
      public void GIVEN_a_mixed_detachment_WHEN_returning_THEN_each_type_is_settled_on_its_own()
      {
         IReadOnlyDictionary<string, int> going = TroopLoanPolicy.ReturnableCounts(
            new Dictionary<string, int> {["spearman"] = 10, ["archer"] = 5},
            new Dictionary<string, int> {["archer"] = 5});

         going.Should().HaveCount(1);
         going["archer"].Should().Be(5);
      }

      // The lender's whole ledger at once, which is the shape the host will actually call: settle the loans
      // whose task has ended, leave the ones still running. Pins that the two halves agree on one lord who
      // has both.
      [Test]
      public void GIVEN_a_lender_with_one_finished_and_one_running_task_WHEN_settling_THEN_only_the_finished_one_returns()
      {
         var finished = new TroopLoan {QuestKey = "cr_quest_mina_1_12"};
         var running = new TroopLoan {QuestKey = "cr_quest_mina_3_30"};

         IReadOnlyList<TroopLoan> due = TroopLoanPolicy.LoansToReturn(
            new[] {finished, running},
            new List<string> {"cr_quest_mina_3_30"});

         due.Should().ContainSingle().Which.Should().BeSameAs(finished);
      }

      // Fail safe, and the one outcome with no defence: a loan whose quest the host can no longer find at
      // all (a purged save, a quest dropped by a migration). The task is over one way or another, so the men
      // go home rather than being stranded with the player forever.
      [Test]
      public void GIVEN_the_task_can_no_longer_be_found_WHEN_settling_THEN_the_men_are_not_stranded()
      {
         TroopLoanPolicy.LoansToReturn(new[] {OpenLoan()}, new List<string>())
                        .Should().HaveCount(1);
      }

      // Fail safe: nothing lent, nothing to settle. The host calls this on every lord it talks to.
      [Test]
      public void GIVEN_no_loans_at_all_WHEN_settling_THEN_nothing_is_due()
      {
         TroopLoanPolicy.LoansToReturn(null, new List<string>()).Should().BeEmpty();
         TroopLoanPolicy.ReturnableCounts(null, new Dictionary<string, int>()).Should().BeEmpty();
         TroopLoanPolicy.ShouldReturn(null, false).Should().BeFalse();
      }

      #region private

      private static TroopLoan OpenLoan()
         => new() {
            QuestKey = "cr_quest_mina_1_12",
            OpenedOnDay = 12,
            Troops = new Dictionary<string, int> {["battanian_clanwarrior"] = 20}
         };

      #endregion
   }
}
