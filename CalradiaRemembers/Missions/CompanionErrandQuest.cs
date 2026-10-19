// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>
   ///   A thin <see cref="QuestBase" /> wrapper around a companion errand (<see cref="CompanionMission" />)
   ///   that makes it visible in Bannerlord's native quest journal, modeled directly on
   ///   <see cref="CalradiaRemembers.Quests.CalradiaRemembersQuest" />. Read-only: it carries no dialogs and
   ///   no reward, it exists purely so "who did I send where" is legible without the console. The journal
   ///   entry is a MIRROR of the data owned by <see cref="CompanionMissionBehavior" />'s <c>_active</c> list;
   ///   it is never authoritative. All lifecycle decisions happen there; <see cref="CompanionErrandJournalBridge" />
   ///   is the only place that opens/updates/closes this mirror.
   ///   Anti-stuck guarantee: <see cref="CompanionErrandJournalBridge.ReconcileOnSessionLaunch" /> force-closes
   ///   any mirror whose underlying errand is no longer active, on every session launch.
   ///   Saveable fields: only <see cref="_companionId" /> and <see cref="_missionId" />. Everything else
   ///   (title, log lines) is pushed in by the bridge from the live <see cref="CompanionMission" />.
   /// </summary>
   public sealed class CompanionErrandQuest : QuestBase
   {
      // Saveable field IDs (do not reuse).
      [SaveableField(1)]
      private readonly string _companionId;

      [SaveableField(2)]
      private readonly string _missionId;

      /// <summary>Creates a new mirror for a just-dispatched <paramref name="mission" />.</summary>
      public CompanionErrandQuest(CompanionMission mission, Hero companion)
         : base(
            BuildQuestId(companion, mission.Id),
            companion,
            // Stored directly as the engine due time: never times out. CompanionMissionBehavior
            // owns the real deadline (LingerUntilDay / RecallOrderDay), this reflects it read-only.
            CampaignTime.Never,
            0) // no gold reward: this is not a quest the player can complete for a payout
      {
         _companionId = companion.StringId;
         _missionId = mission.Id;

         AddLog(new TextObject(CompanionErrandJournalText.BuildStatusLine(
            mission.Type, ToJournalPhase(mission.Phase), mission.CompanionName, mission.DestinationName)));
         SetDialogs();
         StartQuest();
      }

      // We have no engine countdown to show: the errand's real timing lives in CompanionMissionBehavior.
      public override bool IsRemainingTimeHidden => true;

      /// <summary>
      ///   LOAD-BEARING: without a special type, QuestManager.OnGameLoaded cancels every non-finalized quest
      ///   that has no live Issue attached, so the mirror died (with a cancel banner) at every reload.
      ///   See <see cref="Quests.CalradiaRemembersQuest.SpecialQuestType" /> for the full engine analysis.
      /// </summary>
      public override string SpecialQuestType => "CalradiaRemembers";

      public override TextObject Title => new(CompanionErrandJournalText.BuildTitle(ResolveCompanionName()));

      /// <summary>The dispatched companion's StringId, exposed so a diagnostic can cross-check the mirror.</summary>
      internal string CompanionId => _companionId;

      /// <summary>The mission's stable Id, used by <see cref="CompanionErrandJournalBridge" /> to find this mirror.</summary>
      internal string MissionId => _missionId;

      /// <summary>Closes the mirror: the companion was recalled and abandoned the errand (no report).</summary>
      public void CloseAsRecalled(string reason)
      {
         try { CompleteQuestWithCancel(new TextObject(reason)); }
         catch (Exception ex) { ModLog.Error($"[ERRAND-JOURNAL] CloseAsRecalled failed: {ex.Message}"); }
      }

      /// <summary>Closes the mirror: the companion made it home and reported in.</summary>
      public void CloseAsReturned()
      {
         try { CompleteQuestWithSuccess(); }
         catch (Exception ex) { ModLog.Error($"[ERRAND-JOURNAL] CloseAsReturned failed: {ex.Message}"); }
      }

      /// <summary>Pushes a new status line to the journal log; called on every phase change and at closing.</summary>
      public void UpdateStatus(string logLine)
      {
         if (!string.IsNullOrWhiteSpace(logLine)) AddLog(new TextObject(Mapping.ShownText.Of(logLine)));
      }

      internal static string BuildQuestId(Hero companion, string missionId)
         => $"cr_errand_{companion.StringId}_{missionId}";

      /// <summary>Maps the mod's own phase enum onto the pure-Logic one the text builder understands.</summary>
      internal static CompanionErrandPhase ToJournalPhase(CompanionMissionPhase phase)
      {
         switch (phase)
         {
            case CompanionMissionPhase.Outbound: return CompanionErrandPhase.Outbound;
            case CompanionMissionPhase.Lingering: return CompanionErrandPhase.Lingering;
            case CompanionMissionPhase.Returning: return CompanionErrandPhase.Returning;
            case CompanionMissionPhase.AwaitingReunion: return CompanionErrandPhase.AwaitingReunion;
            default: return CompanionErrandPhase.Outbound;
         }
      }

      protected override void InitializeQuestOnGameLoad()
      {
         // Reconnect happens by mission Id lookup (CompanionErrandJournalBridge), not here. If the
         // errand is gone, the reconciler closes this mirror at the next session launch.
         SetDialogs();
      }

      // ── QuestBase contract ────────────────────────────────────────────────

      protected override void SetDialogs()
      {
         // No conversation flows: this is a read-only mirror, not an interactive quest.
      }

      #region private

      private string ResolveCompanionName()
      {
         CompanionMission live = CompanionMissionBehavior.Instance?.FindActive(_missionId);

         return live?.CompanionName ?? QuestGiver?.Name?.ToString() ?? "Your companion";
      }

      #endregion
   }
}
