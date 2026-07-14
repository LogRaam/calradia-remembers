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
      ///   The authored CONVICTION for this hero: what they hold to be true, and therefore what they pursue.
      ///   Null when unset or when custom traits are off, exactly like the backstory it sits beside. Unlike the
      ///   backstory (voice only), this one reaches the prompt as a MOTIVE, so it is synced per turn the same way.
      /// </summary>
      public static string ConvictionFor(Hero hero)
      {
         if (!ModSettings.UseCustomTraits) return null;
         CharacterOverride ov = For(hero);

         return ov is {HasConviction: true}
            ? ov.Conviction
            : null;
      }

      /// <summary>
      ///   Copies the two authored PROSE fields (voice, and what the hero holds true) onto the profile that is
      ///   about to be sent to the model. Called on EVERY turn, never persisted deliberately: a stale copy would
      ///   survive a file edit or the MCM toggle being turned off, and the NPC would keep an authored persona the
      ///   player had already withdrawn. Both fields go to null when custom data is off, which erases any copy an
      ///   earlier turn left on the profile.
      ///   Shared with the self-test harness on purpose: <c>cr.test conviction</c> exercises THIS method, so the
      ///   test fails if the chat's per-turn sync is ever dropped, instead of quietly passing on a parallel path.
      /// </summary>
      public static void SyncAuthoredProse(NpcMemoryService.Core.Models.NpcProfile profile, Hero hero)
      {
         if (profile == null || hero == null) return;

         profile.AuthoredBackstory = BackstoryFor(hero);
         profile.AuthoredConviction = ConvictionFor(hero);
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

      /// <summary>
      ///   Adds (or replaces) a single override at runtime, keyed by hero id/name, without touching
      ///   character_overrides.json. Used by the console to inject a test backstory into the live prompt
      ///   pipeline (the per-turn sync reads <see cref="BackstoryFor" /> from this set). A session reload
      ///   restores the file-backed set. Callers must also ensure custom traits are enabled for it to apply.
      /// </summary>
      public static void InjectRuntimeOverride(CharacterOverride ov)
      {
         if (ov != null) Set = Set.With(ov);
      }

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