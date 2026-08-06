// Code written by Gabriel Mailhot, 06/08/2026.
// Documents which 1:1 [ACTION] verbs a single ordinary conversation turn is deemed to have TAUGHT, given the
// turn's eligibility facts. This is the pure core of the v1.34.2 log-only execution audit (Pattern 1): the
// action bridge runs any parsed [ACTION] without checking it was taught, so the host builds this set from the
// live EncounterContext and logs (never refuses) an execution that is absent from it. Because it is
// observability, not enforcement, the set is deliberately GENEROUS: a false "taught" only quiets the audit,
// while a wrong "not taught" would cry wolf, so every mapped gate here mirrors the SDK PromptBuilder's own
// teaching condition. Get the base set wrong and the audit spams on every routine turn; get a gate wrong and a
// legitimate action is logged as a hallucination that a future ENFORCING version might then wrongly refuse.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
    [TestFixture]
    public sealed class TaughtActionSetTests
    {
        // The always-on base vocabulary the SDK teaches EVERY ordinary turn (LlmServices.BuildActionVocabulary):
        // if any of these were ever judged "not taught", the audit would fire on the most common actions of all
        // (a regard shift, a coin transfer) and drown the signal it exists to surface.
        [TestCase("change_relation")]
        [TestCase("give_gold")]
        [TestCase("take_gold")]
        [TestCase("end_conversation")]
        [TestCase("witness_leaves")]
        [TestCase("request_privacy")]
        public void GIVEN_a_bare_turn_with_no_eligibility_WHEN_the_set_is_built_THEN_the_base_action_is_present(string actionType)
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts());

            set.Should().Contain(actionType);
        }

        // The action-type casing in the LLM's raw output is not guaranteed, and the bridge matches
        // case-insensitively, so the audit membership check must too, or a "Change_Relation" token would be
        // logged as untaught purely on casing.
        [Test]
        public void GIVEN_a_taught_action_in_different_casing_WHEN_membership_is_checked_THEN_it_still_matches()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts());

            set.Contains("Change_Relation").Should().BeTrue();
        }

        // give_item is taught in every ordinary turn where the player is NOT the captive (PromptBuilder.AppendGiveItem
        // skips it for a captive, who has no inventory access). It is not in the base vocabulary, so it must ride
        // on the not-captive fact rather than always being present.
        [Test]
        public void GIVEN_the_player_is_not_captive_WHEN_the_set_is_built_THEN_give_item_is_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {PlayerIsCaptive = false});

            set.Should().Contain("give_item");
        }

        // A captive player cannot hand items over, so teaching give_item would be a promise the bridge refuses:
        // it must drop out of the taught set the moment the player is this NPC's prisoner.
        [Test]
        public void GIVEN_the_player_is_captive_WHEN_the_set_is_built_THEN_give_item_is_not_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {PlayerIsCaptive = true});

            set.Should().NotContain("give_item");
        }

        // The reference personal-command verb (2026-08-05): appoint_governor is taught ONLY when the host proved
        // the NPC can be seated as a governor this turn. Present when the fact is set...
        [Test]
        public void GIVEN_the_npc_can_be_appointed_governor_WHEN_the_set_is_built_THEN_appoint_governor_is_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {NpcCanBeAppointedGovernor = true});

            set.Should().Contain("appoint_governor");
        }

        // ...and absent when it is not, or the audit would never notice a hallucinated governorship (the exact
        // empty-promise class the whole gate exists to prevent).
        [Test]
        public void GIVEN_the_npc_cannot_be_appointed_governor_WHEN_the_set_is_built_THEN_appoint_governor_is_not_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {NpcCanBeAppointedGovernor = false});

            set.Should().NotContain("appoint_governor");
        }

        // dispatch_mission absorbed the legacy gather_news verb (2026-08-05): a model still emitting the old token
        // must map onto the same gate, or an errand the host genuinely offered would be logged as a hallucination.
        [Test]
        public void GIVEN_the_npc_can_be_dispatched_on_mission_WHEN_the_set_is_built_THEN_both_dispatch_mission_and_gather_news_are_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {NpcCanBeDispatchedOnMission = true});

            set.Should().Contain("dispatch_mission");
            set.Should().Contain("gather_news");
        }

        // join_party rides on the recruitable-companion fact (CompanionAskingPrice), the same gate PromptBuilder
        // uses to render the RECRUITMENT section: without it the NPC is a lord/notable the bridge would never hire.
        [Test]
        public void GIVEN_a_recruitable_companion_WHEN_the_set_is_built_THEN_join_party_is_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {CompanionRecruitable = true});

            set.Should().Contain("join_party");
        }

        // The court-verb family (give_influence, lend_troops, sway_opinion, ...) is composed as ONE
        // ExtraActionTeachings bundle, each verb self-gated inside it. For a log-only audit the presence of the
        // bundle is read as teaching the whole family, so a legitimately-composed court verb is never flagged.
        [Test]
        public void GIVEN_the_court_verb_bundle_is_offered_WHEN_the_set_is_built_THEN_the_family_is_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {CourtVerbsOffered = true});

            set.Should().Contain("give_influence");
            set.Should().Contain("lend_troops");
            set.Should().Contain("sway_opinion");
        }

        // With the bundle absent, those same court verbs must fall out, or the audit could never catch a court
        // verb executed in a conversation that never taught one.
        [Test]
        public void GIVEN_the_court_verb_bundle_is_not_offered_WHEN_the_set_is_built_THEN_the_family_is_not_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {CourtVerbsOffered = false});

            set.Should().NotContain("give_influence");
            set.Should().NotContain("lend_troops");
        }

        // The freedom bargain and its grim alternatives are taught only when the player actually HOLDS this NPC
        // captive (PlayerStatus == NpcIsCaptive, set by the bridge from real custody). All three ride that one fact.
        [Test]
        public void GIVEN_the_player_holds_this_npc_captive_WHEN_the_set_is_built_THEN_the_prisoner_bargain_verbs_are_taught()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts {PlayerHoldsThisNpcCaptive = true});

            set.Should().Contain("free_prisoner");
            set.Should().Contain("execute_prisoner");
            set.Should().Contain("turn_nemesis");
        }

        // The whole point of the audit: a verb no fact enables (here appoint_governor on a bare turn) is genuinely
        // untaught and MUST be absent, so an execution of it surfaces as a log line for a future enforcing pass.
        [Test]
        public void GIVEN_a_bare_turn_WHEN_the_set_is_built_THEN_a_clearly_untaught_action_is_absent()
        {
            ISet<string> set = TaughtActionSet.ForTurn(new TaughtActionFacts());

            set.Should().NotContain("appoint_governor");
            set.Should().NotContain("join_clan");
            set.Should().NotContain("marry");
        }

        // The base/flow family must be reported by IsNeverAudited so the host suppresses any audit line for them
        // even if a set-membership edge ever slipped: they are taught every turn and are not world-changing deeds.
        [TestCase("change_relation")]
        [TestCase("give_gold")]
        [TestCase("take_gold")]
        [TestCase("request_privacy")]
        [TestCase("end_conversation")]
        [TestCase("witness_leaves")]
        [TestCase("retire")]
        [TestCase(null)]
        public void GIVEN_a_base_or_flow_type_WHEN_asked_if_never_audited_THEN_it_is_true(string actionType)
        {
            TaughtActionSet.IsNeverAudited(actionType).Should().BeTrue();
        }

        // A genuine world-changing verb must NOT be blanket-suppressed, or the audit could never report it: only
        // the base/flow family is exempt, everything else is subject to the taught-set check.
        [Test]
        public void GIVEN_a_world_changing_verb_WHEN_asked_if_never_audited_THEN_it_is_false()
        {
            TaughtActionSet.IsNeverAudited("appoint_governor").Should().BeFalse();
        }
    }
}
