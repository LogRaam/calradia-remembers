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
   /// </summary>
   public sealed class CrSettings : AttributeGlobalSettings<CrSettings>
   {
      [SettingPropertyDropdown("Adult Content Level", RequireRestart = false, Order = -1,
         HintText = "How much romantic/intimate content NPCs may express. Off, Mature (romance, " +
                    "nothing explicit), Explicit, or Hardcore. Applies on your next conversation.")]
      [SettingPropertyGroup("Content", GroupOrder = 2)]
      public Dropdown<string> AdultContentLevel { get; set; } =
         new(new[] {"Off", "Mature", "Explicit", "Hardcore"}, 1); // default: Mature

      [SettingPropertyText("OpenRouter API Key", RequireRestart = false, Order = -1,
         HintText = "Paste your OpenRouter API key from openrouter.ai/keys. Stored locally on this machine " +
                    "only — never share your config file or a screenshot of this field. Applies immediately.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 3)]
      public string ApiKey { get; set; } = "";

      [SettingPropertyText("API Key (optional)", RequireRestart = false, Order = -2,
         HintText = "API key for the endpoint — leave blank for keyless local servers like LM Studio or Ollama " +
                    "(their default config requires no authentication). Fill it in only if your server is " +
                    "configured to require one. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 5)]
      public string CustomApiKey { get; set; } = "";

      [SettingPropertyText("Endpoint URL", RequireRestart = false, Order = -1,
         HintText = "Base URL of your local OpenAI-compatible server, including the /v1 path " +
                    "(e.g. http://localhost:1234/v1 for LM Studio, http://localhost:11434/v1 for Ollama, " +
                    "http://localhost:8000/v1 for vLLM). The mod appends /chat/completions automatically. " +
                    "Used only when Provider is set to OpenAI-Compatible. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 5)]
      public string CustomEndpointUrl { get; set; } = "";

      [SettingPropertyText("Model", RequireRestart = false, Order = -3,
         HintText = "Model id exactly as your server expects it " +
                    "(e.g. lmstudio-community/Meta-Llama-3.1-8B-Instruct-GGUF for LM Studio, " +
                    "llama3.1:8b for Ollama). Used only when Provider is set to OpenAI-Compatible. Applies immediately.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 5)]
      public string CustomModel { get; set; } = "";

      public override string DisplayName => "Calradia Remembers";

      [SettingPropertyBool("Enable Logging", RequireRestart = false, Order = 0,
         HintText = "Master switch for all mod logging — both the on-screen messages and the " +
                    "CalradiaRemembers.log file. Off by default; turn on only to capture a bug report.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
      public bool EnableLogging { get; set; } = false;

      public override string FolderName => "CalradiaRemembers";
      public override string FormatType => "json2";
      public override string Id => "CalradiaRemembers";

      [SettingPropertyText("Model", RequireRestart = false, Order = -2,
         HintText = "OpenRouter model id (e.g. x-ai/grok-4.20, anthropic/claude-sonnet-4, openai/gpt-4o). " +
                    "Browse models at openrouter.ai/models. Applies immediately.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 3)]
      public string Model { get; set; } = "x-ai/grok-4.20";

      [SettingPropertyText("NanoGPT API Key", RequireRestart = false, Order = -1,
         HintText = "Paste your NanoGPT API key from nano-gpt.com. Stored locally on this machine only — " +
                    "never share your config file or a screenshot of this field. Used only when Provider " +
                    "is set to NanoGPT. Applies immediately.")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 4)]
      public string NanoGptApiKey { get; set; } = "";

      [SettingPropertyText("NanoGPT Model", RequireRestart = false, Order = -2,
         HintText = "NanoGPT model id. Browse the catalog at nano-gpt.com/models and paste the exact id " +
                    "(e.g. deepseek-ai/DeepSeek-V3). Used only when Provider is set to NanoGPT. Applies immediately.")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 4)]
      public string NanoGptModel { get; set; } = "";

      [SettingPropertyDropdown("LLM Provider", RequireRestart = false, Order = 0,
         HintText = "Which OpenAI-compatible service to send requests to. Each provider has its own " +
                    "API key and model field below — switching does not require re-pasting. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> Provider { get; set; } =
         new(new[] {"OpenRouter", "NanoGPT", "OpenAI-Compatible"}, 0); // default: OpenRouter

      [SettingPropertyDropdown("Reasoning Effort", RequireRestart = false, Order = 1,
         HintText = "How hard a reasoning model (e.g. Grok) 'thinks' before replying. Lower or Off makes Grok " +
                    "markedly less prone to moralizing refusals on adult/CNC content. Default leaves it to the " +
                    "model; Off disables reasoning; Minimal/Low/Medium/High set the effort. OpenRouter only. " +
                    "Applies immediately.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 3)]
      public Dropdown<string> ReasoningEffort { get; set; } =
         new(new[] {"Default", "Off", "Minimal", "Low", "Medium", "High"}, 3); // default: Low

      [SettingPropertyDropdown("Relationship Pacing", RequireRestart = false, Order = 0,
         HintText = "How relationships grow through conversation — a philosophy, not a difficulty. " +
                    "Grounded keeps the core game's feel: trust is earned slowly, mostly by deeds; " +
                    "flattery rarely moves a lord. Warm lets good company — humor, sympathy, sincere " +
                    "praise — earn small gains more often. Storyteller is a roleplay-first experience: " +
                    "personal moments, narrative effort and real connection move hearts readily. " +
                    "Affects only positive regard — insults cost the same everywhere. " +
                    "Applies on your next conversation.")]
      [SettingPropertyGroup("Content", GroupOrder = 2)]
      public Dropdown<string> RelationshipWarmth { get; set; } =
         new(new[] {"Grounded", "Warm", "Storyteller"}, 0); // default: Grounded (= Normal)

      [SettingPropertyBool("Story Log", RequireRestart = false, Order = -2,
         HintText = "Writes a clean narrative transcript of every conversation and letter to " +
                    "CalradiaRemembers_story.log in your Bannerlord Logs folder. " +
                    "No timestamps or technical noise — reads like a novel.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
      public bool StoryLogging { get; set; } = false;

      [SettingPropertyButton("Test Connection", RequireRestart = false, Order = -3,
         HintText = "Checks your API key AND the Model id above: validates the key, then sends a one-token " +
                    "test prompt to the model and reports any error (e.g. a misspelled model id). Costs a " +
                    "fraction of a cent.",
         Content = "Test")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 3)]
      public Action TestConnection { get; set; } = ApiKeyTester.Run;

      [SettingPropertyButton("Test Endpoint", RequireRestart = false, Order = -4,
         HintText = "Sends a one-token test completion to your endpoint with the model above. " +
                    "The server must be running — the test makes no external calls. " +
                    "Used only when Provider is set to OpenAI-Compatible.",
         Content = "Test")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 5)]
      public Action TestCustomConnection { get; set; } = ApiKeyTester.RunCustom;

      [SettingPropertyButton("Test NanoGPT Connection", RequireRestart = false, Order = -3,
         HintText = "Checks your NanoGPT key against nano-gpt.com without starting a conversation — no tokens spent.",
         Content = "Test")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 4)]
      public Action TestNanoGptConnection { get; set; } = ApiKeyTester.RunNanoGpt;

      [SettingPropertyBool("Use custom traits", RequireRestart = false, Order = 0,
         HintText = "Apply the hand-authored personalities from ModuleData/character_overrides.json. " +
                    "When on, an authored NPC's five personality traits replace their vanilla ones " +
                    "(driving the prompt, derived features, and the campaign AI alike). Turn off to " +
                    "restore the vanilla traits. Edits to the file load on the next campaign or via cr.reload_lore.")]
      [SettingPropertyGroup("Character", GroupOrder = 3)]
      public bool UseCustomTraits { get; set; } = false;

      [SettingPropertyBool("Verbose Logging", RequireRestart = false, Order = -1,
         HintText = "Logs the full system prompt and NPC response to the mod log on every exchange. " +
                    "Turn on only when capturing a bug report — it floods the log. Requires Enable Logging.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
      public bool VerboseLogging { get; set; } = false;
   }
}