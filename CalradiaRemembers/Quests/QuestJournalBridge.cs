// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 11.7: quest journal — sync between InformalQuest and QuestBase.

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Quests
{
    /// <summary>
    ///   Bridges <see cref="InformalQuest" /> (our persisted record, authority) and
    ///   <see cref="CalradiaRemembersQuest" /> (the <c>QuestBase</c> mirror that appears
    ///   in the vanilla quest journal). The bridge is the ONLY place that creates or
    ///   closes journal mirrors; <see cref="QuestService" /> calls these methods after
    ///   its own lifecycle transitions.
    ///
    ///   Anti-stuck guarantee: <see cref="ReconcileOnSessionLaunch" /> is called at
    ///   every session launch. It scans all live journal mirrors and force-closes any
    ///   whose underlying <see cref="InformalQuest" /> is no longer outstanding (e.g.
    ///   the giver died, the quest was already completed on a previous session, or the
    ///   mod version changed). This prevents the "stuck quest" problem seen in AI Influence.
    /// </summary>
    public static class QuestJournalBridge
    {
        /// <summary>
        ///   Creates a <see cref="CalradiaRemembersQuest" /> mirror for a newly issued
        ///   quest and starts it. Called from <see cref="QuestService.IssueQuest" />.
        /// </summary>
        public static void OpenMirror(InformalQuest quest, Hero giver)
        {
            if (quest == null || giver == null || !giver.IsAlive) return;

            try
            {
                var mirror = new CalradiaRemembersQuest(quest, giver);
                ModLog.Info($"[QUEST-JOURNAL] opened mirror: {mirror.StringId}");

                // Verify it actually registered.
                int count = Campaign.Current?.QuestManager?.Quests
                    ?.OfType<CalradiaRemembersQuest>().Count() ?? -1;
                ModLog.Info($"[QUEST-JOURNAL] CR quests in manager after open: {count}");
            }
            catch (Exception ex)
            {
                // Journal failure is never allowed to break the quest itself.
                ModLog.Error($"[QUEST-JOURNAL] OpenMirror failed for {giver?.Name}: {ex.GetType().Name}: {ex.Message}");
                ModLog.Error($"[QUEST-JOURNAL] Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        ///   Marks the deed verified in the journal log. Called from
        ///   <see cref="QuestDeedTracker" /> after stamping the evidence.
        /// </summary>
        public static void NotifyDeedDone(InformalQuest quest, Hero giver)
        {
            if (quest == null || giver == null) return;
            CalradiaRemembersQuest mirror = FindMirror(quest, giver);
            mirror?.MarkDeedDone(quest.Evidence ?? "Deed verified.");
        }

        /// <summary>
        ///   Closes the journal entry with success and pays the gold reward.
        ///   Called when the giver acknowledges completion via <c>[QUEST_COMPLETE]</c>.
        /// </summary>
        public static void CloseAsCompleted(InformalQuest quest, Hero giver)
            => FindMirror(quest, giver)?.CloseAsCompleted();

        /// <summary>
        ///   Closes as failed (deadline missed or player abandoned).
        /// </summary>
        public static void CloseAsFailed(InformalQuest quest, Hero giver, string reason)
            => FindMirror(quest, giver)?.CloseAsFailed(reason);

        /// <summary>
        ///   Closes as cancelled (war ended, giver dead/eliminated — no player fault).
        /// </summary>
        public static void CloseAsCancelled(InformalQuest quest, Hero giver, string reason)
            => FindMirror(quest, giver)?.CloseAsCancelled(reason);

        /// <summary>
        ///   Anti-stuck pass. Runs at every session launch (wired by
        ///   <see cref="NpcMemoryCampaignBehavior.OnSessionLaunched" />). Iterates every
        ///   live <see cref="CalradiaRemembersQuest" /> and closes any whose underlying
        ///   <see cref="InformalQuest" /> is no longer outstanding. A mirror that survives
        ///   this pass is guaranteed to correspond to a live, un-finished task.
        /// </summary>
        public static void ReconcileOnSessionLaunch(BannerlordNpcMemoryStore store)
        {
            if (store == null || Campaign.Current?.QuestManager == null) return;

            int closed = 0;
            foreach (var q in Campaign.Current.QuestManager.Quests
                                      .OfType<CalradiaRemembersQuest>()
                                      .ToList())   // snapshot — we may mutate during iteration
            {
                if (IsStillOutstanding(q, store)) continue;

                q.CloseAsCancelled("The task has been resolved or is no longer relevant.");
                closed++;
            }

            if (closed > 0)
                ModLog.Info($"[QUEST-JOURNAL] Reconciled {closed} stale mirror(s) at session launch.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static CalradiaRemembersQuest FindMirror(InformalQuest quest, Hero giver)
        {
            if (quest == null || giver == null || Campaign.Current?.QuestManager == null)
                return null;

            string targetId = CalradiaRemembersQuest.BuildQuestId(giver, quest);
            return Campaign.Current.QuestManager.Quests
                           .OfType<CalradiaRemembersQuest>()
                           .FirstOrDefault(q => q.StringId == targetId);
        }

        private static bool IsStillOutstanding(CalradiaRemembersQuest mirror, BannerlordNpcMemoryStore store)
        {
            Hero giver = mirror.QuestGiver;
            if (giver == null || !giver.IsAlive) return false;

            NpcProfile profile = store.Get(giver.StringId);
            if (profile == null) return false;

            return profile.ActiveQuests.Any(q =>
                q.IsOutstanding &&
                CalradiaRemembersQuest.BuildQuestId(giver, q) == mirror.StringId);
        }
    }
}
