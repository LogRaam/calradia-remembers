// Code written by Gabriel Mailhot, 25/07/2026.
// ASSEMBLY Inc 1, the settlement-menu entry point: the player summons an assembly from a town or castle, sees
// it gather, and stands it down. The convene/RSVP/re-validation logic all lives elsewhere (AssemblyConvener,
// AssemblyBehavior, the pure policies); this is only the UI skin over it. Leaving the host cancels the
// assembly (Gabriel's ratified decision #4). The round-table SESSION that "opens" it is a later increment.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>The town/castle menu options to summon, watch, and stand down an assembly, plus the leave-cancels guard.</summary>
    public sealed class AssemblyMenuBehavior : CampaignBehaviorBase
    {
        private const int DeadlineDays = 7;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
        }

        public override void SyncData(IDataStore dataStore) { } // the assembly's state lives on AssemblyBehavior

        #region private

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            foreach (string menu in new[] {"town", "castle"})
            {
                starter.AddGameMenuOption(menu, "cr_assembly_summon", "Summon an assembly",
                    SummonCondition, SummonConsequence);
                starter.AddGameMenuOption(menu, "cr_assembly_open", "Open the assembly",
                    OpenCondition, OpenConsequence);
                starter.AddGameMenuOption(menu, "cr_assembly_status", "The gathering assembly",
                    StatusCondition, StatusConsequence);
            }
        }

        private static bool SummonCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            // Offer it only where a hall exists and nothing is already convened.
            Settlement here = Settlement.CurrentSettlement;

            return here != null && (here.IsTown || here.IsCastle) && AssemblyBehavior.Instance?.HasActiveAssembly != true;
        }

        private static void SummonConsequence(MenuCallbackArgs args)
        {
            Settlement host = Settlement.CurrentSettlement;

            if (host == null) return;

            List<InquiryElement> elements = InvitableHeroes()
                                            .Select(h => new InquiryElement(h.StringId, h.Name.ToString(), null))
                                            .ToList();

            if (elements.Count == 0)
            {
                MBInformationManager.AddQuickInformation(new TextObject("You know no one you could summon here yet."));

                return;
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    titleText: "Summon an Assembly",
                    descriptionText: $"Choose who to call to {host.Name} (they have {DeadlineDays} days to answer):",
                    inquiryElements: elements,
                    isExitShown: true,
                    minSelectableOptionCount: 1,
                    maxSelectableOptionCount: elements.Count,
                    affirmativeText: "Send the summons",
                    negativeText: "Cancel",
                    affirmativeAction: OnInviteesChosen,
                    negativeAction: null,
                    soundEventPath: "",
                    isSeachAvailable: true),
                pauseGameActiveState: false,
                prioritize: true);
        }

        private static void OnInviteesChosen(List<InquiryElement> selected)
        {
            Settlement host = Settlement.CurrentSettlement;

            if (host == null || selected == null || selected.Count == 0) return;

            List<Hero> chosen = selected
                                .Select(e => Hero.FindFirst(x => x.StringId == e.Identifier as string))
                                .Where(h => h != null)
                                .ToList();

            if (chosen.Count == 0) return;

            var today = (int) CampaignTime.Now.ToDays;
            AssemblyConvener.Convene(host, Hero.MainHero, chosen, today, today + DeadlineDays);

            AssemblyProgress p = AssemblyBehavior.Instance.Progress();
            MBInformationManager.AddQuickInformation(new TextObject(
                $"Assembly summoned at {host.Name}: {p.Present} present, {p.InTransit} on the way, {p.Declined} declined."));
            RefreshMenu(host);
        }

        private static bool OpenCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Conversation;

            return HostedHere() && HasArrivals();
        }

        private static void OpenConsequence(MenuCallbackArgs args)
        {
            string result = ChatManager.OpenForAssembly();

            // OpenForAssembly pushes the chat layer on success; if it could not (no arrivals, no window), surface why.
            if (!ChatManager.IsOpen) MBInformationManager.AddQuickInformation(new TextObject(result));
        }

        private static bool StatusCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            return HostedHere();
        }

        private static void StatusConsequence(MenuCallbackArgs args)
        {
            AssemblyBehavior b = AssemblyBehavior.Instance;

            if (b?.Pending == null) return;

            AssemblyProgress p = b.Progress();
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"{p.Present} present, {p.InTransit} on the way, {p.Declined} declined, {p.Awaiting} awaiting.");
            foreach (AssemblyInviteeRecord inv in b.Pending.Invitees)
            {
                Hero h = Hero.FindFirst(x => x.StringId == inv.HeroId);
                lines.AppendLine($"  {h?.Name?.ToString() ?? inv.HeroId}: {inv.Status}");
            }

            InformationManager.ShowInquiry(new InquiryData(
                "The Assembly",
                lines.ToString().TrimEnd(),
                true, true,
                "Stand it down", "Keep waiting",
                () =>
                {
                    Settlement host = Settlement.CurrentSettlement;
                    AssemblyBehavior.Instance?.Cancel();
                    RefreshMenu(host);
                },
                null));
        }

        // Leaving the host stands the assembly down (ratified decision #4: the player must wait for people to
        // arrive; walking away calls it off).
        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            if (party != MobileParty.MainParty || settlement == null) return;

            AssemblyBehavior b = AssemblyBehavior.Instance;

            if (b?.HasActiveAssembly == true && b.Pending?.HostSettlementId == settlement.StringId)
            {
                b.Cancel();
                MBInformationManager.AddQuickInformation(new TextObject("You left, so the assembly is called off."));
            }
        }

        /// <summary>True when an assembly is convened at the settlement the player is currently in.</summary>
        private static bool HostedHere()
        {
            AssemblyBehavior b = AssemblyBehavior.Instance;

            return b?.HasActiveAssembly == true && b.Pending?.HostSettlementId == Settlement.CurrentSettlement?.StringId;
        }

        /// <summary>True when at least one invitee has reached the hall in person or sent an envoy: there is a table to open.</summary>
        private static bool HasArrivals()
            => AssemblyBehavior.Instance?.Pending?.Invitees?.Any(
                i => i.Status == AssemblyInviteeStatus.Present || i.Status == AssemblyInviteeStatus.Ambassador) == true;

        /// <summary>The heroes worth offering in the picker: your companions, your clan, and lords you already know.</summary>
        private static IEnumerable<Hero> InvitableHeroes()
        {
            var set = new HashSet<Hero>();
            Clan clan = Clan.PlayerClan;

            if (clan?.Companions != null)
                foreach (Hero h in clan.Companions)
                    if (Eligible(h))
                        set.Add(h);

            if (clan?.Heroes != null)
                foreach (Hero h in clan.Heroes)
                    if (Eligible(h))
                        set.Add(h);

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store != null)
                foreach (string id in store.ListIds())
                {
                    Hero h = Hero.FindFirst(x => x.StringId == id);

                    if (h != null && h.IsLord && Eligible(h)) set.Add(h);
                }

            return set;
        }

        private static bool Eligible(Hero h)
            => h != null && h != Hero.MainHero && h.IsAlive && !h.IsPrisoner;

        private static void RefreshMenu(Settlement host)
        {
            try
            {
                if (host != null) GameMenu.SwitchToMenu(host.IsCastle ? "castle" : "town");
            }
            catch
            {
                /* refreshing the menu is best-effort; the options re-evaluate on the next open regardless */
            }
        }

        #endregion
    }
}
