// Code written by Gabriel Mailhot, 08/09/2026.
// Guards Gabriel's ruling on the "his success cancelled his quest" report (v2.5.3, 2026-09-07): a player who
// gets a quest target out of a settlement dungeon must have the rescue credited, whatever way the captivity
// actually ended, except a ransom or a release bundled with a peace treaty (and death, which frees nobody).
// QuestDeedTracker.OnHeroPrisonerReleased consults this policy before stamping an outstanding RescuePrisoner
// quest satisfied. If this rule drifts, either the original bug returns (a real rescue goes uncredited and
// later reads as the target vanishing), or the opposite fault appears (a ransom or a peace release wrongly
// pays out a rescue quest the player did nothing to earn).
//
// The reporter's exact route is the vanilla PRISON BREAK (enter the keep's Prison menu, fight or sneak past
// the guards, reach the exit): on success SandBox's PrisonBreakCampaignBehavior calls
// EndCaptivityAction.ApplyByEscape, which the engine reports as ReleasedAfterEscape, mapped here to
// CaptivityEndReason.Escape. Conquering the settlement holding the target (EndCaptivityAction.ApplyByReleasedAfterBattle)
// is the other credited route, alongside the pre-existing field-battle case. Ransom and Peace map onto the
// engine's own ApplyByRansom/ApplyByPeace, so the ruling's two exclusions already match the engine's own
// vocabulary.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestRescueCreditPolicyTests
    {
        // THE PLAYER REPORT ITSELF: getting a captive out of a dungeon (ByChoice, the reason the real
        // free_prisoner bridge action reports) must credit the rescue. This is the exact case that used to
        // go uncredited and later void the quest with "gone from the world" wording.
        [Test]
        public void GIVEN_a_captive_freed_by_choice_WHEN_judging_credit_THEN_it_credits()
        {
            QuestRescueCreditPolicy.CreditsPlayerRescue(CaptivityEndReason.ByChoice).Should().BeTrue();
        }

        // A battle freeing the captive already credited before this fix (QuestDeedTracker.MatchBattle reading
        // BattleFacts.FreedPrisonerHeroIds); this policy must keep agreeing, or the two paths would now disagree
        // on the same deed depending only on which one happens to run first.
        [Test]
        public void GIVEN_a_captive_freed_by_battle_WHEN_judging_credit_THEN_it_credits()
        {
            QuestRescueCreditPolicy.CreditsPlayerRescue(CaptivityEndReason.Battle).Should().BeTrue();
        }

        // THE REPORTER'S EXACT ROUTE: a staged vanilla prison break (fight or sneak past the keep's guards to
        // the exit) IS an escape as far as the engine is concerned (ApplyByEscape -> ReleasedAfterEscape), so
        // crediting Escape is the whole point, not a loophole. Honest consequence, not an oversight: the
        // engine never dispatches WHO staged the break (its own "facilitator" parameter is dropped before the
        // event fires), so a captive who slips free entirely unaided reports the identical reason and credits
        // just the same. Gabriel chose that deliberately: benefit of the doubt to the player everywhere, having
        // just seen what the stricter "only a proven player deed" reading cost a real one.
        [Test]
        public void GIVEN_a_captive_who_escaped_WHEN_judging_credit_THEN_it_credits()
        {
            QuestRescueCreditPolicy.CreditsPlayerRescue(CaptivityEndReason.Escape).Should().BeTrue();
        }

        // Compensation is not named as an exclusion by the ruling ("EXCEPT when the release was a ransom or
        // came with a peace treaty"), so it falls into "everything else" and credits.
        [Test]
        public void GIVEN_a_captive_freed_by_compensation_WHEN_judging_credit_THEN_it_credits()
        {
            QuestRescueCreditPolicy.CreditsPlayerRescue(CaptivityEndReason.Compensation).Should().BeTrue();
        }

        // THE TWO NAMED EXCLUSIONS: Gabriel's ruling is explicit that a ransom and a peace-bundled release
        // never credit, because in both cases the player did not free anyone, coin or a treaty did.
        [TestCase(CaptivityEndReason.Ransom)]
        [TestCase(CaptivityEndReason.Peace)]
        public void GIVEN_a_ransom_or_peace_release_WHEN_judging_credit_THEN_it_does_NOT_credit(CaptivityEndReason reason)
        {
            QuestRescueCreditPolicy.CreditsPlayerRescue(reason).Should().BeFalse();
        }

        // A captive who died was never rescued: there is no one left to free, so crediting this would satisfy
        // a "free them" task with the one outcome that makes freeing them impossible.
        [Test]
        public void GIVEN_a_captive_who_died_WHEN_judging_credit_THEN_it_does_NOT_credit()
        {
            QuestRescueCreditPolicy.CreditsPlayerRescue(CaptivityEndReason.Death).Should().BeFalse();
        }

        // FUTURE-SAFETY: an engine reason this mod's host mapping does not yet recognise (a value TaleWorlds
        // adds in some later patch) must land on a deliberate answer, never fall through unhandled. Per the
        // ruling's own framing ("benefit of the doubt goes to the player everywhere else"), the safe default
        // for an unknown reason is to credit, exactly like every named reason except the two exclusions.
        [Test]
        public void GIVEN_an_unrecognised_future_reason_WHEN_judging_credit_THEN_it_credits()
        {
            QuestRescueCreditPolicy.CreditsPlayerRescue(CaptivityEndReason.Other).Should().BeTrue();
        }
    }
}
