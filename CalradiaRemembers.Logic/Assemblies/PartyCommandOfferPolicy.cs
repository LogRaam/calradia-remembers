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
        ///   What the 1:1 path weighs before showing the LLM assign_party_role. Two facts rather than a
        ///   run of many, but named alongside its two siblings so the class reads one way throughout.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class AssignRoleFacts
        {
           /// <summary>They are one of the player's own companions.</summary>
           public bool NpcIsPlayerCompanion { get; set; }

           /// <summary>
           ///   They ride in the main party. A role acts on the party the member travels with, so a companion
           ///   outside it has no post to take.
           /// </summary>
           public bool NpcInMainParty { get; set; }
        }

        /// <summary>
        ///   assign_party_role: teach it only when the conversation NPC is one of the player's own companions who
        ///   currently rides in the main party (the council's own TargetInPlayerParty fact: a role acts on the
        ///   party the member travels with, so a companion outside it has no post to take). Re-checked at commit by
        ///   <c>CourtActionResolver.TryAssignPartyRole</c>.
        /// </summary>
        public static bool CanOfferAssignRole(AssignRoleFacts facts)
        {
           if (facts == null) return false;

           bool npcIsPlayerCompanion = facts.NpcIsPlayerCompanion;
           bool npcInMainParty = facts.NpcInMainParty;

           return npcIsPlayerCompanion && npcInMainParty;
        }

        /// <summary>
        ///   What the 1:1 path weighs before showing the LLM rejoin_party. FIVE booleans in a row where
        ///   NpcInMainParty is a DISQUALIFIER (a companion already riding along has nothing to rejoin)
        ///   while its neighbours are requirements, which is exactly the kind of run a swap survives.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class RejoinPartyFacts
        {
           /// <summary>They are one of the player's own companions.</summary>
           public bool NpcIsPlayerCompanion { get; set; }

           /// <summary>They are alive.</summary>
           public bool NpcAlive { get; set; }

           /// <summary>They are held prisoner, and a captive cannot travel.</summary>
           public bool NpcIsPrisoner { get; set; }

           /// <summary>They already ride in the main party, so there is nothing to rejoin.</summary>
           public bool NpcInMainParty { get; set; }

           /// <summary>They lead a company of their own, which rejoining would orphan.</summary>
           public bool NpcLeadsOwnCompany { get; set; }
        }

        /// <summary>
        ///   rejoin_party: teach it only when the conversation NPC is one of the player's own companions, alive and
        ///   free, currently OUT of the main party, and not leading a company of their own (the council's own
        ///   TargetOutOfPlayerParty fact, closing bug d). A companion already riding with the player has nothing to
        ///   rejoin; a prisoner cannot travel; a companion who leads their own party would orphan it. Re-checked at
        ///   commit by <c>CourtActionResolver.TryRejoinParty</c>.
        /// </summary>
        public static bool CanOfferRejoinParty(RejoinPartyFacts facts)
        {
           if (facts == null) return false;

           bool npcIsPlayerCompanion = facts.NpcIsPlayerCompanion;
           bool npcAlive = facts.NpcAlive;
           bool npcIsPrisoner = facts.NpcIsPrisoner;
           bool npcInMainParty = facts.NpcInMainParty;
           bool npcLeadsOwnCompany = facts.NpcLeadsOwnCompany;

           return npcIsPlayerCompanion && npcAlive && !npcIsPrisoner && !npcInMainParty && !npcLeadsOwnCompany;
        }

        /// <summary>
        ///   What the 1:1 path weighs before showing the LLM dispatch_mission.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class DispatchMissionFacts
        {
           /// <summary>
           ///   They may ride an errand for the player: a companion OR a member of the player's own clan, the
           ///   same population the mission behaviour accepts.
           /// </summary>
           public bool NpcCanRideForPlayer { get; set; }

           /// <summary>They ride in the main party, so they are at hand to send.</summary>
           public bool NpcInMainParty { get; set; }

           /// <summary>They are free to ride out right now, not already away on an errand.</summary>
           public bool NpcAvailableForErrand { get; set; }
        }

        /// <summary>
        ///   dispatch_mission: teach it only when the conversation NPC can ride an errand for the player
        ///   (<c>NpcCanRideForPlayer</c>, a player companion OR a member of the player's own clan, the
        ///   SAME population CompanionMissionBehavior.IsUsableCompanion accepts and the old gather_news gate offered,
        ///   so folding gather_news into dispatch_mission on 2026-08-05 dropped no one, including the player's own
        ///   clan kin who are not "companions"), rides in the main party, AND is free to ride out right now (a working
        ///   mission system that has them at hand, not already away on an errand). Re-checked at commit by
        ///   <c>CourtActionResolver.TryDispatchMission</c>, which itself defers to the mission behaviour's own guards.
        /// </summary>
        public static bool CanOfferDispatchMission(DispatchMissionFacts facts)
        {
           if (facts == null) return false;

           bool npcCanRideForPlayer = facts.NpcCanRideForPlayer;
           bool npcInMainParty = facts.NpcInMainParty;
           bool npcAvailableForErrand = facts.NpcAvailableForErrand;

           return npcCanRideForPlayer && npcInMainParty && npcAvailableForErrand;
        }
    }
}
