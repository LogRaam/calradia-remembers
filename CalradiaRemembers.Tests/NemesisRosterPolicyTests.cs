// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar: the regional recurring roster. On any qualifying capture/encounter, reuse the living
// nemesis of that band culture in that region if one exists (you recross the SAME boss); otherwise the
// caller mints a fresh one. This is the pure lookup only — minting is out of scope here.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisRosterPolicyTests
    {
        // ── Basic match / no-match ────────────────────────────────────────────

        [Test]
        public void GIVEN_a_living_nemesis_of_the_same_band_and_region_WHEN_finding_a_recurring_nemesis_THEN_he_is_returned()
        {
            NemesisIdentity ranulf = Make("n1", "forest_bandits", "region_north", isAlive: true);
            var roster = new List<NemesisIdentity> { ranulf };

            NemesisRosterPolicy.FindRecurring(roster, "forest_bandits", "region_north").Should().BeSameAs(ranulf);
        }

        [Test]
        public void GIVEN_an_empty_roster_WHEN_finding_a_recurring_nemesis_THEN_null_signals_mint_a_new_one()
        {
            NemesisRosterPolicy.FindRecurring(new List<NemesisIdentity>(), "forest_bandits", "region_north").Should().BeNull();
        }

        [Test]
        public void GIVEN_a_null_roster_WHEN_finding_a_recurring_nemesis_THEN_null_is_returned_without_throwing()
        {
            NemesisRosterPolicy.FindRecurring(null, "forest_bandits", "region_north").Should().BeNull();
        }

        // ── Filters ───────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_the_only_matching_identity_is_dead_WHEN_finding_a_recurring_nemesis_THEN_he_is_ignored_and_null_is_returned()
        {
            NemesisIdentity deadRanulf = Make("n1", "forest_bandits", "region_north", isAlive: false);
            var roster = new List<NemesisIdentity> { deadRanulf };

            NemesisRosterPolicy.FindRecurring(roster, "forest_bandits", "region_north").Should().BeNull();
        }

        [Test]
        public void GIVEN_the_only_matching_identity_has_been_turned_to_the_players_service_WHEN_finding_a_recurring_nemesis_THEN_he_is_ignored()
        {
            // Increment 4: a spared, turned nemesis is still alive but no longer an enemy captor, so he must
            // never be reused as the boss of a fresh band that takes the player prisoner.
            NemesisIdentity turned = Make("n1", "forest_bandits", "region_north", isAlive: true);
            turned.TurnedDay = 120;
            var roster = new List<NemesisIdentity> { turned };

            NemesisRosterPolicy.FindRecurring(roster, "forest_bandits", "region_north").Should().BeNull();
        }

        [Test]
        public void GIVEN_a_living_nemesis_in_a_different_region_WHEN_finding_a_recurring_nemesis_THEN_he_is_ignored()
        {
            NemesisIdentity elsewhere = Make("n1", "forest_bandits", "region_south", isAlive: true);
            var roster = new List<NemesisIdentity> { elsewhere };

            NemesisRosterPolicy.FindRecurring(roster, "forest_bandits", "region_north").Should().BeNull();
        }

        [Test]
        public void GIVEN_a_living_nemesis_of_a_different_band_culture_WHEN_finding_a_recurring_nemesis_THEN_he_is_ignored()
        {
            NemesisIdentity wrongBand = Make("n1", "sea_raiders", "region_north", isAlive: true);
            var roster = new List<NemesisIdentity> { wrongBand };

            NemesisRosterPolicy.FindRecurring(roster, "forest_bandits", "region_north").Should().BeNull();
        }

        // ── Tie-break ─────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_two_qualifying_identities_WHEN_one_has_been_encountered_more_THEN_the_more_encountered_one_wins()
        {
            NemesisIdentity veteran = Make("n2", "forest_bandits", "region_north", isAlive: true, timesEncountered: 5);
            NemesisIdentity fresh = Make("n1", "forest_bandits", "region_north", isAlive: true, timesEncountered: 1);
            var roster = new List<NemesisIdentity> { fresh, veteran };

            NemesisRosterPolicy.FindRecurring(roster, "forest_bandits", "region_north").Should().BeSameAs(veteran);
        }

        [Test]
        public void GIVEN_two_qualifying_identities_tied_on_encounters_WHEN_finding_a_recurring_nemesis_THEN_the_lowest_id_wins_deterministically()
        {
            // Documented tie-break: most-encountered first, then lowest Id (ordinal) as the final,
            // fully deterministic fallback — never a random pick between two otherwise-equal bosses.
            NemesisIdentity a = Make("n-aaa", "forest_bandits", "region_north", isAlive: true, timesEncountered: 3);
            NemesisIdentity b = Make("n-bbb", "forest_bandits", "region_north", isAlive: true, timesEncountered: 3);
            var roster = new List<NemesisIdentity> { b, a };

            NemesisRosterPolicy.FindRecurring(roster, "forest_bandits", "region_north").Should().BeSameAs(a);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // ── Respawn cooldown after a kill (no identical new boss on the next capture) ──

        [Test]
        public void GIVEN_a_recently_killed_nemesis_still_in_cooldown_WHEN_checking_the_culture_THEN_a_fresh_mint_is_blocked()
        {
            NemesisIdentity dead = Make("d1", "looters", "region_a", isAlive: false);
            dead.DeathDay = 100;
            dead.RespawnBlockedUntilDay = 100 + NemesisRosterPolicy.RespawnCooldownMinDays;

            NemesisRosterPolicy.IsCultureRespawnBlocked(new List<NemesisIdentity> { dead }, "looters", today: 120)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_cooldown_has_elapsed_WHEN_checking_the_culture_THEN_a_fresh_mint_is_allowed()
        {
            NemesisIdentity dead = Make("d1", "looters", "region_a", isAlive: false);
            dead.RespawnBlockedUntilDay = 200;

            NemesisRosterPolicy.IsCultureRespawnBlocked(new List<NemesisIdentity> { dead }, "looters", today: 200)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_cooldown_is_for_a_DIFFERENT_culture_WHEN_checking_THEN_this_culture_is_not_blocked()
        {
            NemesisIdentity dead = Make("d1", "sea_raiders", "region_a", isAlive: false);
            dead.RespawnBlockedUntilDay = 300;

            NemesisRosterPolicy.IsCultureRespawnBlocked(new List<NemesisIdentity> { dead }, "looters", today: 120)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_living_nemesis_with_no_death_cooldown_WHEN_checking_THEN_nothing_is_blocked()
        {
            NemesisIdentity living = Make("n1", "looters", "region_a", isAlive: true);

            NemesisRosterPolicy.IsCultureRespawnBlocked(new List<NemesisIdentity> { living }, "looters", today: 120)
                .Should().BeFalse();
        }

        private static NemesisIdentity Make(string id, string bandCultureKey, string regionId, bool isAlive, int timesEncountered = 0)
            => new NemesisIdentity
            {
                Id = id,
                BandCultureKey = bandCultureKey,
                RegionId = regionId,
                IsAlive = isAlive,
                TimesEncountered = timesEncountered
            };
    }
}
