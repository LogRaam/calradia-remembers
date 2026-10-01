// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "Playing wingman for a guy trying hard to get a girl married?" Gabriel: a REAL marriage,
// earned in two steps, her heart then her family's consent; never a marriage forced on the family's word alone. If these
// rules are wrong, a lady is married off to a man she does not care for because her father likes the player, a family
// gives a daughter to a match it has no reason to accept, or two of one house are "matched".

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MatchmakerPolicyTests
    {
        private static MatchFacts WonOverAndTrusted() => new MatchFacts {
            BrideRelationToSuitor = VanillaRelation.FriendTier, OfAnotherHouse = true,
            FamilyRegardForThePlayer = ArrangedMatchPolicy.ConsentRegardThreshold, SuitorClanTier = 2, BrideClanTier = 2
        };

        // Her heart, then her family: both given, they are wed.
        [Test]
        public void GIVEN_she_holds_him_a_friend_and_her_family_trusts_the_player_WHEN_decided_THEN_they_are_wed()
        {
            MatchmakerPolicy.Decide(WonOverAndTrusted()).Should().Be(MatchVerdict.Wed);
        }

        // Gabriel: no forced marriage on the family's word. However much her father trusts the player, she must hold
        // the suitor a friend first.
        [Test]
        public void GIVEN_she_does_not_hold_him_a_friend_WHEN_decided_THEN_she_must_be_won_over_first()
        {
            MatchFacts f = WonOverAndTrusted();
            f.BrideRelationToSuitor = VanillaRelation.FriendTier - 1;
            f.FamilyRegardForThePlayer = 100;

            MatchmakerPolicy.Decide(f).Should().Be(MatchVerdict.NotWonOver);
        }

        // Her family weighs it as any arranged match: not enough regard for the broker and no gain in standing, no.
        [Test]
        public void GIVEN_a_family_with_no_reason_to_accept_WHEN_decided_THEN_it_refuses()
        {
            MatchFacts f = WonOverAndTrusted();
            f.FamilyRegardForThePlayer = ArrangedMatchPolicy.ConsentRegardThreshold - 1;

            MatchmakerPolicy.Decide(f).Should().Be(MatchVerdict.FamilyRefuses);
        }

        // Marrying UP is a gain in itself: the house she joins is the suitor's, and his higher tier wins the family over.
        [Test]
        public void GIVEN_the_suitor_of_a_higher_house_WHEN_decided_THEN_the_family_agrees_on_standing_alone()
        {
            MatchFacts f = WonOverAndTrusted();
            f.FamilyRegardForThePlayer = 0;
            f.SuitorClanTier = 4;

            MatchmakerPolicy.Decide(f).Should().Be(MatchVerdict.Wed);
        }

        // Two of one house are not a match the player brokers.
        [Test]
        public void GIVEN_two_of_one_house_WHEN_decided_THEN_it_is_no_match()
        {
            MatchFacts f = WonOverAndTrusted();
            f.OfAnotherHouse = false;

            MatchmakerPolicy.Decide(f).Should().Be(MatchVerdict.SameHouse);
        }
    }
}
