// Code written by Gabriel Mailhot, 01/11/2026.
// DepaHN (Nexus, 27/10/2026): a friend who holds back in a war should count for something the player can see. Gabriel, 01/11/2026:
// one letter, from the closest friend, so that a war does not bring five. The draw is RaidRestraintBehavior's; this side reads it
// and writes, so the stance does not have to know the letters.

#region

using System;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Raids;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Stance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>In each war against the player, the closest lord sparing their villages tells them so, once.</summary>
   public sealed class VillagesSparedLetterBehavior : CampaignBehaviorBase
   {
      /// <summary>cr.test all seam: the choice of writer runs, and no letter leaves.</summary>
      internal static bool DebugSuppressLetters;

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
      }

      private static void OnDailyTick()
      {
         try
         {
            if (Clan.PlayerClan?.Villages?.Count > 0) TellThePlayer();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RAID] the letter pass failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Lets each war's writer write. Internal for cr.test all.</summary>
      internal static void TellThePlayer()
      {
         RaidRestraintBehavior restraint = RaidRestraintBehavior.Instance;
         if (restraint == null || !ModSettings.RaidRestraint) return;

         foreach (IGrouping<string, RaidRestraint> war in restraint.Ledger.Draws.GroupBy(d => d.FactionId).ToList())
         {
            RaidRestraint writer = RaidRestraintPolicy.WhoWrites(war, RegardOfAWriter);
            if (writer == null) continue;

            writer.PlayerWasTold = true;
            Hero lord = RaidRestraintBehavior.FindHero(writer.HeroId);
            ModLog.Info($"[RAID] {lord.Name} writes that the player's villages are safe from him.");
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            if (DebugSuppressLetters || behavior?.LlmServices == null || behavior.Store == null) continue;

            string bond = RaidRestraintBehavior.IsKinOrInLaw(lord) ? "You are their kin" : "You are their friend";
            string context = $"{bond}, and your realm, {lord.MapFaction.Name}, is now at war with them.";
            LetterGenerationService.QueueInitialLetter(behavior.Store.Get(lord.StringId), lord, LetterReason.VillagesSpared, context, behavior.LlmServices, behavior.Store, (int) CampaignTime.Now.ToDays);
         }
      }

      // Null for a lord who cannot write today: a stranger, a prisoner, one the player silenced.
      private static int? RegardOfAWriter(RaidRestraint draw)
      {
         Hero lord = RaidRestraintBehavior.FindHero(draw.HeroId);
         NpcProfile profile = lord == null ? null : NpcMemoryCampaignBehavior.Instance?.Store?.Get(lord.StringId);
         if (profile == null || !lord.HasMet || !ActionGate.Allows(lord, ActionKind.Letter) || LetterBox.IsSilenced(profile, LetterReason.VillagesSpared)) return null;

         return profile.ReputationWithPlayer;
      }
   }
}
