// Code written by Gabriel Mailhot, 19/11/2026.
// What the narrator of a tale's scene knows of the player and of the world around. Schmidt (Discord, 18/11/2026): "Maybe the
// tales could include what you have in your inventory [...] Not to forget that characters can give you stuff in the tale
// like their weapons, in case the AI says they have it, while you don't. So that you cannot write that you take out your two
// handed axe despite not even having one." And: "characters in the tale don't have knowledge of the world and characters
// outside the tale, like lords and ladies. Like when I mentioned in the tale that I have been sent by lady Gunjadrid,
// daughter of Orvi, and the characters said they never heard of her, despite her being in the closest town. Instead the tale
// made up an old man that was called Gunjadrid."
// Gabriel (19/11/2026) took the three recommendations: (1) what the player carries, and the player uses only that; (2) a
// thing a person hands the player in the scene is theirs for the scene only, never into the inventory (only the author's
// "item" consequence gives a real one); (3) the player's companions, the lord of the place and the lords and ladies of the
// nearest town, so a name the player speaks is that real person. The party joining the scene stays out (party_may_join,
// his call).

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>A person of the world the narrator is told of: the name as the prompt names them, and who they are.</summary>
   public sealed class ScenePersonFact
   {
      /// <summary>The name, as HeroLabel words it.</summary>
      public string Name { get; init; }

      /// <summary>Who they are to the player or the place ("lord of Varcheg", "daughter of Orvi, in Varcheg now").</summary>
      public string Note { get; init; }
   }

   /// <summary>What the game knows of the player and of the world near a tale's scene. Every list may be empty.</summary>
   public sealed class ScenePlayerFacts
   {
      /// <summary>What the player has on them: weapons, armour, mount, by their item names.</summary>
      public IReadOnlyList<string> Carried { get; init; } = new string[0];

      /// <summary>The player's companions in their party: with the party, not in the scene.</summary>
      public IReadOnlyList<ScenePersonFact> Company { get; init; } = new ScenePersonFact[0];

      /// <summary>The lord of the place and the lords and ladies in the nearest town.</summary>
      public IReadOnlyList<ScenePersonFact> Nearby { get; init; } = new ScenePersonFact[0];
   }

   /// <summary>The narrator's block about what the player carries and who in the world they may name. Pure.</summary>
   public static class ScenePlayerText
   {
      /// <summary>The most things listed as carried.</summary>
      public const int MaxCarried = 12;

      /// <summary>The most people of the world listed (companions first, then those nearby).</summary>
      public const int MaxPeople = 14;

      /// <summary>The block for the narrator's prompt, or null when nothing is known.</summary>
      public static string Compose(ScenePlayerFacts f)
      {
         if (f == null) return null;

         var sb = new StringBuilder();
         List<string> carried = (f.Carried ?? new string[0]).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().Take(MaxCarried).ToList();
         if (carried.Count > 0)
         {
            sb.AppendLine("WHAT THE PLAYER CARRIES (they have these, and only these, plus what someone in this scene hands them): " + string.Join(", ", carried) + ".");
            sb.AppendLine("If the player reaches for anything else, it is not there: say so in a line and go on. Never give them a thing by narrating it.");
         }

         // A gift in the scene is the scene's: the narration may let a person lend their spear, never fill the player's pack.
         sb.AppendLine("A person here may lend or give the player something they themselves have. It is the player's for the rest of this scene "
                       + "and the scene remembers it; it does not leave the tale with them.");

         var people = new List<string>();
         var seen = new HashSet<string>();
         foreach (ScenePersonFact p in (f.Company ?? new ScenePersonFact[0]).Concat(f.Nearby ?? new ScenePersonFact[0]))
         {
            if (p == null || string.IsNullOrWhiteSpace(p.Name) || !seen.Add(p.Name.Trim())) continue;
            people.Add(string.IsNullOrWhiteSpace(p.Note) ? "- " + p.Name.Trim() : $"- {p.Name.Trim()}: {p.Note.Trim()}");
            if (people.Count >= MaxPeople) break;
         }

         if (people.Count > 0)
         {
            sb.AppendLine();
            sb.AppendLine("PEOPLE OF THE WORLD THE PLAYER MAY SPEAK OF (real people, none of them in this scene unless listed above):");
            foreach (string l in people) sb.AppendLine(l);
            sb.AppendLine("A name the player speaks that is on this list is that very person: people here may have heard of them, as far as their "
                          + "station makes it likely, and never take them for someone else who bears the name. A name on no list is unknown here: "
                          + "never make up a person to bear it.");
         }

         string text = sb.ToString().TrimEnd();

         return text.Length == 0 ? null : text;
      }
   }
}
