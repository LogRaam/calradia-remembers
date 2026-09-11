// Code written by Gabriel Mailhot, 04/08/2026, reworked 10/09/2026.
// follow_me's start gate: EncounterContextBuilder.Build and FollowMeBehavior.TryStart both ask
// EscortEligibilityPolicy the same question from the same facts, so the prompt teaching and the bridge's own
// re-check can never quietly diverge ("the prompt is advice, the bridge is law").
//
// WHAT CHANGED, AND WHY. A player: "It feels like the AI using this like never follows me despite it agreeing
// to." A gate added 29/08/2026 refused any lord whose realm was at war with ANYONE, so he stayed available for
// his liege's muster call. In an ordinary campaign nearly every kingdom is at war with somebody nearly all the
// time, so follow_me was almost never taught; and a verb that is not taught is ABSENT rather than refused, which
// a model reads as "nobody mentioned it" rather than as "forbidden". The lord agreed warmly, emitted nothing, and
// rode away. Gabriel's ruling: what stops a lord is being COMMITTED to that war right now, an army or a siege or
// a battle, not the war existing somewhere on the map. RealmAtWar changed jobs from refusing to WARNING.
//
// AND WHY THESE TESTS NOW NAME FACTS RATHER THAN ARGUMENTS. Gabriel, 10/09/2026: "Le code doit pouvoir changer,
// evoluer, s'ameliorer, mais le comportement attendu ne doit jamais se degrader." This file was the evidence for
// that rule and the debt it left. Widening the gate changed a positional signature, and seven assertions here had
// to be rewritten, including ones whose behaviour had not moved at all: "a lord who leads no party of his own is
// refused" was as true after as before. They broke on ARITY AND PARAMETER NAMES, which is implementation binding
// wearing its least obvious disguise.
//
// So each test below builds only the facts it actually cares about and says nothing about the others. Adding,
// removing or reordering a fact now costs nothing to any test that never depended on it. That is the property,
// and it is the reason this rewrite should be the last one these tests need.
//
// If these tests fail, either lords have stopped escorting anyone again, or one is about to promise an escort
// while his party is already spoken for by a siege.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Escort;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Escort
{
    [TestFixture]
    public sealed class EscortEligibilityPolicyTests
    {
        /// <summary>A lord free to ride: everything else here changes exactly one fact away from this.</summary>
        private static EscortFacts AFreeLord() => new() {LeadsOwnParty = true};

        // The baseline every other test perturbs one fact away from: with nothing in the way, the escort must be
        // allowed, or a player who did everything right could never get the promise honoured.
        [Test]
        public void GIVEN_a_lord_with_nothing_in_his_way_WHEN_asked_THEN_he_will_ride_with_the_player()
        {
            EscortEligibilityPolicy.CanEscort(AFreeLord()).Should().BeTrue();
            EscortEligibilityPolicy.Refusal(AFreeLord()).Should().Be(EscortRefusal.None);
            EscortEligibilityPolicy.Describe(EscortRefusal.None, "Derthert").Should().BeEmpty();
        }

        // THE REPORTED BUG, pinned so it cannot come back: a lord of a realm at war, who is himself doing nothing
        // about that war, may ride. This one case is the difference between a feature players use and a feature
        // they only ever hear promised.
        [Test]
        public void GIVEN_his_realm_is_at_war_but_he_is_idle_WHEN_asked_THEN_he_may_still_ride()
        {
            EscortFacts idle = AFreeLord();
            idle.RealmAtWar = true;

            EscortEligibilityPolicy.CanEscort(idle).Should().BeTrue();
        }

        // ...and he is expected to say so as he agrees, rather than only when the muster takes him away.
        [Test]
        public void GIVEN_his_realm_is_at_war_WHEN_he_agrees_THEN_he_warns_the_player_he_may_be_called_away()
        {
            EscortFacts atWarSomewhere = AFreeLord();
            atWarSomewhere.RealmAtWar = true;

            EscortEligibilityPolicy.ShouldWarnOfMuster(atWarSomewhere).Should().BeTrue();
            EscortEligibilityPolicy.ShouldWarnOfMuster(AFreeLord()).Should().BeFalse();
        }

        // The author's concern, kept whole. A lord whose party is genuinely spoken for cannot promise it away:
        // his own AI would reclaim it within the day, making the escort unkeepable the moment it was made.
        [Test]
        public void GIVEN_he_is_committed_to_a_war_today_WHEN_asked_THEN_he_will_not_promise_his_men()
        {
            EscortFacts committed = AFreeLord();
            committed.CommittedToWar = true;

            EscortEligibilityPolicy.Refusal(committed).Should().Be(EscortRefusal.CommittedToWar);
        }

        // A companion already riding IN the player's party leads no party of their own, so there is nothing for
        // follow_me to do with them; that case is the separate verb ride_with_me.
        [Test]
        public void GIVEN_he_leads_no_company_of_his_own_WHEN_asked_THEN_there_is_nothing_to_escort_with()
        {
            EscortEligibilityPolicy.Refusal(new EscortFacts()).Should().Be(EscortRefusal.NoPartyOfHisOwn);
        }

        // No lord pledges to ride beside the very army his own realm may fight tomorrow.
        [Test]
        public void GIVEN_their_two_realms_are_at_war_WHEN_asked_THEN_he_will_not_ride_beside_that_army()
        {
            EscortFacts enemy = AFreeLord();
            enemy.AtWarWithPlayer = true;

            EscortEligibilityPolicy.Refusal(enemy).Should().Be(EscortRefusal.AtWarWithPlayer);
        }

        // One escort at a time. Double-booking would leave two records driving the same party.
        [Test]
        public void GIVEN_he_is_already_riding_escort_WHEN_asked_THEN_he_will_not_promise_it_twice()
        {
            EscortFacts busy = AFreeLord();
            busy.AlreadyEscorting = true;

            EscortEligibilityPolicy.Refusal(busy).Should().Be(EscortRefusal.AlreadyEscorting);
        }

        // Order is part of the rule and it is about what a player deserves to hear: war with them settles it, so
        // it must be named ahead of a siege, and having no company at all comes before either.
        [Test]
        public void GIVEN_several_reasons_at_once_WHEN_asked_THEN_he_names_the_one_that_actually_settles_it()
        {
            EscortEligibilityPolicy.Refusal(new EscortFacts {
                LeadsOwnParty = true, AtWarWithPlayer = true, CommittedToWar = true, AlreadyEscorting = true
            }).Should().Be(EscortRefusal.AtWarWithPlayer);

            EscortEligibilityPolicy.Refusal(new EscortFacts {
                AtWarWithPlayer = true, CommittedToWar = true, AlreadyEscorting = true
            }).Should().Be(EscortRefusal.NoPartyOfHisOwn);
        }

        // Every refusal must give the player a sentence, and a different one: "he will not" and "he cannot" are
        // different answers, and a player deserves to know which of the two they just heard.
        [Test]
        public void GIVEN_each_reason_he_declines_WHEN_told_to_the_player_THEN_each_reads_differently()
        {
            var seen = new List<string>();

            foreach (EscortRefusal refusal in new[] {
                         EscortRefusal.NoPartyOfHisOwn, EscortRefusal.AtWarWithPlayer,
                         EscortRefusal.CommittedToWar, EscortRefusal.AlreadyEscorting
                     })
            {
                string note = EscortEligibilityPolicy.Describe(refusal, "Derthert");

                note.Should().NotBeNullOrWhiteSpace();
                seen.Should().NotContain(note);
                seen.Add(note);
            }
        }

        // A nameless lord must still produce a sentence rather than one with a hole where a name should be.
        [Test]
        public void GIVEN_no_name_to_use_WHEN_told_to_the_player_THEN_the_sentence_still_reads()
        {
            foreach (string name in new[] {null, "", "   "})
                EscortEligibilityPolicy.Describe(EscortRefusal.CommittedToWar, name)
                                       .Should().NotBeNullOrWhiteSpace().And.NotContain("  ");
        }

        // Facts we could not read are not permission. These come from live campaign reads that can be null
        // mid-transition, and the safe answer to "I do not know" is that no promise is made.
        [Test]
        public void GIVEN_nothing_could_be_read_about_him_WHEN_asked_THEN_no_promise_is_made()
        {
            EscortEligibilityPolicy.CanEscort(null).Should().BeFalse();
            EscortEligibilityPolicy.Refusal(null).Should().Be(EscortRefusal.NoPartyOfHisOwn);
            EscortEligibilityPolicy.ShouldWarnOfMuster(null).Should().BeFalse();
        }

        // The agreed term is fixed on purpose, so the promise a player hears has one unambiguous length and the
        // model has no second figure to invent.
        [Test]
        public void GIVEN_an_escort_is_agreed_WHEN_asked_how_long_THEN_it_is_the_one_fixed_term()
        {
            EscortEligibilityPolicy.TermDays.Should().Be(15);
        }
    }
}
