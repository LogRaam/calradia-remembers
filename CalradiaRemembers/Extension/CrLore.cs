// Code written by Gabriel Mailhot, 14/11/2026.
// Extension Surface: the authored character files (character_overrides.json). Bhelogan (Discord, 14/11/2026), whose Muh
// Prompts edits CR's Custom files: "Public reloads, e.g. CrPrompt.Reload() / CrLore.Reload(), instead of us calling the
// cr.reload_prompts / cr.reload_lore console commands." Since 2.8.10.

namespace CalradiaRemembers
{
   /// <summary>The door to CR's authored character files. Since 2.8.10.</summary>
   public static class CrLore
   {
      /// <summary>
      ///   Rereads character_overrides.json and applies or restores the authored traits per the "Use custom traits" setting,
      ///   what <c>cr.reload_lore</c> does; the next reply is built from it. Returns false when no campaign is running. Call
      ///   it on the main thread.
      /// </summary>
      public static bool Reload() => Lore.PromptDataReload.ReloadLore();

      /// <summary>
      ///   This campaign's own overrides folder, <c>&lt;Custom folder&gt;/campaigns/&lt;unique game id&gt;/</c>, whether or not it exists
      ///   yet; null when no campaign is loaded. A character_overrides.json written there is laid over the shared one for this
      ///   campaign only: an entry replaces the shared entry with the same key (its id, or its name when it has no id), the
      ///   rest are added. Use it for heroes with runtime ids ("CharacterObject_2526"), which are per save. Create the folder
      ///   yourself, write the file, then call <see cref="Reload" />. Only character_overrides.json is read there: stations,
      ///   folklore and the prompt files stay shared. Since 2.8.10 (Bhelogan, Discord, 14/11/2026).
      /// </summary>
      public static string CampaignFolder() => Lore.CharacterOverrideLoader.CampaignFolder();
   }
}
