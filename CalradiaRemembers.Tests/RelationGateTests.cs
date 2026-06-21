// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the mod's core anti-exploit rule: you cannot talk your way to max relation. The caps,
// the warmth-dependent cooldown, the intimacy bypass, and the negative floors all break in silence —
// a regression here quietly defeats the whole "deeds over words" design.

#region

using CalradiaRemembers;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RelationGateTests
    {
        [Test]
        public void GIVEN_a_zero_delta_WHEN_resolving_THEN_nothing_moves()
        {
            //Act/Assert
            RelationGate.Resolve(0, Profile(), null, 100.0, npcIsCaptive: false).Should().Be(0);
        }

        [Test]
        public void GIVEN_a_generous_positive_proposal_WHEN_resolving_THEN_a_routine_gain_is_capped_to_one()
        {
            //Arrange — the LLM asks for +10; routine warmth never grants more than +1.
            NpcProfile p = Profile();

            //Act
            int granted = RelationGate.Resolve(10, p, null, 100.0, false, RelationWarmth.Normal);

            //Assert
            granted.Should().Be(1);
            p.LastRelationGainHour.Should().Be(100.0, "a granted gain stamps the cooldown clock");
        }

        [Test]
        public void GIVEN_a_routine_gain_within_the_cooldown_window_WHEN_resolving_THEN_it_is_gated_to_zero()
        {
            //Arrange — last gain at hour 100; another routine gain 3h later is still inside the 8h window.
            NpcProfile p = Profile(lastGainHour: 100.0);

            //Act/Assert
            RelationGate.Resolve(1, p, null, 103.0, false, RelationWarmth.Normal).Should().Be(0);
        }

        [Test]
        public void GIVEN_a_routine_gain_after_the_cooldown_window_WHEN_resolving_THEN_it_lands()
        {
            //Arrange — 9h after the last gain, past the 8h Normal window.
            NpcProfile p = Profile(lastGainHour: 100.0);

            //Act/Assert
            RelationGate.Resolve(1, p, null, 109.0, false, RelationWarmth.Normal).Should().Be(1);
        }

        [Test]
        public void GIVEN_an_intimacy_moment_inside_the_cooldown_WHEN_resolving_THEN_it_bypasses_the_throttle_and_grants_three()
        {
            //Arrange — a significant beat always counts, even right after a routine gain.
            NpcProfile p = Profile(lastGainHour: 100.0);

            //Act
            int granted = RelationGate.Resolve(10, p, NotableEventType.Intimacy, 101.0, false, RelationWarmth.Normal);

            //Assert — capped at the intimacy cap (3), and the clock is re-stamped.
            granted.Should().Be(3);
            p.LastRelationGainHour.Should().Be(101.0);
        }

        [Test]
        public void GIVEN_very_easy_warmth_WHEN_resolving_a_routine_gain_THEN_the_cap_rises_to_two_and_the_throttle_is_gone()
        {
            //Arrange — Very Easy removes the cooldown entirely and allows a +2 routine beat.
            NpcProfile p = Profile(lastGainHour: 100.0);

            //Act/Assert — even 0.5h after the last gain, a +2 lands.
            RelationGate.Resolve(5, p, null, 100.5, false, RelationWarmth.VeryEasy).Should().Be(2);
        }

        [Test]
        public void GIVEN_easier_warmth_WHEN_resolving_THEN_the_window_shortens_to_three_hours()
        {
            //Arrange — Easier uses a 3h window: 2h is too soon, 4h lands.
            NpcProfile tooSoon = Profile(lastGainHour: 100.0);
            NpcProfile longEnough = Profile(lastGainHour: 100.0);

            //Act/Assert
            RelationGate.Resolve(1, tooSoon, null, 102.0, false, RelationWarmth.Easier).Should().Be(0);
            RelationGate.Resolve(1, longEnough, null, 104.0, false, RelationWarmth.Easier).Should().Be(1);
        }

        [Test]
        public void GIVEN_a_moderate_insult_WHEN_resolving_THEN_the_full_negative_applies_with_no_cooldown()
        {
            //Arrange — negatives are never rate-limited.
            NpcProfile p = Profile(lastGainHour: 100.0);

            //Act/Assert
            RelationGate.Resolve(-7, p, null, 100.5, false, RelationWarmth.Normal).Should().Be(-7);
        }

        [Test]
        public void GIVEN_a_savage_insult_in_ordinary_conversation_WHEN_resolving_THEN_it_is_bounded_at_the_conversational_floor()
        {
            //Arrange — a -40 proposal cannot exceed the -15 conversational floor when the NPC is free.
            //Act/Assert
            RelationGate.Resolve(-40, Profile(), null, 100.0, npcIsCaptive: false).Should().Be(-15);
        }

        [Test]
        public void GIVEN_the_same_cruelty_toward_a_captive_WHEN_resolving_THEN_the_deeper_captive_floor_is_unlocked()
        {
            //Arrange — captivity is the verifiable extreme that lets regard collapse far further.
            //Act/Assert
            RelationGate.Resolve(-40, Profile(), null, 100.0, npcIsCaptive: true).Should().Be(-40);
        }

        [Test]
        public void GIVEN_an_extreme_proposal_against_a_captive_WHEN_resolving_THEN_it_still_stops_at_the_captive_floor()
        {
            //Act/Assert
            RelationGate.Resolve(-500, Profile(), null, 100.0, npcIsCaptive: true).Should().Be(-100);
        }

        [Test]
        public void GIVEN_a_null_profile_WHEN_resolving_THEN_nothing_moves()
        {
            //Act/Assert — fail safe rather than throw.
            RelationGate.Resolve(5, null, null, 100.0, false).Should().Be(0);
        }

        #region private

        private static NpcProfile Profile(double? lastGainHour = null)
            => new NpcProfile
            {
                Id      = "test_npc",
                Name    = "Test Npc",
                Clan    = "Test Clan",
                Faction = "Test Faction",
                LastRelationGainHour = lastGainHour
            };

        #endregion
    }
}
