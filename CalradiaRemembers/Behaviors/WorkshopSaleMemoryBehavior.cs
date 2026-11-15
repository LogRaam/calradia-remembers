// Code written by Gabriel Mailhot, 14/11/2026.
// Windmolen (Nexus, 14/11/2026): the man who sold him a business "contested my ownership, wanted me to produce a sealed
// document or a witness". The game buys a workshop for the player in one click (ChangeOwnerOfWorkshopAction.ApplyByPlayerBuying,
// the only path that hands the player a workshop) and fires WorkshopOwnerChangedEvent with the former owner; nothing told
// that former owner. This gives him the memory of the sale (Logic.Ventures.WorkshopSaleMemoryPolicy), on his profile.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Ventures;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

#endregion

namespace CalradiaRemembers.Behaviors
{
   /// <summary>Gives a character who sold the player a workshop the memory of the sale.</summary>
   public sealed class WorkshopSaleMemoryBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents()
         => CampaignEvents.WorkshopOwnerChangedEvent.AddNonSerializedListener(this, (workshop, oldOwner) => Remember(workshop, oldOwner, workshop?.Owner));

      // The memory lives on the seller's profile, which the store already saves.
      public override void SyncData(IDataStore dataStore) { }

      /// <summary>
      ///   Records the sale on <paramref name="oldOwner" /> when <paramref name="newOwner" /> is the player buying from him.
      ///   Returns the line remembered, or null. Public for cr.test all.
      /// </summary>
      public static string Remember(Workshop workshop, Hero oldOwner, Hero newOwner)
      {
         try
         {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null || workshop == null) return null;

            string line = WorkshopSaleMemoryPolicy.SellerMemory(new WorkshopSaleFacts {
               NewOwnerIsPlayer = newOwner != null && newOwner == Hero.MainHero,
               OldOwnerIsPlayer = oldOwner != null && oldOwner == Hero.MainHero,
               HasOldOwner = oldOwner != null && !oldOwner.IsDead,
               Trade = workshop.WorkshopType?.Name?.ToString(),
               Town = workshop.Settlement?.Name?.ToString(),
               PlayerName = Hero.MainHero?.Name?.ToString()
            });
            if (line == null) return null;

            NpcProfile profile = store.Get(oldOwner.StringId) ?? HeroProfileMapper.FromHero(oldOwner);
            profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Agreement, line));
            store.Set(profile);
            ModLog.Info($"[WORKSHOP] {oldOwner.Name} remembers selling: {line}");

            return line;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WORKSHOP] remembering a workshop sale failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }
   }
}
