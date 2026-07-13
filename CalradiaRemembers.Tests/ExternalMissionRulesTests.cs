// Code written by Gabriel Mailhot, 27/06/2026.
// Documents the mission-registration-door rules: title/description bounded to the blob-safe length (and an
// empty one rejected), reward gold/relation clamped to CR's own payable caps (the external mod cannot promise
// a fortune or a flood of regard), an optional deadline normalised, and the id+source identity required.
//
// These rules are the entire gate MissionBoard.Register stands behind: once through, the mission becomes a
// real InformalQuest with a journal entry and a paid reward, so a careless or hostile mod that skipped them
// could push an oversized string toward the save-blob limit, or promise a reward CR can't actually honour.
// The id+source check is what lets a mission be deduped, later completed by its own source's callback, and
// (if that source misbehaves) singled out and muted, without touching any other mod's missions.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ExternalMissionRulesTests
    {
        // A mod's own bug (or a hostile one) could hand a negative reward: flooring at zero stops a negative
        // gold grant from ever reaching the payout path.
        [Test]
        public void GIVEN_a_negative_gold_reward_WHEN_clamping_THEN_it_floors_at_zero()
        {
            ExternalMissionRules.ClampRewardGold(-200).Should().Be(0);
        }

        // CR pays only within its own caps, whatever a foreign mission promises: an int.MaxValue reward must
        // clamp down to MaxRewardGold, not actually get granted, or a third-party mission could bankrupt the
        // player's economy or hand out a windfall no native quest ever offers.
        [Test]
        public void GIVEN_an_overlarge_gold_reward_WHEN_clamping_THEN_it_caps_at_the_max()
        {
            ExternalMissionRules.ClampRewardGold(int.MaxValue).Should().Be(ExternalMissionRules.MaxRewardGold);
        }

        // MaxRewardRelation matches CR's own native quest-reward cap, the one that bypasses the relation
        // gate because the deed is verified: an external mission must never be able to buy the player more
        // trust, faster, than the game's own honest quests ever grant for a real deed.
        [Test]
        public void GIVEN_a_relation_reward_above_the_gated_cap_WHEN_clamping_THEN_it_caps_at_the_quest_reward_limit()
        {
            ExternalMissionRules.ClampRewardRelation(99).Should().Be(ExternalMissionRules.MaxRewardRelation);
            ExternalMissionRules.ClampRewardRelation(-5).Should().Be(0);
            ExternalMissionRules.ClampRewardRelation(7).Should().Be(7);
        }

        // Null is the caller's "reject this registration" signal: a mission with no real title text must not
        // silently become a blank line in the player's journal.
        [Test]
        public void GIVEN_an_empty_or_whitespace_title_WHEN_bounding_THEN_it_is_rejected_as_null()
        {
            ExternalMissionRules.BoundTitle(null).Should().BeNull();
            ExternalMissionRules.BoundTitle("   ").Should().BeNull();
        }

        // The save-blob guard: an unbounded description string from a third-party mod is a save-corruption
        // risk, so it is hard-capped at MaxDescriptionChars regardless of what the mod sends.
        [Test]
        public void GIVEN_an_overlong_description_WHEN_bounding_THEN_it_is_truncated_to_the_blob_guard()
        {
            string huge = new string('x', ExternalMissionRules.MaxDescriptionChars + 200);

            ExternalMissionRules.BoundDescription(huge).Length.Should().Be(ExternalMissionRules.MaxDescriptionChars);
        }

        // Incidental whitespace a mod's own string formatting adds should not surface as stray padding in the
        // giver's in-chat line or the journal entry the player actually reads.
        [Test]
        public void GIVEN_a_padded_title_WHEN_bounding_THEN_it_is_trimmed()
        {
            ExternalMissionRules.BoundTitle("  Win the autumn race  ").Should().Be("Win the autumn race");
        }

        // A mod passing 0 or a negative day count (a bug, or genuinely "no deadline") must resolve to open-
        // ended, never to an already-expired quest the player never had a real chance to complete.
        [Test]
        public void GIVEN_no_deadline_WHEN_normalising_THEN_it_stays_open_ended()
        {
            ExternalMissionRules.NormalizeDeadlineDays(null).Should().BeNull();
            ExternalMissionRules.NormalizeDeadlineDays(0).Should().BeNull();
            ExternalMissionRules.NormalizeDeadlineDays(-3).Should().BeNull();
        }

        // Caps how long a foreign mission can occupy the player's journal: a mod that miscalculates a day
        // count (or deliberately sets one absurdly high) must not park a quest there indefinitely.
        [Test]
        public void GIVEN_an_overlong_deadline_WHEN_normalising_THEN_it_caps_at_a_year()
        {
            ExternalMissionRules.NormalizeDeadlineDays(10000).Should().Be(ExternalMissionRules.MaxDeadlineDays);
        }

        // The control case: a legitimate, fair deadline must pass through untouched, so the clamps above
        // never bite a well-behaved mission by mistake.
        [Test]
        public void GIVEN_a_deadline_in_range_WHEN_normalising_THEN_it_is_unchanged()
        {
            ExternalMissionRules.NormalizeDeadlineDays(30).Should().Be(30);
        }

        // Without both a stable id and a source-mod tag, MissionBoard has no way to dedup this mission, route
        // its later Complete() signal back to the right source, or mute that source if it misbehaves: either
        // one missing means the registration is refused outright.
        [Test]
        public void GIVEN_a_missing_id_or_source_WHEN_checking_identity_THEN_it_is_not_registerable()
        {
            ExternalMissionRules.HasRequiredIdentity("", "GrandRaces").Should().BeFalse();
            ExternalMissionRules.HasRequiredIdentity("race_42", "  ").Should().BeFalse();
            ExternalMissionRules.HasRequiredIdentity("race_42", "GrandRaces").Should().BeTrue();
        }
    }
}
