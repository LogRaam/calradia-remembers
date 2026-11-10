// Code written by Gabriel Mailhot, 09/11/2026.
// Extension Surface: other mods add a line to how a character looks. tashmetu (Nexus, 05/11/2026): Traits of Calradia's
// looks traits (Comely, Beautiful, Homely, Hideous) and marks (Scarred, One-eyed) "reach the chat only through the nature
// line... Maifailun would like them in your YOUR OWN APPEARANCE block too, and in how the NPC sees the player... a registered
// function that takes a hero id and the voice (self, or as another sees them) and returns one short line or nothing, which
// CR adds to the block. You'd keep the last word on the face; we would only add a line, never replace yours." Gabriel,
// 09/11/2026: "oui aux deux portes pour tashmetu, go". AppearanceComposer.WithOutsideLines decides what is kept. CR names
// no mod.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The door through which other mods add a line to how a character looks. Since 2.8.7.</summary>
   public static class CrAppearance
   {
      private static readonly SortedDictionary<string, Func<string, bool, string>> Sources = new(StringComparer.Ordinal);
      private static readonly HashSet<string> Faulted = new(StringComparer.Ordinal);

      /// <summary>
      ///   Adds a line to a character's appearance: <paramref name="line" /> takes a hero's StringId and whether the line is
      ///   seen by another (true: third person, "A scar runs across her cheek.", for the player as a character sees them;
      ///   false: the character's own body, second person, "A scar runs across your cheek.") and returns one short sentence,
      ///   or null for nothing. CR's own appearance comes first and is never changed; at most two lines from all mods are
      ///   kept, and a line over 200 characters is left out. Return the same line for the same hero until their looks change
      ///   (the character's own appearance sits in the cached part of the prompt) and keep it cheap: it is called on every
      ///   prompt built. Registering the same <paramref name="source" /> again replaces it. Names no Calradia Remembers type.
      /// </summary>
      public static void RegisterLineSource(string source, Func<string, bool, string> line)
      {
         if (string.IsNullOrWhiteSpace(source) || line == null) return;

         lock (Sources)
         {
            Sources[source] = line;
            Faulted.Remove(source);
         }

         ModLog.Info($"[APPEARANCE API] line source registered by '{source}'");
      }

      /// <summary>Withdraws the line source of <paramref name="source" />. Returns whether one was there.</summary>
      public static bool UnregisterLineSource(string source)
      {
         if (source == null) return false;

         lock (Sources) return Sources.Remove(source);
      }

      /// <summary>
      ///   What every registered source adds for <paramref name="heroId" />, in the order of the sources' names. A source
      ///   that throws adds nothing (logged once).
      /// </summary>
      internal static IReadOnlyList<string> LinesFor(string heroId, bool seenByOther)
      {
         if (string.IsNullOrEmpty(heroId)) return Array.Empty<string>();

         List<KeyValuePair<string, Func<string, bool, string>>> sources;
         lock (Sources)
         {
            if (Sources.Count == 0) return Array.Empty<string>();

            sources = Sources.ToList();
         }

         var lines = new List<string>();
         foreach (KeyValuePair<string, Func<string, bool, string>> source in sources)
            try
            {
               string line = source.Value(heroId, seenByOther);
               if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
            }
            catch (Exception ex)
            {
               bool first;
               lock (Sources) first = Faulted.Add(source.Key);
               if (first) ModLog.Error($"[APPEARANCE API] the line source of '{source.Key}' threw and adds nothing (logged once): {ex.GetType().Name}: {ex.Message}");
            }

         return lines;
      }
   }
}
