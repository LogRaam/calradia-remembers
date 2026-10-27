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
// The tastes the player has learned of a character in bed (IntimateActReading) are named here too; Gabriel (27/10/2026):
// "pour les preferences il faudrait le nommer car le joueur pourrait ne pas s'en souvenir". Only what the player learned,
// never a taste still undiscovered.

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

   /// <summary>What the player has learned of a character's tastes in bed.</summary>
   public sealed class HeartTastesFacts
   {
      /// <summary>The character is a woman (the line's pronouns).</summary>
      public bool IsFemale { get; init; }

      /// <summary>The gestures they love.</summary>
      public IReadOnlyList<IntimateAct> Loved { get; init; } = Array.Empty<IntimateAct>();

      /// <summary>The gestures they dislike.</summary>
      public IReadOnlyList<IntimateAct> Disliked { get; init; } = Array.Empty<IntimateAct>();

      /// <summary>The gestures they are less fond of.</summary>
      public IReadOnlyList<IntimateAct> LessFond { get; init; } = Array.Empty<IntimateAct>();

      /// <summary>The tastes the player has learned (RomanticProfile.KnownActs).</summary>
      public IReadOnlyList<IntimateAct> Known { get; init; } = Array.Empty<IntimateAct>();
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
      ///   The line on the character's page naming the tastes the player has learned: "In bed she loves being on top, is
      ///   less fond of crude words, and does not like being handled roughly." Null when nothing has been learned.
      /// </summary>
      public static string TastesLine(HeartTastesFacts f)
      {
         if (f == null) return null;
         IReadOnlyList<IntimateAct> known = f.Known ?? Array.Empty<IntimateAct>();
         List<IntimateAct> loved = Learned(f.Loved, known, new List<IntimateAct>());
         List<IntimateAct> disliked = Learned(f.Disliked, known, loved);
         List<IntimateAct> lessFond = Learned(f.LessFond, known, loved.Concat(disliked).ToList());

         string her = f.IsFemale ? "her" : "him";
         string Tastes(List<IntimateAct> acts) => Joined(acts.Select(a => Taste(a, f.IsFemale, her)).ToList());
         var clauses = new List<string>();
         if (loved.Count > 0) clauses.Add("loves " + Tastes(loved));
         if (lessFond.Count > 0) clauses.Add("is less fond of " + Tastes(lessFond));
         if (disliked.Count > 0) clauses.Add("does not like " + Tastes(disliked));
         if (clauses.Count == 0) return null;

         string said = clauses.Count == 1 ? clauses[0] : string.Join(", ", clauses.Take(clauses.Count - 1)) + ", and " + clauses[clauses.Count - 1];

         return $"In bed {(f.IsFemale ? "she" : "he")} {said}.";
      }

      // The tastes of one list the player learned, in the gestures' own order, never one already told in another list.
      private static List<IntimateAct> Learned(IReadOnlyList<IntimateAct> tastes, IReadOnlyList<IntimateAct> known, List<IntimateAct> already)
         => (tastes ?? Array.Empty<IntimateAct>()).Where(a => a != IntimateAct.None && known.Contains(a) && !already.Contains(a))
                                                   .Distinct().OrderBy(a => (int) a).ToList();

      // A gesture as the player reads it on the character's page, in the player's second person.
      private static string Taste(IntimateAct a, bool female, string her) => a switch {
         IntimateAct.Kissing => "long, deep kisses",
         IntimateAct.OralGiving => $"taking you in {(female ? "her" : "his")} mouth",
         IntimateAct.OralReceiving => $"your mouth on {her}",
         IntimateAct.AnalGiving => "taking you anally",
         IntimateAct.AnalReceiving => "being taken anally",
         IntimateAct.OnTop => "being on top",
         IntimateAct.Beneath => "lying beneath you",
         IntimateAct.FromBehind => "being taken from behind",
         IntimateAct.HandsAndFingers => $"your hands and fingers on {her}",
         IntimateAct.RoughHandling => "being handled roughly",
         IntimateAct.DirtyTalk => "crude words",
         _ => "something"
      };

      private static string Joined(IReadOnlyList<string> parts)
         => parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];

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
