// Code written by Gabriel Mailhot, 23/10/2026.
// Who watched a tournament the player fought in, and what they keep of it. chmidt (Nexus, 23/10/2026): "the lady of
// Hargard couldn't even remember that I won a tournament at her city, with her completely forgetting that there was even
// one to begin with." Only the heroes the player faced in the bracket (and a champion who beat the player) kept a
// memory, and the player's victory was recorded as news no one could hear (no place, no realm, magnitude 40 under the
// awareness threshold of 50). Now the lords and ladies who were in the town, and the notables the player already knows
// there, keep a spectator's memory; the house that holds the town keeps it even when the player never spoke to them.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Tournaments
{
   /// <summary>One hero in the town when the tournament ended.</summary>
   public sealed class TournamentOnlooker
   {
      /// <summary>The hero's id.</summary>
      public string Id { get; init; }

      /// <summary>A lord or lady (not a notable).</summary>
      public bool IsLord { get; init; }

      /// <summary>Of the house that holds the town.</summary>
      public bool OfTheHoldingHouse { get; init; }

      /// <summary>The player already knows them (they have a memory profile).</summary>
      public bool KnownToThePlayer { get; init; }

      /// <summary>They fought the player in the bracket, or took the crown: they already keep a fighter's memory.</summary>
      public bool FoughtInIt { get; init; }
   }

   /// <summary>Pure: who watched, and the spectator's memory line.</summary>
   public static class TournamentAudiencePolicy
   {
      /// <summary>At most this many onlookers keep the memory, the holding house first, so a crowded town does not flood the saves.</summary>
      public const int MaxOnlookers = 8;

      /// <summary>
      ///   The onlookers who keep a spectator's memory, the holding house first: every lord or lady of the holding house in
      ///   town, any other lord or lady the player knows, and the notables the player knows; never someone who fought in it.
      /// </summary>
      public static IReadOnlyList<string> Who(IEnumerable<TournamentOnlooker> inTown)
         => (inTown ?? Enumerable.Empty<TournamentOnlooker>())
            .Where(o => o != null && !string.IsNullOrEmpty(o.Id) && !o.FoughtInIt && (o.IsLord && o.OfTheHoldingHouse || o.KnownToThePlayer))
            .OrderByDescending(o => o.OfTheHoldingHouse)
            .ThenByDescending(o => o.IsLord)
            .Select(o => o.Id)
            .Distinct()
            .Take(MaxOnlookers)
            .ToList();

      /// <summary>"I watched the tourney at Hargard, where the player took the crown." or "..., where the player fell in the semi-final."</summary>
      public static string SpectatorMemory(TournamentResult result)
      {
         string atTown = string.IsNullOrEmpty(result?.TownName) ? "the tourney" : $"the tourney at {result.TownName}";
         if (result == null) return $"I watched {atTown}, where the player competed.";
         if (result.PlayerWonTournament) return $"I watched {atTown}, where the player took the crown.";

         return result.PlayerEliminatedRound > 0
            ? $"I watched {atTown}, where the player fell in {RoundName(result.PlayerEliminatedRound)}."
            : $"I watched {atTown}, where the player competed.";
      }

      private static string RoundName(int round) => round switch
      {
         4 => "the final",
         3 => "the semi-final",
         2 => "the second round",
         1 => "the first round",
         _ => "a bout"
      };
   }
}
