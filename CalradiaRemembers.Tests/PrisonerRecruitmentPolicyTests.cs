// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.3 recruit_prisoner: the pure gate deciding whether a captured HERO the player holds prisoner can be
// persuaded to switch sides and swear into the player's clan. This closes a specific player report (Nexus): a
// player convinced a captured noble to join them, the LLM narrated the switch, and nothing happened, because
// no executor existed (join_clan excludes prisoners; turn_nemesis is reserved for a tracked bandit nemesis).
//
// WHY IT MATTERS: the policy is consulted at BOTH surfaces around the LLM ("the prompt is advice, the bridge
// is law"): whether recruit_prisoner is taught at all (EncounterContextBuilder), and whether it actually
// executes (BannerlordGameStateBridge.ExecuteRecruitPrisoner, which frees the prisoner and swears them into
// the player's clan and party). Get a gate wrong and either a persuaded captured lord STILL does not join
// (the exact broken promise this ships to fix), or a low-regard enemy or a clan leader is talked into
// defecting, which is trivially exploitable (free enemy lords) or corrupts the save (an orphaned clan).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PrisonerRecruitmentPolicyTests
    {
        // The core positive case behind the player report: a captured hero the player holds, not a clan leader,
        // whose regard has reached the floor MUST be recruitable. If this refuses, the persuaded-captured-lord
        // promise the whole verb ships to keep stays broken (LLM narrates the switch, nothing happens).
        [Test]
        public void GIVEN_a_held_non_leader_prisoner_at_the_regard_floor_WHEN_evaluating_THEN_they_can_be_recruited()
        {
            PrisonerRecruitmentPolicy.Evaluate(true, false, PrisonerRecruitmentPolicy.RegardFloor)
                .Should().Be(PrisonerRecruitmentBlock.None);
            PrisonerRecruitmentPolicy.CanRecruit(true, false, PrisonerRecruitmentPolicy.RegardFloor)
                .Should().BeTrue();
        }

        // The floor is inclusive ("boundary belongs to the rule"): a prisoner who has earned exactly the deep-bond
        // mark must count as won over, not need one further point before the switch ever becomes possible.
        [Test]
        public void GIVEN_regard_exactly_at_the_floor_WHEN_evaluating_THEN_it_passes()
        {
            PrisonerRecruitmentPolicy.CanRecruit(true, false, 60).Should().BeTrue();
        }

        // The strong regard bar is what stops the verb being a trivial "capture any enemy lord, chat, keep him"
        // exploit. Just below the floor must refuse, so a captured enemy who does not deeply esteem the player
        // stays an enemy in the cell.
        [Test]
        public void GIVEN_regard_just_below_the_floor_WHEN_evaluating_THEN_regard_is_too_low()
        {
            PrisonerRecruitmentPolicy.Evaluate(true, false, PrisonerRecruitmentPolicy.RegardFloor - 1)
                .Should().Be(PrisonerRecruitmentBlock.RegardTooLow);
            PrisonerRecruitmentPolicy.CanRecruit(true, false, PrisonerRecruitmentPolicy.RegardFloor - 1)
                .Should().BeFalse();
        }

        // Anti-orphan guard, mirrored from join_clan (v1.34.1 hotfix): reassigning a clan LEADER's Clan to the
        // player's house leaves Clan.Leader / Kingdom.Leader dangling on the abandoned clan, a verified
        // save-corruption bug. The rule is categorical: it must refuse even a leader whose regard is maxed out.
        [Test]
        public void GIVEN_a_held_prisoner_who_leads_their_own_clan_WHEN_evaluating_THEN_they_cannot_be_recruited_even_at_full_regard()
        {
            PrisonerRecruitmentPolicy.Evaluate(true, true, 100)
                .Should().Be(PrisonerRecruitmentBlock.IsClanLeader);
            PrisonerRecruitmentPolicy.CanRecruit(true, true, 100).Should().BeFalse();
        }

        // The executor re-derives held-by-player from the live engine, but if custody ever reads false (the
        // prisoner was ransomed, escaped, or was never really the player's), the verb must refuse rather than
        // swear a free enemy lord into the clan. Held-by-player is the most fundamental gate, surfaced first.
        [Test]
        public void GIVEN_a_hero_the_player_does_not_hold_WHEN_evaluating_THEN_they_are_not_held_by_the_player()
        {
            PrisonerRecruitmentPolicy.Evaluate(false, false, 100)
                .Should().Be(PrisonerRecruitmentBlock.NotHeldByPlayer);
            PrisonerRecruitmentPolicy.CanRecruit(false, false, 100).Should().BeFalse();
        }

        // Which reason surfaces when several gates fail is real, player-facing behaviour (it grounds the refusal
        // line and any diagnostic). Pinning custody ahead of the leader and regard checks keeps that report
        // stable and honest: a prisoner the player does not even hold should never be told their regard is the
        // problem.
        [Test]
        public void GIVEN_a_prisoner_not_held_who_also_leads_a_clan_with_low_regard_WHEN_evaluating_THEN_custody_is_reported_first()
        {
            PrisonerRecruitmentPolicy.Evaluate(false, true, 0)
                .Should().Be(PrisonerRecruitmentBlock.NotHeldByPlayer);
        }
    }
}
