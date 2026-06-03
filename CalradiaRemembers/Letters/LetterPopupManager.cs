// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 12b: NPC-initiated letter correspondence — popup display.

#region

using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Shows a letter delivery popup to the player using the vanilla
    ///   <see cref="InformationManager.ShowInquiry" /> system. Two buttons:
    ///   "Acknowledge" (marks delivered, no reply) and "Reply" (opens the chat
    ///   window in letter-reply mode so the player can compose a response).
    /// </summary>
    public static class LetterPopupManager
    {
        /// <summary>
        ///   Presents the letter popup. Safe to call from the daily tick (main thread).
        /// </summary>
        public static void Show(PendingLetter letter, NpcProfile senderProfile)
        {
            if (letter == null || senderProfile == null) return;

            string title   = $"A letter from {letter.SenderName}";
            string content = letter.Content ?? "(The letter is blank.)";

            InformationManager.ShowInquiry(new InquiryData(
                titleText:              title,
                text:                   content,
                isAffirmativeOptionShown: true,
                isNegativeOptionShown:  true,
                affirmativeText:        "Acknowledge",
                negativeText:           "Reply by letter",
                affirmativeAction:      () => OnAcknowledge(letter, senderProfile),
                negativeAction:         () => OnReply(letter, senderProfile)
            ), pauseGameActiveState: true);
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private static void OnAcknowledge(PendingLetter letter, NpcProfile profile)
        {
            letter.IsDelivered = true;

            int today = (int)CampaignTime.Now.ToDays;
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other,
                $"Received a letter from {profile.Name} ({letter.Reason})."));

            var behavior = NpcMemoryCampaignBehavior.Instance;
            behavior?.Store.Set(profile);

            // Open a child-support quest in the vanilla journal when this letter arrives.
            if (letter.Reason == LetterReason.ChildSupportRequest)
                OpenChildSupportQuest(profile, today, behavior?.Store);

            ModLog.Info($"[LETTER] acknowledged from {profile.Name}.");
        }

        private static void OnReply(PendingLetter letter, NpcProfile profile)
        {
            letter.IsDelivered = true;

            int today = (int)CampaignTime.Now.ToDays;
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other,
                $"Received a letter from {profile.Name} ({letter.Reason}) and chose to reply."));

            var behavior = NpcMemoryCampaignBehavior.Instance;
            behavior?.Store.Set(profile);

            if (letter.Reason == LetterReason.ChildSupportRequest)
                OpenChildSupportQuest(profile, today, behavior?.Store);

            Hero npcHero = Hero.FindFirst(h => h.StringId == profile.Id);
            if (npcHero == null)
            {
                ModLog.Error($"[LETTER] OnReply: hero not found for {profile.Name}.");
                return;
            }

            ChatManager.OpenForLetterReply(profile, npcHero, letter);
        }

        /// <summary>
        ///   Creates a <see cref="QuestType.ProvideGold" /> quest on the mother's profile
        ///   and opens its vanilla journal mirror. Called once when the player first reads
        ///   the <see cref="LetterReason.ChildSupportRequest" /> letter.
        ///   Idempotent — skips silently if an outstanding ProvideGold quest already exists.
        /// </summary>
        private static void OpenChildSupportQuest(NpcProfile profile, int today,
            Storage.BannerlordNpcMemoryStore store)
        {
            if (store == null) return;
            if (profile.ActiveQuests.Any(q => q.Type == QuestType.ProvideGold && q.IsOutstanding)) return;

            Hero mother = Hero.FindFirst(h => h.StringId == profile.Id);
            if (mother == null) return;

            var quest = new InformalQuest {
                Type        = QuestType.ProvideGold,
                Description = $"Provide financial support for your child with {profile.Name}.",
                IssuedOnDay = today,
                DeadlineDay = today + 30,
                RewardGold     = 0,
                RewardRelation = 5,
                Status         = QuestStatus.Active
            };

            profile.ActiveQuests.Add(quest);
            store.Set(profile);
            QuestJournalBridge.OpenMirror(quest, mother);
            ModLog.Info($"[QUEST] ProvideGold opened for {profile.Name} (deadline day {today + 30}).");
        }
    }
}
