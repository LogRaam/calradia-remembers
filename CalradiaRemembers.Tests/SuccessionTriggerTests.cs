// Code written by Gabriel Mailhot, 25/09/2026.
// Documents what counts as a succession. A succession is not a notice: it DELETES the predecessor's private history
// from every NPC's memory (SuccessionMemoryPolicy) and marks the rest inherited, for good. Wallace1067, 25/09/2026:
// switching the active character with The Original Player Switcher was read as a death each way, reframing 24 profiles
// twice while nobody had died. The states below are the engine's own (ApplyHeirSelectionAction, decompiled).

#region

using CalradiaRemembers.Logic.Dynasty;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SuccessionTriggerTests
    {
        // Gabriel (26/10/2026): the player leaves the house for a ruler and the heir takes it up. The old character
        // lives on, so his state alone reads as a switch; CR's own word must make it the succession it is, or the heir
        // carries his predecessor's romances.
        [Test]
        public void GIVEN_the_player_abdicated_for_a_ruler_WHEN_the_change_fires_THEN_it_is_a_succession_though_he_lives()
        {
            PlayerChangeKind kind = SuccessionTrigger.Classify(new PlayerChangeFacts {PredecessorAbdicated = true});

            kind.Should().Be(PlayerChangeKind.Abdication);
            SuccessionTrigger.IsSuccession(kind).Should().BeTrue();
        }

        // The report: the character the player switched away from is alive and well. Treating that as a death is
        // what erased real memories from a whole save, twice.
        [Test]
        public void GIVEN_another_mod_switches_the_player_to_a_living_character_WHEN_the_change_fires_THEN_it_is_no_succession()
        {
            PlayerChangeKind kind = SuccessionTrigger.Classify(new PlayerChangeFacts {PredecessorIsDead = false, PredecessorIsRetired = false});

            kind.Should().Be(PlayerChangeKind.Switch);
            SuccessionTrigger.IsSuccession(kind).Should().BeFalse();
        }

        // The case the feature was built for: vanilla kills the old hero before handing over, so he is dead here.
        [Test]
        public void GIVEN_the_player_died_and_an_heir_took_over_WHEN_the_change_fires_THEN_it_is_a_succession_by_death()
        {
            PlayerChangeKind kind = SuccessionTrigger.Classify(new PlayerChangeFacts {PredecessorIsDead = true});

            kind.Should().Be(PlayerChangeKind.Death);
            SuccessionTrigger.IsSuccession(kind).Should().BeTrue();
        }

        // Vanilla retirement disables the old hero rather than killing him. It is still a succession (an heir takes
        // up the clan), but it is not a death, and the chronicle used to say he died.
        [Test]
        public void GIVEN_the_player_retired_WHEN_the_change_fires_THEN_it_is_a_succession_but_not_a_death()
        {
            PlayerChangeKind kind = SuccessionTrigger.Classify(new PlayerChangeFacts {PredecessorIsRetired = true});

            kind.Should().Be(PlayerChangeKind.Retirement);
            SuccessionTrigger.IsSuccession(kind).Should().BeTrue();
        }
    }
}
