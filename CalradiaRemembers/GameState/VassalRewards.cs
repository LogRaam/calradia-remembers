// Code written by Gabriel Mailhot, 16/11/2026.
// Aporo (15/11/2026): an oath sworn in the chat (join_as_vassal) gave none of the game's oath rewards. Gabriel
// (16/11/2026): "oui fais les recompenses de vassal au oui". What the game gives (LordConversationsCampaignBehavior,
// decompiled from 1.5.7, conversation_player_is_accepted_as_a_vassal_on_consequence and ReceiveVassalRewards), given here
// at the player's yes: the influence of joining at once; the ruler's goodwill at once; the gift of arms and of men in the
// game's own screens once the conversation is over (a screen over the chat would fight it), on the queue a chat close
// does not clear. The game's saved flag (_receivedVassalRewards) is the one record, so neither the game nor CR gives the
// gift twice in a campaign. VassalRewardPolicy decides which.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>The game's oath rewards for an oath sworn through CR.</summary>
   internal static class VassalRewards
   {
      private static readonly AccessTools.FieldRef<LordConversationsCampaignBehavior, bool> Received =
         AccessTools.FieldRefAccess<LordConversationsCampaignBehavior, bool>("_receivedVassalRewards");

      /// <summary>
      ///   Gives what the game's oath gives, for an oath to <paramref name="kingdom" /> the player's clan has just sworn.
      ///   Returns a line for the chat when the gift of arms and men will follow the talk, else null. Never throws.
      /// </summary>
      public static string Grant(Kingdom kingdom)
      {
         try
         {
            VassalRewardsModel model = Campaign.Current?.Models?.VassalRewardsModel;
            LordConversationsCampaignBehavior lords = Campaign.Current?.GetCampaignBehavior<LordConversationsCampaignBehavior>();
            if (kingdom == null || model == null || Hero.MainHero == null) return null;

            VassalRewardGrant grant = VassalRewardPolicy.Decide(new VassalRewardFacts {
               AlreadyRewardedThisCampaign = lords == null || Received(lords),
               RealmHasRuler = kingdom.Leader != null && kingdom.Leader != Hero.MainHero
            });

            if (grant.Influence) GainKingdomInfluenceAction.ApplyForJoiningFaction(Hero.MainHero, model.InfluenceReward);
            if (grant.RulersGoodwill) ChangeRelationAction.ApplyPlayerRelation(kingdom.Leader, model.RelationRewardWithLeader);
            if (!grant.GiftOfArmsAndMen || lords == null) return null;

            Received(lords) = true;
            Campaign asked = Campaign.Current;
            MainThreadDispatcher.EnqueueLasting(() => OpenGiftWhenFree(kingdom, asked));
            ModLog.Info($"[VASSAL] oath to {kingdom.Name}: influence {model.InfluenceReward}, goodwill {model.RelationRewardWithLeader} with {kingdom.Leader?.Name}; the gift of arms and men opens after the talk.");

            return $"{kingdom.Leader?.Name?.ToString() ?? "The ruler"}'s gift of arms and men awaits you when you take your leave.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[VASSAL] the oath's rewards failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The game's saved record that this campaign's player already received an oath's gift; null when it cannot be read (self-test).</summary>
      internal static bool? ReceivedThisCampaign()
      {
         try
         {
            LordConversationsCampaignBehavior lords = Campaign.Current?.GetCampaignBehavior<LordConversationsCampaignBehavior>();

            return lords == null ? (bool?) null : Received(lords);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>Opens the game's own reward screens once no conversation or panel holds the screen; waits a frame otherwise.</summary>
      private static void OpenGiftWhenFree(Kingdom kingdom, Campaign asked)
      {
         try
         {
            if (!ReferenceEquals(asked, Campaign.Current)) return;
            if (Chat.ChatManager.IsOpen || Chat.ExplorationBackdrop.IsOpen || UI.CrPanelHost.AnyPanelOwnsScreen)
            {
               MainThreadDispatcher.EnqueueLasting(() => OpenGiftWhenFree(kingdom, asked));

               return;
            }

            VassalRewardsModel model = Campaign.Current.Models.VassalRewardsModel;
            InventoryScreenHelper.OpenScreenAsReceiveItems(model.GetEquipmentRewardsForJoiningKingdom(kingdom), new TextObject("{=exbSCGzi}Reward Items"));
            PartyScreenHelper.OpenScreenAsReceiveTroops(model.GetTroopRewardsForJoiningKingdom(kingdom), new TextObject("{=tKW8m6bZ}Reward Troops"));
            ModLog.Info($"[VASSAL] the gift of {kingdom.Name} opened (items, troops).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[VASSAL] the gift screens failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
