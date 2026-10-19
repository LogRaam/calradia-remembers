// Code written by Gabriel Mailhot, 27/06/2026.

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   The public, one-way inbound API for missions: a third-party mod <see cref="Register" />s a mission, then
   ///   <see cref="Complete" />s it when its own deed is truly done (or <see cref="Abandon" />s it). CR issues,
   ///   frames, journals, gates the reward, and keeps the lifecycle clean; it never reads or mutates the caller's
   ///   state, and never pays on roleplay alone — the caller's completion signal is the gate.
   /// </summary>
   public static class MissionBoard
   {
      /// <summary>
      ///   The external mod withdraws a mission whose premise is gone. CR cancels it with no player penalty
      ///   (the mod, not the player, called it off) and closes the journal entry. Returns
      ///   <see cref="MissionBoardResult.Abandoned" />, or <see cref="MissionBoardResult.NotFound" />.
      /// </summary>
      public static MissionBoardResult Abandon(string missionId)
         => Resolve(missionId, (store, profile, giver, quest, today) =>
         {
            if (quest.Status != QuestStatus.Active) return MissionBoardResult.NotFound;

            quest.Status = QuestStatus.Cancelled;
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other, "Set aside a task — circumstances changed."));
            QuestJournalBridge.CloseAsCancelled(quest, giver, "This task is no longer needed.");
            store.Set(profile);

            ModLog.Info($"[MISSION] '{missionId}' abandoned by its source mod.");

            return MissionBoardResult.Abandoned;
         });

      /// <summary>
      ///   The external mod's completion signal: the deed is truly done. CR stamps the mission satisfied (the
      ///   gate), so the giver will acknowledge it and pay the reward when the player next reports back in
      ///   conversation. Returns <see cref="MissionBoardResult.Completed" />, or <see cref="MissionBoardResult.NotFound" />
      ///   if no still-outstanding mission carries that id.
      /// </summary>
      public static MissionBoardResult Complete(string missionId)
         => Resolve(missionId, (store, profile, giver, quest, today) =>
         {
            if (!quest.IsOutstanding) return MissionBoardResult.NotFound; // already satisfied, or terminated

            quest.SatisfiedOnDay = today;
            quest.Evidence = $"Reported complete by {quest.ExternalSourceMod} on day {today}.";
            store.Set(profile);

            ModLog.Info($"[MISSION] '{missionId}' marked satisfied — awaiting the player's report to {giver?.Name}.");

            return MissionBoardResult.Completed;
         });

      /// <summary>
      ///   Validates a mission at the door and issues it as a verified quest on the giver. Returns why it was
      ///   rejected, or <see cref="MissionBoardResult.Registered" /> on success. Mutates nothing the caller owns.
      /// </summary>
      public static MissionBoardResult Register(ExternalMission m)
      {
         if (m == null || !ExternalMissionRules.HasRequiredIdentity(m.Id, m.SourceMod)) return MissionBoardResult.RejectedInvalid;

         string title = ExternalMissionRules.BoundTitle(m.Title);
         string description = ExternalMissionRules.BoundDescription(m.Description) ?? title;

         if (description == null) return MissionBoardResult.RejectedInvalid; // a mission with neither title nor body says nothing

         var behavior = NpcMemoryCampaignBehavior.Instance;
         BannerlordNpcMemoryStore store = behavior?.Store;

         if (store == null) return MissionBoardResult.RejectedNoStore;

         // Resolve via Hero.FindFirst — the lookup the rest of the mod uses; MBObjectManager.GetObject<Hero>
         // does not reliably resolve heroes by StringId.
         Hero giver = string.IsNullOrWhiteSpace(m.GiverHeroId)
            ? null
            : Hero.FindFirst(h => h.StringId == m.GiverHeroId);

         if (giver == null) return MissionBoardResult.RejectedUnknownGiver;

         // CR must already have a profile for the giver (the player has met them) — that profile hosts the quest.
         NpcProfile giverProfile = store.Get(giver.StringId);

         if (giverProfile == null) return MissionBoardResult.RejectedUnknownGiver;

         // Dedup on the mission id: a still-live registration with this id blocks a second one.
         if (giverProfile.ActiveQuests.Any(q => q.Type == QuestType.External && q.Status == QuestStatus.Active && string.Equals(q.ExternalId, m.Id, StringComparison.Ordinal)))
            return MissionBoardResult.RejectedDuplicate;

         int today = CurrentDay(behavior);
         int? deadlineDays = ExternalMissionRules.NormalizeDeadlineDays(m.DeadlineDays);

         var quest = new InformalQuest {
            Type = QuestType.External,
            Description = description,
            RewardGold = ExternalMissionRules.ClampRewardGold(m.RewardGold),
            RewardRelation = ExternalMissionRules.ClampRewardRelation(m.RewardRelation),
            IssuedOnDay = today,
            DeadlineDay = deadlineDays.HasValue
               ? today + deadlineDays.Value
               : null,
            ExternalId = m.Id,
            ExternalSourceMod = m.SourceMod,
            Status = QuestStatus.Active
         };

         giverProfile.ActiveQuests.Add(quest);
         giverProfile.Events.Add(new NotableEvent(today, NotableEventType.Agreement, $"Set the player a task: {description}"));
         store.Set(giverProfile);

         // Open a vanilla quest journal mirror — StartQuest() fires its own native notification.
         QuestJournalBridge.OpenMirror(quest, giver);

         ModLog.Info($"[MISSION] {m.SourceMod} registered '{m.Id}' on {giver.Name}: {description} " +
                     $"(reward {quest.RewardGold}g/+{quest.RewardRelation}, deadline {quest.DeadlineDay?.ToString() ?? "none"}).");

         return MissionBoardResult.Registered;
      }

      #region private

      private static int CurrentDay(NpcMemoryCampaignBehavior behavior)
         => behavior?.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;

      /// <summary>Finds the external quest carrying <paramref name="missionId" /> across every profile, then runs the action.</summary>
      private static MissionBoardResult Resolve(string missionId,
                                                Func<BannerlordNpcMemoryStore, NpcProfile, Hero, InformalQuest, int, MissionBoardResult> action)
      {
         if (string.IsNullOrWhiteSpace(missionId)) return MissionBoardResult.NotFound;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         BannerlordNpcMemoryStore store = behavior?.Store;

         if (store == null) return MissionBoardResult.RejectedNoStore;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            InformalQuest quest = profile?.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.External && string.Equals(q.ExternalId, missionId, StringComparison.Ordinal));

            if (quest == null) continue;

            Hero giver = Hero.FindFirst(h => h.StringId == id);

            return action(store, profile, giver, quest, CurrentDay(behavior));
         }

         return MissionBoardResult.NotFound;
      }

      #endregion
   }
}