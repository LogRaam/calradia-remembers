// Code written by Gabriel Mailhot, 05/08/2026.
// PartyCommandOfferPolicy is the 1:1 conversation TEACHING gate for the three party-command verbs
// (assign_party_role, rejoin_party, dispatch_mission) brought to the 1:1 pipeline alongside the council on
// 2026-08-05. What it decides: whether the LLM is even SHOWN each action this turn, from the plain engine facts
// the mod's EncounterContextBuilder reads about the ONE conversation NPC. What it feeds: PromptBuilder's
// Append*Offer gates. What breaks if it is wrong: it guards the empty-promise class the whole conditional-teaching
// contract exists to prevent. Each gate mirrors the council's own offering eligibility for its verb
// (ResolutionOfferingResolver: the TargetInPlayerParty fact for assign/dispatch, the TargetOutOfPlayerParty fact
// for rejoin), so teach a verb only when the bridge's own re-check (CourtActionResolver.TryAssignPartyRole /
// TryRejoinParty / TryDispatchMission) could actually honour it, and an NPC can never "agree" to something the
// game then silently declines. Each fact below removes one way that could happen.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class PartyCommandOfferPolicyTests
    {
        // assign_party_role: a party role (Scout, Engineer, Quartermaster, Surgeon) acts on the party the member
        // travels with, and only the player's own companions take these posts. The all-clear case must offer, or
        // the verb would never be taught in 1:1 and would stay council-only, the exact regression this slice fixes.
        [Test]
        public void GIVEN_a_companion_in_the_party_WHEN_offering_a_role_THEN_it_is_taught()
        {
            PartyCommandOfferPolicy.CanOfferAssignRole(npcIsPlayerCompanion: true, npcInMainParty: true)
                .Should().BeTrue();
        }

        // A role acts on the party the member rides in: a companion outside the main party has no post to take, and
        // the bridge (which re-checks PartyBelongedTo) would decline it. Without this fact the NPC could "accept" a
        // party role while away, an empty promise.
        [Test]
        public void GIVEN_a_companion_out_of_the_party_WHEN_offering_a_role_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferAssignRole(npcIsPlayerCompanion: true, npcInMainParty: false)
                .Should().BeFalse();
        }

        // The 1:1 offer is scoped to the player's OWN companions (the task's gate). A non-companion travelling with
        // the party is not offered a household post through this channel, so the offer never widens past who the
        // design intends to command.
        [Test]
        public void GIVEN_a_non_companion_in_the_party_WHEN_offering_a_role_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferAssignRole(npcIsPlayerCompanion: false, npcInMainParty: true)
                .Should().BeFalse();
        }

        // rejoin_party closes bug d: a companion seated/spoken to while AWAY from the party is otherwise unreachable
        // by any executor. The all-clear case (a live, free companion out of the party, leading no company of their
        // own) must offer, or an away companion could never be told to come back in conversation.
        [Test]
        public void GIVEN_an_away_free_companion_WHEN_offering_rejoin_THEN_it_is_taught()
        {
            PartyCommandOfferPolicy.CanOfferRejoinParty(
                npcIsPlayerCompanion: true, npcAlive: true, npcIsPrisoner: false, npcInMainParty: false, npcLeadsOwnCompany: false)
                .Should().BeTrue();
        }

        // A companion already riding with the player has nothing to rejoin: offering here would teach a no-op the
        // bridge declines (AlreadyInParty). This is the mirror of assign_party_role's in-party fact, on purpose.
        [Test]
        public void GIVEN_a_companion_already_in_the_party_WHEN_offering_rejoin_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferRejoinParty(
                npcIsPlayerCompanion: true, npcAlive: true, npcIsPrisoner: false, npcInMainParty: true, npcLeadsOwnCompany: false)
                .Should().BeFalse();
        }

        // A prisoner cannot travel to the player, so the bridge refuses a rejoin (ActorImprisoned). Teaching it here
        // would let a captured companion "agree" to return from a cell they cannot leave.
        [Test]
        public void GIVEN_an_imprisoned_companion_WHEN_offering_rejoin_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferRejoinParty(
                npcIsPlayerCompanion: true, npcAlive: true, npcIsPrisoner: true, npcInMainParty: false, npcLeadsOwnCompany: false)
                .Should().BeFalse();
        }

        // A companion who leads their OWN company cannot be lifted out of it without orphaning whoever they command:
        // v1 refuses this at the lift (LeadsOwnCompany), so the offer must withhold rather than promise a return the
        // bridge will not perform.
        [Test]
        public void GIVEN_a_companion_leading_their_own_company_WHEN_offering_rejoin_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferRejoinParty(
                npcIsPlayerCompanion: true, npcAlive: true, npcIsPrisoner: false, npcInMainParty: false, npcLeadsOwnCompany: true)
                .Should().BeFalse();
        }

        // A dead hero is no companion to recall; rejoin operates only on the living. Guards the degenerate case so a
        // stale reference can never surface a rejoin offer.
        [Test]
        public void GIVEN_a_dead_companion_WHEN_offering_rejoin_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferRejoinParty(
                npcIsPlayerCompanion: true, npcAlive: false, npcIsPrisoner: false, npcInMainParty: false, npcLeadsOwnCompany: false)
                .Should().BeFalse();
        }

        // dispatch_mission reuses the SAME in-party companion fact as assign_party_role, plus the errand-availability
        // fact (a working mission system with the companion at hand, not already away). The all-clear case must
        // offer, or a companion could never be sent on an errand in 1:1.
        [Test]
        public void GIVEN_an_available_companion_in_the_party_WHEN_offering_a_mission_THEN_it_is_taught()
        {
            PartyCommandOfferPolicy.CanOfferDispatchMission(
                npcIsPlayerCompanion: true, npcInMainParty: true, npcAvailableForErrand: true)
                .Should().BeTrue();
        }

        // A companion already away on an errand (or with no mission system present) cannot be dispatched again: the
        // bridge declines (AlreadyAway / NoMissionSystem). Teaching it here would double-send or promise an errand
        // the system cannot start.
        [Test]
        public void GIVEN_a_companion_not_free_to_ride_out_WHEN_offering_a_mission_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferDispatchMission(
                npcIsPlayerCompanion: true, npcInMainParty: true, npcAvailableForErrand: false)
                .Should().BeFalse();
        }

        // The errand acts on a companion in the player's own party; one outside it cannot be sent from it. Mirrors
        // assign_party_role's in-party requirement, so the two verbs offer on the same footing.
        [Test]
        public void GIVEN_a_companion_out_of_the_party_WHEN_offering_a_mission_THEN_it_is_withheld()
        {
            PartyCommandOfferPolicy.CanOfferDispatchMission(
                npcIsPlayerCompanion: true, npcInMainParty: false, npcAvailableForErrand: true)
                .Should().BeFalse();
        }
    }
}
