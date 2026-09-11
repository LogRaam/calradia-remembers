// Code written by Gabriel Mailhot, 10/07/2026.
// Guards the eligibility and weekly-roll rules CompanionAffectionBehavior consults before a companion
// asks for a private word to confide love and become the player's SECRET LOVER (v1.30.3, "never a
// bargain, kept hidden to spare the household jealousy"). Wrongly eligible means an incompatible,
// unhappy, already-attached, or shallow-bond companion propositions the player out of nowhere; too
// strict means a genuinely deep, faithful bond never gets its payoff. The weekly-chance scaling and
// its hard cap decide how quickly that payoff arrives without ever feeling guaranteed.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionSecretAffectionPolicyTests
    {
        // The happy path: every guard satisfied must actually grant eligibility, or a companion who
        // has earned this (content, compatible, deeply bonded, unattached) could never confide at all.
        [Test]
        public void GIVEN_all_conditions_hold_WHEN_checking_eligibility_THEN_the_companion_could_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = true, AdultEnabled = true, IsContent = true, AlreadyAttached = false, Regard = 60}).Should().BeTrue();
        }

        // CompanionAffectionBehavior only iterates IsPlayerCompanion heroes, but this pins the same
        // rule in the pure policy: any random NPC with high regard must never be able to trigger a
        // secret-lover confession meant only for someone who has actually served in the party.
        [Test]
        public void GIVEN_the_hero_is_not_a_companion_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = false, OrientationCompatible = true, AdultEnabled = true, IsContent = true, AlreadyAttached = false, Regard = 60}).Should().BeFalse();
        }

        // Same class of bug this whole feature area guards elsewhere (jealousy's orientation gate,
        // v1.29.0/v1.30.3): a companion whose authored orientation is not compatible with the player
        // must never be offered a romantic confession, or the moment reads as nonsensical.
        [Test]
        public void GIVEN_an_incompatible_orientation_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = false, AdultEnabled = true, IsContent = true, AlreadyAttached = false, Regard = 60}).Should().BeFalse();
        }

        // Per the production header, this feature only fires "when adult content is enabled";
        // CompanionAffectionBehavior.OnWeeklyTick itself bails on AdultContentLevel.Off, but this
        // pins the same rule in the pure policy so it stays safe even if a future caller forgets it.
        [Test]
        public void GIVEN_adult_content_is_disabled_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = true, AdultEnabled = false, IsContent = true, AlreadyAttached = false, Regard = 60}).Should().BeFalse();
        }

        // The production header calls this "the positive counterpart to CompanionHappinessBehavior's
        // grievance/retirement audiences": an unhappy companion (CompanionHappiness.WantsToLeave)
        // belongs to that other, negative audience, never to a loving confession.
        [Test]
        public void GIVEN_the_companion_is_not_content_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = true, AdultEnabled = true, IsContent = false, AlreadyAttached = false, Regard = 60}).Should().BeFalse();
        }

        // A companion already a consort/secret-lover/committed/spouse has nothing new to confide;
        // without this guard the audience could re-fire on someone already bound to the player,
        // which the console-facing EligibilityOf reason ("already bound to you") exists to prevent.
        [Test]
        public void GIVEN_the_companion_is_already_attached_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = true, AdultEnabled = true, IsContent = true, AlreadyAttached = true, Regard = 60}).Should().BeFalse();
        }

        // Pins the MinRegard=50 boundary (one point below it) so a comrade bond that has not yet run
        // deep cannot trigger this; only a genuinely deep bond, not ordinary loyalty, earns the
        // confession.
        [Test]
        public void GIVEN_the_bond_is_below_the_minimum_regard_WHEN_checking_eligibility_THEN_they_cannot_confide()
        {
            CompanionSecretAffectionPolicy.IsEligible(new CompanionSecretAffectionPolicy.SecretAffectionFacts {IsCompanion = true, OrientationCompatible = true, AdultEnabled = true, IsContent = true, AlreadyAttached = false, Regard = 49}).Should().BeFalse();
        }

        // Pins the formula (regard - MinRegard) * 2 + 5 at both ends and its clamps: floor 5% right at
        // MinRegard so the confession is never a coin flip the moment eligibility opens, and the
        // MaxWeeklyChancePercent=40 ceiling so even a maxed-out bond stays a roll, never a guarantee.
        // regard=40 (below MinRegard) also clamps to the floor, since ShouldConfideThisWeek is only
        // ever called once IsEligible already passed, but the formula itself must not go negative.
        [TestCase(50, 5)]
        [TestCase(60, 25)]
        [TestCase(67, 39)]
        [TestCase(68, 40)]
        [TestCase(100, 40)]
        [TestCase(40, 5)]
        public void GIVEN_a_regard_WHEN_computing_the_weekly_chance_THEN_it_scales_and_clamps(int regard, int expected)
        {
            CompanionSecretAffectionPolicy.WeeklyChancePercent(regard).Should().Be(expected);
        }

        // CompanionAffectionBehavior.OnWeeklyTick feeds MBRandom.RandomInt(100) straight into this
        // roll each week; pins the roll is treated as "land in the bottom N%" (roll < chance), not
        // "top N%" or an off-by-one at the chance value itself, which would silently double or halve
        // the actual weekly odds a player experiences.
        [TestCase(0, true)]
        [TestCase(24, true)]
        [TestCase(25, false)]
        [TestCase(99, false)]
        public void GIVEN_regard_sixty_with_a_chance_of_twenty_five_WHEN_rolling_THEN_only_the_bottom_quarter_confides(int roll, bool confides)
        {
            CompanionSecretAffectionPolicy.ShouldConfideThisWeek(60, roll).Should().Be(confides);
        }

        // MBRandom.RandomInt(100) is always non-negative in practice, but this pins a defensive floor
        // so an unexpected negative input fails SAFE (no confession) rather than always passing the
        // `roll < chance` check.
        [Test]
        public void GIVEN_a_negative_roll_WHEN_deciding_THEN_the_companion_does_not_confide()
        {
            CompanionSecretAffectionPolicy.ShouldConfideThisWeek(60, -1).Should().BeFalse();
        }
    }
}
