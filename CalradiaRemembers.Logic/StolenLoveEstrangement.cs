// Code written by Gabriel Mailhot, 26/08/2026.
// Stolen Love pillar, Increment 2: the persisted state of one married NPC's marriage fraying because of an
// ongoing affair with the player. One record per seduced NPC (keyed by NpcId); the behavior advances it daily
// and the pure EstrangementProgressPolicy reads StartDay + Difficulty to decide the phase. Kept a plain,
// serializable record so it round-trips through the additive SyncData key like DivorceProcess does.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   One married NPC whose marriage is coming apart because of an affair with the player: who they are, who
   ///   they are married to, when the estrangement began, and how hard-won the seduction was (which paces how
   ///   long the rift takes to reach a divorce). Purely data; the timing rule lives in
   ///   <see cref="EstrangementProgressPolicy" />.
   /// </summary>
   public sealed class StolenLoveEstrangement
   {
      /// <summary>StringId of the seduced, still-married NPC.</summary>
      public string NpcId { get; set; }

      /// <summary>StringId of the spouse the NPC is (still) married to: the one who will be left, and who holds the grudge later.</summary>
      public string SpouseId { get; set; }

      /// <summary>The game day the estrangement began (the affair became the crack in the marriage).</summary>
      public int StartDay { get; set; }

      /// <summary>How narrowly the seduction cleared its bar, frozen at the start: a wide-open affair unravels the marriage faster than a barely-won one.</summary>
      public SeductionDifficulty Difficulty { get; set; }

      /// <summary>
      ///   The day the NPC DECLARED to the player that they would leave their spouse (once the rift reached
      ///   ReadyToDivorce and the player was there to hear it). Null until declared. The divorce then follows about
      ///   a month later. Nullable so a record saved before this field existed loads as "not declared", never as
      ///   "declared on day zero".
      /// </summary>
      public int? DeclaredDay { get; set; }

      /// <summary>The day the divorce actually completed. Null until then; once set, the record is a spent marker that the NPC left their spouse for the player (read by the marry/sabotage exits).</summary>
      public int? DivorcedDay { get; set; }
   }
}
