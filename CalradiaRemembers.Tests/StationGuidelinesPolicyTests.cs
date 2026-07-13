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
            => files.TryGetValue(key, out string text) ? text : "";

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
