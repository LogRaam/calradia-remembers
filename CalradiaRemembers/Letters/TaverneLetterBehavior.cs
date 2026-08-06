// Code written by Gabriel Mailhot, 03/06/2026.
// Sprint 12d: player-to-NPC letters — taverne courier conversation hook.
// Sprint 12c: courier hire now costs 500 denars; hooked to the Tavern Keeper's
//             service hub ("tavernkeeper_talk"), not the Tavern Maid.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>
    ///   Registers a courier-hire option in the Tavern Keeper's service menu. The player
    ///   is prompted for a recipient name via <see cref="TextInquiryData" />, then the parchment
    ///   courier window opens in compose mode (charging <see cref="CourierHireCost" /> on send).
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
                110,
                CourierHireClickable);

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

        /// <summary>
        ///   M17 (courier audit): the option stays visible so the player knows the service exists, but greys out
        ///   WITH an explanation when they cannot pay the fee, instead of the old silent block (the Send button
        ///   greyed with no reason only after the player had already composed the letter).
        /// </summary>
        private static bool CourierHireClickable(out TextObject explanation)
        {
            if ((Hero.MainHero?.Gold ?? 0) < CourierHireCost)
            {
                explanation = new TextObject($"You cannot afford the courier ({CourierHireCost} denars).");

                return false;
            }

            explanation = new TextObject("");

            return true;
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

            // Per-correspondent armed escort (player report 2026-08-05: far letters lost almost every time). If this
            // thread is NOT yet escorted, offer the premium once before composing; an already-escorted thread rides
            // guarded for free (never re-offered, never recharged). The election gates the compose window so the
            // charge is settled before the player writes; the plain courier hire fee is still taken on send.
            if (!profile.EscortedChannel)
            {
                OfferEscortThenCompose(id, profile);

                return;
            }

            LetterWindowManager.OpenCompose(profile, CourierHireCost);
        }

        // ── Armed escort election ──────────────────────────────────────────────

        /// <summary>
        ///   Offers the one-off armed-escort premium for this correspondent's whole thread, then opens the compose
        ///   window whichever way the player answers. The option only appears when the player can actually afford
        ///   it (else the letter simply goes unescorted, as before). The premium scales with the map distance to
        ///   the recipient (<see cref="EscortCostPolicy.EscortPremium" />), the long road being the reported case.
        /// </summary>
        private static void OfferEscortThenCompose(string recipientId, NpcProfile profile)
        {
            Hero recipient = Hero.FindFirst(h => h.StringId == recipientId);
            int premium = EscortPremiumFor(recipient);
            int gold = Hero.MainHero?.Gold ?? 0;

            // Cannot afford the escort (or no recipient to price): send unescorted, no option shown.
            if (recipient == null || gold < premium)
            {
                LetterWindowManager.OpenCompose(profile, CourierHireCost);

                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                "Armed Escort",
                $"Hire an armed escort for your correspondence with {profile.Name}? For {premium} denars, every letter " +
                $"between you and {profile.Name} rides under guard, safe from bandits. Paid once: their replies and all " +
                "later letters ride escorted for free.",
                true, true,
                $"Hire escort ({premium} denars)",
                "Send unescorted",
                () => HireEscortThenCompose(profile, premium),
                () => LetterWindowManager.OpenCompose(profile, CourierHireCost)),
                pauseGameActiveState: false);
        }

        /// <summary>
        ///   Charges the escort premium and marks this correspondent's thread escorted, then opens compose. Re-checks
        ///   gold at accept time (the player could have spent it between prompts); if it no longer covers the fee the
        ///   thread stays unescorted and the letter is composed as an ordinary courier hire.
        /// </summary>
        private static void HireEscortThenCompose(NpcProfile profile, int premium)
        {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;

            if (store == null || (Hero.MainHero?.Gold ?? 0) < premium)
            {
                MBInformationManager.AddQuickInformation(new TextObject($"You cannot afford the escort ({premium} denars)."));
                LetterWindowManager.OpenCompose(profile, CourierHireCost);

                return;
            }

            Hero.MainHero.ChangeHeroGold(-premium);

            // Set the flag on the STORED profile (the single source of truth every courier spawn re-reads).
            NpcProfile stored = store.Get(profile.Id) ?? profile;
            stored.EscortedChannel = true;
            store.Set(stored);

            MBInformationManager.AddQuickInformation(new TextObject($"An armed escort now guards your letters to {stored.Name}."));
            ModLog.Info($"[PLAYER LETTER] armed escort hired for the {stored.Name} channel: {premium} denars.");

            LetterWindowManager.OpenCompose(stored, CourierHireCost);
        }

        /// <summary>The escort premium for a recipient, from the map distance between the player and them (safe on any engine hiccup).</summary>
        private static int EscortPremiumFor(Hero recipient)
        {
            try
            {
                return EscortCostPolicy.EscortPremium(PlayerToRecipientDistance(recipient));
            }
            catch (Exception ex)
            {
                ModLog.Error($"[PLAYER LETTER] escort premium calc failed: {ex.GetType().Name}: {ex.Message}");

                return EscortCostPolicy.EscortPremium(0f); // fall back to the base premium
            }
        }

        /// <summary>Map distance from the player to the recipient's current whereabouts (their party, else a settlement anchor).</summary>
        private static float PlayerToRecipientDistance(Hero recipient)
        {
            MapDistanceModel model = Campaign.Current?.Models?.MapDistanceModel;
            MobileParty player = MobileParty.MainParty;

            if (model == null || player == null || recipient == null) return 0f;

            Vec2 target = RecipientPosition(recipient);

            return model.GetDistance(player, new CampaignVec2(target, true), MobileParty.NavigationType.Default, out float _);
        }

        /// <summary>Where the recipient is right now: their party position, else the gate of whatever settlement anchors them.</summary>
        private static Vec2 RecipientPosition(Hero recipient)
        {
            if (recipient.PartyBelongedTo != null) return recipient.PartyBelongedTo.GetPosition2D;

            Settlement anchor = recipient.CurrentSettlement ?? recipient.StayingInSettlement ?? recipient.HomeSettlement;
            if (anchor != null) return anchor.GatePosition.ToVec2();

            return MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
        }
    }
}
