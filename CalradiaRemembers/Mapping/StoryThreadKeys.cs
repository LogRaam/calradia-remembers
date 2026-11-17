// Code written by Gabriel Mailhot, 17/11/2026.
// Gabriel (17/11/2026): "je peux parler de ma mission a un NPC a plusieurs reprises dans differentes conversations, et la
// memoire devrait lier ces conversations." The story archive links conversations by THREAD KEYS written on each line: the
// people and places a line names, read against the game's own heroes and settlements (never guessed), so two talks about
// "the bandits near Varcheg" weeks apart share "place:<Varcheg's id>" even when their words differ. People go through the
// resolver the prompt already uses for "NAMES MENTIONED" (namesakes of the speaker skipped, the likeliest meant kept).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The thread keys of a line: the heroes and settlements it names.</summary>
   public static class StoryThreadKeys
   {
      private static List<(Regex Name, string Id, string Label)> _places;

      /// <summary>"hero:&lt;id&gt;" and "place:&lt;id&gt;" for what <paramref name="text" /> names, as heard by <paramref name="speaker" />. Never throws.</summary>
      public static List<string> For(Hero speaker, string text)
      {
         var keys = new List<string>();
         if (string.IsNullOrWhiteSpace(text)) return keys;

         try
         {
            foreach (string id in ContextualNameResolver.NamedHeroIds(speaker, text))
               keys.Add("hero:" + id);
         }
         catch
         {
            // a name the resolver cannot read costs only that thread
         }

         try
         {
            foreach ((Regex name, string id, string _) in Places())
               if (name.IsMatch(text))
                  keys.Add("place:" + id);
         }
         catch
         {
            // the same
         }

         return keys.Distinct().ToList();
      }

      /// <summary>The name a thread key is told by ("Varcheg", "Count Aldric"); null for a key that names nothing shown (a memory code).</summary>
      public static string LabelOf(string key)
      {
         if (string.IsNullOrEmpty(key)) return null;

         try
         {
            if (key.StartsWith("place:", StringComparison.Ordinal))
               return Places().FirstOrDefault(p => p.Id == key.Substring(6)).Label;
            if (key.StartsWith("hero:", StringComparison.Ordinal))
            {
               string id = key.Substring(5);

               return Hero.FindFirst(h => h != null && h.StringId == id)?.Name?.ToString();
            }
         }
         catch
         {
            // no label: the passage is shown without its matter
         }

         return null;
      }

      // Settlements do not change during a campaign; a new campaign may bring other ones, so the list follows the campaign.
      private static WeakReference<Campaign> _placesFor = new WeakReference<Campaign>(null);

      private static List<(Regex Name, string Id, string Label)> Places()
      {
         if (_places != null && _placesFor.TryGetTarget(out Campaign was) && ReferenceEquals(was, Campaign.Current)) return _places;

         _placesFor = new WeakReference<Campaign>(Campaign.Current);
         _places = Settlement.All
                             .Where(s => s != null && (s.IsTown || s.IsCastle || s.IsVillage) && (s.Name?.ToString().Length ?? 0) >= 4)
                             .Select(s => (new Regex(@"\b" + Regex.Escape(s.Name.ToString()) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                                           s.StringId, s.Name.ToString()))
                             .ToList();

         return _places;
      }
   }
}
