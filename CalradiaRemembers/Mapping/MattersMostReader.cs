// Code written by Gabriel Mailhot, 27/10/2026.
// What the world says is true of a character, read for MattersMostPolicy (Gabriel, 27/10/2026: "qu'est-ce qui compte le
// plus pour un NPC. Ses enfants? Sa richesse? Son amoureux?"): their living children, spouse and kin, a lover, their house
// and lands, and a live grudge. A grudge held against the player and hidden from them moves the character like any other,
// but the encyclopedia says nothing of a score to settle that rests on it alone (VonLemberg, source audit, 15/11/2026;
// Gabriel: "oui je veux cette separation"): until then it counted for neither, and the prompt said both "you nurse a
// secret grudge" and "what matters most to you is your children".

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
         // A spouse estranged from the player, or wed for politics alone, does not live for them: the prompt would say
         // "the player, whom you love" while their stance and grudges say otherwise.
         bool bondBroken = r != null && r.Status is RomanticStatus.Estranged or RomanticStatus.Broken;
         Hero lover = !string.IsNullOrEmpty(r?.TakenLoverId) ? Hero.FindFirst(x => x.StringId == r.TakenLoverId) : null;
         bool isNoble = hero.IsLord && hero.Clan != null && hero.Clan != Clan.PlayerClan || hero.IsLord && hero.Clan == Clan.PlayerClan && hero.CompanionOf == null;

         return new MattersMostFacts {
            HeroId = hero.StringId,
            Traits = RomanticProfileBuilder.TraitsOf(hero),
            HasChildren = hero.Children?.Any(c => c != null && !c.IsDead) == true,
            HasSpouse = spouseAlive && hero.Spouse != player,
            LovesThePlayer = !bondBroken && (spouseAlive && hero.Spouse == player || playerIsLover),
            HasLover = lover != null && !lover.IsDead && lover != player,
            HasKin = hero.Father?.IsDead == false || hero.Mother?.IsDead == false || hero.Siblings?.Any(s => s != null && !s.IsDead) == true,
            IsNoble = isNoble,
            LeadsTheirHouse = hero.Clan?.Leader == hero || hero.Clan?.Kingdom?.Leader == hero,
            HoldsLands = hero.GovernorOf != null || hero.Clan?.Leader == hero && hero.Clan.Settlements?.Any(s => s.IsTown || s.IsCastle) == true,
            IsWanderer = !isNoble && (hero.IsWanderer || hero.CompanionOf != null),
            HasScoreToSettle = ScoreOf(hero).Held
         };
      }

      /// <summary>Their live grudges, read as a score to settle (MattersMostPolicy.ScoreOf).</summary>
      private static ScoreToSettle ScoreOf(Hero hero)
      {
         string playerId = Hero.MainHero?.StringId;
         IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.LiveGrudgesFor(hero, (int) CampaignTime.Now.ToDays);

         return MattersMostPolicy.ScoreOf(grudges?.Where(g => g != null)
                                                 .Select(g => new GrudgeSighting {AgainstThePlayer = g.TargetId == playerId, KnownToPlayer = g.KnownToPlayer}));
      }

      /// <summary>
      ///   The "mattersMost" written in character_overrides.json for a hero, or null. Plugged in by
      ///   <c>Lore.CharacterLoreBehavior</c>, as EncounterContextBuilder.PastBondSource is: Lore already uses Mapping.
      /// </summary>
      public static Func<Hero, MattersMost?> AuthoredSource { get; set; }

      /// <summary>Chooses or keeps what matters most to them; logs a change.</summary>
      internal static void Refresh(Hero hero, NpcProfile profile)
      {
         try
         {
            if (hero == null || profile == null || hero == Hero.MainHero) return;
            MattersMost before = profile.MattersMost;
            // Bhelogan (21/11/2026): a value written on purpose (character_overrides.json, or pinned by another mod) is held.
            var lockFacts = new MattersMostLockFacts {Current = before, Authored = AuthoredSource?.Invoke(hero), Pinned = profile.MattersMostPinned};
            MattersMost? held = MattersMostLockPolicy.Held(lockFacts);
            MattersMost now = held ?? MattersMostPolicy.Resolve(before, FactsOf(hero, profile));
            if (now == before) return;

            profile.MattersMost = now;
            profile.MattersMostKnown = MattersMostPolicy.StillKnown(before, now, profile.MattersMostKnown);
            if (held != null) ModLog.Info($"[MATTERS] {hero.Name}: what matters most set to {now} ({MattersMostLockPolicy.Why(lockFacts)}).");
            else if (before != MattersMost.None) ModLog.Info($"[MATTERS] {hero.Name}: what matters most moved from {before} to {now} ({before} no longer grounded).");
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
            LoverKnownToPlayer = profile.Romantic?.LoverKnownToPlayer == true,
            ScoreHiddenFromThePlayer = profile.MattersMost == MattersMost.Vengeance && ScoreOf(hero).HiddenFromThePlayer
         });
      }
   }
}
