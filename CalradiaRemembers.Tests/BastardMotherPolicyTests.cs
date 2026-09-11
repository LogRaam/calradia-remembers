// Code written by Gabriel Mailhot, 26/06/2026.
// Documents that a bastard's mother is not a uniform extortionist: her traits and regard for the player decide
// whether she blackmails, longs for him, resents him, or simply asks for the child's keep.
//
// WHY IT MATTERS: the Disposition this returns is what BastardBehavior branches on: only Mercenary feeds the
// blackmail-demand/letter flow (BastardBlackmailPolicy); the other three write warmer or colder notes and never
// threaten to leak the secret themselves. CHANGELOG v1.20.3 shipped a fix for exactly this: a mother with warm
// regard for the player (and, in the reported case, an agreement to wed) was still extorting him. This policy's
// own doc comment states the fix directly: fondness is checked FIRST and rules out the mercenary path even for
// a scheming, dishonourable woman. Get the ordering wrong and that regression comes back.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardMotherPolicyTests
    {
        // The one and only path into the blackmail thread: without a genuinely scheming, dishonourable mother
        // reading as Mercenary, BastardBlackmailPolicy would never get exercised at all.
        [Test]
        public void GIVEN_a_scheming_dishonourable_woman_WHEN_resolving_THEN_she_blackmails()
        {
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = -1, Calculating = 1, RelationWithPlayer = 0})
                .Should().Be(BastardMotherDisposition.Mercenary);
        }

        // This is the exact regression CHANGELOG v1.20.3 fixed. Without checking fondness first, a scheming
        // trait alone would still route a woman who loves the player into the Mercenary/blackmail branch.
        [Test]
        public void GIVEN_a_calculating_woman_who_is_nonetheless_fond_WHEN_resolving_THEN_fondness_rules_out_blackmail()
        {
            // A player reported a mother with +50 regard AND an agreement to wed still trying to blackmail him.
            // A woman who loves the player does not extort him whatever her cunning: high regard longs, never schemes.
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = -2, Calculating = 2, RelationWithPlayer = 80})
                .Should().Be(BastardMotherDisposition.Longing);
        }

        // The ordinary Longing case (per the enum's own doc): she misses him, wants him to meet the child,
        // wishes he would wed her. Without this the "variety that gives the world personality" collapses.
        [Test]
        public void GIVEN_an_honourable_fond_woman_WHEN_resolving_THEN_she_longs_for_him()
        {
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = 2, Calculating = 0, RelationWithPlayer = 30})
                .Should().Be(BastardMotherDisposition.Longing);
        }

        // A cold mother writes with reproach, not extortion: without this branch, low regard alone could be
        // mistaken for scheming and misrouted into the blackmail path instead of a resentful note.
        [Test]
        public void GIVEN_a_woman_who_feels_wronged_WHEN_resolving_THEN_she_is_resentful()
        {
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = 1, Calculating = 0, RelationWithPlayer = -25})
                .Should().Be(BastardMotherDisposition.Resentful);
        }

        // The fallback disposition: neither scheming nor attached, she simply informs the player and asks for
        // the child's keep. Confirms the switch has a sane default instead of forcing every mother into a tier.
        [Test]
        public void GIVEN_a_neutral_woman_WHEN_resolving_THEN_she_is_pragmatic()
        {
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = 0, Calculating = 0, RelationWithPlayer = 0})
                .Should().Be(BastardMotherDisposition.Pragmatic);
        }

        // Pins both FondRelation and ColdRelation as INCLUSIVE boundaries: an off-by-one here would misfile a
        // mother exactly at the tier edge into the wrong (adjacent) disposition.
        [Test]
        public void GIVEN_regard_at_the_fond_and_cold_thresholds_WHEN_resolving_THEN_the_tiers_are_inclusive()
        {
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = 0, Calculating = 0, RelationWithPlayer = BastardMotherPolicy.FondRelation}).Should().Be(BastardMotherDisposition.Longing);
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = 0, Calculating = 0, RelationWithPlayer = BastardMotherPolicy.ColdRelation}).Should().Be(BastardMotherDisposition.Resentful);
        }

        // Pins MercenaryThreshold=2 as a genuine gap, not a bare ">": a mother only slightly more calculating
        // than honourable should not be branded a schemer, or too many ordinary mothers would blackmail.
        [Test]
        public void GIVEN_scheming_just_short_of_the_threshold_WHEN_resolving_THEN_she_does_not_blackmail()
        {
            //Arrange — calculating 1, honor 0 → difference 1, below the threshold of 2.
            BastardMotherPolicy.Resolve(new BastardMotherPolicy.MotherDispositionFacts {Honor = 0, Calculating = 1, RelationWithPlayer = 0})
                .Should().Be(BastardMotherDisposition.Pragmatic);
        }
    }
}
