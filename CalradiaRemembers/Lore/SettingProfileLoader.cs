// Code written by Gabriel Mailhot, 23/06/2026.
// Loads ModuleData/setting.json and applies it to the prompt layer (PromptLore), so a total-overhaul mod
// (The Old Realms and the like) can reskin Calradia Remembers by editing one file. The resolve/defaults
// rule is pure and tested in CalradiaRemembers.Logic; this only reads the file and hands the result over.

#region

using System;
using System.IO;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using Newtonsoft.Json;
using NpcMemoryService.Core.Prompts;

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
            // Custom-first, like the narrative prompt files: a player's setting.json under ModuleData/Custom (or
            // their MCM "Custom Folder Path") overrides the shipped one, so a total-overhaul reskin survives a mod
            // update that re-extracts ModuleData (player report 2026-08-26: a TOR setting.json in Custom was
            // ignored because this loader read ModuleData directly).
            string path = NarrativeLoader.ResolveCustomFirst("setting.json");
            bool useCustom = File.Exists(path)
                && path != Path.Combine(NarrativeLoader.ModuleDataFolder(), "setting.json");

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
            ModLog.Info($"  Setting profile{(useCustom ? " (custom)" : "")}: world '{name}' ({adjective}); {cultures.Count} patriarchal culture(s).");
        }
    }
}
