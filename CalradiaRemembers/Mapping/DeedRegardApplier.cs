// Code written by Gabriel Mailhot, 23/10/2026.
// Applies DeedRegardPolicy's table to the game: the victim (or beneficiary) and their close living kin, each through the
// one regard path (ProfileMutator.ApplyReputationDelta, cause "deed"). Gabriel (23/10/2026): "il faudrait que pour chaque
// action il y a un impact sur l'estime, qu'il soit positif ou negatif." A character the player never spoke to has no
// profile yet; one is made from the hero, as rescue gratitude and a divorce's relief do, or a husband left behind, never
// met, would feel nothing. Call it after the deed's grudge is planted, so the grudge's rigidity reads the regard before.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Charges or rewards a deed of the player in personal regard, on the victim and their close kin.</summary>
   internal static class DeedRegardApplier
   {
      /// <summary>
      ///   Applies <paramref name="deed" /> to <paramref name="victim" /> (unless dead, or the path already charged them) and
      ///   to each close living kin. <paramref name="victimDelta" /> replaces the table's victim change (a gift's size).
      ///   Never throws.
      /// </summary>
      internal static void Apply(Hero victim, PlayerDeed deed, int? victimDelta = null)
      {
         try
         {
            if (victim == null || Hero.MainHero == null || victim == Hero.MainHero) return;

            DeedRegard r = DeedRegardPolicy.For(deed);
            int onVictim = victimDelta ?? (r.VictimAlreadyApplied ? 0 : r.Victim);
            if (onVictim != 0 && !victim.IsDead) Move(victim, onVictim, deed);
            if (r.Kin == 0) return;

            foreach (Hero kin in CloseLivingKinOf(victim)) Move(kin, r.Kin, deed);
         }
         catch (Exception ex) { ModLog.Error($"[REGARD] deed {deed} not applied: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Applies <paramref name="deed" /> to every living lord of <paramref name="kingdom" /> (a realm forsaken).</summary>
      internal static void ApplyToRealm(Kingdom kingdom, PlayerDeed deed)
      {
         try
         {
            int delta = DeedRegardPolicy.For(deed).Victim;
            if (kingdom == null || delta == 0) return;

            foreach (Clan clan in kingdom.Clans)
            {
               if (clan == null || clan == Clan.PlayerClan || clan.IsEliminated) continue;
               foreach (Hero lord in clan.Heroes)
                  if (lord is {IsChild: false, IsLord: true} && lord != Hero.MainHero && HeroPresence.IsInTheWorld(lord)) Move(lord, delta, deed);
            }
         }
         catch (Exception ex) { ModLog.Error($"[REGARD] deed {deed} on {kingdom?.Name} not applied: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Applies one change to one character, making their profile when they have none.</summary>
      internal static void Move(Hero hero, int delta, PlayerDeed deed)
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null || hero == null || delta == 0) return;

         NpcProfile profile = store.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);
         int before = profile.ReputationWithPlayer;
         ProfileMutator.ApplyReputationDelta(profile, delta, RegardCause.Deed);
         store.Set(profile);
         ModLog.Info($"[REGARD] {hero.Name}: {deed} {delta:+#;-#;0} (regard {before} -> {profile.ReputationWithPlayer}).");
      }

      /// <summary>Spouse, parents, grown children and siblings, in the world, never the player.</summary>
      internal static IEnumerable<Hero> CloseLivingKinOf(Hero victim)
      {
         var seen = new HashSet<Hero>();
         foreach (Hero h in Candidates(victim))
            if (h is {IsChild: false} && h != Hero.MainHero && h != victim && HeroPresence.IsInTheWorld(h) && seen.Add(h)) yield return h;
      }

      private static IEnumerable<Hero> Candidates(Hero victim)
      {
         yield return victim.Spouse;
         yield return victim.Father;
         yield return victim.Mother;
         if (victim.Children != null)
            foreach (Hero c in victim.Children) yield return c;
         if (victim.Siblings != null)
            foreach (Hero s in victim.Siblings) yield return s;
      }
   }
}
