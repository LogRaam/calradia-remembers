// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Lore
{
   /// <summary>
   ///   Loads and caches <c>character_overrides.json</c> (an array of per-NPC entries) into a
   ///   <see cref="CharacterOverrideSet" />. Safe to (re)load on session launch or on demand.
   /// </summary>
   public static class CharacterOverrideLoader
   {
      /// <summary>The current authored overrides; never null once <see cref="Load" /> has run.</summary>
      public static CharacterOverrideSet Set { get; private set; } = new(null);

      /// <summary>
      ///   The authored backstory for <paramref name="hero" /> when custom data is enabled, else null.
      ///   Used to sync <see cref="NpcMemoryService.Core.Models.NpcProfile.AuthoredBackstory" /> before
      ///   each prompt, so the flavor is never stored stale.
      /// </summary>
      public static string BackstoryFor(Hero hero)
      {
         if (!ModSettings.UseCustomTraits) return null;
         CharacterOverride ov = For(hero);

         return ov is {HasBackstory: true}
            ? ov.Backstory
            : null;
      }

      /// <summary>
      ///   The authored sexual orientation for <paramref name="hero" /> when custom data is enabled and set,
      ///   else null. Synced onto the NPC's RomanticProfile before each prompt so an edit takes effect live.
      /// </summary>
      public static NpcMemoryService.Core.Models.SexualOrientation? OrientationFor(Hero hero)
         => ModSettings.UseCustomTraits ? For(hero)?.ParsedOrientation : null;

      /// <summary>The override that applies to <paramref name="hero" />, or null.</summary>
      public static CharacterOverride For(Hero hero)
         => hero == null
            ? null
            : Set.Resolve(hero.StringId, hero.Name?.ToString());

      public static void Load()
      {
         string path = Path.Combine(
            BasePath.Name, "Modules", "CalradiaRemembers",
            "ModuleData", "character_overrides.json");

         if (!File.Exists(path))
         {
            Set = new CharacterOverrideSet(null);
            ModLog.Info($"  character_overrides.json not found — no authored characters. (looked in: {path})");

            return;
         }

         try
         {
            string json = File.ReadAllText(path);
            List<CharacterOverride> entries =
               JsonConvert.DeserializeObject<List<CharacterOverride>>(json) ?? new List<CharacterOverride>();
            Set = new CharacterOverrideSet(entries);
            ModLog.Info($"  Loaded {Set.Count} authored character override(s) from ModuleData.");
         }
         catch (Exception ex)
         {
            Set = new CharacterOverrideSet(null);
            ModLog.Error($"  character_overrides.json failed to parse — authored characters disabled: {ex.Message}");
         }
      }
   }
}