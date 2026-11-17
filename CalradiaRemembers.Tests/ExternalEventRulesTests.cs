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
        // to the player as something the player had done. The besieger acted; the owner is only concerned.
        [Test]
        public void GIVEN_a_siege_of_the_players_town_by_a_lord_WHEN_fed_THEN_it_is_news_not_the_players_deed()
        {
            var besiegers = new[] {"lord_besieger"};

            ExternalEventRules.IsPlayersOwnDeed(besiegers, "main_hero").Should().BeFalse("the player owns the town; the lord did the besieging");
        }

        // The other half: a lord executed by the player's decree. The bridge lists the executioner among the actors
        // as it would for anyone, without ever asking whether that is the player, and CR files it as the player's deed.
        [Test]
        public void GIVEN_an_execution_by_the_players_decree_WHEN_fed_THEN_it_is_the_players_own_deed()
        {
            var executioners = new[] {"main_hero"};

            ExternalEventRules.IsPlayersOwnDeed(executioners, "main_hero").Should().BeTrue();
        }

        // Most fed facts name no actor at all (a crown losing its grip, a village's harvest). They are news, and a
        // bridge that never fills the field must not have anything filed as the player's.
        [Test]
        public void GIVEN_a_fact_with_no_actors_WHEN_fed_THEN_it_is_news()
        {
            ExternalEventRules.IsPlayersOwnDeed(null, "main_hero").Should().BeFalse();
            ExternalEventRules.IsPlayersOwnDeed(new string[0], "main_hero").Should().BeFalse();
        }

        // ── Details: the full text, kept outside the save (Bellum Civile / AI Rebel Relay request, 17/11/2026) ──

        // STAKE: the request was "no silent truncation" of a kingdom digest; every size up to the cap is kept exactly.
        [TestCase(300)]
        [TestCase(1000)]
        [TestCase(5000)]
        [TestCase(20000)]
        public void GIVEN_details_up_to_the_cap_WHEN_normalized_THEN_they_are_kept_whole(int length)
        {
            string text = new string('a', length - 1) + "z";

            string kept = ExternalEventRules.NormalizeDetails(text);

            kept.Should().Be(text);
            ExternalEventRules.DetailsFit(kept).Should().BeTrue();
        }

        // STAKE: beyond the cap the text is refused whole, never cut: a bridge learns it was too long instead of losing its end.
        [Test]
        public void GIVEN_details_over_the_cap_WHEN_checked_THEN_they_do_not_fit()
        {
            ExternalEventRules.DetailsFit(new string('a', ExternalEventRules.MaxDetailsChars + 1)).Should().BeFalse();
        }

        // STAKE: an event with no details must stay an ordinary event, with nothing written outside the save.
        [Test]
        public void GIVEN_blank_details_WHEN_normalized_THEN_there_are_none()
        {
            ExternalEventRules.NormalizeDetails("   ").Should().BeNull();
            ExternalEventRules.DetailsFit(null).Should().BeTrue();
            ExternalEventRules.DetailsKey("bellum", null).Should().BeNull();
        }

        // STAKE: a save loaded later must read the text IT wrote; a key that did not change with the text would hand an older
        // save the digest a later Update wrote on another branch of the campaign.
        [Test]
        public void GIVEN_two_texts_WHEN_keyed_THEN_the_same_text_keeps_its_key_and_a_changed_one_gets_a_new_key()
        {
            string first = ExternalEventRules.DetailsKey("Bellum", "The crown of Vlandia passed to Derthert's niece.");

            ExternalEventRules.DetailsKey("bellum", "The crown of Vlandia passed to Derthert's niece.").Should().Be(first);
            ExternalEventRules.DetailsKey("Bellum", "The crown of Vlandia passed to Derthert's nephew.").Should().NotBe(first);
            first.Should().MatchRegex("^[0-9a-f]{32}$");
        }

        // STAKE: two mods writing the same words must not share one file, or one's Retract could reach the other's text.
        [Test]
        public void GIVEN_the_same_text_from_two_mods_WHEN_keyed_THEN_the_keys_differ()
        {
            ExternalEventRules.DetailsKey("bellum", "A harvest feast.").Should().NotBe(ExternalEventRules.DetailsKey("relay", "A harvest feast."));
        }

        // STAKE: the summary's cut stays (it is what every aware character hears), but the host must be able to say so.
        [Test]
        public void GIVEN_a_long_summary_WHEN_checked_THEN_the_cut_is_seen()
        {
            ExternalEventRules.SummaryIsCut(new string('a', ExternalEventRules.MaxSummaryChars + 1)).Should().BeTrue();
            ExternalEventRules.SummaryIsCut(new string('a', ExternalEventRules.MaxSummaryChars)).Should().BeFalse();
        }
    }
}
