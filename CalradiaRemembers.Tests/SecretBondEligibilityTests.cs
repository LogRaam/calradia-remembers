// Code written by Gabriel Mailhot, 09/09/2026.
// What this pins: the ONE rule deciding whether a companion and the player may take up a hidden bond, and the
// reason it gives when they may not.
//
// Why it exists, and why the obvious test was not enough. A player reported that a companion declared their
// feelings while the game answered that intimacy was insufficient. The cause was one regard bar written as a
// literal 50 in three places, only one of which had moved onto the Relationship Pacing dial. Sharing the NUMBER
// fixed that and left the real fault standing: each component still carried its own LIST of preconditions, and
// checking them showed the lists had ALREADY drifted on two conditions nobody had noticed. The teaching gate
// required adult content on and refused an ended romance; the bridge guard checked neither. So a dead romance, or
// a campaign with adult content off, would refuse to teach the verb and then execute it anyway if the model
// emitted it untaught, which inverts this mod's doctrine: the prompt proposes, the bridge is law, and a law
// looser than the proposal is no law.
//
// Gabriel's rule, 2026-09-09: a proven regression sensitivity earns a MECHANISM, not merely a test. The mechanism
// is that no caller knows the rule any more, so no caller can hold a private opinion about it. These tests are
// what that one rule SAYS; cr.testall's secret_bond_bar_agrees is what proves the live callers still ask it.
//
// If these tests fail, one of the doors below has silently opened or shut for everyone at once, which is the
// price of centralising: the blast radius is now the whole arc, and that is exactly why it is written down here.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SecretBondEligibilityTests
    {
        private static SecretBondFacts Ready(int regard = 50) => new() {
            AdultContentEnabled = true,
            IsPlayerCompanion = true,
            HasRomanticProfile = true,
            OrientationCompatible = true,
            AlreadyBoundToPlayer = false,
            Status = RomanticStatus.Courting,
            Regard = regard,
            Warmth = RelationWarmth.Normal
        };

        // The ordinary yes. Everything else in this file is a door being shut, so this is the one case that proves
        // the doors are not all shut at once.
        [Test]
        public void GIVEN_a_willing_companion_at_the_bar_WHEN_judged_THEN_the_bond_may_be_taken_up()
        {
            SecretBondEligibility.Evaluate(Ready()).Should().Be(SecretBondVerdict.Eligible);
            SecretBondEligibility.Describe(SecretBondVerdict.Eligible, "Alys").Should().BeEmpty();
        }

        // The first of the two drifts this rule was written to end. The teaching gate honoured the adult-content
        // switch and the bridge did not, so a campaign with the switch OFF could still have the bond formed by an
        // untaught emission. A content setting a player chose must hold at every door, not merely the polite one.
        [Test]
        public void GIVEN_adult_content_is_off_WHEN_judged_THEN_the_whole_arc_is_closed()
        {
            SecretBondFacts facts = Ready();
            facts.AdultContentEnabled = false;

            SecretBondEligibility.Evaluate(facts).Should().Be(SecretBondVerdict.AdultContentOff);
        }

        // The second drift. An estranged or broken romance was refused by the teaching gate and accepted by the
        // bridge, so a dead romance could be raised from the grave as a secret bond. "No path back" has to mean
        // no path back through any door.
        [TestCase(RomanticStatus.Estranged)]
        [TestCase(RomanticStatus.Broken)]
        public void GIVEN_a_romance_that_is_over_WHEN_judged_THEN_it_cannot_be_taken_up_again(RomanticStatus status)
        {
            SecretBondFacts facts = Ready();
            facts.Status = status;

            SecretBondEligibility.Evaluate(facts).Should().Be(SecretBondVerdict.RomanceEnded);
        }

        // Already bound by any of its names. These were three separate flags and a status in three different
        // orders across the old copies, which is exactly the kind of near-agreement that hides a real gap.
        [Test]
        public void GIVEN_they_are_bound_to_the_player_already_WHEN_judged_THEN_it_says_so_however_that_bond_is_named()
        {
            SecretBondFacts byFlag = Ready();
            byFlag.AlreadyBoundToPlayer = true;

            SecretBondFacts bySecret = Ready();
            bySecret.Status = RomanticStatus.SecretLover;

            SecretBondFacts byCommitment = Ready();
            byCommitment.Status = RomanticStatus.Committed;

            SecretBondEligibility.Evaluate(byFlag).Should().Be(SecretBondVerdict.AlreadyBound);
            SecretBondEligibility.Evaluate(bySecret).Should().Be(SecretBondVerdict.AlreadyBound);
            SecretBondEligibility.Evaluate(byCommitment).Should().Be(SecretBondVerdict.AlreadyBound);
        }

        // The bar itself, and that it still follows the pacing dial. This is the condition the player reported,
        // and the boundary is inclusive: at the bar the confession is allowed, so the bond must be too.
        [TestCase(RelationWarmth.Normal)]
        [TestCase(RelationWarmth.Easier)]
        [TestCase(RelationWarmth.VeryEasy)]
        public void GIVEN_the_bond_is_one_short_of_the_bar_WHEN_judged_THEN_it_is_too_shallow_until_it_reaches_it(RelationWarmth warmth)
        {
            int bar = CompanionSecretAffectionPolicy.MinRegardFor(warmth);

            SecretBondFacts below = Ready(bar - 1);
            below.Warmth = warmth;
            SecretBondFacts at = Ready(bar);
            at.Warmth = warmth;

            SecretBondEligibility.Evaluate(below).Should().Be(SecretBondVerdict.BondTooShallow);
            SecretBondEligibility.Evaluate(at).Should().Be(SecretBondVerdict.Eligible);
        }

        // Order is part of the rule, not an accident of writing. A campaign-wide setting is reported before
        // anything personal, so a player who switched adult content off is told THAT, rather than something about
        // this particular companion that would send them looking in the wrong place.
        [Test]
        public void GIVEN_several_doors_are_shut_WHEN_judged_THEN_the_campaign_wide_one_is_named_first()
        {
            SecretBondFacts facts = Ready(0);
            facts.AdultContentEnabled = false;
            facts.IsPlayerCompanion = false;
            facts.OrientationCompatible = false;

            SecretBondEligibility.Evaluate(facts).Should().Be(SecretBondVerdict.AdultContentOff);
        }

        // Missing facts must not be read as permission. This is fed from live campaign reads that can be null
        // mid-transition, and the safe answer to "I do not know" is no.
        [Test]
        public void GIVEN_no_facts_at_all_WHEN_judged_THEN_it_refuses_rather_than_assuming()
        {
            SecretBondEligibility.Evaluate(null).Should().NotBe(SecretBondVerdict.Eligible);
            SecretBondEligibility.IsEligible(null).Should().BeFalse();
        }

        // Every closed door must give the player a sentence, and a different one. While this was a boolean the
        // bridge could only ever say the same thing, so "there is no spark between you" and "that is over between
        // you" were indistinguishable, and a player could not tell which of the two they were living.
        [Test]
        public void GIVEN_each_closed_door_WHEN_described_THEN_the_player_is_told_which_one()
        {
            var verdicts = new[] {
                SecretBondVerdict.AdultContentOff, SecretBondVerdict.NotACompanion,
                SecretBondVerdict.NoRomanticProfile, SecretBondVerdict.NoSpark,
                SecretBondVerdict.AlreadyBound, SecretBondVerdict.RomanceEnded,
                SecretBondVerdict.BondTooShallow
            };

            var seen = new System.Collections.Generic.List<string>();

            foreach (SecretBondVerdict v in verdicts)
            {
                string note = SecretBondEligibility.Describe(v, "Alys");

                note.Should().NotBeNullOrWhiteSpace();
                seen.Should().NotContain(note); // no two doors may read alike
                seen.Add(note);
            }
        }

        // A nameless character must still produce a sentence rather than one with a hole where a name should be.
        [Test]
        public void GIVEN_no_name_to_use_WHEN_described_THEN_the_sentence_still_reads()
        {
            foreach (string name in new[] {null, "", "   "})
                SecretBondEligibility.Describe(SecretBondVerdict.NoSpark, name)
                    .Should().NotBeNullOrWhiteSpace().And.NotContain("  ");
        }
    }
}
