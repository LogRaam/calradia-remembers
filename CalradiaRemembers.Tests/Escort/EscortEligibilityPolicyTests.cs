// Code written by Gabriel Mailhot, 04/08/2026, reworked 10/09/2026.
// follow_me's start gate: EncounterContextBuilder.Build and FollowMeBehavior.TryStart both call
// EscortEligibilityPolicy with the SAME facts, so the prompt teaching and the bridge's own re-check can never
// quietly diverge ("the prompt is advice, the bridge is law").
//
// WHAT CHANGED, AND WHY, 10/09/2026. A player: "It feels like the AI using this like never follows me despite it
// agreeing to." He was right, and the gate added on 29/08/2026 was the reason. It refused any lord whose realm
// was at war with ANYONE, so that he stayed available for his liege's muster call. In an ordinary campaign nearly
// every kingdom is at war with somebody nearly all the time, so follow_me was almost never taught; and a verb
// that is not taught is ABSENT rather than refused, which a model reads as "nobody mentioned it" rather than as
// "forbidden". The lord agreed warmly, emitted nothing, and rode away.
//
// Gabriel's ruling: keep the intent, drop the blanket. What stops a lord is being COMMITTED to that war right
// now, an army or a siege or a battle, not the war existing somewhere on the map. And his condition on relaxing
// it, pinned next door in EscortContinuationPolicyTests: when the muster does reach a lord mid-escort, he leaves
// AND SAYS SO.
//
// The factionAtWar fact did not disappear, it changed jobs: it no longer refuses anything, it warns. A lord whose
// realm is at war tells the player up front that he may be called away, so the promise heard is the promise the
// world can keep.
//
// If these tests fail, either lords have stopped escorting anyone again, or one is about to promise an escort
// while his party is already spoken for by a siege.

#region

using CalradiaRemembers.Logic.Escort;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Escort
{
    [TestFixture]
    public sealed class EscortEligibilityPolicyTests
    {
        // The baseline every other test perturbs one guard away from: with every fact clean, the escort must be
        // allowed, or a player who did everything right could never get the promise honoured.
        [Test]
        public void GIVEN_every_guard_clear_WHEN_checked_THEN_the_escort_is_allowed()
        {
            EscortEligibilityPolicy.CanEscort(leadsParty: true, atWar: false, committedToWar: false, alreadyEscorting: false)
                                   .Should().BeTrue();
            EscortEligibilityPolicy.Refusal(true, false, false, false).Should().Be(EscortRefusal.None);
            EscortEligibilityPolicy.Describe(EscortRefusal.None, "Derthert").Should().BeEmpty();
        }

        // THE REPORTED BUG, pinned so it cannot come back: a lord of a realm at war, who is himself doing
        // nothing about that war, may ride. This single case is the difference between a feature players use and
        // a feature they only ever hear promised.
        [Test]
        public void GIVEN_his_realm_is_at_war_but_he_is_idle_WHEN_checked_THEN_he_may_still_ride()
        {
            EscortEligibilityPolicy.CanEscort(leadsParty: true, atWar: false, committedToWar: false, alreadyEscorting: false)
                                   .Should().BeTrue();

            // ...and he is expected to say so as he agrees, rather than only when he leaves.
            EscortEligibilityPolicy.ShouldWarnOfMuster(factionAtWar: true).Should().BeTrue();
            EscortEligibilityPolicy.ShouldWarnOfMuster(factionAtWar: false).Should().BeFalse();
        }

        // The author's concern, kept whole. A lord whose party is genuinely spoken for cannot promise it away:
        // his own AI would reclaim it within the day, making the escort unkeepable at the moment it was made.
        [Test]
        public void GIVEN_he_is_committed_to_a_war_right_now_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.Refusal(true, false, committedToWar: true, alreadyEscorting: false)
                                   .Should().Be(EscortRefusal.CommittedToWar);
        }

        // A companion already riding IN the player's party leads no party of their own, so there is nothing for
        // follow_me to do with them; that case is the separate verb ride_with_me.
        [Test]
        public void GIVEN_he_leads_no_party_of_his_own_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.Refusal(leadsParty: false, atWar: false, committedToWar: false, alreadyEscorting: false)
                                   .Should().Be(EscortRefusal.NoPartyOfHisOwn);
        }

        // No lord pledges to ride beside the very army his own realm may fight tomorrow.
        [Test]
        public void GIVEN_his_realm_is_at_war_with_the_player_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.Refusal(true, atWar: true, committedToWar: false, alreadyEscorting: false)
                                   .Should().Be(EscortRefusal.AtWarWithPlayer);
        }

        // One escort at a time. Double-booking would leave two records driving the same party.
        [Test]
        public void GIVEN_he_is_already_escorting_WHEN_checked_THEN_the_escort_is_refused()
        {
            EscortEligibilityPolicy.Refusal(true, false, false, alreadyEscorting: true)
                                   .Should().Be(EscortRefusal.AlreadyEscorting);
        }

        // Order is part of the rule. War with the player is the graver fact and must be named ahead of a siege,
        // so a player is told the thing that actually settles it rather than the first thing checked.
        [Test]
        public void GIVEN_several_doors_shut_at_once_WHEN_checked_THEN_the_gravest_is_the_one_named()
        {
            EscortEligibilityPolicy.Refusal(leadsParty: true, atWar: true, committedToWar: true, alreadyEscorting: true)
                                   .Should().Be(EscortRefusal.AtWarWithPlayer);
            EscortEligibilityPolicy.Refusal(leadsParty: false, atWar: true, committedToWar: true, alreadyEscorting: true)
                                   .Should().Be(EscortRefusal.NoPartyOfHisOwn);
        }

        // Every refusal must give the player a sentence, and a different one, or the whole point of naming the
        // door is lost: "he will not" and "he cannot" are different answers and a player deserves to know which.
        [Test]
        public void GIVEN_each_shut_door_WHEN_described_THEN_the_player_is_told_which_one()
        {
            var seen = new System.Collections.Generic.List<string>();

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
        public void GIVEN_no_name_to_use_WHEN_described_THEN_the_sentence_still_reads()
        {
            foreach (string name in new[] {null, "", "   "})
                EscortEligibilityPolicy.Describe(EscortRefusal.CommittedToWar, name)
                                       .Should().NotBeNullOrWhiteSpace().And.NotContain("  ");
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
