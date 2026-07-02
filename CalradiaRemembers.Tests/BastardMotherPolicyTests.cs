// Code written by Gabriel Mailhot, 26/06/2026.
// Documents that a bastard's mother is not a uniform extortionist: her traits and regard for the player decide
// whether she blackmails, longs for him, resents him, or simply asks for the child's keep.

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
        [Test]
        public void GIVEN_a_scheming_dishonourable_woman_WHEN_resolving_THEN_she_blackmails()
        {
            BastardMotherPolicy.Resolve(honor: -1, calculating: 1, relationWithPlayer: 0)
                .Should().Be(BastardMotherDisposition.Mercenary);
        }

        [Test]
        public void GIVEN_a_calculating_woman_who_is_nonetheless_fond_WHEN_resolving_THEN_fondness_rules_out_blackmail()
        {
            // A player reported a mother with +50 regard AND an agreement to wed still trying to blackmail him.
            // A woman who loves the player does not extort him whatever her cunning: high regard longs, never schemes.
            BastardMotherPolicy.Resolve(honor: -2, calculating: 2, relationWithPlayer: 80)
                .Should().Be(BastardMotherDisposition.Longing);
        }

        [Test]
        public void GIVEN_an_honourable_fond_woman_WHEN_resolving_THEN_she_longs_for_him()
        {
            BastardMotherPolicy.Resolve(honor: 2, calculating: 0, relationWithPlayer: 30)
                .Should().Be(BastardMotherDisposition.Longing);
        }

        [Test]
        public void GIVEN_a_woman_who_feels_wronged_WHEN_resolving_THEN_she_is_resentful()
        {
            BastardMotherPolicy.Resolve(honor: 1, calculating: 0, relationWithPlayer: -25)
                .Should().Be(BastardMotherDisposition.Resentful);
        }

        [Test]
        public void GIVEN_a_neutral_woman_WHEN_resolving_THEN_she_is_pragmatic()
        {
            BastardMotherPolicy.Resolve(honor: 0, calculating: 0, relationWithPlayer: 0)
                .Should().Be(BastardMotherDisposition.Pragmatic);
        }

        [Test]
        public void GIVEN_regard_at_the_fond_and_cold_thresholds_WHEN_resolving_THEN_the_tiers_are_inclusive()
        {
            BastardMotherPolicy.Resolve(0, 0, BastardMotherPolicy.FondRelation).Should().Be(BastardMotherDisposition.Longing);
            BastardMotherPolicy.Resolve(0, 0, BastardMotherPolicy.ColdRelation).Should().Be(BastardMotherDisposition.Resentful);
        }

        [Test]
        public void GIVEN_scheming_just_short_of_the_threshold_WHEN_resolving_THEN_she_does_not_blackmail()
        {
            //Arrange — calculating 1, honor 0 → difference 1, below the threshold of 2.
            BastardMotherPolicy.Resolve(honor: 0, calculating: 1, relationWithPlayer: 0)
                .Should().Be(BastardMotherDisposition.Pragmatic);
        }
    }
}
