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
   }
}
