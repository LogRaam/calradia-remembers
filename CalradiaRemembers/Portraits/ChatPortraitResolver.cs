// Code written by Gabriel Mailhot, 10/07/2026.
// Custom chat portraits: a player drops hand-made images in Modules/CalradiaRemembers/ModuleData/ChatPortraits/,
// named by the hero's StringId (rename-proof) or a friendly normalized name, and any chat line by that hero shows
// the image instead of the vanilla 3D render. Resolution is a simple file lookup; loading + display is the
// RuntimePortraitFactory path. Missing folder or file: returns null and the line falls back to the 3D render.

#region

using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>Finds a player-dropped chat portrait for a hero, by StringId then by normalized name.</summary>
    internal static class ChatPortraitResolver
    {
        private static readonly string[] Extensions = {".png", ".jpg", ".jpeg"};

        /// <summary>The folder the player drops images into (may not exist).</summary>
        public static string PortraitDir()
            => Path.Combine(BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData", "ChatPortraits");

        /// <summary>
        ///   Creates the ChatPortraits folder with a short README on session launch, so the player has an obvious,
        ///   ready place to drop images (the module's ModuleData deploy only copies loose files, not subfolders).
        /// </summary>
        public static void EnsureFolder()
        {
            try
            {
                string dir = PortraitDir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string readme = Path.Combine(dir, "README.txt");
                if (!File.Exists(readme))
                    File.WriteAllText(readme,
                        "Calradia Remembers: custom chat portraits.\r\n\r\n" +
                        "Drop a square PNG or JPG here to replace an NPC's (or your own) 3D portrait in the chat window.\r\n" +
                        "Name the file after the hero's StringId (rename-proof) or their name, e.g.:\r\n" +
                        "  player.png         (YOUR OWN portrait)\r\n" +
                        "  lord_1_16.png      (an NPC's StringId; use the cr.portrait_id console command in a chat to see it)\r\n" +
                        "  rhagaea.png        (a friendlier lowercase name, spaces as underscores)\r\n\r\n" +
                        "No image for a hero: their line keeps the normal 3D render. Around 512x512 or larger looks best.\r\n");
            }
            catch
            {
                // best-effort; a missing folder simply means no custom portraits, which is fine
            }
        }

        /// <summary>Full path of the image for this hero, or null when none exists.</summary>
        public static string ResolveForHero(string stringId, string displayName)
        {
            try
            {
                string dir = PortraitDir();
                if (!Directory.Exists(dir)) return null;

                foreach (string key in Keys(stringId, displayName))
                    foreach (string ext in Extensions)
                    {
                        string path = Path.Combine(dir, key + ext);
                        if (File.Exists(path)) return path;
                    }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>The file name(s) the player should drop for this hero, for the cr.portrait_id console.</summary>
        public static string FileNameHint(string stringId, string displayName)
        {
            string norm = Normalize(displayName);

            return string.IsNullOrEmpty(norm)
                ? $"{stringId}.png"
                : $"{stringId}.png  (or the friendlier  {norm}.png)";
        }

        private static IEnumerable<string> Keys(string stringId, string displayName)
        {
            // The player's own portrait uses the simple fixed name "player.png": their StringId is opaque and
            // their character name varies from playthrough to playthrough.
            if (!string.IsNullOrEmpty(stringId) && stringId == Hero.MainHero?.StringId)
                yield return "player";

            if (!string.IsNullOrEmpty(stringId)) yield return stringId;

            string norm = Normalize(displayName);
            if (!string.IsNullOrEmpty(norm)) yield return norm;
        }

        /// <summary>Lowercase, alphanumerics kept, spaces/underscores/dashes collapsed to '_'.</summary>
        private static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            var sb = new StringBuilder();
            foreach (char c in name.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
            }

            return sb.ToString();
        }
    }
}
