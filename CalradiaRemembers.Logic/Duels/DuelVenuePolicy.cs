// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A (pure): where a duel is staged, or whether it can be staged at all right now. A
// settlement stages it at the town gates (the ambush settlement-scene path already used elsewhere), the open
// land stages it as a wilderness field, and at sea there is simply no field to stand on, so the duel must wait
// until the player comes ashore. Sea is checked FIRST and wins over every other fact: a player at sea is never
// truly in a settlement, but the precedence is guarded anyway so a future bug in how "at sea" and "in a
// settlement" are computed can never smuggle a duel onto open water.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>Where a duel is staged, or that it cannot be staged now.</summary>
   public enum DuelVenue
   {
      /// <summary>Staged at the town gates: the ambush settlement-scene path.</summary>
      TownGates,

      /// <summary>Staged as a wilderness field on the open land.</summary>
      Wilderness,

      /// <summary>No venue exists right now (at sea). The duel must be deferred until the player is ashore.</summary>
      NotNow
   }

   /// <summary>Chooses the venue for a duel from where the player stands.</summary>
   public static class DuelVenuePolicy
   {
      /// <summary>
      ///   In a settlement the duel is staged at the town gates (the ambush settlement-scene path); on the
      ///   open land it is a wilderness field; AT SEA there is no duel, so it must be deferred until the
      ///   player is ashore. Sea is checked FIRST: a player at sea is never in a settlement, but the
      ///   precedence is guarded anyway.
      /// </summary>
      public static DuelVenue Choose(bool playerAtSea, bool playerInSettlement)
      {
         if (playerAtSea) return DuelVenue.NotNow;

         return playerInSettlement ? DuelVenue.TownGates : DuelVenue.Wilderness;
      }
   }
}
