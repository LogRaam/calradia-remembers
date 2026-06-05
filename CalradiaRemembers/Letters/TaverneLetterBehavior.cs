// Code written by Gabriel Mailhot, 03/06/2026.
// Sprint 12d: player-to-NPC letters — taverne courier conversation hook.
// Sprint 12c: courier hire now costs 500 denars; hooked to the Tavern Keeper's
//             service hub ("tavernkeeper_talk"), not the Tavern Maid.

#region

using System.Collections.Generic;
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
    ///   Registers a courier-hire option in the Tavern Keeper's service menu. The player
    ///   is prompted for a recipient name via <see cref="TextInquiryData" />, then the chat
    ///   window opens in compose mode (charging <see cref="CourierHireCost" /> on send).
    ///   Hooked to the keeper's <c>"tavernkeeper_talk"</c> hub — the Tavern Maid serves
    ///   drinks only and is not the right vendor for this.
    /// </summary>
    public sealed class TaverneLetterBehavior
    {
        /// <summary>Denars charged for hiring a courier at the tavern.</summary>
        public const int CourierHireCost = 500;

        /// <summary>
        ///   Adds the courier-hire option to the Tavern Keeper's service menu.
        ///   The keeper's player-option hub uses the <c>"tavernkeeper_talk"</c> token —
        ///   the same hub that lists companion recruitment and "people to hire". The
        ///   option loops back to that hub so the conversation continues naturally.
        /// </summary>
        public void Register(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "cr_hire_courier_option",
                "tavernkeeper_talk",
                "cr_hire_courier_confirm",
                $"I need to hire a courier to send a letter. ({CourierHireCost} denars)",
                CanHireCourier,
                null,
                110);

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
            // The "tavernkeeper_talk" token already scopes this to a tavern keeper, so we
            // only need at least one known, living, reachable NPC the player could write to.
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return false;

            return store.ListIds().Any(id =>
            {
                Hero h = Hero.FindFirst(x => x.StringId == id);
                return h != null && h != Hero.MainHero && CourierService.CanReceiveLetter(h);
            });
        }

        // ── Consequences ──────────────────────────────────────────────────────

        /// <summary>
        ///   Presents a searchable list of every known, living, and reachable NPC the
        ///   player could write to. Selecting one opens the compose window. A list
        ///   removes the ambiguity and typos of free-text name entry.
        /// </summary>
        private static void OnHireCourier()
        {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;

            var elements = new List<InquiryElement>();
            foreach (string id in store.ListIds())
            {
                Hero h = Hero.FindFirst(x => x.StringId == id);
                if (h == null || h == Hero.MainHero || !CourierService.CanReceiveLetter(h)) continue;
                elements.Add(new InquiryElement(id, h.Name.ToString(), null));
            }

            if (elements.Count == 0)
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject("You know no one you could write to (or reach) yet."));
                return;
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    titleText:                "Hire a Courier",
                    descriptionText:          $"Choose the recipient ({CourierHireCost} denars on dispatch):",
                    inquiryElements:          elements,
                    isExitShown:              true,
                    minSelectableOptionCount: 1,
                    maxSelectableOptionCount: 1,
                    affirmativeText:          "Write Letter",
                    negativeText:             "Cancel",
                    affirmativeAction:        OnRecipientSelected,
                    negativeAction:           null,
                    soundEventPath:           "",
                    isSeachAvailable:         true), // (sic) — the engine API misspells "Search"
                pauseGameActiveState: false,
                prioritize:           true);
        }

        private static void OnRecipientSelected(List<InquiryElement> selected)
        {
            if (selected == null || selected.Count == 0) return;

            var id = selected[0].Identifier as string;
            if (string.IsNullOrEmpty(id)) return;

            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(id);
            if (profile == null) return;

            ChatManager.OpenForCompose(profile, CourierHireCost);
        }
    }
}
