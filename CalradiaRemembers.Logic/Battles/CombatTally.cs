// Code written by Gabriel Mailhot, 23/10/2026.
// What ONE hero did and suffered in a fight, and the words their own memory keeps of it. chmidt (Nexus, 23/10/2026):
// "They can't tell you what battles they fought in, especially companions that can't remember the last battle you
// fought with them, how many kills they had or if they were downed." Until then the mission journal kept only what
// the PLAYER did or suffered; a companion's memory said they fought at the player's side and nothing of their own part.
// Gabriel (23/10/2026): one capture for every combat (a battle, a tournament, a duel), every hero present, and separate
// readers that give it its meaning. This is the capture's pure half: the tally and the companion's own-part clause.
//
// TRUTH: the mission's verdict on a hero is not final (Bannerlord routinely spares a hero it reported Killed), so a foe
// is "bested" unless the campaign afterwards says dead, and a hero downed in the mission was "struck down", never
// killed: whoever reads this memory is alive to read it.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Battles
{
   /// <summary>One foe a hero put out of the fight, with the campaign's verdict on them afterwards.</summary>
   public sealed class TalliedFoe
   {
      /// <summary>The foe's name, already labelled for a memory line.</summary>
      public string Name { get; init; }

      /// <summary>The campaign says the foe died of it.</summary>
      public bool Died { get; init; }
   }

   /// <summary>What one hero did and suffered in one fight.</summary>
   public sealed class CombatTally
   {
      /// <summary>Rank-and-file this hero put out of the fight with their own hand.</summary>
      public int TroopsFelled { get; init; }

      /// <summary>The named foes this hero put out of the fight, in order, each once.</summary>
      public IReadOnlyList<TalliedFoe> HeroesDowned { get; init; } = Array.Empty<TalliedFoe>();

      /// <summary>This hero was put out of the fight.</summary>
      public bool WasDowned { get; init; }

      /// <summary>The named foe who put this hero down, or empty when a common soldier did, or none did.</summary>
      public string DownedBy { get; init; }
   }

   /// <summary>The words a hero's own memory keeps of their part in a fight.</summary>
   public static class CombatTallyText
   {
      /// <summary>At most this many named foes are told by name; the rest are counted.</summary>
      public const int NamedFoesTold = 2;

      private static readonly string[] Numbers = {"no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"};

      /// <summary>
      ///   The hero's own part, as a sentence to append to their memory of the fight ("My own part: I cut down seven of
      ///   their men and bested Lucon of the dey Meroc, before Ira's blade put me down."), or empty when they did nothing
      ///   of note and were not downed. <paramref name="arena" /> words it for the lists, where no one is cut down.
      /// </summary>
      public static string OwnPart(CombatTally tally, bool arena = false)
      {
         if (tally == null) return string.Empty;

         var deeds = new List<string>();
         if (tally.TroopsFelled > 0)
            deeds.Add(arena
               ? $"put {Count(tally.TroopsFelled)} {(tally.TroopsFelled == 1 ? "opponent" : "opponents")} out of the lists"
               : $"cut down {Count(tally.TroopsFelled)} of their men");

         List<TalliedFoe> named = (tally.HeroesDowned ?? Array.Empty<TalliedFoe>()).Where(f => !string.IsNullOrWhiteSpace(f?.Name)).ToList();
         List<TalliedFoe> told = named.Take(NamedFoesTold).ToList();
         List<string> bested = told.Where(f => arena || !f.Died).Select(f => f.Name.Trim()).ToList();
         List<string> slain = told.Where(f => !arena && f.Died).Select(f => f.Name.Trim()).ToList();
         if (bested.Count > 0) deeds.Add($"bested {string.Join(" and ", bested)}");
         if (slain.Count > 0) deeds.Add($"slew {string.Join(" and ", slain)}");
         int more = named.Count - told.Count;
         if (more > 0) deeds.Add($"{Count(more)} more of name besides");

         string fall = !tally.WasDowned
            ? string.Empty
            : string.IsNullOrWhiteSpace(tally.DownedBy)
               ? arena ? "I was put out of the lists" : "I was struck down"
               : arena ? $"{tally.DownedBy.Trim()} put me out of the lists" : $"{tally.DownedBy.Trim()} struck me down";

         if (deeds.Count == 0)
            return fall.Length == 0 ? string.Empty : $"My own part: {fall}.";

         string done = "I " + (deeds.Count == 1 ? deeds[0] : string.Join(", ", deeds.Take(deeds.Count - 1)) + " and " + deeds[deeds.Count - 1]);

         return fall.Length == 0 ? $"My own part: {done}." : $"My own part: {done}, before {fall}.";
      }

      private static string Count(int n) => n < Numbers.Length ? Numbers[n] : n.ToString();
   }
}
