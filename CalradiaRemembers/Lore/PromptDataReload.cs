// Code written by Gabriel Mailhot, 14/11/2026.
// One place that reloads the editable prompt and lore files, for the console (cr.reload_prompts, cr.reload_lore) and for
// other mods (CrPrompt.Reload, CrLore.Reload). Bhelogan (Discord, 14/11/2026) asked for public reloads instead of calling
// the console commands. Found on the way (Icezera, Discord, 13/11/2026): cr.reload_prompts reread the files but kept the
// character prompt already built from the old ones, so an edited player_description.txt waited for a save reload while the
// command said "The next reply uses them". The chat service is now built again after a reload.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Mapping;

#endregion

namespace CalradiaRemembers.Lore
{
   /// <summary>Reloads the editable prompt and lore files from disk. Main thread only.</summary>
   internal static class PromptDataReload
   {
      /// <summary>
      ///   Rereads the narrative texts, setting.json, cultural_traditions.json and jealousy_cultures.json (Custom first), then
      ///   has the next reply build its prompt from them. Returns the files that failed to reload (empty when all did).
      /// </summary>
      internal static IReadOnlyList<string> ReloadPromptFiles()
      {
         var failed = new List<string>();
         void Reload(string what, Action load)
         {
            try { load(); }
            catch (Exception ex) { failed.Add($"{what} ({ex.GetType().Name})"); }
         }

         Reload("narrative texts", NarrativeLoader.Load);
         Reload("setting.json", SettingProfileLoader.LoadAndApply);
         Reload("cultural_traditions.json", CulturalTraditionsLoader.Load);
         Reload("women_in_ranks.json", WomenInRanksLoader.Load);
         Reload("jealousy_cultures.json", JealousyCultureLoader.Load);

         NpcMemoryCampaignBehavior.Instance?.LlmServices?.Invalidate();

         return failed;
      }

      /// <summary>
      ///   Rereads character_overrides.json (with this campaign's overlay) and applies or restores the authored traits per the
      ///   "Use custom traits" setting. False when the lore behaviour is not running (no campaign).
      /// </summary>
      internal static bool ReloadLore()
      {
         CharacterLoreBehavior behavior = CharacterLoreBehavior.Instance;
         if (behavior == null) return false;

         behavior.ReloadAndReapply();
         NpcMemoryCampaignBehavior.Instance?.LlmServices?.Invalidate();

         return true;
      }
   }
}
