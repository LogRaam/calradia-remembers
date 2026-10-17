// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
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

      /// <summary>Intercepted-letter-leak Inc 1: when ON, a bandit-waylaid substantive letter may leak to a hostile hand. OFF by default (today's behaviour).</summary>

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

      /// <summary>The MCM "Prompt Profile" list (the Prompt Composer), filled and read by Mapping.PromptProfileService; null without MCM.</summary>
      public static MCM.Common.Dropdown<string> PromptProfile => CrSettings.Instance?.PromptProfile;

      /// <summary>When ON (default), the player can convene a council from a settlement menu and the campaign-map button; OFF hides those entry points (console still works).</summary>
      public static bool EnableCouncil => CrSettings.Instance?.EnableCouncil ?? true;

      /// <summary>When ON (default), the Balance of Power Strategic Assessment is an AI counsel from a companion/soldier; OFF (or alone) uses an offline summary.</summary>
      public static bool EnableStrategicAssessment => CrSettings.Instance?.EnableStrategicAssessment ?? true;

      /// <summary>
      ///   Whether an idle other-clan lord may ride IN the player's own party as a retainer (ride_with_me). Read
      ///   live from the MCM menu ("Lords can ride in your party"); ON by default (the party-screen/UI feel was
      ///   validated in-game 2026-08-11) and default-safe (treated as ON) when MCM has not loaded. See
      ///   <see cref="Retainer.RetainerBehavior.Enabled" />, which layers a session-only console override
      ///   (cr.retainer enable [on|off]) on top of this live default.
      /// </summary>
      public static bool EnableRideWithMe => CrSettings.Instance?.EnableRideWithMe ?? true;

      /// <summary>
      ///   Whether a fact a character invents may become a real rumour others can hear (Gabriel, 2026-09-12).
      ///   Falls back to FALSE rather than to the usual "on" default: this one writes permanent rows into the
      ///   player's save, so a settings screen that has not loaded yet must not be read as consent.
      /// </summary>
      public static bool EnableNpcWorldClaims => CrSettings.Instance?.EnableNpcWorldClaims ?? false;

      /// <summary>
      ///   When ON (default), an NPC's end_conversation closes an ordinary Chatroom conversation (input locks,
      ///   Leave button). OFF leaves the chat open so the player keeps the last word and closes it themselves.
      ///   Captive/captor scenes ignore this: they always end on their own Continue/resume flow.
      /// </summary>
      public static bool NpcCanEndConversation => CrSettings.Instance?.NpcCanEndConversation ?? true;

      /// <summary>
      ///   When ON (default), a fought duel reopens as a conversation resuming what led to it (the loser concedes,
      ///   the victor presses their point); OFF ends a fought duel silently with only its consequences applied.
      ///   Read live from MCM, default-safe (treated as ON) when MCM has not loaded.
      /// </summary>
      public static bool EnablePostDuelConversation => CrSettings.Instance?.EnablePostDuelConversation ?? true;

      /// <summary>
      ///   Master switch for the whole jealousy system (the daily competition loop AND event-triggered
      ///   reactions). Read live from the MCM menu; ON by default, and default-safe (treated as ON) when MCM
      ///   has not loaded. When false, <see cref="Jealousy.JealousyBehavior" /> early-returns from its daily
      ///   tick and its marriage hook, so no jealousy of any kind ever fires.
      /// </summary>
      public static bool EnableJealousy => CrSettings.Instance?.EnableJealousy ?? true;

      /// <summary>
      ///   A clan's standing gives its one-time warm start to personal regard (see <see cref="Logic.ClanStandingSeedPolicy" />).
      ///   On by default.
      /// </summary>
      public static bool ClanStandingWarmsFirstMeetings => CrSettings.Instance?.ClanStandingWarmsFirstMeetings ?? true;

      /// <summary>
      ///   Whether a mercenary's (or fiefless vassal's) departure is judged by the realm's lords, the player's and the
      ///   game's companies alike (MCM "Mercenary departures are judged", default on; Fakade, 04/10/2026).
      /// </summary>
      public static bool JudgeMercenaryDepartures => CrSettings.Instance?.JudgeMercenaryDepartures ?? true;

      /// <summary>
      ///   Stolen Love pillar toggle, read live from the MCM menu. Off (default) keeps the shipped affair rule for
      ///   a married NPC; on turns the personality-gated seduction path on (see <see cref="Logic.SeductionEligibilityPolicy" />).
      /// </summary>
      public static bool EnableStolenLove => CrSettings.Instance?.EnableStolenLove ?? false;

      /// <summary>Vassal war duty (Maifailun, Gabriel, 15/10/2026): off by default; gates what the ruler does, not the ledger.</summary>
      public static bool EnableWarDuty => CrSettings.Instance?.EnableWarDuty ?? false;

      /// <summary>Days of war a sworn vassal may go without serving (clamped by WarDutyPolicy.Window).</summary>
      public static int WarDutyWindowDays => Logic.WarDuty.WarDutyPolicy.Window(CrSettings.Instance?.WarDutyWindowDays ?? Logic.WarDuty.WarDutyPolicy.DefaultWindowDays);

      /// <summary>The levy multiplier (1 = as designed), from the MCM percentage.</summary>
      public static double WarLevyScale => (CrSettings.Instance?.WarLevyScalePercent ?? 100) / 100.0;

      /// <summary>
      ///   Player violence (player request 2026-09-06): OFF by default, and it governs KILLING ONLY. Robbery and
      ///   assault are always available; while this is off, no exchange can end a character's life, whatever the
      ///   model narrates. Read by <see cref="Logic.Violence.ViolencePolicy.IsPermitted" /> at BOTH teaching and
      ///   execution time, so the prompt never dangles a deed the bridge would refuse. Defaults to false when MCM
      ///   is unavailable, which keeps the safe answer the safe one.
      /// </summary>
      public static bool AllowKillingCharacters => CrSettings.Instance?.AllowKillingCharacters ?? false;

      /// <summary>
      ///   Arrests and outlaws (Gabriel, 26/09/2026): OFF by default. Gates only what opens a case (a warrant, a
      ///   brigandage); a case already open runs to its end. Off when MCM is unavailable.
      /// </summary>
      public static bool EnableArrests => CrSettings.Instance?.EnableArrests ?? false;

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
      ///   How large the council (round-table) window is drawn, INDEPENDENTLY of the chat's Dark Leather size: the
      ///   council book is much wider than a chat window, so on a high-resolution screen it can overflow at the same
      ///   percent the chat fits at (player report, 2026-08-25). 1.0 (100%) is the shipped council size; clamped to
      ///   [0.6, 1.4] to match the slider even if a stale config carries something outside it; 1.0 when MCM has not
      ///   loaded. Applies to the NEXT council opened (the fixed book size is read when the window is built).
      /// </summary>
      public static float CouncilScale
      {
         get
         {
            int pct = CrSettings.Instance?.CouncilZoomPercent ?? 100;
            if (pct < 60) pct = 60;
            if (pct > 140) pct = 140;
            return pct / 100f;
         }
      }

      /// <summary>
      ///   The regard a candidate must hold for a LOVE-MATCH marriage to be allowed (personal ledger OR vanilla
      ///   relation, whichever is higher), read live from the MCM menu. Lets a player soften the courtship (player
      ///   report 2026-08-25: reaching the shipped bar of 50 by conversation felt impossible). Defaults to and is
      ///   capped at the shipped bar (<see cref="Logic.MarriageEligibility.MarriageEligibilityThreshold" />, 50) so
      ///   the setting can only make marriage EASIER, never harder (which also keeps the self-test's regard seed
      ///   valid); floored at 10 so a wedding is never entirely free. 50 when MCM has not loaded. The family's
      ///   blessing still opens a match below this bar (a political union), exactly as before.
      /// </summary>
      /// <summary>
      ///   The self-esteem falloff exponent (fkasad, 01/10/2026; Gabriel: an MCM slider): 0 keeps every gain whole, the
      ///   shipped 2 makes a gain worth a quarter half way up. Clamped to the menu's own 0..4; the shipped value when MCM
      ///   has not loaded.
      /// </summary>
      public static int SelfEsteemClimb
      {
         get
         {
            int v = CrSettings.Instance?.SelfEsteemClimb ?? SelfEsteemPolicy.DefaultClimbExponent;

            return v < 0 ? 0 : v > 4 ? 4 : v;
         }
      }

      public static int MarriageRegardThreshold
      {
         get
         {
            int v = CrSettings.Instance?.MarriageRegardNeeded ?? MarriageEligibility.MarriageEligibilityThreshold;
            if (v < 10) v = 10;
            if (v > MarriageEligibility.MarriageEligibilityThreshold) v = MarriageEligibility.MarriageEligibilityThreshold;

            return v;
         }
      }

      /// <summary>
      ///   Progressive-regard model: in-game days for one step of a character's warmth resistance to relax (see
      ///   <see cref="RelationGate" />). Defaults to the shipped 7 when MCM has not loaded; clamped to a sane band
      ///   so a hand-edited config cannot absurdly stretch it. ZERO IS ALLOWED and means the cooldown is off: the
      ///   resistance never builds, so warmth costs the same on the hundredth conversation as on the first.
      /// </summary>
      public static int RegardDecayDays
      {
         get
         {
            int v = CrSettings.Instance?.RegardDecayDays ?? (int) RelationGate.DefaultDecayDays;
            // 0 is a real choice, not a broken config: it is the "cooldown off" the v2.5.1 changelog promised,
            // and clamping it to 1 is why no player could ever take it (FloatingMammal, 2026-09-10).
            if (v < 0) v = 0;
            if (v > 30) v = 30;

            return v;
         }
      }


      /// <summary>
      ///   How large the chat DIALOGUE TEXT is drawn, independently of the window size: since the window scale
      ///   scales the whole book (font included), text-per-line stayed constant however the player sized the
      ///   window (player report, 2026-08-25). This scales only the message font, so a smaller value fits more
      ///   words per line. Returned as a snapped 10% step in [70, 130] because the size reaches the widget as a
      ///   sized BRUSH NAME (see <see cref="SnapTextSizeStep" />); 100 when MCM has not loaded. Applies to the
      ///   next conversation (the brush name is read when each message binds).
      /// </summary>
      public static int ChatTextSizeStep => SnapTextSizeStep(CrSettings.Instance?.ChatTextSizePercent ?? 100);

      /// <summary>
      ///   How large the council (round-table) DIALOGUE TEXT is drawn, INDEPENDENTLY of the chat text size
      ///   above. Same snapping and apply-on-next-window semantics as <see cref="ChatTextSizeStep" />.
      /// </summary>
      public static int CouncilTextSizeStep => SnapTextSizeStep(CrSettings.Instance?.CouncilTextSizePercent ?? 100);

      /// <summary>
      ///   Brush.FontSize is NOT a valid Gauntlet bind target (binding it crashed the message list, revert
      ///   b55af7e), so the dialogue font size reaches the MessageText widget as a sized brush NAME, which IS
      ///   bindable (the letter brushes prove the pattern). Sized brushes exist only per 10% step
      ///   (GUI/Brushes/CalradiaRemembersChatText.xml), so the continuous MCM percent is snapped to the nearest
      ///   step after clamping to the slider's own [70, 130] bounds (a stale/edited config can carry anything).
      /// </summary>
      private static int SnapTextSizeStep(int pct)
      {
         if (pct < 70) pct = 70;
         if (pct > 130) pct = 130;
         return (int)Math.Round(pct / 10.0, MidpointRounding.AwayFromZero) * 10;
      }

      /// <summary>
      ///   How readily lords ride out to have a word with the player on the road (player request, Fakade). Read
      ///   live from the MCM menu; Occasionally (the shipped feel) when MCM has not loaded, so nothing changes for
      ///   a player who never touches it.
      /// </summary>
      /// <summary>
      ///   Whether an NPC may stop the player over news that does not concern them
      ///   (<c>RecentMatterRegister.WorldGossip</c>). Off by default: see CrSettings for the three reports.
      ///   Nothing else about approaching is touched by it.
      /// </summary>
      public static bool NpcsRaiseWorldNews => CrSettings.Instance?.NpcsRaiseWorldNews ?? false;

      /// <summary>
      ///   A lord riding out to the player is held on course (on, the 2.6.8 behaviour Lordfadooboo asked for) or only
      ///   given the order, his own AI free to change its mind (off, as before; Fakade, 03/10/2026). On when MCM has
      ///   not loaded.
      /// </summary>
      public static bool LordsHoldTheirCourse => CrSettings.Instance?.LordsHoldTheirCourse ?? true;

      /// <summary>A conversation opens by asking which companions come along (off by default since 2.7.5: the Invite button does it).</summary>
      public static bool AskCompanionsAtStart => CrSettings.Instance?.AskCompanionsAtStart ?? false;

      /// <summary>Characters may offer a written tale (authored quests); on when MCM has not loaded. Off stops new offers only.</summary>
      public static bool AuthoredQuests => CrSettings.Instance?.AuthoredQuests ?? false;

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
      ///   How often a travelling companion asks for a private word, on the road as well as entering a
      ///   settlement (player report, 2026-09-08: "unless I remember to routinely engage with my travel
      ///   buddies, they never say anything on their own"). Named distinctly from the
      ///   <see cref="CompanionAudiencePacing" /> enum it returns (same reason <see cref="LordsApproaching" />
      ///   is not called InterceptionPacing) so the getter body never has to fight its own name for the type.
      ///   Read live from the MCM menu; Occasionally (the shipped settlement feel, plus a road chance) when MCM
      ///   has not loaded, so nothing changes for a player who never touches it.
      /// </summary>
      public static CompanionAudiencePacing CompanionAudienceCadence
      {
         get
         {
            string v = CrSettings.Instance?.CompanionAudiencePacing?.SelectedValue;

            switch (v)
            {
               case "Never": return CompanionAudiencePacing.Never;
               case "Rarely": return CompanionAudiencePacing.Rarely;
               case "Often": return CompanionAudiencePacing.Often;
               default: return CompanionAudiencePacing.Occasionally;
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
      // Also on once CR had to turn to the compact prompt to fit the local model's declared window (fkasad, 13/10/2026):
      // it stays compact for the session, so the prompt does not flip back and forth (and a local server does not
      // re-read ~20k tokens of prefix on every flip). See LocalContextState.
      public static bool LeanPrompt => (CrSettings.Instance?.LeanPrompt ?? false) || LocalContextState.CompactThisSession;

      /// <summary>
      ///   The player's own Compact toggle, without the session's forced compact. For a caller on ANOTHER endpoint than
      ///   the chat (the action interpreter), which fits its own window and must not lose quality to the chat's.
      /// </summary>
      public static bool LeanPromptChosen => CrSettings.Instance?.LeanPrompt ?? false;

      /// <summary>The local model's context window in tokens, as the player declared it; 0 when none (cloud models).</summary>
      public static int LocalContextTokens => Math.Max(0, CrSettings.Instance?.LocalContextTokens ?? 0);

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
      ///   The temperature a conversation turn is sampled at (the "Chat Temperature" MCM slider, default 0.7), held
      ///   in range by <see cref="Logic.ChatTemperaturePolicy" />. Read live; the chat service rebuilds when it moves.
      /// </summary>
      public static float ChatTemperature => Logic.ChatTemperaturePolicy.Clamp(CrSettings.Instance?.ChatTemperature ?? Logic.ChatTemperaturePolicy.Default);

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
      ///   The model that completes a beat the PROSE model cut off mid-sentence (Prose + Interpreter mode only,
      ///   see <see cref="Logic.TruncationFallbackPolicy" />): the "Prose fallback model" MCM field. Blank (the
      ///   default) means "use the Action Interpreter model" (resolved by
      ///   <see cref="Services.LlmServices.ResolveProseFallbackModel" />), so no new config is needed to get the
      ///   safety net. Read live from the MCM menu.
      /// </summary>
      public static string ProseFallbackModel => CrSettings.Instance?.ProseFallbackModel?.Trim() ?? "";

      /// <summary>
      ///   The RAW provider value for the roleplay / prose role: the "Roleplay / Prose provider" MCM field, whose
      ///   property is still named <c>Provider</c> for save compatibility (it was the released global provider). This
      ///   role is the BASE the other three inherit when left on "(default)"; it carries no "(default)" itself, so a
      ///   concrete provider value is always returned (OpenRouter when MCM has not loaded).
      /// </summary>
      public static string RoleplayProviderSelection =>
         CrSettings.Instance?.Provider?.SelectedValue ?? ProviderRolePolicy.OpenRouterValue;

      /// <summary>
      ///   The RAW per-role provider dropdown value for the consensual-intimate role (the "Consensual provider" MCM
      ///   field), or <see cref="Logic.ProviderRolePolicy.DefaultSelection" /> when MCM has not loaded.
      /// </summary>
      public static string ConsensualProviderSelection =>
         CrSettings.Instance?.ConsensualProvider?.SelectedValue ?? ProviderRolePolicy.DefaultSelection;

      /// <summary>
      ///   The RAW per-role provider dropdown value for the CNC / captive role (the "CNC provider" MCM field), or
      ///   <see cref="Logic.ProviderRolePolicy.DefaultSelection" /> when MCM has not loaded.
      /// </summary>
      public static string CncProviderSelection =>
         CrSettings.Instance?.CncProvider?.SelectedValue ?? ProviderRolePolicy.DefaultSelection;

      /// <summary>
      ///   The RAW per-role provider dropdown value for the action-interpreter role (the "Actions Interpreter
      ///   provider" MCM field), or <see cref="Logic.ProviderRolePolicy.DefaultSelection" /> when MCM has not loaded.
      /// </summary>
      public static string ActionInterpreterProviderSelection =>
         CrSettings.Instance?.ActionInterpreterProvider?.SelectedValue ?? ProviderRolePolicy.DefaultSelection;

      /// <summary>Which provider the mod's BACKGROUND work is sent to; "(default)" inherits the prose provider.</summary>
      public static string HousekeepingProviderSelection =>
         CrSettings.Instance?.HousekeepingProvider?.SelectedValue ?? ProviderRolePolicy.DefaultSelection;

      /// <summary>
      ///   The model for the mod's background work (summaries, compression, extraction), or blank to use the
      ///   prose model, which is what every install did before this existed. See ServiceRole.Housekeeping.
      /// </summary>
      public static string HousekeepingModel => CrSettings.Instance?.HousekeepingModel ?? "";

      /// <summary>
      ///   How a chat reply is composed: <see cref="Logic.ChatCompositionMode.Standard" /> (one call, today's
      ///   behaviour) or <see cref="Logic.ChatCompositionMode.Enhanced" /> (prose then a separate action-
      ///   interpreter call). Read live from the MCM menu; defaults to Integrated on anything unexpected.
      /// </summary>
      public static ChatCompositionMode ChatCompositionMode =>
         ChatCompositionModePolicy.Parse(CrSettings.Instance?.ChatCompositionModeSetting?.SelectedValue);

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
               ? ModelModePolicy.SameAsProse
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
      ///   The reasoning-effort keyword for a local/OpenAI-Compatible endpoint (Default/Off/Minimal/Low/Medium/
      ///   High), read live from the MCM menu. Sent on the wire as the top-level <c>reasoning_effort</c> string
      ///   (which Ollama honors), unlike the OpenRouter <c>reasoning</c> object. Falls back to <c>Default</c>
      ///   (send nothing) so an untouched local setup is unchanged: this is a separate dial from the OpenRouter
      ///   <see cref="ReasoningEffort" /> precisely because that one defaults to Low.
      /// </summary>
      public static string CustomReasoningEffort
      {
         get
         {
            string v = CrSettings.Instance?.CustomReasoningEffort?.SelectedValue;

            return string.IsNullOrWhiteSpace(v)
               ? "Default"
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

      /// <summary>
      ///   How many ambient hero witnesses a conversation gathers (player complaint: conversations are always
      ///   crowded). Read live from the MCM menu; falls back to <see cref="Logic.WitnessDensity.Bustling" />
      ///   when MCM has not loaded, which is an EXACT passthrough (see
      ///   <see cref="Logic.WitnessPresencePolicy" />) reproducing today's live behaviour unchanged. Applied
      ///   only to AMBIENT hero witnesses (<see cref="Witnesses.WitnessResolver.ResolveAutoWitnesses" />),
      ///   NEVER to the player's own companions (<see cref="Witnesses.WitnessResolver.ResolveSceneCompanions" />).
      /// </summary>
      public static WitnessDensity WitnessDensity
      {
         get
         {
            string v = CrSettings.Instance?.WitnessDensity?.SelectedValue;

            switch (v)
            {
               case "Sparse": return WitnessDensity.Sparse;
               case "Normal": return WitnessDensity.Normal;
               default: return WitnessDensity.Bustling;
            }
         }
      }
   }
}