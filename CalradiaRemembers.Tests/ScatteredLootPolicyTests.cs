// Code written by Gabriel Mailhot, 24/07/2026.
// Scattered-loot recovery (ratified 2026-07-24, AUDIT-NEMESIS M1): a fled bandit escape scatters the player's
// gear by culture, and defeating a band of that culture has a CHANCE to return the OLDEST-lost matching piece.
// This policy owns those two decisions; ScatteredLootBehavior owns the dice, the strip, and the item grant.
// What breaks if it is wrong: recover the wrong culture's item (loot leaks back from a band the player never
// lost to), drain in the wrong order (a newer loss surfaces before an older), or make recovery certain (the
// whole stock dumps from one victory instead of the ratified piece-by-piece), or impossible (gear lost forever).

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Items;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScatteredLootPolicyTests
    {
        private static ScatteredLootItem Piece(string culture, int lostDay, string itemId = "sword")
            => new() {Item = new HeldItem {ItemStringId = itemId}, CultureKey = culture, LostDay = lostDay};

        // ── RollRecovers ─────────────────────────────────────────────────────

        // The whole point of a chance below 100: a stock must be recovered over SEVERAL fights, not dumped from
        // one victory. A roll below the threshold recovers; at or above it does not, so the boundary is pinned
        // against a future retune silently changing how fast the player gets their gear back.
        [Test]
        public void GIVEN_a_roll_below_the_chance_WHEN_checking_recovery_THEN_it_recovers_and_the_boundary_holds()
        {
            ScatteredLootPolicy.RollRecovers(0).Should().BeTrue();
            ScatteredLootPolicy.RollRecovers(ScatteredLootPolicy.RecoveryChancePercent - 1).Should().BeTrue();
            ScatteredLootPolicy.RollRecovers(ScatteredLootPolicy.RecoveryChancePercent).Should().BeFalse();
        }

        // A malformed roll must never be treated as a recovery: a negative would otherwise sneak past a naive
        // "< chance" test and hand back gear on a fight that should not have yielded any.
        [Test]
        public void GIVEN_a_negative_roll_WHEN_checking_recovery_THEN_it_never_recovers()
        {
            ScatteredLootPolicy.RollRecovers(-1).Should().BeFalse();
        }

        // ── SelectRecoverable ────────────────────────────────────────────────

        // The core rule: a defeated band only yields back gear the player lost to THAT culture. Beating forest
        // bandits must never return an item scattered among looters, or loot leaks from bands the player never
        // lost to.
        [Test]
        public void GIVEN_a_defeated_culture_WHEN_selecting_THEN_only_that_cultures_scattered_item_is_returned()
        {
            var scattered = new List<ScatteredLootItem> {
                Piece("looters", 10, "sword"),
                Piece("forest_bandits", 12, "bow")
            };

            ScatteredLootItem picked = ScatteredLootPolicy.SelectRecoverable(scattered, new[] {"forest_bandits"});

            picked.Should().NotBeNull();
            picked.Item.ItemStringId.Should().Be("bow");
        }

        // A scattered stock drains in the order it was taken: the OLDEST loss of a matching culture surfaces
        // first, so recovery reads as reclaiming your history, not a random dip into the pile.
        [Test]
        public void GIVEN_several_matching_pieces_WHEN_selecting_THEN_the_oldest_loss_comes_back_first()
        {
            var scattered = new List<ScatteredLootItem> {
                Piece("looters", 40, "newer"),
                Piece("looters", 10, "older"),
                Piece("looters", 25, "middle")
            };

            ScatteredLootPolicy.SelectRecoverable(scattered, new[] {"looters"}).Item.ItemStringId.Should().Be("older");
        }

        // Culture matching is case-insensitive: a band-clan StringId cased differently between the scatter and
        // the defeat must still match, or a piece would be stranded forever.
        [Test]
        public void GIVEN_a_culture_cased_differently_WHEN_selecting_THEN_it_still_matches()
        {
            var scattered = new List<ScatteredLootItem> {Piece("Looters", 5)};

            ScatteredLootPolicy.SelectRecoverable(scattered, new[] {"looters"}).Should().NotBeNull();
        }

        // No matching band means no recovery this fight: beating a lord's army, or a bandit culture the player
        // never lost to, must return nothing rather than leak an unrelated piece.
        [Test]
        public void GIVEN_no_matching_culture_WHEN_selecting_THEN_nothing_is_returned()
        {
            var scattered = new List<ScatteredLootItem> {Piece("looters", 5)};

            ScatteredLootPolicy.SelectRecoverable(scattered, new[] {"sea_raiders"}).Should().BeNull();
            ScatteredLootPolicy.SelectRecoverable(scattered, new string[0]).Should().BeNull();
            ScatteredLootPolicy.SelectRecoverable(new List<ScatteredLootItem>(), new[] {"looters"}).Should().BeNull();
        }
    }
}
