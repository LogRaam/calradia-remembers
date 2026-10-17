// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Settings;
using NpcMemoryService.Core.Actions;
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

      // The player's clan and realm as the builder last captured them. They feed {{user_clan}}, {{user_faction}} and
      // {{faction}}, which were read once and kept until the next load: a bridge mod writing {{faction}} kept naming
      // a realm the player had been expelled from (tashmetu, 19/09/2026). A change now rebuilds the builder.
      private string _builtPlayerFaction;
      private string _builtPlayerClan;
      private float _builtTemperature;
      private NpcChatService _chatService;

      private HttpClient _httpClient;

      // Dedicated action-interpreter client (Prose + Interpreter mode). Built lazily for the interpreter role's
      // provider kind + resolved model, and rebuilt when either changes, mirroring how ChatService tracks its
      // built AdultLevel. It exists even when the interpreter kind equals the default kind, so the caller always
      // has one uniform path.
      private ILlmClient _interpreterClient;
      private ILlmClient _housekeepingClient;
      private ProviderKind _builtHousekeepingKind;
      private string _builtHousekeepingModel;
      private ProviderKind _builtInterpreterKind;
      private string _builtInterpreterModel;

      // The NSFW role's own client (04/10/2026): the refused-beat fallback in Enhanced mode reaches the NSFW model
      // on the NSFW role's provider, key and pin, whatever the scene's provider is. Rebuilt like the interpreter's.
      private ILlmClient _nsfwClient;
      private ProviderKind _builtNsfwKind;
      private string _builtNsfwModel;

      /// <summary>
      ///   Test-only override for the player's gender used when building the prompt.
      ///   Set by the cr.crc_scene console command to exercise the female/male captive
      ///   path on a character of the other gender. Null = use the real Hero.MainHero.
      ///   Cleared by <see cref="ClearTestGenderOverride" /> when the chat closes.
      /// </summary>
      public static bool? TestPlayerGenderOverride { get; set; }

      /// <summary>
      ///   Test-only override for the player's NAME used when building the prompt. Set by the roleplay bench
      ///   (cr.rp_bench) so a forced-male scene reads with a masculine name instead of the real character's,
      ///   keeping the staged identity coherent. Null or blank = use the real Hero.MainHero name.
      /// </summary>
      public static string TestPlayerNameOverride { get; set; }

      /// <summary>
      ///   Test-only override for the MODEL ID a request resolves to. Set by the prose + interpreter bench
      ///   (cr.prose_interpret_test) to point a SINGLE LLM call at an arbitrary model id (the prose model), then
      ///   cleared so the next call resolves normally (the MCM-configured action interpreter). When
      ///   non-null/non-whitespace it wins over the ActiveModelMode/consensual/CNC resolution in
      ///   <see cref="ResolveModel" />. Null or blank = normal resolution (no behaviour change). Invalidate the chat
      ///   service after setting/clearing it.
      /// </summary>
      public static string TestModelOverride { get; set; }

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
            float temperature = ModSettings.ChatTemperature;

            string playerFaction = Hero.MainHero?.MapFaction?.Name?.ToString() ?? "";
            string playerClan = Hero.MainHero?.Clan?.Name?.ToString() ?? "";

            if (_chatService != null && _builtAdultLevel == current && _builtWarmth == warmth
                                      && _builtReplyLanguage == replyLanguage
                                      && _builtPlayerFaction == playerFaction && _builtPlayerClan == playerClan
                                      && _builtTemperature == temperature)
               return _chatService;
            _chatService = BuildChatService();
            _builtAdultLevel = current;
            _builtWarmth = warmth;
            _builtReplyLanguage = replyLanguage;
            _builtPlayerFaction = playerFaction;
            _builtPlayerClan = playerClan;
            _builtTemperature = temperature;

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

      /// <summary>
      ///   Optional diagnostic seat beside the wire (cr.wire), null in normal play. Held HERE rather than on
      ///   a client because the interpreter client is rebuilt whenever its provider or model changes, so an
      ///   observer attached to the instance alone would quietly stop reporting halfway through a session.
      ///   Setting it attaches to whatever exists now; every client built later picks it up.
      /// </summary>
      public ILlmWireObserver WireObserver
      {
         get => _wireObserver;
         set
         {
            _wireObserver = value;
            Attach(LlmClient);
            Attach(_interpreterClient);
            Attach(_nsfwClient);
         }
      }

      private ILlmWireObserver _wireObserver;

      private void Attach(ILlmClient client)
      {
         if (client is OpenRouterClient open) open.WireObserver = _wireObserver;
      }

      /// <summary>
      ///   The dedicated client for the ACTION INTERPRETER role (Prose + Interpreter composition). Its base URL,
      ///   API key, caching, and parameter shape are fixed to the interpreter role's provider kind, and its model
      ///   is <see cref="ResolveInterpreterModel" />. Built lazily and rebuilt only when the interpreter provider
      ///   selection or the resolved interpreter model changes. Never returns null and never throws: a build fault
      ///   falls back to the main <see cref="LlmClient" /> so the caller (which already degrades to no-tags on any
      ///   interpreter failure) is never disturbed.
      /// </summary>
      public ILlmClient InterpreterClient
      {
         get
         {
            try
            {
               if (_httpClient == null) return LlmClient;

               ProviderKind kind = ResolveKindForRole(ServiceRole.Interpret);
               string model = ResolveInterpreterModel();

               if (_interpreterClient != null && _builtInterpreterKind == kind && _builtInterpreterModel == model)
                  return _interpreterClient;

               _interpreterClient = BuildInterpreterClient(kind, model);
               _builtInterpreterKind = kind;
               _builtInterpreterModel = model;

               return _interpreterClient;
            }
            catch (Exception ex)
            {
               ModLog.Error($"Failed to build the action-interpreter client; falling back to the main client: {ex.Message}");

               return LlmClient;
            }
         }
      }

      /// <summary>
      ///   The NSFW role's own client, its model <see cref="ResolveNsfwModel" />: where Enhanced mode redoes a beat
      ///   the scene's model refused (<see cref="Logic.RefusalFallbackPolicy" />), on the NSFW role's provider, key,
      ///   reasoning and pin, so an NSFW model on another provider than the prose one is reached where it lives.
      ///   Built lazily, rebuilt when the NSFW provider or model changes. Never throws: a build fault falls back to
      ///   the main <see cref="LlmClient" />.
      /// </summary>
      public ILlmClient NsfwClient
      {
         get
         {
            try
            {
               if (_httpClient == null) return LlmClient;

               ProviderKind kind = ResolveKindForRole(ServiceRole.Nsfw);
               string model = ResolveNsfwModel();

               if (_nsfwClient != null && _builtNsfwKind == kind && _builtNsfwModel == model) return _nsfwClient;

               _nsfwClient = BuildRoleClient(ServiceRole.Nsfw, kind, model);
               _builtNsfwKind = kind;
               _builtNsfwModel = model;

               return _nsfwClient;
            }
            catch (Exception ex)
            {
               ModLog.Error($"Failed to build the NSFW client; falling back to the main client: {ex.Message}");

               return LlmClient;
            }
         }
      }

      /// <summary>
      ///   The NSFW model as configured for the NSFW role's own provider, or empty when it says nothing there (blank,
      ///   or an id of another provider). Empty means there is no distinct model to fall back to.
      /// </summary>
      public string ResolveNsfwModel() => ConfiguredModelFor(ModSettings.CncModel, ResolveKindForRole(ServiceRole.Nsfw)) ?? "";

      /// <summary>The model currently in effect — read live from the MCM menu.</summary>
      public string ModelId => ResolveModel();

      /// <summary>
      ///   The content kind of the scene currently active (set by the captive/captor activators, read back for the
      ///   cr.model diagnostic and the story log). No longer drives model-role selection since the three-state
      ///   in-chat switch (<see cref="ActiveModelMode" />) replaced content-kind auto-routing (21/08/2026): whether
      ///   a captivity scene reaches the NSFW model is now the player's explicit choice, not this value. The async
      ///   captive-scene summary (FinalizeCaptiveSceneMemory, launched via Task.Run after teardown) inherits
      ///   whichever role <see cref="ActiveModelMode" /> held when the scene closed (also not reset on chat close),
      ///   so the model that wrote the scene is the one that summarizes it.
      /// </summary>
      public static SceneContentKind ActiveSceneKind { get; set; } = SceneContentKind.Ordinary;

      /// <summary>
      ///   The THREE-STATE in-chat model switch (Enhanced/Roleplay/NSFW, 21/08/2026, replaces the old binary
      ///   ManualNsfwProse toggle and its MCM visibility gate - the switch is now ALWAYS shown in chat). A RUNTIME
      ///   session flag (not persisted to the save), cycled by the in-chat button. Lazily seeded, on first read of
      ///   a session, from the MCM "Chat Composition" dropdown (Enhanced -> Enhanced, Standard -> Roleplay) so a
      ///   fresh session starts where the player last configured it; once the player cycles the switch, that
      ///   explicit choice sticks for the rest of the session (mirrors the old toggle's "not reset on chat close"
      ///   lifecycle) and no longer re-reads the MCM dropdown.
      /// </summary>
      public static ManualModelMode ActiveModelMode
      {
         get => _activeModelMode ?? SeedModelModeFromSettings();
         set => _activeModelMode = value;
      }

      private static ManualModelMode? _activeModelMode;

      private static ManualModelMode SeedModelModeFromSettings()
         => ModSettings.ChatCompositionMode == ChatCompositionMode.Enhanced ? ManualModelMode.Enhanced : ManualModelMode.Roleplay;

      /// <summary>
      ///   The composition the CURRENT in-chat state resolves to. The single source of truth for whether the
      ///   interpreter call and the hard-failure fallback run this turn (only Enhanced uses either) - callers
      ///   outside this class (the chat exchange loop, the cr.model diagnostic) read this instead of the raw MCM
      ///   "Chat Composition" dropdown, so the in-chat switch - not the MCM default - is authoritative once a
      ///   session has started.
      /// </summary>
      public static ChatCompositionMode ActiveComposition => ModelModePolicy.CompositionFor(ActiveModelMode);

      /// <summary>
      ///   The model role the MAIN chat call uses this turn: purely a function of the three-state switch
      ///   (<see cref="ActiveModelMode" />) - NSFW writes every scene on the NSFW model, Enhanced and Roleplay both
      ///   write on PROSE. Content-kind-based auto-routing (a captivity scene forcing NSFW in Standard) is retired:
      ///   the player now picks NSFW explicitly. The interpreter and the fallback are reached through their own
      ///   paths, not this.
      /// </summary>
      private static ServiceRole ActiveMainRole() => ModelModePolicy.MainRoleFor(ActiveModelMode);

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

         // Availability is judged for the DEFAULT provider only (the one an ordinary turn uses). A misconfigured
         // NON-default role degrades gracefully instead: the live call fails and the in-chat guidance / the
         // interpreter's own try-catch absorb it. Per-role availability messages are a follow-up.
         string provider = ResolveProvider();
         ProviderKind kind = DefaultKind();

         if (provider == ProviderCustom)
         {
            if (string.IsNullOrWhiteSpace(BaseUrlFor(ServiceRole.Prose, kind)))
               return "(No endpoint URL is set. Open Mods → Mod Options → Calradia Remembers, " +
                      "paste your server URL (e.g. http://localhost:1234/v1), then return here — it applies immediately.)";
            if (string.IsNullOrWhiteSpace(ResolveModel()))
               return "(OpenAI-Compatible is selected but no model id is set. Open Mods → Mod Options → Calradia Remembers " +
                      "and paste the model id your server expects — it applies immediately.)";

            return null;
         }

         if (string.IsNullOrWhiteSpace(ApiKeyFor(ServiceRole.Prose, kind)))
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
            // not accept the content-array system message (e.g. NanoGPT). Every lambda
            // resolves for the ACTIVE role's provider kind (ProviderRolePolicy.RoleForMode
            // over ActiveModelMode): an ordinary turn uses the Roleplay role's provider, a
            // consensual scene the Consensual role's, a CNC scene the CNC role's. Calls are
            // sequential, so one live-reading client is safe.
            var config = new OpenRouterConfig {
               ApiKeyProvider = () => ApiKeyFor(ActiveMainRole(), ActiveRoleKind()),
               ModelProvider = ResolveModel,
               BaseUrlProvider = () => BaseUrlFor(ActiveMainRole(), ActiveRoleKind()),
               UseSystemPromptCachingProvider = () => UseCachingFor(ActiveRoleKind()),
               ReasoningProvider = ResolveReasoning,
               ReasoningAsEffortStringProvider = () => ReasoningWirePolicy.UsesEffortString(ActiveRoleKind()),
               TimeoutSecondsProvider = () => ModSettings.LlmResponseTimeoutSeconds,
               ParameterOptionsProvider = () => ParameterOptionsFor(ActiveRoleKind(), ResolveModel()),
               ProviderSlugsProvider = ResolveProviderPin,
               AllowProviderFallbacksProvider = () => ModSettings.AllowOpenRouterProviderFallbacks
            };
            LlmClient = new OpenRouterClient(_httpClient, config) {WireObserver = _wireObserver};

            if (ResolveProvider() != ProviderCustom && string.IsNullOrWhiteSpace(ApiKeyFor(ServiceRole.Prose, DefaultKind())))
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
      /// <summary>
      ///   Test seam (cr.testall): the exact action vocabulary taught to the model, so a self-test can assert an
      ///   action's teaching still carries a required guard without going through the live chat pipeline. Used to
      ///   pin that end_conversation must not fire while the NPC is still awaiting the player's reply.
      /// </summary>
      internal static IReadOnlyList<GameActionDefinition> DebugActionVocabulary(RelationWarmth warmth)
         => BuildActionVocabulary(warmth);

      private static IReadOnlyList<GameActionDefinition> BuildActionVocabulary(RelationWarmth warmth)
      {
         var vocabulary = new List<GameActionDefinition> {
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
                             "or when the PLAYER winds the talk down (a goodbye, a parting word) and you answer in kind. " +
                             "NEVER end on a turn where your own words still engage the player: if you ask a question you want answered, " +
                             "make a request of them, or otherwise await a reply, the conversation MUST stay open, so do NOT emit this action. " +
                             "By default the talk stays open and the PLAYER decides when they are done. A rhetorical outburst as you dismiss them " +
                             "('How dare you?') is still a dismissal and DOES end; a genuine question you expect them to answer does not. " +
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

         // Extension Surface (CrActions.Register): teach the 1:1 prose prompt any externally-registered
         // action too, so a third-party mod's verb is not only visible to the Action Interpreter path.
         // Built-ins are skipped (already hand-authored above, with tuned wording) so none is taught twice.
         // In normal play nothing is registered, so this adds nothing and the vocabulary stays unchanged.
         foreach (GameActionSpec spec in GameActionCatalog.All)
         {
            if (GameActionCatalog.BuiltInTypes.Contains(spec.Type)) continue;

            vocabulary.Add(new GameActionDefinition {
               Type = spec.Type,
               Description = spec.Description,
               Parameters = spec.Parameters.Select(p => p.Name).ToList()
            });
         }

         return vocabulary;
      }

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
            "CONSISTENCY: the sign of 'delta' must match the tone of your OWN reply this turn. If you answer the player " +
            "warmly, with approval or affection, 'delta' is 0 or positive, never negative; make it negative ONLY when " +
            "your reply itself is cold, hurt, angry, or displeased. A friendly reply paired with a negative delta is a " +
            "contradiction. The game enforces pacing and limits, so state what you feel honestly and let it decide what lands.";

         return opening + positive + negative + closing;
      }

      /// <summary>
      ///   The DEFAULT provider kind, parsed from the global "Provider" dropdown. Every role left on "(default)"
      ///   inherits this, which is the migration no-op: an upgraded single-provider config resolves every role to
      ///   this kind exactly as before per-role routing existed. Falls back to OpenRouter when MCM has not loaded.
      /// </summary>
      private static ProviderKind DefaultKind()
         => ProviderRolePolicy.ParseKind(CrSettings.Instance?.Provider?.SelectedValue, ProviderKind.OpenRouter);

      /// <summary>
      ///   The provider kind a given <see cref="ServiceRole" /> resolves to: its own per-role dropdown selection
      ///   layered over <see cref="DefaultKind" /> via <see cref="ProviderRolePolicy.ResolveRoleKind" />. A role
      ///   left on "(default)" returns the default kind. Read live so an MCM change applies immediately.
      /// </summary>
      private static ProviderKind ResolveKindForRole(ServiceRole role)
      {
         string selection;
         switch (role)
         {
            case ServiceRole.Nsfw:
               selection = ModSettings.CncProviderSelection;

               break;
            case ServiceRole.Interpret:
               selection = ModSettings.ActionInterpreterProviderSelection;

               break;
            case ServiceRole.Housekeeping:
               selection = ModSettings.HousekeepingProviderSelection;

               break;
            default:
               selection = ModSettings.RoleplayProviderSelection;

               break;
         }

         return ProviderRolePolicy.ResolveRoleKind(selection, DefaultKind());
      }

      /// <summary>
      ///   The provider kind the CURRENT turn uses: the role for <see cref="ActiveModelMode" /> resolved to a
      ///   kind. The main client's lambdas key off this, so an ordinary turn uses the Roleplay role's provider, a
      ///   consensual scene the Consensual role's, a CNC scene the CNC role's.
      /// </summary>
      private static ProviderKind ActiveRoleKind()
         => ResolveKindForRole(ActiveMainRole());

      /// <summary>Whether a role's requests go to the local (OpenAI-Compatible) server: background work then waits for the chat to close (fkasad, 15/10/2026).</summary>
      public static bool RunsLocally(ServiceRole role) => ResolveKindForRole(role) == ProviderKind.OpenAiCompatible;

      /// <summary>Whether the main client (prose, and the letters written through it) sends to the local server right now.</summary>
      public static bool MainClientRunsLocally => ActiveRoleKind() == ProviderKind.OpenAiCompatible;

      /// <summary>
      ///   The context window a role's requests must fit (fkasad, 13/10/2026): the declared local window when that role
      ///   runs on the OpenAI-compatible (local/custom) endpoint, 0 otherwise (a cloud model is never trimmed).
      /// </summary>
      public static int ContextWindowFor(ServiceRole role)
         => ResolveKindForRole(role) == ProviderKind.OpenAiCompatible ? ModSettings.LocalContextTokens : 0;

      /// <summary>The context window of the role the current turn uses (see <see cref="ContextWindowFor" />).</summary>
      public static int ActiveContextWindow()
         => ContextWindowFor(ActiveMainRole());

      /// <summary>
      ///   Resolves a <paramref name="role" />'s API key for a provider <paramref name="kind" />. A key entered in
      ///   the MCM menu wins; the provider's environment variable (<c>OPENROUTER_API_KEY</c> / <c>NANOGPT_API_KEY</c>)
      ///   remains a fallback for developer setups and power users. Only the OpenAI-Compatible kind varies by role:
      ///   a role may carry its own key override (so two local servers can be split across roles, each with its own
      ///   authentication), falling back to the shared API Key. The aggregators (OpenRouter, NanoGPT) keep one
      ///   account key for every role. Whitespace is treated as unset.
      /// </summary>
      private static string ApiKeyFor(ServiceRole role, ProviderKind kind)
      {
         CrSettings s = CrSettings.Instance;

         if (kind == ProviderKind.OpenAiCompatible)
            return OpenAiApiKeyPolicy.Resolve(s?.CustomApiKey, CustomApiKeyOverrideFor(role));

         bool nano = kind == ProviderKind.NanoGpt;
         string mcmKey = nano
            ? s?.NanoGptApiKey
            : s?.ApiKey;

         if (!string.IsNullOrWhiteSpace(mcmKey)) return mcmKey.Trim();

         return Environment.GetEnvironmentVariable(nano
            ? NanoGptApiKeyEnvVar
            : OpenRouterApiKeyEnvVar);
      }

      /// <summary>
      ///   Resolves the chat-completion parameter shape for a provider kind + model. The aggregators (OpenRouter,
      ///   NanoGPT) normalize parameter names and accept temperature, so they always get the classic shape;
      ///   only the direct OpenAI-Compatible endpoint applies the player's compatibility policy — letting a
      ///   gpt-5/o-series model reached at api.openai.com receive 'max_completion_tokens' with no temperature.
      /// </summary>
      private static ChatParameterOptions ParameterOptionsFor(ProviderKind kind, string modelId)
      {
         if (kind != ProviderKind.OpenAiCompatible) return ChatParameterOptions.Standard;

         LlmWireParameters p = LlmParameterPolicy.Resolve(modelId, ModSettings.CustomParameterCompatibility);

         return new ChatParameterOptions(p.UseMaxCompletionTokens, p.IncludeTemperature);
      }

      /// <summary>
      ///   The OpenRouter providers the player pinned, and ONLY when the ACTIVE role's kind is OpenRouter: the
      ///   field is meaningless to NanoGPT or a local OpenAI-compatible server, so they must never receive it.
      ///   Mode-aware like the model id and the reasoning effort: NSFW mode can pin a different (typically
      ///   uncensored) provider than roleplay via <see cref="ModelModePolicy.ResolveProviderPin" />. This round
      ///   the pin stays exactly as before, keyed off the active role's kind rather than a per-role dropdown.
      /// </summary>
      /// <summary>The provider pin the active role's requests carry right now ("" when none, or not on OpenRouter).</summary>
      internal static string ActiveProviderPin() => ResolveProviderPin();

      private static string ResolveProviderPin() => ResolveProviderPinFor(ActiveMainRole());

      private static string ResolveProviderPinFor(ServiceRole role)
         => ResolveKindForRole(role) == ProviderKind.OpenRouter
            ? ModelModePolicy.ResolveProviderPin(new ModelModePolicy.RoleSettings {Role = role, Prose = ModSettings.OpenRouterProviderPin, Nsfw = ModSettings.OpenRouterNsfwProviderPin, Interpret = ""})
            : "";

      /// <summary>
      ///   Resolves the base URL for a <paramref name="role" /> on a provider <paramref name="kind" />, read live
      ///   per request. Only the OpenAI-Compatible kind varies by role: a role may carry its own endpoint override
      ///   (so two local servers can be split across roles), falling back to the shared Endpoint URL. The
      ///   aggregators (OpenRouter, NanoGPT) have one fixed base URL for every role.
      /// </summary>
      private static string BaseUrlFor(ServiceRole role, ProviderKind kind)
      {
         if (kind == ProviderKind.NanoGpt) return NanoGptBaseUrl;
         if (kind == ProviderKind.OpenAiCompatible)
            return OpenAiEndpointPolicy.Resolve(CrSettings.Instance?.CustomEndpointUrl, CustomEndpointOverrideFor(role));

         return OpenRouterBaseUrl;
      }

      /// <summary>The per-role OpenAI-Compatible endpoint override, or null for the Prose role (which uses the shared endpoint).</summary>
      private static string CustomEndpointOverrideFor(ServiceRole role)
      {
         CrSettings s = CrSettings.Instance;

         switch (role)
         {
            case ServiceRole.Nsfw: return s?.CustomEndpointUrlNsfw;
            case ServiceRole.Interpret: return s?.CustomEndpointUrlInterpret;
            case ServiceRole.Housekeeping: return s?.CustomEndpointUrlHousekeeping;
            default: return null;
         }
      }

      /// <summary>The per-role OpenAI-Compatible API key override, or null for the Prose role (which uses the shared key).</summary>
      private static string CustomApiKeyOverrideFor(ServiceRole role)
      {
         CrSettings s = CrSettings.Instance;

         switch (role)
         {
            case ServiceRole.Nsfw: return s?.CustomApiKeyNsfw;
            case ServiceRole.Interpret: return s?.CustomApiKeyInterpret;
            case ServiceRole.Housekeeping: return s?.CustomApiKeyHousekeeping;
            default: return null;
         }
      }

      /// <summary>
      ///   The roleplay model id for a provider kind: the shared "Roleplay and Prose model" MCM field serves every
      ///   provider (ids are pasted in the provider's own namespace). OpenRouter falls back to the built-in
      ///   default; the other kinds treat a blank field or the untouched OpenRouter default as unconfigured and
      ///   resolve blank, so the provider-aware in-chat guidance fires instead of a cryptic API error.
      /// </summary>
      private static string ResolveRoleplayModelForKind(ProviderKind kind)
      {
         string configured = ConfiguredModelFor(CrSettings.Instance?.Model, kind);

         if (ProviderModelPolicy.OwnsItsOwnModelNamespace(kind)) return configured;

         return string.IsNullOrWhiteSpace(configured)
            ? DefaultModel
            : configured;
      }

      /// <summary>
      ///   One model field, read against the provider the role carrying it actually points at: the trimmed id
      ///   when it is a real choice there, EMPTY when the field is blank or still holds the shipped OpenRouter
      ///   default on a provider that names its own models.
      ///   <para>
      ///     This rule used to live inside <see cref="ResolveRoleplayModelForKind" /> and so protected the PROSE
      ///     role alone. The interpret and NSFW fields sent their untouched default verbatim, which is how a
      ///     player pointing the interpreter at his own LM Studio watched prose work and interpretation fail and
      ///     reasonably concluded the mod had crossed the two (Nexus, 2026-09-11). Every role asks the same
      ///     question now, and it is answered in one place.
      ///   </para>
      /// </summary>
      private static string ConfiguredModelFor(string mcmField, ProviderKind kind)
         => ProviderModelPolicy.Resolve(new ProviderModelPolicy.ModelChoice {
            ConfiguredId = mcmField, Kind = kind, ShippedDefaultId = DefaultModel
         });

      /// <summary>
      ///   Resolves the model id for the ACTIVE turn: the roleplay model for the active role's kind, then the
      ///   consensual/CNC override when <see cref="ActiveModelMode" /> is an explicit mode and one is configured.
      ///   Read live on every request via <c>ModelProvider</c>, so flipping the mode mid-conversation just works.
      /// </summary>
      private static string ResolveModel()
      {
         // TEST SEAM: a non-blank override points a single call at an arbitrary model id (cr.prose_interpret_test),
         // ahead of the normal mode/NSFW resolution. Blank or null falls through to the usual resolution below.
         if (!string.IsNullOrWhiteSpace(TestModelOverride)) return TestModelOverride;

         string proseModel = ResolveRoleplayModelForKind(ActiveRoleKind());

         // Phase 2: PROSE = the base model (per provider kind), NSFW = the CncModel storage (migrated from the old
         // CNC/NSFW slot), INTERPRET = the ActionInterpreterModel storage. The main call is only ever Prose or Nsfw.
         //
         // Each field is read against ITS OWN role's provider, not the active one: a player may keep prose on
         // OpenRouter and put the NSFW model on a local server. A field that says nothing for its provider
         // resolves EMPTY, which is the signal ResolveModelId already treats as "fall back to prose".
         return ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {
            Role = ActiveMainRole(),
            Prose = proseModel,
            Nsfw = ConfiguredModelFor(ModSettings.CncModel, ResolveKindForRole(ServiceRole.Nsfw)),
            Interpret = ConfiguredModelFor(ModSettings.ActionInterpreterModel, ResolveKindForRole(ServiceRole.Interpret))
         });
      }

      /// <summary>
      ///   The model id the ACTION INTERPRETER client uses: the configured "Actions Interpreter model" when set,
      ///   otherwise a blank field reuses the scene's own resolved model (byte-for-byte the pre-feature behaviour,
      ///   where a null ModelOverride let the main client resolve the scene model). The <see cref="TestModelOverride" />
      ///   seam is honoured through <see cref="ResolveModel" />.
      /// </summary>
      public string ResolveInterpreterModel()
      {
         // Read against the INTERPRETER's own provider. An untouched field still holding the shipped OpenRouter
         // id says nothing to a local server, so it counts as unset and the scene's own model is used instead:
         // the same meaning a blank field has always had, now reached by the case that actually bit a player.
         string configured = ConfiguredModelFor(ModSettings.ActionInterpreterModel, ResolveKindForRole(ServiceRole.Interpret));

         return string.IsNullOrWhiteSpace(configured)
            ? ResolveModel()
            : configured;
      }

      /// <summary>
      ///   The model that completes a beat the PROSE model cut off mid-sentence (Prose + Interpreter mode only,
      ///   see <see cref="Logic.TruncationFallbackPolicy" />). Phase 2: this is the NSFW model (the explicit one),
      ///   reached only when the PROSE beat fails hard; when the NSFW field is blank it reuses the current scene
      ///   model via <see cref="ResolveModel" />, so a single-model config still keeps a safety net (the NSFW
      ///   model itself defaults to grok, so no new config is needed by default).
      /// </summary>
      public string ResolveProseFallbackModel()
      {
         // Phase 2: the hard-failure fallback is the NSFW model (the explicit one), reached only when the PROSE
         // beat fails hard. Blank NSFW falls back to the current scene model so a single-model config still works.
         string nsfw = ModSettings.CncModel;

         return string.IsNullOrWhiteSpace(nsfw)
            ? ResolveModel()
            : nsfw;
      }

      /// <summary>
      ///   The selected provider id from the MCM menu (display string). Falls back to OpenRouter when MCM has not
      ///   loaded. Kept for the boot log and <see cref="DescribeUnavailability" />; routing itself goes through the
      ///   kind helpers above.
      /// </summary>
      private static string ResolveProvider()
      {
         string sel = CrSettings.Instance?.Provider?.SelectedValue;

         if (sel == ProviderNanoGpt) return ProviderNanoGpt;
         if (sel == ProviderCustom) return ProviderCustom;

         return ProviderOpenRouter;
      }

      /// <summary>
      ///   Resolves the reasoning-effort keyword for the request. The <c>reasoning</c> parameter is
      ///   OpenRouter-specific, so it is suppressed (null) unless the ACTIVE role's kind is OpenRouter or NanoGPT.
      ///   Mode-aware like the model id: NSFW mode can run a lower effort than roleplay via its own MCM dropdown.
      ///   This round the effort stays exactly as before, keyed off the active role's kind rather than a per-role
      ///   dropdown.
      /// </summary>
      private static string ResolveReasoning() => ResolveReasoningFor(ActiveMainRole());

      private static string ResolveReasoningFor(ServiceRole role)
      {
         ProviderKind kind = ResolveKindForRole(role);

         // OpenRouter and NanoGPT (a second aggregator, sharing OpenRouter's mode-aware dials): the mode-aware
         //   effort (PROSE/NSFW/interpreter). OpenRouter serializes it as its own nested reasoning object; NanoGPT
         //   serializes the SAME keyword as the top-level reasoning_effort string, its own authoritative field
         //   (see ReasoningWirePolicy.UsesEffortString), since NanoGPT ignores the nested object entirely, which
         //   previously left NanoGPT with no reasoning field at all.
         // OpenAI-Compatible (Ollama et al.): its OWN dropdown, also serialized as the top-level reasoning_effort
         //   string. It is a SEPARATE dial defaulting to "Default" (send nothing) so an untouched local setup is
         //   unchanged.
         string keyword =
            kind == ProviderKind.OpenRouter || kind == ProviderKind.NanoGpt
               ? ModelModePolicy.ResolveReasoningEffort(new ModelModePolicy.RoleSettings {Role = role, Prose = ModSettings.ReasoningEffort, Nsfw = ModSettings.NsfwReasoningEffort, Interpret = InterpreterReasoningEffort})
               : kind == ProviderKind.OpenAiCompatible
                  ? ModSettings.CustomReasoningEffort
                  : null;

         if (ModSettings.VerboseLogging)
            ModLog.Info($"[LLM] reasoning: kind={kind}, effort={keyword ?? "(none)"}, "
                      + $"wire={(ReasoningWirePolicy.UsesEffortString(kind) ? "reasoning_effort string" : "reasoning object")}");

         return keyword;
      }

      /// <summary>
      ///   Whether to send the cacheable content-array system message for a provider kind. OpenRouter honors the
      ///   <c>cache_control</c> breakpoint; other providers route to arbitrary backends where the array form is
      ///   untested, so we send a plain string to stay portable.
      /// </summary>
      private static bool UseCachingFor(ProviderKind kind) => kind == ProviderKind.OpenRouter;

      // The interpreter is a MECHANICAL extraction task: read the prose, emit the tags. It must NOT reason. A
      // reasoning model told to think (inheriting the scene's effort) burns the whole small reply budget on
      // internal reasoning and emits no text, so the interpreter call produced no tags in play (2026-08-13,
      // kimi-k2.6 on OpenRouter with the roleplay effort). The interpreter therefore pins reasoning OFF regardless
      // of the scene, independent of the prose model's own effort.
      private const string InterpreterReasoningEffort = "Off";

      /// <summary>
      ///   Builds the dedicated action-interpreter client for a provider <paramref name="kind" /> and a fixed
      ///   <paramref name="model" />. Every lambda is bound to that ONE kind so the interpreter never rides the
      ///   scene's provider. Reasoning is pinned OFF (extraction is mechanical, and a thinking model would spend
      ///   the small reply budget on reasoning and emit no tags); the provider-pin still mirrors the main client's
      ///   OpenRouter-only routing.
      /// </summary>
      /// <summary>
      ///   The model for the mod's BACKGROUND work, or the prose model when the field is blank, which is what
      ///   every install did before the setting existed. Read live, so changing it in the menu applies at once.
      /// </summary>
      private static string ResolveHousekeepingModel()
      {
         string configured = ConfiguredModelFor(ModSettings.HousekeepingModel, ResolveKindForRole(ServiceRole.Housekeeping));

         return string.IsNullOrWhiteSpace(configured) ? ResolveModel() : configured.Trim();
      }

      /// <summary>
      ///   The dedicated client for the mod's BACKGROUND work: summaries, memory and transcript compression,
      ///   claim extraction, the strategic assessment. Built lazily and rebuilt only when its provider or model
      ///   changes, and it falls back to the main client on any fault, exactly like
      ///   <see cref="InterpreterClient" />.
      ///   <para>
      ///     Asked for by fkasad (Nexus, 19/09/2026), who runs a local model: the mod's background prompts and
      ///     its conversation prompts have almost nothing in common, and a local server reusing its KV cache by
      ///     longest common prefix on one slot has them evicting each other every time they alternate. Nothing
      ///     the client sends can prevent that, because the eviction is the server's own bookkeeping; sending
      ///     them somewhere else is what prevents it.
      ///   </para>
      ///   <para>
      ///     WITH THE FIELD BLANK THIS IS THE PROSE MODEL ON THE PROSE PROVIDER, so an install that ignores the
      ///     setting behaves exactly as it did. The cost of the feature to everybody else is one more cached
      ///     client object.
      ///   </para>
      /// </summary>
      public ILlmClient HousekeepingClient
      {
         get
         {
            try
            {
               if (_httpClient == null) return LlmClient;

               ProviderKind kind = ResolveKindForRole(ServiceRole.Housekeeping);
               string model = ResolveHousekeepingModel();

               if (_housekeepingClient != null && _builtHousekeepingKind == kind && _builtHousekeepingModel == model)
                  return _housekeepingClient;

               _housekeepingClient = BuildHousekeepingClient(kind, model);
               _builtHousekeepingKind = kind;
               _builtHousekeepingModel = model;
               // fkasad (17/10/2026): the wire showed the prose model on background calls and nothing said what the
               // background slot really resolved to. Logged once per (re)build. A local server may still answer with
               // whatever model it has loaded, whatever name was asked for.
               ModLog.Info($"[LLM] background client: provider={kind}, model={(string.IsNullOrWhiteSpace(model) ? "(none)" : model)}, address={BaseUrlFor(ServiceRole.Housekeeping, kind)}");

               return _housekeepingClient;
            }
            catch (Exception ex)
            {
               ModLog.Error($"Failed to build the background-work client; falling back to the main client: {ex.Message}");

               return LlmClient;
            }
         }
      }

      private ILlmClient BuildHousekeepingClient(ProviderKind kind, string model)
      {
         var config = new OpenRouterConfig {
            ApiKeyProvider = () => ApiKeyFor(ServiceRole.Housekeeping, kind),
            ModelProvider = () => model,
            BaseUrlProvider = () => BaseUrlFor(ServiceRole.Housekeeping, kind),
            UseSystemPromptCachingProvider = () => UseCachingFor(kind),
            // Background work never reasons, and every one of these callers already says so per request. Saying
            // it here too costs nothing and closes the case where one of them forgets: the retry that silently
            // switched reasoning back on (fixed 17/09/2026) was exactly that failure one level down.
            ReasoningProvider = () => kind == ProviderKind.OpenRouter || kind == ProviderKind.OpenAiCompatible || kind == ProviderKind.NanoGpt
               ? InterpreterReasoningEffort
               : null,
            ReasoningAsEffortStringProvider = () => ReasoningWirePolicy.UsesEffortString(kind),
            TimeoutSecondsProvider = () => ModSettings.LlmResponseTimeoutSeconds,
            ParameterOptionsProvider = () => ParameterOptionsFor(kind, model),
            ProviderSlugsProvider = () => "",
            AllowProviderFallbacksProvider = () => ModSettings.AllowOpenRouterProviderFallbacks
         };

         return new OpenRouterClient(_httpClient, config) {WireObserver = _wireObserver};
      }

      /// <summary>
      ///   A client fixed to one <paramref name="role" />, its provider <paramref name="kind" /> and a fixed
      ///   <paramref name="model" />: the main client's shape (that role's key, base URL, reasoning and pin), with every
      ///   lambda bound to the role instead of following <see cref="ActiveModelMode" />.
      /// </summary>
      private ILlmClient BuildRoleClient(ServiceRole role, ProviderKind kind, string model)
      {
         var config = new OpenRouterConfig {
            ApiKeyProvider = () => ApiKeyFor(role, kind),
            ModelProvider = () => model,
            BaseUrlProvider = () => BaseUrlFor(role, kind),
            UseSystemPromptCachingProvider = () => UseCachingFor(kind),
            ReasoningProvider = () => ResolveReasoningFor(role),
            ReasoningAsEffortStringProvider = () => ReasoningWirePolicy.UsesEffortString(kind),
            TimeoutSecondsProvider = () => ModSettings.LlmResponseTimeoutSeconds,
            ParameterOptionsProvider = () => ParameterOptionsFor(kind, model),
            ProviderSlugsProvider = () => ResolveProviderPinFor(role),
            AllowProviderFallbacksProvider = () => ModSettings.AllowOpenRouterProviderFallbacks
         };

         return new OpenRouterClient(_httpClient, config) {WireObserver = _wireObserver};
      }

      private ILlmClient BuildInterpreterClient(ProviderKind kind, string model)
      {
         var config = new OpenRouterConfig {
            ApiKeyProvider = () => ApiKeyFor(ServiceRole.Interpret, kind),
            ModelProvider = () => model,
            BaseUrlProvider = () => BaseUrlFor(ServiceRole.Interpret, kind),
            UseSystemPromptCachingProvider = () => UseCachingFor(kind),
            // The interpreter is a MECHANICAL extraction that must NEVER reason (a reasoning model told to think
            // burns its small reply budget and emits no tags). Force Off on OpenRouter, NanoGPT and a local
            // endpoint; on the latter two, serialized as the top-level reasoning_effort string (the object form
            // is ignored by both).
            ReasoningProvider = () => kind == ProviderKind.OpenRouter || kind == ProviderKind.OpenAiCompatible || kind == ProviderKind.NanoGpt
               ? InterpreterReasoningEffort
               : null,
            ReasoningAsEffortStringProvider = () => ReasoningWirePolicy.UsesEffortString(kind),
            TimeoutSecondsProvider = () => ModSettings.LlmResponseTimeoutSeconds,
            ParameterOptionsProvider = () => ParameterOptionsFor(kind, model),
            ProviderSlugsProvider = () => kind == ProviderKind.OpenRouter
               ? ModelModePolicy.ResolveProviderPin(new ModelModePolicy.RoleSettings {Role = ServiceRole.Interpret, Prose = ModSettings.OpenRouterProviderPin, Nsfw = ModSettings.OpenRouterNsfwProviderPin, Interpret = ""})
               : "",
            AllowProviderFallbacksProvider = () => ModSettings.AllowOpenRouterProviderFallbacks
         };

         return new OpenRouterClient(_httpClient, config) {WireObserver = _wireObserver};
      }

      /// <summary>
      ///   Debug seam (cr.provider_routing / the provider_routing self-test): reports what a role WOULD resolve to
      ///   (provider kind, base URL, model id) with NO network call, so a test can prove the roles route to
      ///   distinct providers and that all-"(default)" collapses them onto the default provider's base URL.
      /// </summary>
      public (ProviderKind kind, string baseUrl, string model) DebugDescribeRoleRouting(ServiceRole role)
      {
         ProviderKind kind = ResolveKindForRole(role);
         string baseUrl = BaseUrlFor(role, kind);
         string model;

         switch (role)
         {
            case ServiceRole.Interpret:
               model = ResolveInterpreterModel();

               break;
            case ServiceRole.Housekeeping:
               model = ResolveHousekeepingModel();

               break;
            case ServiceRole.Nsfw:
               model = ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {
                  Role = ServiceRole.Nsfw, Prose = ResolveRoleplayModelForKind(kind),
                  Nsfw = ModSettings.CncModel, Interpret = ModSettings.ActionInterpreterModel
               });

               break;
            default:
               model = ResolveRoleplayModelForKind(kind);

               break;
         }

         return (kind, baseUrl, model);
      }

      /// <summary>
      ///   Debug seam (provider_routing self-test only): the API key a <paramref name="role" /> WOULD resolve to,
      ///   with NO network call. NEVER surfaced to any player-facing command (unlike <see cref="DebugDescribeRoleRouting" />,
      ///   which cr.provider_routing prints) so a real configured key is never echoed anywhere; the self-test that
      ///   uses this only ever sets dummy sentinel strings, never a real key.
      /// </summary>
      public string DebugResolveApiKeyFor(ServiceRole role) => ApiKeyFor(role, ResolveKindForRole(role));

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

      /// <summary>
      ///   The REAL PromptBuilder the chat runs on, built exactly as <see cref="BuildChatService" /> builds it.
      ///   Extracted so cr.testall can read the live construction rather than a copy of it: a self-test that
      ///   rebuilt these fields itself would agree with itself forever, which is precisely how the 2026-09-08
      ///   affection-bar drift went unseen for a release.
      /// </summary>
      internal PromptBuilder DebugBuildPromptBuilder() => BuildPromptBuilder();

      private NpcChatService BuildChatService()
      {
         PromptBuilder promptBuilder = BuildPromptBuilder();

         ModLog.Info($"PromptBuilder built (AdultLevel={ModSettings.AdultContentLevel}, " +
                     $"Warmth={ModSettings.RelationWarmth}, PlayerIsFemale={promptBuilder.PlayerIsFemale}).");

         return BuildChatService(promptBuilder);
      }

      private PromptBuilder BuildPromptBuilder()
      {
         return new PromptBuilder {
            AdultLevel = ModSettings.AdultContentLevel,
            PlayerIsFemale = TestPlayerGenderOverride ?? Hero.MainHero?.IsFemale ?? false,
            // Name and clan are injected from the live Hero so NPCs can address the player by
            // their actual in-game name and reference their lineage, regardless of what the user
            // has written (or not written) in player_description.txt.
            PlayerName = string.IsNullOrWhiteSpace(TestPlayerNameOverride)
               ? Hero.MainHero?.Name?.ToString() ?? ""
               : TestPlayerNameOverride,
            PlayerClanName = Hero.MainHero?.Clan?.Name?.ToString() ?? "",
            PlayerFactionName = Hero.MainHero?.MapFaction?.Name?.ToString() ?? "",
            WorldDescription = NarrativeLoader.WorldDescription,
            PlayerDescription = NarrativeLoader.PlayerDescription,
            BehaviorGuidelinesOverride = NarrativeLoader.BehaviorGuidelines,
            PostHistoryInstructions = NarrativeLoader.PostHistoryInstructions,
            HeardNowInstructions = NarrativeLoader.HeardNowInstructions,
            ReplyLanguage = ModSettings.ReplyLanguage,
            ActionVocabulary = BuildActionVocabulary(ModSettings.RelationWarmth),
            // The intimacy consent bar follows the same Relationship Pacing dial as every other gate; the
            // SDK owns the bar, the mod owns how far the player's dial lowers it (audit M3).
            IntimacyThresholdRelief = IntimacyPacingRules.Relief(ModSettings.RelationWarmth),
            // The EMOTIONAL twin of that bar, and the reason it is passed RESOLVED rather than as a relief:
            // this is the very number CompanionSecretAffectionPolicy measures the confession roll from, so the
            // words an NPC is allowed to say and the moment they are allowed to say them cannot drift apart.
            // That drift is not hypothetical: on 2026-09-08 this bar lived as a literal 50 in three places, one
            // of them moved onto the pacing dial, and a companion declared their feelings while the game refused
            // the bond (yozakura12, Nexus).
            DeclaredAffectionBar = CompanionSecretAffectionPolicy.MinRegardFor(ModSettings.RelationWarmth),
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
      }

      private NpcChatService BuildChatService(PromptBuilder promptBuilder)
      {
         return new NpcChatService(
            LlmClient,
            new SectionResponseParser(),
            promptBuilder) {
            // Every turn on the main client is fitted to the window of the role it runs on (fkasad, 13/10/2026).
            ContextWindowProvider = ActiveContextWindow,
            // Audit M13. The MaxTokens/Creativity values restate the SDK defaults deliberately: the whole
            // generation shape is one decision, and splitting it across two projects is how it drifts.
            // MaxTokens raised 1500->3000 (2026-08-15): long explicit captive/Director beats were truncated
            // mid-sentence at 1500 (a beat's prose plus, on a reasoning model, its thinking can exceed it). This
            // is a CEILING, not a target: an ordinary chat reply still stops on its own, so cost only rises on
            // the rare long beat that actually needs the room. Kept in sync with NpcChatService's default.
            ChatParameters = new LlmParameters {
               MaxTokens = 3000,
               // The "Chat Temperature" setting (fkasad, 17/10/2026), 0.7 unless the player moved it.
               Creativity = ModSettings.ChatTemperature,
               PresencePenalty = ScenePresencePenalty
            }
         };
      }

      #endregion
   }
}