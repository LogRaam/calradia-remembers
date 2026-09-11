// Code written by Gabriel Mailhot, 25/07/2026.
// This policy decides whether an arranged cross-clan match is CONSENTED TO: the player proposes
// marrying a member of their own clan to a noble of another clan, and the target family either agrees
// or refuses. It is consumed by the mod's arrange_marriage bridge action, which applies the real
// marriage only when ArrangedMatchPolicy.Decide returns Agreed AND the engine's own
// MarriageModel.IsCoupleSuitableForMarriage also passes. If this drifts, either a family marries its
// kin away for nothing (RefusedStanding wrongly returning Agreed), or the feature never fires at all
// (Agreed wrongly returning RefusedStanding). It is deliberately systems-driven, not an LLM judgment
// call, after a player report (CarolusIV, Nexus, 2026-07-25) that a weak local model "gets confused a
// lot" and must not be talkable into an unwarranted match.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ArrangedMatchPolicyTests
    {
        // STAKE: this feature is specifically arranging a match into ANOTHER clan; an intra-clan pairing
        // is out of scope and must be rejected clearly, not silently married, no matter how favorable the
        // other facts look.
        [Test]
        public void GIVEN_the_target_is_of_the_players_own_clan_WHEN_deciding_THEN_it_is_not_cross_clan()
        {
            //Arrange — high regard and a big tier advantage, but the target is not another clan's noble.
            var facts = new ArrangedMatchFacts {TargetIsOfAnotherClan = false, TargetFamilyRegard = 90, PlayerClanTier = 6, TargetClanTier = 1};

            ArrangedMatchPolicy.Decide(facts).Should().Be(ArrangedMatchConsent.NotCrossClan);
        }

        // STAKE: a family that thinks well of the player consents on goodwill alone, even if the player's
        // clan is not the stronger house; regard is a sufficient reason by itself.
        [Test]
        public void GIVEN_regard_at_or_above_the_threshold_WHEN_deciding_THEN_the_family_agrees()
        {
            //Arrange — high regard, but the player's clan is the lower tier.
            var facts = new ArrangedMatchFacts {TargetIsOfAnotherClan = true, TargetFamilyRegard = 50, PlayerClanTier = 2, TargetClanTier = 5};

            ArrangedMatchPolicy.Decide(facts).Should().Be(ArrangedMatchConsent.Agreed);
        }

        // STAKE: marrying their kin up into a greater house is a gain a family takes even when they are
        // lukewarm on the player; without this branch the feature could only ever fire on high regard.
        [Test]
        public void GIVEN_low_regard_but_the_players_clan_outranks_the_target_WHEN_deciding_THEN_the_family_agrees()
        {
            //Arrange — cool regard, but the player's clan is strictly the higher tier.
            var facts = new ArrangedMatchFacts {TargetIsOfAnotherClan = true, TargetFamilyRegard = 5, PlayerClanTier = 4, TargetClanTier = 1};

            ArrangedMatchPolicy.Decide(facts).Should().Be(ArrangedMatchConsent.Agreed);
        }

        // STAKE: a family does not hand over its kin to a stranger of no greater standing; without this
        // refusal the match would be free, cheapening it into a formality instead of a real negotiation.
        [Test]
        public void GIVEN_low_regard_and_no_tier_advantage_WHEN_deciding_THEN_the_family_refuses()
        {
            //Arrange — cool regard and the player's clan is not the stronger house.
            var facts = new ArrangedMatchFacts {TargetIsOfAnotherClan = true, TargetFamilyRegard = 5, PlayerClanTier = 2, TargetClanTier = 3};

            ArrangedMatchPolicy.Decide(facts).Should().Be(ArrangedMatchConsent.RefusedStanding);
        }

        // STAKE: pins the tunable threshold so a later tweak to ConsentRegardThreshold is deliberate and
        // visible; exactly at the line must agree.
        [Test]
        public void GIVEN_regard_exactly_at_the_threshold_WHEN_deciding_THEN_the_family_agrees()
        {
            //Arrange — regard exactly at ConsentRegardThreshold, no tier advantage.
            var facts = new ArrangedMatchFacts {TargetIsOfAnotherClan = true, TargetFamilyRegard = ArrangedMatchPolicy.ConsentRegardThreshold, PlayerClanTier = 3, TargetClanTier = 3};

            ArrangedMatchPolicy.Decide(facts).Should().Be(ArrangedMatchConsent.Agreed);
        }

        // STAKE: the mirror of the previous test; one point below the threshold, with no tier advantage,
        // must refuse. Together the two pin the boundary as inclusive on the agree side only.
        [Test]
        public void GIVEN_regard_one_below_the_threshold_and_no_tier_advantage_WHEN_deciding_THEN_the_family_refuses()
        {
            //Arrange — one below ConsentRegardThreshold, no tier advantage.
            var facts = new ArrangedMatchFacts {TargetIsOfAnotherClan = true, TargetFamilyRegard = ArrangedMatchPolicy.ConsentRegardThreshold - 1, PlayerClanTier = 3, TargetClanTier = 3};

            ArrangedMatchPolicy.Decide(facts).Should().Be(ArrangedMatchConsent.RefusedStanding);
        }

        // STAKE: only a strictly greater house counts as marrying up; equal standing is not an
        // advancement, so a family at equal tier and low regard has no reason to agree.
        [Test]
        public void GIVEN_equal_clan_tiers_and_low_regard_WHEN_deciding_THEN_the_family_refuses()
        {
            //Arrange — equal tiers, cool regard.
            var facts = new ArrangedMatchFacts {TargetIsOfAnotherClan = true, TargetFamilyRegard = 10, PlayerClanTier = 3, TargetClanTier = 3};

            ArrangedMatchPolicy.Decide(facts).Should().Be(ArrangedMatchConsent.RefusedStanding);
        }
    }
}
