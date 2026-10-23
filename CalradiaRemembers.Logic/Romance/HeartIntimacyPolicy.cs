// Code written by Gabriel Mailhot, 23/10/2026.
// The Heart section's intimate lines. Gabriel (23/10/2026), after cr.heart_seed showed a love story with nothing of its
// nights: "j'aimerais tester les relations sexuelles consensuelles et CNC.. elles doivent s'afficher dans le profil du joueur
// et des NPC impliques. En mode Hardcore uniquement." His rulings: CNC means the forced captive scenes; "les scenes forcees
// subies par le joueur ne sont pas affichees sur la page du joueur. Les actes que le joueur a fait subir peuvent apparaitre.
// Les scenes intimes consentantes apparaissent sur la page du joueur"; a sober detail, "du genre de quelque chose que le
// joueur aurait pu ecrire dans son journal intime"; lines added to Heart. So: the nights counted and dated, never their
// content; on the player's page the prisoners the player took by force. A forced act already shows on its victim's page in
// the Trauma section, so it is not repeated under Heart, which would tell an assault as romance.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Trauma;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>The nights the player shared with one character, as the page tells them.</summary>
   public sealed class HeartNightsFacts
   {
      /// <summary>The character's name, for the player's page.</summary>
      public string Name { get; init; }

      /// <summary>The in-game days of the character's intimate memories with the player; a day held twice is one night.</summary>
      public IReadOnlyList<int> NightDays { get; init; } = Array.Empty<int>();
   }

   /// <summary>The times the player took one prisoner by force.</summary>
   public sealed class HeartForcedActs
   {
      /// <summary>The prisoner's hero id.</summary>
      public string VictimId { get; init; }

      /// <summary>The in-game days, oldest first.</summary>
      public IReadOnlyList<int> Days { get; init; } = Array.Empty<int>();
   }

   /// <summary>The intimate lines of the Heart section: their gate and their words.</summary>
   public static class HeartIntimacyPolicy
   {
      private static readonly string[] Numbers = {"", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"};

      /// <summary>Shown only at Hardcore (Gabriel, 23/10/2026).</summary>
      public static bool Shows(AdultContentLevel level) => level == AdultContentLevel.Hardcore;

      /// <summary>The line on the character's page, in the player's second person; null when they never shared a night.</summary>
      public static string NightsLine(HeartNightsFacts f, Func<int, string> when)
      {
         IReadOnlyList<int> nights = Nights(f);
         if (nights.Count == 0) return null;
         if (nights.Count == 1) return $"You shared a night together in {when(nights[0])}.";

         return $"You have shared {Count(nights.Count)} nights together, {Span(nights, when)}.";
      }

      /// <summary>The line on the player's page for one partner; null when they never shared a night.</summary>
      public static string NightsRecapLine(HeartNightsFacts f, Func<int, string> when)
      {
         IReadOnlyList<int> nights = Nights(f);
         if (nights.Count == 0) return null;
         if (nights.Count == 1) return $"A night with {f.Name}, in {when(nights[0])}.";

         return $"{Capitalised(Count(nights.Count))} nights with {f.Name}, {Span(nights, when)}.";
      }

      /// <summary>
      ///   The prisoners the player took by force, one entry per prisoner, oldest first. Only the player's own deed: never
      ///   what a captor did, nor what a captor forced the player's hands to do, and never a deed that was not intimate.
      /// </summary>
      public static IReadOnlyList<HeartForcedActs> ForcedByThePlayer(IEnumerable<TraumaRecord> ledger)
         => (ledger ?? Enumerable.Empty<TraumaRecord>())
            .Where(r => r != null && r.Kind == TraumaKind.Assaulted && r.Author == TraumaAuthor.Player && !string.IsNullOrEmpty(r.VictimId))
            .GroupBy(r => r.VictimId)
            .Select(g => new HeartForcedActs {VictimId = g.Key, Days = g.Select(r => r.Day).Distinct().OrderBy(d => d).ToList()})
            .OrderBy(a => a.Days[0])
            .ToList();

      /// <summary>The line on the player's page for one prisoner taken by force.</summary>
      public static string ForcedLine(HeartForcedActs acts, string victimName, bool victimIsFemale, Func<int, string> when)
      {
         string lead = $"You took {victimName} by force while {(victimIsFemale ? "she" : "he")} was your prisoner";
         IReadOnlyList<int> days = acts?.Days ?? Array.Empty<int>();
         if (days.Count == 0) return $"{lead}.";
         if (days.Count == 1) return $"{lead}, in {when(days[0])}.";

         return $"{lead}, {(days.Count == 2 ? "twice" : $"{Count(days.Count)} times")}, {Span(days, when)}.";
      }

      private static IReadOnlyList<int> Nights(HeartNightsFacts f)
         => (f?.NightDays ?? Array.Empty<int>()).Distinct().OrderBy(d => d).ToList();

      // "the first in spring 1091, the last in summer 1092", or "in spring 1091" when both fall in one season.
      private static string Span(IReadOnlyList<int> days, Func<int, string> when)
      {
         string first = when(days[0]);
         string last = when(days[days.Count - 1]);

         return first == last ? $"in {first}" : $"the first in {first}, the last in {last}";
      }

      private static string Count(int n) => n < Numbers.Length ? Numbers[n] : n.ToString();

      private static string Capitalised(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
   }
}
