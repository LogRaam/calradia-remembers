// Code written by Gabriel Mailhot, 06/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisTurnPolicyTests
    {
        // A cowed, respectful, pragmatic nemesis with only a modest grudge: the archetype that turns.
        private static NemesisTurnFacts Turnable()
            => new NemesisTurnFacts(fear: 70, respect: 45, grudgeMagnitude: 30, honor: -1, valor: 0, calculating: 1);

        [Test]
        public void GIVEN_a_cowed_respectful_pragmatic_nemesis_WHEN_resolving_THEN_he_turns()
        {
            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(Turnable());

            v.CanTurn.Should().BeTrue();
            v.Refusal.Should().Be(NemesisTurnRefusal.None);
        }

        [Test]
        public void GIVEN_a_nemesis_who_does_not_fear_the_player_WHEN_resolving_THEN_he_refuses_for_too_little_fear()
        {
            var facts = new NemesisTurnFacts(NemesisTurnPolicy.FearFloor - 1, 45, 30, -1, 0, 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.FearsYouTooLittle);
        }

        [Test]
        public void GIVEN_a_nemesis_who_does_not_respect_the_player_WHEN_resolving_THEN_he_refuses_for_too_little_respect()
        {
            var facts = new NemesisTurnFacts(70, NemesisTurnPolicy.RespectFloor - 1, 30, -1, 0, 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.RespectsYouTooLittle);
        }

        [Test]
        public void GIVEN_a_brave_nemesis_who_is_no_calculator_WHEN_resolving_THEN_he_is_too_defiant_to_serve()
        {
            var facts = new NemesisTurnFacts(70, 45, 30, honor: -1, valor: 2, calculating: 0);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.TooDefiant);
        }

        [Test]
        public void GIVEN_an_honorbound_nemesis_who_is_no_calculator_WHEN_resolving_THEN_he_is_too_defiant_to_serve()
        {
            var facts = new NemesisTurnFacts(70, 45, 30, honor: 2, valor: 0, calculating: 0);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.TooDefiant);
        }

        [Test]
        public void GIVEN_a_brave_but_coldly_calculating_nemesis_WHEN_resolving_THEN_pragmatism_overrides_defiance_and_he_turns()
        {
            var facts = new NemesisTurnFacts(70, 45, 30, honor: 0, valor: 2, calculating: 2);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeTrue("a survivor switches sides when the arithmetic favours it, however brave");
        }

        [Test]
        public void GIVEN_a_deep_grudge_with_only_ordinary_dominance_WHEN_resolving_THEN_his_hatred_blocks_the_turn()
        {
            // Fear/respect clear the floors, but not by the dominance margin that overcomes a vendetta.
            var facts = new NemesisTurnFacts(NemesisTurnPolicy.FearFloor + 5, NemesisTurnPolicy.RespectFloor + 5,
                grudgeMagnitude: NemesisTurnPolicy.GrudgeCeiling, honor: -1, valor: 0, calculating: 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.HatesYouTooMuch);
        }

        [Test]
        public void GIVEN_a_deep_grudge_but_overwhelming_dominance_WHEN_resolving_THEN_dominance_overcomes_his_hatred_and_he_turns()
        {
            // Same deep grudge, but fear and respect sit far above the floors: sheer dominance bends him anyway.
            var facts = new NemesisTurnFacts(fear: 95, respect: 90, grudgeMagnitude: NemesisTurnPolicy.GrudgeCeiling + 20,
                honor: -1, valor: 0, calculating: 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeTrue();
        }

        [Test]
        public void GIVEN_facts_exactly_at_both_stance_floors_WHEN_resolving_THEN_the_floors_are_inclusive()
        {
            var facts = new NemesisTurnFacts(NemesisTurnPolicy.FearFloor, NemesisTurnPolicy.RespectFloor,
                grudgeMagnitude: 0, honor: -1, valor: 0, calculating: 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeTrue("the floors are the minimum acceptable, not a value that must be exceeded");
        }
    }
}
