// Code written by Gabriel Mailhot, 20/06/2026.
// The three gates behind join_clan, the most powerful (and most AI-Influence-requested) recruitment: talking a
// LORD into leaving their own house and clan for the player's, no menu, just conversation. This policy is
// consulted at BOTH surfaces around the LLM ("the prompt is advice, the bridge is law"): first whether join_clan
// is even taught to the model, then again whether the action actually executes. Get a gate wrong and either a
// lord with every reason to stay quietly gets talked into abandoning house and liege, or a genuinely desperate,
// deeply-trusting, outranked, estranged lord is refused with no visible reason. These tests pin exactly where
// each of the three gates (Trust, Rank, Vulnerability) sits, and that Vulnerability is an OR of three independent
// levers, not an AND of all three.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LordRecruitmentTests
   {
      // MinRelation (60) is meant to read as "deep trust, not merely friendly" (production doc): the floor
      // must be inclusive, or a lord who has genuinely earned deep trust gets told they haven't earned it yet.
      [Test]
      public void GIVEN_a_bond_exactly_at_the_trust_floor_WHEN_evaluating_THEN_it_passes()
      {
         //Arrange — the threshold is inclusive.
         LordRecruitmentFacts f = Facts(LordRecruitment.MinRelation, 3, 3, true);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
      }

      // Without this block, trust and rank alone would be enough to poach any lord regardless of how little
      // reason they have to go: "no one abandons comfort lightly" (production doc). Landed, strong clan, happy
      // liege must together refuse recruitment even at high trust and outranking.
      [Test]
      public void GIVEN_a_comfortable_landed_lord_in_a_strong_house_content_with_their_liege_WHEN_evaluating_THEN_they_will_not_leave()
      {
         //Arrange — trust and rank pass, but nothing makes this lord want to go.
         LordRecruitmentFacts f = Facts(90, 6, 2,
            false, false, true, 30);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.LordNotVulnerable);
      }

      // Vulnerability is an OR of three levers, not an AND: estrangement from a liege alone must flip an
      // otherwise-comfortable lord recruitable. A stricter AND would make recruitment nearly unreachable
      // (landless AND clan-broken AND estranged all at once is rare) and gut the shipped feature.
      [Test]
      public void GIVEN_a_lord_estranged_from_their_liege_WHEN_evaluating_THEN_that_estrangement_makes_them_recruitable()
      {
         //Arrange — comfortable otherwise, but they despise the liege they serve.
         LordRecruitmentFacts f = Facts(65, 4, 3,
            false, false, true, -30);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
      }

      // The same OR discipline as the estrangement test, but for the clan-weak/destroyed lever: a landed lord
      // whose house is shattered has nothing left to protect them, and that alone must be enough on its own.
      [Test]
      public void GIVEN_a_lord_whose_clan_is_broken_WHEN_evaluating_THEN_that_alone_makes_them_recruitable()
      {
         //Arrange — landed but their house is shattered: vulnerability via clan strength.
         LordRecruitmentFacts f = Facts(65, 3, 2, false, true);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
      }

      // Guards a specific data-leak bug class: RelationWithLiege must never count toward vulnerability unless
      // HasLiege is also true. A lordless-of-liege hero can still carry a stale or default RelationWithLiege
      // value; if IsVulnerable ever dropped the HasLiege guard, a landless-masking scenario (landed + strong
      // clan + no liege) would silently start passing recruitment on garbage data.
      [Test]
      public void GIVEN_a_lord_with_no_liege_but_a_low_stored_liege_relation_WHEN_evaluating_THEN_it_does_not_count_as_vulnerability()
      {
         //Arrange — a landless guard would mask this, so test landed + strong clan + no liege.
         LordRecruitmentFacts f = Facts(65, 4, 2,
            false, false, false, -99);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.LordNotVulnerable);
      }

      // Which reason surfaces when several gates fail is real, player-facing behaviour (it is the string shown
      // by cr.recruit_info / join_clan's refusal), not incidental short-circuit order. Pinning Trust first keeps
      // that report stable if a future edit reorders the checks in Evaluate.
      [Test]
      public void GIVEN_both_trust_and_rank_fail_WHEN_evaluating_THEN_trust_is_reported_first()
      {
         //Arrange — the most fundamental blocker is surfaced first, for clear prompt/debug hinting.
         LordRecruitmentFacts f = Facts(10, 1, 5, true);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.TrustTooLow);
      }

      // Exercises IsEligible, the convenience wrapper both live call sites (EncounterContextBuilder's prompt
      // gate and BannerlordGameStateBridge's execution gate) actually use, not just Evaluate directly: a
      // regression that broke only the wrapper would otherwise slip past the other tests in this file.
      [Test]
      public void GIVEN_deep_trust_a_higher_rank_and_a_landless_lord_WHEN_evaluating_THEN_they_can_be_recruited()
      {
         //Arrange
         LordRecruitmentFacts f = Facts(70, 4, 2, true);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
         LordRecruitment.IsEligible(f).Should().BeTrue();
      }

      // The rank gate is "player >= lord", never strict inequality; an off-by-one here would block a player
      // who has just caught up to a lord's house tier, even though nothing about their standing is inferior.
      [Test]
      public void GIVEN_equal_clan_tiers_WHEN_evaluating_THEN_rank_is_not_a_barrier()
      {
         //Arrange — the gate is "player >= lord", so equal standing passes.
         LordRecruitmentFacts f = Facts(65, 3, 3, clanWeak: true);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
      }

      // LiegeResentmentThreshold (-20) must be inclusive, mirroring the same "boundary belongs to the rule"
      // discipline as MinRelation: a lord exactly at the resentment line has to count as estranged, not need
      // to sink one point further before recruitment ever opens.
      [Test]
      public void GIVEN_estrangement_exactly_at_the_threshold_WHEN_evaluating_THEN_it_counts()
      {
         //Arrange — the resentment threshold is inclusive.
         LordRecruitmentFacts f = Facts(65, 4, 3,
            hasLiege: true, relWithLiege: LordRecruitment.LiegeResentmentThreshold);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.None);
      }

      // join_clan hands a lord's entire house to the player; a missing/failed facts lookup (e.g. an engine
      // exception in LordRecruitmentFactsBuilder) must never quietly read as eligible. Failing to TrustTooLow
      // keeps the action closed rather than defaulting open on absent data.
      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_THEN_it_fails_closed()
      {
         //Act/Assert — never default a powerful action open.
         LordRecruitment.IsEligible(null).Should().BeFalse();
      }

      // Pins the substantive gap between "friendly" (40) and MinRelation's "deep trust" (60): every other gate
      // qualifies here, so this isolates that friendliness alone is not enough to uproot a lord from their house.
      [Test]
      public void GIVEN_only_a_friendly_bond_WHEN_evaluating_THEN_trust_is_too_low()
      {
         //Arrange — everything else qualifies, but the bond is merely friendly, not deep.
         LordRecruitmentFacts f = Facts(40, 5, 1, true);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.TrustTooLow);
      }

      // Guards the political-plausibility rule regardless of personal warmth: a duke does not follow a lesser
      // house. Without this gate a max-trust, highly-vulnerable lord could be poached by a player far below
      // their own station, which would make clan-tier standing meaningless for recruitment.
      [Test]
      public void GIVEN_the_player_outranked_by_the_lords_house_WHEN_evaluating_THEN_their_rank_is_too_low()
      {
         //Arrange — a duke does not follow a lesser house, however warm the bond.
         LordRecruitmentFacts f = Facts(80, 2, 4, true);

         //Act/Assert
         LordRecruitment.Evaluate(f).Should().Be(LordRecruitmentBlock.PlayerRankTooLow);
      }

      #region private

      private static LordRecruitmentFacts Facts(
         int relation,
         int playerTier,
         int lordTier,
         bool landless = false,
         bool clanWeak = false,
         bool hasLiege = false,
         int relWithLiege = 0)
         => new() {
            Relation = relation,
            PlayerClanTier = playerTier,
            LordClanTier = lordTier,
            LordIsLandless = landless,
            LordClanWeakOrDestroyed = clanWeak,
            HasLiege = hasLiege,
            RelationWithLiege = relWithLiege
         };

      #endregion
   }
}