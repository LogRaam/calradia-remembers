// Code written by Gabriel Mailhot, 27/06/2026.
// Documents the ingestion-door rules: magnitude is clamped to the carryable range, a summary is bounded to the
// blob-safe length (and an empty one is rejected), and a source is held to a daily quota.
//
// This is the gate Feed.Record stands behind before a third-party fact ever reaches the SAME world-event
// store the Chronicle, rumour hearsay and NPC awareness already read: past it, an unbounded summary
// threatens the save-blob limit, an unclamped magnitude could outweigh every native CR event in how far it
// carries, and a source with no quota could flood the store (and every NPC's hearsay) by calling Record in
// a loop or re-emitting the same event every tick.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ExternalEventRulesTests
    {
        // Magnitude drives how far a fact carries through hearsay; a negative value (a mod's bug, or a
        // hostile one) must not produce an undefined or negative "reach" downstream in the locality logic.
        [Test]
        public void GIVEN_a_negative_magnitude_WHEN_clamping_THEN_it_floors_at_zero()
        {
            ExternalEventRules.ClampMagnitude(-50).Should().Be(0);
        }

        // Caps a foreign event's reach to the same 0..100 ceiling CR's own native events use, so a single
        // fed fact can never claim to outweigh, and so dominate NPC awareness over, everything CR itself has
        // ever recorded.
        [Test]
        public void GIVEN_an_overlarge_magnitude_WHEN_clamping_THEN_it_caps_at_the_max()
        {
            ExternalEventRules.ClampMagnitude(5000).Should().Be(ExternalEventRules.MaxMagnitude);
        }

        // The control case: a legitimate magnitude must pass through untouched, so a well-behaved source's
        // events are never needlessly flattened by the clamp above.
        [Test]
        public void GIVEN_a_magnitude_in_range_WHEN_clamping_THEN_it_is_unchanged()
        {
            ExternalEventRules.ClampMagnitude(42).Should().Be(42);
        }

        // Null is Feed.Record's reject signal: a world event with no real summary text says nothing, and
        // must not silently enter the store as a blank entry NPCs could later "recall".
        [Test]
        public void GIVEN_an_empty_or_whitespace_summary_WHEN_bounding_THEN_it_is_rejected_as_null()
        {
            ExternalEventRules.BoundSummary(null).Should().BeNull();
            ExternalEventRules.BoundSummary("   ").Should().BeNull();
        }

        // The same save-blob guard as the mission door: an oversized summary from a third-party mod is hard-
        // capped at MaxSummaryChars regardless of what the mod actually sends.
        [Test]
        public void GIVEN_an_overlong_summary_WHEN_bounding_THEN_it_is_truncated_to_the_blob_guard()
        {
            string huge = new string('x', ExternalEventRules.MaxSummaryChars + 200);

            ExternalEventRules.BoundSummary(huge).Length.Should().Be(ExternalEventRules.MaxSummaryChars);
        }

        // The summary is stored and later surfaced near-verbatim as hearsay/rumour text an NPC speaks: stray
        // whitespace a mod's formatting left in must not carry into that spoken line.
        [Test]
        public void GIVEN_a_padded_summary_WHEN_bounding_THEN_it_is_trimmed()
        {
            ExternalEventRules.BoundSummary("  a quiet rumour  ").Should().Be("a quiet rumour");
        }

        // The anti-flood boundary Feed.ConsumeQuota enforces, and it must be INCLUSIVE at the cap: one below
        // MaxPerSourcePerDay still gets through, exactly at it is throttled, or a source calling Record in a
        // loop (or re-emitting the same event every tick) could still flood the store one event past the
        // intended limit.
        [Test]
        public void GIVEN_a_source_at_its_quota_WHEN_checking_THEN_it_is_throttled()
        {
            ExternalEventRules.WithinDailyQuota(ExternalEventRules.MaxPerSourcePerDay - 1).Should().BeTrue();
            ExternalEventRules.WithinDailyQuota(ExternalEventRules.MaxPerSourcePerDay).Should().BeFalse();
        }

        // tashmetu's Bellum bridge names a besieged town's OWNER in HeroIds. When the player owns it, the siege
        // became "a deed of yours": it left the news, entered the player's chronicle, and characters told it back
        // to the player as something the player had done. Named is concerned, not the one who acted.
        [Test]
        public void GIVEN_the_player_only_named_as_the_owner_of_a_besieged_town_WHEN_fed_THEN_it_is_news_not_their_deed()
        {
            ExternalEventRules.IsPlayersOwnDeed(playerIsActor: false).Should().BeFalse("naming the player in HeroIds is not saying they acted");
        }

        // The other half: a bridge reporting something the player DID (an execution by the player's decree) says
        // so, and it becomes the player's deed, heard of by others as such, whether or not it also names the player in HeroIds.
        [Test]
        public void GIVEN_the_feeder_declares_the_player_acted_WHEN_fed_THEN_it_is_the_players_own_deed()
        {
            ExternalEventRules.IsPlayersOwnDeed(playerIsActor: true).Should().BeTrue();
        }
    }
}
