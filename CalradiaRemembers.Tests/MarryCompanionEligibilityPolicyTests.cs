// Code written by Gabriel Mailhot, 11/08/2026.
// MarryCompanionEligibilityPolicy is the mod-side rule a future SameClan carve-out would consult before letting the
// player wed one of their own wanderer companions. The whole point of the carve-out is that a hired companion is NOT
// blood kin, so the incest custom the SameClan bar enforces does not reach them. These tests pin that the rule admits
// exactly the intended hero and NO other: a companion of compatible sex/orientation, adult, unwed, not blood kin,
// while the player can still take a spouse. If any refusal silently flipped, the carve-out could marry a blood
// relative, a second spouse onto an already-wed player, a child, or a corpse, which the SameClan bar was there to
// stop. Every clause is asserted alone so a reordered or added condition cannot slip through unclassified.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MarryCompanionEligibilityPolicyTests
    {
        /// <summary>
        ///   A companion the player could wed: theirs, living, grown, compatible, unwed, no kin, and the player
        ///   free to marry. Each test below spoils exactly one of those and says nothing about the other six.
        /// </summary>
        private static MarryCompanionEligibilityPolicy.MarryCompanionFacts AWillingCompanion()
            => new() {
                IsCompanion = true, IsAlive = true, IsAdult = true,
                OrientationCompatibleWithPlayer = true, PlayerCanTakeAnotherSpouse = true
            };

        /// <summary>That companion with one thing about them changed, which is what each rule below is.</summary>
        private static MarryCompanionEligibilityPolicy.MarryCompanionFacts Spoiled(
            System.Action<MarryCompanionEligibilityPolicy.MarryCompanionFacts> change)
        {
            MarryCompanionEligibilityPolicy.MarryCompanionFacts facts = AWillingCompanion();
            change(facts);

            return facts;
        }

        // The whole reason the carve-out exists: a companion of compatible sex/orientation, adult, unwed, not blood
        // kin, while the player is free to marry, is admitted. If this ever returned false the feature would be dead
        // on arrival and players would keep hitting the SameClan wall on the very companion they meant to wed.
        [Test]
        public void GIVEN_a_compatible_unwed_adult_companion_and_a_free_player_WHEN_asked_THEN_they_may_wed()
        {
            MarryCompanionEligibilityPolicy.CanMarry(AWillingCompanion()).Should().BeTrue();
        }

        // A hero who is NOT a companion is outside this carve-out entirely: the ordinary marriage gates decide their
        // case. The carve-out only ever relaxes SameClan for the one same-clan hero who is not kin, so a non-companion
        // must never be admitted through it.
        [Test]
        public void GIVEN_the_hero_is_not_a_companion_WHEN_asked_THEN_refused()
        {
            MarryCompanionEligibilityPolicy.CanMarry(Spoiled(c => { c.IsCompanion = false; })).Should().BeFalse();
        }

        // A dead companion cannot exchange vows. The engine's own suitability requires an active hero; the mod must
        // not offer the match on a corpse.
        [Test]
        public void GIVEN_a_dead_companion_WHEN_asked_THEN_refused()
        {
            MarryCompanionEligibilityPolicy.CanMarry(Spoiled(c => { c.IsAlive = false; })).Should().BeFalse();
        }

        // Under the minimum marriage age there is no marriage in law (the engine's MinimumMarriageAge gate). A
        // companion below it is refused however compatible otherwise, so the carve-out can never wed a child.
        [Test]
        public void GIVEN_an_underage_companion_WHEN_asked_THEN_refused()
        {
            MarryCompanionEligibilityPolicy.CanMarry(Spoiled(c => { c.IsAdult = false; })).Should().BeFalse();
        }

        // The engine marries opposite-sex only, and the mod defers to the player's own orientation besides. An
        // incompatible companion is refused so the carve-out never promises a union the engine would silently drop.
        [Test]
        public void GIVEN_an_orientation_incompatible_companion_WHEN_asked_THEN_refused()
        {
            MarryCompanionEligibilityPolicy.CanMarry(Spoiled(c => { c.OrientationCompatibleWithPlayer = false; })).Should().BeFalse();
        }

        // A companion already married to someone else is refused: no one is wed twice. This mirrors the engine's own
        // Spouse == null precondition on both parties.
        [Test]
        public void GIVEN_an_already_wed_companion_WHEN_asked_THEN_refused()
        {
            MarryCompanionEligibilityPolicy.CanMarry(Spoiled(c => { c.AlreadyWed = true; })).Should().BeFalse();
        }

        // The one same-clan hero the SameClan bar must ALWAYS keep barring: a blood relative. A companion who is also
        // kin (a rare seeded case, or an adopted-then-related hero) stays refused, so relaxing SameClan for companions
        // never opens a door to incest, the very thing the bar was written to forbid.
        [Test]
        public void GIVEN_a_companion_who_is_blood_kin_WHEN_asked_THEN_refused()
        {
            MarryCompanionEligibilityPolicy.CanMarry(Spoiled(c => { c.IsBloodKinOfPlayer = true; })).Should().BeFalse();
        }

        // A player who already has a spouse cannot take a second: no second marriage exists in law
        // (MarriageBlockReason.PlayerAlreadyWed). The carve-out defers to that absolute bar rather than around it.
        [Test]
        public void GIVEN_the_player_cannot_take_another_spouse_WHEN_asked_THEN_refused()
        {
            MarryCompanionEligibilityPolicy.CanMarry(Spoiled(c => { c.PlayerCanTakeAnotherSpouse = false; })).Should().BeFalse();
        }
    }
}
