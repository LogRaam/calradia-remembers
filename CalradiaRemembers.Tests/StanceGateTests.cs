// Code written by Gabriel Mailhot, 21/06/2026.
// Protects "words nudge, deeds prove": the heart of the stance system's anti-exploit guarantee. The
// word cap + cooldown and the much larger deed cap break in silence — a drift lets a player talk their
// way to any posture, or makes proven deeds count for nothing.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StanceGateTests
    {
        // ── ClampAxis ──────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_bipolar_axis_WHEN_clamping_THEN_it_is_held_within_minus_hundred_and_hundred()
        {
            StanceGate.ClampAxis(StanceAxis.Trust, 250).Should().Be(100);
            StanceGate.ClampAxis(StanceAxis.Respect, -250).Should().Be(-100);
        }

        [Test]
        public void GIVEN_the_fear_axis_WHEN_clamping_THEN_it_is_unipolar_zero_to_hundred()
        {
            StanceGate.ClampAxis(StanceAxis.Fear, -30).Should().Be(0);
            StanceGate.ClampAxis(StanceAxis.Fear, 150).Should().Be(100);
        }

        // ── Deeds ──────────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_proven_deed_WHEN_applied_THEN_it_moves_the_axis_strongly()
        {
            //Arrange
            NpcStance s = new NpcStance { Trust = 10 };

            //Act — a kept promise.
            StanceGate.ApplyDeed(s, StanceAxis.Trust, 25);

            //Assert
            s.Trust.Should().Be(35);
        }

        [Test]
        public void GIVEN_an_outsized_deed_delta_WHEN_applied_THEN_it_is_bounded_by_the_deed_cap()
        {
            //Arrange
            NpcStance s = new NpcStance();

            //Act — a single deed cannot exceed the deed cap.
            StanceGate.ApplyDeed(s, StanceAxis.Respect, 500);

            //Assert
            s.Respect.Should().Be(StanceGate.DeedCap);
        }

        [Test]
        public void GIVEN_a_deed_pushing_past_the_axis_limit_WHEN_applied_THEN_it_clamps()
        {
            //Arrange
            NpcStance s = new NpcStance { Fear = 90 };

            //Act
            StanceGate.ApplyDeed(s, StanceAxis.Fear, 30);

            //Assert
            s.Fear.Should().Be(100);
        }

        [Test]
        public void GIVEN_a_deed_WHEN_applied_THEN_it_ignores_the_word_cooldown()
        {
            //Arrange — a word round just happened.
            NpcStance s = new NpcStance { LastWordInfluenceHour = 100.0, Respect = 0 };

            //Act — a deed moments later still lands in full.
            StanceGate.ApplyDeed(s, StanceAxis.Respect, 20);

            //Assert
            s.Respect.Should().Be(20);
        }

        // ── Words ──────────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_persuasive_speech_WHEN_a_word_round_is_applied_THEN_each_axis_moves_only_within_the_word_cap()
        {
            //Arrange — the LLM proposes big spoken shifts.
            NpcStance s = new NpcStance();

            //Act
            bool applied = StanceGate.TryApplyWordRound(s, new StanceShift { Trust = 10, Respect = 10, Fear = 10 }, 100.0);

            //Assert — words only colour: each is capped.
            applied.Should().BeTrue();
            s.Trust.Should().Be(StanceGate.WordCap);
            s.Respect.Should().Be(StanceGate.WordCap);
            s.Fear.Should().Be(StanceGate.WordCap);
            s.LastWordInfluenceHour.Should().Be(100.0);
        }

        [Test]
        public void GIVEN_a_recent_word_round_WHEN_speaking_again_within_the_cooldown_THEN_it_is_refused()
        {
            //Arrange
            NpcStance s = new NpcStance { LastWordInfluenceHour = 100.0, Trust = 2 };

            //Act — 3h later, inside the 8h window.
            bool applied = StanceGate.TryApplyWordRound(s, new StanceShift { Trust = 2 }, 103.0);

            //Assert — no grinding the same flattery.
            applied.Should().BeFalse();
            s.Trust.Should().Be(2);
        }

        [Test]
        public void GIVEN_the_cooldown_has_passed_WHEN_speaking_again_THEN_a_new_round_lands()
        {
            //Arrange
            NpcStance s = new NpcStance { LastWordInfluenceHour = 100.0, Trust = 2 };

            //Act — 9h later, past the window.
            bool applied = StanceGate.TryApplyWordRound(s, new StanceShift { Trust = 2 }, 109.0);

            //Assert
            applied.Should().BeTrue();
            s.Trust.Should().Be(4);
            s.LastWordInfluenceHour.Should().Be(109.0);
        }

        [Test]
        public void GIVEN_a_word_round_that_says_nothing_WHEN_applied_THEN_it_is_a_no_op_and_does_not_start_the_cooldown()
        {
            //Arrange
            NpcStance s = new NpcStance();

            //Act
            bool applied = StanceGate.TryApplyWordRound(s, new StanceShift(), 100.0);

            //Assert
            applied.Should().BeFalse();
            s.LastWordInfluenceHour.Should().BeNull();
        }

        [Test]
        public void GIVEN_words_against_a_settled_judgement_WHEN_compared_to_a_deed_THEN_the_deed_dwarfs_them()
        {
            //Arrange — the same goal pursued by talk vs by proof.
            NpcStance byWords = new NpcStance();
            NpcStance byDeed = new NpcStance();

            //Act
            StanceGate.TryApplyWordRound(byWords, new StanceShift { Respect = 30 }, 100.0);
            StanceGate.ApplyDeed(byDeed, StanceAxis.Respect, 30);

            //Assert — words moved 2, the deed moved 30.
            byWords.Respect.Should().Be(StanceGate.WordCap);
            byDeed.Respect.Should().Be(30);
            byDeed.Respect.Should().BeGreaterThan(byWords.Respect);
        }

        // ── Fear decay ─────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_time_passing_WHEN_fear_decays_THEN_it_fades_toward_calm()
        {
            //Arrange
            NpcStance s = new NpcStance { Fear = 60 };

            //Act — five days.
            StanceGate.ApplyFearDecay(s, 5);

            //Assert — 60 - 2*5.
            s.Fear.Should().Be(50);
        }

        [Test]
        public void GIVEN_long_calm_WHEN_fear_decays_THEN_it_never_falls_below_zero()
        {
            NpcStance s = new NpcStance { Fear = 6 };
            StanceGate.ApplyFearDecay(s, 10);
            s.Fear.Should().Be(0);
        }

        [Test]
        public void GIVEN_no_fear_or_no_days_WHEN_decaying_THEN_nothing_happens()
        {
            NpcStance calm = new NpcStance { Fear = 0 };
            NpcStance afraid = new NpcStance { Fear = 40 };

            StanceGate.ApplyFearDecay(calm, 5);
            StanceGate.ApplyFearDecay(afraid, 0);

            calm.Fear.Should().Be(0);
            afraid.Fear.Should().Be(40);
        }
    }
}
