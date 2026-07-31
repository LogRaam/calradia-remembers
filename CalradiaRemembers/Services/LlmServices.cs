// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Net.Http;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Settings;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Services
{
   /// <summary>
   ///   Centralizes the construction and lifetime of the LLM stack
   ///   (HTTP client, OpenRouter client, parser, prompt builder, chat service).
   ///   Built once at mod startup; queried by anyone needing LLM access.
   ///   Sprint 8.2: the <see cref="PromptBuilder" /> is built lazily on first
   ///   use so it can read <see cref="ModSettings.AdultContentLevel" /> and
   ///   <see cref="Hero.MainHero" /> after the campaign is loaded. This avoids
   ///   the NullReferenceException risk of reading Hero.MainHero at submodule
   ///   load time.
   ///   If the OPENROUTER_API_KEY environment variable is missing, <see cref="IsAvailable" />
   ///   returns false and <see cref="ChatService" /> remains null. The mod must
   ///   gracefully degrade in that case (skip LLM features, keep vanilla behavior).
   /// </summary>
   public sealed class LlmServices : IDisposable
   {
      internal const string DefaultModel = "x-ai/grok-4.20"; // OpenRouter default
      private const string NanoGptApiKeyEnvVar = "NANOGPT_API_KEY";
      private const string NanoGptBaseUrl = "https://nano-gpt.com/api/v1";

      private const string OpenRouterApiKeyEnvVar = "OPENROUTER_API_KEY";

      private const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
      private const string ProviderCustom = "OpenAI-Compatible";

      private const string ProviderNanoGpt = "NanoGPT";

      // Provider ids — must match the values in CrSettings.Provider dropdown.
      private const string ProviderOpenRouter = "OpenRouter";
      private AdultContentLevel _builtAdultLevel;
      private RelationWarmth _builtWarmth;
      private string _builtReplyLanguage;
      private NpcChatService _chatService;

      private HttpClient _httpClient;

      /// <summary>
      ///   Test-only override for the player's gender used when building the prompt.
      ///   Set by the cr.crc_scene console command to exercise the female/male captive
      ///   path on a character of the other gender. Null = use the real Hero.MainHero.
      ///   Cleared by <see cref="ClearTestGenderOverride" /> when the chat closes.
      /// </summary>
      public static bool? TestPlayerGenderOverride { get; set; }

      /// <summary>
      ///   Lazily-constructed chat service. The PromptBuilder and action vocabulary
      ///   inside are built on first access, and rebuilt whenever the adult-content
      ///   level, relationship-warmth, or reply-language setting changes, so options
      ///   edits apply without a restart.
      /// </summary>
      public NpcChatService ChatService
      {
         get
         {
            if (!IsAvailable) return null;
            AdultContentLevel current = ModSettings.AdultContentLevel;
            RelationWarmth warmth = ModSettings.RelationWarmth;
            string replyLanguage = ModSettings.ReplyLanguage;

            if (_chatService != null && _builtAdultLevel == current && _builtWarmth == warmth
                                      && _builtReplyLanguage == replyLanguage)
               return _chatService;
            _chatService = BuildChatService();
            _builtAdultLevel = current;
            _builtWarmth = warmth;
            _builtReplyLanguage = replyLanguage;

            return _chatService;
         }
      }

      /// <summary>
      ///   Live availability — computed each access so a key entered in-game takes
      ///   effect on the next turn. Defined as "nothing to complain about": the
      ///   conditions live in <see cref="DescribeUnavailability" /> ONLY, so the
      ///   gate and the user-facing explanation can never drift apart.
      /// </summary>
      public bool IsAvailable => DescribeUnavailability() == null;

      public ILlmClient LlmClient { get; private set; }

      /// <summary>The model currently in effect — read live from the MCM menu.</summary>
      public string ModelId => ResolveModel();

      /// <summary>
      ///   Which of the two configured models requests currently resolve to (roleplay or NSFW).
      ///   DELIBERATE LIFECYCLE: the mode is SET at conversation activation and NOT reset on chat close.
      ///   FinalizeCaptiveSceneMemory launches an async LLM summary of the explicit transcript via Task.Run
      ///   that executes after teardown; it must still resolve the NSFW model (a prudish roleplay model could
      ///   refuse and lose the memory). Background calls between conversations therefore inherit the last
      ///   mode, which is the safe direction; an ordinary conversation open sets Roleplay, so it self-corrects.
      /// </summary>
      public static ModelMode ActiveModelMode { get; set; } = ModelMode.Roleplay;

      /// <summary>
      ///   The model id the current mode resolves to, read live (same value every request sees via
      ///   <c>ModelProvider</c>). Exposed for the in-chat mode button log line and the cr.model command.
      /// </summary>
      public static string ActiveModelId => ResolveModel();

      /// <summary>
      ///   Clears any test gender override and forces a PromptBuilder rebuild so the
      ///   next conversation uses the real player gender. Called when the chat closes.
      /// </summary>
      public static void ClearTestGenderOverride()
      {
         if (TestPlayerGenderOverride == null) return;
         TestPlayerGenderOverride = null;
         NpcMemoryCampaignBehavior.Instance?.LlmServices?.Invalidate();
      }

      /// <summary>
      ///   User-facing explanation of why the LLM cannot serve a conversation right
      ///   now — provider-aware, with the exact next step. Null when fully configured.
      ///   Single source of truth for availability (<see cref="IsAvailable" /> derives
      ///   from it): stack built, non-empty API key (MCM menu or env var), and a model
      ///   id (only NanoGPT can be model-less — it has no default).
      /// </summary>
      public string DescribeUnavailability()
      {
         if (LlmClient == null)
            return "(The AI stack failed to initialize — check CalradiaRemembers.log.)";

         string provider = ResolveProvider();

         if (provider == ProviderCustom)
         {
            if (string.IsNullOrWhiteSpace(ResolveBaseUrl()))
               return "(No endpoint URL is set. Open Mods → Mod Options → Calradia Remembers, " +
                      "paste your server URL (e.g. http://localhost:1234/v1), then return here — it applies immediately.)";
            if (string.IsNullOrWhiteSpace(ResolveModel()))
               return "(OpenAI-Compatible is selected but no model id is set. Open Mods → Mod Options → Calradia Remembers " +
                      "and paste the model id your server expects — it applies immediately.)";

            return null;
         }

         if (string.IsNullOrWhiteSpace(ResolveApiKey()))
            return $"(No {provider} API key is set. Open Mods → Mod Options → Calradia Remembers, " +
                   $"paste your {provider} API key, then return here — it applies immediately, no restart needed.)";

         if (string.IsNullOrWhiteSpace(ResolveModel()))
            return "(NanoGPT is selected but no model id is set. Open Mods → Mod Options → Calradia Remembers " +
                   "and paste a model id from nano-gpt.com/models — it applies immediately.)";

         return null;
      }

      public void Dispose()
      {
         _httpClient?.Dispose();
         _httpClient = null;
      }

      /// <summary>
      ///   Initializes the LLM stack. Call once at mod startup.
      ///   Returns true on success, false if the API key is missing.
      /// </summary>
      public bool Initialize()
      {
         try
         {
            // A high ceiling so HttpClient's own 100s default never pre-empts the real, configurable
            // per-request timeout the OpenRouterClient enforces via a linked token (ModSettings.LlmResponseTimeoutSeconds).
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

            // The client reads key/model/base-URL live on every request, so a provider,
            // key or model picked in the MCM menu applies immediately — no restart, no
            // save reload. The cache_control extension is disabled for providers that do
            // not accept the content-array system message (e.g. NanoGPT).
            var config = new OpenRouterConfig {
               ApiKeyProvider = ResolveApiKey,
               ModelProvider = ResolveModel,
               BaseUrlProvider = ResolveBaseUrl,
               UseSystemPromptCachingProvider = ResolveUseSystemPromptCaching,
               ReasoningProvider = ResolveReasoning,
               TimeoutSecondsProvider = () => ModSettings.LlmResponseTimeoutSeconds,
               ParameterOptionsProvider = ResolveParameterOptions,
               ProviderSlugsProvider = ResolveProviderPin,
               AllowProviderFallbacksProvider = () => ModSettings.AllowOpenRouterProviderFallbacks
            };
            LlmClient = new OpenRouterClient(_httpClient, config);

            if (ResolveProvider() != ProviderCustom && string.IsNullOrWhiteSpace(ResolveApiKey()))
               ModLog.Info($"LLM stack built, but no {ResolveProvider()} API key is set yet. " +
                           "Add it in Mods → Mod Options → Calradia Remembers (applies immediately).");
            else
               ModLog.Info($"LLM stack initialized — provider {ResolveProvider()}, model {ResolveModel()}.");

            return IsAvailable;
         }
         catch (Exception ex)
         {
            ModLog.Error($"Failed to initialize LLM stack: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Forces the next access to <see cref="ChatService" /> to rebuild the
      ///   <see cref="PromptBuilder" /> with the latest file-loaded content.
      ///   Call after reloading narrative files at session launch.
      /// </summary>
      public void Invalidate() => _chatService = null;

      #region private

      /// <summary>
      ///   Builds the chat service with a PromptBuilder configured against
      ///   current mod settings and main hero. Called lazily so the campaign
      ///   is guaranteed to be loaded by this point.
      /// </summary>
      // Game actions the LLM may emit. Defined here rather than loaded from JSON
      // because adding a new action always requires code changes in the parser and
      // bridge anyway — external configurability adds no practical value. The
      // change_relation wording varies with the Relationship Warmth setting so the
      // player's chosen pacing is reflected in what the NPC is told to feel.
      private static IReadOnlyList<GameActionDefinition> BuildActionVocabulary(RelationWarmth warmth) =>
         new List<GameActionDefinition> {
            new() {
               Type = "change_relation",
               Description = ChangeRelationDescription(warmth),
               Parameters = new List<string> {"delta"}
            },
            new() {
               Type = "end_conversation",
               Description = "End this conversation now. Emit when you, in character, would genuinely refuse to continue: " +
                             "after a grave insult, a crude or unwelcome advance you will not tolerate, " +
                             "an advance you already refused that the player presses again, repeated disrespect, an open threat — " +
                             "or when the exchange has reached its natural close and there is nothing more you wish to say. " +
                             "CRUCIAL — your words and this action must AGREE: if your dialogue this turn dismisses the player or " +
                             "declares the talk over ('I have duties that demand my attention', 'I will hear no more of this today', " +
                             "'we are done here', a firm farewell, or sending them on their way), you MUST emit this action so the " +
                             "conversation actually ends. Do not say goodbye or refuse to hear more while leaving the conversation open. " +
                             "When you end because the player wronged or disrespected you, you MUST also emit change_relation with a " +
                             "negative delta matching the offense. Do not end over a single clumsy remark or mere awkwardness; reserve " +
                             "it for a real breach, a clear dismissal, or a true farewell.",
               Parameters = new List<string>()
            },
            new() {
               Type = "witness_leaves",
               Description = "One of the PLAYER'S OWN companions leaves the conversation. Emit ONLY when the player " +
                             "explicitly asks one of their companions to step away or leave the two of you alone — " +
                             "never on your own initiative. This applies only to witnesses marked as the player's " +
                             "companion; your own people do not answer to the player (a private audience is the way " +
                             "to clear the room). Set 'name' to the companion's name exactly as listed under " +
                             "WITNESSES PRESENT, and acknowledge the departure naturally in [DIALOGUE].",
               Parameters = new List<string> {"name"}
            },
            new() {
               Type = "give_gold",
               Description = "Transfer gold from you to the player (payment, reward, gift, bribe). " +
                             "'amount' is a whole number of denars. " +
                             "The amount must reflect your actual willingness: do not give what you would not give. " +
                             "DIRECTION CHECK: use this ONLY when YOU hand YOUR OWN coin to the player. When the " +
                             "PLAYER is the one paying, including in a stage direction such as *hands you 1000 " +
                             "denars* or *he gives her the coin* (the player writing about themselves in the third " +
                             "person), that is take_gold, never give_gold.",
               Parameters = new List<string> {"amount"}
            },
            new() {
               Type = "take_gold",
               Description = "Transfer gold from the player to you (tribute, fee, debt repaid). " +
                             "'amount' is a whole number of denars. " +
                             "Only emit if the player has agreed or you have the authority to demand it. " +
                             "This includes when the player mimes the payment in a stage direction (*hands you " +
                             "500 denars*, *he gives her the money*): the player is the one paying, so it is " +
                             "take_gold, not give_gold.",
               Parameters = new List<string> {"amount"}
            }
         };

      /// <summary>
      ///   Assembles the change_relation instruction. The opening, the negative scale,
      ///   and the closing are constant at every warmth level — only the guidance on
      ///   WHEN a positive gain is warranted changes, so insults stay equally costly
      ///   while the player's chosen warmth controls how readily goodwill is earned.
      /// </summary>
      private static string ChangeRelationDescription(RelationWarmth warmth)
      {
         const string opening =
            "Reflect a genuine shift in how you regard the player — this is the ONLY way your relationship with them changes. ";

         string positive;
         switch (warmth)
         {
            case RelationWarmth.Easier:
               positive =
                  "Emit +1 whenever the player genuinely pleases you this exchange — a well-turned compliment, shared humor, " +
                  "an engaging story, real sympathy or support, a pledge of loyalty or service you welcome, sincere praise of " +
                  "your deeds or leadership, or simply good company. Good company earns goodwill here: do not " +
                  "withhold a small gain merely because no grand deed was done. A larger positive is reserved for a truly " +
                  "significant, self-evident moment such as physical intimacy. ";

               break;

            case RelationWarmth.VeryEasy:
               positive =
                  "Emit +1 — occasionally +2 — for almost any sincerely positive exchange: pleasant talk, a kind word, a shared " +
                  "laugh, a pledge of support, praise of your deeds or your leadership, warm words after a shared victory, " +
                  "a moment of real connection. You warm to people readily and give them the benefit of the doubt; you need " +
                  "no grand deed to feel goodwill. When in doubt whether an exchange earned the +1, emit it rather than " +
                  "withhold it. Reserve larger gains for a truly significant, self-evident moment such as physical intimacy. ";

               break;

            default: // Normal — the balanced baseline
               positive =
                  "Emit when the player earns it through something real — never for idle flattery or empty promises. A +1 is right " +
                  "when this exchange genuinely warms you: a service rendered or a promise kept, a real step forward between you " +
                  "(courtship advancing, trust shown, respect earned through deeds), sincere support in a moment that mattered, or a " +
                  "connection that rings true. A larger positive is reserved for a truly significant, self-evident moment — completing " +
                  "a real task you set them, or physical intimacy. Trust is slow to grow and quick to break — but do not stay frozen at " +
                  "neutral when the player has actually earned your regard; a genuinely earned moment should move it. ";

               break;
         }

         const string negative =
            "For a wrong done to you, make 'delta' negative and scale it to the gravity — " +
            "a cold rebuff (-2), a crude or unwelcome advance (-5), a pointed insult (-5), " +
            "a lewd proposition made in front of others (-8), the betrayal of a real agreement (-15); " +
            "if you are the player's captive and they treat you with cruelty, your regard may collapse entirely. " +
            "Do NOT stay neutral when the player is disrespectful, vulgar, or presses an advance you have already refused — your regard must fall. ";

         const string closing =
            "The game enforces pacing and limits, so state what you feel honestly and let it decide what lands.";

         return opening + positive + negative + closing;
      }

      /// <summary>
      ///   Resolves the active provider's API key. A key entered in the MCM menu wins;
      ///   the provider's environment variable (<c>OPENROUTER_API_KEY</c> /
      ///   <c>NANOGPT_API_KEY</c>) remains a fallback for developer setups and power
      ///   users. Whitespace is treated as unset.
      /// </summary>
      private static string ResolveApiKey()
      {
         CrSettings s = CrSettings.Instance;
         string provider = ResolveProvider();

         if (provider == ProviderCustom)
         {
            string customKey = s?.CustomApiKey?.Trim();

            return string.IsNullOrWhiteSpace(customKey)
               ? string.Empty
               : customKey;
         }

         bool nano = provider == ProviderNanoGpt;
         string mcmKey = nano
            ? s?.NanoGptApiKey
            : s?.ApiKey;

         if (!string.IsNullOrWhiteSpace(mcmKey)) return mcmKey.Trim();

         return Environment.GetEnvironmentVariable(nano
            ? NanoGptApiKeyEnvVar
            : OpenRouterApiKeyEnvVar);
      }

      /// <summary>
      ///   Resolves the chat-completion parameter shape for the active request. The aggregators (OpenRouter,
      ///   NanoGPT) normalize parameter names and accept temperature, so they always get the classic shape;
      ///   only the direct OpenAI-Compatible endpoint applies the player's compatibility policy — letting a
      ///   gpt-5/o-series model reached at api.openai.com receive 'max_completion_tokens' with no temperature.
      /// </summary>
      private static ChatParameterOptions ResolveParameterOptions()
      {
         if (ResolveProvider() != ProviderCustom) return ChatParameterOptions.Standard;

         LlmWireParameters p = LlmParameterPolicy.Resolve(ResolveModel(), ModSettings.CustomParameterCompatibility);

         return new ChatParameterOptions(p.UseMaxCompletionTokens, p.IncludeTemperature);
      }

      /// <summary>
      ///   The OpenRouter providers the player pinned, and ONLY when OpenRouter is the active provider: the
      ///   field is meaningless to NanoGPT or a local OpenAI-compatible server, so they must never receive it.
      ///   Mode-aware like the model id and the reasoning effort: NSFW mode can pin a different (typically
      ///   uncensored) provider than roleplay via <see cref="ModelModePolicy.ResolveProviderPin" />. Read live
      ///   per request, so flipping the mode mid-conversation just works.
      /// </summary>
      private static string ResolveProviderPin()
         => ResolveProvider() == ProviderOpenRouter
            ? ModelModePolicy.ResolveProviderPin(ActiveModelMode, ModSettings.OpenRouterProviderPin, ModSettings.OpenRouterNsfwProviderPin)
            : "";

      /// <summary>Resolves the active provider's OpenAI-compatible base URL. Read live per request.</summary>
      private static string ResolveBaseUrl()
      {
         string provider = ResolveProvider();

         if (provider == ProviderNanoGpt) return NanoGptBaseUrl;
         if (provider == ProviderCustom) return CrSettings.Instance?.CustomEndpointUrl?.Trim() ?? "";

         return OpenRouterBaseUrl;
      }

      /// <summary>
      ///   Resolves the model id: the shared "Model (roleplay)" MCM field serves every provider
      ///   (ids are pasted in the active provider's own namespace). OpenRouter falls back to the
      ///   built-in default; the other providers treat a blank field or the untouched OpenRouter
      ///   default as unconfigured and resolve blank, so the provider-aware in-chat guidance fires
      ///   instead of a cryptic API error. When <see cref="ActiveModelMode" /> is NSFW and the player
      ///   configured an NSFW model, that model overrides the roleplay one (any provider). Read live
      ///   on every request via <c>ModelProvider</c>, so flipping the mode mid-conversation just works.
      /// </summary>
      private static string ResolveModel()
      {
         CrSettings s = CrSettings.Instance;
         string provider = ResolveProvider();
         string mcmModel = s?.Model?.Trim() ?? string.Empty;

         string roleplayModel;

         if (provider == ProviderCustom || provider == ProviderNanoGpt)
         {
            // The untouched OpenRouter default means "never configured for this provider", not a
            // choice: model namespaces differ, and a wrong id is worse than an explicit blank.
            roleplayModel = mcmModel == DefaultModel ? string.Empty : mcmModel;
         }
         else
         {
            roleplayModel = string.IsNullOrWhiteSpace(mcmModel)
               ? DefaultModel
               : mcmModel;
         }

         return ModelModePolicy.ResolveModelId(ActiveModelMode, roleplayModel, ModSettings.NsfwModel);
      }

      /// <summary>
      ///   The selected provider id from the MCM menu. Falls back to OpenRouter when
      ///   MCM has not loaded. Read live so a provider change applies immediately.
      /// </summary>
      private static string ResolveProvider()
      {
         string sel = CrSettings.Instance?.Provider?.SelectedValue;

         if (sel == ProviderNanoGpt) return ProviderNanoGpt;
         if (sel == ProviderCustom) return ProviderCustom;

         return ProviderOpenRouter;
      }

      /// <summary>
      ///   Resolves the reasoning-effort keyword for the request. The <c>reasoning</c> parameter
      ///   is OpenRouter-specific, so it is suppressed (null) for NanoGPT. Mode-aware like the
      ///   model id: NSFW mode can run a lower effort than roleplay (fewer moralizing refusals,
      ///   faster scene beats) via its own MCM dropdown. Read live per request.
      /// </summary>
      private static string ResolveReasoning() =>
         ResolveProvider() == ProviderOpenRouter
            ? ModelModePolicy.ResolveReasoningEffort(ActiveModelMode, ModSettings.ReasoningEffort, ModSettings.NsfwReasoningEffort)
            : null;

      /// <summary>
      ///   Whether to send the cacheable content-array system message. OpenRouter honors
      ///   the <c>cache_control</c> breakpoint; other providers route to arbitrary backends
      ///   where the array form is untested, so we send a plain string to stay portable.
      /// </summary>
      private static bool ResolveUseSystemPromptCaching() =>
         ResolveProvider() == ProviderOpenRouter;

      /// <summary>
      ///   TUNE. Adult-prompt audit M13: the mod's anti-repetition rested entirely on prompt text, which is
      ///   exactly what a weaker model ignores, so intimate and captive scenes reused the same gestures and
      ///   phrases turn after turn. A sampling penalty is the only lever that also reaches those models.
      ///   <para>
      ///     Deliberately modest. The penalty is blunt: it discourages every token already used, and in
      ///     roleplay the tokens that recur most are the character and place NAMES. Raising this buys variety
      ///     at the cost of the model drifting off its own cast, so it wants play-testing before it moves.
      ///     Set to 0 to send nothing at all and restore the exact previous payload.
      ///   </para>
      /// </summary>
      // AUDIT-CAPTIVE-STYLE T5 (2026-07-24): raised 0.3 -> 0.5 to push harder against the model reusing its
      // own recent tokens within a scene (completes AUDIT-PROMPTS-ADULTES P5.3). Not the temperature (raising
      // that risks role incoherence). NOTE: a reasoning model (gpt-5*/o*) drops temperature AND penalty
      // together (LlmParameterPolicy), so this only bites on ordinary sampled models.
      private const float ScenePresencePenalty = 0.5f;

      private NpcChatService BuildChatService()
      {
         var promptBuilder = new PromptBuilder {
            AdultLevel = ModSettings.AdultContentLevel,
            PlayerIsFemale = TestPlayerGenderOverride ?? Hero.MainHero?.IsFemale ?? false,
            // Name and clan are injected from the live Hero so NPCs can address the player by
            // their actual in-game name and reference their lineage, regardless of what the user
            // has written (or not written) in player_description.txt.
            PlayerName = Hero.MainHero?.Name?.ToString() ?? "",
            PlayerClanName = Hero.MainHero?.Clan?.Name?.ToString() ?? "",
            PlayerFactionName = Hero.MainHero?.MapFaction?.Name?.ToString() ?? "",
            WorldDescription = NarrativeLoader.WorldDescription,
            PlayerDescription = NarrativeLoader.PlayerDescription,
            BehaviorGuidelinesOverride = NarrativeLoader.BehaviorGuidelines,
            PostHistoryInstructions = NarrativeLoader.PostHistoryInstructions,
            ReplyLanguage = ModSettings.ReplyLanguage,
            ActionVocabulary = BuildActionVocabulary(ModSettings.RelationWarmth),
            // The intimacy consent bar follows the same Relationship Pacing dial as every other gate; the
            // SDK owns the bar, the mod owns how far the player's dial lowers it (audit M3).
            IntimacyThresholdRelief = IntimacyPacingRules.Relief(ModSettings.RelationWarmth),
            // The NPC's standing is the live game relation (synced each turn),
            // moved only through the gated change_relation action — so the LLM
            // does not write a parallel reputation score.
            EnableReputationBlock = false,
            // [MEMORY] data is never consumed by the game — omitting it saves output
            // tokens and latency on every reply, including witness beats and captive scenes.
            EnableMemoryBlock = false,
            // The mod can verify deeds (QuestDeedTracker) and pay rewards, so the
            // NPC is allowed to offer and resolve informal quests.
            EnableQuests = true
         };

         ModLog.Info($"PromptBuilder built (AdultLevel={ModSettings.AdultContentLevel}, " +
                     $"Warmth={ModSettings.RelationWarmth}, PlayerIsFemale={promptBuilder.PlayerIsFemale}).");

         return new NpcChatService(
            LlmClient,
            new SectionResponseParser(),
            promptBuilder) {
            // Audit M13. The MaxTokens/Creativity values restate the SDK defaults deliberately: the whole
            // generation shape is one decision, and splitting it across two projects is how it drifts.
            ChatParameters = new LlmParameters {
               MaxTokens = 1500,
               Creativity = 0.7f,
               PresencePenalty = ScenePresencePenalty
            }
         };
      }

      #endregion
   }
}