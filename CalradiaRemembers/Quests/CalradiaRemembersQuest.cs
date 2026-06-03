// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 11.7: quest journal integration — QuestBase mirror of InformalQuest.

#region

using System;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

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
    ///
    ///   Anti-stuck guarantee: <see cref="QuestJournalBridge.ReconcileOnSessionLaunch" />
    ///   force-closes any mirror whose <see cref="InformalQuest" /> is no longer
    ///   outstanding on every session launch. A stuck entry cannot outlive one session.
    ///
    ///   Saveable fields: only <see cref="_giverId"/> and <see cref="_questKey"/>.
    ///   Everything else is reconstituted from the live <see cref="InformalQuest" /> on
    ///   load via <see cref="InitializeQuestOnGameLoad" />.
    /// </summary>
    public sealed class CalradiaRemembersQuest : QuestBase
    {
        // Saveable field IDs — do not reuse.
        [TaleWorlds.SaveSystem.SaveableField(1)] private string _giverId;
        [TaleWorlds.SaveSystem.SaveableField(2)] private string _questKey;

        // The live log entry so we can update it when the deed is done.
        private JournalLog _progressLog;

        /// <summary>
        ///   Creates a new mirror for <paramref name="quest" />.
        /// </summary>
        public CalradiaRemembersQuest(InformalQuest quest, Hero giver)
            : base(
                BuildQuestId(giver, quest),
                giver,
                quest.DeadlineDay.HasValue
                    ? CampaignTime.DaysFromNow(quest.DeadlineDay.Value - (int)CampaignTime.Now.ToDays)
                    : CampaignTime.Never,
                quest.RewardGold)
        {
            _giverId  = giver.StringId;
            _questKey = BuildQuestKey(giver, quest);

            AddLog(BuildDescriptionText(quest));
            SetDialogs();
            StartQuest();
        }

        public override TextObject Title
            => new TextObject(ResolveLiveQuest()?.Description ?? _questKey);

        // We manage deadline ourselves in QuestService.RunDailyMaintenance —
        // hide the engine's countdown to avoid double-expiry display.
        public override bool IsRemainingTimeHidden => true;

        /// <summary>
        ///   Called when the deed is verified. Updates the journal log so the player
        ///   sees « Done — report back to [giver] » in the quest detail.
        /// </summary>
        public void MarkDeedDone(string evidence)
        {
            _progressLog?.UpdateCurrentProgress(1);
            AddLog(new TextObject($"Done: {evidence} — return to {QuestGiver?.Name} to collect your reward."));
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
        ///   Closes the mirror as cancelled (war ended, giver died, etc.) — no penalty,
        ///   no reward.
        /// </summary>
        public void CloseAsCancelled(string reason)
        {
            try { CompleteQuestWithCancel(new TextObject(reason)); }
            catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsCancelled failed: {ex.Message}"); }
        }

        /// <summary>
        ///   Closes the mirror as failed (deadline missed or abandoned).
        /// </summary>
        public void CloseAsFailed(string reason)
        {
            try { CompleteQuestWithFail(new TextObject(reason)); }
            catch (Exception ex) { ModLog.Error($"[QUEST-JOURNAL] CloseAsFailed failed: {ex.Message}"); }
        }

        // ── QuestBase contract ────────────────────────────────────────────────

        protected override void SetDialogs()
        {
            // No conversation flows needed — we use our own chat window.
        }

        protected override void InitializeQuestOnGameLoad()
        {
            // Reconnect the live quest object. If it is gone or terminal the
            // reconciler (QuestJournalBridge.ReconcileOnSessionLaunch) will
            // call CloseAsCancelled, so we don't need to act here.
            SetDialogs();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private InformalQuest ResolveLiveQuest()
        {
            if (_giverId == null) return null;
            Hero giver = Hero.FindFirst(h => h.StringId == _giverId);
            if (giver == null) return null;

            var store = Behaviors.NpcMemoryCampaignBehavior.Instance?.Store;
            NpcProfile profile = store?.Get(_giverId);
            if (profile == null) return null;

            return profile.ActiveQuests.Find(q => BuildQuestKey(giver, q) == _questKey);
        }

        private static TextObject BuildDescriptionText(InformalQuest quest)
            => new TextObject(quest.Description ?? quest.Type.ToString());

        internal static string BuildQuestId(Hero giver, InformalQuest quest)
            => $"cr_quest_{giver.StringId}_{(int)quest.Type}_{quest.IssuedOnDay}";

        private static string BuildQuestKey(Hero giver, InformalQuest quest)
            => BuildQuestId(giver, quest);
    }
}
