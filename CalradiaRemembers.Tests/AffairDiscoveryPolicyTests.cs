// Code written by Gabriel Mailhot, 10/08/2026.
// Guards AffairDiscoveryPolicy (Inc 3b-a): whether a SECRET affair is exposed to the player on a given tryst.
// The chance climbs with the tryst count (a hidden affair leaks the more it is lived) and is capped so exposure
// is never a certainty in one week. An OPEN lover is never discovered here (he is already known), and an affair
// already known never re-fires. On a discovery the mod raises the scandal and sets AffairDiscovered, which the
// confrontation increment (3b-b) gates on. Get the curve wrong and the affair is exposed on the first meeting
// (no suspense) or never (the jealousy beat is dead); drop the open/already-known guards and an open lover
// would be "discovered" as a betrayal, or the scandal would fire every week forever.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AffairDiscoveryPolicyTests
    {
        // The whole design intent: the more trysts an affair has seen, the likelier it leaks. If the chance did
        // not rise, a hidden affair would carry the same exposure risk on its hundredth meeting as its first,
        // and "it leaks over time" would not be modelled at all.
        [Test]
        public void GIVEN_more_trysts_WHEN_computing_the_discovery_chance_THEN_it_rises()
        {
            int first = AffairDiscoveryPolicy.ChancePercent(trystCount: 1);
            int several = AffairDiscoveryPolicy.ChancePercent(trystCount: 4);
            int many = AffairDiscoveryPolicy.ChancePercent(trystCount: 8);

            first.Should().Be(AffairDiscoveryPolicy.BaseChancePercent, "the first tryst carries only the base leak chance");
            several.Should().BeGreaterThan(first, "each further tryst raises the chance the affair is exposed");
            many.Should().BeGreaterThan(several, "the longer the affair runs, the likelier it leaks");
        }

        // Before any tryst there is nothing to leak: an affair just formed but never lived carries zero
        // discovery chance. Without this the very act of taking a secret lover could expose it the same week,
        // with no meeting to leak, collapsing the suspense the increment is built around.
        [Test]
        public void GIVEN_no_trysts_yet_WHEN_computing_the_discovery_chance_THEN_it_is_zero()
        {
            AffairDiscoveryPolicy.ChancePercent(trystCount: 0)
                                 .Should().Be(0, "nothing has happened yet to leak");
        }

        // The cap keeps exposure a CHANCE and never a certainty: even a long-running affair has a real weekly
        // probability of staying hidden. Without the clamp a long affair would drive the chance past 100 and be
        // exposed the very next tryst every time, which reads as a scripted reveal rather than a gradual leak.
        [Test]
        public void GIVEN_a_very_long_affair_WHEN_computing_the_discovery_chance_THEN_it_clamps_to_the_max()
        {
            AffairDiscoveryPolicy.ChancePercent(trystCount: 100000)
                                 .Should().Be(AffairDiscoveryPolicy.MaxChancePercent, "exposure is capped so a hidden affair is never certain to be caught");
        }

        // The boundary the mod's MBRandom roll is compared against: a roll UNDER the chance exposes the affair,
        // a roll AT or ABOVE it does not. Pins the strictly-less comparison so the discovery knobs actually
        // govern the reveal rather than an off-by-one exposing everyone (or no one).
        [Test]
        public void GIVEN_a_secret_unknown_affair_WHEN_the_roll_lands_under_the_chance_THEN_it_is_discovered()
        {
            int chance = AffairDiscoveryPolicy.ChancePercent(trystCount: 5);

            AffairDiscoveryPolicy.IsDiscovered(isSecret: true, alreadyKnown: false, trystCount: 5, rollPercent: chance - 1)
                                 .Should().BeTrue("a roll below the chance exposes the affair this tryst");
            AffairDiscoveryPolicy.IsDiscovered(isSecret: true, alreadyKnown: false, trystCount: 5, rollPercent: chance)
                                 .Should().BeFalse("a roll at the chance is outside it (strictly-less comparison)");
        }

        // An OPEN lover is never "discovered" as a betrayal: he is already known under the reciprocal open
        // terms, so no scandal beat exists for him. If this leaked, a partner the player openly consented to
        // would trigger a betrayal confrontation (3b-b) she never earned, inverting the open-arrangement design.
        [Test]
        public void GIVEN_an_open_lover_WHEN_asked_THEN_he_is_never_discovered()
        {
            AffairDiscoveryPolicy.IsDiscovered(isSecret: false, alreadyKnown: false, trystCount: 1000, rollPercent: 0)
                                 .Should().BeFalse("an open lover is simply known, never discovered as a betrayal");
        }

        // A discovery fires ONCE: once the affair is known the scandal must never re-fire, or the player would
        // be told his wife was unfaithful again every single tryst thereafter. The one-time flag the mod sets
        // (LoverKnownToPlayer) is fed here as alreadyKnown so the reveal is idempotent.
        [Test]
        public void GIVEN_an_already_known_affair_WHEN_asked_THEN_it_is_not_rediscovered()
        {
            AffairDiscoveryPolicy.IsDiscovered(isSecret: true, alreadyKnown: true, trystCount: 1000, rollPercent: 0)
                                 .Should().BeFalse("an affair already known is never re-discovered; the scandal fires once");
        }
    }
}
