// Code written by Gabriel Mailhot, 20/11/2026.
// ScreenButtonClaimPolicy: which mod may put its own screen behind a Balance of Power button (Diplomacy, History,
// Rumors & Events). The Bellum Civile bridge (Discord, 20/11/2026) asked for a way to open its diplomacy and world-events
// screens from those buttons without Harmony-patching CR, with "Available Soon" kept when nothing is registered. If this
// is wrong, two mods take turns over one button by load order, a mod clears another's screen, or a button opens nothing.

#region

using CalradiaRemembers.Logic.Extension;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScreenButtonClaimPolicyTests
    {
        // STAKE: the bridge's diplomacy screen opens from the Diplomacy button that said "Available Soon".
        [Test]
        public void GIVEN_a_free_button_WHEN_a_mod_claims_it_with_a_screen_THEN_it_is_that_mods()
        {
            ScreenButtonClaimPolicy.Decide(new ScreenButtonClaimFacts {Claimant = "Bellum Bridge", HasScreen = true}).Should().Be(ScreenButtonClaim.Taken);
        }

        // STAKE: a second mod installed alongside never steals the button, so what it opens does not depend on load order.
        [Test]
        public void GIVEN_a_button_held_by_one_mod_WHEN_another_claims_it_THEN_the_claim_is_refused()
        {
            ScreenButtonClaimPolicy.Decide(new ScreenButtonClaimFacts {Holder = "Bellum Bridge", Claimant = "Other Mod", HasScreen = true}).Should().Be(ScreenButtonClaim.Refused);
        }

        // STAKE: the holder can swap its own screen (a reload, a new version of its window) without letting go first.
        [Test]
        public void GIVEN_a_button_held_by_a_mod_WHEN_the_same_mod_claims_it_again_THEN_its_screen_is_replaced()
        {
            ScreenButtonClaimPolicy.Decide(new ScreenButtonClaimFacts {Holder = "Bellum Bridge", Claimant = "bellum bridge", HasScreen = true}).Should().Be(ScreenButtonClaim.Replaced);
        }

        // STAKE: a claim with nothing to open, or from no named mod, would leave a dead button no one could free.
        [Test]
        public void GIVEN_a_claim_without_a_screen_or_a_name_WHEN_decided_THEN_it_is_refused()
        {
            ScreenButtonClaimPolicy.Decide(new ScreenButtonClaimFacts {Claimant = "Bellum Bridge", HasScreen = false}).Should().Be(ScreenButtonClaim.Refused);
            ScreenButtonClaimPolicy.Decide(new ScreenButtonClaimFacts {Claimant = " ", HasScreen = true}).Should().Be(ScreenButtonClaim.Refused);
        }

        // STAKE: only the holder can give the button back to "Available Soon"; another mod cannot clear it.
        [Test]
        public void GIVEN_a_held_button_WHEN_a_mod_asks_to_release_it_THEN_only_the_holder_may()
        {
            ScreenButtonClaimPolicy.MayRelease("Bellum Bridge", "Bellum Bridge").Should().BeTrue();
            ScreenButtonClaimPolicy.MayRelease("Bellum Bridge", "Other Mod").Should().BeFalse();
            ScreenButtonClaimPolicy.MayRelease(null, "Bellum Bridge").Should().BeFalse();
        }
    }
}
