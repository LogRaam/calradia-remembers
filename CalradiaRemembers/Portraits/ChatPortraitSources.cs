// Code written by Gabriel Mailhot, 14/11/2026.
// Portrait sources from other mods. Bhelogan (Detective's Notebook, Muh Prompts), 14/11/2026: "a portrait source for chat
// portraits, so a mod can supply a picture per hero without naming files by runtime id". A player's dropped file is named
// by the hero's StringId, and a hero created during play has a per-save runtime id (CharacterObject_N), so a mod making
// pictures had to write files under names that mean nothing in the next campaign. A mod registers a source here and answers
// with the path of its own image for a hero; CR shows it wherever a chat portrait is shown. The player's own file still
// wins (it is their choice), and a source wins over the realm face CR gives a queen. Public door: CrPortraits.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>The registered portrait sources, asked in the order of their names.</summary>
    internal static class ChatPortraitSources
    {
        private static readonly object Gate = new object();
        private static readonly SortedDictionary<string, Func<string, string>> Sources = new SortedDictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Complained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] Extensions = {".png", ".jpg", ".jpeg"};

        /// <summary>Registers (or replaces) the source called <paramref name="name" />. A blank name or a null source is ignored.</summary>
        internal static void Register(string name, Func<string, string> source)
        {
            if (string.IsNullOrWhiteSpace(name) || source == null) return;

            lock (Gate)
            {
                Sources[name.Trim()] = source;
                Complained.Remove(name.Trim());
            }

            ModLog.Info($"[EXT PORTRAIT] source '{name.Trim()}' registered");
        }

        /// <summary>Removes the source called <paramref name="name" />. Whether one was there.</summary>
        internal static bool Unregister(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            lock (Gate) return Sources.Remove(name.Trim());
        }

        /// <summary>
        ///   The first image a source gives for <paramref name="heroId" />: a full path to an existing PNG or JPG. A source
        ///   that answers null, a missing file or another kind of file is passed over; one that throws is passed over and
        ///   logged once. Null when none gives one.
        /// </summary>
        internal static string Resolve(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;

            KeyValuePair<string, Func<string, string>>[] sources;
            lock (Gate)
            {
                if (Sources.Count == 0) return null;
                sources = Sources.ToArray();
            }

            foreach (KeyValuePair<string, Func<string, string>> source in sources)
            {
                string path;
                try { path = source.Value(heroId); }
                catch (Exception ex)
                {
                    bool first;
                    lock (Gate) first = Complained.Add(source.Key);
                    if (first) ModLog.Error($"[EXT PORTRAIT] source '{source.Key}' threw for {heroId} ({ex.GetType().Name}: {ex.Message}); passed over (said once)");
                    continue;
                }

                if (Usable(path)) return path;
            }

            return null;
        }

        private static bool Usable(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path)
                                                        && Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                                                        && File.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
