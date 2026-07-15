// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0: what may be WAGERED on a duel, and what walking away from one costs before a crowd.
// Honour always rides (relation, renown and regard move whatever is staked), so the wager is the optional
// extra the ratified design puts on the table from Inc 1: gold, or a prisoner's freedom. The load-bearing
// rule is that a wager must be one the challenger can actually cover, or a duel becomes a way to promise gold
// you do not have and free a prisoner you do not hold, an empty promise the bridge must catch BEFORE it is
// offered. The forfeit cost is the other half: refusing a duel before witnesses has to sting in renown, and
// sting more the larger the crowd, or nobody ever fights and the pillar is decorative.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelStakesPolicyTests
   {
      // Honour is always a legal stake because it is not a transfer of goods: it is the relation, renown and
      // regard that move on every duel regardless. If honour were ever rejected, the simplest duel of all, two
      // lords settling a point of pride with nothing material at stake, could not be offered.
      [Test]
      public void GIVEN_an_honour_only_stake_WHEN_checking_legality_THEN_it_is_always_legal()
      {
         DuelStakesPolicy.IsWagerLegal(DuelStakeKind.Honour, 0, 0, false).Should().BeTrue();
      }

      // Staking gold you have is legal; this is the ordinary wager and it must work, or the whole "real stakes
      // from Inc 1" decision is dead on arrival.
      [Test]
      public void GIVEN_gold_the_challenger_can_cover_WHEN_checking_legality_THEN_it_is_legal()
      {
         DuelStakesPolicy.IsWagerLegal(DuelStakeKind.Gold, 500, 500, false).Should().BeTrue();
      }

      // Staking gold you do NOT have is the empty promise the bridge exists to catch: without this, an NPC (or a
      // player) could wager a purse they cannot pay, and the loser's transfer would either fail or drive the
      // balance negative. The bar is "at least as much as staked", tested at the shortfall.
      [Test]
      public void GIVEN_gold_beyond_the_challenger_purse_WHEN_checking_legality_THEN_it_is_refused()
      {
         DuelStakesPolicy.IsWagerLegal(DuelStakeKind.Gold, 500, 499, false).Should().BeFalse();
      }

      // A zero or negative gold wager is not a wager: it must be rejected so "stake gold" cannot be used as a
      // no-cost formality that still claims a material stake happened.
      [Test]
      public void GIVEN_a_non_positive_gold_amount_WHEN_checking_legality_THEN_it_is_refused()
      {
         DuelStakesPolicy.IsWagerLegal(DuelStakeKind.Gold, 0, 1000, false).Should().BeFalse();
         DuelStakesPolicy.IsWagerLegal(DuelStakeKind.Gold, -50, 1000, false).Should().BeFalse();
      }

      // You can only wager a prisoner's freedom when you actually hold a prisoner to free. Without this guard the
      // stake would resolve to freeing nobody, a promise with no referent, and the dramatic wager the design
      // wants (a captured lord's life on the outcome) would silently evaporate.
      [Test]
      public void GIVEN_a_prisoner_freedom_stake_WHEN_the_challenger_holds_one_THEN_it_is_legal_else_refused()
      {
         DuelStakesPolicy.IsWagerLegal(DuelStakeKind.PrisonerFreedom, 0, 0, true).Should().BeTrue();
         DuelStakesPolicy.IsWagerLegal(DuelStakeKind.PrisonerFreedom, 0, 0, false).Should().BeFalse();
      }

      // The forfeit cost must GROW with the audience: refusing a challenge before a full hall is a worse stain
      // than one no one saw, and that gradient is the entire social pressure that makes NPCs (and players) accept
      // rather than duck. A flat cost would let a public coward off as lightly as a private one.
      [Test]
      public void GIVEN_more_witnesses_to_a_refusal_WHEN_costing_the_forfeit_THEN_the_renown_hit_grows()
      {
         int privately = DuelStakesPolicy.ForfeitRenownCost(0);
         int beforeAFew = DuelStakesPolicy.ForfeitRenownCost(3);

         privately.Should().Be(DuelStakesPolicy.BaseForfeitRenown);
         beforeAFew.Should().BeGreaterThan(privately);
      }

      // The cost is capped so no single refusal becomes career-ending legend: a huge crowd should shame heavily
      // but not erase a clan's whole standing in one moment. Pinning the cap stops the per-witness growth from
      // running away in a packed arena.
      [Test]
      public void GIVEN_a_vast_crowd_WHEN_costing_the_forfeit_THEN_it_is_capped()
      {
         DuelStakesPolicy.ForfeitRenownCost(1000).Should().Be(DuelStakesPolicy.MaxForfeitRenown);
      }

      // A negative witness count is a malformed input, not a reason to PAY renown back for refusing: it must
      // floor to the private baseline, never invert the sign and reward a forfeit.
      [Test]
      public void GIVEN_a_negative_witness_count_WHEN_costing_the_forfeit_THEN_it_floors_to_the_private_baseline()
      {
         DuelStakesPolicy.ForfeitRenownCost(-5).Should().Be(DuelStakesPolicy.BaseForfeitRenown);
      }
   }
}
