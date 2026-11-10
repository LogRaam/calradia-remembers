// Code written by Gabriel Mailhot, 10/11/2026.
// When a companion riding back to the player (from an errand or a patrol) rejoins the party.
//
// Aporo (Discord, 10/11/2026): "when he tries to return to my party he stands close to me and i can't do anything about
// it only cancel quest, happened 2 times." Read in the code: a returning companion rejoined only when within 2 map units
// of the player at the moment it was checked, and an errand checked once a day, steering toward where the player stood
// at that tick; the phase had no end, so a party parked just out of reach waited for good, and the journal mirror's
// cancel moves nothing. Gabriel (10/11/2026): "go".
//
// So: arrived rejoins (checked every hour now, toward where the player is); and after GraceDays on the road home, the
// companion rejoins wherever they are, as a party that rode home would have by then. Never in the middle of a battle.

namespace CalradiaRemembers.Logic
{
   /// <summary>How a companion riding home stands this hour. One fact per line.</summary>
   public sealed class ReturnHomeFacts
   {
      /// <summary>Within the arrival distance of the player.</summary>
      public bool Arrived { get; init; }

      /// <summary>Whole days since they set out for home.</summary>
      public int DaysOnTheRoad { get; init; }

      /// <summary>Their party is in a battle right now.</summary>
      public bool InBattle { get; init; }
   }

   /// <summary>What the companion does this hour.</summary>
   public enum ReturnHomeStep
   {
      /// <summary>Keep riding toward the player.</summary>
      KeepRiding,

      /// <summary>Rejoin the player's party now.</summary>
      Rejoin,

      /// <summary>Wait: they are fighting.</summary>
      Wait
   }

   /// <summary>Decides when a companion riding home rejoins the party. Pure.</summary>
   public static class ReturnHomePolicy
   {
      /// <summary>TUNE: days on the road home after which the companion rejoins wherever they are.</summary>
      public const int GraceDays = 2;

      /// <summary>Never mid-battle; arrived, or on the road for <see cref="GraceDays" /> days, rejoins.</summary>
      public static ReturnHomeStep Decide(ReturnHomeFacts facts)
      {
         if (facts == null) return ReturnHomeStep.KeepRiding;
         if (facts.InBattle) return ReturnHomeStep.Wait;

         return facts.Arrived || facts.DaysOnTheRoad >= GraceDays ? ReturnHomeStep.Rejoin : ReturnHomeStep.KeepRiding;
      }
   }
}
