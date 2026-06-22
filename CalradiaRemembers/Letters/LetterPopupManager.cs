// Code written by Gabriel Mailhot, 02/06/2026.
// Sprint 12b: NPC-initiated letter correspondence — delivery presenter.
// 21/06/2026: letters left the chat overlay for a dedicated parchment window (LetterWindowManager).
// This now records the "received" bookkeeping once on arrival and opens that window in read mode; the
// reply is offered inside the window itself (no second popup, no chat overlay).

#region

using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Presents a delivered letter to the player on the parchment courier window. Marks the letter
    ///   received (and opens any letter-triggered quest) once on arrival, then shows it for reading;
    ///   Acknowledge or Reply are chosen inside the window.
    /// </summary>
    public static class LetterPopupManager
    {
        /// <summary>Presents the letter. Safe to call from the daily/courier tick (main thread).</summary>
        public static void Show(PendingLetter letter, NpcProfile senderProfile)
        {
            if (letter == null || senderProfile == null) return;

            int today = (int)CampaignTime.Now.ToDays;
            string content = letter.Content ?? "(The letter is blank.)";
            StoryLog.WriteLetterReceived(letter.SenderName, content, today);

            MarkReceived(letter, senderProfile, today);
            LetterWindowManager.OpenRead(letter, senderProfile);
        }

        // ── Bookkeeping ────────────────────────────────────────────────────

        /// <summary>
        ///   Records that the player has now seen the letter: marks it delivered, files a memory of it
        ///   on the sender, persists, and opens any quest the letter carries. Done once on arrival so a
        ///   letter never re-pops, whether the player then acknowledges, replies, or simply closes.
        /// </summary>
        private static void MarkReceived(PendingLetter letter, NpcProfile profile, int today)
        {
            letter.IsDelivered = true;
            profile.Events.Add(new NotableEvent(today, NotableEventType.Other,
                $"Received a letter from {profile.Name} ({letter.Reason})."));

            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            behavior?.Store.Set(profile);

            if (letter.Reason == LetterReason.ChildSupportRequest)
                OpenChildSupportQuest(profile, today, behavior?.Store);

            ModLog.Info($"[LETTER] received from {profile.Name} ({letter.Reason}).");
        }

        /// <summary>
        ///   Creates a <see cref="QuestType.ProvideGold" /> quest on the mother's profile and opens its
        ///   vanilla journal mirror. Idempotent — skips if an outstanding ProvideGold quest already exists.
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
