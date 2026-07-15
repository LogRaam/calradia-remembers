// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0: what a duel LEAVES BEHIND, across the four ways it can end. The through-line the whole
// pillar teaches is that steel honestly crossed SETTLES (respect is earned, a driving grudge is answered
// whoever wins) while walking away HARDENS (contempt, a grudge that festers because it was never faced). The
// signs are the entire lesson: if winning a duel cost the NPC's respect, or fleeing one settled a grudge, the
// feature would train players to do the opposite of what a duel means. Non-lethal is assumed throughout, so
// these are movements of regard, renown and reputation, never a death.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelOutcomePolicyTests
   {
      // Winning a fair duel must EARN the loser's respect and some fear, and hand the player renown. This is the
      // core payoff of ever fighting one; if the respect sign were wrong, beating a lord would make him think
      // less of you, inverting the whole point.
      [Test]
      public void GIVEN_the_player_won_a_fair_duel_WHEN_resolving_THEN_the_npc_respects_and_fears_the_player_more_and_renown_rises()
      {
         DuelOutcomeResult r = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon));

         r.NpcRespectDelta.Should().BePositive();
         r.NpcFearDelta.Should().BePositive();
         r.PlayerRenownDelta.Should().BePositive();
      }

      // A win before a crowd must travel further than one in private: renown scales with witnesses, because a
      // duel's whole social weight is who saw it. Without the gradient, fighting publicly would be no more
      // rewarding than a quiet bout and the witness system would be pointless here.
      [Test]
      public void GIVEN_the_player_won_WHEN_more_witnessed_it_THEN_the_renown_gain_is_larger()
      {
         int privately = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon, witnesses: 0)).PlayerRenownDelta;
         int beforeACrowd = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon, witnesses: 6)).PlayerRenownDelta;

         beforeACrowd.Should().BeGreaterThan(privately);
      }

      // Losing a duel you DARED is not contemptible: the NPC still gains a little respect for the player facing
      // them, and sheds some fear (they have taken your measure). If a loss only ever cost the player standing,
      // players would never risk a fight they might lose, and half the drama would vanish.
      [Test]
      public void GIVEN_the_npc_won_a_fair_duel_WHEN_resolving_THEN_the_player_still_earns_some_respect_and_the_npc_fears_them_less()
      {
         DuelOutcomeResult r = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.NpcWon));

         r.NpcRespectDelta.Should().BePositive();
         r.NpcFearDelta.Should().BeNegative();
      }

      // Losing is respectable but not glory: the player's renown for a loss must NOT balloon with the crowd the
      // way a win does. Pinning the flat, non-scaled value stops a public loss from paying like a public win.
      [Test]
      public void GIVEN_the_npc_won_WHEN_more_witnessed_it_THEN_the_player_renown_does_not_scale_with_the_crowd()
      {
         int privately = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.NpcWon, witnesses: 0)).PlayerRenownDelta;
         int beforeACrowd = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.NpcWon, witnesses: 20)).PlayerRenownDelta;

         beforeACrowd.Should().Be(privately);
      }

      // The marquee case: a grudge that DROVE the duel is settled by steel WHOEVER wins, and the fight clears
      // enough air to restore some warmth. This is the payoff the Grudges pillar reaches for. If a fought grudge
      // were left unsettled, "settle it with steel" would be a lie.
      [Test]
      public void GIVEN_a_grudge_drove_the_duel_WHEN_it_was_fought_to_a_finish_THEN_it_settles_whoever_won()
      {
         DuelOutcomeResult playerWon = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon, grudge: true));
         DuelOutcomeResult npcWon = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.NpcWon, grudge: true));

         playerWon.GrudgeEffect.Should().Be(DuelGrudgeEffect.Settled);
         npcWon.GrudgeEffect.Should().Be(DuelGrudgeEffect.Settled);
      }

      // Settling a grudge by fighting must make the player MORE liked than winning an ungrudged duel, not less:
      // the cleared-air warmth has to outweigh a proud loser's sting, or "we settled it and moved on" would read
      // as the NPC hating you more for beating them.
      [Test]
      public void GIVEN_a_player_win_WHEN_it_settled_a_grudge_versus_not_THEN_affection_ends_up_higher_with_the_grudge_settled()
      {
         int settled = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon, grudge: true)).NpcAffectionDelta;
         int noGrudge = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon, grudge: false)).NpcAffectionDelta;

         settled.Should().BeGreaterThan(noGrudge);
      }

      // The player walking away must COST respect, fear and renown: this is the price that makes accepting mean
      // something. If a forfeit were free, the accept-or-duck choice the acceptance policy models would have no
      // teeth on the player's own side.
      [Test]
      public void GIVEN_the_player_forfeited_WHEN_resolving_THEN_respect_fear_and_renown_all_fall()
      {
         DuelOutcomeResult r = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerForfeited, witnesses: 3));

         r.NpcRespectDelta.Should().BeNegative();
         r.NpcFearDelta.Should().BeNegative();
         r.PlayerRenownDelta.Should().BeNegative();
      }

      // A grudge the player DUCKED must harden, never settle: refusing to face it is the opposite of answering it,
      // and this is the sign that most defines the pillar. A settled-on-forfeit bug would let players clear
      // grudges by cowardice, the exact inverse of the design.
      [Test]
      public void GIVEN_a_grudge_drove_it_WHEN_the_player_forfeited_THEN_the_grudge_hardens()
      {
         DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerForfeited, grudge: true))
            .GrudgeEffect.Should().Be(DuelGrudgeEffect.Hardened);
      }

      // When the NPC is the one who balks, the player gains renown (a called bluff) and the humiliated NPC
      // resents them more; a driving grudge hardens on the NPC's side too. Without this branch, an NPC could
      // duck with no consequence and the player's challenge would be a bluff of its own.
      [Test]
      public void GIVEN_the_npc_forfeited_WHEN_resolving_THEN_the_player_gains_renown_and_a_driving_grudge_hardens()
      {
         DuelOutcomeResult r = DuelOutcomePolicy.Resolve(Ctx(DuelResolution.NpcForfeited, grudge: true));

         r.PlayerRenownDelta.Should().BePositive();
         r.NpcAffectionDelta.Should().BeNegative();
         r.GrudgeEffect.Should().Be(DuelGrudgeEffect.Hardened);
      }

      // An honour-only duel transfers no goods however it ends: the award must be None even on a clear win, so
      // the host never tries to move gold or free a prisoner that was never staked.
      [Test]
      public void GIVEN_an_honour_only_stake_WHEN_the_player_won_THEN_no_wager_changes_hands()
      {
         DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon, stake: DuelStakeKind.Honour))
            .StakeAward.Should().Be(DuelStakeAward.None);
      }

      // A material wager goes to whoever won the FOUGHT duel: the winner collects, and only then. This is what
      // makes gold or a prisoner's freedom a real stake rather than decoration.
      [Test]
      public void GIVEN_a_material_stake_WHEN_a_side_won_the_fight_THEN_that_side_collects()
      {
         DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerWon, stake: DuelStakeKind.Gold))
            .StakeAward.Should().Be(DuelStakeAward.ToPlayer);
         DuelOutcomePolicy.Resolve(Ctx(DuelResolution.NpcWon, stake: DuelStakeKind.PrisonerFreedom))
            .StakeAward.Should().Be(DuelStakeAward.ToNpc);
      }

      // A refusal is not a contest, so no wager changes hands even with gold on the table: the stake only moves
      // on a fight actually fought. Otherwise ducking a duel could still cost (or win) the purse, which no one agreed to.
      [Test]
      public void GIVEN_a_material_stake_WHEN_a_side_forfeited_THEN_no_wager_changes_hands()
      {
         DuelOutcomePolicy.Resolve(Ctx(DuelResolution.PlayerForfeited, stake: DuelStakeKind.Gold))
            .StakeAward.Should().Be(DuelStakeAward.None);
         DuelOutcomePolicy.Resolve(Ctx(DuelResolution.NpcForfeited, stake: DuelStakeKind.Gold))
            .StakeAward.Should().Be(DuelStakeAward.None);
      }

      // A null context yields an all-zero, no-effect result: a missing outcome must never silently move regard,
      // renown, a grudge, or a wager.
      [Test]
      public void GIVEN_no_context_WHEN_resolving_THEN_nothing_moves()
      {
         DuelOutcomeResult r = DuelOutcomePolicy.Resolve(null);

         r.NpcAffectionDelta.Should().Be(0);
         r.NpcRespectDelta.Should().Be(0);
         r.NpcFearDelta.Should().Be(0);
         r.PlayerRenownDelta.Should().Be(0);
         r.GrudgeEffect.Should().Be(DuelGrudgeEffect.None);
         r.StakeAward.Should().Be(DuelStakeAward.None);
      }

      private static DuelResolutionContext Ctx(
         DuelResolution resolution,
         bool grudge = false,
         int witnesses = 0,
         DuelStakeKind stake = DuelStakeKind.Honour)
         => new() {
            Resolution = resolution,
            AGrudgeDroveIt = grudge,
            WitnessCount = witnesses,
            Stake = stake
         };
   }
}
