// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using CalradiaRemembers.Logic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

#endregion

namespace CalradiaRemembers.Settings
{
   /// <summary>
   ///   MCM (Mod Configuration Menu) global settings for Calradia Remembers.
   ///   Stored per-machine under <c>Configs/ModSettings/Global</c>, deliberately NOT
   ///   in the save file, so a shared save never carries the player's API key.
   ///   Read through the <see cref="ModSettings" /> façade, which falls back to safe
   ///   defaults when MCM has not yet loaded.
   ///   Menu layout (GroupOrder): the shared LLM Provider group first (provider choice,
   ///   the model fields, timeout, compact prompt), then one connection section per
   ///   provider (key, endpoint, provider-specific options, Test button), then gameplay
   ///   (Content, Character, Companions), Diagnostics last. Within each group, Order runs
   ///   ascending. The model fields are SHARED across providers: ids are pasted in the
   ///   active provider's own namespace; resolution treats the untouched OpenRouter default
   ///   as unconfigured for the other providers (see LlmServices.ResolveModel).
   /// </summary>
   public sealed class CrSettings : AttributeGlobalSettings<CrSettings>
   {
      [SettingPropertyDropdown("Adult Content Level", RequireRestart = false, Order = 0,
         HintText = "How much romantic/intimate content NPCs may express. Off, Mature (romance, " +
                    "nothing explicit), Explicit, or Hardcore. Applies on your next conversation.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> AdultContentLevel { get; set; } =
         new(new[] {"Off", "Mature", "Explicit", "Hardcore"}, 1); // default: Mature

      [SettingPropertyDropdown("Chat Window Style", RequireRestart = false, Order = 5,
         HintText = "The look of the conversation window. Origin is the original panel; Dark Leather is the " +
                    "illuminated leather book. Both are the same conversation, only the look differs. Applies on " +
                    "your next conversation.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> ChatSkin { get; set; } =
         new(new[] {"Origin", "Dark Leather"}, 1); // default: Dark Leather (the illuminated book)

      [SettingPropertyInteger("Chat Text Size (%)", 70, 130, "0 '%'", RequireRestart = false, Order = 6,
         HintText = "Scales ONLY the dialogue text in the chat window, not the window itself: a smaller value fits " +
                    "more words per line, a larger one reads easier. 100 is the shipped size. Applies on your next conversation.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public int ChatTextSizePercent { get; set; } = 100;

      [SettingPropertyInteger("Dark Leather Size (%)", 80, 140, "0 '%'", RequireRestart = false, Order = 7,
         HintText = "Scales the Dark Leather chat window. Raise it on a high-resolution screen (1440p+) if the book " +
                    "feels small or cramped. 100 is the shipped size. No effect on the Origin skin.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public int DarkLeatherZoomPercent { get; set; } = 100;

      [SettingPropertyInteger("Council Size (%)", 60, 140, "0 '%'", RequireRestart = false, Order = 8,
         HintText = "Scales the council (round-table) window, INDEPENDENTLY of the chat size above. The council book " +
                    "is wider than a chat window, so on a 1440p+ screen it can feel too big at the default: lower this " +
                    "if it overflows, raise it if it feels small. 100 is the shipped size. Applies to the next council.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public int CouncilZoomPercent { get; set; } = 100;

      [SettingPropertyInteger("Council Text Size (%)", 70, 130, "0 '%'", RequireRestart = false, Order = 9,
         HintText = "Scales ONLY the dialogue text in the council (round-table) window, INDEPENDENTLY of the chat " +
                    "text size above. 100 is the shipped size. Applies to the next council.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public int CouncilTextSizePercent { get; set; } = 100;

      [SettingPropertyInteger("Marriage Regard Needed", 10, 50, "0", RequireRestart = false, Order = 10,
         HintText = "How much regard a lord or lady must hold for you before they will accept a LOVE-MATCH marriage " +
                    "(their own personal regard, OR your vanilla clan relation, whichever is higher). 50 is the " +
                    "shipped bar; lower it for a gentler courtship. Their FAMILY'S BLESSING can still open a match " +
                    "below this bar. Applies immediately.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public int MarriageRegardNeeded { get; set; } = 50;

      [SettingPropertyText("OpenRouter API Key", RequireRestart = false, Order = 0,
         HintText = "Paste your key from openrouter.ai/keys. Stored locally on this machine only. NEVER share your " +
                    "config file or a screenshot of this field. Applies immediately.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public string ApiKey { get; set; } = "";

      // Hints must stay SHORT: a long one overflows the MCM control and its first line is cut off the top
      // (Gabriel, screenshot 2026-07-13). Two or three lines, no more. Say the stake, not the essay.
      // ProviderPin and NsfwProviderPin are resolved per model mode by ModelModePolicy.ResolveProviderPin.
      [SettingPropertyText("Pin Providers (advanced)", RequireRestart = false, Order = 4,
         HintText = "Leave blank unless you need it. Some OpenRouter providers censor their own output, cutting " +
                    "replies mid-sentence. Enter trusted provider slugs, comma separated (e.g. deepinfra, together).")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public string ProviderPin { get; set; } = "";

      [SettingPropertyText("Pin Providers (NSFW)", RequireRestart = false, Order = 5,
         HintText = "Blank reuses the pin above. Set a different provider only for explicit scenes, e.g. " +
                    "cheapest routing for chat and an uncensored provider for scenes.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public string NsfwProviderPin { get; set; } = "";

      [SettingPropertyBool("Allow provider fallbacks", RequireRestart = false, Order = 6,
         HintText = "Only matters with providers pinned above. OFF (default) makes the pin a hard rule. ON lets " +
                    "OpenRouter fall back to others when yours are unavailable.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public bool AllowProviderFallbacks { get; set; } = false;

      [SettingPropertyBool("Enable Council", RequireRestart = false, Order = 3,
         HintText = "When ON, you can convene a council of your companions, kin, clan lords, or a war council from a " +
                    "town, castle, or village menu, and from a button on the campaign map while on land. Turn OFF to " +
                    "hide those entry points; the council can still be opened from the console.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public bool EnableCouncil { get; set; } = true;

      [SettingPropertyDropdown("Council Voices", RequireRestart = false, Order = 4,
         HintText = "Default for a NEWLY opened council (flip it anytime with the button in the council window " +
                    "itself). One call for the table keeps the scene cohesive, everyone reacts inside the same " +
                    "completion, but the other members are voiced from little more than a name. One call per " +
                    "member gives each their real memory, quests, and regard, at the cost of one round-trip per " +
                    "member and no shared view of the same instant.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> CouncilVoices { get; set; } =
         new(new[] {"One call for the table", "One call per member"}, 0); // default: whole table

      [SettingPropertyBool("Companion contentment", RequireRestart = false, Order = 0,
         HintText = "When ON, a neglected companion grows restless, asks for an audience, and if you keep refusing " +
                    "it, eventually leaves by letter (never silently). Also gates the occasional found-topic " +
                    "audience (a mission report, a party grudge, practical counsel, an old memory, or news of " +
                    "the world). OFF disables both entirely.")]
      [SettingPropertyGroup("Companions", GroupOrder = 6)]
      public bool CompanionContentment { get; set; } = true;

      [SettingPropertyDropdown("Contentment Pacing", RequireRestart = false, Order = 1,
         HintText = "How fast companions get over a grievance: Forgiving, Balanced (default), or Exacting. In every " +
                    "setting, a companion left merely idle NEVER leaves you.")]
      [SettingPropertyGroup("Companions", GroupOrder = 6)]
      public Dropdown<string> ContentmentPacing { get; set; } =
         new(new[] {"Forgiving", "Balanced", "Exacting"}, 1); // default: Balanced

      [SettingPropertyText("API Key (optional)", RequireRestart = false, Order = 0,
         HintText = "Leave blank for keyless local servers like LM Studio or Ollama. Fill it in only if your server " +
                    "requires one. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKey { get; set; } = "";

      [SettingPropertyText("Endpoint URL", RequireRestart = false, Order = 1,
         HintText = "Base URL of your local server, including /v1 (e.g. http://localhost:1234/v1 for LM Studio, " +
                    "http://localhost:11434/v1 for Ollama). Used only when Provider is OpenAI-Compatible.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrl { get; set; } = "";

      [SettingPropertyText("Endpoint URL (NSFW)", RequireRestart = false, Order = 2,
         HintText = "Optional. A separate OpenAI-Compatible server URL for the NSFW role only, so you can split roles " +
                    "across two local machines. Blank = use the shared Endpoint URL above. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrlNsfw { get; set; } = "";

      [SettingPropertyText("Endpoint URL (Interpret)", RequireRestart = false, Order = 3,
         HintText = "Optional. A separate OpenAI-Compatible server URL for the Interpret role only, so the interpreter " +
                    "can run on its own local machine. Blank = use the shared Endpoint URL above. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrlInterpret { get; set; } = "";

      [SettingPropertyDropdown("Parameter Compatibility", RequireRestart = false, Order = 4,
         HintText = "Which sampling parameters to send. Auto detects OpenAI reasoning models (gpt-5, o1/o3/o4), which " +
                    "reject the classic max_tokens. Leave on Auto unless your endpoint rejects a field.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public Dropdown<string> CustomParameterCompatibility { get; set; } =
         new(new[] {"Auto", "Standard", "Reasoning"}, 0); // default: Auto

      [SettingPropertyDropdown("Reasoning Effort", RequireRestart = false, Order = 6,
         HintText = "Controls thinking on a local/OpenAI-Compatible endpoint by sending reasoning_effort on the wire. " +
                    "Set Off (reasoning_effort=none) if a reasoning model like Qwen wastes the whole reply budget " +
                    "thinking and stalls. Default sends nothing, so an untouched setup is unchanged. Some local servers " +
                    "(e.g. vLLM) ignore this and need their own flag; turn on Verbose Logging to see what is sent.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public Dropdown<string> CustomReasoningEffort { get; set; } =
         new(new[] {"Default", "Off", "Minimal", "Low", "Medium", "High"}, 0); // default: Default (send nothing)

      [SettingPropertyText("API Key (NSFW)", RequireRestart = false, Order = 5,
         HintText = "Optional. A separate API key for the NSFW role only, so it can authenticate against a different " +
                    "OpenAI-Compatible server than the shared one. Blank = use the API Key above. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKeyNsfw { get; set; } = "";

      [SettingPropertyText("API Key (Interpret)", RequireRestart = false, Order = 6,
         HintText = "Optional. A separate API key for the Interpret role only, so the interpreter can authenticate " +
                    "against its own OpenAI-Compatible server. Blank = use the API Key above. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKeyInterpret { get; set; } = "";

      public override string DisplayName => "Calradia Remembers";

      [SettingPropertyBool("Enable File Logging", RequireRestart = false, Order = 0,
         HintText = "Writes the mod's diagnostics to CalradiaRemembers.log (only the last two sessions are kept, " +
                    "so the file never grows without bound). Off by default; turn on to capture a bug report.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool EnableLogging { get; set; } = false;

      [SettingPropertyBool("Enable On-Screen Logging", RequireRestart = false, Order = 1,
         HintText = "Also prints the mod's diagnostics as on-screen messages (top-left). Off by default: the " +
                    "stream can crowd the screen during play. Turn it on only when you want to watch the mod live; " +
                    "the file log above is the better channel for a bug report.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool EnableScreenLogging { get; set; } = false;

      [SettingPropertyBool("Enable Jealousy", RequireRestart = false, Order = 0,
         HintText = "On (default): the world is richer, partners react to being favoured or slighted, a neglected " +
                    "co-partner can feel wronged, and open-relationship pacts matter. Off: a drama-free household, " +
                    "no jealousy of any kind, from any partner.")]
      [SettingPropertyGroup("Relationships", GroupOrder = 8)]
      public bool EnableJealousy { get; set; } = true;

      [SettingPropertyBool("Enable Stolen Love", RequireRestart = false, Order = 1,
         HintText = "Off (default): a married character resists an affair as before (deep trust alone can open one). " +
                    "On: seducing a MARRIED character becomes a gated, personality-true path (an honourable or " +
                    "happily married one holds firm however high your standing), the groundwork for luring them away " +
                    "from their spouse. Explicit content still follows the Adult Content Level.")]
      [SettingPropertyGroup("Relationships", GroupOrder = 8)]
      public bool EnableStolenLove { get; set; } = false;

      [SettingPropertyBool("Post-duel conversation", RequireRestart = false, Order = 0,
         HintText = "On (default): after a duel is fought, the conversation reopens over the scene and resumes what " +
                    "led to it. The loser concedes (a beaten character of honour may grant a favour you ask, such as " +
                    "keeping away from someone); the victor makes plain what they now expect. Off: a fought duel ends " +
                    "with only its consequences applied, no conversation.")]
      [SettingPropertyGroup("Duels", GroupOrder = 9)]
      public bool EnablePostDuelConversation { get; set; } = true;

      public override string FolderName => "CalradiaRemembers";
      public override string FormatType => "json2";
      public override string Id => "CalradiaRemembers";

      [SettingPropertyBool("Compact prompt for small models", RequireRestart = false, Order = 12,
         HintText = "Trims the prompt to fit a SMALL, short-context model (4k to 8k). Turn ON if the AI stays silent: " +
                    "the full prompt is overflowing it. Replies grow less richly grounded. Leave OFF for cloud models.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public bool LeanPrompt { get; set; } = false;

      [SettingPropertyBool("Compress old memories", RequireRestart = false, Order = 13,
         HintText = "Lets characters fold old, superseded memories into a short background outline, so a long " +
                    "campaign stops inflating every prompt (the usual cause of slow replies on a local model). " +
                    "First meetings, recent events, and grave moments are never dropped. Turn OFF if another mod " +
                    "manages this character's memory for you.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public bool MemoryCompression { get; set; } = true;

      // Phase 2: the hard-failure fallback is the NSFW model, so this legacy field is no longer shown in MCM or
      // read anywhere. Kept as a plain (un-attributed) property so an old stored value loads without orphaning.
      public string ProseFallbackModel { get; set; } = "";

      [SettingPropertyInteger("LLM Response Timeout (s)", 15, 600, "0 's'", RequireRestart = false, Order = 11,
         HintText = "How long to wait for a reply, in seconds (default 120). Raise it for a slow local or reasoning " +
                    "model; lower it to fail fast. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public int LlmTimeoutSeconds { get; set; } = 120;

      [SettingPropertyText("PROSE model", RequireRestart = false, Order = 2,
         HintText = "Model id for the everyday PROSE model, in the ACTIVE provider's own format (OpenRouter: " +
                    "x-ai/grok-4.20; NanoGPT: deepseek-ai/DeepSeek-V3; Ollama: llama3.1:8b). It writes ordinary chat " +
                    "and, in Enhanced mode, the prose of every scene. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public string Model { get; set; } = "x-ai/grok-4.20";

      // Per-role provider (2026-08-13): each model role may go to its OWN provider. The roleplay / prose role is
      // the BASE: it is the Provider dropdown below (property name kept for save compatibility), presented right
      // here as "Roleplay / Prose provider". The other three roles keep a "(default)" option that inherits this
      // base, so an upgraded config behaves exactly as before and there is no redundant global provider field.
      [SettingPropertyDropdown("Roleplay / Prose provider", RequireRestart = false, Order = 3,
         HintText = "Which provider the roleplay and prose model above is sent to. It is also the base every other " +
                    "role inherits when left on (default). Each provider keeps its own key further down. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> Provider { get; set; } =
         new(new[] {ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0); // default: OpenRouter

      [SettingPropertyText("NanoGPT API Key", RequireRestart = false, Order = 0,
         HintText = "Paste your key from nano-gpt.com. Stored locally on this machine only. NEVER share your config " +
                    "file or a screenshot of this field.")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 2)]
      public string NanoGptApiKey { get; set; } = "";

      [SettingPropertyDropdown("Chat Composition", RequireRestart = false, Order = 1,
         HintText = "The DEFAULT for the in-chat Enhanced/Roleplay/NSFW switch (always visible above the chat " +
                    "input): Enhanced (default) seeds the switch on Enhanced - the PROSE model writes, the " +
                    "INTERPRET model tags, the NSFW model is a hard-failure fallback - richest, but more tokens " +
                    "and slower. Standard seeds it on Roleplay - one integrated call on the PROSE model, no " +
                    "interpret, no fallback - cheapest and fastest. Cycling the in-chat switch overrides this for " +
                    "the rest of the session; this dropdown only sets where a fresh session starts.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> ChatCompositionModeSetting { get; set; } =
         new(new[] {ChatCompositionModePolicy.StandardLabel, ChatCompositionModePolicy.EnhancedLabel}, 1); // default: Enhanced

      // Phase 2: consensual scenes are now "chat" (the PROSE model), so this separate consensual model and its
      // provider are no longer shown in MCM or read anywhere. Kept as plain (un-attributed) properties, still named
      // NsfwModel for its legacy save-identity, so an old stored value loads without orphaning.
      public string NsfwModel { get; set; } = "";

      public Dropdown<string> ConsensualProvider { get; set; } =
         new(new[] {ProviderRolePolicy.DefaultSelection, ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0);

      [SettingPropertyText("NSFW model", RequireRestart = false, Order = 6,
         HintText = "Model id for explicit content: captivity scenes in Standard mode, and the hard-failure fallback " +
                    "in Enhanced mode (when the PROSE model truncates, empties, or refuses a beat). Default " +
                    "x-ai/grok-4.20. Blank = use the PROSE model.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public string CncModel { get; set; } = "x-ai/grok-4.20";

      [SettingPropertyDropdown("NSFW provider", RequireRestart = false, Order = 7,
         HintText = "Which provider the NSFW model above is sent to. (default) uses the PROSE provider. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> CncProvider { get; set; } =
         new(new[] {ProviderRolePolicy.DefaultSelection, ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0);

      [SettingPropertyText("Interpret model", RequireRestart = false, Order = 8,
         HintText = "Enhanced mode only: the model that reads the structured actions out of the prose. Default " +
                    "x-ai/grok-4.20. Blank = use the PROSE model.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public string ActionInterpreterModel { get; set; } = "x-ai/grok-4.20";

      [SettingPropertyDropdown("Interpret provider", RequireRestart = false, Order = 9,
         HintText = "Which provider the Interpret model above is sent to. (default) uses the PROSE provider (set it " +
                    "to a local OpenAI-Compatible endpoint to run the interpreter locally). Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> ActionInterpreterProvider { get; set; } =
         new(new[] {ProviderRolePolicy.DefaultSelection, ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0);

      [SettingPropertyDropdown("Actions Interpreter Timing", RequireRestart = false, Order = 10,
         HintText = "Only in Prose + Interpreter mode. Synchronous: tags apply with the reply (recommended). " +
                    "Asynchronous: the reply shows instantly, tags apply a moment later.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> ActionInterpreterTimingSetting { get; set; } =
         new(new[] {ActionInterpreterTimingPolicy.SynchronousLabel, ActionInterpreterTimingPolicy.AsynchronousLabel}, 0); // default: Synchronous

      [SettingPropertyDropdown("Reasoning Effort (NSFW)", RequireRestart = false, Order = 2,
         HintText = "Reasoning effort while the chat is in NSFW mode. Lower or Off makes a reasoning model far less " +
                    "prone to moralizing refusals, and speeds up scenes. OpenRouter and NanoGPT.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Dropdown<string> NsfwReasoningEffort { get; set; } =
         new(new[] {ModelModePolicy.SameAsProse, "Default", "Off", "Minimal", "Low", "Medium", "High"}, 0); // default: Same as roleplay

      [SettingPropertyDropdown("Reasoning Effort (PROSE)", RequireRestart = false, Order = 1,
         HintText = "How hard the PROSE model thinks before replying. Higher can mean finer dialogue but slower " +
                    "replies, more truncation, and MORE refusals on adult content - and in Enhanced mode the PROSE " +
                    "model writes captivity scenes too, so keep it LOW or Medium. Default leaves it to the model. OpenRouter and NanoGPT.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Dropdown<string> ReasoningEffort { get; set; } =
         new(new[] {"Default", "Off", "Minimal", "Low", "Medium", "High"}, 3); // default: Low

      [SettingPropertyDropdown("Relationship Pacing", RequireRestart = false, Order = 1,
         HintText = "How relationships grow through conversation: a philosophy, not a difficulty. Grounded keeps the " +
                    "core game's feel; Warm rewards good company; Storyteller is roleplay-first. Insults cost the " +
                    "same everywhere.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> RelationshipWarmth { get; set; } =
         new(new[] {"Grounded", "Warm", "Storyteller"}, 0); // default: Grounded (= Normal)

      [SettingPropertyText("Reply Language", RequireRestart = false, Order = 3,
         HintText = "Leave blank to auto-detect and mirror whatever language you write in. Name a language " +
                    "(e.g. Spanish) to force every reply, letter, memory, and the illuminated chronicle into " +
                    "it, no matter what you write.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public string ReplyLanguage { get; set; } = "";

      [SettingPropertyDropdown("Lords Approaching You", RequireRestart = false, Order = 2,
         HintText = "How often a lord rides out to have a word with you on the road. Never switches it off " +
                    "entirely. Occasionally is the shipped feel.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> InterceptionPacing { get; set; } =
         new(new[] {"Never", "Rarely", "Occasionally", "Often"}, 2); // default: Occasionally (today's behaviour)

      // Player report (2026-08-05): in a scene with several NPCs, they kept talking to each other for a long
      // stretch before he could get a word in. This dial caps how many NPC-to-NPC exchanges fire per player
      // turn (was a hard-coded 3). 2 is the new shipped feel; 0 turns cross-talk off completely. See
      // CalradiaRemembers.Chat.ChatViewModel (WitnessExchangeCap) and ModSettings.NpcCrossTalkRounds.
      [SettingPropertyInteger("NPC Cross-Talk", 0, 3, "0", RequireRestart = false, Order = 10,
         HintText = "How many times NPCs may talk amongst themselves (one reacting to another) before the turn " +
                    "comes back to you, in a scene with witnesses. 2 is the default; 0 turns it off so only the " +
                    "NPC you spoke to replies. Applies on your next message.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public int NpcCrossTalkRounds { get; set; } = 2;

      [SettingPropertyDropdown("Letters from NPCs", RequireRestart = false, Order = 6,
         HintText = "How many unprompted letters NPCs send you. Fewer keeps only what matters; the mod also learns " +
                    "from which letters you answer versus dismiss. Applies immediately.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> LetterPacing { get; set; } =
         new(new[] {"Rarely", "Occasionally", "Often"}, 1); // default: Occasionally

      // Player report (2026-08-05): letters get lost too often (almost every time) when the sender is far. The
      // per-band waylay roll compounds over a long trip. A distance-independent cap now blunts that; this toggle
      // is the total opt-out for a player who never wants a letter intercepted at all. Off = today's behaviour.
      [SettingPropertyBool("Invulnerable Couriers", RequireRestart = false, Order = 11,
         HintText = "Off: your letter couriers can be waylaid by bandits on the road (as advertised). On: your " +
                    "letters always get through, no interception.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public bool InvulnerableCouriers { get; set; } = false;

      // Intercepted-letter-leak Inc 1 (player report 2026-08-24): OFF ships today's behaviour (a waylaid letter is
      // simply lost). On: a bandit-waylaid letter of real substance has a bounded chance its contents fall into a
      // hostile hand, who writes to the player to exploit them.
      [SettingPropertyBool("Intercepted letters can leak", RequireRestart = false, Order = 12,
         HintText = "Off (default): a letter waylaid by bandits is simply lost. On: a substantive intercepted letter " +
                    "has a chance its contents fall into a hostile lord's hands, who then writes to exploit them.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public bool InterceptedLettersCanLeak { get; set; } = false;

      // Player request (2026-08-04, "AI Executes" mod parity): a captive under torture wanted the scene able to
      // end their character for real. High-stakes and IRREVERSIBLE (game over, or heir succession), so this is
      // opt-in like AdultContentLevel/Hardcore, off by default, and re-validated by the bridge at the moment of
      // execution regardless of what this reads (see CalradiaRemembers.Logic.PlayerExecutionPolicy).
      [SettingPropertyBool("Allow Captor Executions", RequireRestart = false, Order = 8,
         HintText = "Let a captor's scene end your character, at their own deliberate choice, deep in a Hardcore " +
                    "captive scene. Off by default. Irreversible: game over, or your heir succeeds you.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public bool AllowCaptorExecutions { get; set; } = false;

      // COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): a king promised a fief and marriage to himself as
      // the reward of a quest, and both evaporated on completion (no executor existed). Off by default so the
      // existing guardrail (never promise a fief or marriage as the price of a task) stays fully intact for
      // everyone who never touches this menu; on, the guardrail relaxes AND the reward is genuinely delivered
      // (or honestly degrades) on completion, re-validated at both issuance and payout.
      [SettingPropertyBool("Allow Fief And Marriage Quest Rewards", RequireRestart = false, Order = 9,
         HintText = "Let a lord promise one of their own fiefs, or a marriage into their house, as a quest " +
                    "reward, delivered when you complete it. Off by default.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public bool AllowFiefAndMarriageQuestRewards { get; set; } = false;

      [SettingPropertyBool("Story Log", RequireRestart = false, Order = 2,
         HintText = "Writes a clean narrative transcript of every conversation and letter to " +
                    "CalradiaRemembers_story.log in your Logs folder. No timestamps, no noise: it reads like a novel.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool StoryLogging { get; set; } = false;

      [SettingPropertyBool("Report unsupported actions", RequireRestart = false, Order = 4,
         HintText = "When on, shows a note in chat when the AI attempts an action the mod does not support yet. " +
                    "Every such attempt is logged to CalradiaRemembers_unknown_actions.log regardless of this " +
                    "setting. Send that log to the mod author (LogRaam) on Nexus to request that the action be " +
                    "supported in a future update.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool ReportUnsupportedActions { get; set; } = false;

      [SettingPropertyButton("Test Connection", RequireRestart = false, Order = 3,
         HintText = "Validates your key, then sends a one-token test to the roleplay model and, if set, the consensual " +
                    "model, reporting any error. Costs a fraction of a cent.",
         Content = "Test")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Action TestConnection { get; set; } = ApiKeyTester.Run;

      [SettingPropertyButton("Test Endpoint", RequireRestart = false, Order = 5,
         HintText = "Sends a one-token test to your endpoint with the roleplay model and, if set, the consensual model. " +
                    "Your server must be running. OpenAI-Compatible only.",
         Content = "Test")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public Action TestCustomConnection { get; set; } = ApiKeyTester.RunCustom;

      [SettingPropertyButton("Test NanoGPT Connection", RequireRestart = false, Order = 1,
         HintText = "Checks your NanoGPT key against nano-gpt.com without starting a conversation. No tokens spent.",
         Content = "Test")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 2)]
      public Action TestNanoGptConnection { get; set; } = ApiKeyTester.RunNanoGpt;

      [SettingPropertyBool("Use custom traits", RequireRestart = false, Order = 0,
         HintText = "Applies the hand-authored personalities from ModuleData/character_overrides.json, replacing an " +
                    "authored NPC's five vanilla traits. Edits load on the next campaign, or via cr.reload_lore.")]
      [SettingPropertyGroup("Character", GroupOrder = 5)]
      public bool UseCustomTraits { get; set; } = false;

      [SettingPropertyText("Custom Folder Path", RequireRestart = false, Order = 2,
         HintText = "Where your edited prompts and styles are read from. BLANK = the default (the mod's own " +
                    "ModuleData/Custom). Set an absolute folder path to keep them OUTSIDE the mod so an update " +
                    "never wipes them (just re-enter it after an update); also lets you keep several prompt sets " +
                    "and switch. Applies on the next campaign or cr.reload_lore.")]
      [SettingPropertyGroup("Character", GroupOrder = 5)]
      public string CustomFolderPath { get; set; } = "";

      [SettingPropertyBool("Verbose Logging", RequireRestart = false, Order = 1,
         HintText = "Logs the full system prompt and NPC reply on every exchange. Turn on only to capture a bug " +
                    "report: it floods the log. Requires Enable Logging.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool VerboseLogging { get; set; } = false;

      // Player complaint: conversations are ALWAYS crowded with ambient bystanders. Thins the AMBIENT hero
      // witnesses a conversation gathers only; never the player's own companions (see
      // CalradiaRemembers.Logic.WitnessPresencePolicy + WitnessResolver.ResolveAutoWitnesses). Default Bustling
      // is an EXACT passthrough (p=1.0, cap=int.MaxValue): the mod is LIVE, so an existing player who never opens
      // this menu sees precisely today's room, unchanged.
      [SettingPropertyDropdown("Witness Density", RequireRestart = false, Order = 12,
         HintText = "How many ambient bystanders a conversation gathers. Sparse thins the room fast; Normal keeps " +
                    "a genuine middle ground; Bustling (default) is today's full room, unchanged. Never touches " +
                    "companions you bring yourself. Applies on your next conversation.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> WitnessDensity { get; set; } =
         new(new[] {"Sparse", "Normal", "Bustling"}, 2); // default: Bustling (today's live behaviour, unchanged)
   }
}