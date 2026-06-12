// Code written by Gabriel Mailhot, 05/06/2026.
// Updated 09/06/2026: NanoGPT as a second OpenAI-compatible provider — Provider dropdown
//                     plus a NanoGPT key/model/test group alongside the OpenRouter one.

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
            new Dropdown<string>(new[] {"Off", "Mature", "Explicit", "Hardcore"}, 1); // default: Mature

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
            new Dropdown<string>(new[] {"Grounded", "Warm", "Storyteller"}, 0); // default: Grounded (= Normal)

        [SettingPropertyDropdown("LLM Provider", RequireRestart = false, Order = 0,
            HintText = "Which OpenAI-compatible service to send requests to. Each provider has its own " +
                       "API key and model field below — switching does not require re-pasting. Applies immediately.")]
        [SettingPropertyGroup("LLM Provider", GroupOrder = 0)]
        public Dropdown<string> Provider { get; set; } =
            new Dropdown<string>(new[] {"OpenRouter", "NanoGPT"}, 0); // default: OpenRouter

        [SettingPropertyText("OpenRouter API Key", RequireRestart = false, Order = -1,
            HintText = "Paste your OpenRouter API key from openrouter.ai/keys. Stored locally on this machine " +
                       "only — never share your config file or a screenshot of this field. Applies immediately.")]
        [SettingPropertyGroup("OpenRouter", GroupOrder = 3)]
        public string ApiKey { get; set; } = "";

        public override string DisplayName => "Calradia Remembers";
        public override string FolderName => "CalradiaRemembers";
        public override string FormatType => "json2";
        public override string Id => "CalradiaRemembers";

        [SettingPropertyText("Model", RequireRestart = false, Order = -2,
            HintText = "OpenRouter model id (e.g. x-ai/grok-4.3, anthropic/claude-sonnet-4, openai/gpt-4o). " +
                       "Browse models at openrouter.ai/models. Applies immediately.")]
        [SettingPropertyGroup("OpenRouter", GroupOrder = 3)]
        public string Model { get; set; } = "x-ai/grok-4.3";

        [SettingPropertyButton("Test Connection", RequireRestart = false, Order = -3,
            HintText = "Checks your API key against OpenRouter without starting a conversation — no tokens spent.",
            Content = "Test")]
        [SettingPropertyGroup("OpenRouter", GroupOrder = 3)]
        public Action TestConnection { get; set; } = ApiKeyTester.Run;

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

        [SettingPropertyButton("Test NanoGPT Connection", RequireRestart = false, Order = -3,
            HintText = "Checks your NanoGPT key against nano-gpt.com without starting a conversation — no tokens spent.",
            Content = "Test")]
        [SettingPropertyGroup("NanoGPT", GroupOrder = 4)]
        public Action TestNanoGptConnection { get; set; } = ApiKeyTester.RunNanoGpt;

        [SettingPropertyBool("Enable Logging", RequireRestart = false, Order = 0,
            HintText = "Master switch for all mod logging — both the on-screen messages and the " +
                       "CalradiaRemembers.log file. Off by default; turn on only to capture a bug report.")]
        [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
        public bool EnableLogging { get; set; } = false;

        [SettingPropertyBool("Verbose Logging", RequireRestart = false, Order = -1,
            HintText = "Logs the full system prompt and NPC response to the mod log on every exchange. " +
                       "Turn on only when capturing a bug report — it floods the log. Requires Enable Logging.")]
        [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
        public bool VerboseLogging { get; set; } = false;

        [SettingPropertyBool("Story Log", RequireRestart = false, Order = -2,
            HintText = "Writes a clean narrative transcript of every conversation and letter to " +
                       "CalradiaRemembers_story.log in your Bannerlord Logs folder. " +
                       "No timestamps or technical noise — reads like a novel.")]
        [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
        public bool StoryLogging { get; set; } = false;
    }
}