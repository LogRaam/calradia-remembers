// Code written by Gabriel Mailhot, 21/08/2026.
// Manual action-injection menu (the button beside the purse and the quill): the player picks from the
// actions POSSIBLE RIGHT NOW against the conversation NPC, injected through the same bridge an LLM action
// would use. This is the pure gate deciding what appears: get a fact wrong here and the player either sees
// an entry the bridge will only refuse (a broken-feeling menu) or never sees one they earned (a feature that
// looks unbuilt). NPC-JUDGMENT actions (change_relation, sway_opinion, give_gold...) never enter this
// catalog at all (ManualActionCatalog's own header states that boundary); these tests only cover which of
// the PLAYER-DRIVEN entries a given snapshot of facts exposes or withholds.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
    [TestFixture]
    public sealed class ManualActionAvailabilityTests
    {
        /// <summary>Every fact defaults false/null: the empty conversation, nothing possible at all.</summary>
        private static ManualActionFacts Empty() => new ManualActionFacts();

        // A null snapshot must never throw: the menu button fails soft rather than crashing the chat window
        // if the mod-side resolver ever hands back nothing.
        [Test]
        public void GIVEN_a_null_facts_snapshot_WHEN_resolving_availability_THEN_nothing_is_offered()
        {
            ManualActionAvailability.GetAvailable(null).Should().BeEmpty();
        }

        // take_gold carries no engine precondition beyond a live conversation, which the menu button itself
        // already guarantees (ChatViewModel.IsActionMenuVisible only shows it in ChatMode.Conversation). So
        // an otherwise-empty snapshot still offers it: the player can always try to hand the NPC coin.
        [Test]
        public void GIVEN_an_otherwise_empty_conversation_WHEN_resolving_availability_THEN_take_gold_is_always_offered()
        {
            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(Empty());

            available.Should().Contain(ManualActionCatalog.TakeGold);
        }

        // give_item and spend_influence each carry their own real precondition (an item to hand over; the
        // NPC's own clan being needy per CourtActionResolver.TrySpendInfluence): an empty snapshot must
        // withhold both, or the menu would offer a gift the player cannot make / the bridge will only refuse.
        [Test]
        public void GIVEN_no_items_and_no_influence_need_WHEN_resolving_availability_THEN_give_item_and_spend_influence_are_withheld()
        {
            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(Empty());

            available.Should().NotContain(ManualActionCatalog.GiveItem);
            available.Should().NotContain(ManualActionCatalog.SpendInfluence);
        }

        [Test]
        public void GIVEN_the_player_carries_a_giveable_item_WHEN_resolving_availability_THEN_give_item_is_offered()
        {
            var facts = new ManualActionFacts {PlayerHasGiveableItems = true};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.GiveItem);
        }

        [Test]
        public void GIVEN_the_npcs_clan_can_accept_backing_WHEN_resolving_availability_THEN_spend_influence_is_offered()
        {
            var facts = new ManualActionFacts {CanSpendInfluence = true};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.SpendInfluence);
        }

        // The stake named in the task brief itself: a held prisoner exposes execute/free/recruit. All three
        // are gated on the SAME custody fact so they always appear or vanish together for a held captive.
        [Test]
        public void GIVEN_the_npc_is_a_prisoner_the_player_holds_and_eligible_to_turn_WHEN_resolving_availability_THEN_execute_free_and_recruit_prisoner_are_all_offered()
        {
            var facts = new ManualActionFacts {NpcIsPlayerPrisoner = true, CanRecruitPrisoner = true};

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().Contain(ManualActionCatalog.ExecutePrisoner);
            available.Should().Contain(ManualActionCatalog.FreePrisoner);
            available.Should().Contain(ManualActionCatalog.RecruitPrisoner);
        }

        // recruit_prisoner has its OWN regard/clan-leader gate on top of mere custody (PrisonerRecruitmentPolicy):
        // a captive too proud or too lowly-regarded to turn must still be executable/freeable, but never
        // offered a coat to turn that the bridge would refuse on sight (the empty-promise class of bug).
        [Test]
        public void GIVEN_a_held_prisoner_not_yet_eligible_to_turn_WHEN_resolving_availability_THEN_execute_and_free_are_offered_but_recruit_is_withheld()
        {
            var facts = new ManualActionFacts {NpcIsPlayerPrisoner = true, CanRecruitPrisoner = false};

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().Contain(ManualActionCatalog.ExecutePrisoner);
            available.Should().Contain(ManualActionCatalog.FreePrisoner);
            available.Should().NotContain(ManualActionCatalog.RecruitPrisoner);
        }

        // Without custody, none of the three prisoner entries may appear, even when every OTHER prisoner-side
        // fact happens to be true (a stale or mis-set eligibility flag must never surface a prisoner action
        // against someone who is not, in fact, the player's own captive).
        [Test]
        public void GIVEN_the_npc_is_not_the_players_prisoner_WHEN_resolving_availability_THEN_no_prisoner_action_is_offered()
        {
            var facts = new ManualActionFacts {NpcIsPlayerPrisoner = false, CanRecruitPrisoner = true};

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().NotContain(ManualActionCatalog.ExecutePrisoner);
            available.Should().NotContain(ManualActionCatalog.FreePrisoner);
            available.Should().NotContain(ManualActionCatalog.RecruitPrisoner);
        }

        // Companion-only entries (recall_companion, reassure_companion) must never appear for a non-companion
        // NPC even if the secondary fact (away on errand / discontented) happens to be set: the membership
        // check comes first, or a stray lord could be offered a companion-only deed.
        [Test]
        public void GIVEN_the_npc_is_not_a_companion_WHEN_resolving_availability_THEN_recall_and_reassure_are_withheld_even_if_the_secondary_fact_is_set()
        {
            var facts = new ManualActionFacts {
                NpcIsPlayerCompanion = false, NpcIsAwayOnErrand = true, CompanionIsDiscontented = true
            };

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().NotContain(ManualActionCatalog.RecallCompanion);
            available.Should().NotContain(ManualActionCatalog.ReassureCompanion);
        }

        [Test]
        public void GIVEN_a_companion_away_on_an_errand_WHEN_resolving_availability_THEN_recall_companion_is_offered()
        {
            var facts = new ManualActionFacts {NpcIsPlayerCompanion = true, NpcIsAwayOnErrand = true};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.RecallCompanion);
        }

        [Test]
        public void GIVEN_a_companion_riding_along_and_not_away_WHEN_resolving_availability_THEN_recall_companion_is_withheld()
        {
            var facts = new ManualActionFacts {NpcIsPlayerCompanion = true, NpcIsAwayOnErrand = false};

            ManualActionAvailability.GetAvailable(facts).Should().NotContain(ManualActionCatalog.RecallCompanion);
        }

        [Test]
        public void GIVEN_a_discontented_companion_WHEN_resolving_availability_THEN_reassure_companion_is_offered()
        {
            var facts = new ManualActionFacts {NpcIsPlayerCompanion = true, CompanionIsDiscontented = true};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.ReassureCompanion);
        }

        // follow_me / dismiss_escort and ride_with_me / part_ways are each two mutually-informed halves of
        // one arrangement: RideWithMeEligibilityPolicy and EscortEligibilityPolicy are built so a lord who
        // leads their own party can never qualify for BOTH pairs at once, but this gate only reads the facts
        // it is handed, so each half is asserted independently here exactly as the mod resolver computes it.
        [Test]
        public void GIVEN_a_lord_who_could_begin_escorting_the_player_WHEN_resolving_availability_THEN_follow_me_is_offered_and_dismiss_escort_is_not()
        {
            var facts = new ManualActionFacts {NpcCanEscortPlayer = true, NpcIsEscortingPlayer = false};

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().Contain(ManualActionCatalog.FollowMe);
            available.Should().NotContain(ManualActionCatalog.DismissEscort);
        }

        [Test]
        public void GIVEN_a_lord_already_escorting_the_player_WHEN_resolving_availability_THEN_dismiss_escort_is_offered_and_follow_me_is_not()
        {
            var facts = new ManualActionFacts {NpcCanEscortPlayer = false, NpcIsEscortingPlayer = true};

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().Contain(ManualActionCatalog.DismissEscort);
            available.Should().NotContain(ManualActionCatalog.FollowMe);
        }

        [Test]
        public void GIVEN_an_idle_lord_who_could_ride_along_WHEN_resolving_availability_THEN_ride_with_me_is_offered_and_part_ways_is_not()
        {
            var facts = new ManualActionFacts {NpcCanRideWithPlayer = true, NpcIsRidingWithPlayer = false};

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().Contain(ManualActionCatalog.RideWithMe);
            available.Should().NotContain(ManualActionCatalog.PartWays);
        }

        [Test]
        public void GIVEN_a_retainer_already_riding_along_WHEN_resolving_availability_THEN_part_ways_is_offered_and_ride_with_me_is_not()
        {
            var facts = new ManualActionFacts {NpcCanRideWithPlayer = false, NpcIsRidingWithPlayer = true};

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().Contain(ManualActionCatalog.PartWays);
            available.Should().NotContain(ManualActionCatalog.RideWithMe);
        }

        [Test]
        public void GIVEN_a_dispatchable_assignable_and_rejoinable_companion_WHEN_resolving_availability_THEN_all_three_party_command_entries_are_offered()
        {
            var facts = new ManualActionFacts {
                NpcCanBeDispatchedOnMission = true, NpcCanBeAssignedPartyRole = true, NpcCanRejoinParty = true
            };

            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(facts);

            available.Should().Contain(ManualActionCatalog.DispatchMission);
            available.Should().Contain(ManualActionCatalog.AssignPartyRole);
            available.Should().Contain(ManualActionCatalog.RejoinParty);
        }

        // The stake named in the task brief: a non-ruler must never see grant_fief (nor its governance
        // siblings), since a player who does not rule a kingdom has no crown fief to hand out or reclaim, no
        // vacant post to fill, and no coffers-of-state stipend to fund from that authority.
        [Test]
        public void GIVEN_a_player_who_does_not_rule_a_kingdom_WHEN_resolving_availability_THEN_every_governance_entry_is_withheld()
        {
            IReadOnlyList<ManualActionEntry> available = ManualActionAvailability.GetAvailable(Empty());

            available.Should().NotContain(ManualActionCatalog.GrantFief);
            available.Should().NotContain(ManualActionCatalog.RevokeFief);
            available.Should().NotContain(ManualActionCatalog.AppointGovernor);
            available.Should().NotContain(ManualActionCatalog.GrantStipend);
            available.Should().NotContain(ManualActionCatalog.GiveTroops);
        }

        [Test]
        public void GIVEN_a_ruling_player_with_a_vassal_and_a_grantable_fief_WHEN_resolving_availability_THEN_grant_fief_is_offered()
        {
            var facts = new ManualActionFacts {
                NpcCanBeGrantedFief = true, GrantableFiefs = new List<string> {"Praven"}
            };

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.GrantFief);
        }

        [Test]
        public void GIVEN_a_ruling_player_with_a_revocable_vassal_fief_WHEN_resolving_availability_THEN_revoke_fief_is_offered()
        {
            var facts = new ManualActionFacts {
                NpcCanHaveFiefRevoked = true, RevocableFiefs = new List<string> {"Tulga"}
            };

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.RevokeFief);
        }

        [Test]
        public void GIVEN_a_clan_member_who_could_be_seated_over_a_vacant_fief_WHEN_resolving_availability_THEN_appoint_governor_is_offered()
        {
            var facts = new ManualActionFacts {
                NpcCanBeAppointedGovernor = true, AppointableFiefs = new List<string> {"Marunath"}
            };

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.AppointGovernor);
        }

        [Test]
        public void GIVEN_a_companion_who_could_be_put_on_a_stipend_WHEN_resolving_availability_THEN_grant_stipend_is_offered()
        {
            var facts = new ManualActionFacts {NpcCanBeGrantedStipend = true};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.GrantStipend);
        }

        [Test]
        public void GIVEN_an_npc_whose_own_party_is_under_strength_WHEN_resolving_availability_THEN_give_troops_is_offered()
        {
            var facts = new ManualActionFacts {CanGiveTroops = true};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.GiveTroops);
        }

        [Test]
        public void GIVEN_a_faction_leader_the_player_can_mediate_with_WHEN_resolving_availability_THEN_mediate_peace_is_offered()
        {
            var facts = new ManualActionFacts {
                PlayerCanMediatePeace = true, MediatablePeaceFactions = new List<string> {"Sturgia"}
            };

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.MediatePeace);
        }

        [Test]
        public void GIVEN_a_settlement_notable_eligible_to_bequeath_their_post_WHEN_resolving_availability_THEN_recruit_notable_is_offered()
        {
            var facts = new ManualActionFacts {CanRecruitNotable = true};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.RecruitNotable);
        }

        // join_party is grounded on a real hiring price rather than a bare bool: no price means no hireable
        // wanderer, so the entry must vanish, never offer a hire the bridge would refuse for lack of a price.
        [Test]
        public void GIVEN_a_hireable_wanderer_with_a_resolved_asking_price_WHEN_resolving_availability_THEN_join_party_is_offered()
        {
            var facts = new ManualActionFacts {CompanionAskingPrice = 4000};

            ManualActionAvailability.GetAvailable(facts).Should().Contain(ManualActionCatalog.JoinParty);
        }

        [Test]
        public void GIVEN_no_resolved_hiring_price_WHEN_resolving_availability_THEN_join_party_is_withheld()
        {
            var facts = new ManualActionFacts {CompanionAskingPrice = null};

            ManualActionAvailability.GetAvailable(facts).Should().NotContain(ManualActionCatalog.JoinParty);
        }
    }
}
