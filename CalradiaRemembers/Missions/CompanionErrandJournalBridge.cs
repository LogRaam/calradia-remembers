// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   Bridges <see cref="CompanionMission" /> (the persisted record, authority, owned by
   ///   <see cref="CompanionMissionBehavior" />'s <c>_active</c> list) and <see cref="CompanionErrandQuest" />
   ///   (the <c>QuestBase</c> mirror in the vanilla quest journal). The bridge is the ONLY place that
   ///   opens, updates, or closes journal mirrors; <see cref="CompanionMissionBehavior" /> calls these
   ///   methods after its own lifecycle transitions. Modeled directly on
   ///   <see cref="CalradiaRemembers.Quests.QuestJournalBridge" />.
   ///   Every entry point is try/catch-guarded: a journal failure must never break the underlying errand.
   ///   Mirrors are found by the mission's stable Id (not by reconstructing the Hero-keyed quest id), so
   ///   closing still works even in the rare case the companion Hero reference is unavailable (e.g. they
   ///   died while away).
   ///   Anti-stuck guarantee: <see cref="ReconcileOnSessionLaunch" /> is called at every session launch and
   ///   force-closes any mirror whose mission Id is no longer in <see cref="CompanionMissionBehavior" />'s
   ///   active list.
   /// </summary>
   public static class CompanionErrandJournalBridge
   {
      /// <summary>Closes the mirror: the companion made it home and reported in.</summary>
      public static void CloseMirrorReturned(CompanionMission mission, Hero companion)
      {
         if (mission == null) return;

         try
         {
            CompanionErrandQuest mirror = FindMirror(mission.Id);
            if (mirror == null) return;

            mirror.UpdateStatus(CompanionErrandJournalText.BuildReturnedLine(mission.CompanionName));
            mirror.CloseAsReturned();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ERRAND-JOURNAL] CloseMirrorReturned failed for {mission.CompanionName}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Closes the mirror: the companion was recalled and abandoned the errand.
      ///   <paramref name="reason" /> is player-facing (e.g. "as your letter ordered") and may be null.
      /// </summary>
      public static void CloseMirrorRecalled(CompanionMission mission, Hero companion, string reason)
      {
         if (mission == null) return;

         try
         {
            CompanionErrandQuest mirror = FindMirror(mission.Id);
            if (mirror == null) return;

            string line = CompanionErrandJournalText.BuildRecalledLine(mission.CompanionName, reason);
            mirror.UpdateStatus(line);
            mirror.CloseAsRecalled(line);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ERRAND-JOURNAL] CloseMirrorRecalled failed for {mission.CompanionName}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Creates and starts a mirror for a just-dispatched errand. Requires a live companion Hero.</summary>
      public static void OpenMirror(CompanionMission mission, Hero companion)
      {
         if (mission == null || companion is not {IsAlive: true}) return;

         try
         {
            var mirror = new CompanionErrandQuest(mission, companion);
            ModLog.Info($"[ERRAND-JOURNAL] opened mirror: {mirror.StringId}");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ERRAND-JOURNAL] OpenMirror failed for {companion.Name}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Anti-stuck pass. Runs at every session launch (wired by
      ///   <see cref="CalradiaRemembers.Behaviors.NpcMemoryCampaignBehavior.OnSessionLaunched" />). Closes
      ///   any live <see cref="CompanionErrandQuest" /> whose mission Id is no longer in
      ///   <see cref="CompanionMissionBehavior" />'s active list (e.g. the companion died while away, or the
      ///   errand resolved on a session where the mirror failed to close for some other reason).
      /// </summary>
      public static void ReconcileOnSessionLaunch()
      {
         if (Campaign.Current?.QuestManager == null) return;
         CompanionMissionBehavior behavior = CompanionMissionBehavior.Instance;
         if (behavior == null) return;

         var closed = 0;
         foreach (CompanionErrandQuest q in Campaign.Current.QuestManager.Quests
                                                    .OfType<CompanionErrandQuest>()
                                                    .ToList()) // snapshot, we may mutate during iteration
         {
            if (behavior.FindActive(q.MissionId) != null) continue;

            ModLog.Info($"[ERRAND-JOURNAL] closing stale mirror '{q.StringId}' at session launch, errand no longer active.");
            q.CloseAsRecalled("The errand is no longer tracked.");
            closed++;
         }

         if (closed > 0)
            ModLog.Info($"[ERRAND-JOURNAL] reconciled {closed} stale mirror(s) at session launch.");
      }

      /// <summary>Pushes an updated status line to the live mirror for a phase change (no-op if none exists).</summary>
      public static void UpdateMirror(CompanionMission mission, Hero companion)
      {
         if (mission == null) return;

         try
         {
            CompanionErrandQuest mirror = FindMirror(mission.Id);
            mirror?.UpdateStatus(CompanionErrandJournalText.BuildStatusLine(
               mission.Type, CompanionErrandQuest.ToJournalPhase(mission.Phase), mission.CompanionName, mission.DestinationName));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ERRAND-JOURNAL] UpdateMirror failed for {mission.CompanionName}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #region private

      private static CompanionErrandQuest FindMirror(string missionId)
      {
         if (string.IsNullOrEmpty(missionId) || Campaign.Current?.QuestManager == null) return null;

         return Campaign.Current.QuestManager.Quests
                        .OfType<CompanionErrandQuest>()
                        .FirstOrDefault(q => q.MissionId == missionId);
      }

      #endregion
   }
}
