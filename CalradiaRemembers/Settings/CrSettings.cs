// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

#endregion

namespace CalradiaRemembers.Settings
{
   /// <summary>
   ///   MCM (Mod Configuration Menu) global settings for Calradia Remembers.
   ///   Stored per-machine under <c>Configs/ModSettings/Global</c> — deliberately NOT
   ///   in the save file, so a shared save never carries the player's API key.
   ///   Read through the <see cref="ModSettings" /> façade, which falls back to safe
   ///   defaults when MCM has not yet loaded.
   ///   Menu layout (GroupOrder): the shared LLM Provider group first (provider choice,
   ///   the two model fields, timeout, compact prompt), then one connection section per
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

      [SettingPropertyText("OpenRouter API Key", RequireRestart = false, Order = 0,
         HintText = "Paste your OpenRouter API key from openrouter.ai/keys. Stored locally on this machine " +
                    "only — never share your config file or a screenshot of this field. Applies immediately.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public string ApiKey { get; set; } = "";

      [SettingPropertyText("API Key (optional)", RequireRestart = false, Order = 0,
         HintText = "API key for the endpoint — leave blank for keyless local servers like LM Studio or Ollama " +
                    "(their default config requires no authentication). Fill it in only if your server is " +
                    "configured to require one. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKey { get; set; } = "";

      [SettingPropertyText("Endpoint URL", RequireRestart = false, Order = 1,
         HintText = "Base URL of your local OpenAI-compatible server, including the /v1 path " +
                    "(e.g. http://localhost:1234/v1 for LM Studio, http://localhost:11434/v1 for Ollama, " +
                    "http://localhost:8000/v1 for vLLM). The mod appends /chat/completions automatically. " +
                    "Used only when Provider is set to OpenAI-Compatible. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrl { get; set; } = "";

      [SettingPropertyDropdown("Parameter Compatibility", RequireRestart = false, Order = 2,
         HintText = "Which sampling parameters the request sends. Auto detects OpenAI reasoning models " +
                    "(gpt-5*, o1/o3/o4*) and switches them to 'max_completion_tokens' with no temperature — " +
                    "they reject the classic 'max_tokens'. Standard forces the classic shape (max_tokens + " +
                    "temperature); Reasoning forces the reasoning shape. Leave on Auto unless your endpoint " +
                    "rejects a field. Used only when Provider is set to OpenAI-Compatible. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public Dropdown<string> CustomParameterCompatibility { get; set; } =
         new(new[] {"Auto", "Standard", "Reasoning"}, 0); // default: Auto

      public override string DisplayName => "Calradia Remembers";

      [SettingPropertyBool("Enable Logging", RequireRestart = false, Order = 0,
         HintText = "Master switch for all mod logging — both the on-screen messages and the " +
                    "CalradiaRemembers.log file. Off by default; turn on only to capture a bug report.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool EnableLogging { get; set; } = false;

      public override string FolderName => "CalradiaRemembers";
      public override string FormatType => "json2";
      public override string Id => "CalradiaRemembers";

      [SettingPropertyText("Model (roleplay)", RequireRestart = false, Order = 1,
         HintText = "Model id used for ordinary conversations, in the ACTIVE provider's own format: " +
                    "OpenRouter e.g. x-ai/grok-4.20 (browse openrouter.ai/models); NanoGPT e.g. " +
                    "deepseek-ai/DeepSeek-V3 (browse nano-gpt.com/models); LM Studio e.g. " +
                    "lmstudio-community/Meta-Llama-3.1-8B-Instruct-GGUF; Ollama e.g. llama3.1:8b. " +
                    "Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public string Model { get; set; } = "x-ai/grok-4.20";

      [SettingPropertyText("NSFW model (optional)", RequireRestart = false, Order = 2,
         HintText = "Model id used automatically in captive and other explicit scenes, and whenever you flip " +
                    "the in-chat mode button to NSFW (for example, a consensual scene that escalates). Same " +
                    "format as the roleplay model above, same provider. Leave empty to always use the " +
                    "roleplay model. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public string NsfwModel { get; set; } = "";

      [SettingPropertyText("NanoGPT API Key", RequireRestart = false, Order = 0,
         HintText = "Paste your NanoGPT API key from nano-gpt.com. Stored locally on this machine only — " +
                    "never share your config file or a screenshot of this field. Used only when Provider " +
                    "is set to NanoGPT. Applies immediately.")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 2)]
      public string NanoGptApiKey { get; set; } = "";

      [SettingPropertyDropdown("LLM Provider", RequireRestart = false, Order = 0,
         HintText = "Which OpenAI-compatible service to send requests to. The model fields below are shared: " +
                    "paste ids in the active provider's format. Each provider keeps its own API key and " +
                    "connection settings in its section further down. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> Provider { get; set; } =
         new(new[] {"OpenRouter", "NanoGPT", "OpenAI-Compatible"}, 0); // default: OpenRouter

      [SettingPropertyDropdown("Reasoning Effort (roleplay)", RequireRestart = false, Order = 1,
         HintText = "How hard a reasoning model 'thinks' before replying, for ordinary conversations (the " +
                    "roleplay model). Default leaves it to the model; Off disables reasoning; " +
                    "Minimal/Low/Medium/High set the effort. Higher can mean finer dialogue but slower " +
                    "replies. OpenRouter only. Applies immediately.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Dropdown<string> ReasoningEffort { get; set; } =
         new(new[] {"Default", "Off", "Minimal", "Low", "Medium", "High"}, 3); // default: Low

      [SettingPropertyDropdown("Reasoning Effort (NSFW)", RequireRestart = false, Order = 2,
         HintText = "Reasoning effort used while the chat is in NSFW mode. Lower or Off makes reasoning models " +
                    "markedly less prone to moralizing refusals on adult/CNC content, and speeds up scene " +
                    "beats. 'Same as roleplay' (the default) reuses the setting above. OpenRouter only. " +
                    "Applies immediately.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Dropdown<string> NsfwReasoningEffort { get; set; } =
         new(new[] {CalradiaRemembers.Logic.ModelModePolicy.SameAsRoleplay, "Default", "Off", "Minimal", "Low", "Medium", "High"}, 0); // default: Same as roleplay

      [SettingPropertyDropdown("Relationship Pacing", RequireRestart = false, Order = 1,
         HintText = "How relationships grow through conversation — a philosophy, not a difficulty. " +
                    "Grounded keeps the core game's feel: trust is earned slowly, mostly by deeds; " +
                    "flattery rarely moves a lord. Warm lets good company — humor, sympathy, sincere " +
                    "praise — earn small gains more often. Storyteller is a roleplay-first experience: " +
                    "personal moments, narrative effort and real connection move hearts readily. " +
                    "Affects only positive regard — insults cost the same everywhere. " +
                    "Applies on your next conversation.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> RelationshipWarmth { get; set; } =
         new(new[] {"Grounded", "Warm", "Storyteller"}, 0); // default: Grounded (= Normal)

      [SettingPropertyBool("Story Log", RequireRestart = false, Order = 2,
         HintText = "Writes a clean narrative transcript of every conversation and letter to " +
                    "CalradiaRemembers_story.log in your Bannerlord Logs folder. " +
                    "No timestamps or technical noise — reads like a novel.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool StoryLogging { get; set; } = false;

      [SettingPropertyButton("Test Connection", RequireRestart = false, Order = 3,
         HintText = "Checks your API key AND every configured model from the LLM Provider section: validates " +
                    "the key, then sends a one-token test prompt to the roleplay model and, if set, the NSFW " +
                    "model, reporting any error (e.g. a misspelled model id). Costs a fraction of a cent.",
         Content = "Test")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Action TestConnection { get; set; } = ApiKeyTester.Run;

      [SettingPropertyButton("Test Endpoint", RequireRestart = false, Order = 3,
         HintText = "Sends a one-token test completion to your endpoint with the roleplay model and, if set, " +
                    "the NSFW model from the LLM Provider section. The server must be running — the test makes " +
                    "no external calls. Used only when Provider is set to OpenAI-Compatible.",
         Content = "Test")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public Action TestCustomConnection { get; set; } = ApiKeyTester.RunCustom;

      [SettingPropertyButton("Test NanoGPT Connection", RequireRestart = false, Order = 1,
         HintText = "Checks your NanoGPT key against nano-gpt.com without starting a conversation — no tokens spent.",
         Content = "Test")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 2)]
      public Action TestNanoGptConnection { get; set; } = ApiKeyTester.RunNanoGpt;

      [SettingPropertyBool("Use custom traits", RequireRestart = false, Order = 0,
         HintText = "Apply the hand-authored personalities from ModuleData/character_overrides.json. " +
                    "When on, an authored NPC's five personality traits replace their vanilla ones " +
                    "(driving the prompt, derived features, and the campaign AI alike). Turn off to " +
                    "restore the vanilla traits. Edits to the file load on the next campaign or via cr.reload_lore.")]
      [SettingPropertyGroup("Character", GroupOrder = 5)]
      public bool UseCustomTraits { get; set; } = false;

      [SettingPropertyBool("Companion contentment", RequireRestart = false, Order = 0,
         HintText = "When ON, companions left idle slowly grow restless; an unhappy one asks for an audience, and " +
                    "if you keep refusing it they eventually leave by farewell letter (never silently). Turn OFF " +
                    "to disable companion contentment entirely — no restlessness, no audiences, no departures.")]
      [SettingPropertyGroup("Companions", GroupOrder = 6)]
      public bool CompanionContentment { get; set; } = true;

      [SettingPropertyInteger("LLM Response Timeout (s)", 15, 600, "0 's'", RequireRestart = false, Order = 3,
         HintText = "How long to wait for the AI to reply before giving up, in seconds (default 120). Raise it " +
                    "if you run a slow local model or a reasoning model and see 'the model did not respond' on " +
                    "longer messages; lower it if you prefer to fail fast. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public int LlmTimeoutSeconds { get; set; } = 120;

      [SettingPropertyBool("Compact prompt for small models", RequireRestart = false, Order = 4,
         HintText = "Trims the system prompt so it fits a SMALL / short-context model (roughly 4k-8k tokens, e.g. a " +
                    "little local model in LM Studio / Ollama). Turn ON if the AI stays silent or you see '(My words " +
                    "escape me…)' — the full prompt is overflowing your model's context. It drops the heavy flavour " +
                    "sections (relationships, world lore, cultural background, long behaviour guidelines) and keeps " +
                    "less memory, so replies are a little less richly grounded. Leave OFF for large / cloud models. " +
                    "Applies on your next conversation.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public bool LeanPrompt { get; set; } = false;

      [SettingPropertyBool("Verbose Logging", RequireRestart = false, Order = 1,
         HintText = "Logs the full system prompt and NPC response to the mod log on every exchange. " +
                    "Turn on only when capturing a bug report — it floods the log. Requires Enable Logging.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool VerboseLogging { get; set; } = false;
   }
}
