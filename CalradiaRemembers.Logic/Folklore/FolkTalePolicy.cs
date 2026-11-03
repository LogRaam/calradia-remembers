// Code written by Gabriel Mailhot, 03/11/2026.
// Gabriel, 03/11/2026: "J'aimerais creer des contes qui existeraient dans la culture des factions de Calradia. Ces contes
// pourraient faire partie de la memoire de plusieurs NPC. Les NPC sauraient que c'est des contes, mais pourraient parfois
// en faire reference, ou meme raconter le conte si on leur demande... ces histoires que l'on raconte autour du feu."
// "On peut faire les contes."
//
// Decided here, with no model call: which tales of their own people a character carries, and which one they love best.
// The favourite is the only one with its parts, so it is the only one they can tell; it is drawn from the character's id
// with a fixed hash, so the same lord loves the same tale in every session (string.GetHashCode is randomised per
// process, and a choice that moved would also break the prompt cache, since the tales sit in its stable part).
// The tales are read from ModuleData/folklore/<culture id>.txt, keyed on the character's OWN culture, not their realm's:
// a Battanian who swore to Vlandia still grew up on Battanian tales.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NpcMemoryService.Core.Knowledge;

#endregion

namespace CalradiaRemembers.Logic.Folklore
{
   /// <summary>One tale as written in a folklore file.</summary>
   public sealed class FolkTale
   {
      /// <summary>The name the tale goes by.</summary>
      public string Title { get; init; } = "";

      /// <summary>What it is about, in one line.</summary>
      public string Gist { get; init; } = "";

      /// <summary>Its parts in order; a tale with none can be named and alluded to, never told.</summary>
      public IReadOnlyList<string> Beats { get; init; } = new List<string>();
   }

   /// <summary>Reads folklore files and decides which tales a character carries.</summary>
   public static class FolkTalePolicy
   {
      /// <summary>The most tales one character carries, the favourite included: each costs a line in every prompt.</summary>
      public const int MaxTalesKnown = 6;

      /// <summary>
      ///   The game's own culture ids (SandBoxCore spcultures.xml, 1.4.8 with War Sails) a folklore file may be named
      ///   after. A file for another mod's culture is read too: the loader also takes any file it finds.
      /// </summary>
      public static readonly IReadOnlyList<string> GameCultureIds =
         new[] {"empire", "aserai", "sturgia", "vlandia", "battania", "khuzait", "nord", "vakken", "darshi"};

      /// <summary>
      ///   Reads a folklore file: each tale opens on a line "[Title]"; "gist:" gives its one-line outline and each "beat:"
      ///   one of its parts, in order. A line with no key continues the line before it. Lines starting with "#" are never
      ///   read. A tale with no gist is left out, since it could not even be alluded to.
      /// </summary>
      public static IReadOnlyList<FolkTale> Parse(string text)
      {
         var tales = new List<FolkTale>();
         if (string.IsNullOrWhiteSpace(text)) return tales;

         string title = null;
         var gist = new StringBuilder();
         var beats = new List<StringBuilder>();
         StringBuilder current = null;

         void Close()
         {
            if (title != null && gist.Length > 0)
               tales.Add(new FolkTale {
                  Title = title,
                  Gist = gist.ToString(),
                  Beats = beats.Select(b => b.ToString()).Where(b => b.Length > 0).ToList()
               });

            title = null;
            gist.Clear();
            beats.Clear();
            current = null;
         }

         foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
         {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            if (line.StartsWith("[") && line.EndsWith("]") && line.Length > 2)
            {
               Close();
               title = line.Substring(1, line.Length - 2).Trim();

               continue;
            }

            if (title == null) continue;

            if (StartsWithKey(line, "gist", out string rest))
            {
               current = gist;
               Append(current, rest);
            }
            else if (StartsWithKey(line, "beat", out rest))
            {
               current = new StringBuilder();
               beats.Add(current);
               Append(current, rest);
            }
            else if (current != null)
               Append(current, line);
         }

         Close();

         return tales;
      }

      /// <summary>
      ///   The tales a character carries: the favourite first, drawn from their id among the tales that have parts, then
      ///   others of their people up to <see cref="MaxTalesKnown" />, starting from a point their id also sets so that
      ///   not every character names the same few. Null when their people has no tales.
      /// </summary>
      public static FolkloreFacts Compose(string characterId, IReadOnlyList<FolkTale> tales)
      {
         if (tales == null || tales.Count == 0) return null;

         uint seed = StableHash(characterId ?? "");
         List<FolkTale> tellable = tales.Where(t => t.Beats.Count > 0).ToList();
         FolkTale favourite = tellable.Count > 0 ? tellable[(int)(seed % (uint)tellable.Count)] : null;

         var known = new List<FolkTale>();
         if (favourite != null) known.Add(favourite);

         int start = (int)((seed / 7) % (uint)tales.Count);
         for (int i = 0; i < tales.Count && known.Count < MaxTalesKnown; i++)
         {
            FolkTale tale = tales[(start + i) % tales.Count];
            if (!ReferenceEquals(tale, favourite)) known.Add(tale);
         }

         return new FolkloreFacts {
            Tales = known.Select(t => new FolkTaleFacts {
               Title = t.Title,
               Gist = t.Gist,
               // Only the favourite carries its parts: the others are outlines the character may not fill in.
               Beats = ReferenceEquals(t, favourite) ? t.Beats : new List<string>()
            }).ToList()
         };
      }

      /// <summary>FNV-1a over the id's characters: the same id gives the same number in every session and process.</summary>
      public static uint StableHash(string text)
      {
         uint hash = 2166136261;
         foreach (char c in text)
         {
            hash ^= c;
            hash *= 16777619;
         }

         return hash;
      }

      #region private

      private static bool StartsWithKey(string line, string key, out string rest)
      {
         rest = null;
         if (!line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase)) return false;

         rest = line.Substring(key.Length + 1).Trim();

         return true;
      }

      private static void Append(StringBuilder field, string text)
      {
         if (string.IsNullOrEmpty(text)) return;
         if (field.Length > 0) field.Append(' ');
         field.Append(text);
      }

      #endregion
   }
}
