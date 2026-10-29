// Code written by Gabriel Mailhot, 28/10/2026.
// One definition per social station, composed into the prompt (Gabriel, 28/10/2026: "que dirais-tu de faire une definition
// par statut social, et injecter par composition le bon statut dans le prompt. Il pourrait y en avoir un pour un compagnon
// wanderer, pour un Noble, pour un villageois, pour un Bandit, pour un Marchand, etc."), each saying how the station treats
// the player on each of the five rungs ("Un paysan qui parle au joueur niveau 1 sera plus rude que si il parle au joueur
// niveau 20 Clan tier 3"). avalanche24 (Nexus, 28/10/2026) met companions and a landowner who spoke "way above their
// station". The definitions are text files a player or a mod may rewrite (Gabriel: "txt"); what a station may DO (a duel,
// a brawl) stays in code, so a file never grants a mechanic.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Social
{
   /// <summary>What a character's station is read from.</summary>
   public sealed class SocialStationFacts
   {
      /// <summary>Rules a realm.</summary>
      public bool IsRuler { get; init; }

      /// <summary>A lord or a lady of a noble house (the game's Lord occupation).</summary>
      public bool IsNoble { get; init; }

      /// <summary>Heads their own house, landed or not (a minor or a mercenary clan's head too).</summary>
      public bool HeadsHouse { get; init; }

      /// <summary>A companion hired into the player's company.</summary>
      public bool IsPlayerCompanion { get; init; }

      /// <summary>The game's occupation name ("Merchant", "Wanderer", "GangLeader"...).</summary>
      public string Occupation { get; init; } = "";
   }

   /// <summary>Resolves a character's station and composes its definition for the player's rung.</summary>
   public static class SocialStationPolicy
   {
      /// <summary>Station keys: the file <c>ModuleData/stations/&lt;key&gt;.txt</c> defines each.</summary>
      public const string Ruler = "ruler", Lord = "lord", Noble = "noble", Companion = "companion", Wanderer = "wanderer",
         GangLeader = "gangleader";

      /// <summary>Every station that ships a definition, the notable trades included.</summary>
      public static readonly string[] Keys = {Ruler, Lord, Noble, Companion, Wanderer, "merchant", "artisan", "ruralnotable", "headman", "preacher", GangLeader};

      /// <summary>The section that says who the character is.</summary>
      public const string WhoSection = "who";

      /// <summary>The station of a character, or null for one no definition covers (a soldier, a child).</summary>
      public static string Resolve(SocialStationFacts f)
      {
         if (f == null) return null;
         if (f.IsRuler) return Ruler;
         if (f.IsNoble) return f.HeadsHouse ? Lord : Noble;
         if (f.IsPlayerCompanion) return Companion;

         switch (f.Occupation)
         {
            case "Wanderer": return Wanderer;
            case "GangLeader":
            case "Bandit": return GangLeader;
            case "Merchant":
            case "Artisan":
            case "RuralNotable":
            case "Headman":
            case "Preacher": return f.Occupation.ToLowerInvariant();
            default: return null;
         }
      }

      /// <summary>The section that says how the station treats a player on <paramref name="rung" />.</summary>
      public static string RungSection(PlayerRung rung) => rung.ToString().ToLowerInvariant();

      /// <summary>
      ///   Reads a definition: sections opened by a line "[name]", lines starting with "#" ignored, text kept as written.
      ///   Section names are lower case. Text before the first section is ignored.
      /// </summary>
      public static IReadOnlyDictionary<string, string> Parse(string text)
      {
         var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
         if (string.IsNullOrWhiteSpace(text)) return sections;

         string current = null;
         var body = new StringBuilder();

         foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
         {
            string line = raw.Trim();
            if (line.StartsWith("#")) continue;

            if (line.StartsWith("[") && line.EndsWith("]") && line.Length > 2)
            {
               Close(sections, current, body);
               current = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();

               continue;
            }

            if (current == null || line.Length == 0) continue;
            if (body.Length > 0) body.Append(' ');
            body.Append(line);
         }

         Close(sections, current, body);

         return sections;
      }

      /// <summary>Who the character is, from their definition; null when the definition says nothing.</summary>
      public static string Who(IReadOnlyDictionary<string, string> definition) => Section(definition, WhoSection);

      /// <summary>How the character treats the player on <paramref name="rung" />; null when the definition says nothing.</summary>
      public static string Toward(IReadOnlyDictionary<string, string> definition, PlayerRung rung) => Section(definition, RungSection(rung));

      #region private

      private static string Section(IReadOnlyDictionary<string, string> definition, string name)
         => definition != null && definition.TryGetValue(name, out string text) && !string.IsNullOrWhiteSpace(text) ? text : null;

      private static void Close(Dictionary<string, string> sections, string current, StringBuilder body)
      {
         if (current != null) sections[current] = body.ToString().Trim();
         body.Clear();
      }

      #endregion
   }
}
