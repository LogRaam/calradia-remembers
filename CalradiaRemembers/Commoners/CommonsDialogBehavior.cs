// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
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
      // Vanilla quests whose clues come from talking to random townsfolk; CR's idle chat is withheld
      // while one is ongoing so it does not divert the quest conversation. Matched by type NAME so no
      // SandBox assembly reference is needed.
      private static readonly System.Collections.Generic.HashSet<string> TownsfolkDependentQuests = new(System.StringComparer.Ordinal) {"TheSpyPartyIssueQuest"};

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
            null);

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

      #region private

      // ── Private ───────────────────────────────────────────────────────────

      private static bool IsCommonerEligible()
      {
         // Engine adapter: gather the facts and let the pure, tested policy decide. The eligibility
         // rule (and the occupation whitelist that excludes hideout captains, tavern keepers, etc.)
         // lives in CommonerChatContext so it is protected by unit tests against regressions.
         Settlement settlement = Hero.MainHero?.CurrentSettlement;
         CharacterObject partner = Campaign.Current?.ConversationManager?.OneToOneConversationCharacter;

         var context = new CommonerChatContext(
            Hero.OneToOneConversationHero != null,
            settlement != null && (settlement.IsTown || settlement.IsVillage || settlement.IsCastle),
            partner != null,
            partner?.Occupation.ToString(),
            NpcMemoryCampaignBehavior.Instance?.LlmServices?.IsAvailable == true,
            TownsfolkDependentQuestActive());

         return context.ShouldOfferIdleChat();
      }

      private static bool TownsfolkDependentQuestActive()
         => Campaign.Current?.QuestManager?.Quests?.Any(
               q => q != null && q.IsOngoing && TownsfolkDependentQuests.Contains(q.GetType().Name)) == true;

      private static void OnCommonerSelected()
      {
         Settlement settlement = Hero.MainHero?.CurrentSettlement;

         if (settlement == null) return;

         CommonsKnowledge knowledge = CommonsKnowledgeBuilder.Build(settlement);
         NpcProfile profile = CommonsProfileFactory.Create(settlement);

         ChatManager.OpenForCommoner(profile, knowledge);
      }

      #endregion
   }
}