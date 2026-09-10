// Code written by Gabriel Mailhot, 04/08/2026, reworked 10/09/2026.
// FollowMeBehavior.Advance asks EscortContinuationPolicy every daily tick whether an escort is over.
//
// WHAT CHANGED, AND WHY, 10/09/2026. A player: "It feels like the AI using this like never follows me despite it
// agreeing to." The start gate was the cause (see EscortEligibilityPolicyTests), and relaxing it came with a
// condition from Gabriel: "si il accepte de suivre le joueur et apres il y a un appel pour joindre une armee, ce
// Lord devrait expliquer au joueur qu'il doit le quitter pour suivre l'appel de son seigneur."
//
// That condition is what makes the relaxation honest rather than merely permissive. A lord of a warring realm may
// now ride along, so his war must be able to call him back, and leaving without a word would be a worse version
// of the very complaint above: a player watching a promise evaporate with nobody explaining anything.
//
// So an escort no longer ends on a bool. It ends for a NAMED reason, and every reason has a sentence, including
// the four that already existed and were all silent: the term running out, the lord dying, the lord being taken,
// and war breaking out between the realms. Any of them could leave a player wondering where his escort went.
//
// If these tests fail, an escort is about to end without the player being told, or to run on through a siege.

#region

using CalradiaRemembers.Logic.Escort;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Escort
{
    [TestFixture]
    public sealed class EscortContinuationPolicyTests
    {
        private static EscortStandingFacts Riding(int daysRemaining = 5) => new() {DaysRemaining = daysRemaining};

        // The ordinary day: nothing has happened, the term has time left, the lord rides on. Everything else
        // here is an ending, so this is the case proving they are not all endings.
        [Test]
        public void GIVEN_a_quiet_day_with_time_left_WHEN_judged_THEN_the_escort_continues()
        {
            EscortContinuationPolicy.Resolve(Riding()).Should().Be(EscortEndReason.None);
            EscortContinuationPolicy.ShouldEnd(Riding()).Should().BeFalse();
            EscortContinuationPolicy.Describe(EscortEndReason.None, "Derthert").Should().BeEmpty();
        }

        // Gabriel's condition, pinned. This is the reason the enum exists.
        [Test]
        public void GIVEN_the_muster_reaches_him_mid_escort_WHEN_judged_THEN_he_leaves_for_his_lieges_call()
        {
            EscortStandingFacts summoned = Riding();
            summoned.SummonedToWar = true;

            EscortContinuationPolicy.Resolve(summoned).Should().Be(EscortEndReason.SummonedByLiege);
            EscortContinuationPolicy.Describe(EscortEndReason.SummonedByLiege, "Derthert")
                                    .Should().Contain("Derthert").And.Contain("muster");
        }

        // And the whole point of naming it: the player is TOLD. A silent departure is the complaint this change
        // answers, so every ending must produce a sentence, not merely the interesting one.
        [Test]
        public void GIVEN_any_way_an_escort_can_end_WHEN_described_THEN_the_player_is_told_something()
        {
            var endings = new[] {
                EscortEndReason.TermExpired, EscortEndReason.LordDead, EscortEndReason.LordCaptured,
                EscortEndReason.WarWithPlayer, EscortEndReason.SummonedByLiege
            };

            var seen = new System.Collections.Generic.List<string>();

            foreach (EscortEndReason reason in endings)
            {
                string note = EscortContinuationPolicy.Describe(reason, "Derthert");

                note.Should().NotBeNullOrWhiteSpace();
                seen.Should().NotContain(note); // no two endings may read alike
                seen.Add(note);
            }
        }

        // The agreed term running out is the expected ending and the only happy one, so it must not read like a
        // betrayal: the lord finishes what he promised and goes back to his own business.
        [TestCase(0)]
        [TestCase(-1)]
        public void GIVEN_the_term_has_run_out_WHEN_judged_THEN_the_escort_ends_as_agreed(int daysRemaining)
        {
            EscortContinuationPolicy.Resolve(Riding(daysRemaining)).Should().Be(EscortEndReason.TermExpired);
            EscortContinuationPolicy.Describe(EscortEndReason.TermExpired, "Derthert")
                                    .Should().Contain("as long as he agreed");
        }

        // A dead lord, a captured lord, and a lord whose realm has declared war are each an ending in their own
        // right, whatever the term still says.
        [Test]
        public void GIVEN_the_lord_is_lost_to_us_WHEN_judged_THEN_the_escort_ends_whatever_the_term_says()
        {
            EscortStandingFacts dead = Riding(10);
            dead.IsDead = true;
            EscortStandingFacts captured = Riding(10);
            captured.IsCaptured = true;
            EscortStandingFacts atWar = Riding(10);
            atWar.IsAtWarWithPlayer = true;

            EscortContinuationPolicy.Resolve(dead).Should().Be(EscortEndReason.LordDead);
            EscortContinuationPolicy.Resolve(captured).Should().Be(EscortEndReason.LordCaptured);
            EscortContinuationPolicy.Resolve(atWar).Should().Be(EscortEndReason.WarWithPlayer);
        }

        // Order is part of the rule and it is about dignity as much as correctness: a dead lord is not reported
        // as having answered a muster, and a captured one is not reported as having run out of time.
        [Test]
        public void GIVEN_several_endings_at_once_WHEN_judged_THEN_the_gravest_is_the_one_reported()
        {
            var everything = new EscortStandingFacts {
                DaysRemaining = 0, IsDead = true, IsCaptured = true, IsAtWarWithPlayer = true, SummonedToWar = true
            };

            EscortContinuationPolicy.Resolve(everything).Should().Be(EscortEndReason.LordDead);

            var takenAndSummoned = new EscortStandingFacts {DaysRemaining = 5, IsCaptured = true, SummonedToWar = true};
            EscortContinuationPolicy.Resolve(takenAndSummoned).Should().Be(EscortEndReason.LordCaptured);
        }

        // A lord the campaign can no longer find is gone, not riding. Reading unknowable as "carry on" would
        // leave an escort record driving a party that does not exist.
        [Test]
        public void GIVEN_nothing_can_be_read_about_him_WHEN_judged_THEN_the_escort_ends_rather_than_continuing()
        {
            EscortContinuationPolicy.Resolve(null).Should().Be(EscortEndReason.LordDead);
            EscortContinuationPolicy.ShouldEnd(null).Should().BeTrue();
        }

        // A nameless lord still produces a sentence rather than one with a hole in it, since these are built
        // from live campaign reads that can be null mid-transition.
        [Test]
        public void GIVEN_no_name_to_use_WHEN_described_THEN_the_sentence_still_reads()
        {
            foreach (string name in new[] {null, "", "   "})
                EscortContinuationPolicy.Describe(EscortEndReason.SummonedByLiege, name)
                                        .Should().NotBeNullOrWhiteSpace().And.NotContain("  ");
        }

        // The four-fact overload is kept so nothing that already called it changed behaviour the day the reason
        // was added. Same facts, same answer.
        [Test]
        public void GIVEN_the_older_four_fact_call_WHEN_used_THEN_it_answers_exactly_as_it_always_did()
        {
            EscortContinuationPolicy.ShouldEnd(5, false, false, false).Should().BeFalse();
            EscortContinuationPolicy.ShouldEnd(0, false, false, false).Should().BeTrue();
            EscortContinuationPolicy.ShouldEnd(10, true, false, false).Should().BeTrue();
            EscortContinuationPolicy.ShouldEnd(10, false, true, false).Should().BeTrue();
            EscortContinuationPolicy.ShouldEnd(10, false, false, true).Should().BeTrue();
        }
    }
}
