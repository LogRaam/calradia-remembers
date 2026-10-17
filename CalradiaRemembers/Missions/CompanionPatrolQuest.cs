// Code written by Gabriel Mailhot, 17/10/2026.
// The patrol in the game's quest journal (jaserms1, 16/10/2026: a council's patrol order did nothing, and nothing showed
// it). A read-only mirror of a patrol owned by CompanionPatrolBehavior, modelled on CompanionErrandQuest: it carries no
// dialog and no reward, it exists so "who guards what, since when" is legible. Saveable fields: the companion and the
// patrol ids only; a mirror whose patrol is gone is closed at session launch.

#region

using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>The journal entry of a companion's patrol, a mirror of <see cref="CompanionPatrolBehavior" />'s record.</summary>
   public sealed class CompanionPatrolQuest : QuestBase
   {
      // Saveable field IDs (do not reuse).
      [SaveableField(1)]
      private readonly string _companionId;

      [SaveableField(2)]
      private readonly string _patrolId;

      [SaveableField(3)]
      private readonly string _title;

      /// <summary>Opens the entry for a patrol that just rode out.</summary>
      public CompanionPatrolQuest(CompanionPatrol patrol, Hero companion, string firstLine)
         : base($"cr_patrol_{companion.StringId}_{patrol.Id}", companion, CampaignTime.Never, 0)
      {
         _companionId = companion.StringId;
         _patrolId = patrol.Id;
         _title = $"Patrol: {patrol.CompanionName} around {patrol.SettlementName}";
         Log(firstLine);
         SetDialogs();
         StartQuest();
      }

      public override bool IsRemainingTimeHidden => true;

      /// <summary>LOAD-BEARING, as for every CR mirror: without it the engine cancels the entry at each reload.</summary>
      public override string SpecialQuestType => "CalradiaRemembers";

      public override TextObject Title => new(_title ?? "Patrol");

      /// <summary>The patrol's id.</summary>
      internal string PatrolId => _patrolId;

      /// <summary>Adds a line to the entry's log.</summary>
      public void Log(string line)
      {
         if (string.IsNullOrWhiteSpace(line)) return;
         try { AddLog(new TextObject(Mapping.ShownText.Of(line))); }
         catch (Exception ex) { ModLog.Error($"[PATROL] journal log failed: {ex.Message}"); }
      }

      /// <summary>Closes the entry: the band came home.</summary>
      public void CloseHome(string line)
      {
         Log(line);
         try { CompleteQuestWithSuccess(); }
         catch (Exception ex) { ModLog.Error($"[PATROL] journal close failed: {ex.Message}"); }
      }

      /// <summary>Closes the entry: the patrol was broken (capture, death, scattered, or no longer tracked).</summary>
      public void CloseLost(string reason)
      {
         try { CompleteQuestWithCancel(new TextObject(Mapping.ShownText.Of(reason))); }
         catch (Exception ex) { ModLog.Error($"[PATROL] journal close failed: {ex.Message}"); }
      }

      /// <summary>The live entry of a patrol, or null.</summary>
      internal static CompanionPatrolQuest Of(string patrolId)
         => string.IsNullOrEmpty(patrolId) || Campaign.Current?.QuestManager == null
            ? null
            : Campaign.Current.QuestManager.Quests.OfType<CompanionPatrolQuest>().FirstOrDefault(q => q.PatrolId == patrolId && q.IsOngoing);

      protected override void InitializeQuestOnGameLoad() => SetDialogs();

      protected override void SetDialogs()
      {
         // A read-only mirror: no conversation flows.
      }
   }
}
