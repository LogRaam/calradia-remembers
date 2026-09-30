// Code written by Gabriel Mailhot, 30/09/2026.
// A clan's standing warms a first meeting (tashmetu, Nexus, 30/09/2026: "With a clan standing of +54 (friendship, by
// the game's own terms) and a personal regard of +5, an NPC can still treat the player like a near-stranger who has to
// prove himself... It would stay personal, just not start from zero with someone the whole clan already respects.").
// Ruled by Gabriel the same day: the clan's standing seeds personal regard ONCE, at the first conversation where it
// warrants a seed, and only as far as "friendly regard", the same ceiling letters have: a reputation earned without
// meeting takes you to friendly and no further. The pace of regard (cooldown, cap) is untouched. If this is wrong,
// either the whole clan's friendship still counts for nothing in person (the report), or the clan's standing becomes
// a lever the player pulls again and again, erasing a personal quarrel each time.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ClanStandingSeedPolicyTests
    {
        private static ClanStandingSeedFacts Facts(int clanStanding, int regard = 0, bool alreadySeeded = false, bool enabled = true)
            => new ClanStandingSeedFacts {ClanStanding = clanStanding, CurrentRegard = regard, AlreadySeeded = alreadySeeded, Enabled = enabled};

        // tashmetu's case: the whole clan counts the player a trusted friend, and the lord starts as a near-stranger.
        [Test]
        public void GIVEN_a_clan_that_counts_the_player_a_trusted_friend_WHEN_its_lord_is_first_met_THEN_they_start_on_friendly_terms()
        {
            ClanStandingSeed seed = ClanStandingSeedPolicy.Decide(Facts(54));

            seed.Shift.Should().Be(Regard.FriendlyTier);
            seed.Consumed.Should().BeTrue();
        }

        // The prompt tells the character "your clan regards this player favorably" from +10; a warm-but-not-close clan
        // earns a warmer start, but less than a trusted friend's.
        [Test]
        public void GIVEN_a_clan_that_merely_regards_the_player_favorably_WHEN_first_met_THEN_the_start_is_cordial()
        {
            ClanStandingSeedPolicy.Decide(Facts(15)).Shift.Should().Be(Regard.CordialTier);
        }

        // Standings the prompt calls "no strong feeling" (and anything colder) change nothing, and the seed stays
        // available for the day the clan's standing rises.
        [Test]
        public void GIVEN_a_clan_with_no_strong_feeling_WHEN_first_met_THEN_nothing_changes_and_the_seed_waits()
        {
            ClanStandingSeed seed = ClanStandingSeedPolicy.Decide(Facts(5));

            seed.Shift.Should().Be(0);
            seed.Consumed.Should().BeFalse();
        }

        // A clan's hostility is not turned into a personal grudge here: tashmetu asked for the warm side only, and a
        // grudge in this mod is anchored on a deed.
        [Test]
        public void GIVEN_a_hostile_clan_WHEN_first_met_THEN_personal_regard_is_not_lowered()
        {
            ClanStandingSeedPolicy.Decide(Facts(-60)).Shift.Should().Be(0);
        }

        // Someone already met, whose regard is +5 with a clan at +54: a one-time catch-up to friendly, not a bonus on top.
        [Test]
        public void GIVEN_someone_already_met_below_the_clan_s_level_WHEN_the_seed_is_first_applied_THEN_regard_rises_to_it_and_no_further()
        {
            ClanStandingSeedPolicy.Decide(Facts(54, regard: 5)).Shift.Should().Be(Regard.FriendlyTier - 5);
        }

        // Regard earned in person above the seed is never lowered by it, and the seed is spent.
        [Test]
        public void GIVEN_regard_already_above_what_the_clan_would_give_WHEN_checked_THEN_nothing_changes_and_the_seed_is_spent()
        {
            ClanStandingSeed seed = ClanStandingSeedPolicy.Decide(Facts(54, regard: 25));

            seed.Shift.Should().Be(0);
            seed.Consumed.Should().BeTrue();
        }

        // "It would stay personal": a character the player has personally offended keeps the offence, whatever the
        // clan thinks. The seed is spent so it can never wash the quarrel away later.
        [Test]
        public void GIVEN_a_character_the_player_has_personally_offended_WHEN_the_clan_is_friendly_THEN_the_offence_stands()
        {
            ClanStandingSeed seed = ClanStandingSeedPolicy.Decide(Facts(54, regard: -8));

            seed.Shift.Should().Be(0);
            seed.Consumed.Should().BeTrue();
        }

        // Once only: after the seed, regard that falls (a quarrel) is not lifted back by the clan at the next meeting.
        [Test]
        public void GIVEN_the_seed_already_given_WHEN_regard_has_since_fallen_THEN_the_clan_does_not_lift_it_again()
        {
            ClanStandingSeedPolicy.Decide(Facts(54, regard: 2, alreadySeeded: true)).Shift.Should().Be(0);
        }

        // The MCM switch: a player who wants regard to start from zero with everyone keeps that.
        [Test]
        public void GIVEN_the_option_turned_off_WHEN_a_trusted_friend_s_clan_is_met_THEN_regard_starts_from_zero_as_before()
        {
            ClanStandingSeed seed = ClanStandingSeedPolicy.Decide(Facts(54, enabled: false));

            seed.Shift.Should().Be(0);
            seed.Consumed.Should().BeFalse();
        }
    }
}
