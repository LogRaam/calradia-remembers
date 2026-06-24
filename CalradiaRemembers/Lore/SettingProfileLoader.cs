// Code written by Gabriel Mailhot, 23/06/2026.
// Loads ModuleData/setting.json and applies it to the prompt layer (PromptLore), so a total-overhaul mod
// (The Old Realms and the like) can reskin Calradia Remembers by editing one file. The resolve/defaults
// rule is pure and tested in CalradiaRemembers.Logic; this only reads the file and hands the result over.

#region

using System;
using System.IO;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Lore
{
    /// <summary>
    ///   Reads <c>ModuleData/setting.json</c> (the world's name/adjective + which cultures read as
    ///   patriarchal) and applies it to <see cref="PromptLore"/>. Any absent field falls back to the
    ///   Calradia default, so a stock install is unchanged. Safe at session launch and on cr.reload_lore.
    /// </summary>
    public static class SettingProfileLoader
    {
        public static void LoadAndApply()
        {
            string path = Path.Combine(
                BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData", "setting.json");

            SettingProfile profile = null;
            if (File.Exists(path))
            {
                try { profile = JsonConvert.DeserializeObject<SettingProfile>(File.ReadAllText(path)); }
                catch (Exception ex) { ModLog.Error($"  setting.json failed to parse — using Calradia defaults: {ex.Message}"); }
            }

            (string name, string adjective, System.Collections.Generic.IReadOnlyList<string> cultures) =
                SettingProfileDefaults.Resolve(profile);

            PromptLore.WorldName = name;
            PromptLore.WorldAdjective = adjective;
            PromptLore.PatriarchalCultures = cultures;
            ModLog.Info($"  Setting profile: world '{name}' ({adjective}); {cultures.Count} patriarchal culture(s).");
        }
    }
}
