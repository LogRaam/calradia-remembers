// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Companions;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Logic.Witnesses;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

#endregion

namespace CalradiaRemembers.Foundation
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
      [SettingPropertyDropdown("{=CR_MCM_AdultContentLevel}Adult Content Level", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_AdultContentLevel_Hint}How much romantic/intimate content NPCs may express. Off, Mature (romance, " +
                    "nothing explicit), Explicit, or Hardcore. A child can be conceived only at Explicit or " +
                    "above. Applies on your next conversation.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> AdultContentLevel { get; set; } =
         new(new[] {"Off", "Mature", "Explicit", "Hardcore"}, 1); // default: Mature

      // The Prompt Composer (Gabriel, 11/10/2026: "Un param MCM pourrait designer l'ordonnanceur a utiliser"). Filled with the
      // profiles found at the main menu and at each session launch (Mapping.PromptProfileService); MCM saves only the place
      // in this list, so the service also keeps the pick by id.
      [SettingPropertyDropdown("{=CR_MCM_PromptProfile}Prompt Profile", RequireRestart = false, Order = 40,
         HintText = "{=CR_MCM_PromptProfile_Hint}Which prompt profile composes what characters read: stock, your own (Custom/Prompts) or one a mod ships (cr_prompts). Stock is the reset. cr.prompt_check lists what each profile changes.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> PromptProfile { get; set; } = new(new[] {"Stock (Calradia Remembers)"}, 0);

      [SettingPropertyDropdown("{=CR_MCM_ChatSkin}Chat Window Style", RequireRestart = false, Order = 5,
         HintText = "{=CR_MCM_ChatSkin_Hint}The look of the conversation window. Origin is the original panel; Dark Leather is the " +
                    "illuminated leather book. Both are the same conversation, only the look differs. Applies on " +
                    "your next conversation.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> ChatSkin { get; set; } =
         new(new[] {"Origin", "Dark Leather"}, 1); // default: Dark Leather (the illuminated book)

      [SettingPropertyInteger("{=CR_MCM_ChatTextSizePercent}Chat Text Size (%)", 70, 130, "0 '%'", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_ChatTextSizePercent_Hint}Scales ONLY the dialogue text in the chat window, not the window itself: a smaller value fits " +
                    "more words per line, a larger one reads easier. 100 is the shipped size. Applies on your next conversation.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public int ChatTextSizePercent { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_DarkLeatherZoomPercent}Dark Leather Size (%)", 80, 140, "0 '%'", RequireRestart = false, Order = 7,
         HintText = "{=CR_MCM_DarkLeatherZoomPercent_Hint}Scales the Dark Leather chat window. Raise it on a high-resolution screen (1440p+) if the book " +
                    "feels small or cramped. 100 is the shipped size. No effect on the Origin skin.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public int DarkLeatherZoomPercent { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_CouncilZoomPercent}Council Size (%)", 60, 140, "0 '%'", RequireRestart = false, Order = 8,
         HintText = "{=CR_MCM_CouncilZoomPercent_Hint}Scales the council window independently of the chat size above. The council book is wider, so on a 1440p " +
                    "or larger screen it can feel too big: lower it if it overflows, raise it if it feels small. 100 is the " +
                    "shipped size.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public int CouncilZoomPercent { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_CouncilTextSizePercent}Council Text Size (%)", 70, 130, "0 '%'", RequireRestart = false, Order = 9,
         HintText = "{=CR_MCM_CouncilTextSizePercent_Hint}Scales ONLY the dialogue text in the council (round-table) window, INDEPENDENTLY of the chat " +
                    "text size above. 100 is the shipped size. Applies to the next council.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public int CouncilTextSizePercent { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_MarriageRegardNeeded}Marriage Regard Needed", 10, 50, "0", RequireRestart = false, Order = 10,
         HintText = "{=CR_MCM_MarriageRegardNeeded_Hint}Regard a lord or lady must hold for you before accepting a LOVE-MATCH marriage (their personal regard or " +
                    "your clan relation, whichever is higher). 50 is the shipped bar. Their family's blessing can still open " +
                    "a match below it. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public int MarriageRegardNeeded { get; set; } = 50;

      [SettingPropertyInteger("{=CR_MCM_RegardDecayDays}Regard Warmth Cooldown (days)", 0, 30, "0 'days'", RequireRestart = false, Order = 11,
         HintText = "{=CR_MCM_RegardDecayDays_Hint}How long warmth settles before it can deepen again. Each point of regard costs one more warm moment " +
                    "than the last, and a step relaxes after this many days. 0 turns it off: warmth never gets dearer. " +
                    "7 is the default. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public int RegardDecayDays { get; set; } = 7;

      [SettingPropertyText("{=CR_MCM_ApiKey}OpenRouter API Key", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_ApiKey_Hint}Paste your key from openrouter.ai/keys. Stored locally on this machine only. NEVER share your " +
                    "config file or a screenshot of this field. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenRouter}OpenRouter", GroupOrder = 1)]
      public string ApiKey { get; set; } = "";

      // Hints must stay SHORT: a long one overflows the MCM control and its first line is cut off the top
      // (Gabriel, screenshot 2026-07-13). Two or three lines, no more. Say the stake, not the essay.
      // ProviderPin and NsfwProviderPin are resolved per model mode by ModelModePolicy.ResolveProviderPin.
      [SettingPropertyText("{=CR_MCM_ProviderPin}Pin Providers (advanced)", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_ProviderPin_Hint}Leave blank unless needed. Some providers censor their own output, cutting replies " +
                    "mid-sentence. Pinning also steadies prompt caching, which lowers cost: unpinned, OpenRouter " +
                    "may switch provider mid-session and the cache is lost. Comma separated.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenRouter}OpenRouter", GroupOrder = 1)]
      public string ProviderPin { get; set; } = "";

      [SettingPropertyText("{=CR_MCM_NsfwProviderPin}Pin Providers (NSFW)", RequireRestart = false, Order = 5,
         HintText = "{=CR_MCM_NsfwProviderPin_Hint}Blank reuses the pin above. Set a different provider only for explicit scenes, e.g. " +
                    "cheapest routing for chat and an uncensored provider for scenes.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenRouter}OpenRouter", GroupOrder = 1)]
      public string NsfwProviderPin { get; set; } = "";

      [SettingPropertyBool("{=CR_MCM_AllowProviderFallbacks}Allow provider fallbacks", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_AllowProviderFallbacks_Hint}Only matters with providers pinned above. OFF (default) makes the pin a hard rule. ON lets " +
                    "OpenRouter fall back to others when yours are unavailable.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenRouter}OpenRouter", GroupOrder = 1)]
      public bool AllowProviderFallbacks { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_EnableRideWithMe}Lords can ride in your party", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_EnableRideWithMe_Hint}On (default): an idle lord or lady of another clan may agree to ride IN your party for a time, keeping " +
                    "their own house. Off: only the follow_me escort (their own party riding alongside yours) remains. " +
                    "cr.retainer enable overrides for the session.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool EnableRideWithMe { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_EnableNpcWorldClaims}Characters can add news to the world", RequireRestart = false, Order = 13,
         HintText = "{=CR_MCM_EnableNpcWorldClaims_Hint}Off (default): what a character invents stays in that conversation. On: a blight, brigands, " +
                    "a death or an omen they mention can become a real rumour others may hear. Never numbers, " +
                    "money, or who holds what - the game owns those.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool EnableNpcWorldClaims { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_NpcCanEndConversation}NPCs can end conversations", RequireRestart = false, Order = 5,
         HintText = "{=CR_MCM_NpcCanEndConversation_Hint}On (default): when a character ends a conversation the chat closes and you click Leave. Off: their " +
                    "goodbye no longer closes it, so you can have the last word and close it yourself. Captivity scenes keep " +
                    "their own flow.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool NpcCanEndConversation { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_EnableCouncil}Enable Council", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_EnableCouncil_Hint}When ON, you can convene a council of companions, kin, clan lords or a war council from a town, castle " +
                    "or village menu, and from a button on the campaign map while on land. OFF hides those entry points; the " +
                    "console can still open it.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool EnableCouncil { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_EnableStrategicAssessment}Strategic Assessment (AI)", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_EnableStrategicAssessment_Hint}On (default): the Strategic Assessment is written by your highest-Tactics companion (a warband soldier " +
                    "if you have none), and the sharper their Tactics the better the counsel. Off, or alone: " +
                    "a plain summary, no AI call.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool EnableStrategicAssessment { get; set; } = true;

      [SettingPropertyDropdown("{=CR_MCM_CouncilVoices}Council Voices", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_CouncilVoices_Hint}Default for a new council (flip it in the council window). One call for the table keeps the scene " +
                    "cohesive but voices the others from little more than a name. One call per member gives " +
                    "each their real memory and regard, at a round-trip apiece.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> CouncilVoices { get; set; } =
         new(new[] {"One call for the table", "One call per member"}, 0); // default: whole table

      [SettingPropertyBool("{=CR_MCM_CompanionContentment}Companion contentment", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_CompanionContentment_Hint}When ON, a neglected companion grows restless, asks for an audience, and if you keep refusing eventually " +
                    "leaves by letter, never silently. Also gates the occasional found-topic audience. OFF disables both " +
                    "entirely.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Companions}Companions", GroupOrder = 6)]
      public bool CompanionContentment { get; set; } = true;

      [SettingPropertyDropdown("{=CR_MCM_ContentmentPacing}Contentment Pacing", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_ContentmentPacing_Hint}How fast companions get over a grievance: Forgiving, Balanced (default), or Exacting. In every " +
                    "setting, a companion left merely idle NEVER leaves you.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Companions}Companions", GroupOrder = 6)]
      public Dropdown<string> ContentmentPacing { get; set; } =
         new(new[] {"Forgiving", "Balanced", "Exacting"}, 1); // default: Balanced

      [SettingPropertyDropdown("{=CR_MCM_CompanionAudiencePacing}Companions Asking For A Word", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_CompanionAudiencePacing_Hint}How often a travelling companion asks for a private word, in a settlement or on " +
                    "the road. Never switches it off. Occasionally is the shipped feel. With The World's Initiative on, it sets " +
                    "how much of its turns companions get.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Companions}Companions", GroupOrder = 6)]
      public Dropdown<string> CompanionAudiencePacing { get; set; } =
         new(new[] {"Never", "Rarely", "Occasionally", "Often"}, 2); // default: Occasionally (today's settlement feel, plus a road chance)

      [SettingPropertyText("{=CR_MCM_CustomApiKey}API Key (optional)", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_CustomApiKey_Hint}Leave blank for keyless local servers like LM Studio or Ollama. Fill it in only if your server " +
                    "requires one. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKey { get; set; } = "";

      [SettingPropertyText("{=CR_MCM_CustomEndpointUrl}Endpoint URL (Prose / shared)", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_CustomEndpointUrl_Hint}Base URL of your local server, with /v1 (e.g. http://localhost:1234/v1). Serves the Prose " +
                    "role, and any role whose own Endpoint below is blank. Set that role's model under LLM " +
                    "Provider too, or it sends an OpenRouter id to your server.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrl { get; set; } = "";

      [SettingPropertyText("{=CR_MCM_CustomEndpointUrlNsfw}Endpoint URL (NSFW)", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_CustomEndpointUrlNsfw_Hint}Optional. A separate OpenAI-Compatible server URL for the NSFW role only, so you can split roles " +
                    "across two local machines. Blank = use the shared Endpoint URL above. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrlNsfw { get; set; } = "";

      [SettingPropertyText("{=CR_MCM_CustomEndpointUrlHousekeeping}Endpoint URL (Background)", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_CustomEndpointUrlHousekeeping_Hint}Optional. A separate OpenAI-Compatible server URL for the mod's background work only " +
                    "(summaries, memory compression). Leave blank to use the same server as everything else.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrlHousekeeping { get; set; } = "";

      [SettingPropertyText("{=CR_MCM_CustomEndpointUrlInterpret}Endpoint URL (Interpret)", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_CustomEndpointUrlInterpret_Hint}Optional. A separate OpenAI-Compatible server URL for the Interpret role only, so the interpreter " +
                    "can run on its own local machine. Blank = use the shared Endpoint URL above. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomEndpointUrlInterpret { get; set; } = "";

      [SettingPropertyDropdown("{=CR_MCM_CustomParameterCompatibility}Parameter Compatibility", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_CustomParameterCompatibility_Hint}Which sampling parameters to send. Auto detects OpenAI reasoning models (gpt-5, o1/o3/o4), which " +
                    "reject the classic max_tokens. Leave on Auto unless your endpoint rejects a field.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public Dropdown<string> CustomParameterCompatibility { get; set; } =
         new(new[] {"Auto", "Standard", "Reasoning"}, 0); // default: Auto

      [SettingPropertyDropdown("{=CR_MCM_CustomReasoningEffort}Reasoning Effort", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_CustomReasoningEffort_Hint}Sends reasoning_effort to a local or OpenAI-compatible endpoint. Set Off if a reasoning model spends the " +
                    "whole reply budget thinking and stalls. Default sends nothing. Some servers (vLLM) ignore it; Verbose " +
                    "Logging shows what was sent.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public Dropdown<string> CustomReasoningEffort { get; set; } =
         new(new[] {"Default", "Off", "Minimal", "Low", "Medium", "High"}, 0); // default: Default (send nothing)

      [SettingPropertyText("{=CR_MCM_CustomApiKeyNsfw}API Key (NSFW)", RequireRestart = false, Order = 5,
         HintText = "{=CR_MCM_CustomApiKeyNsfw_Hint}Optional. A separate API key for the NSFW role only, so it can authenticate against a different " +
                    "OpenAI-Compatible server than the shared one. Blank = use the API Key above. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKeyNsfw { get; set; } = "";

      [SettingPropertyText("{=CR_MCM_CustomApiKeyHousekeeping}API Key (Background)", RequireRestart = false, Order = 7,
         HintText = "{=CR_MCM_CustomApiKeyHousekeeping_Hint}Optional. A separate API key for the mod's background work only. Leave blank to use the " +
                    "same key as everything else.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKeyHousekeeping { get; set; } = "";

      [SettingPropertyText("{=CR_MCM_CustomApiKeyInterpret}API Key (Interpret)", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_CustomApiKeyInterpret_Hint}Optional. A separate API key for the Interpret role only, so the interpreter can authenticate " +
                    "against its own OpenAI-Compatible server. Blank = use the API Key above. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public string CustomApiKeyInterpret { get; set; } = "";

      public override string DisplayName => "Calradia Remembers";

      [SettingPropertyBool("{=CR_MCM_EnableLogging}Enable File Logging", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_EnableLogging_Hint}Writes the mod's diagnostics to CalradiaRemembers.log (only the last two sessions are kept, " +
                    "so the file never grows without bound). Off by default; turn on to capture a bug report.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Diagnostics}Diagnostics", GroupOrder = 7)]
      public bool EnableLogging { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_EnableScreenLogging}Enable On-Screen Logging", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_EnableScreenLogging_Hint}Also prints the mod's diagnostics as on-screen messages (top-left). Off by default: the " +
                    "stream can crowd the screen during play. Turn it on only when you want to watch the mod live; " +
                    "the file log above is the better channel for a bug report.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Diagnostics}Diagnostics", GroupOrder = 7)]
      public bool EnableScreenLogging { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_EnableJealousy}Enable Jealousy", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_EnableJealousy_Hint}On (default): the world is richer, partners react to being favoured or slighted, a neglected " +
                    "co-partner can feel wronged, and open-relationship pacts matter. Off: a drama-free household, " +
                    "no jealousy of any kind, from any partner.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool EnableJealousy { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_EnableStolenLove}Enable Stolen Love", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_EnableStolenLove_Hint}Off (default): a married character resists an affair as before. On: seducing a MARRIED character is a " +
                    "gated, personality-true path (an honourable or content one holds firm however high your " +
                    "standing). Explicit content follows Adult Content Level.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool EnableStolenLove { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_AllowKillingCharacters}Allow killing characters", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_AllowKillingCharacters_Hint}Off (default): you may rob or beat someone in conversation, never kill. On: a killing attempt is " +
                    "possible, never a formality (it turns on how you compare and whether they are alone). The " +
                    "world answers: a crime, lost standing, kin who remember.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool AllowKillingCharacters { get; set; } = false;

      // Arrests in the King's name (Gabriel, 26/09/2026): OFF by default (it can cost the player their fiefs and put
      // them at war). Gates what OPENS a case (a warrant, a brigandage); a trial or a sentence already under way runs to its
      // end, so turning it off mid-way never strands the player in a cell. Since 23/10/2026 (froggyluv: "I've disabled that
      // in options but they're still showing up") turning it off also lifts every bounty, as an amnesty would, and stops
      // the hunters.
      [SettingPropertyBool("{=CR_MCM_EnableArrests}Arrests and outlaws", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_EnableArrests_Hint}Off (default). On: as a vassal or mercenary, a grave act brings a warrant: face your " +
                    "liege's council or flee as an outlaw with a price on your head. Robbing merchants in peace " +
                    "makes you a brigand. Never a ruler. Off lifts every bounty.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool EnableArrests { get; set; } = false;

      // Fakade (Nexus, 04/10/2026): "If this is due to the mercenary thing, can that also be an option to disable?"
      [SettingPropertyBool("{=CR_MCM_JudgeMercenaryDepartures}Mercenary departures are judged", RequireRestart = false, Order = 5,
         HintText = "{=CR_MCM_JudgeMercenaryDepartures_Hint}On (default): when a contract ends, the realm's lords judge it. Leaving a losing war, or going to " +
                    "its enemy, costs their regard, which fades over a year. A contract renewed within days is no " +
                    "departure. Off: never judged.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool JudgeMercenaryDepartures { get; set; } = true;

      // Maifailun (Nexus, 15/10/2026) and Gabriel's ladder the same day: summons, levy, goods, a fief. OFF by default
      // (it can cost the player a fief). Gates what the RULER does; the service ledger is kept either way.
      [SettingPropertyBool("{=CR_MCM_EnableWarDuty}Vassal war duty", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_EnableWarDuty_Hint}Off (default). On: a sworn vassal (never a mercenary) who sits out the realm's war is summoned by " +
                    "the ruler to answer for it, then asked a levy in coin, or troops, horses or arms instead, and may " +
                    "lose a fief by giving nothing.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool EnableWarDuty { get; set; } = false;

      [SettingPropertyInteger("{=CR_MCM_WarDutyWindowDays}War duty window (days)", 10, 120, "0", RequireRestart = false, Order = 7,
         HintText = "{=CR_MCM_WarDutyWindowDays_Hint}Days of war a sworn vassal may go without fighting for the realm (a battle, a siege, a day in its " +
                    "army) before the ruler calls them to account. Default 30.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public int WarDutyWindowDays { get; set; } = 30;

      [SettingPropertyInteger("{=CR_MCM_WarLevyScalePercent}War levy scale (%)", 25, 400, "0 '%'", RequireRestart = false, Order = 8,
         HintText = "{=CR_MCM_WarLevyScalePercent_Hint}Scales the levy a ruler asks of a vassal who did not fight (it grows with clan tier and fiefs " +
                    "held, at most 10,000 per hand-over). 100 = as designed.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public int WarLevyScalePercent { get; set; } = 100;

      // tashmetu (30/09/2026): a clan at +54 with the player, and its lord met him as a near-stranger.
      [SettingPropertyBool("{=CR_MCM_ClanStandingWarmsFirstMeetings}Clan standing warms first meetings", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_ClanStandingWarmsFirstMeetings_Hint}On (default): when you next speak with someone whose clan already counts you a friend, they " +
                    "start warmer toward you, once, up to friendly regard; the rest is earned in person. An offence " +
                    "stands. Off: regard starts from zero.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool ClanStandingWarmsFirstMeetings { get; set; } = true;

      // tashmetu (20/10/2026): a player read a wife's secret affair in her lover's Memories panel before it came to light.
      [SettingPropertyBool("{=CR_MCM_ShowSecretMemories}Show secret memories", RequireRestart = false, Order = 9,
         HintText = "{=CR_MCM_ShowSecretMemories_Hint}Lists in a character's Memories panel the secrets other mods give them (an affair, a hidden " +
                    "fondness), marked secret. Off by default, so you learn them in play rather than by reading the panel.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool ShowSecretMemories { get; set; } = false;

      // Gabriel (21/10/2026), edicts: "les PNJ peuvent aussi". OFF by default for the first build; the player's own work either way.
      [SettingPropertyBool("{=CR_MCM_EnableNpcEdicts}Edicts of the world's lords", RequireRestart = false, Order = 10,
         HintText = "{=CR_MCM_EnableNpcEdicts_Hint}Off (default). On: the world's lords decree edicts on their own towns, and rulers on their realms, " +
                    "to answer a sore need (hunger, unrest, lawlessness), told as realm news. Your own edicts work either way.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool EnableNpcEdicts { get; set; } = false;

      // Gabriel (29-30/10/2026): a lord's ambition is whatever makes his house or realm stronger, plotting against a rival or
      // a realm grown too strong, arrangements with weaker realms against it. OFF by default for the first build, as edicts.
      [SettingPropertyBool("{=CR_MCM_EnableLordStrategy}Lords play the realm's strategy", RequireRestart = false, Order = 11,
         HintText = "{=CR_MCM_EnableLordStrategy_Hint}Off (default). On: when one realm grows far too strong, lords of the other realms put war, " +
                    "peace, alliances and calls to arms against it to their realm's vote, and plot against houses grown too strong, " +
                    "yours included.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Relationships}Relationships", GroupOrder = 8)]
      public bool EnableLordStrategy { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_EnablePostDuelConversation}Post-duel conversation", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_EnablePostDuelConversation_Hint}On (default): after a duel the conversation reopens and resumes what led to it. The loser concedes (a " +
                    "beaten character of honour may grant a favour you ask); the victor says what they now expect. Off: only " +
                    "the duel's consequences apply.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Duels}Duels", GroupOrder = 9)]
      public bool EnablePostDuelConversation { get; set; } = true;

      public override string FolderName => "CalradiaRemembers";
      public override string FormatType => "json2";
      public override string Id => "CalradiaRemembers";

      [SettingPropertyBool("{=CR_MCM_LeanPrompt}Compact prompt for small models", RequireRestart = false, Order = 12,
         HintText = "{=CR_MCM_LeanPrompt_Hint}Trims the prompt to fit a SMALL, short-context model (4k to 8k). Turn ON if the AI stays silent: " +
                    "the full prompt is overflowing it. Replies grow less richly grounded, and fewer tasks are offered. Leave OFF for cloud models.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public bool LeanPrompt { get; set; } = false;

      [SettingPropertyInteger("{=CR_MCM_LocalContextTokens}Local model context (tokens)", 0, 262144, "0", RequireRestart = false, Order = 13,
         HintText = "{=CR_MCM_LocalContextTokens_Hint}0 = off (cloud models). For a local/custom server: the context length set there, per request. CR then trims " +
                    "each request to fit it (oldest lines first, then the compact prompt) instead of the server cutting it blind.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public int LocalContextTokens { get; set; } = 0;

      [SettingPropertyBool("{=CR_MCM_MemoryCompression}Compress old memories", RequireRestart = false, Order = 14,
         HintText = "{=CR_MCM_MemoryCompression_Hint}Lets characters fold old, superseded memories into a short outline, so a long campaign stops inflating " +
                    "every prompt (the usual cause of slow replies on a local model). First meetings, recent events and grave " +
                    "moments are never dropped.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public bool MemoryCompression { get; set; } = true;

      // avalanche24 (Nexus, 07/11/2026): "Can we have an option to limit the memories to a certain number", a local model
      // drowning past 16k. Gabriel (08/11/2026): "met les réglages dans MCM". The prompt only: every memory stays saved.
      [SettingPropertyInteger("{=CR_MCM_PromptMemoryLimit}Memories in the prompt", 0, 50, "0", RequireRestart = false, Order = 14,
         HintText = "{=CR_MCM_PromptMemoryLimit_Hint}Most memories of you a character reads in each prompt, the latest kept. 0 = all " +
                    "(Compact keeps 6 at most). Lower it if a small local model drowns in a long history. Every memory stays saved.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public int PromptMemoryLimit { get; set; } = 0;

      // The same report: the fold at 24 memories (MemoryCompressionPolicy.DefaultThreshold) came too late for his model.
      [SettingPropertyInteger("{=CR_MCM_MemoryCompressionThreshold}Compress after (memories)", 8, 60, "0", RequireRestart = false, Order = 14,
         HintText = "{=CR_MCM_MemoryCompressionThreshold_Hint}How many memories a character holds before old, superseded ones are folded " +
                    "into an outline (default 24). Lower keeps prompts short on a local model; each fold costs one background call.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public int MemoryCompressionThreshold { get; set; } = MemoryCompressionPolicy.DefaultThreshold;

      // Phase 2: the hard-failure fallback is the NSFW model, so this legacy field is no longer shown in MCM or
      // read anywhere. Kept as a plain (un-attributed) property so an old stored value loads without orphaning.
      public string ProseFallbackModel { get; set; } = "";

      // fkasad (Nexus, 17/10/2026) asked whether the mod tunes temperature by the kind of call; it does, but the
      // conversation value was fixed at 0.7. Gabriel the same day: a setting, for players fitting a local model.
      [SettingPropertyFloatingInteger("{=CR_MCM_ChatTemperature}Chat Temperature", ChatTemperaturePolicy.Min, ChatTemperaturePolicy.Max, "0.00", RequireRestart = false, Order = 15,
         HintText = "{=CR_MCM_ChatTemperature_Hint}How varied conversation replies are (default 0.70). Lower is steadier and more repetitive, higher " +
                    "more surprising; above 1.0 a local model may lose its character. Reasoning models ignore it. " +
                    "Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public float ChatTemperature { get; set; } = ChatTemperaturePolicy.Default;

      // avalanche24 (Nexus, 03/11/2026): a local reasoning model counts its thinking against the reply budget and was cut off
      // mid-thought at Compact's fixed 1200. Gabriel's go the same day.
      [SettingPropertyInteger("{=CR_MCM_ReplyTokenBudget}Reply Token Budget", ReplyBudgetPolicy.Auto, ReplyBudgetPolicy.Max, "0", RequireRestart = false, Order = 16,
         HintText = "{=CR_MCM_ReplyTokenBudget_Hint}Most tokens a conversation reply may use. 0 = Auto (3000, 1200 in Compact). A reasoning model " +
                    "counts its thinking in it: raise it if it is cut off mid-thought. On a local server it shares the context window.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public int ReplyTokenBudget { get; set; } = ReplyBudgetPolicy.Auto;

      // avalanche24 (Nexus, 03/11/2026): "the presence penalty is killing my model's thought process prematurely".
      [SettingPropertyFloatingInteger("{=CR_MCM_PresencePenalty}Presence Penalty", PresencePenaltyPolicy.Min, PresencePenaltyPolicy.Max, "0.00", RequireRestart = false, Order = 17,
         HintText = "{=CR_MCM_PresencePenalty_Hint}How hard conversation replies avoid repeating words (default 0.50). 0 sends none, for a local " +
                    "model it hurts. Higher is more varied but may drift off names. Reasoning models ignore it.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public float PresencePenalty { get; set; } = PresencePenaltyPolicy.Default;

      [SettingPropertyInteger("{=CR_MCM_LlmTimeoutSeconds}LLM Response Timeout (s)", 15, 600, "0 's'", RequireRestart = false, Order = 11,
         HintText = "{=CR_MCM_LlmTimeoutSeconds_Hint}How long to wait for a reply, in seconds (default 120), in conversations and for letters and " +
                    "summaries (never under 60). Raise it for a slow local or reasoning model; lower it to fail fast. " +
                    "Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public int LlmTimeoutSeconds { get; set; } = 120;

      [SettingPropertyText("{=CR_MCM_Model}PROSE model", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_Model_Hint}Model id in the ACTIVE provider's own format (OpenRouter: x-ai/grok-4.20; NanoGPT: " +
                    "deepseek-ai/DeepSeek-V3; Ollama: llama3.1:8b). Writes ordinary chat, and in Enhanced mode " +
                    "the prose of every scene. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public string Model { get; set; } = "x-ai/grok-4.20";

      // Per-role provider (2026-08-13): each model role may go to its OWN provider. The roleplay / prose role is
      // the BASE: it is the Provider dropdown below (property name kept for save compatibility), presented right
      // here as "Roleplay / Prose provider". The other three roles keep a "(default)" option that inherits this
      // base, so an upgraded config behaves exactly as before and there is no redundant global provider field.
      [SettingPropertyDropdown("{=CR_MCM_Provider}Roleplay / Prose provider", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_Provider_Hint}Which provider the roleplay and prose model above is sent to. It is also the base every other " +
                    "role inherits when left on (default). Each provider keeps its own key further down. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public Dropdown<string> Provider { get; set; } =
         new(new[] {ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0); // default: OpenRouter

      [SettingPropertyText("{=CR_MCM_NanoGptApiKey}NanoGPT API Key", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_NanoGptApiKey_Hint}Paste your key from nano-gpt.com. Stored locally on this machine only. NEVER share your config " +
                    "file or a screenshot of this field.")]
      [SettingPropertyGroup("{=CR_MCM_Group_NanoGPT}NanoGPT", GroupOrder = 2)]
      public string NanoGptApiKey { get; set; } = "";

      [SettingPropertyDropdown("{=CR_MCM_ChatCompositionModeSetting}Chat Composition", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_ChatCompositionModeSetting_Hint}Where a fresh session's in-chat Enhanced/Roleplay switch starts. Enhanced (default): the prose model " +
                    "writes and the interpret model tags, richest but slower. Standard: one call, cheapest and fastest. The " +
                    "in-chat switch overrides it for the session.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public Dropdown<string> ChatCompositionModeSetting { get; set; } =
         new(new[] {ChatCompositionModePolicy.StandardLabel, ChatCompositionModePolicy.EnhancedLabel}, 1); // default: Enhanced

      // Phase 2: consensual scenes are now "chat" (the PROSE model), so this separate consensual model and its
      // provider are no longer shown in MCM or read anywhere. Kept as plain (un-attributed) properties, still named
      // NsfwModel for its legacy save-identity, so an old stored value loads without orphaning.
      public string NsfwModel { get; set; } = "";

      public Dropdown<string> ConsensualProvider { get; set; } =
         new(new[] {ProviderRolePolicy.DefaultSelection, ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0);

      [SettingPropertyText("{=CR_MCM_CncModel}NSFW model", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_CncModel_Hint}Model id for explicit content: captivity in Standard mode, the fallback in Enhanced. Blank " +
                    "uses the PROSE model - and so does the shipped OpenRouter id if you point this role at your " +
                    "own server without naming ITS model.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public string CncModel { get; set; } = "x-ai/grok-4.20";

      [SettingPropertyDropdown("{=CR_MCM_CncProvider}NSFW provider", RequireRestart = false, Order = 7,
         HintText = "{=CR_MCM_CncProvider_Hint}Which provider the NSFW model above is sent to. (default) uses the PROSE provider. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public Dropdown<string> CncProvider { get; set; } =
         new(new[] {ProviderRolePolicy.DefaultSelection, ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0);

      // fkasad (Nexus, 19/09/2026), running a local model: the mod's background work and its conversations are
      // two completely different prompts, and on a single-slot local server they evict each other's KV cache
      // every time they alternate. He asked what was happening behind the curtain, and the honest answer was
      // that there was no way to separate them. There is now.
      //
      // Blank means what it has always meant: the same model as the prose reply, so nobody who does not go
      // looking for this is affected by it.
      [SettingPropertyText("{=CR_MCM_HousekeepingModel}Background model", RequireRestart = false, Order = 11,
         HintText = "{=CR_MCM_HousekeepingModel_Hint}Optional. A cheaper or smaller chat model (not an embedding model) for the mod's background " +
                    "work: summaries, memory compression, claim extraction. Blank uses the roleplay model. Useful " +
                    "on a local server, where two prompts share one cache.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public string HousekeepingModel { get; set; } = "";

      [SettingPropertyDropdown("{=CR_MCM_HousekeepingProvider}Background provider", RequireRestart = false, Order = 12,
         HintText = "{=CR_MCM_HousekeepingProvider_Hint}Which provider the Background model above is sent to. (default) uses the PROSE provider.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public Dropdown<string> HousekeepingProvider { get; set; } =
         new(new[] {"(default)", "OpenRouter", "NanoGPT", "OpenAI-Compatible"}, 0);

      [SettingPropertyText("{=CR_MCM_ActionInterpreterModel}Interpret model", RequireRestart = false, Order = 8,
         HintText = "{=CR_MCM_ActionInterpreterModel_Hint}Enhanced mode only: the model that reads the structured actions out of the prose. Blank " +
                    "uses the PROSE model - and so does the shipped OpenRouter id if you point this role at " +
                    "your own server without naming ITS model.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public string ActionInterpreterModel { get; set; } = "deepseek/deepseek-v4.1-flash";

      [SettingPropertyDropdown("{=CR_MCM_ActionInterpreterProvider}Interpret provider", RequireRestart = false, Order = 9,
         HintText = "{=CR_MCM_ActionInterpreterProvider_Hint}Which provider the Interpret model above is sent to. (default) uses the PROSE provider (set it " +
                    "to a local OpenAI-Compatible endpoint to run the interpreter locally). Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_LLMProvider}LLM Provider", GroupOrder = 0)]
      public Dropdown<string> ActionInterpreterProvider { get; set; } =
         new(new[] {ProviderRolePolicy.DefaultSelection, ProviderRolePolicy.OpenRouterValue, ProviderRolePolicy.NanoGptValue, ProviderRolePolicy.OpenAiCompatibleValue}, 0);

      [SettingPropertyDropdown("{=CR_MCM_NsfwReasoningEffort}Reasoning Effort (NSFW)", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_NsfwReasoningEffort_Hint}Reasoning effort while the chat is in NSFW mode. Lower or Off makes a reasoning model far less " +
                    "prone to moralizing refusals, and speeds up scenes. OpenRouter and NanoGPT.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenRouter}OpenRouter", GroupOrder = 1)]
      public Dropdown<string> NsfwReasoningEffort { get; set; } =
         new(new[] {ModelModePolicy.SameAsProse, "Default", "Off", "Minimal", "Low", "Medium", "High"}, 0); // default: Same as roleplay

      [SettingPropertyDropdown("{=CR_MCM_ReasoningEffort}Reasoning Effort (PROSE)", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_ReasoningEffort_Hint}How hard the PROSE model thinks before replying. Higher can mean finer dialogue but slower replies, more " +
                    "truncation and more refusals on adult content; keep it Low or Medium. Grok 4 only thinks or not: Low means " +
                    "Off there. OpenRouter and NanoGPT.")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenRouter}OpenRouter", GroupOrder = 1)]
      public Dropdown<string> ReasoningEffort { get; set; } =
         new(new[] {"Default", "Off", "Minimal", "Low", "Medium", "High"}, 3); // default: Low

      [SettingPropertyDropdown("{=CR_MCM_RelationshipWarmth}Relationship Pacing", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_RelationshipWarmth_Hint}How relationships grow through conversation: a philosophy, not a difficulty. Grounded keeps the " +
                    "core game's feel; Warm rewards good company; Storyteller is roleplay-first. Insults cost the " +
                    "same everywhere.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> RelationshipWarmth { get; set; } =
         new(new[] {"Grounded", "Warm", "Storyteller"}, 0); // default: Grounded (= Normal)

      [SettingPropertyText("{=CR_MCM_ReplyLanguage}Reply Language", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_ReplyLanguage_Hint}Leave blank to use the game's language (an English game mirrors whatever language you write " +
                    "in). Name a language (e.g. Spanish) to force every reply, letter, memory, and the illuminated " +
                    "chronicle into it, no matter what you write.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public string ReplyLanguage { get; set; } = "";

      // RaveN017Mod (Nexus PM, 29/10/2026): replies too long, "like a novel". Sentences, not words: a word cap cuts mid-sentence.
      [SettingPropertyDropdown("{=CR_MCM_ReplyLength}Reply Length", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_ReplyLength_Hint}How long characters' replies run. Full is the length the mod has always used (3 to 7 " +
                    "sentences, longer for a story). Balanced: 2 to 4. Short: 1 to 3, always. Counted in sentences, so a " +
                    "reply is never cut off mid-way.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> ReplyLength { get; set; } =
         new(new[] {"Full", "Balanced", "Short"}, 0); // default: Full, the shipped length

      // Three players in two days, none of them defending it (Kilju101 17/09, Lordfadooboo 18/09, fkasad 18/09).
      // fkasad put the cost plainly: "Maybe one in ten NPCs has something interesting to say, but unfortunately,
      // I'm forced to decline the conversation because of the other nine... it's wasted playtime."
      //
      // The nine have a name in the code: RecentMatterRegister.WorldGossip, a happening that does NOT involve
      // the player, which the lord does not comment on but ASKS the player about. It always yields to anything
      // personal, so it fires exactly when a lord has nothing of his own to say. This switches that one register
      // off and leaves every other reason to approach untouched.
      //
      // Default OFF, and that is the decision, not the switch: a player annoyed by this does not go looking
      // through mod options, they stop using the mod. Anyone who wants the world talked about can turn it on.
      [SettingPropertyBool("{=CR_MCM_NpcsRaiseWorldNews}NPCs Raise World News", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_NpcsRaiseWorldNews_Hint}Off (default): an NPC approaches you only about something between the two of you. On: they " +
                    "may also stop you to ask what you make of distant news. Their own deeds and yours are " +
                    "always allowed either way.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool NpcsRaiseWorldNews { get; set; } = false;

      // Lordfadooboo (Nexus, 21/10/2026): a conversation opened cold. Gabriel (22/10/2026): on by default, as captive
      // scenes already open themselves; the switch is for a player who counts the cost (one more call per conversation).
      [SettingPropertyBool("{=CR_MCM_CharactersSetTheScene}Characters Set The Scene", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_CharactersSetTheScene_Hint}On (default): when you start a conversation, the character opens it, what they were doing and their first words, while you may already type. One more model call per conversation. Off: the short greeting alone.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool CharactersSetTheScene { get; set; } = true;

      [SettingPropertyDropdown("{=CR_MCM_InterceptionPacing}Lords Approaching You", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_InterceptionPacing_Hint}How often a lord rides out to have a word with you on the road. Never switches it off " +
                    "entirely. Occasionally is the shipped feel. With The World's Initiative on, it sets how much of its turns lords get.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> InterceptionPacing { get; set; } =
         new(new[] {"Never", "Rarely", "Occasionally", "Often"}, 2); // default: Occasionally (today's behaviour)

      // Authored quests (docs/design/AUTHORED_QUESTS.md; Gabriel, 03/10/2026): a switch for the new written tales.
      // Gabriel (05/10/2026): shipped in 2.7.0 OFF and marked experimental; the scene's close is not yet play-verified.
      [SettingPropertyBool("{=CR_MCM_AuthoredQuests}Authored Quests (experimental)", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_AuthoredQuests_Hint}Experimental, off by default. On: characters may ask your help with a written tale (a quest in several stages, like the wolf of the wood). Off: no new tale is offered; one under way goes on (cr.story_abandon sets it aside).")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool AuthoredQuests { get; set; } = false;

      // Fakade (Nexus, 03/10/2026): since 2.6.8 a lord riding out to the player is held on course (Lordfadooboo had
      // asked for it); Fakade liked the old way too, where they lost interest quickly. Both, by the player's choice.
      [SettingPropertyBool("{=CR_MCM_LordsHoldTheirCourse}Lords Hold Their Course", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_LordsHoldTheirCourse_Hint}On: a lord riding out to speak with you holds his course until he reaches you, or gives up after a few days. Off: as before 2.6.8, he may turn back to his own affairs and lose interest quickly. Applies to the next chase.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool LordsHoldTheirCourse { get; set; } = true;

      // Gabriel (08/10/2026): with the Invite button, asking at every conversation which companions come along is no
      // longer needed. Off by default; on brings back the old question.
      [SettingPropertyBool("{=CR_MCM_AskCompanionsAtStart}Ask Which Companions Come Along", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_AskCompanionsAtStart_Hint}Off (default): a conversation opens with whoever is truly there; bring a companion, a lord riding with you or a prisoner in with the Invite button. On: as before 2.7.5, each conversation first asks which companions come along.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool AskCompanionsAtStart { get; set; } = false;

      // Player report (2026-08-05): in a scene with several NPCs, they kept talking to each other for a long
      // stretch before he could get a word in. This dial caps how many NPC-to-NPC exchanges fire per player
      // turn (was a hard-coded 3). 2 is the new shipped feel; 0 turns cross-talk off completely. See
      // CalradiaRemembers.Chat.ChatViewModel (WitnessExchangeCap) and ModSettings.NpcCrossTalkRounds.
      [SettingPropertyInteger("{=CR_MCM_NpcCrossTalkRounds}NPC Cross-Talk", 0, 3, "0", RequireRestart = false, Order = 10,
         HintText = "{=CR_MCM_NpcCrossTalkRounds_Hint}How many times NPCs may talk amongst themselves (one reacting to another) before the turn " +
                    "comes back to you, in a scene with witnesses. 2 is the default; 0 turns it off so only the " +
                    "NPC you spoke to replies. Applies on your next message.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public int NpcCrossTalkRounds { get; set; } = 2;

      // Player report (2026-08-05): letters get lost too often (almost every time) when the sender is far. The
      // per-band waylay roll compounds over a long trip. A distance-independent cap now blunts that; this toggle
      // is the total opt-out for a player who never wants a letter intercepted at all. Off = today's behaviour.
      [SettingPropertyBool("{=CR_MCM_InvulnerableCouriers}Invulnerable Couriers", RequireRestart = false, Order = 11,
         HintText = "{=CR_MCM_InvulnerableCouriers_Hint}Off: couriers can be intercepted by bandits or by the sender's enemies. A lost letter is sent " +
                    "again, and what it said travels with its captors to whoever beats them. On: no interception.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool InvulnerableCouriers { get; set; } = false;

      // kurushimi00 (Nexus, 31/10/2026): "In 30 seconds of gameplay [...] I got flooded with 6 more letter carriers [...] Please
      // do add an option to disable that mechanic altogether." The letter box took the weight of many letters, not the map's.
      [SettingPropertyBool("{=CR_MCM_LetterRidersOnMap}Letter Riders on the Map", RequireRestart = false, Order = 12,
         HintText = "{=CR_MCM_LetterRidersOnMap_Hint}On: a rider carries each letter to you across the map. Off: no rider comes; a letter reaches your box on its date instead and cannot be intercepted on the way. Riders already out turn back within a day. Your own letters still ride.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool LetterRidersOnMap { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_UnaskedLetters}Unasked Letters", RequireRestart = false, Order = 13,
         HintText = "{=CR_MCM_UnaskedLetters_Hint}Off: characters stop writing of their own accord. Still come: replies to your letters, war summons and levies, divorce, news of your children and their support, blackmail, threats, a courtship's answer, patrol reports, farewells, other mods' letters.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool UnaskedLetters { get; set; } = true;

      // SgtAlexGreen (Nexus, 04/11/2026): "letters being written when they are sent instead of opened? ... I click to open a
      // letter and then have to wait for it to be generated". Off by default: a letter never opened then costs nothing.
      [SettingPropertyBool("{=CR_MCM_WriteLettersOnArrival}Write Letters on Arrival", RequireRestart = false, Order = 13,
         HintText = "{=CR_MCM_WriteLettersOnArrival_Hint}On: each letter is written as it reaches your box, so it opens at once. Off: written when you open it, so a letter you throw away or never open costs nothing. The text is as of the day it was sent either way.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool WriteLettersOnArrival { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_CaptorConversations}Captor Conversations", RequireRestart = false, Order = 14,
         HintText = "{=CR_MCM_CaptorConversations_Hint}A lord who holds you may send for you to talk: courtesy, a ransom, an oath, an exchange. At most one a week, at every content level. Off: no such scene.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool CaptorConversations { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_RaidRestraint}Friends May Spare Your Villages", RequireRestart = false, Order = 15,
         HintText = "{=CR_MCM_RaidRestraint_Hint}When a war sets a friend or relative against you, they draw once: the closer they are, the likelier they leave your villages alone. Off: every lord raids as in the base game.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool RaidRestraint { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_Legends}Legends", RequireRestart = false, Order = 16,
         HintText = "{=CR_MCM_Legends_Hint}A battle won against the odds, a siege held or a last stand can become a legend that characters tell in their own way, yours included. At most one a season, your own excepted. Off: no new legend, none told.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool Legends { get; set; } = true;

      [SettingPropertyBool("{=CR_MCM_HiddenAffairs}Hidden Affairs Among Lords", RequireRestart = false, Order = 17,
         HintText = "{=CR_MCM_HiddenAffairs_Hint}Now and then a married lord takes a hidden lover, a handful at most. Send a companion to spy on a lord to find out. A found affair can be used or told. Off: no new affair. Never at adult content Off.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool HiddenAffairs { get; set; } = true;

      // "Intercepted letters can leak" (Order 12) is gone since 26/09/2026: Gabriel ruled that an intercepted letter
      // with substance ALWAYS becomes a secret carried in the world (CourierSecretBehavior); the one switch left is
      // Invulnerable Couriers above.

      // Player request (2026-08-04, "AI Executes" mod parity): a captive under torture wanted the scene able to
      // end their character for real. High-stakes and IRREVERSIBLE (game over, or heir succession), so this is
      // opt-in like AdultContentLevel/Hardcore, off by default, and re-validated by the bridge at the moment of
      // execution regardless of what this reads (see CalradiaRemembers.Logic.PlayerExecutionPolicy).
      [SettingPropertyBool("{=CR_MCM_AllowCaptorExecutions}Allow Captor Executions", RequireRestart = false, Order = 8,
         HintText = "{=CR_MCM_AllowCaptorExecutions_Hint}Let a captor's scene end your character, at their own deliberate choice, deep in a Hardcore " +
                    "captive scene. Off by default. Irreversible: game over, or your heir succeeds you.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool AllowCaptorExecutions { get; set; } = false;

      // COUNCIL_ACTIONS.md Partie 5 (the "Caladog" case): a king promised a fief and marriage to himself as
      // the reward of a quest, and both evaporated on completion (no executor existed). Off by default so the
      // existing guardrail (never promise a fief or marriage as the price of a task) stays fully intact for
      // everyone who never touches this menu; on, the guardrail relaxes AND the reward is genuinely delivered
      // (or honestly degrades) on completion, re-validated at both issuance and payout.
      [SettingPropertyBool("{=CR_MCM_AllowFiefAndMarriageQuestRewards}Allow Fief And Marriage Quest Rewards", RequireRestart = false, Order = 9,
         HintText = "{=CR_MCM_AllowFiefAndMarriageQuestRewards_Hint}Let a lord promise one of their own fiefs, or a marriage into their house, as a quest " +
                    "reward, delivered when you complete it. Off by default.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public bool AllowFiefAndMarriageQuestRewards { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_StoryLogging}Story Log", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_StoryLogging_Hint}Writes a clean narrative transcript of every conversation and letter to " +
                    "CalradiaRemembers_story.log in your Logs folder. No timestamps, no noise: it reads like a novel.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Diagnostics}Diagnostics", GroupOrder = 7)]
      public bool StoryLogging { get; set; } = false;

      [SettingPropertyBool("{=CR_MCM_ReportUnsupportedActions}Report unsupported actions", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_ReportUnsupportedActions_Hint}Shows a note in chat when the AI attempts an action the mod does not support yet. Every attempt is " +
                    "logged to CalradiaRemembers_unknown_actions.log regardless of this setting. Send that log to LogRaam on " +
                    "Nexus to request support.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Diagnostics}Diagnostics", GroupOrder = 7)]
      public bool ReportUnsupportedActions { get; set; } = false;

      [SettingPropertyButton("{=CR_MCM_TestConnection}Test Connection", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_TestConnection_Hint}Validates your key, then sends a one-token test to the roleplay model and, if set, the NSFW " +
                    "model, reporting any error. Costs a fraction of a cent.",
         Content = "{=CR_MCM_TestConnection_Button}Test")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenRouter}OpenRouter", GroupOrder = 1)]
      public Action TestConnection { get; set; } = () => ConnectionTest?.Invoke();

      [SettingPropertyButton("{=CR_MCM_TestCustomConnection}Test Endpoint", RequireRestart = false, Order = 5,
         HintText = "{=CR_MCM_TestCustomConnection_Hint}Sends a one-token test to your endpoint with the roleplay model and, if set, the NSFW model. " +
                    "Your server must be running. OpenAI-Compatible only.",
         Content = "{=CR_MCM_TestCustomConnection_Button}Test")]
      [SettingPropertyGroup("{=CR_MCM_Group_OpenAICompatible}OpenAI-Compatible", GroupOrder = 3)]
      public Action TestCustomConnection { get; set; } = () => CustomConnectionTest?.Invoke();

      [SettingPropertyButton("{=CR_MCM_TestNanoGptConnection}Test NanoGPT Connection", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_TestNanoGptConnection_Hint}Checks your NanoGPT key against nano-gpt.com without starting a conversation. No tokens spent.",
         Content = "{=CR_MCM_TestNanoGptConnection_Button}Test")]
      [SettingPropertyGroup("{=CR_MCM_Group_NanoGPT}NanoGPT", GroupOrder = 2)]
      public Action TestNanoGptConnection { get; set; } = () => NanoGptConnectionTest?.Invoke();

      /// <summary>
      ///   What the three Test buttons above run, set once by the submodule at load (Services' ApiKeyTester). The
      ///   settings sit in Foundation, which every namespace reads, and the tester in Services, which reads the
      ///   settings: naming it here made the two use each other (19/10/2026). Read at the click, so load order is moot.
      /// </summary>
      internal static Action ConnectionTest, CustomConnectionTest, NanoGptConnectionTest;

      // fkasad (Nexus, 01/10/2026): "almost everyone in my faction is at 100 just by doing chores"; Gabriel: a slider.
      [SettingPropertyInteger("{=CR_MCM_SelfEsteemClimb}Self-Esteem Climb", 0, 4, "0", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_SelfEsteemClimb_Hint}How hard a character's pride climbs once already high. 0: every success counts in full, as before. 2 (default): half way up, a victory counts a quarter; near the top, almost nothing. Higher: harder still. Applies immediately.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Character}Character", GroupOrder = 5)]
      public int SelfEsteemClimb { get; set; } = SelfEsteemPolicy.DefaultClimbExponent;

      [SettingPropertyBool("{=CR_MCM_UseCustomTraits}Use custom traits", RequireRestart = false, Order = 0,
         HintText = "{=CR_MCM_UseCustomTraits_Hint}Applies the hand-authored personalities from ModuleData/character_overrides.json, replacing an " +
                    "authored NPC's five vanilla traits. Edits load on the next campaign, or via cr.reload_lore.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Character}Character", GroupOrder = 5)]
      public bool UseCustomTraits { get; set; } = false;

      [SettingPropertyText("{=CR_MCM_CustomFolderPath}Custom Folder Path", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_CustomFolderPath_Hint}Where your edited prompts and styles are read from. Blank = the mod's own ModuleData/Custom. An absolute " +
                    "path keeps them outside the mod so an update never wipes them (re-enter it afterwards). Applies on the " +
                    "next campaign or cr.reload_lore.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Character}Character", GroupOrder = 5)]
      public string CustomFolderPath { get; set; } = "";

      [SettingPropertyBool("{=CR_MCM_VerboseLogging}Verbose Logging", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_VerboseLogging_Hint}Logs the full system prompt and NPC reply on every exchange. Turn on only to capture a bug " +
                    "report: it floods the log. Requires Enable Logging.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Diagnostics}Diagnostics", GroupOrder = 7)]
      public bool VerboseLogging { get; set; } = false;

      // Player complaint: conversations are ALWAYS crowded with ambient bystanders. Thins the AMBIENT hero
      // witnesses a conversation gathers only; never the player's own companions (see
      // CalradiaRemembers.Logic.Witnesses.WitnessPresencePolicy + WitnessResolver.ResolveAutoWitnesses). Default Bustling
      // is an EXACT passthrough (p=1.0, cap=int.MaxValue): the mod is LIVE, so an existing player who never opens
      // this menu sees precisely today's room, unchanged.
      [SettingPropertyDropdown("{=CR_MCM_WitnessDensity}Witness Density", RequireRestart = false, Order = 12,
         HintText = "{=CR_MCM_WitnessDensity_Hint}How many ambient bystanders a conversation gathers. Sparse thins the room fast; Normal keeps " +
                    "a genuine middle ground; Bustling (default) is today's full room, unchanged. Never touches " +
                    "companions you bring yourself. Applies on your next conversation.")]
      [SettingPropertyGroup("{=CR_MCM_Group_Content}Content", GroupOrder = 4)]
      public Dropdown<string> WitnessDensity { get; set; } =
         new(new[] {"Sparse", "Normal", "Bustling"}, 2); // default: Bustling (today's live behaviour, unchanged)

      // The world's initiative (Gabriel, 28/10/2026): one director decides whose turn it is to come to the player unasked,
      // and the player tunes it ("combien de systemes peuvent se manifester en meme temps, le delai avant que l'orchestrateur
      // decide de lancer une intervention"). Off leaves every system to pace itself as before.
      [SettingPropertyDropdown("{=CR_MCM_WorldInitiativePace}How Often the World Comes to You", RequireRestart = false, Order = 1,
         HintText = "{=CR_MCM_WorldInitiativePace_Hint}One conductor decides whose turn it is to come to you unasked (lords, companions, tales...). Off: each paces itself, as before. Custom uses the settings below.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public Dropdown<string> WorldInitiativePace { get; set; } =
         new(new[] {"Off", "Rarely", "Occasionally", "Often", "Custom"}, 2); // default: Occasionally

      [SettingPropertyInteger("{=CR_MCM_InitiativeAtOnce}At Once (Custom)", 1, 3, "0", RequireRestart = false, Order = 2,
         HintText = "{=CR_MCM_InitiativeAtOnce_Hint}How many of them may be at you at the same time, e.g. a lord riding after you and a word asked in town. Custom only.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeAtOnce { get; set; } = 1;

      [SettingPropertyInteger("{=CR_MCM_InitiativePerWindow}Interruptions per Window (Custom)", 1, 10, "0", RequireRestart = false, Order = 3,
         HintText = "{=CR_MCM_InitiativePerWindow_Hint}How many times the world may come to you within the window below. A letter that asks for a turn counts as set further down. Custom only.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativePerWindow { get; set; } = 3;

      [SettingPropertyInteger("{=CR_MCM_InitiativeWindowDays}Window (days, Custom)", 1, 30, "0", RequireRestart = false, Order = 4,
         HintText = "{=CR_MCM_InitiativeWindowDays_Hint}The span, in game days, the interruptions above are counted over. Custom only.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeWindowDays { get; set; } = 7;

      [SettingPropertyInteger("{=CR_MCM_InitiativeGapHours}Time Between Two (hours, Custom)", 0, 72, "0", RequireRestart = false, Order = 5,
         HintText = "{=CR_MCM_InitiativeGapHours_Hint}The least time, in game hours, between two interruptions. Custom only.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeGapHours { get; set; } = 12;

      [SettingPropertyInteger("{=CR_MCM_InitiativeSettlingHours}Settling Delay (hours, Custom)", 0, 24, "0", RequireRestart = false, Order = 6,
         HintText = "{=CR_MCM_InitiativeSettlingHours_Hint}How long the world waits, in game hours, after a battle or a conversation ends before it comes to you. Custom only.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeSettlingHours { get; set; } = 2;

      [SettingPropertyInteger("{=CR_MCM_InitiativeQuietHours}Quiet After Your Own Talk (hours, Custom)", 0, 96, "0", RequireRestart = false, Order = 7,
         HintText = "{=CR_MCM_InitiativeQuietHours_Hint}After a conversation you opened yourself, the world leaves you alone this many game hours. Custom only.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeQuietHours { get; set; } = 24;

      [SettingPropertyDropdown("{=CR_MCM_InitiativeVariety}Variety", RequireRestart = false, Order = 8,
         HintText = "{=CR_MCM_InitiativeVariety_Hint}How far a kind that just came to you steps back so the others get their turn. The world's urgent matters are not affected.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public Dropdown<string> InitiativeVariety { get; set; } =
         new(new[] {"Low", "Normal", "Strong"}, 1); // default: Normal

      [SettingPropertyInteger("{=CR_MCM_InitiativeExpireDays}Requests Expire After (days)", 1, 30, "0", RequireRestart = false, Order = 9,
         HintText = "{=CR_MCM_InitiativeExpireDays_Hint}A wish to come to you that waited this many game days is dropped, like stale news. The world's urgent matters never expire.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeExpireDays { get; set; } = 10;

      [SettingPropertyDropdown("{=CR_MCM_InitiativeLettersCountAs}Letters Count As", RequireRestart = false, Order = 10,
         HintText = "{=CR_MCM_InitiativeLettersCountAs_Hint}Most letters ask for no turn. This is what the few that do cost from the count above: a lord's friendship or threat, a companion's farewell, a divorce demand.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public Dropdown<string> InitiativeLettersCountAs { get; set; } =
         new(new[] {"A Full Interruption", "Half", "Nothing"}, 1); // default: Half

      [SettingPropertyBool("{=CR_MCM_InitiativeUrgentWait}Urgent Matters Wait Their Turn", RequireRestart = false, Order = 11,
         HintText = "{=CR_MCM_InitiativeUrgentWait_Hint}Off (default): an arrest, your liege's war summons, an ambush or an envoy with a short deadline comes whenever the moment allows. On: they wait their turn like the rest.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public bool InitiativeUrgentWait { get; set; } = false;

      [SettingPropertyInteger("{=CR_MCM_InitiativeWeightLords}Weight: Lords (%)", 0, 200, "0 '%'", RequireRestart = false, Order = 12,
         HintText = "{=CR_MCM_InitiativeWeightLords_Hint}How much lords seeking a word or riding out to you weigh when the turn is drawn. 0 turns them off; 100 is even. Each still gets a turn a month when it waits.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeWeightLords { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_InitiativeWeightCompanions}Weight: Companions (%)", 0, 200, "0 '%'", RequireRestart = false, Order = 13,
         HintText = "{=CR_MCM_InitiativeWeightCompanions_Hint}How much your companions asking for a word weigh when the turn is drawn. 0 turns them off; 100 is even. Each still gets a turn a month when it waits.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeWeightCompanions { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_InitiativeWeightHousehold}Weight: Household (%)", 0, 200, "0 '%'", RequireRestart = false, Order = 14,
         HintText = "{=CR_MCM_InitiativeWeightHousehold_Hint}How much your spouse and kin weigh when the turn is drawn. 0 turns them off; 100 is even. Each still gets a turn a month when it waits.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeWeightHousehold { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_InitiativeWeightTales}Weight: Tales (%)", 0, 200, "0 '%'", RequireRestart = false, Order = 16,
         HintText = "{=CR_MCM_InitiativeWeightTales_Hint}How much written tales offered on the map weigh when the turn is drawn. 0 turns them off; 100 is even. Each still gets a turn a month when it waits.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeWeightTales { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_InitiativeWeightWarAndLaw}Weight: War and Law (%)", 0, 200, "0 '%'", RequireRestart = false, Order = 17,
         HintText = "{=CR_MCM_InitiativeWeightWarAndLaw_Hint}How much war and the law (not the urgent matters) weigh when the turn is drawn. 0 turns them off; 100 is even. Each still gets a turn a month when it waits.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeWeightWarAndLaw { get; set; } = 100;

      [SettingPropertyInteger("{=CR_MCM_InitiativeWeightAims}Weight: Lords' Aims (%)", 0, 200, "0 '%'", RequireRestart = false, Order = 18,
         HintText = "{=CR_MCM_InitiativeWeightAims_Hint}How much lords putting a matter of their own to you weigh when the turn is drawn. 0 turns them off; 100 is even. Each still gets a turn a month when it waits.")]
      [SettingPropertyGroup("{=CR_MCM_Group_WorldInitiative}The World's Initiative", GroupOrder = 10)]
      public int InitiativeWeightAims { get; set; } = 100;
   }
}