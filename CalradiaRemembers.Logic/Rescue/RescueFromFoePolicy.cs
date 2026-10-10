// Code written by Gabriel Mailhot, 10/10/2026.
// zhenja1 (Nexus, 10/10/2026): "I liberated two from Garios, and both are saying they were never prisoner." The game
// hands a beaten lord's prisoners to the player's loot: freed on the spot or taken and let go later, they leave the
// PLAYER's custody, which the rescue chain read as a pardon (a prisoner of the player's own let go), so nothing was
// remembered. Gabriel (10/10/2026): a lord the player took from an enemy who held them, and then let go, was rescued;
// and the memory names who held them ("the hands of the Western Empire" said nothing of Garios).

namespace CalradiaRemembers.Logic.Rescue
{
   /// <summary>A lord let go from the player's custody, and how they came into it.</summary>
   public sealed class CustodyReleaseFacts
   {
      /// <summary>The player chose to let them go (the game's ReleasedByChoice).</summary>
      public bool ReleasedByChoice { get; init; }

      /// <summary>The day the player took them from an enemy who held them prisoner, or null when they did not.</summary>
      public int? TakenFromFoeOnDay { get; init; }

      public int Today { get; init; }
   }

   /// <summary>Who held a rescued lord, as the rescued remember it.</summary>
   public sealed class RescueCaptorFacts
   {
      /// <summary>The lord who held them, already labelled (HeroLabel), or null.</summary>
      public string CaptorLabel { get; init; }

      /// <summary>The settlement whose dungeons held them, or null.</summary>
      public string SettlementName { get; init; }

      /// <summary>The realm or faction that held them, or null.</summary>
      public string FactionName { get; init; }
   }

   /// <summary>A rescue from an enemy's captivity through the player's custody, and how it is remembered. Pure.</summary>
   public static class RescueFromFoePolicy
   {
      /// <summary>How long after the battle a lord taken from a foe and let go still counts as rescued: a season.</summary>
      public static int WindowDays => NpcMemoryService.Core.Calendar.WorldCalendar.DaysPerSeason;

      /// <summary>Let go by the player's choice, taken from an enemy who held them, within the window.</summary>
      public static bool IsRescue(CustodyReleaseFacts f)
         => f != null && f.ReleasedByChoice && f.TakenFromFoeOnDay is int taken && f.Today - taken >= 0 && f.Today - taken <= WindowDays;

      /// <summary>
      ///   What the rescued lord was freed from: the lord who held them (and where), else the dungeons, else the realm,
      ///   else plain captivity. Read after "You freed me from ...".
      /// </summary>
      public static string CaptorPhrase(RescueCaptorFacts f)
      {
         string captor = f?.CaptorLabel?.Trim(), place = f?.SettlementName?.Trim(), faction = f?.FactionName?.Trim();
         if (!string.IsNullOrEmpty(captor) && !string.IsNullOrEmpty(place)) return $"the dungeons of {place}, where {captor} held me";
         if (!string.IsNullOrEmpty(captor)) return $"{captor}, who held me prisoner";
         if (!string.IsNullOrEmpty(place)) return $"the dungeons of {place}";
         if (!string.IsNullOrEmpty(faction)) return $"the hands of {faction}";

         return "captivity";
      }
   }
}
