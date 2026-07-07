// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 3 (bounty missions, the world hook): a lord tasks the player to hunt down a
// specific active nemesis, verified by the real capture or execution event, reusing the existing quest
// framework end to end (InformalQuest + its CalradiaRemembersQuest journal mirror + QuestDeedTracker's
// hero-killed/hero-prisoner-taken hooks, both widened to recognize QuestType.NemesisBounty alongside
// their ordinary ExecuteEnemy/CapturePrisoner cases). The offer itself is scripted, never LLM-proposed:
// by the time it is shown, the target and the reward are already fixed, deterministic facts. Registered
// on the same hero_main_options hub LlmDialogFlow and NemesisPrisonerDialogFlow already use, so it shows
// up in the ordinary conversation with any eligible lord, above the ordinary "A word with you." line.
// Eligibility (region rule only; see NemesisBountyPolicy's own header for why the optional "past victim"
// rule is not built) and the reward math (NemesisBountyRewardPolicy) are pure and unit-tested; this class
// is the engine glue: resolving a Hero's region keys, showing the accept/decline inquiry, and issuing the
// quest through QuestService.IssueNemesisBounty on acceptance.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Nemesis
{
    /// <summary>
    ///   Registers the scripted bounty-offer line at <c>hero_main_options</c>: shown only when the
    ///   conversation partner is a lord eligible to put a price on a living, unresolved, not-already-
    ///   bountied nemesis (<see cref="NemesisBountyPolicy" />). Accepting issues the quest via
    ///   <see cref="QuestService.IssueNemesisBounty" />; declining leaves the offer available to be shown
    ///   again on a later conversation (nothing is marked "declined", eligibility is simply re-evaluated
    ///   every time).
    /// </summary>
    public static class NemesisBountyDialogFlow
    {
        // Ordinal placement: below NemesisPrisonerDialogFlow's captor-scene line (116, shown only to a
        // held nemesis) and above LlmDialogFlow's ordinary "A word with you." (110), so an eligible lord's
        // bounty offer stands out ahead of the plain chat option.
        private const int BountyOfferPriority = 112;

        private const string OfferOptionText =
            "Word reaches me of a bandit chief who still troubles these lands. Would you take up the hunt for coin?";

        /// <summary>Adds the bounty-offer line to the hub every ordinary hero conversation reaches.</summary>
        public static void Register(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "calradia_remembers_nemesis_bounty_offer",
                "hero_main_options",
                "cr_nemesis_bounty_dummy_token",
                OfferOptionText,
                CanOfferBounty,
                OfferBounty,
                BountyOfferPriority);
        }

        #region private

        /// <summary>
        ///   Accepts the offer: issues the quest on the lord's profile and reports the outcome via a
        ///   simple acknowledgement popup (this line is reached from an ordinary conversation, not a live
        ///   chat window, so there is no chat transcript to append to).
        /// </summary>
        private static void AcceptBounty(Hero lord, Hero nemesisHero, string nemesisDisplayName, int gold, int relation)
        {
            try
            {
                NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;

                if (behavior == null || lord == null || nemesisHero == null) return;

                NpcProfile profile = behavior.Store?.Get(lord.StringId) ?? HeroProfileMapper.FromHero(lord);
                int day = behavior.GameStateBridge.GetCurrentGameDay();

                InformalQuest quest = QuestService.IssueNemesisBounty(
                    profile, lord, nemesisHero, nemesisDisplayName, gold, relation, day, behavior.Store);

                string message = quest != null
                    ? $"{lord.Name} has set a bounty on {nemesisDisplayName}: {gold} denars for his capture or death."
                    : $"{lord.Name} already has an unfinished task for you.";

                ShowAcknowledgement("Bounty accepted", message);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[NEMESIS] AcceptBounty failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool CanOfferBounty() => FindEligibleNemesisFor(Hero.OneToOneConversationHero) != null;

        /// <summary>
        ///   The first living, Hero-bound nemesis the given lord is eligible to put a bounty on, or null.
        ///   Public (well, internal to the flow but exercised directly) so the console force-offer command
        ///   and the self-test harness can prove the exact same gate the dialogue line uses.
        /// </summary>
        internal static NemesisIdentity FindEligibleNemesisFor(Hero lord)
        {
            if (lord == null || lord == Hero.MainHero || !lord.IsLord || !lord.IsAlive) return null;

            NemesisBehavior roster = NemesisBehavior.Instance;
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

            if (roster == null || store == null) return null;

            string homeId = lord.HomeSettlement?.StringId;
            string currentId = lord.CurrentSettlement?.StringId;
            string cultureId = lord.Culture?.StringId;

            foreach (NemesisIdentity nem in roster.All)
            {
                if (string.IsNullOrEmpty(nem.HeroStringId)) continue; // never manifested, nothing to hunt yet

                bool regionMatches = NemesisBountyPolicy.RegionMatches(homeId, currentId, cultureId, nem.RegionId);
                bool alreadyActive = QuestService.HasActiveBountyFor(nem.HeroStringId, store);

                if (NemesisBountyPolicy.IsEligibleIssuer(nem.IsAlive, alreadyActive, regionMatches))
                    return nem;
            }

            return null;
        }

        /// <summary>
        ///   Shows the offer: names the nemesis, states the reward (scaled by his notoriety), and lets the
        ///   player accept or decline. Re-validates everything at click time (the bridge/gate is law, the
        ///   line shown earlier is only advice), in case the world moved between the option appearing and
        ///   being picked.
        /// </summary>
        private static void OfferBounty()
        {
            try
            {
                Hero lord = Hero.OneToOneConversationHero;
                NemesisIdentity nem = FindEligibleNemesisFor(lord);

                if (lord == null || nem == null) return;

                Hero nemesisHero = Hero.FindFirst(h => h != null && h.StringId == nem.HeroStringId);

                if (nemesisHero == null) return;

                int gold = NemesisBountyRewardPolicy.ComputeGold(nem.RecaptureCount, nem.TimesEncountered);
                int relation = NemesisBountyRewardPolicy.ComputeRelation(nem.RecaptureCount);
                string displayName = $"{nem.Name} {nem.Epithet}";

                var title = new TextObject($"A bounty from {lord.Name}");
                var body = new TextObject(
                    $"{displayName} preys on these lands still, and I have had my fill of it. Bring me his " +
                    $"chains, or his head, and I shall pay you {gold} denars for it.");

                InformationManager.ShowInquiry(new InquiryData(
                    title.ToString(), body.ToString(),
                    true, true,
                    "I will hunt him down.", "Not this day.",
                    () => AcceptBounty(lord, nemesisHero, displayName, gold, relation),
                    null), true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[NEMESIS] OfferBounty failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void ShowAcknowledgement(string title, string body)
        {
            try
            {
                InformationManager.ShowInquiry(new InquiryData(title, body, true, false, "OK", null, null, null), true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[NEMESIS] ShowAcknowledgement failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion
    }
}
