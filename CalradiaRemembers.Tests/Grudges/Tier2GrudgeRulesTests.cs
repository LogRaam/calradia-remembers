// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, Tier 2 source catalog: one behaviour per test, Gabriel reads these as the design
// documentation for DeliveredPrisoner / CaptiveAbuse / SwayExposed / UnblessedMarriage formation.

#region

using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
    [TestFixture]
    public sealed class Tier2GrudgeRulesTests
    {
        // ── DeliveredPrisoner ────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_captive_delivered_to_their_own_enemy_WHEN_evaluating_THEN_a_grudge_forms_known_to_the_player()
        {
            var facts = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = true, CaptiveRegardForPlayer = 0};

            GrudgeProposal proposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(facts);

            proposal.Forms.Should().BeTrue();
            proposal.KnownToPlayer.Should().BeTrue("the captive was physically handed over, there is nothing to conceal");
            proposal.Magnitude.Should().Be(Tier2GrudgeRules.DeliveredPrisonerToEnemyMagnitude);
        }

        [Test]
        public void GIVEN_a_captive_delivered_to_a_neutral_or_allied_party_WHEN_evaluating_THEN_a_grudge_still_forms_but_lighter()
        {
            var toEnemy = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = true, CaptiveRegardForPlayer = 0};
            var toNeutral = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = false, CaptiveRegardForPlayer = 0};

            GrudgeProposal enemyProposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(toEnemy);
            GrudgeProposal neutralProposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(toNeutral);

            neutralProposal.Forms.Should().BeTrue("even a lesser betrayal of custody still resents, per the ROADMAP's ALWAYS-forms design call");
            neutralProposal.Magnitude.Should().BeLessThan(enemyProposal.Magnitude);
            neutralProposal.Magnitude.Should().Be(Tier2GrudgeRules.DeliveredPrisonerBaseMagnitude);
        }

        [Test]
        public void GIVEN_a_captive_who_already_disliked_the_player_WHEN_delivered_THEN_the_magnitude_is_bumped()
        {
            var neutralRegard = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = false, CaptiveRegardForPlayer = 0};
            var lowRegard = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = false, CaptiveRegardForPlayer = Tier2GrudgeRules.DeliveredPrisonerLowRegardThreshold};

            int neutralMagnitude = Tier2GrudgeRules.DeliveredPrisonerGrudge(neutralRegard).Magnitude;
            int lowRegardMagnitude = Tier2GrudgeRules.DeliveredPrisonerGrudge(lowRegard).Magnitude;

            lowRegardMagnitude.Should().Be(neutralMagnitude + Tier2GrudgeRules.DeliveredPrisonerLowRegardBonus);
        }

        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_delivered_prisoner_THEN_nothing_forms()
        {
            Tier2GrudgeRules.DeliveredPrisonerGrudge(null).Forms.Should().BeFalse();
        }

        // ── CaptiveAbuse ─────────────────────────────────────────────────────

        [TestCase(0)]
        public void GIVEN_a_benign_captive_scene_severity_zero_WHEN_evaluating_THEN_nothing_forms(int severity)
        {
            var facts = new CaptiveAbuseFacts {Severity = severity, PlayerWasThePerpetrator = false};

            Tier2GrudgeRules.CaptiveAbuseGrudge(facts).Forms.Should().BeFalse("Reward/Interrogation register as benign, Severity 0");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GIVEN_a_captive_scene_at_or_above_severity_one_WHEN_evaluating_THEN_a_grudge_forms(int severity)
        {
            var facts = new CaptiveAbuseFacts {Severity = severity, PlayerWasThePerpetrator = false};

            GrudgeProposal proposal = Tier2GrudgeRules.CaptiveAbuseGrudge(facts);

            proposal.Forms.Should().BeTrue();
            proposal.KnownToPlayer.Should().BeTrue("the victim lived the scene firsthand");
        }

        [Test]
        public void GIVEN_rising_severity_WHEN_evaluating_THEN_magnitude_climbs_steeply()
        {
            int mild = Tier2GrudgeRules.CaptiveAbuseGrudge(new CaptiveAbuseFacts {Severity = 1, PlayerWasThePerpetrator = false}).Magnitude;
            int moderate = Tier2GrudgeRules.CaptiveAbuseGrudge(new CaptiveAbuseFacts {Severity = 2, PlayerWasThePerpetrator = false}).Magnitude;
            int grievous = Tier2GrudgeRules.CaptiveAbuseGrudge(new CaptiveAbuseFacts {Severity = 3, PlayerWasThePerpetrator = false}).Magnitude;

            mild.Should().BeLessThan(moderate);
            moderate.Should().BeLessThan(grievous);
        }

        [Test]
        public void GIVEN_the_player_as_direct_perpetrator_WHEN_evaluating_THEN_the_magnitude_is_heaviest_at_any_given_severity()
        {
            var bystander = new CaptiveAbuseFacts {Severity = 2, PlayerWasThePerpetrator = false};
            var perpetrator = new CaptiveAbuseFacts {Severity = 2, PlayerWasThePerpetrator = true};

            int bystanderMagnitude = Tier2GrudgeRules.CaptiveAbuseGrudge(bystander).Magnitude;
            int perpetratorMagnitude = Tier2GrudgeRules.CaptiveAbuseGrudge(perpetrator).Magnitude;

            perpetratorMagnitude.Should().Be(bystanderMagnitude + Tier2GrudgeRules.CaptiveAbusePlayerPerpetratorBonus);
        }

        [Test]
        public void GIVEN_grievous_severity_with_the_player_as_perpetrator_WHEN_evaluating_THEN_the_magnitude_peaks_but_is_not_a_forced_vendetta()
        {
            var facts = new CaptiveAbuseFacts {Severity = 3, PlayerWasThePerpetrator = true};

            GrudgeProposal proposal = Tier2GrudgeRules.CaptiveAbuseGrudge(facts);

            proposal.Magnitude.Should().Be(Tier2GrudgeRules.CaptiveAbuseMaxMagnitude, "this is the ceiling this rule can ever propose");

            // Magnitude sets how deep the wound starts; it is not rigidity. Even at this ceiling, a
            // holder with high regard for the player still resolves it at a rigidity well short of
            // GrudgePolicy.VendettaRigidity — only GrudgeSource.ExecutedKin is unforgivable by construction.
            var candidate = new GrudgeCandidateFacts {Source = GrudgeSource.CaptiveAbuse, Magnitude = proposal.Magnitude, IsAttributable = true};
            int rigidityAtHighRegard = GrudgePolicy.RigidityAt(regardForTarget: 60, candidate);

            rigidityAtHighRegard.Should().BeLessThan(GrudgePolicy.VendettaRigidity);
        }

        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_captive_abuse_THEN_nothing_forms()
        {
            Tier2GrudgeRules.CaptiveAbuseGrudge(null).Forms.Should().BeFalse();
        }

        // ── SwayExposed ──────────────────────────────────────────────────────

        [Test]
        public void GIVEN_the_rumour_never_reached_the_target_WHEN_evaluating_THEN_nothing_forms()
        {
            var facts = new SwayExposedFacts {RumourReachedTarget = false, SwayMagnitude = 80};

            Tier2GrudgeRules.SwayExposedGrudge(facts).Forms.Should().BeFalse("an unheard sway leaves nothing to resent");
        }

        [Test]
        public void GIVEN_the_rumour_reached_the_target_WHEN_evaluating_THEN_a_grudge_forms_hidden_from_the_player()
        {
            var facts = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = 20};

            GrudgeProposal proposal = Tier2GrudgeRules.SwayExposedGrudge(facts);

            proposal.Forms.Should().BeTrue();
            proposal.KnownToPlayer.Should().BeFalse("the target resents quietly until the grudge surfaces some other way, mirroring scheme-warned concealment");
        }

        [Test]
        public void GIVEN_a_harder_sway_WHEN_evaluating_THEN_the_grudge_magnitude_tracks_the_sway_strength()
        {
            var lightSway = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = 10};
            var heavySway = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = 40};

            int lightMagnitude = Tier2GrudgeRules.SwayExposedGrudge(lightSway).Magnitude;
            int heavyMagnitude = Tier2GrudgeRules.SwayExposedGrudge(heavySway).Magnitude;

            heavyMagnitude.Should().BeGreaterThan(lightMagnitude);
        }

        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_sway_exposed_THEN_nothing_forms()
        {
            Tier2GrudgeRules.SwayExposedGrudge(null).Forms.Should().BeFalse();
        }

        // ── UnblessedMarriage ────────────────────────────────────────────────

        [Test]
        public void GIVEN_an_unblessed_marriage_still_standing_WHEN_evaluating_THEN_a_grudge_forms_known_to_the_player()
        {
            var facts = new UnblessedMarriageFacts {MarriageIsUnblessed = true, StillMarried = true};

            GrudgeProposal proposal = Tier2GrudgeRules.UnblessedMarriageGrudge(facts);

            proposal.Forms.Should().BeTrue();
            proposal.KnownToPlayer.Should().BeTrue("the family has made their disapproval plain");
            proposal.Magnitude.Should().Be(Tier2GrudgeRules.UnblessedMarriageMagnitude);
        }

        [Test]
        public void GIVEN_a_blessed_marriage_WHEN_evaluating_THEN_nothing_forms_even_if_still_married()
        {
            var facts = new UnblessedMarriageFacts {MarriageIsUnblessed = false, StillMarried = true};

            Tier2GrudgeRules.UnblessedMarriageGrudge(facts).Forms.Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_unblessed_marriage_that_has_ended_WHEN_evaluating_THEN_nothing_forms_because_this_grudge_is_meant_to_clear_on_divorce()
        {
            // DivorceConsequencePolicy.ResolveUnblessedFamilyReaction already turns this very family's
            // reaction into Gratitude once the marriage ends; the engine wiring is expected to explicitly
            // resolve/remove any live UnblessedMarriage grudge for this holder the same tick, so the two
            // paths never disagree. This rule simply refuses to re-propose one once StillMarried is false.
            var facts = new UnblessedMarriageFacts {MarriageIsUnblessed = true, StillMarried = false};

            Tier2GrudgeRules.UnblessedMarriageGrudge(facts).Forms.Should().BeFalse();
        }

        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_unblessed_marriage_THEN_nothing_forms()
        {
            Tier2GrudgeRules.UnblessedMarriageGrudge(null).Forms.Should().BeFalse();
        }

        // ── Cross-cutting: every formed magnitude clears the formation floor ──

        [Test]
        public void GIVEN_every_tier_2_rules_lightest_forming_case_WHEN_evaluating_THEN_the_magnitude_clears_the_formation_floor()
        {
            int deliveredPrisonerLightest = Tier2GrudgeRules.DeliveredPrisonerGrudge(
                new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = false, CaptiveRegardForPlayer = 100}).Magnitude;

            int captiveAbuseLightest = Tier2GrudgeRules.CaptiveAbuseGrudge(
                new CaptiveAbuseFacts {Severity = 1, PlayerWasThePerpetrator = false}).Magnitude;

            int swayExposedLightest = Tier2GrudgeRules.SwayExposedGrudge(
                new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = 0}).Magnitude;

            int unblessedMarriage = Tier2GrudgeRules.UnblessedMarriageGrudge(
                new UnblessedMarriageFacts {MarriageIsUnblessed = true, StillMarried = true}).Magnitude;

            deliveredPrisonerLightest.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
            captiveAbuseLightest.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
            swayExposedLightest.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
            unblessedMarriage.Should().BeGreaterThanOrEqualTo(GrudgePolicy.MagnitudeFloor);
        }
    }
}
