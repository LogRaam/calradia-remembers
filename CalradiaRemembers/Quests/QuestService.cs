// Code written by Gabriel Mailhot, 01/06/2026.
// Sprint 11: informal quests — issuance, completion, and daily lifecycle.

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
        /// <summary>Personal-regard hit when the player lets a quest's deadline lapse.</summary>
        private const int DeadlineFailurePenalty = 5;

        /// <summary>Personal-regard hit when the player openly abandons a quest — lighter
        /// than a silent deadline failure, because they were honest about withdrawing.</summary>
        private const int AbandonPenalty = 3;

        /// <summary>Cap on how many terminated quests we keep per NPC before pruning oldest.</summary>
        private const int MaxRetainedQuests = 10;

        /// <summary>
        ///   Applies a turn's quest outcomes: a newly offered quest (<c>[QUEST]</c>) and/or
        ///   an acknowledged completion (<c>[QUEST_COMPLETE]</c>). Chat lines are emitted
        ///   through <paramref name="notify"/> (text, isPositive).
        /// </summary>
        public static void ApplyResponse(NpcProfile giver, Hero giverHero, ParsedResponse response,
            int gameDay, IGameStateBridge bridge, BannerlordNpcMemoryStore store, Action<string, bool> notify)
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
        ///   Daily pass over every profile: cancel quests whose war premise ended, expire
        ///   overdue ones (with a relation penalty and a remembered slight), and prune the
        ///   oldest terminated quests so the list cannot grow without bound.
        /// </summary>
        public static void RunDailyMaintenance(BannerlordNpcMemoryStore store, int gameDay)
        {
            if (store == null) return;

            foreach (var id in store.ListIds())
            {
                NpcProfile profile = store.Get(id);
                if (profile == null || profile.ActiveQuests.Count == 0) continue;

                Hero giverHero = Hero.FindFirst(h => h.StringId == id);
                bool changed = false;

                foreach (InformalQuest quest in profile.ActiveQuests)
                {
                    if (quest.Status != QuestStatus.Active) continue;

                    if (quest.IsOutstanding && PremiseGone(quest, giverHero))
                    {
                        quest.Status = QuestStatus.Cancelled;
                        AddEvent(profile, gameDay, NotableEventType.Other,
                            $"Set aside the task to {Describe(quest)} — circumstances changed.");
                        changed = true;
                        continue;
                    }

                    if (quest.IsOutstanding && quest.DeadlineDay.HasValue && gameDay > quest.DeadlineDay.Value)
                    {
                        quest.Status = QuestStatus.Expired;
                        ApplyPenalty(profile);
                        AddEvent(profile, gameDay, NotableEventType.Conflict,
                            $"The player let the deadline lapse on the task to {Describe(quest)} — a disappointment.");
                        ModLog.Info($"[QUEST] expired for {profile.Name}: {Describe(quest)} (-{DeadlineFailurePenalty} regard).");
                        changed = true;
                    }
                }

                if (Prune(profile)) changed = true;
                if (changed) store.Set(profile);
            }
        }

        /// <summary>
        ///   Finds an outstanding letter the player is carrying for <paramref name="recipient"/>,
        ///   scanning every giver's quests. Returns the giver profile and the quest, or null.
        /// </summary>
        public static LetterErrand FindOutstandingLetterFor(Hero recipient, BannerlordNpcMemoryStore store)
        {
            if (recipient == null || store == null) return null;

            foreach (var id in store.ListIds())
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
        public static void MarkLetterDelivered(LetterErrand errand, Hero recipient, int gameDay,
            BannerlordNpcMemoryStore store)
        {
            if (errand == null || store == null) return;

            errand.Quest.SatisfiedOnDay = gameDay;
            errand.Quest.Evidence = $"Delivered the message to {recipient?.Name} on day {gameDay}";
            ModLog.Info($"[QUEST] letter delivered: {errand.Giver.Name} → {recipient?.Name} (day {gameDay}).");
            store.Set(errand.Giver);
        }

        #region private

        private static void IssueQuest(NpcProfile giver, Hero giverHero, QuestProposal proposal,
            int gameDay, BannerlordNpcMemoryStore store, Action<string, bool> notify)
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

            ModLog.Info($"[QUEST] {giver.Name} issued: {Describe(quest)} " +
                        $"(reward {quest.RewardGold}g/+{quest.RewardRelation}, deadline {quest.DeadlineDay?.ToString() ?? "none"}).");
            notify?.Invoke($"New task from {giver.Name}: {Describe(quest)}.", true);
        }

        private static void CompleteQuest(NpcProfile giver, Hero giverHero, QuestCompletionClaim claim,
            int gameDay, IGameStateBridge bridge, BannerlordNpcMemoryStore store, Action<string, bool> notify)
        {
            InformalQuest quest = giver.ActiveQuests.FirstOrDefault(q =>
                q.IsAwaitingReward && (claim.Type == null || q.Type == claim.Type.Value));

            if (quest == null)
            {
                ModLog.Info($"[QUEST] {giver.Name} claimed completion with no verified quest to match — ignored.");
                return;
            }

            ActionOutcome reward = bridge?.GrantQuestReward(giverHero, giver, quest.RewardGold, quest.RewardRelation);
            quest.Status = QuestStatus.Completed;
            AddEvent(giver, gameDay, NotableEventType.Collaboration, $"The player completed the task to {Describe(quest)}.");
            store.Set(giver);

            if (reward != null) notify?.Invoke(reward.Message, true);
            else notify?.Invoke($"{giver.Name} thanks you for {Describe(quest)}.", true);
        }

        private static void AbandonQuest(NpcProfile giver, QuestAbandonClaim claim, int gameDay,
            BannerlordNpcMemoryStore store, Action<string, bool> notify)
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
            giver.ReputationWithPlayer = after < -100 ? -100 : after;
            AddEvent(giver, gameDay, NotableEventType.Conflict,
                $"The player gave up the task to {Describe(quest)} — a broken word, however honest.");
            store.Set(giver);

            ModLog.Info($"[QUEST] {giver.Name}: player abandoned '{quest.Type}' (-{AbandonPenalty} regard).");
            notify?.Invoke($"You abandoned {giver.Name}'s task ({Describe(quest)}). −{AbandonPenalty} regard.", false);
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

        private static void ApplyPenalty(NpcProfile profile)
        {
            int after = profile.ReputationWithPlayer - DeadlineFailurePenalty;
            profile.ReputationWithPlayer = after < -100 ? -100 : after;
        }

        private static bool Prune(NpcProfile profile)
        {
            if (profile.ActiveQuests.Count <= MaxRetainedQuests) return false;

            // Drop the oldest terminated quests first; never drop a live one.
            List<InformalQuest> terminated = profile.ActiveQuests
                .Where(q => q.Status != QuestStatus.Active)
                .OrderBy(q => q.IssuedOnDay)
                .ToList();

            bool removed = false;
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

        private static void AddEvent(NpcProfile profile, int gameDay, NotableEventType type, string summary)
            => profile.Events.Add(new NotableEvent(gameDay, type, summary));

        /// <summary>One short clause describing the task, for chat lines and memory events.</summary>
        private static string Describe(InformalQuest q)
        {
            if (!string.IsNullOrWhiteSpace(q.Description)) return q.Description.TrimEnd('.');

            switch (q.Type)
            {
                case QuestType.BanditClear:     return $"clear bandits near {q.TargetSettlement}";
                case QuestType.BanditHideout:   return $"clear the hideout near {q.TargetSettlement}";
                case QuestType.AttackFaction:   return $"strike {q.TargetFactionName}";
                case QuestType.AttackLord:      return $"defeat {q.TargetHeroName}";
                case QuestType.RaidVillage:     return $"raid {q.TargetSettlement}";
                case QuestType.AttackCaravan:   return $"destroy a caravan of {q.TargetFactionName}";
                case QuestType.Siege:           return $"help take {q.TargetSettlement}";
                case QuestType.CapturePrisoner: return $"capture {q.TargetHeroName}";
                case QuestType.ExecuteEnemy:    return $"kill {q.TargetHeroName}";
                case QuestType.RescuePrisoner:  return $"free {q.TargetHeroName}";
                case QuestType.DeliverLetter:   return $"carry a message to {q.TargetHeroName}";
                default:                        return "carry out a task";
            }
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
