// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, Tier 2 source catalog: one behaviour per test, Gabriel reads these as the design
// documentation for DeliveredPrisoner / CaptiveAbuse / SwayExposed / UnblessedMarriage formation.
// These are the ROADMAP's "new detections, high value" sources: NOT migration backfill, only ever
// live-planted from the wiring increment on. Each rule only decides Forms/Magnitude/KnownToPlayer; the
// GrudgeProposal feeds GrudgePolicy.ShouldForm downstream (attributability + trait-modulated floor) before
// a grudge is actually planted, and KnownToPlayer decides whether the Encyclopedia grievance panel and the
// prompt's concealment instructions ever show it. Get Forms wrong and a betrayal the player should answer
// for goes silently unresented; get KnownToPlayer wrong and either a hidden grudge leaks (breaking the
// stance-assassin surprise pattern) or a plainly-caused one wrongly hides from the player.

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

        // The headline Tier 2 case ("you sold me to my enemy", give_prisoner). If this failed to form, the
        // player could hand a captive lord to his own bitter foe with zero narrative consequence, undermining
        // the whole point of adding this source to the catalog.
        [Test]
        public void GIVEN_a_captive_delivered_to_their_own_enemy_WHEN_evaluating_THEN_a_grudge_forms_known_to_the_player()
        {
            var facts = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = true, CaptiveRegardForPlayer = 0};

            GrudgeProposal proposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(facts);

            proposal.Forms.Should().BeTrue();
            proposal.KnownToPlayer.Should().BeTrue("the captive was physically handed over, there is nothing to conceal");
            proposal.Magnitude.Should().Be(Tier2GrudgeRules.DeliveredPrisonerToEnemyMagnitude);
        }

        // Player report (2026-08-26): prisoner ransom is a core, honorable practice, and being ransomed home is a
        // GOOD outcome for the captive, so a delivery that is NOT to their enemy (a ransom back to their own side,
        // or a sale to a neutral party) must plant NO grudge. Only a hand-over to their bitter enemy is a betrayal
        // worth resenting. Without this, every routine ransom would sour the freed prisoner ("sold me off like
        // chattel" on a lord you just released to his faction).
        [Test]
        public void GIVEN_a_captive_delivered_to_a_non_enemy_WHEN_evaluating_THEN_no_grudge_forms()
        {
            var toNeutral = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = false, CaptiveRegardForPlayer = 0};

            GrudgeProposal proposal = Tier2GrudgeRules.DeliveredPrisonerGrudge(toNeutral);

            proposal.Forms.Should().BeFalse("a ransom home or to a neutral party is a normal, honorable practice, not a betrayal");
            proposal.Magnitude.Should().Be(0);
        }

        // Mirrors DivorceConsequencePolicy.SpouseGrudgeLowRegardBonus's own low-regard-nurses-harder pattern:
        // a captive who already resented the player takes the betrayal harder, not milder, than one who was
        // neutral toward them.
        [Test]
        public void GIVEN_a_captive_who_already_disliked_the_player_WHEN_sold_to_their_enemy_THEN_the_magnitude_is_bumped()
        {
            var neutralRegard = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = true, CaptiveRegardForPlayer = 0};
            var lowRegard = new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = true, CaptiveRegardForPlayer = Tier2GrudgeRules.DeliveredPrisonerLowRegardThreshold};

            int neutralMagnitude = Tier2GrudgeRules.DeliveredPrisonerGrudge(neutralRegard).Magnitude;
            int lowRegardMagnitude = Tier2GrudgeRules.DeliveredPrisonerGrudge(lowRegard).Magnitude;

            lowRegardMagnitude.Should().Be(neutralMagnitude + Tier2GrudgeRules.DeliveredPrisonerLowRegardBonus);
        }

        // Fail-safe default: a missing facts object (e.g. an upstream lookup failure) must degrade to "no
        // grudge", never crash or silently default to Forms=true.
        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_delivered_prisoner_THEN_nothing_forms()
        {
            Tier2GrudgeRules.DeliveredPrisonerGrudge(null).Forms.Should().BeFalse();
        }

        // ── CaptiveAbuse ─────────────────────────────────────────────────────

        // Pins the engine-side CaptiveSceneIntent-to-severity mapping: Reward and Interrogation must map to
        // Severity 0 and never plant a grudge, or every benign or even positive captive scene would start
        // accruing resentment it never earned.
        [TestCase(0)]
        public void GIVEN_a_benign_captive_scene_severity_zero_WHEN_evaluating_THEN_nothing_forms(int severity)
        {
            var facts = new CaptiveAbuseFacts {Severity = severity, PlayerWasThePerpetrator = false};

            Tier2GrudgeRules.CaptiveAbuseGrudge(facts).Forms.Should().BeFalse("Reward/Interrogation register as benign, Severity 0");
        }

        // Pins the formation floor (CaptiveAbuseFormationSeverity=1): anything mild or worse must register.
        // KnownToPlayer is always true here, unlike SwayExposed below, because the victim lived the scene
        // firsthand, so there is nothing left to later discover or conceal.
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

        // Deliberately steep per the rule's own doc comment: this is the deepest grudge class, a VICTIM's
        // grudge, so a torture scene must always outweigh a rough interrogation, never read as comparable.
        [Test]
        public void GIVEN_rising_severity_WHEN_evaluating_THEN_magnitude_climbs_steeply()
        {
            int mild = Tier2GrudgeRules.CaptiveAbuseGrudge(new CaptiveAbuseFacts {Severity = 1, PlayerWasThePerpetrator = false}).Magnitude;
            int moderate = Tier2GrudgeRules.CaptiveAbuseGrudge(new CaptiveAbuseFacts {Severity = 2, PlayerWasThePerpetrator = false}).Magnitude;
            int grievous = Tier2GrudgeRules.CaptiveAbuseGrudge(new CaptiveAbuseFacts {Severity = 3, PlayerWasThePerpetrator = false}).Magnitude;

            mild.Should().BeLessThan(moderate);
            moderate.Should().BeLessThan(grievous);
        }

        // Distinguishes the player's OWN cruelty from merely being present for a captor's act (e.g. the
        // player's own captivity, or witnessing a companion-victim scene): directly doing it must weigh more
        // than standing by while someone else does.
        [Test]
        public void GIVEN_the_player_as_direct_perpetrator_WHEN_evaluating_THEN_the_magnitude_is_heaviest_at_any_given_severity()
        {
            var bystander = new CaptiveAbuseFacts {Severity = 2, PlayerWasThePerpetrator = false};
            var perpetrator = new CaptiveAbuseFacts {Severity = 2, PlayerWasThePerpetrator = true};

            int bystanderMagnitude = Tier2GrudgeRules.CaptiveAbuseGrudge(bystander).Magnitude;
            int perpetratorMagnitude = Tier2GrudgeRules.CaptiveAbuseGrudge(perpetrator).Magnitude;

            perpetratorMagnitude.Should().Be(bystanderMagnitude + Tier2GrudgeRules.CaptiveAbusePlayerPerpetratorBonus);
        }

        // The worst possible CaptiveAbuse case must still, in principle, be forgivable at high regard: only
        // GrudgeSource.ExecutedKin is unforgivable BY CONSTRUCTION (GrudgePolicy.RigidityAt's own design
        // rule). If this ceiling ever crept high enough to compute Vendetta-level rigidity on its own, even a
        // player who tortured a captive could never be reconciled with, contradicting that rule.
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

        // Fail-safe default, same reasoning as DeliveredPrisoner's null case above.
        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_captive_abuse_THEN_nothing_forms()
        {
            Tier2GrudgeRules.CaptiveAbuseGrudge(null).Forms.Should().BeFalse();
        }

        // ── SwayExposed ──────────────────────────────────────────────────────

        // The rumour reaching the target, not the sway's own strength, is this rule's real gate (per the
        // rule's own doc comment). A sway that never propagated must not plant a grudge for something the
        // target never learned of, however hard the sway itself hit.
        [Test]
        public void GIVEN_the_rumour_never_reached_the_target_WHEN_evaluating_THEN_nothing_forms()
        {
            var facts = new SwayExposedFacts {RumourReachedTarget = false, SwayMagnitude = 80};

            Tier2GrudgeRules.SwayExposedGrudge(facts).Forms.Should().BeFalse("an unheard sway leaves nothing to resent");
        }

        // KnownToPlayer is false BY DESIGN, mirroring the concealment already modelled for scheme-warned/
        // scheme-abetted grudges (ROADMAP's knowledge-gating). If this defaulted to known, the target would
        // openly confront the player over a sway they were never told they got caught doing, breaking the
        // "smooth until unmasked" surprise the assassin-register concealment relies on.
        [Test]
        public void GIVEN_the_rumour_reached_the_target_WHEN_evaluating_THEN_a_grudge_forms_hidden_from_the_player()
        {
            var facts = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = 20};

            GrudgeProposal proposal = Tier2GrudgeRules.SwayExposedGrudge(facts);

            proposal.Forms.Should().BeTrue();
            proposal.KnownToPlayer.Should().BeFalse("the target resents quietly until the grudge surfaces some other way, mirroring scheme-warned concealment");
        }

        // Pins the proportional scaling (half the sway's own strength, on top of a small base): a harder sway
        // should read as a deeper wound than a light one, not a flat magnitude regardless of severity.
        [Test]
        public void GIVEN_a_harder_sway_WHEN_evaluating_THEN_the_grudge_magnitude_tracks_the_sway_strength()
        {
            var lightSway = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = 10};
            var heavySway = new SwayExposedFacts {RumourReachedTarget = true, SwayMagnitude = 40};

            int lightMagnitude = Tier2GrudgeRules.SwayExposedGrudge(lightSway).Magnitude;
            int heavyMagnitude = Tier2GrudgeRules.SwayExposedGrudge(heavySway).Magnitude;

            heavyMagnitude.Should().BeGreaterThan(lightMagnitude);
        }

        // Fail-safe default, same reasoning as the other rules' null cases.
        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_sway_exposed_THEN_nothing_forms()
        {
            Tier2GrudgeRules.SwayExposedGrudge(null).Forms.Should().BeFalse();
        }

        // ── UnblessedMarriage ────────────────────────────────────────────────

        // Per the ROADMAP's 2026-07-06 resolution, only the PLAYER's OWN unblessed marriages ever plant this
        // source (NPC-NPC marriages default to blessed, since those weddings happen off-screen and a hidden
        // grudge there would be noise the player never perceives). KnownToPlayer is always true: the family
        // "made their disapproval plain" (ROADMAP wording), so there is nothing hidden to conceal.
        [Test]
        public void GIVEN_an_unblessed_marriage_still_standing_WHEN_evaluating_THEN_a_grudge_forms_known_to_the_player()
        {
            var facts = new UnblessedMarriageFacts {MarriageIsUnblessed = true, StillMarried = true};

            GrudgeProposal proposal = Tier2GrudgeRules.UnblessedMarriageGrudge(facts);

            proposal.Forms.Should().BeTrue();
            proposal.KnownToPlayer.Should().BeTrue("the family has made their disapproval plain");
            proposal.Magnitude.Should().Be(Tier2GrudgeRules.UnblessedMarriageMagnitude);
        }

        // MarriageIsUnblessed is the real gate, not StillMarried alone: a family that DID bless the union must
        // never accrue a grudge just because the marriage persists, or "still married" would be wrongly read
        // as "still disapproving".
        [Test]
        public void GIVEN_a_blessed_marriage_WHEN_evaluating_THEN_nothing_forms_even_if_still_married()
        {
            var facts = new UnblessedMarriageFacts {MarriageIsUnblessed = false, StillMarried = true};

            Tier2GrudgeRules.UnblessedMarriageGrudge(facts).Forms.Should().BeFalse();
        }

        // Without this StillMarried gate, a family whose disapproved marriage just ended, and whose reaction
        // DivorceConsequencePolicy.ResolveUnblessedFamilyReaction just flipped to Gratitude, would keep
        // re-proposing a live UnblessedMarriage grudge every tick, contradicting the very Gratitude nudge the
        // divorce was supposed to deliver.
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

        // Fail-safe default, same reasoning as the other rules' null cases.
        [Test]
        public void GIVEN_null_facts_WHEN_evaluating_unblessed_marriage_THEN_nothing_forms()
        {
            Tier2GrudgeRules.UnblessedMarriageGrudge(null).Forms.Should().BeFalse();
        }

        // ── Cross-cutting: every formed magnitude clears the formation floor ──

        // The anti-noise guardrail (ROADMAP: "a magnitude floor... never a bookkeeper's ledger") lives in
        // GrudgePolicy.ShouldForm, one hop downstream of these rules. If any Tier 2 rule's OWN lightest
        // forming case ever dipped below GrudgePolicy.MagnitudeFloor, that rule would report Forms=true here
        // yet be silently refused at the real formation gate, making it dead code in practice.
        [Test]
        public void GIVEN_every_tier_2_rules_lightest_forming_case_WHEN_evaluating_THEN_the_magnitude_clears_the_formation_floor()
        {
            // The lightest FORMING case is now a sale to the captive's enemy at high regard (no low-regard bonus);
            // a non-enemy delivery no longer forms at all (a normal ransom is not resented).
            int deliveredPrisonerLightest = Tier2GrudgeRules.DeliveredPrisonerGrudge(
                new DeliveredPrisonerFacts {DeliveredToEnemyOfCaptive = true, CaptiveRegardForPlayer = 100}).Magnitude;

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
