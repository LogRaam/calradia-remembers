// Code written by Gabriel Mailhot, 01/11/2026.
// DepaHN (Nexus, 27/10/2026): "a close friend was the first to burn my villages", and of kin, "They would only act against me
// when there is open hostility or a clear reason". Gabriel, 01/11/2026: "un ami PEUT ne pas toucher le village. Ca devrait etre
// un RND. Plus l'amitie est grande et moins il y a de chances qu'il attaque les villages." Each friend or relative at war with
// the player draws once per war; what he drew then stands. The figures are mine, to tune in play.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.Raids
{
   /// <summary>What a lord's draw reads when a war sets him against the player.</summary>
   public sealed class RaidRestraintFacts
   {
      /// <summary>The lord's regard for the player, the mod's own ledger.</summary>
      public int Regard { get; init; }

      /// <summary>True when the lord is the player's blood or in-law.</summary>
      public bool IsKin { get; init; }

      /// <summary>The heaviest unresolved grudge the lord holds against the player.</summary>
      public int HeaviestGrudge { get; init; }
   }

   /// <summary>Whether a lord at war with the player may spare the player's villages, and how likely he is to.</summary>
   public static class RaidRestraintPolicy
   {
      /// <summary>The regard from which a lord counts as a friend, the bar the calls for help use.</summary>
      public const int FriendFloor = Regard.WarmTier;

      /// <summary>The regard a relative must keep for kinship to weigh: the bar the calls for help use for kin.</summary>
      public const int KinFloor = Regard.CoolTier;

      /// <summary>The chance, in percent, of a friend just over the floor.</summary>
      public const int ChanceOfANewFriend = 25;

      /// <summary>The chance, in percent, of the closest friend: below 100, for a friend only MAY hold back.</summary>
      public const int ChanceOfTheClosestFriend = 95;

      /// <summary>The chance, in percent, kinship gives on its own.</summary>
      public const int ChanceOfKin = 60;

      /// <summary>The chance, from 0 to 95 percent, that this lord spares the player's villages in this war.</summary>
      public static int ChanceToSpare(RaidRestraintFacts f)
      {
         if (f == null || f.HeaviestGrudge >= CaptorReleasePolicy.VendettaGrudgeFloor) return 0;

         // Seven points of chance for eight of regard: the closest friend's chance falls at the top of the ledger, 100.
         int friendship = f.Regard >= FriendFloor ? System.Math.Min(ChanceOfTheClosestFriend, ChanceOfANewFriend + (f.Regard - FriendFloor) * 7 / 8) : 0;
         int kinship = f.IsKin && f.Regard >= KinFloor ? ChanceOfKin : 0;

         return System.Math.Max(friendship, kinship);
      }

      /// <summary>
      ///   The one lord who tells the player, for one realm's war: the closest of those sparing them who may write. None
      ///   when the player was already told: Gabriel accepted one letter a war, not five.
      /// </summary>
      public static RaidRestraint WhoWrites(IEnumerable<RaidRestraint> drawsOfThisWar, Func<RaidRestraint, int?> regardOfAWriter)
      {
         List<RaidRestraint> draws = drawsOfThisWar?.ToList() ?? new List<RaidRestraint>();
         if (regardOfAWriter == null || draws.Any(d => d.PlayerWasTold)) return null;

         return draws.Where(d => d.IsSparing && regardOfAWriter(d).HasValue).OrderByDescending(d => regardOfAWriter(d).Value).FirstOrDefault();
      }

      /// <summary>True when a draw from 0 to 99 lands inside the chance.</summary>
      public static bool Spares(int chancePercent, int rollPercent) => rollPercent < chancePercent;

      /// <summary>True when the lord's grudge is reason enough to set his restraint aside.</summary>
      public static bool GrudgeBreaksRestraint(int heaviestGrudge) => heaviestGrudge >= CaptorReleasePolicy.VendettaGrudgeFloor;
   }
}
