// Code written by Gabriel Mailhot, 22/06/2026.

#region

using CalradiaRemembers.Settings;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   Mod-wide settings façade. Sprint 16 backs <see cref="AdultContentLevel" /> with
   ///   the MCM menu (<see cref="CrSettings" />) so callers are unchanged; the sound
   ///   paths remain hard-coded fallbacks.
   /// </summary>
   public static class ModSettings
   {
      /// <summary>
      ///   How much of the romantic profile is injected into the LLM system prompt.
      ///   Read live from the MCM menu. Falls back to <c>Mature</c> (romance, nothing
      ///   explicit) when MCM has not loaded — the release-safe default, never Hardcore.
      /// </summary>
      public static AdultContentLevel AdultContentLevel
      {
         get
         {
            CrSettings s = CrSettings.Instance;

            if (s?.AdultContentLevel == null)
               return AdultContentLevel.Mature;

            switch (s.AdultContentLevel.SelectedValue)
            {
               case "Off": return AdultContentLevel.Off;
               case "Explicit": return AdultContentLevel.Explicit;
               case "Hardcore": return AdultContentLevel.Hardcore;
               default: return AdultContentLevel.Mature;
            }
         }
      }

      /// <summary>
      ///   The model's reasoning-effort keyword (Default/Off/Minimal/Low/Medium/High), read
      ///   live from the MCM menu. Lowering or disabling reasoning cuts moralizing refusals on
      ///   adult/CNC content. Falls back to <c>Low</c> (the default) when MCM has not loaded.
      /// </summary>
      public static string ReasoningEffort
      {
         get
         {
            string v = CrSettings.Instance?.ReasoningEffort?.SelectedValue;

            return string.IsNullOrWhiteSpace(v)
               ? "Low"
               : v;
         }
      }

      /// <summary>
      ///   Sound event path played when a negative relation change occurs.
      ///   Example: "event:/ui/notification/relation_decrease"
      /// </summary>
      /// <summary>
      ///   Sound event path played when a negative relation change occurs.
      ///   Vanilla fallback — replace with "event:/CR_Negative" once the custom
      ///   soundbank strings-loading issue is resolved in FMOD Studio.
      /// </summary>
      public static string RelationDecreaseSound { get; set; } = "event:/ui/persuasion/critical_fail";

      /// <summary>
      ///   Sound event path played when a positive relation change occurs.
      ///   Vanilla fallback — replace with "event:/CR_Positive" once the custom
      ///   soundbank strings-loading issue is resolved in FMOD Studio.
      /// </summary>
      public static string RelationIncreaseSound { get; set; } = "event:/ui/persuasion/success";

      /// <summary>
      ///   How readily NPCs grow to like the player through conversation. Read live from
      ///   the MCM menu. Falls back to <c>Normal</c> (the balanced default) when MCM has
      ///   not loaded. Only loosens positive regard — negatives are unaffected.
      /// </summary>
      public static RelationWarmth RelationWarmth
      {
         get
         {
            CrSettings s = CrSettings.Instance;

            if (s?.RelationshipWarmth == null) return RelationWarmth.Normal;

            switch (s.RelationshipWarmth.SelectedValue)
            {
               // Current labels (experience-oriented — tester feedback: this is a
               // philosophy of interaction, not a difficulty).
               case "Warm": return RelationWarmth.Easier;
               case "Storyteller": return RelationWarmth.VeryEasy;
               // Pre-rename labels — MCM may have persisted these for existing users.
               case "Easier": return RelationWarmth.Easier;
               case "Very Easy": return RelationWarmth.VeryEasy;
               default: return RelationWarmth.Normal;
            }
         }
      }

      /// <summary>
      ///   When true, hand-authored personalities from <c>character_overrides.json</c> replace the
      ///   vanilla traits of the NPCs they name. Read live from the MCM menu; off by default.
      /// </summary>
      public static bool UseCustomTraits => CrSettings.Instance?.UseCustomTraits ?? false;

      /// <summary>
      ///   When true, the companion-contentment loop runs (restlessness, audiences, farewell-letter
      ///   departures). When false the whole system is inert. Read live from the MCM menu; on by default.
      /// </summary>
      public static bool CompanionContentment => CrSettings.Instance?.CompanionContentment ?? true;

      /// <summary>
      ///   When true, the full system prompt and NPC response are logged on every LLM
      ///   exchange. Read live from the MCM menu; off by default.
      /// </summary>
      public static bool VerboseLogging => CrSettings.Instance?.VerboseLogging ?? false;
   }
}