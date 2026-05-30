// Code written by Gabriel Mailhot, 30/05/2026.

#region

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Loads and caches the <c>cultural_traditions.json</c> file from the mod's
    ///   ModuleData folder. Keyed by culture display name (e.g. "Empire", "Vlandia")
    ///   to match the format used by AI Influence and other mods, enabling
    ///   interoperability: a conversion mod author can ship their own
    ///   <c>cultural_traditions.json</c> with culture names appropriate to their world
    ///   and CalradiaRemembers will pick them up automatically.
    ///   Unknown cultures fall back to an empty string — the caller handles graceful degradation.
    /// </summary>
    public static class CulturalTraditionsLoader
    {
        private static Dictionary<string, string> _cache;

        /// <summary>
        ///   Loads (or reloads) the JSON file. Safe to call on every session launch.
        /// </summary>
        public static void Load()
        {
            string path = Path.Combine(
                BasePath.Name, "Modules", "CalradiaRemembers",
                "ModuleData", "cultural_traditions.json");

            if (!File.Exists(path))
            {
                ModLog.Info($"  cultural_traditions.json not found — cultural context disabled. (looked in: {path})");
                _cache = new Dictionary<string, string>();
                return;
            }

            string json = File.ReadAllText(path);
            _cache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                     ?? new Dictionary<string, string>();

            ModLog.Info($"  Loaded {_cache.Count} cultural tradition(s) from ModuleData.");
        }

        /// <summary>
        ///   Returns the cultural description for <paramref name="cultureName" />,
        ///   or an empty string if unknown or not yet loaded.
        /// </summary>
        public static string Get(string cultureName)
        {
            if (_cache == null || string.IsNullOrEmpty(cultureName)) return "";
            return _cache.TryGetValue(cultureName, out string desc) ? desc : "";
        }
    }
}
