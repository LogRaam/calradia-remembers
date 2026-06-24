// Code written by Gabriel Mailhot, 24/06/2026.
// Protects the conception policy: who (if anyone) becomes the mother of a vaginal act. The "same-sex /
// no fertile female → no child" rule and the fertile window are the whole guarantee — a drift would
// silently conceive impossible children or deny valid ones.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ConceptionResolverTests
    {
        private const double Fertile = 25d;

        // ── The two productive pairings ────────────────────────────────────────

        [Test]
        public void GIVEN_a_fertile_female_player_and_a_living_male_npc_WHEN_resolving_THEN_the_player_is_the_mother()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: true, playerIsAlive: true, playerAge: Fertile,
                    npcIsFemale: false, npcIsAlive: true, npcAge: 30d)
                .Should().Be(ConceptionRole.PlayerIsMother);
        }

        [Test]
        public void GIVEN_a_male_player_and_a_fertile_female_npc_WHEN_resolving_THEN_the_npc_is_the_mother()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: false, playerIsAlive: true, playerAge: 30d,
                    npcIsFemale: true, npcIsAlive: true, npcAge: Fertile)
                .Should().Be(ConceptionRole.NpcIsMother);
        }

        // ── Same-sex pairings produce no child ─────────────────────────────────

        [Test]
        public void GIVEN_two_females_WHEN_resolving_THEN_no_conception_for_lack_of_a_father()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: true, playerIsAlive: true, playerAge: Fertile,
                    npcIsFemale: true, npcIsAlive: true, npcAge: Fertile)
                .Should().Be(ConceptionRole.None);
        }

        [Test]
        public void GIVEN_two_males_WHEN_resolving_THEN_no_conception_for_lack_of_a_mother()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: false, playerIsAlive: true, playerAge: 30d,
                    npcIsFemale: false, npcIsAlive: true, npcAge: 30d)
                .Should().Be(ConceptionRole.None);
        }

        // ── Fertility window on the mother ─────────────────────────────────────

        [Test]
        public void GIVEN_a_female_player_below_the_fertile_window_WHEN_resolving_THEN_no_conception()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: true, playerIsAlive: true, playerAge: 17d,
                    npcIsFemale: false, npcIsAlive: true, npcAge: 30d)
                .Should().Be(ConceptionRole.None);
        }

        [Test]
        public void GIVEN_a_female_npc_above_the_fertile_window_WHEN_resolving_THEN_no_conception()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: false, playerIsAlive: true, playerAge: 30d,
                    npcIsFemale: true, npcIsAlive: true, npcAge: 46d)
                .Should().Be(ConceptionRole.None);
        }

        [Test]
        public void GIVEN_the_fertile_window_bounds_WHEN_checking_the_mother_THEN_eighteen_and_fortyfive_are_inclusive()
        {
            ConceptionResolver.IsFertileMother(true, true, 18d).Should().BeTrue();
            ConceptionResolver.IsFertileMother(true, true, 45d).Should().BeTrue();
            ConceptionResolver.IsFertileMother(true, true, 17.9d).Should().BeFalse();
            ConceptionResolver.IsFertileMother(true, true, 45.1d).Should().BeFalse();
        }

        // ── The dead conceive nothing ──────────────────────────────────────────

        [Test]
        public void GIVEN_a_dead_fertile_female_WHEN_resolving_THEN_no_conception()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: true, playerIsAlive: false, playerAge: Fertile,
                    npcIsFemale: false, npcIsAlive: true, npcAge: 30d)
                .Should().Be(ConceptionRole.None);
        }

        [Test]
        public void GIVEN_a_fertile_female_npc_and_a_dead_male_player_WHEN_resolving_THEN_no_conception_for_lack_of_a_living_father()
        {
            ConceptionResolver.Resolve(
                    playerIsFemale: false, playerIsAlive: false, playerAge: 30d,
                    npcIsFemale: true, npcIsAlive: true, npcAge: Fertile)
                .Should().Be(ConceptionRole.None);
        }
    }
}
