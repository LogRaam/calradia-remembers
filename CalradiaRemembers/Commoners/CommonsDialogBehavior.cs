// Code written by Gabriel Mailhot, 18/06/2026.

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Commoners
{
    /// <summary>
    ///   Adds a "What's the news here?" option when the player talks to a random
    ///   non-Hero agent inside a settlement. Hooks at the universal <c>start</c> token
    ///   so it fires for townspeople, villagers, and castle servants — anyone who is
    ///   NOT a named Hero (<see cref="Hero.OneToOneConversationHero" /> is null).
    ///   On selection, the vanilla dialog closes and the CR commoner chat window opens.
    /// </summary>
    public sealed class CommonsDialogBehavior
    {
        /// <summary>
        ///   Registers the commoner chat option into the campaign's dialog flow.
        ///   Call from <see cref="NpcMemoryCampaignBehavior.OnSessionLaunched" />.
        /// </summary>
        public void Register(CampaignGameStarter starter)
        {
            // Player option — appears on the vanilla choice list when talking to
            // any non-Hero agent inside a settlement.
            starter.AddPlayerLine(
                "cr_commoner_ask_news",
                "start",
                "cr_commoner_response",
                "What's the news here?",
                IsCommonerEligible,
                null,
                100);

            // NPC's reply — immediately closes the vanilla dialog and opens the
            // CR commoner chat window in the consequence.
            starter.AddDialogLine(
                "cr_commoner_response_line",
                "cr_commoner_response",
                "close_window",
                "Aye, a word. What would you know?",
                null,
                OnCommonerSelected);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static bool IsCommonerEligible()
        {
            // Must be a non-Hero agent (random townsperson, not a named lord or notable).
            if (Hero.OneToOneConversationHero != null) return false;

            // Must be inside a settlement — villages, towns, or castles.
            Settlement settlement = Hero.MainHero?.CurrentSettlement;
            if (settlement == null) return false;

            // Respect the mod's availability check (API key configured, etc.).
            return NpcMemoryCampaignBehavior.Instance?.LlmServices?.IsAvailable == true;
        }

        private static void OnCommonerSelected()
        {
            Settlement settlement = Hero.MainHero?.CurrentSettlement;
            if (settlement == null) return;

            CommonsKnowledge knowledge = CommonsKnowledgeBuilder.Build(settlement);
            NpcProfile       profile   = CommonsProfileFactory.Create(settlement);

            ChatManager.OpenForCommoner(profile, knowledge);
        }
    }
}
