// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: the engine side of ResolutionWorldFacts. Reads live campaign state (a vacant
// governable fief, whether any seated member is available or rides in the player's own party) into the facts
// ResolutionEligibilityPolicy judges, so the set of resolution kinds a council turn TEACHES the LLM can never
// promise a deed the lift cannot honour (the exact empty-promise class the whole catalogue exists to prevent).
// This is the populator ResolutionCatalog and ResolutionEligibilityPolicy were built for; appoint_governor,
// assign_party_role, rejoin_party and, mirroring assign_party_role's own TargetInPlayerParty fact exactly (no
// dedicated plumbing needed here, see ResolutionCatalog's DispatchOnMission entry), dispatch_mission are wired
// today (ResolutionCatalog.IsExecutable), so the remaining scoped kinds never surface here regardless of facts,
// and this reader stays minimal until they land.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>Reads live campaign state into the OFFERED executable resolution kind keys for a council turn.</summary>
   public static class ResolutionOfferingResolver
   {
      /// <summary>
      ///   The kind keys (e.g. "quest", "appoint_governor", "assign_party_role", "rejoin_party") a council of
      ///   <paramref name="scope" /> may currently teach the LLM. <paramref name="anySeatedMemberAvailable" />
      ///   should be true once the council has at least one filled seat: the roster already applies
      ///   CouncilRosterPolicy's own availability gate before a member is seated (see CouncilRosterResolver.Seated),
      ///   so a seated member IS the TargetAvailable case ResolutionEligibilityPolicy asks about, and there is no
      ///   separate per-member re-check to do here. <paramref name="anySeatedMemberInPlayerParty" /> is the
      ///   narrower TargetInPlayerParty fact assign_party_role needs: a seat alone does not prove party
      ///   membership (a companion may be seated while governing a town), so the caller resolves each seat's live
      ///   Hero and checks PartyBelongedTo for real (see ChatViewModel.AnySeatedMemberInPlayerParty).
      ///   <paramref name="anySeatedCompanionOutOfPlayerParty" /> is the symmetric TargetOutOfPlayerParty fact
      ///   rejoin_party needs, closing bug d (a clan companion seated at the table while away is otherwise
      ///   unreachable by any executor, see ChatViewModel.AnySeatedCompanionOutOfPlayerParty). Never throws: any
      ///   engine read failure degrades to no vacant fief, so a failing lookup only WITHHOLDS a motion, never
      ///   over-offers one the lift cannot honour.
      /// </summary>
      public static IReadOnlyList<string> OfferedKindKeys(CouncilScope scope, bool anySeatedMemberAvailable, bool anySeatedMemberInPlayerParty = false, bool anySeatedCompanionOutOfPlayerParty = false)
      {
         try
         {
            var facts = new ResolutionWorldFacts(
               targetInPlayerParty: anySeatedMemberInPlayerParty,
               targetAvailable: anySeatedMemberAvailable,
               playerHasVacantFief: HasVacantGovernableFief(),
               playerCanAffordStipend: false,
               missionAvailable: false,
               targetOutOfPlayerParty: anySeatedCompanionOutOfPlayerParty);

            return ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(scope, facts).ToList();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL] ResolutionOfferingResolver.OfferedKindKeys failed: {ex.GetType().Name}: {ex.Message}");

            return Array.Empty<string>();
         }
      }

      // A town or castle the player's clan still owns, with no governor seated. Villages carry no Governor slot
      // at all (CouncilLift.SettleAppointGovernor's own re-check at the lift already excludes them the same
      // way), so restricting to IsTown/IsCastle here keeps the offering and the lift's own rule in lockstep.
      private static bool HasVacantGovernableFief()
      {
         Clan clan = Clan.PlayerClan;
         if (clan == null) return false;

         return Settlement.All != null && Settlement.All.Any(s =>
            (s.IsTown || s.IsCastle) && s.OwnerClan == clan && s.Town?.Governor == null);
      }
   }
}
