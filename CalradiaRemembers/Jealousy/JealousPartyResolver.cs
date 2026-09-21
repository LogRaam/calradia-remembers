// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Standing;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Jealousy
{
   /// <summary>The chosen jealous party plus the context needed to flavour their reaction.</summary>
   public sealed class JealousResolution
   {
      public Hero Courted; // the NPC the player acted romantically toward
      public Hero Other; // the rival focus for competition jealousy (else null)
      public Hero Party; // who grows jealous
      public JealousStake Stake;
   }

   /// <summary>
   ///   Picks at most ONE believable jealous party for a romantic act, in priority order:
   ///   the courted NPC's spouse, then the player's own partner, then a rival suitor, then a
   ///   spurned admirer. Every candidate must have a real, inferable stake — no random malus.
   /// </summary>
   public static class JealousPartyResolver
   {
      // A rival suitor must be at least a genuine associate of the courted NPC (NPC<->NPC
      // relation), since the game does not model NPC desire — we infer it from "close enough
      // to plausibly have wanted them, and able to marry them".
      private const int RivalRelationThreshold = VanillaRelation.TrustedTier;

      // A spurned admirer must have carried real attraction to the player (0..100 scale).
      private const int SpurnedAttractionThreshold = 55;

      /// <summary>
      ///   Resolves the jealous party for the player's romantic act toward <paramref name="courted" />,
      ///   or null when no plausible party exists. <paramref name="lockedRivalId" /> pins a rival
      ///   chosen on an earlier act so the grudge stays consistent across the arc.
      /// </summary>
      public static JealousResolution Resolve(
         Hero courted,
         Func<string, NpcProfile> getProfile,
         string lockedRivalId)
      {
         var player = Hero.MainHero;

         if (courted == null || player == null || courted == player) return null;

         // 1) The courted NPC's spouse — strongest, most telegraphed stake.
         Hero spouseOfCourted = courted.Spouse;

         if (spouseOfCourted is {IsAlive: true} && spouseOfCourted != player)
            return new JealousResolution {
               Party = spouseOfCourted, Stake = JealousStake.SpouseOfCourted, Courted = courted
            };

         // 2) The player's own partner (native spouse, else a committed consort).
         Hero partner = ResolvePlayerPartner(courted, getProfile);

         if (partner != null)
            return new JealousResolution {
               Party = partner, Stake = JealousStake.PlayerPartner, Courted = courted, Other = courted
            };

         // 2b) The player's own SECRET LOVER: a real intimate bond, wounded quietly (its own stake, so the
         // reaction stays discreet: a private letter, never a public scene, no grudge).
         Hero secretLover = ResolvePlayerSecretLover(courted, getProfile);

         if (secretLover != null)
            return new JealousResolution {
               Party = secretLover, Stake = JealousStake.SecretLover, Courted = courted, Other = courted
            };

         // 3) A rival suitor for the courted NPC — kept consistent via the locked id when present.
         Hero rival = ResolveRivalSuitor(courted, lockedRivalId);

         if (rival != null)
            return new JealousResolution {
               Party = rival, Stake = JealousStake.RivalSuitor, Courted = courted
            };

         // 4) A spurned admirer of the player.
         Hero admirer = ResolveSpurnedAdmirer(courted, getProfile);

         if (admirer != null)
            return new JealousResolution {
               Party = admirer, Stake = JealousStake.SpurnedAdmirer, Courted = courted, Other = courted
            };

         return null;
      }

      #region private

      private static Hero ResolvePlayerPartner(Hero courted, Func<string, NpcProfile> getProfile)
      {
         var player = Hero.MainHero;
         Hero spouse = player.Spouse;

         if (spouse is {IsAlive: true} && spouse != courted)
            return spouse;

         // A committed consort counts as a primary partner for jealousy purposes.
         if (getProfile == null) return null;
         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == courted || h == player) continue;
            NpcProfile p = getProfile(h.StringId);

            if (p?.Romantic == null) continue;

            if (p.Romantic.IsConsort && p.Romantic.Status == RomanticStatus.Committed)
               return h;
         }

         return null;
      }

      /// <summary>
      ///   The player's own secret lover (the discreet companion bond), when one exists and is not the courted
      ///   NPC themselves. A real intimate bond: it reacts, but with its own quiet stake.
      /// </summary>
      private static Hero ResolvePlayerSecretLover(Hero courted, Func<string, NpcProfile> getProfile)
      {
         if (getProfile == null) return null;

         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == courted || h == Hero.MainHero || !h.IsAlive) continue;
            NpcProfile p = getProfile(h.StringId);

            if (p?.Romantic == null) continue;

            if (p.Romantic.IsSecretLover)
               return h;
         }

         return null;
      }

      private static Hero ResolveRivalSuitor(Hero courted, string lockedRivalId)
      {
         // A rival suitor only makes sense for someone still unwed — no one schemes to marry a
         // person already married (to the player or to anyone else).
         if (courted.Spouse != null) return null;

         // Honour a previously locked rival if they are still a valid match for the courted NPC.
         if (!string.IsNullOrEmpty(lockedRivalId))
         {
            Hero locked = Hero.FindFirst(h => h.StringId == lockedRivalId);

            if (locked is {IsAlive: true} && MarriageProspectResolver.IsMarriageable(courted, locked) && !IsCloseKin(courted, locked))
               return locked;
         }

         Hero best = null;
         int bestRelation = RivalRelationThreshold - 1;
         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == courted || h == Hero.MainHero) continue;
            if (!MarriageProspectResolver.IsMarriageable(courted, h)) continue; // unwed, adult, opposite sex to X, age gap
            // M-J3: a rival SUITOR schemes to marry the courted NPC, so their own close kin (parent, child,
            // sibling) can never be one; without this an opposite-sex parent or sibling could be picked and write
            // an incestuous "I intend to marry {courted}".
            if (IsCloseKin(courted, h)) continue;
            int rel = h.GetRelation(courted);

            if (rel <= bestRelation) continue;
            bestRelation = rel;
            best = h;
         }

         return best;
      }

      /// <summary>True when <paramref name="a" /> and <paramref name="b" /> are close blood kin (parent/child or
      /// siblings), who can never court or marry each other (romance audit M-J3).</summary>
      private static bool IsCloseKin(Hero a, Hero b)
      {
         if (a == null || b == null) return false;
         if (a.Father == b || a.Mother == b || b.Father == a || b.Mother == a) return true;

         if (a.Siblings != null)
            foreach (Hero sibling in a.Siblings)
               if (sibling == b)
                  return true;

         return false;
      }

      private static Hero ResolveSpurnedAdmirer(Hero courted, Func<string, NpcProfile> getProfile)
      {
         if (getProfile == null) return null;
         var player = Hero.MainHero;

         Hero best = null;
         int bestAttraction = SpurnedAttractionThreshold - 1;
         foreach (Hero h in Hero.AllAliveHeroes)
         {
            if (h == courted || h == player) continue;
            if (h.Spouse == player) continue; // already the player's — not spurned
            NpcProfile p = getProfile(h.StringId);

            if (p?.Romantic == null) continue;
            if (p.Romantic.AttractionToPlayer <= bestAttraction) continue;
            bestAttraction = p.Romantic.AttractionToPlayer;
            best = h;
         }

         return best;
      }

      #endregion
   }
}