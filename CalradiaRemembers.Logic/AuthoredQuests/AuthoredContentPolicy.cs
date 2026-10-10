// Code written by Gabriel Mailhot, 10/10/2026.
// A written tale's content level (Gabriel, 10/10/2026: "can a quest be classed Hardcore?"). Until now a tale had none:
// a hardcore tale would have been offered to every player, adult content off included, and its facts and scenes would
// have reached their conversations as written. "requires.content" names the level the tale needs, in the MCM's own
// words; a tale is offered only to a player whose setting reaches it, and one under way stops when the setting is lowered.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>A tale's content level against the player's setting. Pure.</summary>
   public static class AuthoredContentPolicy
   {
      /// <summary>The levels a tale may ask for, lowest first, as the MCM names them ("none" for a tale for everyone).</summary>
      public static readonly IReadOnlyList<string> Levels = new[] {"none", "mature", "explicit", "hardcore"};

      /// <summary>The setting a tale's "content" needs; Off when it asks for none; null when the word is no level.</summary>
      public static AdultContentLevel? Required(string content)
      {
         if (string.IsNullOrWhiteSpace(content)) return AdultContentLevel.Off;
         switch (content.Trim().ToLowerInvariant())
         {
            case "none": return AdultContentLevel.Off;
            case "mature": return AdultContentLevel.Mature;
            case "explicit": return AdultContentLevel.Explicit;
            case "hardcore": return AdultContentLevel.Hardcore;
            default: return null;
         }
      }

      /// <summary>The player's setting reaches the tale's level. A level the game does not know is never allowed.</summary>
      public static bool Allows(string content, AdultContentLevel playerSetting)
         => Required(content) is AdultContentLevel needed && playerSetting >= needed;
   }
}
