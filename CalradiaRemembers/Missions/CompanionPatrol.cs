// Code written by Gabriel Mailhot, 17/10/2026.

namespace CalradiaRemembers.Missions
{
   /// <summary>Where a companion's patrol stands.</summary>
   public enum CompanionPatrolState
   {
      /// <summary>Patrolling around the ordered settlement.</summary>
      Patrolling,

      /// <summary>Ordered home: riding back to the player to hand over every man.</summary>
      Returning
   }

   /// <summary>A companion patrolling a settlement for the player with men taken from the player's party (saved as JSON).</summary>
   public sealed class CompanionPatrol
   {
      /// <summary>Battles the patrol lost.</summary>
      public int BattlesLost { get; set; }

      /// <summary>Battles the patrol won.</summary>
      public int BattlesWon { get; set; }

      /// <summary>StringId of the companion who leads it.</summary>
      public string CompanionId { get; set; }

      /// <summary>Cached name, so a notice still reads right if the hero is gone.</summary>
      public string CompanionName { get; set; }

      /// <summary>Men the beaten foes brought to the field, summed over the patrol's victories.</summary>
      public int FoesBeaten { get; set; }

      /// <summary>Stable unique id.</summary>
      public string Id { get; set; }

      /// <summary>In-game day of the last letter of news (the departure day until the first one).</summary>
      public int LastReportDay { get; set; }

      /// <summary>Healthy men who rode out.</summary>
      public int MenAtDeparture { get; set; }

      /// <summary>StringId of the patrol's clan party.</summary>
      public string PartyId { get; set; }

      /// <summary>In-game day a letter-borne order to come home takes effect; 0 when none is pending.</summary>
      public int RecallOrderDay { get; set; }

      /// <summary>
      ///   In-game day the patrol set out for home (0 = not yet stamped; stamped lazily on the first returning hour).
      ///   After ReturnHomePolicy.GraceDays they rejoin wherever they are (Aporo, Discord 10/11/2026). Additive JSON field.
      /// </summary>
      public int ReturningSinceDay { get; set; }

      /// <summary>StringId of the settlement patrolled.</summary>
      public string SettlementId { get; set; }

      /// <summary>Cached settlement name.</summary>
      public string SettlementName { get; set; }

      /// <summary>In-game day the patrol rode out.</summary>
      public int StartDay { get; set; }

      public CompanionPatrolState State { get; set; }
   }
}
