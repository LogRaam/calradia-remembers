// Code written by Gabriel Mailhot, 02/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>
   ///   Owns the quest lifecycle on the SDK side of the bridge: issuing a quest the LLM
   ///   offered, paying out a quest the LLM acknowledged as done, and the daily checks
   ///   that expire overdue quests (with a consequence) or cancel ones whose premise has
   ///   evaporated (the target faction made peace). The deed-verification half lives in
   ///   <see cref="QuestDeedTracker" />, which stamps a quest satisfied from real events.
   /// </summary>
   public static class QuestService
   {
      /// <summary>
      ///   Personal-regard hit when the player openly abandons a quest — lighter
      ///   than a silent deadline failure, because they were honest about withdrawing.
      /// </summary>
      private const int AbandonPenalty = 3;

      /// <summary>Personal-regard hit when the player lets a quest's deadline lapse.</summary>
      private const int DeadlineFailurePenalty = 5;

      /// <summary>Cap on how many terminated quests we keep per NPC before pruning oldest.</summary>
      private const int MaxRetainedQuests = 10;

      /// <summary>
      ///   Applies a turn's quest outcomes: a newly offered quest (<c>[QUEST]</c>) and/or
      ///   an acknowledged completion (<c>[QUEST_COMPLETE]</c>). Chat lines are emitted
      ///   through <paramref name="notify" /> (text, isPositive).
      /// </summary>
      public static void ApplyResponse(NpcProfile giver,
                                       Hero giverHero,
                                       ParsedResponse response,
                                       int gameDay,
                                       IGameStateBridge bridge,
                                       BannerlordNpcMemoryStore store,
                                       Action<string, bool> notify)
      {
         if (giver == null || response == null) return;

         if (response.QuestGiven != null)
            IssueQuest(giver, giverHero, response.QuestGiven, gameDay, store, notify);

         if (response.QuestCompleted != null)
            CompleteQuest(giver, giverHero, response.QuestCompleted, gameDay, bridge, store, notify);

         if (response.QuestAbandoned != null)
            AbandonQuest(giver, response.QuestAbandoned, gameDay, store, notify);
      }

      /// <summary>
      ///   Finds an outstanding letter the player is carrying for <paramref name="recipient" />,
      ///   scanning every giver's quests. Returns the giver profile and the quest, or null.
      /// </summary>
      public static LetterErrand FindOutstandingLetterFor(Hero recipient, BannerlordNpcMemoryStore store)
      {
         if (recipient == null || store == null) return null;

         foreach (string id in store.ListIds())
         {
            NpcProfile giver = store.Get(id);

            if (giver == null) continue;

            InformalQuest letter = giver.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.DeliverLetter
               && q.IsOutstanding
               && string.Equals(q.TargetHeroId, recipient.StringId, StringComparison.Ordinal));

            if (letter != null) return new LetterErrand(giver, letter);
         }

         return null;
      }

      /// <summary>
      ///   Stamps a letter quest as delivered the moment the player hands it to the
      ///   recipient in conversation. The reward is collected later, when the player
      ///   reports back to the giver and the giver acknowledges the delivery.
      /// </summary>
      public static void MarkLetterDelivered(LetterErrand errand,
                                             Hero recipient,
                                             int gameDay,
                                             BannerlordNpcMemoryStore store)
      {
         if (errand == null || store == null) return;

         errand.Quest.SatisfiedOnDay = gameDay;
         errand.Quest.Evidence = $"Delivered the message to {recipient?.Name} on day {gameDay}";
         ModLog.Info($"[QUEST] letter delivered: {errand.Giver.Name} → {recipient?.Name} (day {gameDay}).");
         store.Set(errand.Giver);
      }

      /// <summary>
      ///   Closes an item-barter bargain that was settled on the spot (the player handed the
      ///   goods over via the picker and the bridge already executed the favor). Marks the
      ///   quest done, records the memory, mirrors the journal entry as completed, and
      ///   persists. The reward grant itself is NOT re-run here — the bridge already did it.
      /// </summary>
      public static void MarkBargainSettled(NpcProfile giver,
                                            InformalQuest quest,
                                            Hero giverHero,
                                            int gameDay,
                                            BannerlordNpcMemoryStore store)
      {
         if (giver == null || quest == null || store == null) return;

         quest.SatisfiedOnDay = gameDay;
         quest.Evidence = $"Handed over goods of the agreed value on day {gameDay}.";
         quest.Status = QuestStatus.Completed;
         AddEvent(giver, gameDay, NotableEventType.Collaboration,
            $"The player settled our bargain in goods — {Describe(quest)}.");
         QuestJournalBridge.CloseAsCompleted(quest, giverHero);
         Stance.StanceService.OnPromiseKept(giver);
         store.Set(giver);

         ModLog.Info($"[BARGAIN] settled for {giver.Name}: {Describe(quest)} (day {gameDay}).");
      }

      /// <summary>
      ///   Daily pass over every profile: cancel quests whose war premise ended, expire
      ///   overdue ones (with a relation penalty and a remembered slight), and prune the
      ///   oldest terminated quests so the list cannot grow without bound.
      /// </summary>
      public static void RunDailyMaintenance(BannerlordNpcMemoryStore store, int gameDay)
      {
         if (store == null) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null || profile.ActiveQuests.Count == 0) continue;

            Hero giverHero = Hero.FindFirst(h => h.StringId == id);

            // The giver is dead or gone from the world: close their open tasks now (not the player's
            // fault, so no penalty), instead of leaving them stuck in the journal until the next reload
            // (which is all ReconcileOnSessionLaunch did). The HeroKilledEvent path closes them the
            // instant the giver is slain; this daily sweep is the safety net for anything it missed.
            if (giverHero is not {IsAlive: true})
            {
               CancelQuestsOfDeadGiver(profile, giverHero, gameDay, store);

               continue;
            }

            var changed = false;

            foreach (InformalQuest quest in profile.ActiveQuests)
            {
               if (quest.Status != QuestStatus.Active) continue;

               if (quest.IsOutstanding && PremiseGone(quest, giverHero))
               {
                  quest.Status = QuestStatus.Cancelled;
                  AddEvent(profile, gameDay, NotableEventType.Other,
                     $"Set aside the task to {Describe(quest)} — circumstances changed.");
                  QuestJournalBridge.CloseAsCancelled(quest, giverHero, "Circumstances changed — this task is no longer needed.");
                  changed = true;

                  continue;
               }

               if (!quest.IsOutstanding || !quest.DeadlineDay.HasValue || gameDay <= quest.DeadlineDay.Value) continue;

               quest.Status = QuestStatus.Expired;
               ApplyPenalty(profile);
               Stance.StanceService.OnPromiseBroken(profile); // a lapsed deadline is a broken word too
               AddEvent(profile, gameDay, NotableEventType.Conflict,
                  $"The player let the deadline lapse on the task to {Describe(quest)} — a disappointment.");
               QuestJournalBridge.CloseAsFailed(quest, giverHero, $"You did not complete this task in time. {profile.Name} is disappointed.");
               ModLog.Info($"[QUEST] expired for {profile.Name}: {Describe(quest)} (-{DeadlineFailurePenalty} regard).");
               changed = true;
            }

            if (Prune(profile)) changed = true;
            if (changed) store.Set(profile);
         }
      }

      /// <summary>
      ///   Closes every still-open quest of a giver who has died (or been removed from the world):
      ///   both an outstanding task and one whose deed is already done but whose reward can no longer
      ///   be collected because the giver is gone. This is never the player's fault, so there is NO
      ///   relation penalty and no broken-word: the quests are Cancelled and their journal mirrors
      ///   closed with a death reason. Fires mid-session (on the giver's death, and as a daily safety
      ///   net) so a quest no longer lingers in the log until the next reload. Returns true if it
      ///   closed anything.
      /// </summary>
      public static bool CancelQuestsOfDeadGiver(NpcProfile giver, Hero giverHero, int gameDay, BannerlordNpcMemoryStore store)
      {
         if (giver == null || store == null) return false;

         var changed = false;
         foreach (InformalQuest quest in giver.ActiveQuests)
         {
            if (quest.Status != QuestStatus.Active) continue; // Active covers both outstanding and awaiting-reward

            quest.Status = QuestStatus.Cancelled;
            QuestJournalBridge.CloseAsCancelled(quest, giverHero,
               $"{giver.Name} is dead. This task can no longer be answered.");
            changed = true;
            ModLog.Info($"[QUEST] cancelled on giver death: {giver.Name} — {Describe(quest)} (day {gameDay}).");
         }

         if (changed) store.Set(giver);

         return changed;
      }

      #region private

      private static void AbandonQuest(NpcProfile giver,
                                       QuestAbandonClaim claim,
                                       int gameDay,
                                       BannerlordNpcMemoryStore store,
                                       Action<string, bool> notify)
      {
         // Only an outstanding (not-yet-done) quest can be abandoned — a finished one is done.
         InformalQuest quest = giver.ActiveQuests.FirstOrDefault(q =>
            q.IsOutstanding && (claim.Type == null || q.Type == claim.Type.Value));

         if (quest == null)
         {
            ModLog.Info($"[QUEST] {giver.Name} acknowledged an abandonment with no matching outstanding quest — ignored.");

            return;
         }

         quest.Status = QuestStatus.Abandoned;
         int after = giver.ReputationWithPlayer - AbandonPenalty;
         giver.ReputationWithPlayer = after < -100
            ? -100
            : after;
         AddEvent(giver, gameDay, NotableEventType.Conflict,
            $"The player gave up the task to {Describe(quest)} — a broken word, however honest.");
         QuestJournalBridge.CloseAsFailed(quest, FindGiverHero(giver), "You chose not to complete this task.");
         Stance.StanceService.OnPromiseBroken(giver); // a broken word spends trust
         store.Set(giver);

         ModLog.Info($"[QUEST] {giver.Name}: player abandoned '{quest.Type}' (-{AbandonPenalty} regard).");
         notify?.Invoke($"You abandoned {giver.Name}'s task ({Describe(quest)}). −{AbandonPenalty} regard.", false);
      }

      private static void AddEvent(NpcProfile profile, int gameDay, NotableEventType type, string summary)
         => profile.Events.Add(new NotableEvent(gameDay, type, summary));

      private static void ApplyPenalty(NpcProfile profile)
      {
         int after = profile.ReputationWithPlayer - DeadlineFailurePenalty;
         profile.ReputationWithPlayer = after < -100
            ? -100
            : after;
      }

      private static void CompleteQuest(NpcProfile giver,
                                        Hero giverHero,
                                        QuestCompletionClaim claim,
                                        int gameDay,
                                        IGameStateBridge bridge,
                                        BannerlordNpcMemoryStore store,
                                        Action<string, bool> notify)
      {
         InformalQuest quest = giver.ActiveQuests.FirstOrDefault(q =>
            q.IsAwaitingReward && (claim.Type == null || q.Type == claim.Type.Value));

         if (quest == null)
         {
            ModLog.Info($"[QUEST] {giver.Name} claimed completion with no verified quest to match — ignored.");

            return;
         }

         ActionOutcome reward = bridge?.GrantQuestReward(giverHero, giver, quest.RewardGold, quest.RewardRelation);

         // Negotiation framework: a bargain whose reward is a favor (join the party, etc.)
         // honors it now that the deed is verified. The grant outcome takes the chat line.
         ActionOutcome grant = quest.Reward != RewardGrant.None
            ? bridge?.GrantConditionalReward(giverHero, giver, quest)
            : null;

         quest.Status = QuestStatus.Completed;
         AddEvent(giver, gameDay, NotableEventType.Collaboration, $"The player completed the task to {Describe(quest)}.");
         QuestJournalBridge.CloseAsCompleted(quest, giverHero);
         Stance.StanceService.OnPromiseKept(giver); // a kept promise earns trust
         store.Set(giver);

         if (grant != null) notify?.Invoke(grant.Message, grant.IsPositive);
         else if (reward != null) notify?.Invoke(reward.Message, true);
         else notify?.Invoke($"{giver.Name} thanks you for {Describe(quest)}.", true);
      }

      /// <summary>One short clause describing the task, for chat lines and memory events.</summary>
      private static string Describe(InformalQuest q)
      {
         if (!string.IsNullOrWhiteSpace(q.Description)) return q.Description.TrimEnd('.');

         switch (q.Type)
         {
            case QuestType.BanditClear: return $"clear bandits near {q.TargetSettlement}";
            case QuestType.BanditHideout: return $"clear the hideout near {q.TargetSettlement}";
            case QuestType.AttackFaction: return $"strike {q.TargetFactionName}";
            case QuestType.AttackLord: return $"defeat {q.TargetHeroName}";
            case QuestType.RaidVillage: return $"raid {q.TargetSettlement}";
            case QuestType.AttackCaravan: return $"destroy a caravan of {q.TargetFactionName}";
            case QuestType.Siege: return $"help take {q.TargetSettlement}";
            case QuestType.CapturePrisoner: return $"capture {q.TargetHeroName}";
            case QuestType.ExecuteEnemy: return $"kill {q.TargetHeroName}";
            case QuestType.RescuePrisoner: return $"free {q.TargetHeroName}";
            case QuestType.DeliverLetter: return $"carry a message to {q.TargetHeroName}";
            case QuestType.ProvideGold: return "provide financial support for your child";
            case QuestType.DeliverItems:
                return q.Reward == RewardGrant.JoinParty
                    ? "bring goods in payment for their service"
                    : $"deliver goods worth {q.RequiredItemValue} denars";
            default: return "carry out a task";
         }
      }

      private static Hero FindGiverHero(NpcProfile profile)
         => profile == null
            ? null
            : Hero.FindFirst(h => h.StringId == profile.Id);

      private static void IssueQuest(NpcProfile giver,
                                     Hero giverHero,
                                     QuestProposal proposal,
                                     int gameDay,
                                     BannerlordNpcMemoryStore store,
                                     Action<string, bool> notify)
      {
         // One task at a time: never stack a new quest on an unfinished one.
         if (giver.ActiveQuests.Any(q => q.IsOutstanding))
         {
            ModLog.Info($"[QUEST] {giver.Name} offered a task while one is outstanding — ignored.");

            return;
         }

         InformalQuest quest = QuestFactory.Create(proposal, giverHero, gameDay);
         if (quest == null)
         {
            ModLog.Info($"[QUEST] {giver.Name} offered a task that could not be grounded (type {proposal.Type}) — ignored.");

            return;
         }

         giver.ActiveQuests.Add(quest);
         AddEvent(giver, gameDay, NotableEventType.Agreement, $"Asked the player to {Describe(quest)}.");
         store.Set(giver);

         // Open a vanilla quest journal mirror — StartQuest() fires its own
         // native notification, so we don't add a second one here.
         QuestJournalBridge.OpenMirror(quest, giverHero);

         ModLog.Info($"[QUEST] {giver.Name} issued: {Describe(quest)} " + $"(reward {quest.RewardGold}g/+{quest.RewardRelation}, deadline {quest.DeadlineDay?.ToString() ?? "none"}).");
      }

      /// <summary>
      ///   True when a war-bound quest's premise has ended — the giver's faction is no
      ///   longer at war with the target faction, so striking it serves no purpose.
      /// </summary>
      private static bool PremiseGone(InformalQuest quest, Hero giverHero)
      {
         if (string.IsNullOrEmpty(quest.TargetFactionId)) return false;
         if (giverHero?.MapFaction == null) return false;

         IFaction target = ResolveFactionById(quest.TargetFactionId);

         if (target == null) return false;

         return !FactionManager.IsAtWarAgainstFaction(giverHero.MapFaction, target);
      }

      private static bool Prune(NpcProfile profile)
      {
         if (profile.ActiveQuests.Count <= MaxRetainedQuests) return false;

         // Drop the oldest terminated quests first; never drop a live one.
         var terminated = new List<InformalQuest>();
         foreach (InformalQuest quest in profile.ActiveQuests.Where(q => q.Status != QuestStatus.Active)
                                                .OrderBy(q => q.IssuedOnDay)) terminated.Add(quest);

         var removed = false;
         foreach (InformalQuest q in terminated)
         {
            if (profile.ActiveQuests.Count <= MaxRetainedQuests) break;
            profile.ActiveQuests.Remove(q);
            removed = true;
         }

         return removed;
      }

      private static IFaction ResolveFactionById(string id)
      {
         if (string.IsNullOrEmpty(id)) return null;
         IFaction k = Kingdom.All?.FirstOrDefault(x => x.StringId == id);

         if (k != null) return k;

         return Clan.All?.FirstOrDefault(x => x.StringId == id);
      }

      #endregion
   }

   /// <summary>Pairs a letter quest with the giver who issued it, for the delivery flow.</summary>
   public sealed class LetterErrand
   {
      public LetterErrand(NpcProfile giver, InformalQuest quest)
      {
         Giver = giver;
         Quest = quest;
      }

      public NpcProfile Giver { get; }
      public InformalQuest Quest { get; }
   }
}