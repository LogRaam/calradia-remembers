// Code written by Gabriel Mailhot, 03/07/2026.
// Station-matched behavior guidelines: occupation -> register mapping + the text fallback chain.
// Get ResolveStationKey wrong and a street bandit boss or common merchant is voiced with the SAME lordly
// courtly register as a king, the exact tone mismatch a player reported. Get SelectStationText's fallback
// chain wrong and a missing or blank station file (shipping without one, or a modder's own edit) blanks the
// prompt guidance entirely instead of quietly falling back to the base guidelines, so every existing save and
// modder customization keeps working unchanged.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public class StationGuidelinesPolicyTests
    {
        private static string Lookup(Dictionary<string, string> files, string key)
            => files.TryGetValue(key, out string? text) ? text : "";

        [Test]
        public void GIVEN_a_gang_leader_WHEN_resolving_the_station_THEN_the_street_register_applies()
        {
            // The player report: a gang leader greeted the player as a lady rising from her throne.
            StationGuidelinesPolicy.ResolveStationKey("GangLeader").Should().Be(StationGuidelinesPolicy.GangLeaderKey);
        }

        // Bandit heroes share the same station as gang leaders: a captured or fought bandit boss must never
        // slip back into the lordly courtly voice the player report was about.
        [Test]
        public void GIVEN_a_bandit_hero_WHEN_resolving_the_station_THEN_the_street_register_applies_too()
        {
            StationGuidelinesPolicy.ResolveStationKey("Bandit").Should().Be(StationGuidelinesPolicy.GangLeaderKey);
        }

        // Every civilian occupation this policy recognizes must land on ONE shared register: a per-occupation
        // typo here would silently strand a merchant or headman back on the mismatched lordly voice.
        [Test]
        public void GIVEN_town_and_village_notables_WHEN_resolving_the_station_THEN_they_share_the_notable_register()
        {
            foreach (string occupation in new[] {"Merchant", "Artisan", "RuralNotable", "Headman", "Preacher"})
                StationGuidelinesPolicy.ResolveStationKey(occupation).Should().Be(StationGuidelinesPolicy.NotableKey, occupation);
        }

        // Wanderers are potential companions the player may hire; a distinct free-blade register keeps them
        // from reading as courtly lords before they have ever held a title.
        [Test]
        public void GIVEN_a_wanderer_WHEN_resolving_the_station_THEN_the_free_blade_register_applies()
        {
            StationGuidelinesPolicy.ResolveStationKey("Wanderer").Should().Be(StationGuidelinesPolicy.WandererKey);
        }

        [Test]
        public void GIVEN_a_lord_or_an_unknown_occupation_WHEN_resolving_the_station_THEN_the_base_register_is_kept()
        {
            // Conservative by design: anything unrecognized keeps the lordly base, never a wrong register.
            StationGuidelinesPolicy.ResolveStationKey("Lord").Should().BeNull();
            StationGuidelinesPolicy.ResolveStationKey("NotARealOccupation").Should().BeNull();
            StationGuidelinesPolicy.ResolveStationKey(null).Should().BeNull();
        }

        // A recruited companion keeps the raw "Wanderer" occupation, so occupation alone would voice a sworn
        // shieldmate at the player's side as a loose tavern free blade (the mismatch fkasad/TOR reported). The
        // companion signal lifts them out of the wanderer register into their own.
        [Test]
        public void GIVEN_a_wanderer_who_joined_the_player_WHEN_resolving_the_station_THEN_the_companion_register_applies()
        {
            StationGuidelinesPolicy.ResolveStationKey("Wanderer", isPlayerCompanion: true, holdsFief: false)
                .Should().Be(StationGuidelinesPolicy.CompanionKey);
        }

        // The top rung of the ladder: a companion granted a fief is landed, and a landed hero carries
        // themselves as a lord, so the fief signal outranks the companion one and returns the base register.
        [Test]
        public void GIVEN_a_companion_granted_a_fief_WHEN_resolving_the_station_THEN_it_rises_to_the_lordly_base()
        {
            StationGuidelinesPolicy.ResolveStationKey("Wanderer", isPlayerCompanion: true, holdsFief: true)
                .Should().BeNull();
        }

        // With no promotion signals the overload must be exactly the plain occupation mapping: a loose
        // wanderer stays a free blade, a merchant stays a notable. This pins that the ladder never disturbs
        // an NPC who has not risen in the player's service.
        [Test]
        public void GIVEN_no_promotion_signals_WHEN_resolving_the_station_THEN_it_matches_the_plain_occupation_mapping()
        {
            StationGuidelinesPolicy.ResolveStationKey("Wanderer", isPlayerCompanion: false, holdsFief: false)
                .Should().Be(StationGuidelinesPolicy.WandererKey);
            StationGuidelinesPolicy.ResolveStationKey("Merchant", isPlayerCompanion: false, holdsFief: false)
                .Should().Be(StationGuidelinesPolicy.NotableKey);
        }

        // The straightforward path: a station file that genuinely exists with real text is used as-is,
        // the baseline the fallback-chain tests below all depart from.
        [Test]
        public void GIVEN_the_station_file_exists_WHEN_selecting_the_text_THEN_it_is_returned()
        {
            var files = new Dictionary<string, string> {{StationGuidelinesPolicy.WandererKey, "wanderer text"}};

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.WandererKey, k => Lookup(files, k))
                .Should().Be("wanderer text");
        }

        [Test]
        public void GIVEN_the_gangleader_file_is_missing_WHEN_selecting_the_text_THEN_it_falls_back_to_the_notable_register()
        {
            // The nearest civilian register beats the lordly default for a street boss.
            var files = new Dictionary<string, string> {{StationGuidelinesPolicy.NotableKey, "notable text"}};

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.GangLeaderKey, k => Lookup(files, k))
                .Should().Be("notable text");
        }

        // A modder who has not written a companion file yet must not drop a sworn companion all the way back
        // to the lordly base: the register they rose FROM (wanderer) is the nearest fit, so a companion falls
        // to the wanderer file before the base default.
        [Test]
        public void GIVEN_the_companion_file_is_missing_WHEN_selecting_the_text_THEN_it_falls_back_to_the_wanderer_register()
        {
            var files = new Dictionary<string, string> {{StationGuidelinesPolicy.WandererKey, "wanderer text"}};

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.CompanionKey, k => Lookup(files, k))
                .Should().Be("wanderer text");
        }

        // When the companion file DOES exist it is used as-is, never overridden by the wanderer fallback below it.
        [Test]
        public void GIVEN_the_companion_file_exists_WHEN_selecting_the_text_THEN_it_is_used()
        {
            var files = new Dictionary<string, string> {
                {StationGuidelinesPolicy.CompanionKey, "companion text"},
                {StationGuidelinesPolicy.WandererKey, "wanderer text"}
            };

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.CompanionKey, k => Lookup(files, k))
                .Should().Be("companion text");
        }

        // Windmolen (19/10/2026) asked whether merchants could have a guidelines file of their own. A merchant
        // with a merchant file must be voiced by it, or the player's file is silently ignored.
        [Test]
        public void GIVEN_a_merchant_file_exists_WHEN_selecting_a_merchants_text_THEN_the_trade_file_wins()
        {
            var files = new Dictionary<string, string> {
                {StationGuidelinesPolicy.NotableKey, "notable text"},
                {"merchant", "merchant text"}
            };

            string trade = StationGuidelinesPolicy.TradeKeyOf("Merchant");

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.NotableKey, trade, k => Lookup(files, k))
                .Should().Be("merchant text");
        }

        // None ships: an artisan, or a merchant whose player wrote no file, keeps the shared notable voice.
        [Test]
        public void GIVEN_no_file_for_this_trade_WHEN_selecting_the_text_THEN_the_shared_notable_file_applies()
        {
            var files = new Dictionary<string, string> {
                {StationGuidelinesPolicy.NotableKey, "notable text"},
                {"merchant", "merchant text"},
                {"artisan", "  "}
            };

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.NotableKey, StationGuidelinesPolicy.TradeKeyOf("Artisan"), k => Lookup(files, k))
                .Should().Be("notable text");
        }

        // A trade file is looked for only under a notable: a gang leader or a lord never picks up a file
        // because its name happens to match.
        [Test]
        public void GIVEN_someone_who_is_not_a_notable_WHEN_asking_their_trade_THEN_there_is_none()
        {
            foreach (string occupation in new[] {"GangLeader", "Bandit", "Wanderer", "Lord", null})
                StationGuidelinesPolicy.TradeKeyOf(occupation).Should().BeNull(occupation ?? "null");
        }

        // Every trade the loader reads is a trade some notable can have, and each notable has one: a key
        // missing from TradeKeys would leave that trade's file unread.
        [Test]
        public void GIVEN_each_notable_occupation_WHEN_asking_its_trade_THEN_the_loader_reads_that_file()
        {
            foreach (string occupation in new[] {"Merchant", "Artisan", "RuralNotable", "Headman", "Preacher"})
                StationGuidelinesPolicy.TradeKeys.Should().Contain(StationGuidelinesPolicy.TradeKeyOf(occupation), occupation);
        }

        // A guideline section written "if you are a headman" is followed only if the model is told it is one.
        // Headmen and preachers used to read as "a person of some standing in Calradia".
        [Test]
        public void GIVEN_each_notable_occupation_WHEN_describing_their_rank_THEN_the_trade_is_named()
        {
            StationGuidelinesPolicy.NotableTradeLine("Merchant").Should().Contain("merchant");
            StationGuidelinesPolicy.NotableTradeLine("Artisan").Should().Contain("artisan");
            StationGuidelinesPolicy.NotableTradeLine("RuralNotable").Should().Contain("village notable");
            StationGuidelinesPolicy.NotableTradeLine("Headman").Should().Contain("headman");
            StationGuidelinesPolicy.NotableTradeLine("Preacher").Should().Contain("preacher");
            StationGuidelinesPolicy.NotableTradeLine("Lord").Should().BeNull();
        }

        // A modder who ships the mod without any station files (or hasn't written them yet) must fall
        // through cleanly to the base guidelines, never to a missing or blank prompt section.
        [Test]
        public void GIVEN_no_station_file_exists_WHEN_selecting_the_text_THEN_null_means_use_the_base_guidelines()
        {
            var empty = new Dictionary<string, string>();

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.NotableKey, k => Lookup(empty, k)).Should().BeNull();
            StationGuidelinesPolicy.SelectStationText(null, k => Lookup(empty, k)).Should().BeNull();
        }

        // A shipped-but-blank station file (a placeholder, or a modder's WIP edit) must not blank the
        // prompt's behavior guidance outright; treating blank as "absent" keeps the base guidelines as the
        // safety net instead of silently voicing the NPC with no guidance at all.
        [Test]
        public void GIVEN_a_blank_station_file_WHEN_selecting_the_text_THEN_it_falls_through_instead_of_blanking_the_prompt()
        {
            var files = new Dictionary<string, string> {{StationGuidelinesPolicy.WandererKey, "   "}};

            StationGuidelinesPolicy.SelectStationText(StationGuidelinesPolicy.WandererKey, k => Lookup(files, k)).Should().BeNull();
        }
    }
}
