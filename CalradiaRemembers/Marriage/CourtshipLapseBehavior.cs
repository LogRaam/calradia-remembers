// Code written by Gabriel Mailhot, 04/11/2026.
// avalanche24 (Nexus, 04/11/2026): the game wed Queen Rhagaea to another lord while the player courted her, and CR kept
// the courtship running: her Heart page said "courting", romantic letters kept coming, and she denied it in chat. This
// ends the bond the day she weds someone else (CourtshipLapsePolicy: Estranged, Gabriel 04/11/2026) and gives her the
// memory, so she owns her choice instead of denying what was.

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>Ends a character's courtship of the player when they marry someone else.</summary>
   public sealed class CourtshipLapseBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents()
         => CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);

      public override void SyncData(IDataStore dataStore)
      {
      }

      /// <summary>Ends <paramref name="wed" />'s bond with the player as they wed <paramref name="spouse" />; returns whether it moved. cr.test seam.</summary>
      internal static bool Lapse(Hero wed, Hero spouse)
      {
         if (wed == null || spouse == null || wed == Hero.MainHero) return false;

         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(wed.StringId);
         if (profile?.Romantic == null) return false;

         RomanticStatus before = profile.Romantic.Status;
         RomanticStatus after = CourtshipLapsePolicy.After(before, spouse == Hero.MainHero);
         if (after == before) return false;

         var today = (int) CampaignTime.Now.ToDays;
         profile.Romantic.MoveTo(after, today);
         string spouseName = HeroLabel.Describe(new HeroLabel.HeroNaming {Name = spouse.Name?.ToString(), ClanName = spouse.Clan?.Name?.ToString(), CultureName = spouse.Culture?.Name?.ToString()});
         profile.Events.Add(new NotableEvent(today, NotableEventType.OwnLife, CourtshipLapsePolicy.Memory(spouseName)));
         store.Set(profile);
         ModLog.Info($"[ROMANCE] {wed.Name} weds {spouse.Name}: bond with the player {before} -> {after}.");

         return true;
      }

      private void OnBeforeHeroesMarried(Hero first, Hero second, bool showNotification)
      {
         try
         {
            Lapse(first, second);
            Lapse(second, first);
         }
         catch (Exception ex) { ModLog.Error($"[ROMANCE] courtship lapse on a wedding failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
