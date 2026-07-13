// Code written by Gabriel Mailhot, 20/06/2026.
// This is CalradiaRemembers.Logic's core anti-exploit rule: the LLM proposes a relation delta after every
// exchange, and RelationGate is the only thing standing between that proposal and the number
// BannerlordGameStateBridge actually applies to Hero relation. Get the caps or cooldown wrong and players can
// talk their way to max relation in a single sitting, the exact failure this mod was built to avoid in the
// genre. The knobs it pins: routine warmth capped to +1 per cooldown window (widened or removed by the
// RelationWarmth MCM setting), a self-evident intimacy beat allowed to bypass the cooldown for a bigger,
// one-off gain, and negatives left uncapped except for a floor (deeper still when the NPC is a verified
// captive). These tests are the only protection this silently-breaking rule has.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RelationGateTests
   {
      // The core exploit this whole gate exists to close: an LLM asked to play along can propose double-digit
      // gains from ordinary flattery. Capping to +1 (not merely "less than proposed") is what keeps trust
      // "gained slowly" instead of a single warm exchange buying real regard.
      [Test]
      public void GIVEN_a_generous_positive_proposal_WHEN_resolving_THEN_a_routine_gain_is_capped_to_one()
      {
         //Arrange — the LLM asks for +10; routine warmth never grants more than +1.
         NpcProfile p = Profile();

         //Act
         int granted = RelationGate.Resolve(10, p, null, 100.0, false);

         //Assert
         granted.Should().Be(1);
         p.LastRelationGainHour.Should().Be(100.0, "a granted gain stamps the cooldown clock");
      }

      // Negatives are the mirror design pillar: trust lost quickly means no rate limiting and no discount, an
      // insult costs its full value the instant it happens, unlike a compliment which is throttled and clamped.
      [Test]
      public void GIVEN_a_moderate_insult_WHEN_resolving_THEN_the_full_negative_applies_with_no_cooldown()
      {
         //Arrange — negatives are never rate-limited.
         NpcProfile p = Profile(100.0);

         //Act/Assert
         RelationGate.Resolve(-7, p, null, 100.5, false).Should().Be(-7);
      }

      // Fail-safe path: if the profile lookup ever misses (unknown or not-yet-tracked NPC), the gate must
      // return 0 rather than throw or let an unclamped delta through, since ExecuteChangeRelation calls this
      // on every single proposal.
      [Test]
      public void GIVEN_a_null_profile_WHEN_resolving_THEN_nothing_moves()
      {
         //Act/Assert — fail safe rather than throw.
         RelationGate.Resolve(5, null, null, 100.0, false).Should().Be(0);
      }

      // Confirms the cooldown is a WINDOW, not a permanent block: once 8h+ have passed, warmth is allowed to
      // register again, so an NPC does not cap out at +1 forever after a single early compliment.
      [Test]
      public void GIVEN_a_routine_gain_after_the_cooldown_window_WHEN_resolving_THEN_it_lands()
      {
         //Arrange — 9h after the last gain, past the 8h Normal window.
         NpcProfile p = Profile(100.0);

         //Act/Assert
         RelationGate.Resolve(1, p, null, 109.0, false).Should().Be(1);
      }

      // The throttle at the heart of the exploit fix: without it, a proposer could still get +1 every single
      // message and climb steadily, just slower. Gating repeat gains to 0 inside the window is what makes
      // "slowly" mean hours of game time, not seconds of chat.
      [Test]
      public void GIVEN_a_routine_gain_within_the_cooldown_window_WHEN_resolving_THEN_it_is_gated_to_zero()
      {
         //Arrange — last gain at hour 100; another routine gain 3h later is still inside the 8h window.
         NpcProfile p = Profile(100.0);

         //Act/Assert
         RelationGate.Resolve(1, p, null, 103.0, false).Should().Be(0);
      }

      // Pins the ordinary floor at -15: a single ugly exchange can sour things badly but not annihilate the
      // relation outright, that deeper collapse is reserved for a verified extreme (see the captive-floor
      // tests below).
      [Test]
      public void GIVEN_a_savage_insult_in_ordinary_conversation_WHEN_resolving_THEN_it_is_bounded_at_the_conversational_floor()
      {
         //Arrange — a -40 proposal cannot exceed the -15 conversational floor when the NPC is free.
         //Act/Assert
         RelationGate.Resolve(-40, Profile(), null, 100.0, false).Should().Be(-15);
      }

      // Guards the early return: a zero proposal must not stamp LastRelationGainHour, or a no-op exchange
      // would needlessly start the cooldown clock and block the NPC's next real gain.
      [Test]
      public void GIVEN_a_zero_delta_WHEN_resolving_THEN_nothing_moves()
      {
         //Act/Assert
         RelationGate.Resolve(0, Profile(), null, 100.0, false).Should().Be(0);
      }

      // Even the widened captive floor has a ceiling on cruelty: a wild -500 proposal must still stop at -100,
      // not pass through unclamped, or the LLM's arbitrary magnitude would leak straight into the relation
      // number.
      [Test]
      public void GIVEN_an_extreme_proposal_against_a_captive_WHEN_resolving_THEN_it_still_stops_at_the_captive_floor()
      {
         //Act/Assert
         RelationGate.Resolve(-500, Profile(), null, 100.0, true).Should().Be(-100);
      }

      // The one deliberate exception: intimacy is an engine-verified event (the scene actually happened), not
      // merely an LLM claim, so it is trusted to bypass the cooldown and land a bigger +3 even seconds after a
      // routine gain. Also re-stamps the clock, so it does not open a loophole for the very next routine gain.
      [Test]
      public void GIVEN_an_intimacy_moment_inside_the_cooldown_WHEN_resolving_THEN_it_bypasses_the_throttle_and_grants_three()
      {
         //Arrange — a significant beat always counts, even right after a routine gain.
         NpcProfile p = Profile(100.0);

         //Act
         int granted = RelationGate.Resolve(10, p, NotableEventType.Intimacy, 101.0, false);

         //Assert — capped at the intimacy cap (3), and the clock is re-stamped.
         granted.Should().Be(3);
         p.LastRelationGainHour.Should().Be(101.0);
      }

      // Pins the "Warm" MCM difficulty setting's actual effect: the cooldown window must genuinely shrink to
      // 3h, not just cosmetically relabel the same behaviour, so players who want faster-building
      // relationships get a real, measurable change.
      [Test]
      public void GIVEN_easier_warmth_WHEN_resolving_THEN_the_window_shortens_to_three_hours()
      {
         //Arrange — Easier uses a 3h window: 2h is too soon, 4h lands.
         NpcProfile tooSoon = Profile(100.0);
         NpcProfile longEnough = Profile(100.0);

         //Act/Assert
         RelationGate.Resolve(1, tooSoon, null, 102.0, false, RelationWarmth.Easier).Should().Be(0);
         RelationGate.Resolve(1, longEnough, null, 104.0, false, RelationWarmth.Easier).Should().Be(1);
      }

      // The same -40 that gets clamped to -15 in ordinary conversation lands in full here: the deeper floor
      // is gated on the ENGINE's captive status (GetPlayerStatus), not anything the LLM narrates, so a
      // proposal cannot unlock this by merely claiming the NPC is at the player's mercy.
      [Test]
      public void GIVEN_the_same_cruelty_toward_a_captive_WHEN_resolving_THEN_the_deeper_captive_floor_is_unlocked()
      {
         //Arrange — captivity is the verifiable extreme that lets regard collapse far further.
         //Act/Assert
         RelationGate.Resolve(-40, Profile(), null, 100.0, true).Should().Be(-40);
      }

      // Pins the most permissive difficulty rung ("Storyteller"): the cooldown is removed entirely, but even
      // here a cap still exists, +2 rather than the proposed +5, so the mod never lets a raw LLM delta through
      // completely unclamped, no matter how loosely the player has tuned relation warmth.
      [Test]
      public void GIVEN_very_easy_warmth_WHEN_resolving_a_routine_gain_THEN_the_cap_rises_to_two_and_the_throttle_is_gone()
      {
         //Arrange — Very Easy removes the cooldown entirely and allows a +2 routine beat.
         NpcProfile p = Profile(100.0);

         //Act/Assert — even 0.5h after the last gain, a +2 lands.
         RelationGate.Resolve(5, p, null, 100.5, false, RelationWarmth.VeryEasy).Should().Be(2);
      }

      #region private

      private static NpcProfile Profile(double? lastGainHour = null)
         => new() {
            Id = "test_npc",
            Name = "Test Npc",
            Clan = "Test Clan",
            Faction = "Test Faction",
            LastRelationGainHour = lastGainHour
         };

      #endregion
   }
}