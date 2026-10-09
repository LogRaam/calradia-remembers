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
   ///   Adds a "What's the news here?" option when the player talks to a random Townsfolk or Villager inside
   ///   a settlement. Attached at the VANILLA commoner HUB token <c>town_or_village_player</c>, NOT the raw
   ///   <c>start</c> token (compatibility fix, player report 2026-07-27): a player line at <c>start</c> turned
   ///   the conversation entry itself into a player choice and preempted the vanilla greeting that routes
   ///   townsfolk to that hub, so any other mod adding options there (e.g. Immersive Recruit Commoners, whose
   ///   recruit options also live at <c>town_or_village_player</c>) had its options blocked out. At the hub,
   ///   CR's option sits ALONGSIDE the vanilla and third-party ones. The hub is reached only for Townsfolk and
   ///   Villager occupations, which is exactly the set <see cref="CommonerChatContext" /> already whitelists,
   ///   so no target is lost. On selection, the vanilla dialog closes and the CR commoner chat window opens.
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
         // Player option, sitting at the vanilla commoner HUB (town_or_village_player), so it coexists with the
         // vanilla options and any other mod's (e.g. Immersive Recruit Commoners) instead of preempting the
         // conversation entry the way a line hooked at the raw "start" token did.
         starter.AddPlayerLine(
            "cr_commoner_ask_news",
            "town_or_village_player",
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

         var context = new CommonerChatContext {IsHeroConversation = Hero.OneToOneConversationHero != null, IsCivilianSettlement = settlement != null && (settlement.IsTown || settlement.IsVillage || settlement.IsCastle), HasConversationPartner = partner != null, PartnerOccupation = partner?.Occupation.ToString(), LlmAvailable = NpcMemoryCampaignBehavior.Instance?.LlmServices?.IsAvailable == true, TownsfolkQuestActive = TownsfolkDependentQuestActive()};

         return context.ShouldOfferIdleChat();
      }

      private static bool TownsfolkDependentQuestActive()
         => Campaign.Current?.QuestManager?.Quests?.Any(
               q => q != null && q.IsOngoing && TownsfolkDependentQuests.Contains(q.GetType().Name)) == true;

      private static void OnCommonerSelected()
      {
         Settlement settlement = Hero.MainHero?.CurrentSettlement;

         if (settlement == null) return;

         // Use the ACTUAL villager's gender (player report: a female villager was voiced as a man because the
         // profile's gender was a coin flip disconnected from who is on screen). The conversation character is
         // the villager the player selected; fall back to the coin flip only if it cannot be resolved.
         CharacterObject partner = Campaign.Current?.ConversationManager?.OneToOneConversationCharacter;

         CommonsKnowledge knowledge = CommonsKnowledgeBuilder.Build(settlement);
         NpcProfile profile = CommonsProfileFactory.Create(settlement, partner?.IsFemale);

         ChatManager.OpenForCommoner(profile, knowledge);
         // Gabriel (08/10/2026): a villager of the place a tale's stage turns on opens on that stage's picture.
         if (Quests.AuthoredQuestBehavior.Instance?.ConversationImageForLocalsOf(settlement) is (string talePicture, string taleTitle))
            ChatManager.ActiveViewModel?.ShowSceneImage(talePicture, taleTitle);
      }

      #endregion
   }
}