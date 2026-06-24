// Code written by Gabriel Mailhot, 13/06/2026.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>
   ///   A thin <see cref="QuestBase" /> wrapper around an <see cref="InformalQuest" />
   ///   that makes it visible in Bannerlord's native quest journal. The quest journal
   ///   entry is a MIRROR of the data owned by our <see cref="InformalQuest" /> — it
   ///   is never the authoritative source. All lifecycle decisions (completion, expiry,
   ///   cancellation, abandonment) happen in <see cref="QuestService" /> which then
   ///   calls the appropriate <c>CompleteQuestWith…</c> here to close the mirror.
   ///   Anti-stuck guarantee: <see cref="QuestJournalBridge.ReconcileOnSessionLaunch" />
   ///   force-closes any mirror whose <see cref="InformalQuest" /> is no longer
   ///   outstanding on every session launch. A stuck entry cannot outlive one session.
   ///   Saveable fields: only <see cref="_giverId" /> and <see cref="_questKey" />.
   ///   Everything else is reconstituted from the live <see cref="InformalQuest" /> on
   ///   load via <see cref="InitializeQuestOnGameLoad" />.
   /// </summary>
   public sealed class CalradiaRemembersQuest : QuestBase
   {
      // Saveable field IDs — do not reuse.
      [SaveableField(1)]
      private readonly string _giverId;

      [SaveableField(2)]
      private readonly string _questKey;

      /// <summary>
      ///   Creates a new mirror for <paramref name="quest" />.
      /// </summary>
      public CalradiaRemembersQuest(InformalQuest quest, Hero giver)
         : base(
            BuildQuestId(giver, quest),
            giver,
            // NEVER hand the engine a finite timeout: WE own deadline handling
            // (QuestService.RunDailyMaintenance) and hide the engine countdown
            // (IsRemainingTimeHidden). A native duration let the engine auto-FAIL the quest
            // on its own — observed as an in-progress quest dropping to failed on RELOAD.
            CampaignTime.Never,
            quest.RewardGold)
      {
         _giverId = giver.StringId;
         _questKey = BuildQuestKey(giver, quest);

         AddLog(BuildDescriptionText(quest));
         SetDialogs();
         StartQuest();
      }

      // We manage deadline ourselves in QuestService.RunDailyMaintenance —
      // hide the engine's countdown to avoid double-expiry display.
      public override bool IsRemainingTimeHidden => true;

      public override TextObject Title => new(ResolveLiveQuest()?.Description ?? _questKey);

      /// <summary>
      ///   Closes the mirror as cancelled (war ended, giver died, etc.) — no penalty,
      ///   no reward.
      /// </summary>
      public void CloseAsCancelled(string reason)
      {
         try { CompleteQuestWithCancel(new TextObject(reason)); }
         catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsCancelled failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Closes the mirror with success and pays the gold reward.
      /// </summary>
      public void CloseAsCompleted()
      {
         try { CompleteQuestWithSuccess(); }
         catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsCompleted failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Closes the mirror as failed (deadline missed or abandoned).
      /// </summary>
      public void CloseAsFailed(string reason)
      {
         try { CompleteQuestWithFail(new TextObject(reason)); }
         catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsFailed failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Called when the deed is verified. Updates the journal log so the player
      ///   sees « Done — report back to [giver] » in the quest detail.
      /// </summary>
      public void MarkDeedDone(string evidence)
      {
         AddLog(new TextObject($"Done: {evidence} — return to {QuestGiver?.Name} to collect your reward."));
      }

      internal static string BuildQuestId(Hero giver, InformalQuest quest)
         => $"cr_quest_{giver.StringId}_{(int) quest.Type}_{quest.IssuedOnDay}";

      protected override void InitializeQuestOnGameLoad()
      {
         // Reconnect the live quest object. If it is gone or terminal the
         // reconciler (QuestJournalBridge.ReconcileOnSessionLaunch) will
         // call CloseAsCancelled, so we don't need to act here.
         SetDialogs();
      }

      // ── QuestBase contract ────────────────────────────────────────────────

      protected override void SetDialogs()
      {
         // No conversation flows needed — we use our own chat window.
      }

      #region private

      private static TextObject BuildDescriptionText(InformalQuest quest) => new(quest.Description);

      private static string BuildQuestKey(Hero giver, InformalQuest quest)
         => BuildQuestId(giver, quest);

      // ── Helpers ───────────────────────────────────────────────────────────

      private InformalQuest ResolveLiveQuest()
      {
         if (_giverId == null) return null;
         Hero giver = Hero.FindFirst(h => h.StringId == _giverId);

         if (giver == null) return null;

         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(_giverId);

         return profile?.ActiveQuests.Find(q => BuildQuestKey(giver, q) == _questKey);
      }

      #endregion
   }
}