// Code written by Gabriel Mailhot, 03/06/2026.
// Sprint 12d: player-to-NPC letters — taverne courier conversation hook.

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Registers a courier-hire option in the tavernkeeper conversation. The player
    ///   is prompted for a recipient name, then for the message body. Both steps use
    ///   <see cref="TextInquiryData" /> so they work on any screen. The chat window
    ///   is opened in compose mode to let the player write their message.
    ///
    ///   NOTE: The taverne conversation token ("tavernkeeper") has not been validated
    ///   in-game. The primary test path for Sprint 12d is <c>cr.compose [name]</c>.
    /// </summary>
    public sealed class TaverneLetterBehavior
    {
        /// <summary>Adds the courier-hire dialog lines to the campaign's conversation pool.</summary>
        public void Register(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "cr_hire_courier_option",
                "tavernkeeper",
                "cr_hire_courier_confirm",
                "I need to hire a courier to send a letter.",
                CanHireCourier,
                null,
                100);

            starter.AddDialogLine(
                "cr_hire_courier_confirm_line",
                "cr_hire_courier_confirm",
                "close_window",
                "Of course. A rider leaves within the hour. Who shall receive your message?",
                null,
                OnHireCourier);
        }

        // ── Conditions ────────────────────────────────────────────────────────

        private static bool CanHireCourier()
        {
            if (CharacterObject.OneToOneConversationCharacter?.Occupation != Occupation.TavernWench)
                return false;

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return false;

            return store.ListIds().Any(id =>
            {
                Hero h = Hero.FindFirst(x => x.StringId == id);
                return h != null && h.IsAlive && h != Hero.MainHero;
            });
        }

        // ── Consequences ──────────────────────────────────────────────────────

        private static void OnHireCourier()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(
                titleText:                "Hire a Courier",
                text:                     "Enter the name of the recipient:",
                isAffirmativeOptionShown: true,
                isNegativeOptionShown:    true,
                affirmativeText:          "Search",
                negativeText:             "Cancel",
                affirmativeAction:        OnRecipientNameEntered,
                negativeAction:           null,
                shouldInputBeObfuscated:  false,
                textCondition:            null,
                soundEventPath:           "",
                defaultInputText:         ""), pauseGameActiveState: false);
        }

        private static void OnRecipientNameEntered(string nameFragment)
        {
            if (string.IsNullOrWhiteSpace(nameFragment)) return;

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            // Find the first known NPC whose name matches the fragment.
            NpcProfile profile = null;
            foreach (string id in store.ListIds())
            {
                Hero h = Hero.FindFirst(x => x.StringId == id);
                if (h == null || !h.IsAlive || h == Hero.MainHero) continue;

                if (h.Name?.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    profile = store.Get(id);
                    break;
                }
            }

            if (profile == null)
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject($"No known NPC named '{nameFragment}' found."));
                return;
            }

            string recipientName = profile.Name;
            ChatManager.OpenForCompose(profile);
        }
    }
}
