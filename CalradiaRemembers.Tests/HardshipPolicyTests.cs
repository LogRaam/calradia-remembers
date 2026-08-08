// Code written by Gabriel Mailhot, 08/08/2026.
// Guards HardshipPolicy, the gate deciding whether a town's distress (a food shortage and/or collapsing
// prosperity) is worth recording as a Hardship WorldEvent (which then spreads as rumour, "what you've heard",
// and the chronicle). Its answer feeds the weekly world-conditions scan directly: a wrong "yes" fills the realm
// with phantom famines, a wrong "no" leaves a genuinely starving town's plight unspoken. Each boundary below pins
// one of those failures. Rationale derived from the policy's own header/docs.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class HardshipPolicyTests
    {
        // A town that is neither starving nor poor is simply doing fine; recording hardship for it would be a
        // verifiable lie (the player can open the town and read healthy prosperity). Both signals absent must refuse.
        [Test]
        public void GIVEN_no_food_shortage_and_healthy_prosperity_WHEN_deciding_THEN_it_is_not_recorded()
        {
            HardshipPolicy.ShouldRecord(foodShortage: false, prosperity: HardshipPolicy.ProsperityFloor, HardshipPolicy.ProsperityFloor, daysSinceLastForThisSettlement: null)
                .Should().BeFalse();
        }

        // A food shortage alone is real hardship even when the town is nominally prosperous (rations can run short
        // while coffers are still full). This pins that either signal qualifies on its own, so a famine in a rich
        // town is not silenced by its prosperity.
        [Test]
        public void GIVEN_a_food_shortage_despite_high_prosperity_WHEN_deciding_THEN_it_is_recorded()
        {
            HardshipPolicy.ShouldRecord(foodShortage: true, prosperity: HardshipPolicy.ProsperityFloor + 5000, HardshipPolicy.ProsperityFloor, daysSinceLastForThisSettlement: null)
                .Should().BeTrue();
        }

        // Collapsing prosperity alone is real hardship even with the granary full: trade drying up is its own misery.
        // Pins the second independent signal, so bleeding prosperity is not silenced by an adequate food stock.
        [Test]
        public void GIVEN_collapsed_prosperity_despite_adequate_food_WHEN_deciding_THEN_it_is_recorded()
        {
            HardshipPolicy.ShouldRecord(foodShortage: false, prosperity: HardshipPolicy.ProsperityFloor - 1, HardshipPolicy.ProsperityFloor, daysSinceLastForThisSettlement: null)
                .Should().BeTrue();
        }

        // The floor is EXCLUSIVE: prosperity exactly at the floor is still "not below" and, with no food shortage,
        // must NOT record. Pins the exact edge so a retune of ProsperityFloor cannot silently shift which towns
        // count as hard-hit by one point.
        [Test]
        public void GIVEN_prosperity_exactly_at_the_floor_and_no_shortage_WHEN_deciding_THEN_it_is_not_recorded()
        {
            HardshipPolicy.ShouldRecord(foodShortage: false, prosperity: HardshipPolicy.ProsperityFloor, HardshipPolicy.ProsperityFloor, daysSinceLastForThisSettlement: null)
                .Should().BeFalse();
        }

        // A town does not climb out of distress overnight; re-announcing the same town's misery every weekly pass
        // would be noise. A settlement inside the cooldown window must be held back even while still distressed.
        [Test]
        public void GIVEN_the_same_town_was_recorded_too_recently_WHEN_deciding_THEN_it_is_not_recorded()
        {
            HardshipPolicy.ShouldRecord(foodShortage: true, prosperity: 0, HardshipPolicy.ProsperityFloor, daysSinceLastForThisSettlement: HardshipPolicy.RerecordCooldownDays - 1)
                .Should().BeFalse();
        }

        // The cooldown is INCLUSIVE at its edge: once exactly RerecordCooldownDays have passed a still-distressed
        // town may surface again. Pins the re-fire boundary so a lingering hardship resumes being spoken of on schedule.
        [Test]
        public void GIVEN_the_cooldown_has_exactly_elapsed_and_still_distressed_WHEN_deciding_THEN_it_is_recorded()
        {
            HardshipPolicy.ShouldRecord(foodShortage: true, prosperity: 0, HardshipPolicy.ProsperityFloor, daysSinceLastForThisSettlement: HardshipPolicy.RerecordCooldownDays)
                .Should().BeTrue();
        }

        // A null "days since last" means this town has NEVER been recorded as hard-hit; it must be free to record
        // the first one, or a town poor from the campaign's start would never have its plight spoken of at all.
        [Test]
        public void GIVEN_the_town_was_never_recorded_before_WHEN_distressed_THEN_it_is_recorded()
        {
            HardshipPolicy.ShouldRecord(foodShortage: true, prosperity: 0, HardshipPolicy.ProsperityFloor, daysSinceLastForThisSettlement: null)
                .Should().BeTrue();
        }
    }
}
