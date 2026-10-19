// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (2026-07-09): every courier sent was waylaid, every single time, losing the typed letter
// for good. Design ruling (2026-07-10): a courier is now IGNORED by default; each bandit party it passes
// rolls independently (about 1 in 3) to turn hostile and give chase, and even then a fast courier usually
// outruns the chase. These two independent rolls are what keeps real interception rare and earned, instead
// of the routine loss the report described.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CourierWaylayPolicyTests
    {
        // This is the ruled fix for the reported "every courier waylaid" bug: pins the exact roll boundary
        // (33) so the hostile chance stays at the ruled ~1-in-3, not drifting back toward "always gives chase."
        [TestCase(0, true)]
        [TestCase(32, true)]
        [TestCase(33, false)]
        [TestCase(99, false)]
        public void GIVEN_a_roll_WHEN_deciding_hostility_THEN_only_the_bottom_third_turns_hostile(int roll, bool hostile)
        {
            CourierWaylayPolicy.ShouldTurnHostile(roll).Should().Be(hostile);
        }

        // Defensive: a malformed or out-of-range roll must fail safe to "let the courier pass" rather than
        // accidentally always (or never) triggering hostility.
        [Test]
        public void GIVEN_a_negative_or_out_of_range_roll_WHEN_deciding_THEN_it_is_not_hostile()
        {
            CourierWaylayPolicy.ShouldTurnHostile(-1).Should().BeFalse();
            CourierWaylayPolicy.ShouldTurnHostile(100).Should().BeFalse();
        }

        // A full-range coverage check that the actual hostile count matches BanditHostileChancePercent
        // exactly, guarding against an off-by-one at the boundary silently shifting the real-world odds
        // away from the ruled "roughly 1 in 3."
        [Test]
        public void GIVEN_the_full_roll_range_WHEN_counting_hostiles_THEN_it_is_about_one_in_three()
        {
            var hostile = 0;
            for (var roll = 0; roll < 100; roll++)
                if (CourierWaylayPolicy.ShouldTurnHostile(roll))
                    hostile++;

            hostile.Should().Be(CourierWaylayPolicy.BanditHostileChancePercent);
        }

        // The second half of the two-roll design, the courier's own speed advantage; if the catch rate crept
        // up, "speed should keep them out of trouble" would stop being true and interception would become
        // routine again, reviving the original player complaint.
        [TestCase(0, true)]
        [TestCase(29, true)]
        [TestCase(30, false)]
        [TestCase(99, false)]
        public void GIVEN_a_chase_WHEN_deciding_the_catch_THEN_the_fast_courier_usually_escapes(int roll, bool caught)
        {
            CourierWaylayPolicy.ChaseCatchesCourier(roll).Should().Be(caught);
        }

        // Pins the end-to-end odds a single bandit band actually catches the courier (well under either
        // individual roll), confirming the combined two-roll design is what actually fixes the reported
        // "every courier waylaid" bug, not just one contributing roll on its own.
        [Test]
        public void GIVEN_both_filters_WHEN_combined_THEN_the_effective_per_band_waylay_is_well_under_the_chase_rate()
        {
            // The two independent filters (gives chase AND catches) make interception rare and speed-rewarding.
            double effective = CourierWaylayPolicy.BanditHostileChancePercent / 100.0
                               * (CourierWaylayPolicy.ChaseCatchesCourierPercent / 100.0);
            effective.Should().BeLessThan(0.15);
        }

        // The whole point of the cap (player report 2026-08-05: letters lost almost every time when far). The
        // per-band roll is independent, so on a LONG trip the courier passes MANY bands and the ~10% per-band
        // loss compounds toward near-certain (10 bands ~65%, 20 bands ~88%). Before any band has rolled, the
        // budget must be open so a courier is never immune from the very first band it passes.
        [Test]
        public void GIVEN_no_band_has_rolled_yet_WHEN_checking_the_budget_THEN_a_band_may_still_roll()
        {
            CourierWaylayPolicy.HasWaylayBudget(0).Should().BeTrue();
        }

        // The second band must still get its roll: the cap is 2, not 1, so interception stays possible (the
        // design keeps it rare-but-real) rather than collapsing to a single all-or-nothing chance per courier.
        [Test]
        public void GIVEN_one_band_has_rolled_WHEN_checking_the_budget_THEN_a_second_band_may_still_roll()
        {
            CourierWaylayPolicy.HasWaylayBudget(1).Should().BeTrue();
        }

        // The boundary that makes loss distance-independent: once MaxWaylayAttemptsPerCourier (2) bands have
        // decided, the courier is through the gauntlet and no further band rolls, whatever the remaining
        // distance. Without this exact cutoff a far sender's letter keeps compounding losses band after band,
        // which is precisely the reported "almost every time when far" failure.
        [TestCase(2)]
        [TestCase(3)]
        public void GIVEN_the_cap_of_bands_has_rolled_WHEN_checking_the_budget_THEN_no_further_band_may_roll(int bandsRolled)
        {
            CourierWaylayPolicy.HasWaylayBudget(bandsRolled).Should().BeFalse();
        }
    }
}
