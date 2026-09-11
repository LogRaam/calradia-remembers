// Code written by Gabriel Mailhot, 24/08/2026.
// Intercepted-letter-leak Inc 1: LetterLeakPolicy decides whether a waylaid player letter leaks, and
// LetterLeakAntagonistPolicy decides who receives it. What breaks if wrong: a leak fires when the feature is off
// or on a trivial note (drama the player never opted into, or a "secret" that was one line of nothing), or it
// leaks to no one / the wrong sort of person. The feature ships OFF, so the enabled gate is load-bearing: it must
// never leak while disabled, whatever else is true.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterLeakPolicyTests
    {
        // The load-bearing off switch: the feature ships OFF, so nothing leaks while disabled even when every other
        // condition (bandit waylay, substantive letter, antagonist present, a rolling-zero roll) is met.
        [Test]
        public void GIVEN_the_feature_is_disabled_THEN_nothing_leaks_however_favourable_everything_else_is()
        {
            LetterLeakPolicy.ShouldLeak(new LetterLeakPolicy.LeakFacts {Enabled = false, ByBandit = true, HasSubstance = true, AntagonistAvailable = true, Chance = LetterLeakPolicy.DefaultLeakChance, Roll = 0.0}).Should().BeFalse();
        }

        // A letter simply lost on the road (no bandit) reaches no one, so it cannot leak: interception is the whole
        // premise. Guards against leaking on every courier despawn.
        [Test]
        public void GIVEN_the_loss_was_not_a_bandit_waylay_THEN_it_does_not_leak()
        {
            LetterLeakPolicy.ShouldLeak(new LetterLeakPolicy.LeakFacts {Enabled = true, ByBandit = false, HasSubstance = true, AntagonistAvailable = true, Chance = LetterLeakPolicy.DefaultLeakChance, Roll = 0.0}).Should().BeFalse();
        }

        // No substance, no secret: a one-line note has nothing worth exploiting, so it never leaks.
        [Test]
        public void GIVEN_the_letter_has_no_substance_THEN_it_does_not_leak()
        {
            LetterLeakPolicy.ShouldLeak(new LetterLeakPolicy.LeakFacts {Enabled = true, ByBandit = true, HasSubstance = false, AntagonistAvailable = true, Chance = LetterLeakPolicy.DefaultLeakChance, Roll = 0.0}).Should().BeFalse();
        }

        // No antagonist to receive it, no leak: a secret falls into the wrong hands only if such hands exist.
        [Test]
        public void GIVEN_no_antagonist_is_available_THEN_it_does_not_leak()
        {
            LetterLeakPolicy.ShouldLeak(new LetterLeakPolicy.LeakFacts {Enabled = true, ByBandit = true, HasSubstance = true, AntagonistAvailable = false, Chance = LetterLeakPolicy.DefaultLeakChance, Roll = 0.0}).Should().BeFalse();
        }

        // The chance boundary: a roll below the chance leaks, a roll at or above it does not (so the leak stays the
        // rare event the chance names, not a certainty).
        [Test]
        public void GIVEN_all_conditions_met_THEN_the_roll_decides_at_the_chance_boundary()
        {
            LetterLeakPolicy.ShouldLeak(new LetterLeakPolicy.LeakFacts {Enabled = true, ByBandit = true, HasSubstance = true, AntagonistAvailable = true, Chance = 0.35, Roll = 0.34}).Should().BeTrue();
            LetterLeakPolicy.ShouldLeak(new LetterLeakPolicy.LeakFacts {Enabled = true, ByBandit = true, HasSubstance = true, AntagonistAvailable = true, Chance = 0.35, Roll = 0.35}).Should().BeFalse();
            LetterLeakPolicy.ShouldLeak(new LetterLeakPolicy.LeakFacts {Enabled = true, ByBandit = true, HasSubstance = true, AntagonistAvailable = true, Chance = 0.35, Roll = 0.9}).Should().BeFalse();
        }

        // Substance heuristic: a short note is below the bar; a fuller letter clears it.
        [Test]
        public void GIVEN_content_length_THEN_substance_tracks_the_minimum()
        {
            LetterLeakPolicy.HasSubstance("Meet me at Sargot.").Should().BeFalse();
            LetterLeakPolicy.HasSubstance(null).Should().BeFalse();
            LetterLeakPolicy.HasSubstance("   ").Should().BeFalse();
            LetterLeakPolicy.HasSubstance("I mean to march on Pravend the moment the Vlandian host turns south; hold your oath.").Should().BeTrue();
        }

        // Antagonist choice: a grudge-holder (a personal enemy) is preferred over a merely hostile lord, since they
        // make the sharpest, most characterful use of the secret.
        [Test]
        public void GIVEN_both_a_grudge_holder_and_a_hostile_lord_THEN_the_grudge_holder_is_chosen()
        {
            LetterLeakAntagonistPolicy.Pick(new List<string> {"grudge_lord"}, new List<string> {"enemy_lord"})
                .Should().Be("grudge_lord");
        }

        // With no grudge-holder, a hostile lord receives it (the fallback the ratified design names).
        [Test]
        public void GIVEN_no_grudge_holder_THEN_a_hostile_lord_receives_the_letter()
        {
            LetterLeakAntagonistPolicy.Pick(new List<string>(), new List<string> {"enemy_lord"})
                .Should().Be("enemy_lord");
        }

        // No one hostile at all: no leak target, so the caller does not leak.
        [Test]
        public void GIVEN_neither_a_grudge_holder_nor_a_hostile_lord_THEN_no_antagonist_is_picked()
        {
            LetterLeakAntagonistPolicy.Pick(null, null).Should().BeNull();
            LetterLeakAntagonistPolicy.Pick(new List<string>(), new List<string>()).Should().BeNull();
        }

        // Blank ids (a stale/empty candidate) are skipped, not returned, so a leak never targets nobody-by-id.
        [Test]
        public void GIVEN_blank_candidate_ids_THEN_they_are_skipped()
        {
            LetterLeakAntagonistPolicy.Pick(new List<string> {"", null}, new List<string> {"", "enemy_lord"})
                .Should().Be("enemy_lord");
        }
    }
}
