// Code written by Gabriel Mailhot, 27/06/2026.
// Documents who dares to do what as a posture hardens: mild dislike is a cold shoulder, real hostility a threat,
// and deep hatred turns to a plot / an open challenge / an assassination ONLY for the character that fits —
// honour stays a lord's hand from murder and scheming, and terror of the player rules out striking in person.
// The friendly ladder mirrors it: warmth, then a boon, then a trusted friend's warning of a plot.
//
// Feeds two consumers (StanceConsequenceBehavior): the CONVERSATION hint (StanceConsequenceHint colours
// how the NPC acts this meeting) and the WEEKLY proactive sweep (a hardened verdict becomes an unbidden
// letter, a real scheme via SchemeStore, an InterceptionBehavior pursuit, or an armed town ambush). Secret
// hatred (Plot/Assassinate) deliberately sends no letter: it would tip its own hand; get a verdict wrong
// and either a merely rude lord ends up plotting murder, or a genuinely murderous one stays undetected.

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

        // Baseline no-op guard: a neutral relationship must never provoke an unwarranted letter, scheme,
        // or ambush.
        [Test]
        public void GIVEN_a_neutral_stance_WHEN_resolving_THEN_nothing_is_warranted()
        {
            var stance = new NpcStance { Affection = 0 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.None);
        }

        // HostileFloor: where the hostile ladder starts. ColdShoulder is conversation-only (no letter,
        // per StanceConsequenceBehavior): mild dislike should colour a meeting, not trigger outward action.
        [Test]
        public void GIVEN_mild_dislike_WHEN_resolving_THEN_a_cold_shoulder()
        {
            var stance = new NpcStance { Affection = -30 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.ColdShoulder);
        }

        // ThreatFloor: past this point the weekly sweep DOES send a cold/menacing letter. Pins that real
        // hostility escalates past mere coldness without yet needing any particular trait combination.
        [Test]
        public void GIVEN_real_hostility_WHEN_resolving_THEN_a_threat()
        {
            var stance = new NpcStance { Affection = -50 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.Threat);
        }

        // The rarest, most dangerous verdict: arms a real town ambush (StanceAmbushArmedDay). ALL of
        // dishonourable + calculating + cruel (mercy<=-1) + undeterred by fear must hold together, drop
        // any one and a merely rude lord must never end up arming an assassination attempt.
        [Test]
        public void GIVEN_deep_hatred_in_a_cold_cruel_schemer_WHEN_resolving_THEN_an_assassination()
        {
            //Arrange — dishonourable, calculating, cruel, and not afraid to act.
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = -1, Valor = 0, Mercy = -2, Calculating = 2})
                .Should().Be(StanceConsequence.Assassinate);
        }

        // The exact "scruple" line: a schemer without mercy<=-1 stops short of literal murder and plots
        // (a real SchemeStore scheme) instead: pins that mercy, not calculating alone, is what tips a
        // plotter into a killer.
        [Test]
        public void GIVEN_deep_hatred_in_a_cold_schemer_with_a_scruple_WHEN_resolving_THEN_a_plot_not_murder()
        {
            //Arrange — a cold schemer, but not cruel enough to kill (mercy not below zero).
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = -1, Valor = 0, Mercy = 0, Calculating = 2})
                .Should().Be(StanceConsequence.Plot);
        }

        // A bold, valorous character rides out openly (InterceptionBehavior) rather than schemes or kills
        // in secret: the character-consistency rule, valour prefers a real fight to a hidden blow.
        [Test]
        public void GIVEN_deep_hatred_in_a_bold_warrior_WHEN_resolving_THEN_an_open_challenge()
        {
            //Arrange — honourable and bold: he rides out rather than schemes.
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 1, Valor = 2, Mercy = 0, Calculating = -1})
                .Should().Be(StanceConsequence.Intercept);
        }

        // BoldFearCeiling: even a bold, murderous-inclined lord will not strike the player IN PERSON while
        // terrified of them (Fear>=60): hatred turns indirect (a plot) or, absent a scheming bent, to mere
        // words. Without this carve-out a cowed enemy would still ride out to challenge the player.
        [Test]
        public void GIVEN_deep_hatred_but_terror_of_the_player_WHEN_resolving_THEN_no_strike_in_person()
        {
            //Arrange — bold by nature, but too terrified to confront the player, and no scheming bent.
            var stance = new NpcStance { Affection = -80, Fear = 80 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 2, Mercy = 0, Calculating = 0})
                .Should().Be(StanceConsequence.Threat);
        }

        // The single most important character guard: Honor stays a lord's hand from murder AND scheming
        // regardless of every other trait pointing at it. Break this and an "honourable" lord could still
        // plot or assassinate, contradicting the whole design's WHO-dares-WHAT premise.
        [Test]
        public void GIVEN_deep_hatred_in_an_honourable_lord_WHEN_resolving_THEN_he_will_not_scheme_or_murder()
        {
            //Arrange — every trait points at murder EXCEPT honour, which forbids it; and he is not bold.
            var stance = new NpcStance { Affection = -80, Fear = 10 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 2, Valor = 0, Mercy = -2, Calculating = 2})
                .Should().Be(StanceConsequence.Threat);
        }

        // FriendlyFloor: mirrors HostileFloor on the warm side. WarmWelcome is conversation-only, the
        // friendly ladder's own baseline.
        [Test]
        public void GIVEN_mild_warmth_WHEN_resolving_THEN_a_warm_welcome()
        {
            var stance = new NpcStance { Affection = 30 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.WarmWelcome);
        }

        // FavorFloor + FavorRespect: a real boon (gift/discount) wants genuine esteem, not just fondness
        // alone (StanceConsequencePolicy's own comment): pure warmth without Respect must stay a WarmWelcome.
        [Test]
        public void GIVEN_strong_warmth_and_respect_WHEN_resolving_THEN_a_favor()
        {
            var stance = new NpcStance { Affection = 50, Respect = 40, Trust = 0 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.Favor);
        }

        // WarningTrust takes PRIORITY over Favor in ResolveFriendly's own ordering: a deeply trusted friend
        // must be checked first, or they'd be downgraded to a mere boon instead of naming a real threat
        // (SchemeStore.GetSecretSchemerAgainst), a materially more valuable consequence to the player.
        [Test]
        public void GIVEN_strong_warmth_and_deep_trust_WHEN_resolving_THEN_a_warning_of_a_plot()
        {
            //Arrange — a deeply trusted friend tips you off; trust takes priority over a mere boon.
            var stance = new NpcStance { Affection = 50, Trust = 50, Respect = 40 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.Warning);
        }

        // M-S1 (regard audit): fondness alone must not warrant a WARM welcome when trust has collapsed. Without
        // this, a friendly-affection stance whose Trust is deeply negative produced a "genuinely glad to see you"
        // hint printed right beside the "you deeply distrust the player" axis line, the perception system openly
        // contradicting itself. Deep distrust withholds the warm hint (None); the axis lines still speak.
        [Test]
        public void GIVEN_warm_affection_but_collapsed_trust_WHEN_resolving_THEN_no_warm_welcome()
        {
            //Arrange: fond (Affection 50) yet deeply distrustful (Trust -80); no clean warm hint fits.
            var stance = new NpcStance { Affection = 50, Trust = -80 };

            StanceConsequencePolicy.Resolve(stance, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.None);
        }

        // The boundary is inclusive at -40: trust exactly at the floor still withholds the warm welcome, but a
        // touch above it (a wary but not collapsed trust) still allows the fondness to read as warmth.
        [Test]
        public void GIVEN_warm_affection_with_trust_just_above_the_floor_WHEN_resolving_THEN_still_a_warm_welcome()
        {
            var atFloor = new NpcStance { Affection = 50, Trust = -40 };
            var aboveFloor = new NpcStance { Affection = 50, Trust = -39 };

            StanceConsequencePolicy.Resolve(atFloor, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.None);
            StanceConsequencePolicy.Resolve(aboveFloor, new CalradiaRemembers.Logic.Appreciations.TraitFacts {Honor = 0, Valor = 0, Mercy = 0, Calculating = 0}).Should().Be(StanceConsequence.WarmWelcome);
        }

        // Gabriel (30/10/2026): "les lettres d'amities devraient demander plus de regards pour diminuer le nombre". A lord
        // whose esteem earns the Favor rung in conversation does not yet write unbidden; only a deep bond does. If this
        // slips, the crowd of friendly letters tashmetu's players received comes back.
        [Test]
        public void GIVEN_a_favor_rung_below_a_deep_bond_WHEN_a_friendly_letter_is_weighed_THEN_none_is_written_until_the_bond_deepens()
        {
            var favor = new NpcStance { Affection = 50, Respect = 40 };
            var deep = new NpcStance { Affection = StanceConsequencePolicy.FriendlyLetterAffection, Respect = 40 };

            StanceConsequencePolicy.Resolve(favor, new CalradiaRemembers.Logic.Appreciations.TraitFacts()).Should().Be(StanceConsequence.Favor);
            StanceConsequencePolicy.WarrantsFriendlyLetter(favor).Should().BeFalse();
            StanceConsequencePolicy.WarrantsFriendlyLetter(deep).Should().BeTrue();
        }
    }
}
