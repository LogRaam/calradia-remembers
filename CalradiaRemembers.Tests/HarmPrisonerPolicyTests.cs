// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.2 resilience hardening (prisoner/gold audit, finding 1). This policy is the single gate between an LLM's
// harm_prisoner [ACTION] and a REAL loss of the player's hit points. Before it, the executor deducted health with
// only a per-scene cap and a floor, never re-checking that the player was genuinely a captor's prisoner inside a
// live captive scene, and it fired regardless of the Hardcore content floor the feature is advertised behind.
// What breaks if this is wrong: too loose, a stray or premature emission wounds the player during an ordinary
// conversation (or below the advertised content level); too strict, a legitimate captive scene lands no toll.
// The bridge re-validates every guard here ("the prompt is advice, the bridge is law").

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class HarmPrisonerPolicyTests
    {
        // STAKE: harm_prisoner deducts real health. If the player is not actually this captor's prisoner, a
        // stray emission during an ordinary conversation must never wound them (the core correctness bug this
        // fix closes).
        [Test]
        public void GIVEN_the_player_is_not_the_captors_prisoner_WHEN_the_other_guards_hold_THEN_harm_is_refused()
        {
            HarmPrisonerPolicy.CanHarm(new HarmPrisonerPolicy.HarmFacts {PlayerIsCaptiveOfCaptor = false, CaptiveSceneActive = true, Hardcore = true})
                .Should().BeFalse();
        }

        // STAKE: a player can be a genuine prisoner yet NOT in a running captive scene (ordinary captivity
        // conversation). Health may only be lost while a scene is live; without this a late emission after the
        // scene closed would still bite.
        [Test]
        public void GIVEN_no_captive_scene_is_active_WHEN_the_other_guards_hold_THEN_harm_is_refused()
        {
            HarmPrisonerPolicy.CanHarm(new HarmPrisonerPolicy.HarmFacts {PlayerIsCaptiveOfCaptor = true, CaptiveSceneActive = false, Hardcore = true})
                .Should().BeFalse();
        }

        // STAKE: harm_prisoner is advertised Hardcore-only and the rest of the captive-scene vocabulary is gated
        // on Hardcore; the executor used to contradict that and wound the player at any content level.
        [Test]
        public void GIVEN_content_level_is_below_Hardcore_WHEN_the_other_guards_hold_THEN_harm_is_refused()
        {
            HarmPrisonerPolicy.CanHarm(new HarmPrisonerPolicy.HarmFacts {PlayerIsCaptiveOfCaptor = true, CaptiveSceneActive = true, Hardcore = false})
                .Should().BeFalse();
        }

        // STAKE: only when the player is genuinely this captor's prisoner, inside a live scene, at the Hardcore
        // floor does the narrated violence become a real toll.
        [Test]
        public void GIVEN_every_guard_holds_THEN_harm_is_permitted()
        {
            HarmPrisonerPolicy.CanHarm(new HarmPrisonerPolicy.HarmFacts {PlayerIsCaptiveOfCaptor = true, CaptiveSceneActive = true, Hardcore = true})
                .Should().BeTrue();
        }

        // STAKE: exhaustive combination coverage (the full 2^3 truth table). This is a pure AND across three
        // booleans and a real-health deduction tolerates no untested corner: every combination but all-true
        // must refuse.
        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(true, false, true, false)]
        [TestCase(false, true, true, false)]
        [TestCase(true, true, true, true)]
        public void GIVEN_every_combination_of_the_three_guards_THEN_only_all_true_permits_harm(
            bool playerIsCaptiveOfCaptor, bool captiveSceneActive, bool hardcore, bool expected)
        {
            HarmPrisonerPolicy.CanHarm(new HarmPrisonerPolicy.HarmFacts {PlayerIsCaptiveOfCaptor = playerIsCaptiveOfCaptor, CaptiveSceneActive = captiveSceneActive, Hardcore = hardcore}).Should().Be(expected);
        }
    }
}
