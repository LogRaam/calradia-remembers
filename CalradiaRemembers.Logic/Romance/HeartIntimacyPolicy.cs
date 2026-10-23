// Code written by Gabriel Mailhot, 23/10/2026.
// The Heart section's intimate lines. Gabriel (23/10/2026), after cr.heart_seed showed a love story with nothing of its
// nights: "j'aimerais tester les relations sexuelles consensuelles et CNC.. elles doivent s'afficher dans le profil du joueur
// et des NPC impliques. En mode Hardcore uniquement." His rulings: CNC means the forced captive scenes; "les scenes forcees
// subies par le joueur ne sont pas affichees sur la page du joueur. Les actes que le joueur a fait subir peuvent apparaitre.
// Les scenes intimes consentantes apparaissent sur la page du joueur"; a sober detail, "du genre de quelque chose que le
// joueur aurait pu ecrire dans son journal intime"; lines added to Heart. So: the nights counted and dated, never their
// content. The prisoners the player took by force were first told here too; Gabriel (23/10/2026): "Heart doit etre une
// section sur l'amour et le desir. L'evenement 'take by force' devrait etre une autre section avec des actions
// reprehensibles", so they moved to the player's Misdeeds section (MisdeedsPagePolicy).

#region

using System;
using System.Collections.Generic;
using System.Linq;
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
