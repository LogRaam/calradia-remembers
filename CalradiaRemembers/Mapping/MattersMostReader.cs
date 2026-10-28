// Code written by Gabriel Mailhot, 27/10/2026.
// What the world says is true of a character, read for MattersMostPolicy (Gabriel, 27/10/2026: "qu'est-ce qui compte le
// plus pour un NPC. Ses enfants? Sa richesse? Son amoureux?"): their living children, spouse and kin, a lover, their house
// and lands, and a live grudge the player may know of. A grudge held against the player and hidden from them never
// grounds a score to settle: the encyclopedia would otherwise tell the player of a grudge they were never meant to see.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Character;
using CalradiaRemembers.Logic.Grudges;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads what is true of a character, keeps what matters most to them up to date, and words it for their page.</summary>
   internal static class MattersMostReader
   {
      /// <summary>What is true of <paramref name="hero" /> today.</summary>
      internal static MattersMostFacts FactsOf(Hero hero, NpcProfile profile)
      {
         Hero player = Hero.MainHero;
         RomanticProfile r = profile?.Romantic;
         bool spouseAlive = hero.Spouse != null && !hero.Spouse.IsDead;
         bool playerIsLover = r != null && r.Status is RomanticStatus.Intimate or RomanticStatus.SecretLover or RomanticStatus.Committed;
         Hero lover = !string.IsNullOrEmpty(r?.TakenLoverId) ? Hero.FindFirst(x => x.StringId == r.TakenLoverId) : null;
         bool isNoble = hero.IsLord && hero.Clan != null && hero.Clan != Clan.PlayerClan || hero.IsLord && hero.Clan == Clan.PlayerClan && hero.CompanionOf == null;
         var today = (int) CampaignTime.Now.ToDays;
         IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, today);

         return new MattersMostFacts {
            HeroId = hero.StringId,
            Traits = RomanticProfileBuilder.TraitsOf(hero),
            HasChildren = hero.Children?.Any(c => c != null && !c.IsDead) == true,
            HasSpouse = spouseAlive && hero.Spouse != player,
            LovesThePlayer = spouseAlive && hero.Spouse == player || playerIsLover,
            HasLover = lover != null && !lover.IsDead && lover != player,
            HasKin = hero.Father?.IsDead == false || hero.Mother?.IsDead == false || hero.Siblings?.Any(s => s != null && !s.IsDead) == true,
            IsNoble = isNoble,
            LeadsTheirHouse = hero.Clan?.Leader == hero || hero.Clan?.Kingdom?.Leader == hero,
            HoldsLands = hero.GovernorOf != null || hero.Clan?.Leader == hero && hero.Clan.Settlements?.Any(s => s.IsTown || s.IsCastle) == true,
            IsWanderer = !isNoble && (hero.IsWanderer || hero.CompanionOf != null),
            HasScoreToSettle = grudges?.Any(g => g != null && (g.TargetId != player?.StringId || g.KnownToPlayer)) == true
         };
      }

      /// <summary>Chooses or keeps what matters most to them; logs a change.</summary>
      internal static void Refresh(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null || profile == null || hero == Hero.MainHero) return;
            MattersMost before = profile.MattersMost;
            MattersMost now = MattersMostPolicy.Resolve(before, FactsOf(hero, profile));
            if (now == before) return;

            profile.MattersMost = now;
            if (before != MattersMost.None) ModLog.Info($"[MATTERS] {hero.Name}: what matters most moved from {before} to {now} (its ground fell).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MATTERS] refresh failed for {hero?.Name}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The line on their page, or null.</summary>
      internal static string PageLine(Hero hero, NpcProfile profile)
      {
         if (hero == null || profile == null) return null;

         return MattersMostPolicy.PageLine(new MattersMostPageFacts {
            MattersMost = profile.MattersMost, Known = profile.MattersMostKnown, IsFemale = hero.IsFemale,
            LoverKnownToPlayer = profile.Romantic?.LoverKnownToPlayer == true
         });
      }
   }
}
