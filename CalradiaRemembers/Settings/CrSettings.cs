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

      [SettingPropertyBool("Companion contentment", RequireRestart = false, Order = 0,
         HintText = "When ON, a neglected companion grows restless, asks for an audience, and if you keep refusing " +
                    "it, eventually leaves by letter (never silently). OFF disables the system entirely.")]
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

      [SettingPropertyDropdown("Parameter Compatibility", RequireRestart = false, Order = 2,
         HintText = "Which sampling parameters to send. Auto detects OpenAI reasoning models (gpt-5, o1/o3/o4), which " +
                    "reject the classic max_tokens. Leave on Auto unless your endpoint rejects a field.")]
      [SettingPropertyGroup("OpenAI-Compatible", GroupOrder = 3)]
      public Dropdown<string> CustomParameterCompatibility { get; set; } =
         new(new[] {"Auto", "Standard", "Reasoning"}, 0); // default: Auto

      public override string DisplayName => "Calradia Remembers";

      [SettingPropertyBool("Enable Logging", RequireRestart = false, Order = 0,
         HintText = "Master switch for all mod logging, both the on-screen messages and CalradiaRemembers.log. " +
                    "Off by default; turn on only to capture a bug report.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool EnableLogging { get; set; } = false;

      public override string FolderName => "CalradiaRemembers";
      public override string FormatType => "json2";
      public override string Id => "CalradiaRemembers";

      [SettingPropertyBool("Compact prompt for small models", RequireRestart = false, Order = 4,
         HintText = "Trims the prompt to fit a SMALL, short-context model (4k to 8k). Turn ON if the AI stays silent: " +
                    "the full prompt is overflowing it. Replies grow less richly grounded. Leave OFF for cloud models.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public bool LeanPrompt { get; set; } = false;

      [SettingPropertyInteger("LLM Response Timeout (s)", 15, 600, "0 's'", RequireRestart = false, Order = 3,
         HintText = "How long to wait for a reply, in seconds (default 120). Raise it for a slow local or reasoning " +
                    "model; lower it to fail fast. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public int LlmTimeoutSeconds { get; set; } = 120;

      [SettingPropertyText("Model (roleplay)", RequireRestart = false, Order = 1,
         HintText = "Model id for ordinary conversations, in the ACTIVE provider's own format (OpenRouter: " +
                    "x-ai/grok-4.20; NanoGPT: deepseek-ai/DeepSeek-V3; Ollama: llama3.1:8b). Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public string Model { get; set; } = "x-ai/grok-4.20";

      [SettingPropertyText("NanoGPT API Key", RequireRestart = false, Order = 0,
         HintText = "Paste your key from nano-gpt.com. Stored locally on this machine only. NEVER share your config " +
                    "file or a screenshot of this field.")]
      [SettingPropertyGroup("NanoGPT", GroupOrder = 2)]
      public string NanoGptApiKey { get; set; } = "";

      [SettingPropertyText("NSFW model (optional)", RequireRestart = false, Order = 2,
         HintText = "Model id used automatically in explicit scenes, and when you flip the in-chat NSFW button. Same " +
                    "provider as above. Leave empty to always use the roleplay model.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public string NsfwModel { get; set; } = "";

      [SettingPropertyDropdown("Reasoning Effort (NSFW)", RequireRestart = false, Order = 2,
         HintText = "Reasoning effort while the chat is in NSFW mode. Lower or Off makes a reasoning model far less " +
                    "prone to moralizing refusals, and speeds up scenes. OpenRouter only.")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Dropdown<string> NsfwReasoningEffort { get; set; } =
         new(new[] {ModelModePolicy.SameAsRoleplay, "Default", "Off", "Minimal", "Low", "Medium", "High"}, 0); // default: Same as roleplay

      [SettingPropertyDropdown("LLM Provider", RequireRestart = false, Order = 0,
         HintText = "Which OpenAI-compatible service to send requests to. The model fields are shared; each provider " +
                    "keeps its own key further down. Applies immediately.")]
      [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
      public Dropdown<string> Provider { get; set; } =
         new(new[] {"OpenRouter", "NanoGPT", "OpenAI-Compatible"}, 0); // default: OpenRouter

      [SettingPropertyDropdown("Reasoning Effort (roleplay)", RequireRestart = false, Order = 1,
         HintText = "How hard a reasoning model thinks before replying. Higher can mean finer dialogue but slower " +
                    "replies. Default leaves it to the model. OpenRouter only.")]
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

      [SettingPropertyDropdown("Lords Approaching You", RequireRestart = false, Order = 2,
         HintText = "How often a lord rides out to have a word with you on the road. Never switches it off " +
                    "entirely. Occasionally is the shipped feel.")]
      [SettingPropertyGroup("Content", GroupOrder = 4)]
      public Dropdown<string> InterceptionPacing { get; set; } =
         new(new[] {"Never", "Rarely", "Occasionally", "Often"}, 2); // default: Occasionally (today's behaviour)

      [SettingPropertyBool("Story Log", RequireRestart = false, Order = 2,
         HintText = "Writes a clean narrative transcript of every conversation and letter to " +
                    "CalradiaRemembers_story.log in your Logs folder. No timestamps, no noise: it reads like a novel.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool StoryLogging { get; set; } = false;

      [SettingPropertyButton("Test Connection", RequireRestart = false, Order = 3,
         HintText = "Validates your key, then sends a one-token test to the roleplay model and, if set, the NSFW " +
                    "model, reporting any error. Costs a fraction of a cent.",
         Content = "Test")]
      [SettingPropertyGroup("OpenRouter", GroupOrder = 1)]
      public Action TestConnection { get; set; } = ApiKeyTester.Run;

      [SettingPropertyButton("Test Endpoint", RequireRestart = false, Order = 3,
         HintText = "Sends a one-token test to your endpoint with the roleplay model and, if set, the NSFW model. " +
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

      [SettingPropertyBool("Verbose Logging", RequireRestart = false, Order = 1,
         HintText = "Logs the full system prompt and NPC reply on every exchange. Turn on only to capture a bug " +
                    "report: it floods the log. Requires Enable Logging.")]
      [SettingPropertyGroup("Diagnostics", GroupOrder = 7)]
      public bool VerboseLogging { get; set; } = false;
   }
}