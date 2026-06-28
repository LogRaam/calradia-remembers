// Code written by Gabriel Mailhot, 27/06/2026.
// Documents who dares to do what as a posture hardens: mild dislike is a cold shoulder, real hostility a threat,
// and deep hatred turns to a plot / an open challenge / an assassination ONLY for the character that fits —
// honour stays a lord's hand from murder and scheming, and terror of the player rules out striking in person.
// The friendly ladder mirrors it: warmth, then a boon, then a trusted friend's warning of a plot.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StanceConsequencePolicyTests
    {
        // Trait order: honor, valor, mercy, calculating (authored −2..+2).

        [Test]
        public void GIVEN_a_neutral_stance_WHEN_resolving_THEN_nothing_is_warranted()
        {
            var stance = new NpcStance { Affection = 0 };

            StanceConsequencePolicy.Resolve(stance, 0, 0, 0, 0).Should().Be(StanceConsequence.None);
        }

        [Test]
        public void GIVEN_mild_dislike_WHEN_resolving_THEN_a_cold_shoulder()
        {
            var stance = new NpcStance { Affection = -30 };

            StanceConsequencePolicy.Resolve(stance, 0, 0, 0, 0).Should().Be(StanceConsequence.ColdShoulder);
        }

        [Test]
        public void GIVEN_real_hostility_WHEN_resolving_THEN_a_threat()
        {
            var stance = new NpcStance { Affection = -50 };

            StanceConsequencePolicy.Resolve(stance, 0, 0, 0, 0).Should().Be(StanceConsequence.Threat);
        }

        [Test]
        public void GIVEN_deep_hatred_in_a_cold_cruel_schemer_WHEN_resolving_THEN_an_assassination()
        {
            //Arrange — dishonourable, calculating, cruel, and not afraid to act.
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, honor: -1, valor: 0, mercy: -2, calculating: 2)
                .Should().Be(StanceConsequence.Assassinate);
        }

        [Test]
        public void GIVEN_deep_hatred_in_a_cold_schemer_with_a_scruple_WHEN_resolving_THEN_a_plot_not_murder()
        {
            //Arrange — a cold schemer, but not cruel enough to kill (mercy not below zero).
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, honor: -1, valor: 0, mercy: 0, calculating: 2)
                .Should().Be(StanceConsequence.Plot);
        }

        [Test]
        public void GIVEN_deep_hatred_in_a_bold_warrior_WHEN_resolving_THEN_an_open_challenge()
        {
            //Arrange — honourable and bold: he rides out rather than schemes.
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, honor: 1, valor: 2, mercy: 0, calculating: -1)
                .Should().Be(StanceConsequence.Intercept);
        }

        [Test]
        public void GIVEN_deep_hatred_but_terror_of_the_player_WHEN_resolving_THEN_no_strike_in_person()
        {
            //Arrange — bold by nature, but too terrified to confront the player, and no scheming bent.
            var stance = new NpcStance { Affection = -80, Fear = 80 };

            StanceConsequencePolicy.Resolve(stance, honor: 0, valor: 2, mercy: 0, calculating: 0)
                .Should().Be(StanceConsequence.Threat);
        }

        [Test]
        public void GIVEN_deep_hatred_in_an_honourable_lord_WHEN_resolving_THEN_he_will_not_scheme_or_murder()
        {
            //Arrange — every trait points at murder EXCEPT honour, which forbids it; and he is not bold.
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, honor: 2, valor: 0, mercy: -2, calculating: 2)
                .Should().Be(StanceConsequence.Threat);
        }

        [Test]
        public void GIVEN_mild_warmth_WHEN_resolving_THEN_a_warm_welcome()
        {
            var stance = new NpcStance { Affection = 30 };

            StanceConsequencePolicy.Resolve(stance, 0, 0, 0, 0).Should().Be(StanceConsequence.WarmWelcome);
        }

        [Test]
        public void GIVEN_strong_warmth_and_respect_WHEN_resolving_THEN_a_favor()
        {
            var stance = new NpcStance { Affection = 50, Respect = 40, Trust = 0 };

            StanceConsequencePolicy.Resolve(stance, 0, 0, 0, 0).Should().Be(StanceConsequence.Favor);
        }

        [Test]
        public void GIVEN_strong_warmth_and_deep_trust_WHEN_resolving_THEN_a_warning_of_a_plot()
        {
            //Arrange — a deeply trusted friend tips you off; trust takes priority over a mere boon.
            var stance = new NpcStance { Affection = 50, Trust = 50, Respect = 40 };

            StanceConsequencePolicy.Resolve(stance, 0, 0, 0, 0).Should().Be(StanceConsequence.Warning);
        }
    }
}
