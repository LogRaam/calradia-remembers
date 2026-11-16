// Code written by Gabriel Mailhot, 09/11/2026.
// What these pin: a deed the interpreter reads is carried out once and as the deed the game knows it to be.
//
// Cost bench, 09/11/2026 (Gabriel: "vas-y pour l'interprete, les refus et give_troops"): a refused demand for coin was
// read as change_relation alone, so the attempt was never recorded, and a lord taking the fresh men he had asked for
// was read as give_troops, so his task stayed open. The reader is now taught both; the game makes sure that a refusal
// read as extort AND as anger costs regard once (TurnRegardPolicy), that an extort paid beside a give_gold is paid once
// (TurnPaymentPolicy), and that men a lord asked for close his task (TroopHandOverReadingPolicy). If these fail, a
// refused threat costs the player twice, a paid one empties the purse twice, or a delivered errand never completes.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
   [TestFixture]
   public sealed class DeedReadingTests
   {
      // ── One regard cost per deed ──

      // STAKE: Orthes refuses the demand and the reader writes extort and change_relation(-12); the extort already
      // charges the regard hit and the grudge, so the anger is the same cost and must not land a second time.
      [Test]
      public void GIVEN_a_refused_demand_read_as_extort_and_anger_WHEN_the_turn_runs_THEN_regard_is_lost_once()
      {
         TurnRegardPolicy.IsSameCostAsADeedThisTurn(new TurnRegardFacts {
            ActionType = "change_relation", Delta = -12, DeedsTheGameWillCarryOut = new[] {"change_relation", "extort"}
         }).Should().BeTrue();
      }

      // STAKE: when the game will refuse the extort (no hold over this character), it charges nothing, so the
      // character's anger at the threat is the only cost left and still applies.
      [Test]
      public void GIVEN_a_threat_the_game_will_refuse_WHEN_the_character_is_angered_THEN_the_anger_still_counts()
      {
         TurnRegardPolicy.IsSameCostAsADeedThisTurn(new TurnRegardFacts {
            ActionType = "change_relation", Delta = -12, DeedsTheGameWillCarryOut = new[] {"change_relation"}
         }).Should().BeFalse();
      }

      // STAKE: the rule only removes a cost written twice; warmth is not part of what the extort charges.
      [Test]
      public void GIVEN_warmth_beside_an_extort_WHEN_the_turn_runs_THEN_it_is_not_taken_for_the_deeds_cost()
      {
         TurnRegardPolicy.IsSameCostAsADeedThisTurn(new TurnRegardFacts {
            ActionType = "change_relation", Delta = 3, DeedsTheGameWillCarryOut = new[] {"extort"}
         }).Should().BeFalse();
      }

      // STAKE: an angry reply in an ordinary turn, with no deed charging its own cost, keeps its regard change.
      [Test]
      public void GIVEN_anger_in_a_turn_with_no_deed_WHEN_the_turn_runs_THEN_it_applies()
      {
         TurnRegardPolicy.IsSameCostAsADeedThisTurn(new TurnRegardFacts {
            ActionType = "change_relation", Delta = -5, DeedsTheGameWillCarryOut = new[] {"change_relation", "give_gold"}
         }).Should().BeFalse();
      }

      // ── One payment per extortion ──

      // STAKE: the extort executor moves the coin it takes; a give_gold written beside it for "here, take it" is
      // the same coin, and honoured would take a second payment from the character's purse.
      [Test]
      public void GIVEN_a_paid_extortion_and_a_gold_move_WHEN_the_turn_runs_THEN_the_coin_moves_once()
      {
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn("give_gold", new[] {"extort", "give_gold"}).Should().BeTrue();
      }

      // ── Men a lord asked for are his errand ──

      // STAKE: Vsevolod takes the fifteen levies he asked for and the reader calls it give_troops; the player holds
      // them all, so the hand-over is his task and must close it.
      [Test]
      public void GIVEN_a_lords_open_recruit_task_and_the_men_in_hand_WHEN_read_as_give_troops_THEN_the_task_is_delivered()
      {
         TroopHandOverReadingPolicy.IsTheRecruitTask(new TroopHandOverFacts {RecruitTaskOutstanding = true, PlayerHoldsTheRecruitsAsked = true})
            .Should().BeTrue();
      }

      // STAKE: a player short of the recruits who offers other soldiers to a thin lord is reinforcing him, not
      // delivering; carried out as the task, the game would refuse it for want of recruits.
      [Test]
      public void GIVEN_an_open_task_but_too_few_recruits_WHEN_soldiers_are_offered_THEN_they_reinforce_him_as_offered()
      {
         TroopHandOverReadingPolicy.IsTheRecruitTask(new TroopHandOverFacts {RecruitTaskOutstanding = true, PlayerHoldsTheRecruitsAsked = false})
            .Should().BeFalse();
      }

      // STAKE: with no task asked of the player, soldiers given are only ever a reinforcement.
      [Test]
      public void GIVEN_no_recruit_task_WHEN_soldiers_are_given_THEN_they_reinforce_him()
      {
         TroopHandOverReadingPolicy.IsTheRecruitTask(new TroopHandOverFacts {RecruitTaskOutstanding = false, PlayerHoldsTheRecruitsAsked = true})
            .Should().BeFalse();
      }

      // ── Men for the walls are the garrison task ──

      // STAKE: Aporo stands in Onira with the men Queen Rhagaea asked for her walls and the reader writes give_troops;
      // read as a field party's reinforcement it was refused "no room or need" and the task never closed.
      [Test]
      public void GIVEN_a_garrison_task_waiting_where_the_player_stands_WHEN_read_as_give_troops_THEN_it_is_the_hand_over()
      {
         TroopHandOverReadingPolicy.IsTheGarrisonTask(new GarrisonHandOverFacts {GarrisonTaskWaitingHere = true}).Should().BeTrue();
      }

      // STAKE: away from the place, or with no such task, soldiers given are a reinforcement as offered.
      [Test]
      public void GIVEN_no_garrison_task_waiting_here_WHEN_soldiers_are_given_THEN_they_reinforce_as_offered()
      {
         TroopHandOverReadingPolicy.IsTheGarrisonTask(new GarrisonHandOverFacts {GarrisonTaskWaitingHere = false}).Should().BeFalse();
      }

      // ── A warmth is "paid by the quest" only when the quest pays ──

      // STAKE: Aporo's queen claimed her task was done, the game refused the claim and paid nothing, and her +3 warmth
      // was dropped as "already covered": he lost both. A refused claim must leave the warmth to its own gate.
      [Test]
      public void GIVEN_a_completion_claim_the_game_refuses_WHEN_warmth_is_written_THEN_the_warmth_is_kept()
      {
         QuestClaimRegardPolicy.IsPaidByTheQuestReward(new QuestClaimRegardFacts {CompletionClaimed = true, ClaimWillBePaid = false, HandOverSettlesATask = false, Delta = 3})
            .Should().BeFalse();
      }

      // STAKE: M-R3: a claim the game pays brings its own fixed relation reward; the warmth beside it is the same goodwill twice.
      [Test]
      public void GIVEN_a_completion_claim_the_game_pays_WHEN_warmth_is_written_THEN_it_is_dropped()
      {
         QuestClaimRegardPolicy.IsPaidByTheQuestReward(new QuestClaimRegardFacts {CompletionClaimed = true, ClaimWillBePaid = true, Delta = 3})
            .Should().BeTrue();
      }

      // STAKE: men handed over in the same reply settle the task and pay its reward themselves, so the warmth is a double count.
      [Test]
      public void GIVEN_a_hand_over_that_settles_the_task_in_the_same_reply_WHEN_warmth_is_written_THEN_it_is_dropped()
      {
         QuestClaimRegardPolicy.IsPaidByTheQuestReward(new QuestClaimRegardFacts {CompletionClaimed = true, HandOverSettlesATask = true, Delta = 2})
            .Should().BeTrue();
      }

      // STAKE: an offence given during a turn-in is real; only warmth is ever taken for the reward's goodwill.
      [Test]
      public void GIVEN_a_paid_claim_WHEN_anger_is_written_THEN_it_still_counts()
      {
         QuestClaimRegardPolicy.IsPaidByTheQuestReward(new QuestClaimRegardFacts {CompletionClaimed = true, ClaimWillBePaid = true, Delta = -4})
            .Should().BeFalse();
      }
   }
}
