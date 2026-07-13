// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 4 (SPARE + TURN): the pure gate deciding whether a beaten, held nemesis accepts
// being spared and taken into the player's service instead of being killed. Four conditions must ALL hold
// (fears you enough, respects you enough, his nature allows bending, his grudge is not an unbreakable
// vendetta), each with an escape valve described in the production file's header.
//
// WHY IT MATTERS: this is now wired live (BannerlordGameStateBridge's turn_nemesis action re-runs this exact
// gate on the NPC's live Fear/Respect/grudge/traits before honouring an in-character "I offer to spare you"
// moment; a genuine turn frees him into the player's clan and closes his vendetta for good, via
// NemesisResolutionBehavior.Turn). Get a threshold wrong and either a nemesis who should die free instead
// grovels out of character, or a nemesis who should plausibly bend is refused every time, making the whole
// increment 4 fantasy (beat him, spare him, recruit him) unreachable in practice.

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

        // The baseline positive case: the archetype the production header calls out as typically turnable
        // (low Honor, some Calculating, cowed and respectful). If this refuses, increment 4's core fantasy
        // (beat him, spare him, recruit him) is unreachable even in the easiest case.
        [Test]
        public void GIVEN_a_cowed_respectful_pragmatic_nemesis_WHEN_resolving_THEN_he_turns()
        {
            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(Turnable());

            v.CanTurn.Should().BeTrue();
            v.Refusal.Should().Be(NemesisTurnRefusal.None);
        }

        // Condition 1: "a captor he does not fear has no reason to bend" (file header). Without this floor a
        // nemesis could surrender out of character to a player he has no real reason to fear.
        [Test]
        public void GIVEN_a_nemesis_who_does_not_fear_the_player_WHEN_resolving_THEN_he_refuses_for_too_little_fear()
        {
            var facts = new NemesisTurnFacts(NemesisTurnPolicy.FearFloor - 1, 45, 30, -1, 0, 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.FearsYouTooLittle);
        }

        // Condition 2: "you beat him as a worthy foe, not by luck" (file header). Serving a captor he does
        // not respect would be shameful to him, so low respect must refuse independently of fear.
        [Test]
        public void GIVEN_a_nemesis_who_does_not_respect_the_player_WHEN_resolving_THEN_he_refuses_for_too_little_respect()
        {
            var facts = new NemesisTurnFacts(70, NemesisTurnPolicy.RespectFloor - 1, 30, -1, 0, 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.RespectsYouTooLittle);
        }

        // Condition 3, first half: "a brave or honor-bound man dies free" (file header). A high-Valor nemesis
        // with no Calculating escape valve must refuse even once fear and respect both clear their floors.
        [Test]
        public void GIVEN_a_brave_nemesis_who_is_no_calculator_WHEN_resolving_THEN_he_is_too_defiant_to_serve()
        {
            var facts = new NemesisTurnFacts(70, 45, 30, honor: -1, valor: 2, calculating: 0);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.TooDefiant);
        }

        // Same condition 3, the Honor half: an honor-bound nemesis "does not grovel" either, independently
        // of Valor, so both traits must gate defiance on their own.
        [Test]
        public void GIVEN_an_honorbound_nemesis_who_is_no_calculator_WHEN_resolving_THEN_he_is_too_defiant_to_serve()
        {
            var facts = new NemesisTurnFacts(70, 45, 30, honor: 2, valor: 0, calculating: 0);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeFalse();
            v.Refusal.Should().Be(NemesisTurnRefusal.TooDefiant);
        }

        // Condition 3's escape valve: a cold Calculating trait makes even a brave man a pragmatic survivor
        // who switches sides "when the arithmetic says so" (file header), overriding otherwise-certain defiance.
        [Test]
        public void GIVEN_a_brave_but_coldly_calculating_nemesis_WHEN_resolving_THEN_pragmatism_overrides_defiance_and_he_turns()
        {
            var facts = new NemesisTurnFacts(70, 45, 30, honor: 0, valor: 2, calculating: 2);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeTrue("a survivor switches sides when the arithmetic favours it, however brave");
        }

        // Condition 4: "his HATRED of you is not an unbreakable vendetta" (file header). A grudge at the
        // ceiling must block the turn on its own, even once stance clears both floors, or a genuinely
        // vengeful nemesis would grovel to a captor he still hates.
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

        // Condition 4's escape valve: sheer dominance (fear and respect well above their floors) can overcome
        // even a maxed-out grudge, so total mastery of the nemesis is never blocked by hatred alone.
        [Test]
        public void GIVEN_a_deep_grudge_but_overwhelming_dominance_WHEN_resolving_THEN_dominance_overcomes_his_hatred_and_he_turns()
        {
            // Same deep grudge, but fear and respect sit far above the floors: sheer dominance bends him anyway.
            var facts = new NemesisTurnFacts(fear: 95, respect: 90, grudgeMagnitude: NemesisTurnPolicy.GrudgeCeiling + 20,
                honor: -1, valor: 0, calculating: 1);

            NemesisTurnVerdict v = NemesisTurnPolicy.Resolve(facts);

            v.CanTurn.Should().BeTrue();
        }

        // Both floors are documented as "at least this much", so sitting EXACTLY on Fear/Respect must pass,
        // not require exceeding them; a strict-greater-than bug here would silently raise the real bar past
        // the tuned constants Gabriel is meant to be able to retune.
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
