// Code written by Gabriel Mailhot, 01/07/2026.

#region

using CalradiaRemembers.Logic;
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
      ///   When true, the companion-contentment loop runs (restlessness, audiences, farewell-letter
      ///   departures). When false the whole system is inert. Read live from the MCM menu; on by default.
      /// </summary>
      public static bool CompanionContentment => CrSettings.Instance?.CompanionContentment ?? true;

      /// <summary>
      ///   How readily lords ride out to have a word with the player on the road (player request, Fakade). Read
      ///   live from the MCM menu; Occasionally (the shipped feel) when MCM has not loaded, so nothing changes for
      ///   a player who never touches it.
      /// </summary>
      public static InterceptionPacing LordsApproaching
      {
         get
         {
            string v = CrSettings.Instance?.InterceptionPacing?.SelectedValue;

            switch (v)
            {
               case "Never": return InterceptionPacing.Never;
               case "Rarely": return InterceptionPacing.Rarely;
               case "Often": return InterceptionPacing.Often;
               default: return InterceptionPacing.Occasionally;
            }
         }
      }

      /// <summary>
      ///   The OpenRouter providers the player has pinned (comma-separated slugs), or blank for OpenRouter's own
      ///   routing. Read live from the MCM menu. Lets a player avoid providers that moderate their own output
      ///   and cut a reply mid-sentence.
      /// </summary>
      public static string OpenRouterProviderPin => CrSettings.Instance?.ProviderPin?.Trim() ?? "";

      /// <summary>
      ///   The OpenRouter providers pinned for NSFW mode specifically, or blank to reuse
      ///   <see cref="OpenRouterProviderPin" />. Read live from the MCM menu. Lets a player route ordinary
      ///   roleplay to the cheapest provider while pinning a specific uncensored provider for explicit scenes.
      /// </summary>
      public static string OpenRouterNsfwProviderPin => CrSettings.Instance?.NsfwProviderPin?.Trim() ?? "";

      /// <summary>
      ///   Whether a pinned OpenRouter request may still fall back to providers outside the pin. Off by default,
      ///   so pinning actually forces. Read live from the MCM menu.
      /// </summary>
      public static bool AllowOpenRouterProviderFallbacks => CrSettings.Instance?.AllowProviderFallbacks ?? false;

      /// <summary>
      ///   How readily companions recover from a grievance: the player's own taste. Read live from the MCM menu;
      ///   Balanced when MCM has not loaded. Affects the recovery half of the loop only, never what a slight
      ///   costs, and no setting lets mere idleness carry a companion out of your service.
      /// </summary>
      public static ContentmentPacing CompanionPacing
      {
         get
         {
            string v = CrSettings.Instance?.ContentmentPacing?.SelectedValue;

            switch (v)
            {
               case "Forgiving": return ContentmentPacing.Forgiving;
               case "Exacting": return ContentmentPacing.Exacting;
               default: return ContentmentPacing.Balanced;
            }
         }
      }

      /// <summary>
      ///   How the OpenAI-Compatible endpoint shapes its sampling parameters. Read live from the MCM menu;
      ///   falls back to <c>Auto</c> (detect reasoning models from the id) when MCM has not loaded. Lets a
      ///   player force the classic or reasoning contract for a strict endpoint (e.g. api.openai.com on a
      ///   gpt-5 model, which rejects the classic <c>max_tokens</c>).
      /// </summary>
      public static LlmCompatibilityMode CustomParameterCompatibility
      {
         get
         {
            string v = CrSettings.Instance?.CustomParameterCompatibility?.SelectedValue;

            switch (v)
            {
               case "Standard": return LlmCompatibilityMode.Standard;
               case "Reasoning": return LlmCompatibilityMode.Reasoning;
               default: return LlmCompatibilityMode.Auto;
            }
         }
      }

      /// <summary>
      ///   When true, the system prompt is trimmed to fit a small / short-context model (the "Compact prompt for
      ///   small models" option). Read live from the MCM menu; OFF by default (full prompt for capable models).
      /// </summary>
      public static bool LeanPrompt => CrSettings.Instance?.LeanPrompt ?? false;

      /// <summary>
      ///   How long (seconds) to wait for an LLM reply before timing out. Read live from the MCM
      ///   menu; falls back to 120 when MCM has not loaded. Drives the per-request timeout so a slow
      ///   local/reasoning model on a long prompt no longer hits the opaque 100s HttpClient default.
      /// </summary>
      public static int LlmResponseTimeoutSeconds => CrSettings.Instance?.LlmTimeoutSeconds ?? 120;

      /// <summary>
      ///   The optional NSFW model id: used automatically in explicit scenes and via the in-chat mode button.
      ///   Blank (the default) means the roleplay model serves everything. Read live from the MCM menu.
      /// </summary>
      public static string NsfwModel => CrSettings.Instance?.NsfwModel ?? "";

      /// <summary>
      ///   The reasoning-effort keyword for NSFW mode, read live from the MCM menu. The default
      ///   sentinel ("Same as roleplay") makes NSFW reuse <see cref="ReasoningEffort" />; players
      ///   typically set it LOWER than roleplay (fewer moralizing refusals, faster scene beats).
      /// </summary>
      public static string NsfwReasoningEffort
      {
         get
         {
            string v = CrSettings.Instance?.NsfwReasoningEffort?.SelectedValue;

            return string.IsNullOrWhiteSpace(v)
               ? ModelModePolicy.SameAsRoleplay
               : v;
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
      ///   Explicit reply language name (e.g. "Spanish"), or blank for the auto-detect mirror. Read live from
      ///   the MCM menu. A player report (2026-07-21, Spanish + DeepSeek V3) showed the mirror's abstract rule
      ///   goes unfollowed by a weaker model, or a language absent from its worked examples, when the rest of
      ///   an English prompt outweighs it, so naming the language explicitly removes the guesswork.
      /// </summary>
      public static string ReplyLanguage => CrSettings.Instance?.ReplyLanguage?.Trim() ?? "";

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
      ///   When true, the full system prompt and NPC response are logged on every LLM
      ///   exchange. Read live from the MCM menu; off by default.
      /// </summary>
      public static bool VerboseLogging => CrSettings.Instance?.VerboseLogging ?? false;
   }
}