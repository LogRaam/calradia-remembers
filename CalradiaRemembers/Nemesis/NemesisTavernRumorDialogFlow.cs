// Code written by Gabriel Mailhot, 21/08/2026.
// Nemesis pillar, discoverability piece: the last open bit noted in the ROADMAP ("a standalone
// tavern-keeper rumour variant remains an optional minor follow-up" to the dossier + NemesisBounty
// interception trigger shipped in v2.1.2). A tavern keeper can drop a rumour that NAMES a nemesis
// encountered by the player and his region, pointing toward the existing lord-bounty channel
// (NemesisBountyDialogFlow) without issuing or settling anything itself: no reward figure, no accept/
// decline, no quest state. Player-initiated (the player asks), so unlike the lord's proactive bounty
// offer there is no cooldown or spam concern to gate.
// Eligibility could not reuse NemesisBountyDialogFlow.FindEligibleNemesisFor verbatim: that gate requires
// `lord.IsLord`, which a tavern keeper (a settlement Notable) never satisfies, and it also folds in
// "already bountied by this giver" bookkeeping that has no meaning for a keeper who issues nothing. So
// this class keeps its own small FindRumorNemesisFor query, reusing the shared pure
// NemesisBountyPolicy.RegionMatches (not re-deriving the region-key resolution order) and adding the one
// extra gate the discoverability design calls for: the nemesis must be ENCOUNTERED (TimesEncountered > 0,
// the same signal NemesisDossierFactory gates the encyclopedia page on), so a latent nemesis never gets
// spoiled by tavern gossip before the player has ever met him.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Nemesis;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Nemesis
{
    /// <summary>
    ///   Registers the player-asked rumour line at the tavern keeper's <c>"tavernkeeper_talk"</c> hub:
    ///   shown only when the keeper's own settlement/culture region matches a living, not-turned nemesis
    ///   the player has already encountered. The keeper's reply names him and his region and gestures at
    ///   the existing bounty channel, then the conversation loops back to the hub.
    /// </summary>
    public static class NemesisTavernRumorDialogFlow
    {
        // Text variable substituted into the keeper's reply line (set right before the reply is reached, so
        // it always reflects the nemesis this exact conversation resolved for this exact keeper).
        private const string RumorLineVariable = "CR_NEMESIS_TAVERN_RUMOR_LINE";

        private const string AskOptionText = "Heard any talk of trouble on the roads lately?";

        /// <summary>Adds the rumour-ask line and its reply to the tavern keeper's service hub.</summary>
        public static void Register(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "calradia_remembers_nemesis_tavern_rumor_ask",
                "tavernkeeper_talk",
                "cr_nemesis_tavern_rumor_reply",
                AskOptionText,
                CanOfferRumor,
                PrepareRumorReply,
                105);

            starter.AddDialogLine(
                "calradia_remembers_nemesis_tavern_rumor_reply_line",
                "cr_nemesis_tavern_rumor_reply",
                "tavernkeeper_talk",
                $"{{{RumorLineVariable}}}",
                null,
                null,
                105);
        }

        #region private

        /// <summary>
        ///   True when the conversation partner (the tavern keeper) has an eligible rumour to share.
        ///   Re-derived every time the hub is shown, exactly like <see cref="NemesisBountyDialogFlow" />'s
        ///   own gate: nothing is marked "already told", the rumour simply reflects the current world.
        /// </summary>
        private static bool CanOfferRumor() => FindRumorNemesisFor(Hero.OneToOneConversationHero) != null;

        /// <summary>
        ///   The first living, not-turned, ENCOUNTERED nemesis whose region matches this keeper's own
        ///   settlement/culture, or null. Internal so the self-test harness can prove the exact same gate
        ///   the dialogue line uses. Deliberately narrower than
        ///   <see cref="NemesisBountyDialogFlow.FindEligibleNemesisFor" />: a keeper is never a bounty
        ///   giver, so "already bountied by this giver" does not apply here, but a rumour about a nemesis
        ///   the player has never actually met would spoil the pillar's reveal, hence the added
        ///   TimesEncountered gate that the bounty flow does not need.
        /// </summary>
        internal static NemesisIdentity FindRumorNemesisFor(Hero keeper)
        {
            if (keeper == null || keeper == Hero.MainHero || !keeper.IsAlive) return null;

            NemesisBehavior roster = NemesisBehavior.Instance;
            if (roster == null) return null;

            string homeId = keeper.HomeSettlement?.StringId;
            string currentId = keeper.CurrentSettlement?.StringId;
            string cultureId = keeper.Culture?.StringId;

            foreach (NemesisIdentity nem in roster.All)
            {
                if (string.IsNullOrEmpty(nem.HeroStringId)) continue; // never manifested, nothing to gossip about
                if (nem.TimesEncountered <= 0) continue; // discoverability gate: never spoil a latent nemesis
                if (!nem.IsAlive || nem.IsTurned) continue; // dead or turned is no longer a threat worth naming

                if (NemesisBountyPolicy.RegionMatches(new NemesisBountyPolicy.RegionMatchFacts {LordHomeSettlementId = homeId, LordCurrentSettlementId = currentId, LordCultureId = cultureId, NemesisRegionId = nem.RegionId}))
                    return nem;
            }

            return null;
        }

        /// <summary>
        ///   Re-validates the gate (the prompt line is advice, this is law) and composes the keeper's
        ///   reply into <see cref="RumorLineVariable" /> right before the reply line is reached, so the
        ///   text substituted in matches exactly the nemesis this click resolved for.
        /// </summary>
        private static void PrepareRumorReply()
        {
            try
            {
                Hero keeper = Hero.OneToOneConversationHero;
                NemesisIdentity nem = FindRumorNemesisFor(keeper);

                if (nem == null)
                {
                    ModLog.Error("[NEMESIS] tavern rumor reached with no eligible nemesis (gate re-check failed).");
                    MBTextManager.SetTextVariable(RumorLineVariable, "");

                    return;
                }

                string displayName = $"{nem.Name} {nem.Epithet}";
                string regionDisplay = NemesisDossierFactory.ResolveRegionDisplay(nem.RegionId);
                string line = NemesisRumorLine.Compose(displayName, regionDisplay);

                MBTextManager.SetTextVariable(RumorLineVariable, line);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[NEMESIS] PrepareRumorReply failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion
    }
}
