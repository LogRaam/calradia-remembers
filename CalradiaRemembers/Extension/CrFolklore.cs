// Code written by Gabriel Mailhot, 09/11/2026.
// Extension Surface: other mods give a character folk tales beside those of their own people. tashmetu (Nexus, 05/11/2026):
// "Faiths of Calradia has faiths that cross cultures, with their own saints and legends. Would you consider a way for a mod
// to add tales for a hero by something other than culture? For example, a registered function that takes a hero and returns
// a few tales in your format." Gabriel, 09/11/2026: "oui aux deux portes pour tashmetu, go". The function returns the text of
// a folklore file (the format of ModuleData/folklore/<culture>.txt), so the mod's own types name nothing of CR; the tales go
// through FolkTalePolicy like a culture's and share the same six places. CR names no mod.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Folklore;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>The door through which other mods give a character folk tales of their own. Since 2.8.4.</summary>
   public static class CrFolklore
   {
      // A source's text is parsed once and kept, since the same few texts (one per faith) come back for many heroes.
      private const int MaxParsedKept = 256;

      private static readonly SortedDictionary<string, Func<Hero, string>> Sources = new(StringComparer.Ordinal);
      private static readonly HashSet<string> Faulted = new(StringComparer.Ordinal);
      private static readonly Dictionary<string, IReadOnlyList<FolkTale>> Parsed = new(StringComparer.Ordinal);

      /// <summary>
      ///   Gives characters tales beside their people's: <paramref name="tales" /> takes a hero and returns the text of a
      ///   folklore file ("[Title]", "gist:", "beat:" lines, as in ModuleData/folklore/battania.txt), or null for none.
      ///   The tales share the six a character carries with their people's, alternating with them, and one with beats may be
      ///   the favourite they tell in full. Return the same text for the same hero from one day to the next (the tales sit in
      ///   the cached part of the prompt) and keep it cheap: it is called on every prompt built for that character.
      ///   Registering the same <paramref name="source" /> again replaces it. Names no Calradia Remembers type.
      /// </summary>
      public static void RegisterTaleSource(string source, Func<Hero, string> tales)
      {
         if (string.IsNullOrWhiteSpace(source) || tales == null) return;

         lock (Sources)
         {
            Sources[source] = tales;
            Faulted.Remove(source);
         }

         ModLog.Info($"[FOLKLORE API] tale source registered by '{source}'");
      }

      /// <summary>Withdraws the tale source of <paramref name="source" />. Returns whether one was there.</summary>
      public static bool UnregisterTaleSource(string source)
      {
         if (source == null) return false;

         lock (Sources) return Sources.Remove(source);
      }

      /// <summary>
      ///   The tales every registered source gives <paramref name="hero" />, in the order of the sources' names. A source
      ///   that throws adds nothing (logged once).
      /// </summary>
      internal static IReadOnlyList<FolkTale> TalesFor(Hero hero)
      {
         if (hero == null) return Array.Empty<FolkTale>();

         List<KeyValuePair<string, Func<Hero, string>>> sources;
         lock (Sources)
         {
            if (Sources.Count == 0) return Array.Empty<FolkTale>();

            sources = Sources.ToList();
         }

         var tales = new List<FolkTale>();
         foreach (KeyValuePair<string, Func<Hero, string>> source in sources)
            try
            {
               string text = source.Value(hero);
               if (!string.IsNullOrWhiteSpace(text)) tales.AddRange(ParseKept(text));
            }
            catch (Exception ex)
            {
               bool first;
               lock (Sources) first = Faulted.Add(source.Key);
               if (first) ModLog.Error($"[FOLKLORE API] the tale source of '{source.Key}' threw and adds nothing (logged once): {ex.GetType().Name}: {ex.Message}");
            }

         return tales;
      }

      #region private

      private static IReadOnlyList<FolkTale> ParseKept(string text)
      {
         lock (Parsed)
         {
            if (Parsed.TryGetValue(text, out IReadOnlyList<FolkTale> kept)) return kept;

            if (Parsed.Count >= MaxParsedKept) Parsed.Clear();

            IReadOnlyList<FolkTale> tales = FolkTalePolicy.Parse(text);
            Parsed[text] = tales;

            return tales;
         }
      }

      #endregion
   }
}
