// Code written by Gabriel Mailhot, 23/10/2026.
// The encyclopedia's Misdeeds section, on the player's own page. Gabriel (23/10/2026), after the prisoners the player took
// by force were first told under Heart: "Heart doit etre une section sur l'amour et le desir. L'evenement 'take by force'
// devrait etre une autre section avec des actions reprehensibles." His earlier ruling still holds: "les actes que le joueur
// a fait subir peuvent apparaitre", "les scenes forcees subies par le joueur ne sont pas affichees sur la page du joueur".
// So the section tells what the player did of their own will, from the Trauma ledger (the source of each victim's Trauma
// section, so the two pages never disagree): never what a captor did, nor what a captor forced the player's hands to do.
// Hardcore only, as the Trauma section it mirrors. Sober: who, what, how many times, the first and the last.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Trauma
{
   /// <summary>One kind of deed the player did to one character, every time it happened.</summary>
   public sealed class MisdeedFacts
   {
      /// <summary>The character's hero id.</summary>
      public string VictimId { get; init; }

      /// <summary>What the player did.</summary>
      public TraumaKind Kind { get; init; }

      /// <summary>The in-game days, oldest first, each once.</summary>
      public IReadOnlyList<int> Days { get; init; } = Array.Empty<int>();
   }

   /// <summary>The Misdeeds section's gate, its gathering and its words.</summary>
   public static class MisdeedsPagePolicy
   {
      private static readonly string[] Numbers = {"", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"};

      /// <summary>Shown only at Hardcore, and only on the player's own page (a character's page has its Trauma section).</summary>
      public static bool Shows(AdultContentLevel level, bool isThePlayersPage) => level == AdultContentLevel.Hardcore && isThePlayersPage;

      /// <summary>
      ///   The player's own deeds, one entry per character and kind, the oldest first. Only the player's will: never what a
      ///   captor or a captor's men did, never what a captor forced the player's hands to do.
      /// </summary>
      public static IReadOnlyList<MisdeedFacts> Of(IEnumerable<TraumaRecord> ledger)
         => (ledger ?? Enumerable.Empty<TraumaRecord>())
            .Where(r => r != null && r.Author == TraumaAuthor.Player && !string.IsNullOrEmpty(r.VictimId))
            .GroupBy(r => (r.VictimId, r.Kind))
            .Select(g => new MisdeedFacts {VictimId = g.Key.VictimId, Kind = g.Key.Kind, Days = g.Select(r => r.Day).Distinct().OrderBy(d => d).ToList()})
            .OrderBy(m => m.Days[0])
            .ToList();

      /// <summary>
      ///   One line, in the player's second person: "You took Phenoria by force while she was your prisoner, twice, the first
      ///   in spring 1091, the last in summer 1092."
      /// </summary>
      public static string Line(MisdeedFacts deed, string victimName, bool victimIsFemale, Func<int, string> when)
      {
         string prisoner = $"while {(victimIsFemale ? "she" : "he")} was your prisoner";
         string lead = deed?.Kind switch
         {
            TraumaKind.Assaulted => $"You took {victimName} by force {prisoner}",
            TraumaKind.Tortured => $"You tortured {victimName} {prisoner}",
            TraumaKind.Interrogated => $"You interrogated {victimName} under duress {prisoner}",
            TraumaKind.Beaten => $"You beat {victimName}",
            TraumaKind.Robbed => $"You robbed {victimName} by force",
            _ => $"You tormented {victimName} {prisoner}"
         };
         IReadOnlyList<int> days = deed?.Days ?? Array.Empty<int>();
         if (days.Count == 0) return $"{lead}.";
         if (days.Count == 1) return $"{lead}, in {when(days[0])}.";

         return $"{lead}, {(days.Count == 2 ? "twice" : $"{Count(days.Count)} times")}, {Span(days, when)}.";
      }

      // "the first in spring 1091, the last in summer 1092", or "in spring 1091" when both fall in one season.
      private static string Span(IReadOnlyList<int> days, Func<int, string> when)
      {
         string first = when(days[0]);
         string last = when(days[days.Count - 1]);

         return first == last ? $"in {first}" : $"the first in {first}, the last in {last}";
      }

      private static string Count(int n) => n < Numbers.Length ? Numbers[n] : n.ToString();
   }
}
