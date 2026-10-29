// Code written by Gabriel Mailhot, 28/10/2026.
// The character archetypes were written for lords ("A formidable lord.", "The Hollow Lord. [...] Holds their title by
// inheritance alone.") and served unchanged to companions, wanderers and notables. avalanche24 (Nexus, 28/10/2026) found
// companions talking "way above their station", one reasoning from "The Cold Patron"; Gabriel (28/10/2026) asked that each
// social station be voiced as itself. A commoner keeps the same nature, worded without a lordship they do not have.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Character
{
   /// <summary>Words an archetype for the station of the one who carries it.</summary>
   public static class ArchetypeWording
   {
      // Each lordly phrase of the archetype catalog, and its commoner wording; the nature they describe is unchanged.
      private static readonly string[][] Commoner = {
         new[] {"A lord of genuine contradictions", "A person of genuine contradictions"},
         new[] {"a lord of pure disciplined conviction", "a soul of pure disciplined conviction"},
         new[] {"A blade rather than a lord.", "A blade, nothing more."},
         new[] {"The Hollow Lord.", "The Hollow One."},
         new[] {"Holds their title by inheritance alone.", "Holds their place by luck alone."},
         new[] {"The Chivalric Lord.", "The Chivalric Soul."},
         new[] {"the kind of lord who builds lasting peace", "the kind of person who keeps the peace"},
         new[] {"A formidable lord.", "A formidable fighter."},
         new[] {"the kind of lord who inspires songs", "the kind of comrade who inspires songs"},
         new[] {"A genuinely good lord by design.", "Genuinely good by design."},
         new[] {"A lord worth following for the resources", "Someone worth knowing for the coin"},
         new[] {"The Lowest Lord.", "The Lowest of the Low."},
         new[] {"every lord's vice", "every vice"},
         new[] {"Holds their title through circumstance alone.", "Holds their place through circumstance alone."},
         new[] {"The Reluctant Lord.", "The Reluctant One."},
         new[] {"The kind of lord songs are written about", "The kind of person songs are written about"},
         new[] {"A good lord to follow.", "A good person to have at your side."},
         new[] {"An old-fashioned lord in an age", "An old-fashioned soul in an age"},
         new[] {"the most useless kind of lord.", "the most useless kind of ally."},
         new[] {"the lord lives them out", "they live them out"}
      };

      /// <summary>The archetype as it reads for its bearer: unchanged for a noble, without lordship for anyone else.</summary>
      public static string ForStation(string archetype, bool noble)
      {
         if (noble || string.IsNullOrEmpty(archetype)) return archetype;

         string text = archetype;
         foreach (string[] pair in Commoner) text = text.Replace(pair[0], pair[1]);

         return text;
      }

      /// <summary>Whether a commoner's stored archetype still carries a lordship (a profile written before this wording).</summary>
      public static bool HasLordlyWords(string archetype)
      {
         if (string.IsNullOrEmpty(archetype)) return false;
         foreach (string[] pair in Commoner)
            if (archetype.IndexOf(pair[0], StringComparison.Ordinal) >= 0) return true;

         return false;
      }
   }
}
