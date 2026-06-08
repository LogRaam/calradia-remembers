// Code written by Gabriel Mailhot, 17/05/2026.
// Updated 24/05/2026: lazy PromptBuilder construction so it can read
//                     ModSettings.AdultContentLevel + Hero.MainHero.IsFemale
//                     at first use, after the campaign is loaded (Sprint 8.2).

#region

using System;
using System.Collections.Generic;
using System.Net.Http;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.LlmClient.OpenRouter;
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
    ///
    ///   Sprint 8.2: the <see cref="PromptBuilder"/> is built lazily on first
    ///   use so it can read <see cref="ModSettings.AdultContentLevel"/> and
    ///   <see cref="Hero.MainHero"/> after the campaign is loaded. This avoids
    ///   the NullReferenceException risk of reading Hero.MainHero at submodule
    ///   load time.
    ///
    ///   If the OPENROUTER_API_KEY environment variable is missing, <see cref="IsAvailable" />
    ///   returns false and <see cref="ChatService" /> remains null. The mod must
    ///   gracefully degrade in that case (skip LLM features, keep vanilla behavior).
    /// </summary>
    public sealed class LlmServices : IDisposable
    {
        private const string ApiKeyEnvVar = "OPENROUTER_API_KEY";
        private const string DefaultModel = "x-ai/grok-4.3";

        /// <summary>
        ///   Test-only override for the player's gender used when building the prompt.
        ///   Set by the cr.crc_scene console command to exercise the female/male captive
        ///   path on a character of the other gender. Null = use the real Hero.MainHero.
        ///   Cleared by <see cref="ClearTestGenderOverride" /> when the chat closes.
        /// </summary>
        public static bool? TestPlayerGenderOverride { get; set; }

        private HttpClient _httpClient;
        private NpcChatService _chatService;
        private NpcMemoryService.Core.Models.AdultContentLevel _builtAdultLevel;

        public ILlmClient LlmClient { get; private set; }

        /// <summary>The model currently in effect — read live from the MCM menu.</summary>
        public string ModelId => ResolveModel();

        /// <summary>
        ///   Live availability: the stack is built and a non-empty API key currently
        ///   resolves (from the MCM menu or the env var). Computed each access so a key
        ///   entered in-game takes effect on the next turn — no restart, no save reload.
        /// </summary>
        public bool IsAvailable => LlmClient != null && !string.IsNullOrWhiteSpace(ResolveApiKey());

        /// <summary>
        ///   Lazily-constructed chat service. The PromptBuilder inside is built on first
        ///   access — and rebuilt whenever the adult-content level changes — so options
        ///   edits apply without a restart.
        /// </summary>
        public NpcChatService ChatService
        {
            get
            {
                if (!IsAvailable) return null;
                NpcMemoryService.Core.Models.AdultContentLevel current = ModSettings.AdultContentLevel;
                if (_chatService == null || _builtAdultLevel != current)
                {
                    _chatService     = BuildChatService();
                    _builtAdultLevel = current;
                }
                return _chatService;
            }
        }

        /// <summary>
        ///   Forces the next access to <see cref="ChatService"/> to rebuild the
        ///   <see cref="PromptBuilder"/> with the latest file-loaded content.
        ///   Call after reloading narrative files at session launch.
        /// </summary>
        public void Invalidate() => _chatService = null;

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
                _httpClient = new HttpClient();

                // The client reads the key live via ResolveApiKey on every request, so a key
                // entered in the MCM menu applies immediately — no restart, no save reload.
                OpenRouterConfig config = new OpenRouterConfig {
                    ApiKeyProvider = ResolveApiKey,
                    ModelProvider  = ResolveModel
                };
                LlmClient = new OpenRouterClient(_httpClient, config);

                if (string.IsNullOrWhiteSpace(ResolveApiKey()))
                    ModLog.Info("LLM stack built, but no OpenRouter API key is set yet. " +
                                "Add it in Mods → Mod Options → Calradia Remembers (applies immediately).");
                else
                    ModLog.Info($"LLM stack initialized with model {DefaultModel}.");

                return IsAvailable;
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to initialize LLM stack: {ex.Message}");
                return false;
            }
        }

        #region private

        /// <summary>
        ///   Resolves the OpenRouter API key. A key entered in the MCM menu wins; the
        ///   <c>OPENROUTER_API_KEY</c> environment variable remains a fallback for
        ///   developer setups and power users. Whitespace is treated as unset.
        /// </summary>
        private static string ResolveApiKey()
        {
            string mcmKey = Settings.CrSettings.Instance?.ApiKey;
            if (!string.IsNullOrWhiteSpace(mcmKey)) return mcmKey.Trim();
            return Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        }

        /// <summary>
        ///   Resolves the OpenRouter model id: the MCM menu value wins; falls back to the
        ///   built-in default. Read live on every request via <c>ModelProvider</c>.
        /// </summary>
        private static string ResolveModel()
        {
            string mcmModel = Settings.CrSettings.Instance?.Model;
            return string.IsNullOrWhiteSpace(mcmModel) ? DefaultModel : mcmModel.Trim();
        }

        /// <summary>
        ///   Builds the chat service with a PromptBuilder configured against
        ///   current mod settings and main hero. Called lazily so the campaign
        ///   is guaranteed to be loaded by this point.
        /// </summary>
        // Game actions the LLM may emit. Defined here rather than loaded from JSON
        // because adding a new action always requires code changes in the parser and
        // bridge anyway — external configurability adds no practical value.
        private static readonly IReadOnlyList<GameActionDefinition> ActionVocabulary =
            new List<GameActionDefinition> {
                new GameActionDefinition {
                    Type        = "change_relation",
                    Description = "Reflect a genuine shift in how you regard the player — this is the ONLY way your relationship with them changes. " +
                                  "Emit only when something real passes between you, never for idle pleasantries. " +
                                  "Set 'delta' to the magnitude your heart actually feels: +1 when the player earns sincere warmth this exchange; " +
                                  "a larger positive is justified only by a truly significant, self-evident moment such as physical intimacy. " +
                                  "For a wrong done to you, make 'delta' negative and scale it to the gravity — " +
                                  "a cold rebuff (-2), a crude or unwelcome advance (-5), a pointed insult (-5), " +
                                  "a lewd proposition made in front of others (-8), the betrayal of a real agreement (-15); " +
                                  "if you are the player's captive and they treat you with cruelty, your regard may collapse entirely. " +
                                  "Do NOT stay neutral when the player is disrespectful, vulgar, or presses an advance you have already refused — your regard must fall. " +
                                  "Trust is slow to grow and quick to break. The game enforces pacing and limits, so state what you feel honestly and let it decide what lands.",
                    Parameters  = new List<string> { "delta" }
                },
                new GameActionDefinition {
                    Type        = "end_conversation",
                    Description = "End this conversation now. Emit ONLY when you, in character, would genuinely refuse to continue: " +
                                  "after a grave insult, a crude or unwelcome advance you will not tolerate, " +
                                  "an advance you already refused that the player presses again, repeated disrespect, an open threat — " +
                                  "or when the exchange has reached its natural close and there is nothing more you wish to say. " +
                                  "When you end the conversation because the player wronged or disrespected you, " +
                                  "you MUST also emit change_relation with a negative delta matching the offense. " +
                                  "Do not end over a single clumsy remark or mere awkwardness; reserve it for a real breach or a true farewell.",
                    Parameters  = new List<string>()
                },
                new GameActionDefinition {
                    Type        = "give_gold",
                    Description = "Transfer gold from you to the player (payment, reward, gift, bribe). " +
                                  "The amount must reflect your actual willingness — do not give what you would not give.",
                    Parameters  = new List<string> { "amount" }
                },
                new GameActionDefinition {
                    Type        = "take_gold",
                    Description = "Transfer gold from the player to you (tribute, fee, debt repaid). " +
                                  "Only emit if the player has agreed or you have the authority to demand it.",
                    Parameters  = new List<string> { "amount" }
                }
            };

        private NpcChatService BuildChatService()
        {
            PromptBuilder promptBuilder = new PromptBuilder
            {
                AdultLevel                 = ModSettings.AdultContentLevel,
                PlayerIsFemale             = TestPlayerGenderOverride ?? Hero.MainHero?.IsFemale ?? false,
                WorldDescription           = NarrativeLoader.WorldDescription,
                PlayerDescription          = NarrativeLoader.PlayerDescription,
                BehaviorGuidelinesOverride = NarrativeLoader.BehaviorGuidelines,
                ActionVocabulary           = ActionVocabulary,
                // The NPC's standing is the live game relation (synced each turn),
                // moved only through the gated change_relation action — so the LLM
                // does not write a parallel reputation score.
                EnableReputationBlock      = false,
                // The mod can verify deeds (QuestDeedTracker) and pay rewards, so the
                // NPC is allowed to offer and resolve informal quests.
                EnableQuests               = true
            };

            ModLog.Info($"PromptBuilder built (AdultLevel={ModSettings.AdultContentLevel}, " +
                        $"PlayerIsFemale={promptBuilder.PlayerIsFemale}).");

            return new NpcChatService(
                LlmClient,
                new SectionResponseParser(),
                promptBuilder);
        }

        #endregion
    }
}
