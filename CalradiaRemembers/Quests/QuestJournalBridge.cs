// Code written by Gabriel Mailhot, 13/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
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
   ///   Anti-stuck guarantee: <see cref="ReconcileOnSessionLaunch" /> is called at
   ///   every session launch. It scans all live journal mirrors and force-closes any
   ///   whose underlying <see cref="InformalQuest" /> is no longer outstanding (e.g.
   ///   the giver died, the quest was already completed on a previous session, or the
   ///   mod version changed). This prevents the "stuck quest" problem seen in AI Influence.
   /// </summary>
   public static class QuestJournalBridge
   {
      /// <summary>
      ///   Closes as cancelled (war ended, giver dead/eliminated — no player fault).
      /// </summary>
      public static void CloseAsCancelled(InformalQuest quest, Hero giver, string reason)
         => FindMirror(quest, giver)?.CloseAsCancelled(reason);

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
      ///   Creates a <see cref="CalradiaRemembersQuest" /> mirror for a newly issued
      ///   quest and starts it. Called from <see cref="QuestService.IssueQuest" />.
      /// </summary>
      public static void OpenMirror(InformalQuest quest, Hero giver)
      {
         if (quest == null || giver is not {IsAlive: true}) return;

         // Immediate in-conversation bargains (item-barter recruitment) are settled on the
         // spot via the bag picker — a journal entry would open and close within the same
         // conversation, spamming "new quest"/"quest complete" popups, and a declined one
         // would linger as a phantom errand. They stay OUT of the vanilla journal; the
         // bargain is still persisted on the giver's profile so the player can return to
         // settle it. Deferred deeds (go away and return) DO mirror — they need the reminder.
         if (quest.Type == QuestType.DeliverItems) return;

         try
         {
            var mirror = new CalradiaRemembersQuest(quest, giver);
            ModLog.Info($"[QUEST-JOURNAL] opened mirror: {mirror.StringId}");

            // Verify it actually registered.
            int count = Campaign.Current?.QuestManager?.Quests
                                ?.OfType<CalradiaRemembersQuest>().Count() ??
                        -1;
            ModLog.Info($"[QUEST-JOURNAL] CR quests in manager after open: {count}");
         }
         catch (Exception ex)
         {
            // Journal failure is never allowed to break the quest itself.
            ModLog.Error($"[QUEST-JOURNAL] OpenMirror failed for {giver.Name}: {ex.GetType().Name}: {ex.Message}");
            ModLog.Error($"[QUEST-JOURNAL] Stack: {ex.StackTrace}");
         }
      }

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

         var closed = 0;
         foreach (CalradiaRemembersQuest q in Campaign.Current.QuestManager.Quests
                                                      .OfType<CalradiaRemembersQuest>()
                                                      .ToList()) // snapshot — we may mutate during iteration
         {
            MirrorReconcileDecision decision = DecideForMirror(q, store, out string detail);

            if (decision == MirrorReconcileDecision.Keep)
            {
               // Log the ambiguous keeps (there is a detail) so a "why didn't it close" repro stays diagnosable.
               if (detail != null) ModLog.Info($"[QUEST-JOURNAL] Keeping mirror '{q.StringId}' at session launch: {detail}.");

               continue;
            }

            ModLog.Info($"[QUEST-JOURNAL] Closing mirror '{q.StringId}' at session launch: {detail}.");
            q.CloseAsCancelled(decision == MirrorReconcileDecision.CloseGiverGone
               ? "The one who set you this task is gone."
               : "This task has already been resolved.");
            closed++;
         }

         if (closed > 0)
            ModLog.Info($"[QUEST-JOURNAL] Reconciled {closed} stale mirror(s) at session launch.");

         RestoreMissingMirrors(store);
      }

      /// <summary>
      ///   The reconciler's second direction: the journal is a PROJECTION of the authoritative store, so a live
      ///   <see cref="InformalQuest" /> (outstanding or awaiting its reward) whose journal mirror is missing gets
      ///   the mirror RE-CREATED. This heals saves damaged by the old false-cancel bug (the quest survived in the
      ///   giver's profile; only its journal entry was destroyed on load) and any future mirror loss. Idempotent:
      ///   an already-open mirror blocks re-creation, and <see cref="Campaign.Current" />'s quest list only holds
      ///   ongoing quests, so closed history never blocks a legitimate restore. Per-quest failures are contained.
      /// </summary>
      public static void RestoreMissingMirrors(BannerlordNpcMemoryStore store)
      {
         if (store == null || Campaign.Current?.QuestManager == null) return;

         var openKeys = new HashSet<string>(Campaign.Current.QuestManager.Quests
                                                    .OfType<CalradiaRemembersQuest>()
                                                    .Select(q => q.QuestKey));

         var restored = 0;
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null || profile.ActiveQuests.Count == 0) continue;

            Hero giver = Hero.FindFirst(h => h.StringId == profile.Id);
            bool giverAliveAndResolved = giver is {IsAlive: true};

            foreach (InformalQuest quest in profile.ActiveQuests.ToList())
            {
               string key = giverAliveAndResolved ? CalradiaRemembersQuest.BuildQuestId(giver, quest) : null;
               bool questIsLive = quest.IsOutstanding || quest.IsAwaitingReward;

               if (!QuestReconcilePolicy.ShouldRestoreMirror(questIsLive, giverAliveAndResolved, key != null && openKeys.Contains(key)))
                  continue;

               try
               {
                  var mirror = new CalradiaRemembersQuest(quest, giver);
                  if (quest.IsAwaitingReward)
                     mirror.MarkDeedDone(quest.Evidence ?? "The task is done");
                  openKeys.Add(key);
                  restored++;
                  ModLog.Info($"[QUEST-JOURNAL] Restored missing mirror '{key}' for {giver.Name} ({quest.Status}{(quest.IsAwaitingReward ? ", awaiting reward" : "")}).");
               }
               catch (Exception ex)
               {
                  ModLog.Error($"[QUEST-JOURNAL] Restore of '{key}' failed: {ex.GetType().Name}: {ex.Message}");
               }
            }
         }

         if (restored > 0)
            ModLog.Info($"[QUEST-JOURNAL] Restored {restored} missing journal mirror(s) from the quest store.");
      }

      #region private

      // ── Helpers ───────────────────────────────────────────────────────────

      private static CalradiaRemembersQuest FindMirror(InformalQuest quest, Hero giver)
      {
         if (quest == null || giver == null || Campaign.Current?.QuestManager == null)
            return null;

         string targetKey = CalradiaRemembersQuest.BuildQuestId(giver, quest);

         // Match on the STABLE QuestKey, never the engine's StringId (which it can rewrite on registration).
         // Matching on StringId here silently failed to find the mirror, so a completed/cancelled quest never
         // closed its journal entry, and the next session-launch reconciler then cancelled it.
         return Campaign.Current.QuestManager.Quests
                        .OfType<CalradiaRemembersQuest>()
                        .FirstOrDefault(q => q.QuestKey == targetKey);
      }

      /// <summary>
      ///   Decides a mirror's fate at session launch, delegating the actual rule to the pure
      ///   <see cref="QuestReconcilePolicy" />. The guiding principle: NEVER cancel a quest we cannot positively
      ///   prove is finished. A false cancel destroys a valid, often already-completed task (the repeatedly-
      ///   reported "quest auto-failed on load" bug). So only a confirmed-dead giver or a matching quest found in
      ///   a terminal state closes a mirror; a null giver reference, an unloaded profile, or id drift after a
      ///   version upgrade all resolve to Keep. <paramref name="detail" /> is a human-readable reason (null for a
      ///   plain, matched keep) for the diagnostic log.
      /// </summary>
      private static MirrorReconcileDecision DecideForMirror(CalradiaRemembersQuest mirror, BannerlordNpcMemoryStore store, out string detail)
      {
         Hero giver = mirror.QuestGiver;

         // A NULL giver reference is ambiguous (the engine may not have restored it), NOT a confirmed death:
         // treating it as death here would nuke a valid quest, so only a resolved-but-not-alive giver counts.
         bool giverConfirmedDead = giver != null && !giver.IsAlive;

         NpcProfile profile = giver != null ? store.Get(giver.StringId) : null;

         bool hasLiveMatch = false, hasTerminalMatch = false;
         if (profile != null && giver != null)
            foreach (InformalQuest q in profile.ActiveQuests)
            {
               // Match on the STABLE key, never mirror.StringId (the engine may have rewritten it).
               if (CalradiaRemembersQuest.BuildQuestId(giver, q) != mirror.QuestKey) continue;
               if (q.IsOutstanding || q.IsAwaitingReward) hasLiveMatch = true;
               else hasTerminalMatch = true;
            }

         MirrorReconcileDecision decision = QuestReconcilePolicy.Decide(giverConfirmedDead, hasLiveMatch, hasTerminalMatch);

         switch (decision)
         {
            case MirrorReconcileDecision.CloseGiverGone:
               detail = $"giver {giver?.Name?.ToString() ?? "?"} is dead, the task can no longer be reported";

               break;
            case MirrorReconcileDecision.CloseTerminal:
               detail = "a matching quest has genuinely terminated";

               break;
            default:
               // Keep. Only annotate the AMBIGUOUS keeps (no live match found) for diagnostics; a clean match is silent.
               detail = hasLiveMatch
                  ? null
                  : $"no confident match for living giver {giver?.Name?.ToString() ?? "(unresolved reference)"}, keeping rather than risk a false cancel";

               break;
         }

         return decision;
      }

      #endregion
   }
}