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
      ///   Whether a captor may, at their own deliberate choice deep in a Hardcore captive scene, end the
      ///   player's character for real (player request via the "AI Executes" mod, COUNCIL_ACTIONS.md 10.6).
      ///   Read live from the MCM menu; OFF by default, since this is IRREVERSIBLE: game over, or the
      ///   player's clan heir succeeds them. See <see cref="Logic.PlayerExecutionPolicy" /> for the full gate
      ///   the bridge re-checks before it ever calls KillCharacterAction.
      /// </summary>
      public static bool AllowCaptorExecutions => CrSettings.Instance?.AllowCaptorExecutions ?? false;

      /// <summary>
      ///   Whether a lord may promise one of their own clan's fiefs, or a marriage into their house (or
      ///   themselves), as the delivered PRICE of a quest reward (COUNCIL_ACTIONS.md Partie 5, the "Caladog"
      ///   case). Read live from the MCM menu; OFF by default, which keeps the existing "never promise a fief
      ///   or marriage as the price of a task" guardrail fully intact. See
      ///   <see cref="Logic.QuestRewardGatePolicy" /> for the emission/payout gates this only unlocks, never
      ///   bypasses.
      /// </summary>
      public static bool AllowFiefAndMarriageQuestRewards => CrSettings.Instance?.AllowFiefAndMarriageQuestRewards ?? false;

      /// <summary>
      ///   When true, letter couriers are never intercepted: they stay un-auto-targeted AND no bandit band is
      ///   ever rolled against them (see CourierService.ScanForWaylayers). The total opt-out for a player who
      ///   never wants to lose a letter. Read live from the MCM menu; OFF by default, which keeps today's
      ///   vulnerable-courier behaviour (then subject to the distance-independent
      ///   <see cref="Logic.CourierWaylayPolicy.MaxWaylayAttemptsPerCourier" /> cap).
      /// </summary>
      public static bool InvulnerableCouriers => CrSettings.Instance?.InvulnerableCouriers ?? false;

      /// <summary>
      ///   The chat window skin prefab chosen in the MCM menu ("Chat Window Style"), mapped from its display
      ///   label to the prefab name. Read live; falls back to the Dark Leather (Manuscript) book when MCM has
      ///   not loaded, which is the shipped default.
      /// </summary>
      public static string ChatSkinPrefab
      {
         get
         {
            string v = CrSettings.Instance?.ChatSkin?.SelectedValue;

            switch (v)
            {
               case "Origin": return Chat.ChatManager.ClassicSkin;
               default: return Chat.ChatManager.ManuscriptSkin; // Dark Leather (default)
            }
         }
      }

      /// <summary>
      ///   When true, the companion-contentment loop runs (restlessness, audiences, farewell-letter
      ///   departures). When false the whole system is inert. Read live from the MCM menu; on by default.
      /// </summary>
      public static bool CompanionContentment => CrSettings.Instance?.CompanionContentment ?? true;

      /// <summary>
      ///   Master switch for the whole jealousy system (the daily competition loop AND event-triggered
      ///   reactions). Read live from the MCM menu; ON by default, and default-safe (treated as ON) when MCM
      ///   has not loaded. When false, <see cref="Jealousy.JealousyBehavior" /> early-returns from its daily
      ///   tick and its marriage hook, so no jealousy of any kind ever fires.
      /// </summary>
      public static bool EnableJealousy => CrSettings.Instance?.EnableJealousy ?? true;

      /// <summary>
      ///   The player-chosen base folder for prompt/style overrides (MCM), or "" for the default ModuleData/Custom.
      ///   Read by <see cref="Mapping.NarrativeLoader.CustomBaseFolder" /> so a player can keep their edits outside
      ///   the module and survive an update that replaces it (player report v1.33.5).
      /// </summary>
      public static string CustomFolderPath => CrSettings.Instance?.CustomFolderPath?.Trim() ?? "";

      /// <summary>
      ///   Player-chosen zoom factor for the Dark Leather (Manuscript) chat window ("Dark Leather Size (%)"),
      ///   read live from the MCM menu. 1.0 (100%) reproduces the shipped look; clamped to [0.8, 1.4] to match
      ///   the slider's own bounds even if a stale/edited config carries something outside them. Falls back to
      ///   1.0 when MCM has not loaded. No effect on the Origin skin (see <see cref="Chat.ChatManager" />).
      /// </summary>
      public static float DarkLeatherScale
      {
         get
         {
            int pct = CrSettings.Instance?.DarkLeatherZoomPercent ?? 100;
            if (pct < 80) pct = 80;
            if (pct > 140) pct = 140;
            return pct / 100f;
         }
      }

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
      ///   How many unprompted letters NPCs send the player. Read live from the MCM menu; Occasionally (the
      ///   shipped feel) when MCM has not loaded, so nothing changes for a player who never touches it. B1:
      ///   plumbing only for now, nothing yet reads this to steer the mailbox (see LetterPacingRules.BudgetFor).
      /// </summary>
      public static LetterPacing LettersFromNpcs
      {
         get
         {
            string v = CrSettings.Instance?.LetterPacing?.SelectedValue;

            switch (v)
            {
               case "Rarely": return LetterPacing.Rarely;
               case "Often": return LetterPacing.Often;
               default: return LetterPacing.Occasionally;
            }
         }
      }

      /// <summary>
      ///   The PERSISTED DEFAULT for how a NEWLY opened council speaks: <see cref="CouncilVoiceMode.WholeTable" />
      ///   (one call, cohesive, other members voiced from little more than a name) or
      ///   <see cref="CouncilVoiceMode.PerMember" /> (each seated member's own full dossier, at the cost of one
      ///   round-trip per member). Read live from the MCM menu; WholeTable when MCM has not loaded. The
      ///   in-window toggle on an open council is session state and never writes back to this setting.
      /// </summary>
      public static CouncilVoiceMode DefaultCouncilVoiceMode
      {
         get
         {
            string v = CrSettings.Instance?.CouncilVoices?.SelectedValue;

            return v == "One call per member"
               ? CouncilVoiceMode.PerMember
               : CouncilVoiceMode.WholeTable;
         }
      }

      /// <summary>
      ///   How many times, at most, the NPCs may bounce a remark off each other (an NPC reacts to a witness who
      ///   reacts back) before the turn returns to the player. 2 by default; 0 switches the cross-talk off
      ///   entirely, so the main NPC answers and the witnesses fall silent until the player types again. Read
      ///   live from the MCM menu; clamped to [0, 3] to match the slider even on a stale config. Falls back to 2
      ///   when MCM has not loaded. This is the old hard-coded <c>WitnessExchangeCap</c> made adjustable after a
      ///   player report that multi-NPC scenes ran on too long before he could reply.
      /// </summary>
      public static int NpcCrossTalkRounds
      {
         get
         {
            int n = CrSettings.Instance?.NpcCrossTalkRounds ?? 2;
            if (n < 0) n = 0;
            if (n > 3) n = 3;
            return n;
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
      ///   Whether characters may compress their own old memories (folding superseded events into a background
      ///   outline) to keep the prompt from growing without bound over a long campaign. ON by default, and
      ///   deliberately switchable: a third-party mod that injects and curates memories itself (the extension
      ///   API's AddMemory door) would otherwise find CR quietly dropping what it just wrote.
      /// </summary>
      public static bool MemoryCompression => CrSettings.Instance?.MemoryCompression ?? true;

      /// <summary>
      ///   How long (seconds) to wait for an LLM reply before timing out. Read live from the MCM
      ///   menu; falls back to 120 when MCM has not loaded. Drives the per-request timeout so a slow
      ///   local/reasoning model on a long prompt no longer hits the opaque 100s HttpClient default.
      /// </summary>
      public static int LlmResponseTimeoutSeconds => CrSettings.Instance?.LlmTimeoutSeconds ?? 120;

      /// <summary>
      ///   The model id for consensual-intimate scenes (the "NSFW Consensual model" MCM field). Its stored value
      ///   lives under the legacy <c>CrSettings.NsfwModel</c> persistence identity (see that property's comment), so
      ///   an existing config that set the old single "NSFW model" is preserved as the consensual model after an
      ///   upgrade. Read live from the MCM menu. Blank (the default) means the roleplay model serves these scenes.
      /// </summary>
      public static string ConsensualModel => CrSettings.Instance?.NsfwModel?.Trim() ?? "";

      /// <summary>
      ///   The model id for CNC / captive scenes. Its own MCM field when set; otherwise it falls back to
      ///   <see cref="ConsensualModel" /> (and thus to the roleplay model when that too is blank), so a config that
      ///   set only the consensual (old NSFW) model keeps serving CNC scenes exactly as before.
      /// </summary>
      public static string CncModel
      {
         get
         {
            string v = CrSettings.Instance?.CncModel?.Trim();

            return string.IsNullOrWhiteSpace(v)
               ? ConsensualModel
               : v;
         }
      }

      /// <summary>
      ///   The optional small/cheap model that reads structured actions out of the prose in the Prose + Interpreter
      ///   composition mode. Blank (the default) means the scene's own model does the interpretation. Read live from
      ///   the MCM menu.
      /// </summary>
      public static string ActionInterpreterModel => CrSettings.Instance?.ActionInterpreterModel?.Trim() ?? "";

      /// <summary>
      ///   How a chat reply is composed: <see cref="Logic.ChatCompositionMode.Integrated" /> (one call, today's
      ///   behaviour) or <see cref="Logic.ChatCompositionMode.ProseInterpret" /> (prose then a separate action-
      ///   interpreter call). Read live from the MCM menu; defaults to Integrated on anything unexpected.
      /// </summary>
      public static ChatCompositionMode ChatCompositionMode =>
         ChatCompositionModePolicy.Parse(CrSettings.Instance?.ChatCompositionModeSetting?.SelectedValue);

      /// <summary>
      ///   In <see cref="Logic.ChatCompositionMode.ProseInterpret" /> mode, WHEN the action interpreter's tags are
      ///   applied: <see cref="Logic.ActionInterpreterTiming.Synchronous" /> (in-turn, recommended) or
      ///   <see cref="Logic.ActionInterpreterTiming.Asynchronous" /> (reply shows first, tags apply a moment later).
      ///   Read live from the MCM menu; defaults to Synchronous on anything unexpected. No effect in Integrated
      ///   mode (there is only one call to time).
      /// </summary>
      public static ActionInterpreterTiming ActionInterpreterTiming =>
         ActionInterpreterTimingPolicy.Parse(CrSettings.Instance?.ActionInterpreterTimingSetting?.SelectedValue);

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

      /// <summary>
      ///   When true, the chat shows a sober report hint after the immersive notice whenever the AI attempts an
      ///   [ACTION] type the mod does not support (an invented verb). The attempt is logged to
      ///   CalradiaRemembers_unknown_actions.log regardless of this toggle; this only gates the in-chat hint.
      ///   Read live from the MCM menu; off by default. See <see cref="Logic.UnknownActionNotice" />.
      /// </summary>
      public static bool ReportUnsupportedActions => CrSettings.Instance?.ReportUnsupportedActions ?? false;
   }
}