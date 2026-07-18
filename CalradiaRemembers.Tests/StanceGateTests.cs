// Code written by Gabriel Mailhot, 21/06/2026.
// Moves the Trust/Respect/Fear axes that StanceGatePolicy later gates real actions on: prisoner-release
// coercion (Fear), lending troops/influence (Trust), joining the player's clan (Respect). The caps here
// are the WHOLE anti-exploit guarantee for that machinery: WordCap keeps a single chat exchange from
// ever reaching a gate threshold on its own (per the ROADMAP's own math, walking Fear to 40 by words
// alone would take ~20 accepted rounds over ~160 in-game hours), while DeedCap lets one verified deed
// move a stance decisively. Get a cap, the cooldown, or a clamp bound wrong here, and the LLM can talk
// its way past a gate meant to require proof.

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

      // The hard ceiling/floor every StanceGatePolicy threshold is read against. Let this clamp slip and
      // a stance could drift past +-100, silently out-ranking (or under-ranking) every gate that assumes
      // the axis is bounded.
      [Test]
      public void GIVEN_a_bipolar_axis_WHEN_clamping_THEN_it_is_held_within_minus_hundred_and_hundred()
      {
         StanceGate.ClampAxis(StanceAxis.Trust, 250).Should().Be(100);
         StanceGate.ClampAxis(StanceAxis.Respect, -250).Should().Be(-100);
      }

      // A deed landing near the ceiling must clamp, not overflow past 100: a coercion gate checking
      // "Fear >= YieldFearFloor" only ever needs to know the axis reached the cap, never by how much a
      // bug overshot it.
      [Test]
      public void GIVEN_a_deed_pushing_past_the_axis_limit_WHEN_applied_THEN_it_clamps()
      {
         //Arrange
         var s = new NpcStance {Fear = 90};

         //Act
         StanceGate.ApplyDeed(s, StanceAxis.Fear, 30);

         //Assert
         s.Fear.Should().Be(100);
      }

      // The "deeds prove" half of the file header's rule: a deed moments after a word round must still
      // land in full. Conflating the two cooldowns would let flattery indirectly gate a real, verified
      // deed (e.g. a kept promise arriving right after some chat) instead of applying it outright.
      [Test]
      public void GIVEN_a_deed_WHEN_applied_THEN_it_ignores_the_word_cooldown()
      {
         //Arrange — a word round just happened.
         var s = new NpcStance {LastWordInfluenceHour = 100.0, Respect = 0};

         //Act — a deed moments later still lands in full.
         StanceGate.ApplyDeed(s, StanceAxis.Respect, 20);

         //Assert
         s.Respect.Should().Be(20);
      }

      // ── Words ──────────────────────────────────────────────────────────────

      // The anti-flattery guarantee itself (ROADMAP-ratified 2026-07-04): however large the LLM's
      // proposed spoken shift, each axis is capped to WordCap (2). This is what keeps a gate like
      // "Fear >= 40" out of reach of chat alone; reaching it by words would take ~20 accepted rounds
      // over ~160 in-game hours, and Fear decays underneath the whole time.
      [Test]
      public void GIVEN_a_persuasive_speech_WHEN_a_word_round_is_applied_THEN_each_axis_moves_only_within_the_word_cap()
      {
         //Arrange — the LLM proposes big spoken shifts.
         var s = new NpcStance();

         //Act
         bool applied = StanceGate.TryApplyWordRound(s, new StanceShift {Trust = 10, Respect = 10, Fear = 10}, 100.0);

         //Assert — words only colour: each is capped.
         applied.Should().BeTrue();
         s.Trust.Should().Be(StanceGate.WordCap);
         s.Respect.Should().Be(StanceGate.WordCap);
         s.Fear.Should().Be(StanceGate.WordCap);
         s.LastWordInfluenceHour.Should().Be(100.0);
      }

      // ── Deeds ──────────────────────────────────────────────────────────────

      // Confirms a deed applies its FULL delta, not capped down to WordCap, as long as it stays inside
      // DeedCap: the "strong, because there is proof" half of the rule, and what a gate is actually
      // meant to respond to.
      [Test]
      public void GIVEN_a_proven_deed_WHEN_applied_THEN_it_moves_the_axis_strongly()
      {
         //Arrange
         var s = new NpcStance {Trust = 10};

         //Act — a kept promise.
         StanceGate.ApplyDeed(s, StanceAxis.Trust, 25);

         //Assert
         s.Trust.Should().Be(35);
      }

      // Rate limiting: without it, repeating the same flattery every exchange could grind a stance
      // upward turn after turn. The cooldown (matches RelationGate's 8h) is the second half of the
      // anti-exploit guarantee alongside WordCap.
      [Test]
      public void GIVEN_a_recent_word_round_WHEN_speaking_again_within_the_cooldown_THEN_it_is_refused()
      {
         //Arrange
         var s = new NpcStance {LastWordInfluenceHour = 100.0, Trust = 2};

         //Act — 3h later, inside the 8h window.
         bool applied = StanceGate.TryApplyWordRound(s, new StanceShift {Trust = 2}, 103.0);

         //Assert — no grinding the same flattery.
         applied.Should().BeFalse();
         s.Trust.Should().Be(2);
      }

      // An all-zero shift must not consume a cooldown slot: otherwise an LLM turn that says nothing
      // stance-relevant would still block the NEXT exchange's genuine word round for a full 8 in-game
      // hours.
      [Test]
      public void GIVEN_a_word_round_that_says_nothing_WHEN_applied_THEN_it_is_a_no_op_and_does_not_start_the_cooldown()
      {
         //Arrange
         var s = new NpcStance();

         //Act
         bool applied = StanceGate.TryApplyWordRound(s, new StanceShift(), 100.0);

         //Assert
         applied.Should().BeFalse();
         s.LastWordInfluenceHour.Should().BeNull();
      }

      // Even a "strong, because there is proof" deed has a ceiling (30): without it, a single mistuned
      // caller (or a bridge feeding a raw, unvalidated delta) could swing a stance from neutral straight
      // past every gate threshold in one call.
      [Test]
      public void GIVEN_an_outsized_deed_delta_WHEN_applied_THEN_it_is_bounded_by_the_deed_cap()
      {
         //Arrange
         var s = new NpcStance();

         //Act — a single deed cannot exceed the deed cap.
         StanceGate.ApplyDeed(s, StanceAxis.Respect, 500);

         //Assert
         s.Respect.Should().Be(StanceGate.DeedCap);
      }

      // Fear is unipolar (0 = unafraid, per NpcStance's doc): without this floor, a long stretch of calm
      // would drive it negative, and any gate written as a plain "Fear <= X" calm-check would be
      // comparing against values the axis was never designed to hold.
      [Test]
      public void GIVEN_long_calm_WHEN_fear_decays_THEN_it_never_falls_below_zero()
      {
         var s = new NpcStance {Fear = 6};
         StanceGate.ApplyFearDecay(s, 10);
         s.Fear.Should().Be(0);
      }

      // Two guard clauses in one call: an already-calm NPC and a zero-day tick must both be true no-ops,
      // so decay never nudges a fully calm stance negative, and never fires on a same-day call (e.g. an
      // event that passes 0 by construction rather than skipping the call).
      [Test]
      public void GIVEN_no_fear_or_no_days_WHEN_decaying_THEN_nothing_happens()
      {
         var calm = new NpcStance {Fear = 0};
         var afraid = new NpcStance {Fear = 40};

         StanceGate.ApplyFearDecay(calm, 5);
         StanceGate.ApplyFearDecay(afraid, 0);

         calm.Fear.Should().Be(0);
         afraid.Fear.Should().Be(40);
      }

      // M-S2 (regard audit): Trust and Respect must FADE toward zero without renewal, or a standing earned once
      // is a permanent gate. Both a positive and a negative axis step one toward zero, so an old favour AND an old
      // slight both lapse over time; a zero axis stays put (no drift into the negative).
      [Test]
      public void GIVEN_standing_on_both_sides_of_zero_WHEN_decaying_THEN_each_steps_one_toward_zero()
      {
         var s = new NpcStance {Trust = 40, Respect = -30, Fear = 50};

         bool moved = StanceGate.ApplyStandingDecay(s);

         moved.Should().BeTrue();
         s.Trust.Should().Be(39, "a positive standing fades one step toward zero");
         s.Respect.Should().Be(-29, "a negative standing also fades one step toward zero");
         s.Fear.Should().Be(50, "standing decay never touches fear (fear has its own, faster fade)");
      }

      // Guards the return value the daily sweep relies on to persist only what changed: a stance already at zero
      // on both standing axes must report no movement, so a neutral NPC is not re-serialized every interval.
      [Test]
      public void GIVEN_trust_and_respect_already_zero_WHEN_decaying_THEN_nothing_moves()
      {
         var s = new NpcStance {Trust = 0, Respect = 0, Fear = 20};

         StanceGate.ApplyStandingDecay(s).Should().BeFalse();
      }

      // The other side of the cooldown gate (pairs with the refused-within-cooldown test above): confirms
      // the window actually reopens and LastWordInfluenceHour is restamped on the accepted round. A stale
      // stamp here would either permanently lock out future word rounds or let them fire every tick.
      [Test]
      public void GIVEN_the_cooldown_has_passed_WHEN_speaking_again_THEN_a_new_round_lands()
      {
         //Arrange
         var s = new NpcStance {LastWordInfluenceHour = 100.0, Trust = 2};

         //Act — 9h later, past the window.
         bool applied = StanceGate.TryApplyWordRound(s, new StanceShift {Trust = 2}, 109.0);

         //Assert
         applied.Should().BeTrue();
         s.Trust.Should().Be(4);
         s.LastWordInfluenceHour.Should().Be(109.0);
      }

      // Fear is the one unipolar axis (0..100, per NpcStance's doc): a negative Fear would be meaningless
      // both to StanceDescriptor's prompt text and to any "Fear >= threshold" gate, so ClampAxis must
      // floor it at zero instead of mirroring the -100 floor the other three (bipolar) axes use.
      [Test]
      public void GIVEN_the_fear_axis_WHEN_clamping_THEN_it_is_unipolar_zero_to_hundred()
      {
         StanceGate.ClampAxis(StanceAxis.Fear, -30).Should().Be(0);
         StanceGate.ClampAxis(StanceAxis.Fear, 150).Should().Be(100);
      }

      // ── Fear decay ─────────────────────────────────────────────────────────

      // Pins the actual decay rate (FearDecayPerDay=2/day): this is what lets a coercion-gated captive's
      // Fear soften back to normal once the threat has receded, instead of staying pinned at a one-time
      // spike (and the gated terms it unlocked) forever.
      [Test]
      public void GIVEN_time_passing_WHEN_fear_decays_THEN_it_fades_toward_calm()
      {
         //Arrange
         var s = new NpcStance {Fear = 60};

         //Act — five days.
         StanceGate.ApplyFearDecay(s, 5);

         //Assert — 60 - 2*5.
         s.Fear.Should().Be(50);
      }

      // The whole "words nudge, deeds prove" philosophy made concrete in one comparison: the same goal
      // (Respect +30) pursued by talk lands at 2, pursued by proof lands at the full 30. This is the
      // number a player report of "I just talked my way past a gate" would come back to.
      [Test]
      public void GIVEN_words_against_a_settled_judgement_WHEN_compared_to_a_deed_THEN_the_deed_dwarfs_them()
      {
         //Arrange — the same goal pursued by talk vs by proof.
         var byWords = new NpcStance();
         var byDeed = new NpcStance();

         //Act
         StanceGate.TryApplyWordRound(byWords, new StanceShift {Respect = 30}, 100.0);
         StanceGate.ApplyDeed(byDeed, StanceAxis.Respect, 30);

         //Assert — words moved 2, the deed moved 30.
         byWords.Respect.Should().Be(StanceGate.WordCap);
         byDeed.Respect.Should().Be(30);
         byDeed.Respect.Should().BeGreaterThan(byWords.Respect);
      }
   }
}