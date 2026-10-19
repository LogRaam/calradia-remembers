// Code written by Gabriel Mailhot, 19/06/2026.
// Sprint 24 (jealousy): loads ModuleData/jealousy_cultures.json (per-culture jealousy norms).

#region

using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Jealousy
{
    /// <summary>
    ///   Loads and caches <c>jealousy_cultures.json</c> from the mod's ModuleData folder,
    ///   keyed by culture display name ("Khuzait", "Battania", …) to match
    ///   <see cref="Mapping.CulturalTraditionsLoader" /> and stay interoperable with conversion
    ///   mods. Any culture absent from the file resolves to
    ///   <see cref="JealousyCultureProfile.Monogamous" />, so faction mods that add new cultures
    ///   degrade safely to monogamous behaviour without a code change.
    /// </summary>
    public static class JealousyCultureLoader
    {
        private static Dictionary<string, JealousyCultureProfile> _cache;

        /// <summary>Loads (or reloads) the JSON file. Safe to call on every session launch.</summary>
        public static void Load()
        {
            string path = NarrativeLoader.ResolveCustomFirst("jealousy_cultures.json");

            if (!File.Exists(path))
            {
                ModLog.Info($"  jealousy_cultures.json not found — all cultures default to monogamous. (looked in: {path})");
                _cache = new Dictionary<string, JealousyCultureProfile>();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                JealousyCultureFile file = JsonConvert.DeserializeObject<JealousyCultureFile>(json);
                _cache = file?.Cultures ?? new Dictionary<string, JealousyCultureProfile>();
                ModLog.Info($"  Loaded {_cache.Count} jealousy culture profile(s) from ModuleData.");
            }
            catch (System.Exception ex)
            {
                _cache = new Dictionary<string, JealousyCultureProfile>();
                ModLog.Error($"  jealousy_cultures.json failed to parse — all cultures default to monogamous. {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        ///   Returns the jealousy profile for <paramref name="cultureName" />, or the
        ///   monogamous fallback when the culture is unknown or the file is not loaded.
        /// </summary>
        public static JealousyCultureProfile Get(string cultureName)
        {
            if (_cache == null || string.IsNullOrEmpty(cultureName))
                return JealousyCultureProfile.Monogamous;

            return _cache.TryGetValue(cultureName, out JealousyCultureProfile profile)
                ? profile
                : JealousyCultureProfile.Monogamous;
        }

        /// <summary>The parsed, testable culture for <paramref name="cultureName" /> (monogamous when unknown).</summary>
        public static JealousyCulture GetCulture(string cultureName) => Get(cultureName).ToCulture();

        /// <summary>
        ///   True when <paramref name="cultureName" /> is a HAREM culture (harem-accepting AND patriarchal with a
        ///   submissive wife, e.g. Aserai): a neglected wife may take a SECRET lover even without an open pact.
        ///   Any unknown or unlisted culture resolves to the egalitarian monogamous fallback (false), so faction
        ///   mods keep control via the JSON without a code change.
        /// </summary>
        public static bool IsHaremCulture(string cultureName) => GetCulture(cultureName).IsHaremCulture;

        /// <summary>Wrapper matching the on-disk shape: a doc <c>_comment</c> plus the keyed profiles.</summary>
        private sealed class JealousyCultureFile
        {
            [JsonProperty("cultures")]
            public Dictionary<string, JealousyCultureProfile> Cultures { get; set; }
        }
    }
}
