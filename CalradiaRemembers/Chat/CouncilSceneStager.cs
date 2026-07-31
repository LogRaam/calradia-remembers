// Code written by Gabriel Mailhot, 31/07/2026.
// Council scene hosting, Phase 1: hosts the council as a 3D scene by entering the settlement's real Location
// (lordshall or tavern) instead of opening a map overlay, which smears/risks VRAM. Reuses the proven
// TownDuelBehavior recipe (Arm → CreateAndOpenMissionController → consume at Mission.AfterStart), but WITHOUT
// any agent injection: we spawn NOTHING, the Location's own ambient nobles are the backdrop, and the council
// members are UI-only (chat witnesses), exactly as the map-overlay council already presents them. Enter at
// TryBeginSettlementCouncil (map/menu, no mission yet, so the block-check is clean), then open the council over
// the resulting MissionScreen at OpenCouncilIfPending (Mission.AfterStart), with the InScene bypass, since we
// are intentionally inside our own council mission by then.

#region

using System;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>What came of trying to stage a settlement council; consumed by the caller for a player-facing message.</summary>
   public enum CouncilStageOutcome
   {
      /// <summary>The Location mission is opening; the council itself opens once it has fully started.</summary>
      Staged,

      /// <summary>The player is not inside a settlement with a usable interior, or is at sea/on the map with no scene.</summary>
      NotInSettlement,

      /// <summary>A settlement scene exists, but the council itself cannot open right now (chat open, captive, nobody seated).</summary>
      Blocked
   }

   /// <summary>Stages a council INSIDE a settlement's real Location scene, with no agent injection.</summary>
   public static class CouncilSceneStager
   {
      // Set when a settlement council is armed, consumed by OpenCouncilIfPending once the mission has fully
      // started. One player, one council at a time, and a council never spans a save, so static process-only
      // state is correct here, exactly as TownDuelBehavior's own pending state is.
      private static CouncilScope? _pendingScope;

      /// <summary>
      ///   Arms a settlement council of <paramref name="scope" /> and opens the settlement's lordshall (if the
      ///   player's clan owns it) or tavern (otherwise), falling back to whichever interior actually exists. Must
      ///   be called with the player inside a settlement and no mission running. Returns a human line via
      ///   <paramref name="message" /> for every outcome; the council itself is opened later, from the
      ///   Mission.AfterStart postfix. On any failure the pending state is cleared, so a botched launch cannot
      ///   leave a council armed against the next mission the player opens.
      /// </summary>
      public static CouncilStageOutcome TryBeginSettlementCouncil(CouncilScope scope, out string message)
      {
         message = "";

         if (Settlement.CurrentSettlement == null || Mission.Current != null || PlayerEncounter.LocationEncounter == null)
         {
            message = "A settlement council needs the player inside a town or castle.";

            return CouncilStageOutcome.NotInSettlement;
         }

         string preBlocked = ChatManager.PreCouncilBlockReason();

         if (preBlocked != null)
         {
            message = preBlocked;

            return CouncilStageOutcome.Blocked;
         }

         if (CouncilRosterResolver.Seated(scope).Count == 0)
         {
            message = $"Nobody is seated for a {scope} council right now.";

            return CouncilStageOutcome.Blocked;
         }

         bool owns = Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
         bool hasHall = LocationComplex.Current?.GetLocationWithId("lordshall") != null;
         bool hasTavern = LocationComplex.Current?.GetLocationWithId("tavern") != null;
         CouncilVenue venue = CouncilVenuePolicy.Choose(owns, hasHall, hasTavern);

         if (venue == CouncilVenue.None)
         {
            message = "This settlement has no hall or tavern to convene in.";

            return CouncilStageOutcome.NotInSettlement;
         }

         try
         {
            _pendingScope = scope;

            Location loc = LocationComplex.Current.GetLocationWithId(CouncilVenuePolicy.LocationId(venue));
            PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(loc);
            ModLog.Info($"[COUNCIL] settlement council armed ({scope}) in the {venue}; opening the scene.");

            message = venue == CouncilVenue.Hall
               ? "You gather them in your hall."
               : "You gather them in the tavern.";

            return CouncilStageOutcome.Staged;
         }
         catch (Exception ex)
         {
            _pendingScope = null;
            ModLog.Error($"[COUNCIL] failed to open the settlement scene: {ex.GetType().Name}: {ex.Message}");
            message = $"Could not open the scene: {ex.GetType().Name} (see the log).";

            return CouncilStageOutcome.NotInSettlement;
         }
      }

      /// <summary>
      ///   Called from the Harmony postfix on <c>Mission.AfterStart</c> (the one reliable "the mission has FULLY
      ///   started" seam, shared with the town duel and street ambush): opens the pending council over the now-live
      ///   MissionScreen, bypassing the InScene refusal since this scene IS the council. A no-op for every
      ///   ordinary mission.
      /// </summary>
      public static void OpenCouncilIfPending(Mission mission)
      {
         try
         {
            if (_pendingScope == null) return;

            CouncilScope scope = _pendingScope.Value;
            _pendingScope = null; // consume before opening, so a failure below cannot re-trigger on the next mission

            ChatManager.OpenForCouncil(scope, allowInScene: true);
            ModLog.Info($"[COUNCIL] scene-hosted council opened ({scope}) after AfterStart.");
         }
         catch (Exception ex)
         {
            _pendingScope = null;
            ModLog.Error($"[COUNCIL] opening scene-hosted council after AfterStart failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
