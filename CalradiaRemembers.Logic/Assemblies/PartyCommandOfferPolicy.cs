// Code written by Gabriel Mailhot, 05/08/2026.
// Party-command verbs (assign_party_role, rejoin_party, dispatch_mission) brought to the 1:1 conversation
// pipeline alongside the council (2026-08-05, the batch after the appoint_governor reference slice). This is the
// 1:1 TEACHING gate for all three: it reduces the SAME eligibility the council's own offering proves before it
// teaches these at the table (ResolutionOfferingResolver's TargetInPlayerParty fact for assign_party_role and
// dispatch_mission, its TargetOutOfPlayerParty fact for rejoin_party) to the plain engine facts the mod's
// EncounterContextBuilder reads about the ONE conversation NPC. Pure and total, mirroring
// GovernorAppointmentPolicy.CanOfferAppointment: the bridge re-checks every one of these again at commit time
// (CourtActionResolver.TryAssignPartyRole / TryRejoinParty / TryDispatchMission), so a true answer here only ever
// OFFERS a teaching, it never seals a deed.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   The 1:1 conversation TEACHING gates for the three party-command verbs. Each mirrors the council's own
    ///   offering eligibility for that verb, reduced to the facts EncounterContextBuilder can read about the single
    ///   conversation NPC. The bridge re-validates every fact at commit time, so a true answer only decides whether
    ///   the LLM is even SHOWN the action this turn.
    /// </summary>
    public static class PartyCommandOfferPolicy
    {
        /// <summary>
        ///   assign_party_role: teach it only when the conversation NPC is one of the player's own companions who
        ///   currently rides in the main party (the council's own TargetInPlayerParty fact: a role acts on the
        ///   party the member travels with, so a companion outside it has no post to take). Re-checked at commit by
        ///   <c>CourtActionResolver.TryAssignPartyRole</c>.
        /// </summary>
        public static bool CanOfferAssignRole(bool npcIsPlayerCompanion, bool npcInMainParty)
            => npcIsPlayerCompanion && npcInMainParty;

        /// <summary>
        ///   rejoin_party: teach it only when the conversation NPC is one of the player's own companions, alive and
        ///   free, currently OUT of the main party, and not leading a company of their own (the council's own
        ///   TargetOutOfPlayerParty fact, closing bug d). A companion already riding with the player has nothing to
        ///   rejoin; a prisoner cannot travel; a companion who leads their own party would orphan it. Re-checked at
        ///   commit by <c>CourtActionResolver.TryRejoinParty</c>.
        /// </summary>
        public static bool CanOfferRejoinParty(bool npcIsPlayerCompanion, bool npcAlive, bool npcIsPrisoner, bool npcInMainParty, bool npcLeadsOwnCompany)
            => npcIsPlayerCompanion && npcAlive && !npcIsPrisoner && !npcInMainParty && !npcLeadsOwnCompany;

        /// <summary>
        ///   dispatch_mission: teach it only when the conversation NPC is a companion in the main party (the SAME
        ///   TargetInPlayerParty fact assign_party_role reads) AND is free to ride out right now (a working mission
        ///   system that has them at hand, not already away on an errand). Re-checked at commit by
        ///   <c>CourtActionResolver.TryDispatchMission</c>, which itself defers to the mission behaviour's own guards.
        /// </summary>
        public static bool CanOfferDispatchMission(bool npcIsPlayerCompanion, bool npcInMainParty, bool npcAvailableForErrand)
            => npcIsPlayerCompanion && npcInMainParty && npcAvailableForErrand;
    }
}
