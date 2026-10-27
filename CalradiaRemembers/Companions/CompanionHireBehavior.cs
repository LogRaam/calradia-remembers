// Code written by Gabriel Mailhot, 26/10/2026.
// fearente (Nexus, 26/10/2026): "a companion that I hired a week ago talked like we were of the same clan from childhood".
// Nothing kept the day a companion joined, so nothing could tell them how new they are to the player's company. This keeps
// it, on every hire the game makes (its own tavern hire and every CR path go through AddCompanionAction, which fires
// NewCompanionAdded), on the companion's profile.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Companions
{
   /// <summary>Keeps the day each companion joined the player's company.</summary>
   public sealed class CompanionHireBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents() => CampaignEvents.NewCompanionAdded.AddNonSerializedListener(this, OnHired);

      // The day lives on the companion's profile, which the store already saves.
      public override void SyncData(IDataStore dataStore) { }

      /// <summary>Records <paramref name="companion" /> as having joined the player's company today. Public for cr.testall.</summary>
      public static void OnHired(Hero companion)
      {
         try
         {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (companion == null || store == null) return;

            NpcProfile profile = store.Get(companion.StringId) ?? HeroProfileMapper.FromHero(companion);
            profile.JoinedPlayerCompanyDay = (int) CampaignTime.Now.ToDays;
            store.Set(profile);
            ModLog.Info($"[COMPANION] {companion.Name} joined your company on day {profile.JoinedPlayerCompanyDay}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COMPANION] keeping the hire day failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
