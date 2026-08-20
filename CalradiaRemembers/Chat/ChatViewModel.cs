// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.GUI;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Portraits;
using CalradiaRemembers.Services;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Styles;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel : ViewModel, IRoundTableHost, IActionConfirmationHost, IOfferHost
   {
      // Externalized scene STRUCTURE: a director walks an explicit stage machine (Intro → Initiate
      // → Intensify → Climax → Conclude, plus collective sub-acts) and feeds the model ONE named
      // beat per turn with a reminder of what is already done. This is what stops the model looping
      // a beat or cramming the arc. A paranoia beat cap force-ends a scene that somehow runs away.
      private const int CaptiveBeatSafetyCap = 16;

      // Cream color matching ChatMessageViewModel's default NPC hue, used when the
      // main NPC appears as a reacting witness (while another participant speaks).
      private const string NpcCreamColor = "#e8d8b8FF";
      private const string OocColor = "#8fb8c8FF"; // muted slate-cyan, clearly out-of-character (terminal prompts)
      private const string PlaceholderText = "(considering...)";

      /// <summary>
      ///   Council narrator model (2026-08-02): the waiting placeholder for a REAL council turn, replacing the
      ///   ordinary "(considering...)": that text reads as a single NPC pondering, which is exactly the
      ///   presiding-voice framing the narrator model drops. The council as a whole is weighing the player's
      ///   words, not any one seated member.
      /// </summary>
      private const string CouncilPlaceholderText = "The council weighs your words...";

      // Scene narration (neutral third-person narrator prose during captive/CNC scenes):
      // pale violet, distinct from dialogue, no portrait — reads like a novel's narration.
      private const string SceneNarrationColor = "#c0a8d0FF";

      // System notification line colors (relation/gold outcomes shown inline in chat).
      private const string SystemGainColor = "#9fe0a0FF"; // soft green — favorable
      private const string SystemLossColor = "#e0a0a0FF"; // soft red — unfavorable

      // ── Sprint 15C: NPC→witness auto-exchange ─────────────────────────────
      // Maximum NPC→witness auto-exchanges per player turn. Each exchange is one
      // LLM call; the chain terminates early if witnesses fall silent.
      // How many NPC-to-NPC witness exchanges may fire per player turn. Player-adjustable since a report
      // (2026-08-05) that multi-NPC scenes ran on too long before he could reply; 2 by default, 0 turns
      // cross-talk off. Read live from the MCM menu each turn (see ModSettings.NpcCrossTalkRounds).
      private static int WitnessExchangeCap => ModSettings.NpcCrossTalkRounds;

      // Shortest gap between two wrapped-preview refreshes; ~150 ms is imperceptible for reading but turns a
      // per-keystroke re-layout of a long message into a handful per second.
      private static readonly long ComposePreviewThrottleTicks = TimeSpan.FromMilliseconds(150).Ticks;

      // The per-insert cap once collapsed the transcript to one live tableau per speaker, which blanked older
      // lines' portraits. MEASURED SAFE and relaxed (2026-07-09): over a long conversation the game's dedicated
      // VRAM stayed flat as live portraits climbed, because the original v1.23 leak was the map screen rendering
      // behind the overlay (fixed, unrelated to portrait count) and we free every tableau on close
      // (ReleaseAllPortraits). So all portraits now persist. Flip back to true only if a VRAM report resurfaces.
      // static readonly, not const: keeps this a runtime kill-switch (flip to true to re-enable the cap) without
      // the compiler folding the const away and flagging the guard below as unreachable dead code.
      private static readonly bool EnforcePortraitCapInChat = false;

      private static readonly string[] EscapeFailureFallbacks = {
         "An iron grip closes on you and hauls you back. \"Going somewhere? I think not.\"",
         "You barely move before you are slammed down again. \"That was a mistake. You'll pay for it.\"",
         "A fist twists into your hair and wrenches you back into place. \"Try that again and lose a hand.\""
      };

      // Generic fallbacks used only when the constrained outcome beat fails or refuses.
      private static readonly string[] EscapeSuccessFallbacks = {
         "\"After them! AFTER THEM!\" — but the dark has already swallowed the prisoner, and the shouting comes to nothing.",
         "A snarl of rage tears from your throat as your hands close on empty air. They are gone.",
         "\"NO— get back here!\" Too late. The night has taken them, and you are left cursing at shadows."
      };

      // Hash-based palette: each witness gets a deterministic color from their
      // HeroStringId so Martira is always the same shade across conversations.
      private static readonly string[] WitnessPalette = {
         "#88c890FF", // soft green
         "#d4a84bFF", // warm gold
         "#c87878FF", // muted rose
         "#a08ac8FF" // light purple
      };

      private static bool _llmFailureHintShown;

      // ── Action confirmation (terminal-style, in chat) ─────────────────────
      // A consequential LLM action (coin transfer / service commitment) is not applied on the spot: it is queued
      // and the player asked to confirm IN CHAT. The whole feature (queue, dedup, Y/n flow, apply/decline) lives
      // in its own controller so it no longer threads through the shared ChatViewModel partials (mirroring the
      // round-table extraction); the controller drives this view model through the IActionConfirmationHost seam.
      private readonly ActionConfirmationController _confirmation = new ActionConfirmationController();

      // Last reaction text shown per witness (canonical name → text). Used to drop a witness's
      // verbatim-repeated line: weaker models copy their own [WITNESS_REACTION] turn after turn,
      // which soft prompt rules do not stop — so we filter the duplicate mechanically.
      private readonly Dictionary<string, string> _lastWitnessReaction = new(StringComparer.OrdinalIgnoreCase);

      // ── Offer ("give") flow ───────────────────────────────────────────────
      // The bag (item/gold/prisoner gift, plus settling an outstanding bargain or supply quest) lives in its own
      // controller so it no longer threads through the shared ChatViewModel partials (mirroring the round-table and
      // action-confirmation extractions); the controller owns _offeredItemId and drives this view model through the
      // IOfferHost seam. ExecuteOfferItem/ExecuteOfferGold stay here because their buttons are prefab-bound.
      private readonly OfferController _offer = new OfferController();

      // ── Round table (group mode) ──────────────────────────────────────────
      // The group-lap feature (open the floor to everyone) lives in its own controller so it no longer threads
      // through the shared ChatViewModel partials (which forced a surgical-commit dance every release). The
      // controller owns the "requested"/rotation state and drives the deterministic lap through the
      // IRoundTableHost seam (this view model). _isRoundTableVisible backs the prefab-bound IsRoundTableVisible
      // button; its value is computed by the controller.
      private readonly RoundTableController _roundTable = new RoundTableController();

      // Attributed conversation history — the single source of truth for rebuilding
      // each speaker's ChatSession per turn (their own lines as assistant, everyone
      // else's as "[Name]: …" user lines). Kept in lockstep with the displayed
      // conversational messages (player, main NPC, witnesses); system lines excluded.
      private readonly ConversationTranscript _transcript = new();
      private readonly Dictionary<string, string> _witnessColors = new();

      // ── Multi-NPC witnesses ───────────────────────────────────────────────
      // Mutable list — authoritative for the session; cleared on privacy-accepted.
      // Passed into EncounterContext each turn so PromptBuilder can inject them.
      private readonly List<WitnessEntry> _witnesses = new();

      // Portrait troop per heroless (synthetic bandit) witness, so the chief's men show distinct
      // faces instead of a blank avatar. Keyed by witness name.
      private readonly Dictionary<string, CharacterObject> _witnessPortraits = new(StringComparer.OrdinalIgnoreCase);
      private bool _audienceLanded; // retirement audience: landed (step back) vs landless (leave)
      private NpcProfile _audienceProfile; // the companion's profile for this audience (MainSpeaker uses it)

      // Companion AUDIENCE — a normal conversation the companion themselves requested (grievance / retirement),
      // opened programmatically from a map popup rather than the vanilla dialogue, so the profile is carried here.
      private CompanionAudienceReason _audienceReason = CompanionAudienceReason.None;
      private bool _awaitingOpener; // true while an NPC-led OPENER is generating (captive/audience) — hide the input

      private int _captiveBeatCount;
      private CaptiveSceneIntent _captiveEncounterIntent;
      private bool _captiveEncounterIsBandit; // captor is a faceless brigand, not a lord
      private bool _captiveEncounterIsCollective;

      // ── Sprint 17: captive encounter mode payload ────────────────────────
      // When opened from CaptiveEncounterService, the chat runs with the captor
      // as main NPC. The profile and CNC context are stored here rather than via
      // DialogFlow (which is only populated during vanilla conversations).
      private NpcProfile _captiveEncounterProfile;
      private bool _captiveMemoryFinalized; // idempotency guard for the end-of-scene memory record
      private bool _captivePlayerCoerced; // with a victim: the player is forced to act, not just watch
      private bool _captiveVictimIsFemale; // the victim companion's gender, so the prompt uses the right pronouns
      private string _captiveVictimName; // set => the scene's victim is this companion, not the player

      // ── Player-as-captor scene (the inverse): the NPC is the player's prisoner and the player drives. A normal
      //    live conversation (full memory + actions), flagged so the prompt frames the NPC as the captive. ──
      private bool _captorScene;
      private CaptiveSceneIntent _captorSceneIntent;
      private bool _captorSceneMemoryFinalized; // idempotency guard for the Grudges pillar's CaptiveAbuse plant
      private NpcProfile _captorSceneProfile;
      private float _chatVisualOffsetX = -30f; // matches the prefab's resting ChatPanel.PositionXOffset

      private string _closeButtonText = "End conversation";

      // ── Commoner mode payload (v1.1) ──────────────────────────────────────
      // Synthetic profile + settlement snapshot created per session; never stored.
      private NpcProfile _commonerProfile;
      private ChatSession _commonerSession;
      private CommonsKnowledge _commonsKnowledge;
      private bool _composePreviewDirty;
      private string _composePreviewText = "";

      // Set when the NPC emits an end_conversation action — locks input so the
      // player can only read the farewell and leave.
      private bool _conversationEnded;
      private bool _conversationMemoryFinalized; // idempotency guard
      private bool _conversationStartedRaised;   // Extension Surface: CrConversation.Started fired at most once
      private bool _conversationEndedRaised;      // Extension Surface: CrConversation.Ended fired at most once

      // The witness the player is currently addressing (sticky). Null = main NPC.
      private string _currentSpeakerHeroId;
      private CaptiveSceneDirector _director;

      // ── Fallback conversation memory ──────────────────────────────────────
      // The LLM mostly emits [EVENT] on a natural farewell; if the player closes
      // the window mid-exchange, nothing was recorded and the NPC forgets the
      // conversation ever happened (tester report). These track whether the
      // fallback recap is needed when the chat closes. Recorded REGARDLESS of
      // whether an [EVENT] also fired: an [EVENT] captures one dramatic beat,
      // this recap captures what was discussed/decided/promised, and a stated
      // plan the model did not flag as an [EVENT] must not vanish (tester report:
      // NPCs forgot a same-day stated plan one conversation later).
      private bool _hasWitnesses;
      private string _inputText = "";
      private bool _isComposeOpen;
      private bool _isManuscriptLeftPageOpen;
      private string _manuscriptDossierKey = ""; // hero id the manuscript left page currently shows (for click-to-toggle)
      private bool _isMemoryPanelOpen;
      private bool _isPleaseContinueVisible;

      // ── Portrait panel ────────────────────────────────────────────────────
      // A left panel (same area as the memory panel) showing a clicked speaker's portrait at full size: their
      // custom ChatPortraits image when present, else the vanilla 3D render. Opened by a left-click on a portrait.
      private bool _isPortraitPanelOpen;
      private bool _isPrivateRequestVisible;
      private bool _isRoundTableVisible;

      // Pause state — stored context so ExecuteContinue can resume without parameters.
      private bool _isScenePaused;
      private bool _isSendEnabled = true;
      private long _lastComposePreviewTicks;

      // The scene-focus index picked last round (BuildSceneSetup/BuildResumeSetup), so
      // SceneFocusSelector never repeats the same driving focus two rounds running. -1 = no prior round.
      private int _lastSceneFocusIndex = -1;

      // AUDIT-CAPTIVE-STYLE T1/T2 (2026-07-24): this scene's writing lens, and the last lens drawn. The last
      // index is STATIC on purpose (unlike the per-scene focus reset): it survives across captive scenes within
      // a play session, so two consecutive scenes are never told through the same lens (the reported cross-scene
      // sameness). Session-only, resets on relaunch, which is a graceful degradation not a correctness need.
      private static int _lastCaptiveStyleIndex = -1;
      private string _captiveSceneStyle;

      // The driving focus STRING of the current captive round, carried into every turn's context so it
      // governs the whole scene (B, Gabriel 2026-07-19), not just the opener that first named it.
      private string _captiveSceneFocus;

      // ── Shared-memories panel (read-only) ─────────────────────────────────
      // A left panel mirroring cr.memory: what this NPC remembers of the player.
      // Rebuilt from the persisted profile each time it opens.
      private MBBindingList<MemoryEntryViewModel> _memories;
      private string _memoryPanelTitle = "Shared memories";
      private MBBindingList<ChatMessageViewModel> _messages;

      private ChatMode _mode = ChatMode.Conversation;

      /// <summary>
      ///   Set while this window is a COUNCIL (the player presides over a seated roster). Null for an ordinary
      ///   conversation. Session state, not persisted and not global: it dies with the window.
      /// </summary>
      private CouncilScope? _councilScope;

      /// <summary>
      ///   Set only for a <see cref="CouncilScope.Parley" /> (null for every other scope): the StringId of the
      ///   specific enemy faction this parley was convened against, carried from convening
      ///   (<see cref="ActivateCouncil" />) through to <see cref="ChatViewModel.PlayerCanMakePeace" /> (the
      ///   offering) and, via <see cref="CalradiaRemembers.Assemblies.CouncilSessionBehavior.BeginSession" />, to
      ///   <see cref="CouncilLift.SettleMakePeace" /> at the lift. Session state like <see cref="_councilScope" />
      ///   itself, but ALSO handed to the session behavior so it survives a mid-session save (see that class's
      ///   own additive snapshot field).
      /// </summary>
      private string _councilParleyTargetFactionId;

      /// <summary>
      ///   How the CURRENT council speaks (whole-table single call, or one call per seated member). Seeded from
      ///   <see cref="ModSettings.DefaultCouncilVoiceMode" /> when the council opens (<see cref="ActivateCouncil" />),
      ///   then flipped live by the in-window toggle (<see cref="ExecuteToggleCouncilVoiceMode" />): session
      ///   state only, never written back to the MCM default. Meaningless outside a council.
      /// </summary>
      private CouncilVoiceMode _councilVoiceMode = CouncilVoiceMode.WholeTable;
      private bool _isCouncilVoiceToggleVisible;
      private string _councilVoiceModeText = "";
      private bool _isStylePickerVisible;
      private bool _isStyleLabelVisible;
      private string _styleButtonText = "";
      private string _styleIconPath = "";

      /// <summary>
      ///   The current council's [RESOLUTION] ledger: every pledge recorded or withdrawn this session, settled
      ///   only at the lift (<see cref="FinalizeCouncilResolutions" />). Session state like <see cref="_councilScope" />
      ///   itself: never persisted, dies with the window. Harmless (and unused) outside a council.
      /// </summary>
      private readonly AssemblyLedger _councilLedger = new AssemblyLedger();

      /// <summary>Idempotency guard for <see cref="FinalizeCouncilResolutions" />, mirroring every other Finalize* flag.</summary>
      private bool _councilResolutionsFinalized;
      private Hero _npcHero;
      private string _npcName = "the lord";

      // Portrait fallback for a heroless speaker (a synthesized bandit captor): the face of a
      // real bandit troop stands in for the brigand chief, who has no Hero of their own.
      private CharacterObject _npcPortraitCharacter;

      // True while RunNpcWitnessExchangeAsync is in flight; prevents the
      // RunLlmExchangeAsync finally block from re-enabling input prematurely.
      private bool _npcWitnessExchangePending;

      // Total player text length this session — feeds the one-substantial-turn fallback-memory rule.
      private int _playerCharsThisSession;

      private bool _companionHeardNoted; // the closing "you heard them out" lift, applied at most once per window
      private int _playerTurnsThisSession; // completed player→NPC exchanges

      /// <summary>
      ///   False once the opening scene-setting narration has been asked for. A dedicated latch rather than
      ///   a read of <see cref="_playerTurnsThisSession" />, because a captor's opening turn is sent with
      ///   suppressPlayerLine and never counts as a turn, so the counter reported "still the opening" twice.
      /// </summary>
      private bool _openingNarrationPending = true;

      /// <summary>True exactly once per conversation: this is the turn that establishes the scene.</summary>
      private bool ConsumeOpeningNarration()
      {
         if (!_openingNarrationPending) return false;
         _openingNarrationPending = false;

         return true;
      }
      private string _portraitPanelTitle = "Portrait";

      // Set when the player clicks "Request Private" — injected once, then cleared.
      private bool _privacyRequestPending;

      // Set once the request is used (accepted or refused) — button never reappears.
      private bool _privacyRequestUsed;
      private string _relationSummary = "";
      private bool _retireGranted; // the player granted the retirement this audience (executed on close)
      private bool _sceneContinuationPending;
      private int _sceneContinuations;
      private NpcMemoryCampaignBehavior _scenePauseBehavior;
      private ChatSpeaker _scenePauseSpeaker;
      private string _sendButtonText = "Send";

      // A roleplay dossier shown ABOVE the portrait when the clicked line is a known NPC (hidden for the player's
      // own line or a heroless speaker): inline facts (culture / age / role / relation / personal regard), the
      // vanilla encyclopedia background, the mod's authored backstory, the physical description from body stats,
      // the traits/kinks discovered so far, and the grievances the player has learned this NPC holds.

      // ── Story Log ─────────────────────────────────────────────────────────
      // True once the conversation intro has been written this session (lazy —
      // deferred until the first player message so witnesses are already set).
      private bool _storyIntroWritten;

      // Settlement or location description captured when the conversation opens.
      private string _storyLocation;
      private string _titleText = "Calradia Remembers";

      private string _witnessesText = "";

      // How many auto-exchanges have fired this player turn. Reset on every send.
      private int _witnessExchangesThisRound;
      private bool _witnessMemoryFinalized; // idempotency guard for the per-witness "I was present" memory

      public ChatViewModel()
      {
         _messages = new MBBindingList<ChatMessageViewModel>();
         _memories = new MBBindingList<MemoryEntryViewModel>();

         // A character tableau is a GPU render-target; one per transcript line exhausts VRAM on a long scene
         // (the create_render_texture crash). On every insertion, collapse to at most one live portrait per
         // distinct speaker present, freeing the render-targets of that speaker's older lines.
         _messages.ListChanged += OnMessagesListChanged;

         // Every chat opens as an ordinary scene; the captive/captor activators set the scene kind to Cnc when
         // the scene is sexual, which (in Standard composition) routes it to the NSFW model.
         LlmServices.ActiveSceneKind = SceneContentKind.Ordinary;

         RefreshStyleUi(); // the header's style button: hidden unless armed, labelled with the live voice
      }

      // ── Chat mode ─────────────────────────────────────────────────────────
      // One window, five behaviors. A single mode value (instead of a trio of
      // booleans) so adding a mode means extending the enum and reviewing the
      // named predicates below — not hunting scattered flag checks.
      private enum ChatMode
      {
         Conversation, // live dialogue with an NPC — witnesses, actions, story log
         CaptiveEncounter, // captor-led scene — the player is a prisoner (Sprint 17)
         Commoner // v1.1 — transient street-level chat, no memory, no witnesses
      }

      /// <summary>
      ///   Horizontal offset of the chat window (bound to ChatPanel.PositionXOffset). Normally a small negative
      ///   margin from the right edge; <see cref="SetVisualOffscreen" /> slides it fully off-screen so a game
      ///   screen opened over the chat (the encyclopedia) is not covered by the chat's high-order rendering.
      /// </summary>
      [DataSourceProperty]
      public float ChatVisualOffsetX
      {
         get => _chatVisualOffsetX;
         set
         {
            if (_chatVisualOffsetX == value) return;
            _chatVisualOffsetX = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string CloseButtonText
      {
         get => _closeButtonText;
         set
         {
            if (_closeButtonText == value) return;
            _closeButtonText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      // Player report (v1.29.0): typing a LONG message tanked FPS (~200 -> ~15) because the wrapped preview
      // (a RichTextWidget) re-parsed and re-wrapped the whole text on EVERY keystroke. It now binds to this
      // separate property, pushed at most a few times a second by PumpComposePreview from the per-frame tick.
      [DataSourceProperty]
      public string ComposePreviewText
      {
         get => _composePreviewText;
         set
         {
            if (_composePreviewText == value) return;
            _composePreviewText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>True when at least one witness is present — drives IsVisible on the witnesses line.</summary>
      [DataSourceProperty]
      public bool HasWitnesses
      {
         get => _hasWitnesses;
         set
         {
            if (_hasWitnesses == value) return;
            _hasWitnesses = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string InputText
      {
         get => _inputText;
         set
         {
            if (_inputText == value) return;
            _inputText = value;
            OnPropertyChangedWithValue(value);
            _composePreviewDirty = true; // the wrapped preview refreshes on a throttle, not every keystroke
         }
      }

      /// <summary>
      ///   The chat input (small box + Send + the "&lt;" toggle) — shown only when the player may actually type:
      ///   the normal input is available, we are not still waiting on the NPC's opening turn (a captive scene or
      ///   companion audience where the NPC speaks first), and the conversation has not ended. Hidden during
      ///   "(considering…)" so it does not appear before the LLM has given its introduction.
      ///   The <see cref="_conversationEnded" /> clause closes a bug (player report, duel arming 2026-08-14): every
      ///   end state (farewell, end_conversation, captor release, escape, duel arm...) sets IsSendEnabled false but
      ///   leaves IsScenePaused false, so without this clause the box stayed visible and editable while doing
      ///   nothing on Enter, a dead control the player could not tell apart from a live one. Every site that sets
      ///   _conversationEnded = true already means "lock to the close button only" (verified: farewell lock,
      ///   end_conversation, execute_prisoner/nemesis-turned, captive-scene endings, captor release, escape,
      ///   duel arm, round-table MarkConversationEndedBySpeaker), so hiding the input there is safe everywhere;
      ///   "Please continue" and the close button are separate controls (IsPleaseContinueVisible / CloseButtonText)
      ///   unaffected by this flag.
      /// </summary>
      [DataSourceProperty]
      public bool IsChatInputVisible => IsNormalInputVisible && !_awaitingOpener && !_conversationEnded;

      /// <summary>True while the left review panel (a wrapped, read-only view of the message) is open.</summary>
      [DataSourceProperty]
      public bool IsComposeOpen
      {
         get => _isComposeOpen;
         set
         {
            if (_isComposeOpen == value) return;
            _isComposeOpen = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChanged(nameof(IsComposeToggleVisible));
         }
      }

      /// <summary>The "&lt;" open-review button shows only when the chat input is available and the panel is closed.</summary>
      [DataSourceProperty]
      public bool IsComposeToggleVisible => IsChatInputVisible && !_isComposeOpen;

      /// <summary>
      ///   Manuscript skin only: whether the LEFT dossier page is shown. The manuscript opens with only the right
      ///   (conversation) page; the "&lt;" toggle reveals this one. Ignored by the Classic skin (no such page).
      /// </summary>
      [DataSourceProperty]
      public bool IsManuscriptLeftPageOpen
      {
         get => _isManuscriptLeftPageOpen;
         set
         {
            if (_isManuscriptLeftPageOpen == value) return;
            _isManuscriptLeftPageOpen = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>
      ///   True when there is a real hero across the conversation to open an encyclopedia page for
      ///   (a synthesized bandit captor or a nameless commoner has none, so the button hides).
      /// </summary>
      [DataSourceProperty]
      public bool IsEncyclopediaVisible => _npcHero != null;

      /// <summary>
      ///   The "Options" button: reproduces what pressing ESC does, opening the vanilla escape/options menu
      ///   (and the MCM mod options) over the chat, so the player can change settings, enable logging, or swap
      ///   models WITHOUT ending the conversation (the chat is a keyboard-focus layer, so it swallows ESC).
      ///   Shown in an ordinary live conversation; hidden in a captor/captive scene, which rides a mission where
      ///   the map escape menu is not the right target.
      /// </summary>
      [DataSourceProperty]
      public bool IsGameMenuButtonVisible => _mode == ChatMode.Conversation && !_captorScene;

      /// <summary>True while the left "shared memories" panel is open (a read-only view of what this NPC remembers).</summary>
      [DataSourceProperty]
      public bool IsMemoryPanelOpen
      {
         get => _isMemoryPanelOpen;
         set
         {
            if (_isMemoryPanelOpen == value) return;
            _isMemoryPanelOpen = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChanged(nameof(IsMemoryToggleVisible));
         }
      }

      /// <summary>
      ///   The "Memories" open button: shown only when there is a real hero across the conversation (one with a
      ///   persistent profile to remember anything (a synthesized bandit captor or nameless commoner has none),
      ///   and the panel is not already open. Mirrors <see cref="IsEncyclopediaVisible" />'s population.
      /// </summary>
      [DataSourceProperty]
      public bool IsMemoryToggleVisible => _npcHero != null && !_isMemoryPanelOpen;

      /// <summary>
      ///   The restored manual PROSE/NSFW switch (a player asked for it back, 2026-08-20). Shown only when the MCM
      ///   gate "Manual PROSE/NSFW switch in chat" is on; OFF by default, so most players never see it and routing
      ///   stays automatic (Standard content-routes captivity to NSFW; Enhanced writes on PROSE with NSFW fallback).
      ///   When shown, clicking it forces prose onto the NSFW model; the hard-failure fallback still applies on top.
      /// </summary>
      [DataSourceProperty]
      public bool IsModelModeToggleVisible => ModSettings.ShowManualModelToggle;

      /// <summary>Inverse of <see cref="IsScenePaused" /> — drives the normal InputBox + SendButton.</summary>
      [DataSourceProperty]
      public bool IsNormalInputVisible => !_isScenePaused;

      /// <summary>
      ///   Always hidden now: gold is folded into the unified "give" control (the gift button),
      ///   so the separate "Give Gold" button is retired. Kept for the existing XML binding.
      /// </summary>
      [DataSourceProperty]
      public bool IsOfferGoldVisible => false;

      /// <summary>
      ///   True in plain Conversation mode while the exchange is still open — hides
      ///   the "Offer item" button in letter/captive modes and after the NPC says goodbye.
      /// </summary>
      [DataSourceProperty]
      public bool IsOfferItemVisible => _mode == ChatMode.Conversation && !_conversationEnded;

      /// <summary>
      ///   Raises every binding that reads <see cref="_conversationEnded" /> (a plain field, not a property, so
      ///   no setter does this automatically). Called at every site that locks the conversation, right after
      ///   setting the field to true, so the "Offer item" button AND the chat input (Fix A, duel-arming dead
      ///   input bug) hide together the instant the close button becomes the only interactive control.
      /// </summary>
      private void NotifyConversationEndedChanged()
      {
         OnPropertyChanged(nameof(IsOfferItemVisible));
         OnPropertyChanged(nameof(IsChatInputVisible));
         OnPropertyChanged(nameof(IsComposeToggleVisible));
      }

      /// <summary>
      ///   True once a captive scene has ended (other than by escape): shows a "Please continue"
      ///   button beside Leave so the player can relaunch a fresh cycle of the encounter if they
      ///   want more — and as a safety net when a weaker model ended a scene abruptly.
      /// </summary>
      [DataSourceProperty]
      public bool IsPleaseContinueVisible
      {
         get => _isPleaseContinueVisible;
         set
         {
            if (_isPleaseContinueVisible == value) return;
            _isPleaseContinueVisible = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>True while the left PORTRAIT panel is open (a full-size view of a clicked speaker's portrait).</summary>
      [DataSourceProperty]
      public bool IsPortraitPanelOpen
      {
         get => _isPortraitPanelOpen;
         set
         {
            if (_isPortraitPanelOpen == value) return;
            _isPortraitPanelOpen = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>
      ///   True only while a council is open: shows the in-window control that flips between whole-table and
      ///   per-member voices. Set by <see cref="ActivateCouncil" />; an ordinary conversation never shows it.
      /// </summary>
      [DataSourceProperty]
      public bool IsCouncilVoiceToggleVisible
      {
         get => _isCouncilVoiceToggleVisible;
         set
         {
            if (_isCouncilVoiceToggleVisible == value) return;
            _isCouncilVoiceToggleVisible = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>Label naming the CURRENT council voice mode, so the player can always read the live state at a glance.</summary>
      [DataSourceProperty]
      public string CouncilVoiceModeText
      {
         get => _councilVoiceModeText;
         set
         {
            if (_councilVoiceModeText == value) return;
            _councilVoiceModeText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>
      ///   True when the in-window narrative-style picker is shown. Mirrors <see cref="StyleService.PickerEnabled" />
      ///   (armed with <c>cr.style_ui on</c>), refreshed by <see cref="RefreshStyleUi" /> so arming it while a chat
      ///   is already open takes effect at once.
      /// </summary>
      [DataSourceProperty]
      public bool IsStylePickerVisible
      {
         get => _isStylePickerVisible;
         set
         {
            if (_isStylePickerVisible == value) return;
            _isStylePickerVisible = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>
      ///   True when the quill asset is present, so the header shows the icon button. Mutually exclusive with
      ///   <see cref="IsStyleLabelVisible" />: exactly one of the two style buttons is ever shown, so a missing
      ///   asset degrades to the labelled button instead of leaving a blank one.
      /// </summary>
      [DataSourceProperty]
      public bool IsStyleIconVisible => _isStylePickerVisible && !_isStyleLabelVisible;

      /// <summary>True when the style button falls back to its text label (the quill asset is not on disk).</summary>
      [DataSourceProperty]
      public bool IsStyleLabelVisible
      {
         get => _isStyleLabelVisible;
         set
         {
            if (_isStyleLabelVisible == value) return;
            _isStyleLabelVisible = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>Full path of the quill image the icon button renders (empty when the asset is absent).</summary>
      [DataSourceProperty]
      public string StyleIconPath
      {
         get => _styleIconPath;
         set
         {
            if (_styleIconPath == value) return;
            _styleIconPath = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>Skin art: full path to the left-page (NPC info) background, for the Manuscript skin's CrRuntimeImageWidget.</summary>
      [DataSourceProperty]
      public string ChatLeftPanelPath => System.IO.Path.Combine(
         TaleWorlds.Library.BasePath.Name, "Modules", "CalradiaRemembers", "GUI", "Images", "cr_chat_left.jpg");

      /// <summary>Skin art: full path to the right-page (conversation) background, for the Manuscript skin's CrRuntimeImageWidget.</summary>
      [DataSourceProperty]
      public string ChatRightPanelPath => System.IO.Path.Combine(
         TaleWorlds.Library.BasePath.Name, "Modules", "CalradiaRemembers", "GUI", "Images", "cr_chat_right.jpg");

      /// <summary>Label naming the style currently in force, so the player reads the live voice at a glance.</summary>
      [DataSourceProperty]
      public string StyleButtonText
      {
         get => _styleButtonText;
         set
         {
            if (_styleButtonText == value) return;
            _styleButtonText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>True when the "Request Private" button should be shown.</summary>
      [DataSourceProperty]
      public bool IsPrivateRequestVisible
      {
         get => _isPrivateRequestVisible;
         set
         {
            if (_isPrivateRequestVisible == value) return;
            _isPrivateRequestVisible = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>True when the "Round Table" button should be shown (a group is present to go round).</summary>
      [DataSourceProperty]
      public bool IsRoundTableVisible
      {
         get => _isRoundTableVisible;
         set
         {
            if (_isRoundTableVisible == value) return;
            _isRoundTableVisible = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>
      ///   True while the captive scene is mid-narration and the player must choose
      ///   "Continue" or "Try something". Hides the normal input area and shows the
      ///   two scene-pause buttons instead.
      /// </summary>
      [DataSourceProperty]
      public bool IsScenePaused
      {
         get => _isScenePaused;
         set
         {
            if (_isScenePaused == value) return;
            _isScenePaused = value;
            OnPropertyChangedWithValue(value);
            OnPropertyChanged(nameof(IsNormalInputVisible));
            OnPropertyChanged(nameof(IsChatInputVisible));
            OnPropertyChanged(nameof(IsComposeToggleVisible));
         }
      }

      [DataSourceProperty]
      public bool IsSendEnabled
      {
         get => _isSendEnabled;
         set
         {
            if (_isSendEnabled == value) return;
            _isSendEnabled = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The rows of the shared-memories panel: a read-only mirror of this NPC's remembered history.</summary>
      [DataSourceProperty]
      public MBBindingList<MemoryEntryViewModel> Memories
      {
         get => _memories;
         set
         {
            if (_memories == value) return;
            _memories = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The header of the memory panel, for example "What {name} remembers".</summary>
      [DataSourceProperty]
      public string MemoryPanelTitle
      {
         get => _memoryPanelTitle;
         set
         {
            if (_memoryPanelTitle == value) return;
            _memoryPanelTitle = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public MBBindingList<ChatMessageViewModel> Messages
      {
         get => _messages;
         set
         {
            if (_messages == value) return;
            _messages = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>
      ///   The in-chat mode button label: which of the two configured models requests currently use.
      ///   Clicking the button flips it (see <see cref="ExecuteToggleModelMode" />).
      /// </summary>
      [DataSourceProperty]
      /// <summary>
      ///   Adult-prompt audit M12: says MODEL, not "Mode". The button only ever switched which model and
      ///   provider serve the request; the content level stays whatever Mod Options says, and the prompt is
      ///   built from that. Labelled "Mode: NSFW" it read as a content switch, so a player at Mature clicked
      ///   it, got the same chaste writing the Mature prompt asks for, and concluded the button was broken.
      /// </summary>
      public string ModelModeText => LlmServices.ManualNsfwProse ? "Model: NSFW" : "Model: Roleplay";

      public Action OnCloseRequested { get; set; }

      /// <summary>The physical description derived from this hero's body/appearance stats.</summary>
      [DataSourceProperty]
      public string PanelAppearance { get; private set; } = "";

      /// <summary>The vanilla encyclopedia background paragraph for this hero.</summary>
      [DataSourceProperty]
      public string PanelBackground { get; private set; } = "";

      /// <summary>The mod's authored backstory for this hero, when one is defined in the character files.</summary>
      [DataSourceProperty]
      public string PanelCharacter { get; private set; } = "";

      /// <summary>Bulleted list of grievances the player has learned this NPC holds.</summary>
      [DataSourceProperty]
      public string PanelGrievances { get; private set; } = "";

      /// <summary>Bulleted list of traits/kinks discovered about this NPC over the campaign.</summary>
      [DataSourceProperty]
      public string PanelInfoDiscoveries { get; private set; } = "";

      /// <summary>
      ///   Inline facts, one per line: "Culture: Empire", "Age: 38", "Role: Lady", "Relation: +12", "Personal regard:
      ///   neutral (0)".
      /// </summary>
      [DataSourceProperty]
      public string PanelInfoFacts { get; private set; } = "";

      [DataSourceProperty]
      public string PanelPortraitAdditionalArgs { get; private set; } = "";

      [DataSourceProperty]
      public float PanelPortraitHeight { get; private set; } = 458f;

      /// <summary>
      ///   Manuscript skin only: the same portrait, aspect-preserved to fit the SMALLER left-page box (the Classic
      ///   pop-out box is far larger, so its width would overflow the narrow left page). Same aspect, smaller box.
      /// </summary>
      [DataSourceProperty]
      public float ManuscriptPortraitWidth { get; private set; } = 220f;

      [DataSourceProperty]
      public float ManuscriptPortraitHeight { get; private set; } = 264f;

      [DataSourceProperty]
      public string PanelPortraitId { get; private set; } = "";

      // ── Memory-panel portrait (custom PNG or vanilla 3D) ──
      [DataSourceProperty]
      public string PanelPortraitPath { get; private set; } = "";

      [DataSourceProperty]
      public string PanelPortraitTextureProviderName { get; private set; } = "";

      /// <summary>Displayed portrait size, aspect-preserved to fit the portrait box (must be float for the setter).</summary>
      [DataSourceProperty]
      public float PanelPortraitWidth { get; private set; } = 344f;

      /// <summary>The portrait panel's header (the speaker's name).</summary>
      [DataSourceProperty]
      public string PortraitPanelTitle
      {
         get => _portraitPanelTitle;
         set
         {
            if (_portraitPanelTitle == value) return;
            _portraitPanelTitle = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>
      ///   Subtitle showing the NPC's clan standing and personal regard for the player.
      /// </summary>
      [DataSourceProperty]
      public string RelationSummary
      {
         get => _relationSummary;
         set
         {
            if (_relationSummary == value) return;
            _relationSummary = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string SendButtonText
      {
         get => _sendButtonText;
         set
         {
            if (_sendButtonText == value) return;
            _sendButtonText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public bool ShowPanelAppearance { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelBackground { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelCharacter { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelCustomPortrait { get; private set; }

      /// <summary>True when this NPC has at least one discovered trait/kink to list.</summary>
      [DataSourceProperty]
      public bool ShowPanelDiscoveries { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelGrievances { get; private set; }

      // ── Portrait-panel roleplay dossier (scrollable, above the portrait) ──
      /// <summary>True when the clicked line is a known NPC, so the dossier is shown (hidden for the player).</summary>
      [DataSourceProperty]
      public bool ShowPanelInfo { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelVanillaPortrait { get; private set; }

      [DataSourceProperty]
      public string TitleText
      {
         get => _titleText;
         set
         {
            if (_titleText == value) return;
            _titleText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>One-line list of witness names shown below the title bar.</summary>
      [DataSourceProperty]
      public string WitnessesText
      {
         get => _witnessesText;
         set
         {
            if (_witnessesText == value) return;
            _witnessesText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      // A captive does not command the room — the captor's soldiers leave only if
      // the captor wills it. Commoners have no room to clear.
      private bool AllowsPrivacyRequest => _mode == ChatMode.Conversation;
      private bool IsCaptiveEncounter => _mode == ChatMode.CaptiveEncounter;

      // A spoken exchange happening "now" — transcript and story log apply. Every chat mode is now
      // a spoken exchange (letters moved to the dedicated parchment courier window).
      private bool IsLiveConversation => _mode == ChatMode.Conversation || _mode == ChatMode.CaptiveEncounter || _mode == ChatMode.Commoner;

      public void ActivateCaptiveEncounterMode(
         NpcProfile captorProfile,
         bool isCollective,
         CaptiveSceneIntent intent = CaptiveSceneIntent.Interrogation,
         bool captorIsBandit = false,
         string victimCompanionName = null,
         bool playerCoerced = false,
         bool victimIsFemale = false,
         string openingOverride = null)
      {
         _mode = ChatMode.CaptiveEncounter;
         OnPropertyChanged(nameof(IsOfferItemVisible));

         // A structurally-explicit scene: open on the NSFW model (falls back to the roleplay model when none
         // is configured). The player can still flip it back. Audit M10/M11: "captive" is not "sexual", and
         // the adult dial has the last word, so a ransom, an intimidation, or any scene at all with adult
         // content Off is no longer served by the specialised erotic model behind the player's back.
         // A captive/captor scene is CNC by construction; a sexual intent routes it to the CNC model, a
         // non-sexual one stays ordinary (roleplay). NEXT INCREMENT: finer scene->mode routing (auto-detecting
         // a ConsensualIntimate case, splitting Cnc detection) replaces this structural true->Cnc mapping.
         LlmServices.ActiveSceneKind =
            CaptiveSceneIntentRules.IsSexual(intent) ? SceneContentKind.Cnc : SceneContentKind.Ordinary;
         OnPropertyChanged(nameof(ModelModeText));

         _captiveEncounterProfile = captorProfile;
         _captiveEncounterIsCollective = isCollective;
         _captiveEncounterIsBandit = captorIsBandit;
         _captiveEncounterIntent = intent;
         _captiveVictimName = string.IsNullOrWhiteSpace(victimCompanionName)
            ? null
            : victimCompanionName.Trim();
         _captivePlayerCoerced = playerCoerced && _captiveVictimName != null;
         _captiveVictimIsFemale = victimIsFemale && _captiveVictimName != null;
         _captiveBeatCount = 0;
         // Build the scene's structure now: the lead's full arc, plus collective sub-acts when
         // others are present. extraAggressors = the witnesses who can take a turn — the captor's MEN,
         // never the bound victim companion (CountAggressorWitnesses excludes them).
         _director = new CaptiveSceneDirector(isCollective, CountAggressorWitnesses(), MBRandomSource.Instance, captorIsBandit, intent);

         AssignBanditWitnessPortraits(captorIsBandit);

         // Start the harm tracker fresh: a guaranteed body toll lands at scene close for a
         // forced sexual scene only if the captor's own narration never wounded the player.
         NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.BeginCaptiveSceneTracking();

         // Public humiliation: being abased before witnesses stains the player's standing.
         // Only collective scenes with a degrading intent count — there is no audience to
         // shame the player otherwise, and an interrogation/reward is not an abasement.
         // Bandits are excluded: brigands do not move in noble society, so word of an
         // abasement at their hands does not reach the courts — no renown/influence loss.
         if (isCollective && CaptiveIntentRules.IsHumiliating(intent) && !captorIsBandit)
            ApplyPublicHumiliationPenalty();

         // The captor initiates — the player was dragged in and has no opening line.
         // Fire an NPC-led turn so the scene starts on its own, driven by a synthetic
         // stage cue that is sent to the LLM but never shown or logged as a player line.
         ChatSpeaker captor = MainSpeaker();
         if (captor.Profile == null)
         {
            ModLog.Error("Captive encounter: no captor profile — cannot open the scene.");

            return;
         }

         ChatMessageViewModel placeholder = MakeSpeakerMessage(captor, PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;

         // The randomized scene SETUP carries sexual "foci"; a lord reckoning (and the bandit menace intents) are
         // non-sexual confrontations, so they take only their own opener — never the sexual setup.
         bool nonSexual = !CaptiveSceneIntentRules.IsSexual(intent);
         _lastSceneFocusIndex = -1; // a fresh scene resets the avoid-repeat memory
         _captiveSceneFocus = null;
         // The writing lens (T1) is drawn for EVERY captive scene, sexual or not (a non-sexual reckoning or
         // menace scene converges in prose just the same); its last index is static, so this scene never
         // repeats the previous one's lens.
         _captiveSceneStyle = CaptiveSceneText.PickStyleRegister(ref _lastCaptiveStyleIndex);
         // Player report: captured by Sea Raiders, the scene was narrated as a land camp with a tent and a
         // campfire, the ship never registering. Resolved once here so both the fixed opener text and the
         // randomized locale pool draw from the same at-sea verdict.
         bool atSea = ResolveCaptiveSceneAtSea();
         string setup = nonSexual
            ? ""
            : CaptiveSceneText.BuildSceneSetup(intent, ref _lastSceneFocusIndex, atSea, out _captiveSceneFocus);
         // An openingOverride replaces the intent-based opener with a supplied stage cue: used when the scene
         // RESUMES after a lost escape encounter, so the captor opens on the recapture instead of a fresh intent.
         string opener = string.IsNullOrEmpty(openingOverride)
            ? CaptiveSceneText.BuildCaptiveOpener(isCollective, intent, atSea) + setup
            : openingOverride;
         MarkOpenerPending();
         _ = RunLlmExchangeAsync(opener, captor, placeholder, true);
      }

      /// <summary>
      ///   Opens the chat as a PLAYER-AS-CAPTOR scene: the NPC is the player's prisoner and the player drives.
      ///   Runs as a normal live conversation (full memory + actions), flagged so the prompt frames the NPC as the
      ///   captive reacting to the player. The player speaks first; there is no NPC-led opener.
      /// </summary>
      public void ActivateCaptorSceneMode(NpcProfile prisonerProfile, CaptiveSceneIntent intent)
      {
         _mode = ChatMode.Conversation;

         // A structurally-explicit scene: open on the NSFW model (falls back to the roleplay model when none
         // is configured). The player can still flip it back. Audit M10/M11: "captive" is not "sexual", and
         // the adult dial has the last word, so a ransom, an intimidation, or any scene at all with adult
         // content Off is no longer served by the specialised erotic model behind the player's back.
         // A player-as-captor scene is CNC by construction; a sexual intent routes it to the CNC model, a
         // non-sexual one stays ordinary (roleplay). NEXT INCREMENT: finer scene->mode routing (auto-detecting
         // a ConsensualIntimate case, splitting Cnc detection) replaces this structural true->Cnc mapping.
         LlmServices.ActiveSceneKind =
            CaptiveSceneIntentRules.IsSexual(intent) ? SceneContentKind.Cnc : SceneContentKind.Ordinary;
         OnPropertyChanged(nameof(ModelModeText));

         _captorScene = true;
         _captorSceneProfile = prisonerProfile;
         _captorSceneIntent = intent;
         // Q5 (AUDIT-CAPTIVE-STYLE): a player-as-captor scene converges in prose just like a captive one, so it
         // draws a writing lens too, never the same as the last scene's.
         _captiveSceneStyle = CaptiveSceneText.PickStyleRegister(ref _lastCaptiveStyleIndex);

         TitleText = $"Calradia Remembers — {prisonerProfile?.Name ?? "your prisoner"}";
         CloseButtonText = "End";
         SendButtonText = "Send";
      }

      /// <summary>
      ///   Switches the chat window into commoner mode: a transient, street-level
      ///   exchange with a synthetically generated NPC. No memory is saved, no
      ///   witnesses appear, and only the take_gold action is honoured.
      /// </summary>
      public void ActivateCommonerMode(
         NpcProfile profile,
         CommonsKnowledge knowledge)
      {
         _mode = ChatMode.Commoner;
         _commonerProfile = profile;
         _commonsKnowledge = knowledge;
         _commonerSession = new ChatSession();

         TitleText = $"Calradia Remembers — {profile?.Name ?? "a commoner"}";
         CloseButtonText = "Leave";
         SendButtonText = "Say";
      }

      /// <summary>
      ///   Opens a companion AUDIENCE — a normal conversation the companion themselves requested from a map popup
      ///   ("Let us speak of it"): a grievance, or a war-weary bid to retire. The companion speaks FIRST, voicing
      ///   why they came; full memory and actions like any conversation. For a retirement the player can grant
      ///   their leave (the companion emits the <c>retire</c> action, executed at close) or persuade them to stay.
      /// </summary>
      /// <summary>
      ///   Opens a COUNCIL over an already-seated roster: the player leads and every turn from here on fires
      ///   ONE narrator call (see <see cref="ExecuteSendCouncilMessage" />/<see cref="RunLlmExchangeAsync" />'s
      ///   <c>isCouncilNarratorTurn</c> branch): the LLM describes the scene and gives the floor to whichever
      ///   seated member(s) are genuinely moved to speak, never one presiding leader. The in-window "Voices"
      ///   toggle (<see cref="_councilVoiceMode" />) is superseded for a real council by this narrator model: it
      ///   stays visible (Gabriel iterates on it in-game) but <see cref="ExecuteSendMessage" /> no longer
      ///   branches on it. Carried as SESSION state, so closing the window ends council mode and a later
      ///   ordinary chat is unaffected. <paramref name="parleyTargetFactionId" /> is the Parley's own extra piece
      ///   of session state (null for every other scope): the StringId of the specific enemy faction this parley
      ///   was convened against, set by the caller (ChatManager.OpenForCouncil) once it has resolved the roster's
      ///   one counterpart.
      /// </summary>
      public void ActivateCouncil(CouncilScope scope, string parleyTargetFactionId = null)
      {
         _mode = ChatMode.Conversation;
         _councilScope = scope;
         _councilParleyTargetFactionId = parleyTargetFactionId;
         _councilVoiceMode = ModSettings.DefaultCouncilVoiceMode;
         CouncilVoiceModeText = DescribeCouncilVoiceMode(_councilVoiceMode);
         IsCouncilVoiceToggleVisible = true;

         // C4: hand the live ledger + scope to the save-safe session behavior, so a save (or crash) mid-sitting
         // persists the pledges; a normal close clears it again in FinalizeCouncilResolutions. The parley target
         // faction rides along as its own additive field (CouncilSessionSnapshot.ParleyTargetFactionId).
         CalradiaRemembers.Assemblies.CouncilSessionBehavior.Instance?.BeginSession(scope, _councilLedger, parleyTargetFactionId);

         // Bespoke council window (Inc 0): seats the left strip from the roster the caller (OpenForCouncil /
         // OpenForAssembly) already built via AddWitness, and gives the centre column its agenda placeholder.
         CouncilAgendaText = scope switch {
            CouncilScope.Assembly => "Assembly",
            CouncilScope.Family => "Clan and family council",
            CouncilScope.ClanLords => "Council of your clan's lords",
            CouncilScope.WarCouncil => "War council",
            CouncilScope.Parley => "Parley",
            _ => "Council of companions"
         };
         PopulateCouncilParticipants();

         ChatSpeaker speaker = MainSpeaker();
         if (speaker.Profile == null)
         {
            ModLog.Error("[COUNCIL] no profile on the anchor: cannot open the council.");

            return;
         }

         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, CouncilPlaceholderText);
         ApplyCouncilSceneImage(placeholder); // scene image while the council opens, instead of the anchor's own portrait
         _messages.Add(placeholder);
         IsSendEnabled = false;

         // The table opens itself, narrator-style: nobody presides, so the opening turn describes the gathering
         // settling in rather than having the anchor personally welcome everyone. A synthetic cue, never shown
         // or logged as a player line; isCouncilNarratorTurn (armed automatically once _councilScope is set,
         // see RunLlmExchangeAsync) frames this exactly like every later council turn.
         string opener = scope switch {
            CouncilScope.Assembly =>
               "[The player has convened an ASSEMBLY: lords, kin, companions, and envoys speaking for those who could " +
               "not come in person are gathered in the hall at the player's word. Describe the gathering settling in, " +
               "briefly, then give the floor: one seated member may acknowledge the summons and ask what matter the " +
               "player lays before the assembly. The others are present and listening, each with their own standing " +
               "and stake; not all of them need speak yet.]",
            CouncilScope.Family =>
               "[The player has called their own household together: their closest kin are gathered at their word. " +
               "Describe the gathering settling in, briefly, then give the floor: one seated member may acknowledge " +
               "the summons and ask what they would put before the family. The others are present and listening; not " +
               "all of them need speak yet.]",
            CouncilScope.ClanLords =>
               "[The player has called the lords of their clan to council: they are gathered at their word. Describe " +
               "the gathering settling in, briefly, then give the floor: one seated member may acknowledge the " +
               "summons and ask what business the player lays before the clan. The others are present and listening; " +
               "not all of them need speak yet.]",
            CouncilScope.WarCouncil =>
               "[The player has summoned a WAR COUNCIL: their allies are gathered at their word to decide war " +
               "policy. Describe the gathering settling in, briefly, then give the floor: one seated ally may " +
               "acknowledge the summons and ask what war matter the player lays before the table. The others are " +
               "present and listening, each with their own stake in what war would cost them; not all of them need " +
               "speak yet.]",
            CouncilScope.Parley =>
               "[The player has sought a PARLEY: the enemy's own envoy sits across the table, at the player's " +
               "word, to speak of peace. Describe the meeting settling in, briefly, then give the floor: the " +
               "counterpart may acknowledge why they came and ask what terms the player would offer. Wary, not " +
               "friendly, they did not come to surrender, only to hear.]",
            _ =>
               "[The player has called their companions together: they are gathered at their word. Describe the " +
               "gathering settling in, briefly, then give the floor: one seated member may acknowledge the summons " +
               "and ask what the player would have of them all. The others are present and listening; not all of " +
               "them need speak yet.]"
         };

         MarkOpenerPending();
         _ = RunLlmExchangeAsync(opener, speaker, placeholder, true);
      }

      public void ActivateCompanionAudience(NpcProfile profile, CompanionAudienceReason reason, bool landed, string topicDetail = null)
      {
         _mode = ChatMode.Conversation;
         _audienceReason = reason;
         _audienceLanded = landed;
         _retireGranted = false;
         _audienceProfile = profile;

         ChatSpeaker speaker = MainSpeaker();
         if (speaker.Profile == null)
         {
            ModLog.Error("Companion audience: no profile — cannot open the audience.");

            return;
         }

         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;

         // The companion opens (they asked for this word). A synthetic cue, never shown or logged as a player
         // line. The found-topic reasons (Mission..RumourReport) carry the SPECIFIC thing the mod found in
         // topicDetail: the model voices a fact the mod chose, never a generic invitation to talk.
         string opener = reason switch {
            CompanionAudienceReason.Retirement =>
               "[You asked the player for a private word. Open the conversation yourself: voice, in your own way, that " +
               "the long years of war have worn you down and you wish to lay down the sword. Be respectful, the " +
               "outcome is theirs to decide.]",
            CompanionAudienceReason.SecretAffection =>
               "[You asked the player for a private word. Open the conversation yourself: confide, in your own voice, " +
               "that serving faithfully at their side has quietly turned to something more, real affection. Broach it " +
               "with the courage and vulnerability of a comrade, tender and honest, never a demand.]",
            CompanionAudienceReason.MissionFollowUp =>
               "[You asked the player for a private word. Open the conversation yourself: report, in your own voice, " +
               $"on your errand. What happened: {topicDetail}]",
            CompanionAudienceReason.CompanionConflict =>
               "[You asked the player for a private word. Open the conversation yourself: raise, in your own voice, " +
               $"a grudge you carry against a fellow companion, and ask the player to hear your side: {topicDetail}]",
            CompanionAudienceReason.PracticalCounsel =>
               "[You asked the player for a private word. Open the conversation yourself: raise a practical concern " +
               $"about the party that you have noticed yourself, in your own voice: {topicDetail}]",
            CompanionAudienceReason.Callback =>
               "[You asked the player for a private word. Open the conversation yourself: an old memory has come back " +
               $"to you unbidden, and you want to speak of it, in your own voice: {topicDetail}]",
            CompanionAudienceReason.RumourReport =>
               "[You asked the player for a private word. Open the conversation yourself: you have word of the world " +
               $"to bring the player, in your own voice: {topicDetail}]",
            _ =>
               "[You asked the player for a private word. Open the conversation yourself: raise what has been troubling " +
               "you in their service, in your own voice.]"
         };

         MarkOpenerPending();
         _ = RunLlmExchangeAsync(opener, speaker, placeholder, true);
      }

      /// <summary>
      ///   Opens the interception meeting with the lord speaking FIRST. A lord who rode out to stop the player
      ///   does not wait to be addressed: this fires an NPC-led opening turn so he raises why he sought them out.
      ///   The interception directive in the system prompt (EncounterContext.InterceptionReason, resolved on this
      ///   turn's build) shapes what he opens with. A plain live conversation otherwise (full memory + actions),
      ///   mirroring the companion-audience opener.
      /// </summary>
      public void ActivateInterceptionOpener()
      {
         _mode = ChatMode.Conversation;

         ChatSpeaker speaker = MainSpeaker();
         if (speaker.Profile == null)
         {
            ModLog.Error("Interception opener: no profile, cannot open the meeting.");

            return;
         }

         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;

         // The lord opens. A synthetic cue, never shown or logged as a player line; the THIS MEETING directive
         // carries the actual reason he rode out (territorial challenge, or word of a fresh deed).
         const string opener = "[You rode out and stopped the player on the road. Open the conversation YOURSELF, " +
                               "in your own voice, on the footing described above. Do not wait for them to speak first.]";
         MarkOpenerPending();
         _ = RunLlmExchangeAsync(opener, speaker, placeholder, true);
      }

      /// <summary>Adds a single witness to the session. Used by the console command for testing.</summary>
      public void AddWitness(WitnessEntry entry)
      {
         // Never list the same witness twice: a companion who is already present (an auto-resolved witness, or
         // a scene companion) and is then also picked via "Bring" would otherwise appear duplicated (player report).
         if (entry == null || AlreadyPresent(entry)) return;
         _witnesses.Add(entry);
         _witnessColors[entry.Name] = ColorForWitness(entry);
         RefreshWitnessUi();
         ModLog.Info($"[WITNESSES] added: {entry.Name} ({entry.RelationToNpc}) color={_witnessColors[entry.Name]}");
      }

      /// <summary>True when an entry for the same witness (same hero id, or same name for a heroless bystander) is already present.</summary>
      private bool AlreadyPresent(WitnessEntry entry)
         => entry != null && _witnesses.Exists(w => WitnessDedup.IsSame(w, entry));

      /// <summary>
      ///   Displays a scene-narration line (neutral-narrator prose, no portrait) and mirrors
      ///   it to the story log and attributed transcript. Used for [NARRATION] blocks in
      ///   captive/CNC scenes. Recorded as a "Scene" statement so the speaker's next
      ///   rebuilt session sees what physically happened.
      ///   When <paramref name="insertBefore" /> is given, the line is inserted ahead of that
      ///   message (the pending speaker placeholder) instead of appended — used when the model
      ///   LED its turn with the narration, so the emitted order survives at render.
      /// </summary>
      public void AppendNarration(string text, ChatMessageViewModel insertBefore = null)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            var message = new ChatMessageViewModel(text, SceneNarrationColor);
            int insertAt = insertBefore != null ? _messages.IndexOf(insertBefore) : -1;
            if (insertAt >= 0)
               _messages.Insert(insertAt, message);
            else
               _messages.Add(message);
            ChatManager.ScrollToBottom();
            StoryLog.WriteNarration(text);
            // Adult-prompt audit C4: attributed to the SPEAKING NPC, not to a nameless third party. The
            // narration is that NPC's own output, and recording it with no hero id made the rebuild replay
            // it as a USER message ("[Scene]: ..."), so the model never saw [NARRATION] in its own assistant
            // history: on a long Continue sequence a weak model imitates the history over the system prompt,
            // and the inversion also taught it that the PLAYER writes the narration. The display name stays
            // "Scene" so the summarizer transcript still reads as a neutral narrator.
            if (IsLiveConversation)
               RecordTranscript(false, "Scene", _npcHero?.StringId, text, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"AppendNarration threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      public void AppendNpcMessage(string text, bool recordTranscript = true)
      {
         ModLog.Info($"AppendNpcMessage called with text='{text?.Substring(0, Math.Min(40, text?.Length ?? 0))}...'");

         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            // A heroless captor (synthesized bandit), OR a freshly-minted nemesis whose Hero tableau is not yet
            // renderable on the first encounter, falls back to a bandit-troop portrait so the line keeps an avatar.
            var npcMsg = new ChatMessageViewModel(_npcHero, _npcName, text, false);
            if (!npcMsg.HasLivePortrait && _npcPortraitCharacter != null)
               npcMsg = new ChatMessageViewModel(_npcPortraitCharacter, _npcName, text, false);
            _messages.Add(npcMsg);
            ModLog.Info("AppendNpcMessage: added to _messages");
            ChatManager.ScrollToBottom();
            if (recordTranscript) RecordTranscript(false, _npcName, _npcHero?.StringId, text);
         }
         catch (Exception ex)
         {
            ModLog.Error($"AppendNpcMessage threw {ex.GetType().Name}: {ex.Message}");
            ModLog.Error($"Stack: {ex.StackTrace}");
         }
      }

      /// <summary>
      ///   Adds an out-of-character (OOC) line in the terminal-style colour, used for the action-confirmation
      ///   prompts and their results. Distinct from an in-fiction system notification so the player reads it as
      ///   the game talking to them, not the NPC.
      /// </summary>
      public void AppendOocMessage(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            _messages.Add(new ChatMessageViewModel(text, OocColor));
            ChatManager.ScrollToBottom();
         }
         catch (Exception ex)
         {
            ModLog.Error($"AppendOocMessage threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      public void AppendPlayerMessage(string text, bool recordTranscript = true)
      {
         ModLog.Info($"AppendPlayerMessage called with text='{text?.Substring(0, Math.Min(40, text?.Length ?? 0))}...'");

         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            _messages.Add(new ChatMessageViewModel(Hero.MainHero, "You", text, true));
            ModLog.Info("AppendPlayerMessage: added to _messages");
            ChatManager.ScrollToBottom();
            if (recordTranscript) RecordTranscript(true, "You", Hero.MainHero?.StringId, text);
         }
         catch (Exception ex)
         {
            ModLog.Error($"AppendPlayerMessage threw {ex.GetType().Name}: {ex.Message}");
            ModLog.Error($"Stack: {ex.StackTrace}");
         }
      }

      /// <summary>
      ///   Adds a system notification line to the chat (relation change, gold
      ///   transfer). Color-coded by whether the outcome favors the player.
      /// </summary>
      /// <summary>
      ///   A neutral out-of-character notice, for a state change the player must SEE rather than infer.
      ///   Distinct from <see cref="AppendSystemMessage" />, whose green/red both carry a verdict: a
      ///   cordial parting is neither a gain nor a loss, and red would read as a rebuff.
      /// </summary>
      public void AppendSystemNotice(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            _messages.Add(new ChatMessageViewModel(text, OocColor));
            ChatManager.ScrollToBottom();
         }
         catch (Exception ex)
         {
            ModLog.Error($"AppendSystemNotice threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      public void AppendSystemMessage(string text, bool isPositive)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            _messages.Add(new ChatMessageViewModel(text, isPositive
               ? SystemGainColor
               : SystemLossColor));
            ChatManager.ScrollToBottom();
         }
         catch (Exception ex)
         {
            ModLog.Error($"AppendSystemMessage threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Appends a witness reaction as a separate chat message, resolved to the
      ///   witness's own portrait if their <see cref="WitnessEntry.HeroStringId" />
      ///   is known. Falls back to name-only when the hero cannot be found.
      /// </summary>
      public void AppendWitnessReaction(string witnessName, string text, bool recordTranscript = true)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            // Resolve the reacting party's portrait hero, colour, and id — a listed witness, or the
            // main NPC when another participant holds the floor. See ResolveWitnessRender.
            (Hero witnessHero, string color, string heroId) = ResolveWitnessRender(witnessName);

            // Heroless bandit witness → render a bandit-troop portrait instead of a blank avatar.
            CharacterObject witnessFace = null;
            if (witnessHero == null) _witnessPortraits.TryGetValue(witnessName, out witnessFace);

            _messages.Add(witnessHero == null && witnessFace != null
               ? new ChatMessageViewModel(witnessFace, witnessName, text, color)
               : new ChatMessageViewModel(witnessHero, witnessName, text, color));
            ChatManager.ScrollToBottom();
            StoryLog.WriteWitnessLine(witnessName, text);

            // Record into the attributed transcript so every speaker's next rebuilt
            // session sees this reaction as a "[Name]: …" line. Skip letter/compose.
            // recordTranscript=false is used by 15C, which commits manually after the
            // auto-reply LLM call so the session doesn't double-count the reactions.
            if (recordTranscript && IsLiveConversation)
               RecordTranscript(false, witnessName, heroId, text);
         }
         catch (Exception ex)
         {
            ModLog.Error($"AppendWitnessReaction threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      public void ExecuteClose()
      {
         OnCloseRequested?.Invoke();
      }

      /// <summary>The review panel's "Close Preview" button: closes it (the text lives in the small box).</summary>
      public void ExecuteCloseCompose()
      {
         if (!_isComposeOpen) return;
         IsComposeOpen = false;
      }

      /// <summary>The memory panel's "Close" button.</summary>
      public void ExecuteCloseMemoryPanel()
      {
         if (!_isMemoryPanelOpen) return;
         IsMemoryPanelOpen = false;
      }

      /// <summary>The portrait panel's "Close" button.</summary>
      public void ExecuteClosePortraitPanel()
      {
         if (!_isPortraitPanelOpen) return;
         IsPortraitPanelOpen = false;
      }

      /// <summary>
      ///   Player chose "Continue" during a captive scene pause. Fires one more NPC-led
      ///   narration turn. Input stays locked until the NPC asks a question or the scene ends.
      /// </summary>
      public void ExecuteContinue()
      {
         if (!_isScenePaused || _scenePauseSpeaker == null || _scenePauseBehavior == null) return;
         IsScenePaused = false;
         _sceneContinuations++;
         _sceneContinuationPending = true;
         IsSendEnabled = false;
         _ = RunCaptiveSceneContinuationAsync();
      }

      /// <summary>
      ///   Legacy entry point kept for the old "Give Gold" binding: routes to the same gold
      ///   prompt the unified give control now uses.
      /// </summary>
      public void ExecuteOfferGold()
      {
         if (_mode != ChatMode.Conversation || _conversationEnded) return;
         _offer.PromptGold(this);
      }

      /// <summary>
      ///   The bag: the ONE "what do you offer?" control. It gives an item, gold, OR (when the player holds
      ///   hero captives) a prisoner, so no separate prisoner button clutters the row. A bargain awaiting
      ///   payment-in-kind is settled directly; otherwise, when captives are held, the player first picks a
      ///   category, else it goes straight to the item/gold picker. The pick pre-fills the chat input, which
      ///   the player edits and sends as usual.
      /// </summary>
      public void ExecuteOfferItem()
      {
         _offer.Execute(this);
      }

      /// <summary>
      ///   The "&lt;" button: opens the left REVIEW panel — a wrapped, read-only view of what you have typed
      ///   (editing stays in the small box; the panel just lets a verbose player read the whole message back).
      /// </summary>
      public void ExecuteOpenCompose()
      {
         if (!IsNormalInputVisible || _isComposeOpen) return;
         if (_isMemoryPanelOpen) IsMemoryPanelOpen = false; // share the left area with the memory panel
         if (_isPortraitPanelOpen) IsPortraitPanelOpen = false; // and with the portrait panel
         IsComposeOpen = true;
      }

      public void ExecuteOpenEncyclopedia()
      {
         try
         {
            if (_npcHero == null || Campaign.Current?.EncyclopediaManager == null) return;

            // No explicit focus hand-off needed: ChatManager.ArbitrateFocus (pumped every frame) sees the
            // encyclopedia's layer appear and yields the chat's focus to it, then reclaims it on close.
            Campaign.Current.EncyclopediaManager.GoToLink(_npcHero.EncyclopediaLink);
            ModLog.Info($"[ENCYCLOPEDIA] opened on {_npcName} from the chat.");
            ChatManager.LogScreenState("encyclopedia button"); // where did it actually land?
         }
         catch (Exception ex)
         {
            ModLog.Error($"ExecuteOpenEncyclopedia failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Opens the in-game encyclopedia on the conversation partner's page, over the chat, so a player can
      ///   place who they are speaking of (and browse on from there) without the chat's modal focus locking
      ///   the rest of the interface away (a player-requested need). The chat stays open behind it and
      ///   <see cref="ChatManager.SuspendForExternalScreen" /> hands focus to the encyclopedia, reclaiming it
      ///   automatically once the player closes it.
      /// </summary>
      /// <summary>
      ///   The "Options" button: opens the vanilla escape/options menu over the chat, the same call ESC makes
      ///   on the map (see <see cref="GameMenuOpener" />). The focus arbiter yields the chat to it and reclaims
      ///   focus when it closes, exactly like the encyclopedia. A graceful no-op if the menu cannot be reached.
      /// </summary>
      public void ExecuteOpenGameMenu()
      {
         // Pass the chat's real host screen (the naval map under War Sails, or the land map) so the menu opens
         // on the VISIBLE screen rather than the fixed land MapScreen.Instance, which sat hidden behind the naval
         // map for players on the water (the "log says opened but no menu appears" report).
         if (!GameMenuOpener.TryOpenEscapeMenu(ChatManager.HostScreen))
            ModLog.Info("[MENU] the options menu could not be opened from the chat this time.");
      }

      /// <summary>
      ///   Player chose "Please continue" on a finished captive scene: relaunch a FRESH cycle of
      ///   the encounter with the same captor and witnesses (a new structure, new driving focus),
      ///   so the player can keep going if they want — and as a recovery when a weaker model ended
      ///   the scene abruptly and left them hanging.
      /// </summary>
      public void ExecutePleaseContinue()
      {
         if (!IsCaptiveEncounter || !_conversationEnded) return;

         _conversationEnded = false;
         _captiveBeatCount = 0;
         _director = new CaptiveSceneDirector(
            _captiveEncounterIsCollective, CountAggressorWitnesses(), MBRandomSource.Instance,
            _captiveEncounterIsBandit, _captiveEncounterIntent);
         IsPleaseContinueVisible = false;
         IsScenePaused = false;
         IsSendEnabled = false;
         ModLog.Info("[SCENE] player requested another cycle (Please continue).");

         ChatSpeaker captor = MainSpeaker();
         if (captor.Profile == null)
         {
            IsSendEnabled = true;

            return;
         }

         ChatMessageViewModel placeholder = MakeSpeakerMessage(captor, PlaceholderText);
         _messages.Add(placeholder);

         // Same non-sexual guard as ActivateCaptiveEncounterMode: a Reckoning/Extortion/Intimidation/
         // Revenge scene resumed via "Please continue" must not inject a sexual scene focus (bugfix
         // 01/07/2026, the opener path already guarded this, the resume path did not).
         bool nonSexual = _captiveEncounterIntent is CaptiveSceneIntent.Reckoning
            or CaptiveSceneIntent.Extortion
            or CaptiveSceneIntent.Intimidation
            or CaptiveSceneIntent.Revenge;
         // The resumed beat draws its own writing lens too (T1), so a scene that resumes after a lost escape
         // is not told through whatever lens the last window happened to leave on the field.
         _captiveSceneStyle = CaptiveSceneText.PickStyleRegister(ref _lastCaptiveStyleIndex);
         string resumeSetup = nonSexual
            ? ""
            : CaptiveSceneText.BuildResumeSetup(_captiveEncounterIntent, ref _lastSceneFocusIndex, out _captiveSceneFocus);
         string opener = CaptiveSceneText.BuildResumeOpener() + resumeSetup;
         MarkOpenerPending();
         _ = RunLlmExchangeAsync(opener, captor, placeholder, true);
      }

      /// <summary>
      ///   Player requests a private audience — sets the injection flag then immediately
      ///   fires an LLM exchange so the NPC can accept or refuse in the same turn.
      ///   The synthetic player line is shown in the chat and sent to the LLM.
      /// </summary>
      public void ExecuteRequestPrivate()
      {
         if (_privacyRequestUsed || _witnesses.Count == 0) return;

         // A prisoner has no say over who stays: the player commands the room. Clear the witnesses outright,
         // with no LLM round-trip and no chance of a "refusal" (player suggestion, ApocalyptikGreen 2026-07-10).
         if (NpcIsPlayerPrisoner())
         {
            _privacyRequestUsed = true;
            _witnesses.Clear();
            _currentSpeakerHeroId = null; // no witnesses left, back to the main NPC
            RefreshWitnessUi();
            AppendSystemMessage("You order the others out. You are alone with your prisoner.", true);
            ModLog.Info("[WITNESSES] private audience taken by force (the player's prisoner has no say).");

            return;
         }

         _privacyRequestPending = true;
         _privacyRequestUsed = true;
         RefreshWitnessUi();
         ModLog.Info("[WITNESSES] player requested private audience.");
         // Trigger immediate exchange; _privacyRequestPending is already true so
         // EncounterContextBuilder will inject the privacy request into the prompt.
         InputText = "I would speak with you privately.";
         ExecuteSendMessage();
      }

      /// <summary>
      ///   Player opens the floor to the whole group (the "Round Table" button): the main NPC and each present
      ///   witness take a turn in a deterministic lap (capped, they may address one another), then control returns
      ///   to the player. A model-independent alternative to hoping the model volunteers witness reactions.
      /// </summary>
      public void ExecuteRoundTable()
      {
         // Round-table audit D6: the kill-switch was enforced only by the BUTTON's visibility, so any other
         // invocation (a future hotkey, the planned assembly "Open the assembly" entry) would have run a
         // feature that ships deliberately disabled and un-play-tested. The switch belongs on the action.
         if (!RoundTableController.Enabled) return;
         if (_npcWitnessExchangePending || _conversationEnded || IsCaptiveEncounter) return;
         if (_witnesses.Count == 0 || !IsSendEnabled) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.LlmServices is not {IsAvailable: true}) return;

         ModLog.Info("[ROUNDTABLE] player opened the floor (button).");
         _npcWitnessExchangePending = true;
         IsSendEnabled = false;
         _ = RunRoundTableLapAsync(AllPresentSpeakers());
      }

      // A player asked for tooltips on the chat buttons; a single "?" popup was preferred over per-button hints
      // that get repetitive with use. One place to explain the whole window, opened only when the player wants it.
      private static readonly string HelpText =
         "Type anything you like and the character answers in their own voice. The whole conversation stays on "
       + "screen, not just their last line.\n\n"
       + "Buttons:\n"
       + "- Encyclopedia: open this character's page without leaving the chat.\n"
       + "- Memories: everything this character remembers of you, dated, oldest first.\n"
       + "- The < review panel: read your whole message back before you send it.\n"
       + "- Quill (styles): choose the narrative voice the AI writes in.\n"
       + "- Bag (give item) and Give Gold: hand over goods or coin, as a gift or to settle a deal.\n"
       + "- Request Private: ask the character to clear any onlookers so you can speak alone.\n"
       + "- Leave / End: close the conversation.\n\n"
       + "In a captive or paced scene: Continue advances the moment, Try something lets you act in your own "
       + "words, and Please continue nudges a stalled scene forward.\n\n"
       + "What you say has real weight: relations shift, coin changes hands, and promises can become quests. "
       + "The character remembers all of it next time you meet.";

      /// <summary>Opens the "?" help popup that explains the chat window, so the buttons need no per-button tooltips.</summary>
      public void ExecuteShowHelp()
      {
         try
         {
            InformationManager.ShowInquiry(new InquiryData(
               "How the chat window works", HelpText,
               true, false,
               "Close", null,
               () => { }, null), false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"ExecuteShowHelp threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The in-window "Voices" control. SUPERSEDED by the council narrator model (2026-08-02): every real
      ///   council turn now fires the single narrator call regardless of this setting (see
      ///   <see cref="ExecuteSendCouncilMessage" />), which replaced BOTH the old whole-table call (the anchor
      ///   answers, others merely react) and the old per-member lap (a full round-trip per seated member). The
      ///   toggle and its label stay in the window (Gabriel iterates on the reshape in-game) but no longer
      ///   change how a turn actually fires; flipping it still updates the label so the control is not visibly
      ///   dead, but <see cref="ExecuteSendMessage" /> no longer branches on <see cref="_councilVoiceMode" />.
      /// </summary>
      public void ExecuteToggleCouncilVoiceMode()
      {
         if (_councilScope == null) return;

         _councilVoiceMode = _councilVoiceMode == CouncilVoiceMode.WholeTable
            ? CouncilVoiceMode.PerMember
            : CouncilVoiceMode.WholeTable;

         CouncilVoiceModeText = DescribeCouncilVoiceMode(_councilVoiceMode);
         ModLog.Info($"[COUNCIL] voice mode switched to {_councilVoiceMode} (takes effect on the next turn).");
      }

      private static string DescribeCouncilVoiceMode(CouncilVoiceMode mode)
         => mode == CouncilVoiceMode.PerMember
            ? "Voices: each member"
            : "Voices: whole table";

      /// <summary>
      ///   Re-reads the style picker's visibility and label from <see cref="StyleService" />. Called when the
      ///   window opens and by the console toggle, so arming the picker (or picking a style from the console)
      ///   while a chat is open updates the header instead of waiting for the next conversation.
      /// </summary>
      public void RefreshStyleUi()
      {
         IsStylePickerVisible = StyleService.PickerEnabled;
         StyleIconPath = StyleCatalog.IconExists
            ? StyleCatalog.IconPath
            : "";
         IsStyleLabelVisible = IsStylePickerVisible && string.IsNullOrEmpty(StyleIconPath);
         StyleButtonText = $"Style: {StyleService.ActiveName}";
         OnPropertyChanged(nameof(IsStyleIconVisible)); // computed from the two flags above
      }

      /// <summary>
      ///   The header's style button: lists the style files on disk and lets the player pick the writing voice
      ///   without leaving the conversation. The pick is global and applies from the NEXT message on (the style
      ///   rides the prompt's cacheable prefix, so it cannot change a reply already in flight). A no-op with a
      ///   clear notice when the folder holds no styles.
      /// </summary>
      public void ExecuteOpenStylePicker()
      {
         try
         {
            IReadOnlyList<string> names = StyleCatalog.AvailableNames();

            if (names.Count == 0)
            {
               MBInformationManager.AddQuickInformation(new TextObject(
                  $"No narrative styles found. Add a .txt file to {StyleCatalog.FolderPath}."));

               return;
            }

            string active = StyleService.ActiveName;
            List<InquiryElement> options = names
                                           .Select(n => new InquiryElement(n, string.Equals(n, active, StringComparison.OrdinalIgnoreCase)
                                                           ? n + "   (current)"
                                                           : n, null))
                                           .ToList();

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
               "Narrative Style",
               "How should the writing read? The style shapes the prose only, never who the characters are.",
               options, true, 1, 1, "Use this style", "Cancel",
               picked =>
               {
                  if (picked?.FirstOrDefault()?.Identifier is not string chosen) return;

                  StyleService.Select(chosen);
                  RefreshStyleUi();
                  MBInformationManager.AddQuickInformation(new TextObject(
                     $"Narrative style: {StyleService.ActiveName}. It applies from your next message."));
               },
               null));
         }
         catch (Exception ex)
         {
            ModLog.Error($"ExecuteOpenStylePicker threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      public void ExecuteSendMessage()
      {
         // While a consequential action awaits confirmation, this input is the Y/n answer, not an LLM turn.
         // An empty input (the player just pressed Enter) is the default Yes, so intercept BEFORE the empty guard.
         if (_confirmation.IsAwaiting)
         {
            string answer = InputText ?? "";
            InputText = "";
            _confirmation.HandleInput(this, answer);

            return;
         }

         string text = (InputText ?? "").Trim();

         if (string.IsNullOrEmpty(text)) return;
         if (!IsSendEnabled) return;

         // Council narrator model (2026-08-02): a real council never routes to a per-witness speaker (that
         // whole-conversation retargeting is the 15B one-on-one pivot, wrong for a narrator call that must stay
         // backed by ONE consistent profile) and never branches on _councilVoiceMode any more (superseded, see
         // ActivateCouncil). Handled entirely in its own method so the ordinary path below stays exactly as it
         // was for every non-council conversation.
         if (_councilScope != null)
         {
            ExecuteSendCouncilMessage(text);

            return;
         }

         // Sprint 15B: route this turn to whoever the player is addressing — the main
         // NPC by default, or a witness named in a stage direction / vocative (sticky).
         ChatSpeaker speaker = ResolveAddressedSpeaker(text);
         RefreshWitnessUi(); // refresh the "Speaking to …" indicator

         // Display only — the player line is committed to the transcript after the call
         // so BuildSessionFor doesn't double-count it (ChatAsync adds it itself).
         AppendPlayerMessage(text, false);
         IsSendEnabled = false;
         InputText = "";
         if (_isComposeOpen) IsComposeOpen = false; // sending closes the review panel
         _witnessExchangesThisRound = 0; // reset the 15C cap for this new player turn
         _sceneContinuations = 0; // reset scene-continuation count for this new player turn

         // The player may open the floor to the group in their own words ("what do you all think?"); consumed
         // after the main NPC's reply to run a round-table lap. Only outside captive scenes, with a group present.
         _roundTable.OnPlayerSend(text, _witnesses.Count, IsCaptiveEncounter);
         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         _ = RunLlmExchangeAsync(text, speaker, placeholder);
      }

      /// <summary>
      ///   A real council's player turn: ONE narrator call, always backed by the anchor's profile (never
      ///   retargeted the way an ordinary conversation pivots to an addressed witness, see
      ///   <see cref="ResolveAddressedSpeaker" />). Address detection here only flags WHO must be guaranteed a
      ///   reply this turn (<see cref="ResolveAddressedCouncilMemberName" />); it never changes whose profile
      ///   fires the call. Mirrors the shape of the ordinary branch in <see cref="ExecuteSendMessage" /> (player
      ///   line display, engagement-panel mirror, per-turn counters), minus the per-witness/per-member branches
      ///   that no longer apply to a council.
      /// </summary>
      private void ExecuteSendCouncilMessage(string text)
      {
         string addressedName = ResolveAddressedCouncilMemberName(text);

         // Bespoke council window (Inc 1): before this new turn, mirror what the table has settled so far into
         // the Engagements panel. Reads the previous, fully-settled turn (this send's lines are not committed to
         // the transcript until the call runs), so the panel stays one turn behind but never mid-turn.
         KickCouncilEngagementExtraction();

         AppendPlayerMessage(text, false);
         IsSendEnabled = false;
         InputText = "";
         if (_isComposeOpen) IsComposeOpen = false;
         _witnessExchangesThisRound = 0;
         _sceneContinuations = 0;

         ChatSpeaker anchor = MainSpeaker();
         ChatMessageViewModel placeholder = MakeSpeakerMessage(anchor, CouncilPlaceholderText);
         ApplyCouncilSceneImage(placeholder); // scene image while the council weighs this turn, instead of the anchor's own portrait
         _messages.Add(placeholder);
         _ = RunLlmExchangeAsync(text, anchor, placeholder, false, addressedName);
      }

      /// <summary>
      ///   Reuses the Sprint 15B address detector to find whom the player addressed BY NAME this council turn,
      ///   without any of its ordinary side effects (it never retargets the call, never touches
      ///   <see cref="_currentSpeakerHeroId" />): council routing always stays on the anchor. Returns the
      ///   anchor's OWN name when the player addressed them directly (so the prompt's "your own [DIALOGUE]"
      ///   wording resolves correctly), a seated member's name when a witness was addressed, or null when
      ///   nobody in particular was.
      /// </summary>
      private string ResolveAddressedCouncilMemberName(string text)
      {
         string detectedId = DetectAddressedParticipant(text);

         if (detectedId == null) return null;
         if (detectedId == _npcHero?.StringId) return MainSpeaker()?.Name;

         return _witnesses.FirstOrDefault(w => w.HeroStringId == detectedId)?.Name;
      }

      /// <summary>
      ///   The "Memories" button: opens the left panel listing what this NPC remembers of the player (a
      ///   read-only view of the shared history, mirroring cr.memory), or closes it if already open. Shares the
      ///   left area with the review panel, so opening one closes the other.
      /// </summary>
      public void ExecuteToggleMemoryPanel()
      {
         if (_isMemoryPanelOpen)
         {
            IsMemoryPanelOpen = false;

            return;
         }

         if (_isComposeOpen) IsComposeOpen = false;
         if (_isPortraitPanelOpen) IsPortraitPanelOpen = false; // one left panel at a time
         RefreshMemories();
         IsMemoryPanelOpen = true;
      }

      /// <summary>
      ///   Flips the restored manual PROSE-source switch (prose=Roleplay ⇄ prose=NSFW). A runtime session flag on
      ///   <see cref="LlmServices" />, read live by the model/provider resolution, so the next reply routes to the
      ///   chosen model with no rebuild. Refreshes the button label. Only reachable while the MCM gate shows the
      ///   button (<see cref="IsModelModeToggleVisible" />).
      /// </summary>
      public void ExecuteToggleModelMode()
      {
         LlmServices.ManualNsfwProse = !LlmServices.ManualNsfwProse;
         OnPropertyChanged(nameof(ModelModeText));
         ModLog.Info($"[chat] manual PROSE source flipped to {(LlmServices.ManualNsfwProse ? "NSFW" : "Roleplay")} model.");
      }

      /// <summary>
      ///   Player chose "Try something" during a captive scene pause. Restores the normal
      ///   text input so the player can type a response and break the scene's flow.
      /// </summary>
      public void ExecuteTrySomething()
      {
         if (!_isScenePaused) return;
         IsScenePaused = false;
         _sceneContinuations = 0;
         _scenePauseSpeaker = null;
         _scenePauseBehavior = null;
         IsSendEnabled = !_conversationEnded;
      }

      // ── Sprint 17: captive scene memory ───────────────────────────────────

      /// <summary>
      ///   Records the captive scene in the captor's memory. Called once when the scene
      ///   closes (from <see cref="ChatManager.Close" />). Two stages:
      ///   (1) a base Captivity event recorded synchronously — guaranteed even if the
      ///   async summary never returns;
      ///   (2) a richer first-person summary generated asynchronously from the scene
      ///   transcript, which replaces the base summary on arrival.
      ///   Idempotent and a no-op outside captive mode.
      /// </summary>
      public void FinalizeCaptiveSceneMemory()
      {
         if (_captiveMemoryFinalized || !IsCaptiveEncounter) return;
         _captiveMemoryFinalized = true;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         // The scene is closing: mark it inactive so a later harm_prisoner emission during ordinary
         // captivity conversation can no longer deduct the player's health.
         behavior?.GameStateBridge?.EndCaptiveSceneTracking();

         // A committed escape is applied now, as the scene closes — deferred from the moment it
         // was rolled so freeing the player never popped an encounter over the open scene window.
         behavior?.GameStateBridge?.FinalizePendingEscape();

         // A captor-granted release is likewise applied now, as the scene closes (deferred from the moment
         // the captor emitted release_player) so freeing the player never popped an encounter mid-scene.
         behavior?.GameStateBridge?.FinalizePendingRelease();

         // The body's toll for a forced sexual scene: guaranteed only if the captor's own
         // narration never wounded the player (a harm_prisoner action). Runs for both lord and
         // bandit captors, so it precedes the bandit-profile persistence guard below.
         if (CaptiveIntentRules.IsForcedSexual(_captiveEncounterIntent))
         {
            int lost = behavior?.GameStateBridge?.ApplyForcedSceneToll() ?? 0;
            if (lost > 0)
            {
               AppendSystemMessage($"The ordeal leaves its mark — you lose {lost} health.", false);
               ModLog.Info($"[CAPTIVE] forced-scene toll applied: -{lost} HP.");
            }
         }

         // Grudges pillar (Tier 2, CaptiveAbuse — the companion revenge driver): independent of the
         // captor's OWN memory below (skipped outright for an ephemeral bandit captor), since the
         // grudge holder here is the tormented COMPANION, never the captor.
         FinalizeCompanionVictimConsequence();

         NpcProfile profile = _captiveEncounterProfile;

         if (behavior?.Store == null || profile == null) return;

         // A synthesized bandit captor is ephemeral — never persist it (would bloat the store
         // with a profile no future conversation will ever read).
         if (profile.Id.StartsWith("bandit_captor_", StringComparison.Ordinal)) return;

         int gameDay = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string intentVerb = CaptiveIntentRules.Verb(_captiveEncounterIntent);
         string baseSummary = BuildCaptiveBaseSummary(intentVerb);

         // Stage 1 — synchronous base capture.
         var baseEvent = new NotableEvent(gameDay, NotableEventType.Captivity, baseSummary);
         profile.Events.Add(baseEvent);
         behavior.Store.Set(profile);
         ModLog.Info($"[CAPTIVE MEMORY] base event recorded for {profile.Name}: \"{baseSummary}\"");

         // Stage 2 — async rich summary (fire-and-forget; replaces the base on success).
         string transcript = BuildSceneTranscriptText("Prisoner");

         if (string.IsNullOrWhiteSpace(transcript) || behavior.LlmServices?.LlmClient == null) return;

         bool playerIsFemale = LlmServices.TestPlayerGenderOverride ?? Hero.MainHero?.IsFemale ?? false;
         _ = ConversationSummaryService.GenerateAndApplyCaptiveSummaryAsync(behavior, profile.Id, baseEvent, transcript, intentVerb, playerIsFemale);
      }

      /// <summary>
      ///   Grudges pillar (Tier 2, CaptiveAbuse): the reference site for the "player as captor" path
      ///   (<see cref="ActivateCaptorSceneMode" />). Called once when the scene closes, from
      ///   <see cref="ChatManager.Close" />. No-op outside that mode, and no-op when the scene's intent
      ///   maps to Severity 0 (Reward never grudges — rewarding your own prisoner earns no resentment).
      ///   Unlike <see cref="FinalizeCaptiveSceneMemory" />, a captor scene keeps no dedicated Captivity
      ///   memory of its own today — it runs as a plain Conversation, closed by
      ///   <see cref="FinalizeConversationMemory" />'s generic "Spoke with" fallback — so this method
      ///   exists purely to plant the grudge, independent of whichever memory write eventually lands
      ///   there.
      /// </summary>
      public void FinalizeCaptorSceneMemory()
      {
         if (_captorSceneMemoryFinalized || !_captorScene) return;
         _captorSceneMemoryFinalized = true;

         if (Hero.MainHero == null) return;

         Hero prisoner = _npcHero ??
                         (_captorSceneProfile != null
                            ? Hero.FindFirst(h => h != null && h.StringId == _captorSceneProfile.Id)
                            : null);

         // Chronicle pillar: independent of whether a grudge forms below (a Reward scene never
         // grudges, but could still be a chronicle-worthy conquest of a highborn captive).
         RecordCaptiveConquestChronicleIfNeeded(prisoner);

         int severity = CaptiveIntentRules.CaptiveAbuseSeverity(_captorSceneIntent);
         var facts = new CaptiveAbuseFacts {Severity = severity, PlayerWasThePerpetrator = true};
         GrudgeProposal proposal = Tier2GrudgeRules.CaptiveAbuseGrudge(facts);

         if (!proposal.Forms) return;
         if (prisoner == null) return;

         int today = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;
         const string cause = "The player made me suffer while I was helpless, their prisoner to torment.";

         GrudgeBehavior.Instance?.TryPlant(prisoner, Hero.MainHero, GrudgeSource.CaptiveAbuse, cause,
            proposal.Magnitude, proposal.KnownToPlayer, today);
      }

      /// <summary>
      ///   Records a light "I was present" memory into each HERO witness's own profile when a group
      ///   conversation closes, so a later one-to-one with them reflects the shared scene — the player
      ///   reported companions not recognising they had taken part in a group conversation. Called once from
      ///   <see cref="ChatManager.Close" />. Synthetic/bandit witnesses (no HeroStringId) and the main NPC are
      ///   skipped (the latter is recorded by <see cref="FinalizeConversationMemory" />). First slice: only
      ///   witnesses the player has already met (an existing profile) — a stranger witness is left for a later
      ///   get-or-create pass. No-op outside plain Conversation mode or when the exchange had no substance
      ///   (fewer than two completed player turns).
      /// </summary>
      /// <summary>
      ///   Executes a granted companion retirement at chat close — deferred from the in-chat <c>retire</c> action
      ///   so a hero is never removed mid-conversation. Idempotent; a no-op unless this audience was a retirement
      ///   the player granted. The landless companion leaves; the landed one steps back from the field.
      /// </summary>
      public void FinalizeCompanionAudience()
      {
         if (_audienceReason != CompanionAudienceReason.Retirement || !_retireGranted) return;
         _retireGranted = false; // one-shot

         Hero h = _npcHero ??
                  (_audienceProfile != null
                     ? Hero.FindFirst(x => x != null && x.StringId == _audienceProfile.Id)
                     : null);

         if (h == null) return;

         WarWearinessBehavior.Instance?.ExecuteRetirement(h, _audienceLanded);
      }

      // ── Fallback conversation memory (tester report: memories only "clicked"
      //    when the NPC formally ended the conversation) ──────────────────────

      /// <summary>
      ///   Ensures an ordinary conversation always leaves a recap memory, whether or
      ///   not the LLM also emitted an [EVENT] block. Called once from
      ///   <see cref="ChatManager.Close" />. Same two-stage pattern as
      ///   <see cref="FinalizeCaptiveSceneMemory" />: a plain base event recorded
      ///   synchronously, replaced by a richer first-person transcript summary when
      ///   the async call succeeds. An [EVENT] captures one dramatic beat; this recap
      ///   captures what was discussed, decided, or promised, and the two coexist, so
      ///   this no longer skips when an [EVENT] fired that session (a stated plan the
      ///   model did not flag as an [EVENT] was otherwise lost, a player-reported
      ///   fault: NPCs forgot a same-day stated plan one conversation later). Still a
      ///   no-op when the exchange had no substance (pure
      ///   <see cref="ConversationMemoryRules" />: two completed player turns, or ONE
      ///   turn whose text carried real substance, a player-reported fault: a
      ///   single-exchange conversation left no memory), or the chat is not in plain
      ///   Conversation mode (letters keep their own records; captive scenes have
      ///   their own finalizer).
      /// </summary>
      public void FinalizeConversationMemory()
      {
         if (_conversationMemoryFinalized || _mode != ChatMode.Conversation) return;
         _conversationMemoryFinalized = true;

         if (!ConversationMemoryRules.DeservesFallbackMemory(_playerTurnsThisSession, _playerCharsThisSession)) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         // Nemesis audit RC2: this wrote to DialogFlow.Profile, a session-lifetime cache set only by an
         // ordinary conversation's open. A player-as-captor scene (a nemesis you hold prisoner) runs in
         // Conversation mode too but is opened WITHOUT setting it, so a captor scene that ended with no [EVENT]
         // landed its fallback summary on the LAST person chatted with (the exact "Garios thinks he's Fercread"
         // family, already fixed for the speaker resolution). MainSpeaker().Profile is that same hardened
         // resolution: it follows the current scene's hero (captor-scene / captive profile first, DialogFlow
         // only as a last resort), so the memory can never be written to the wrong character.
         NpcProfile profile = MainSpeaker()?.Profile;

         if (behavior?.Store == null || profile == null) return;

         int gameDay = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string player = Hero.MainHero?.Name?.ToString() ?? "the player";

         // Stage 1 — synchronous base capture (guaranteed even if the async summary
         // never returns). Plain but truthful; better than the NPC forgetting.
         var baseEvent = new NotableEvent(gameDay, NotableEventType.Other,
            $"Spoke with {player}.");
         profile.Events.Add(baseEvent);
         behavior.Store.Set(profile);
         ModLog.Info($"[CONV MEMORY] conversation recap recorded for {profile.Name} " + $"({_playerTurnsThisSession} player turn(s)).");

         // Stage 2, the rich summary, via a DURABLE queue instead of a detached Task: the job is persisted
         // with the campaign, drained in the background, and replayed on load, so a plan or promise the NPC
         // made survives a save/quit/crash instead of being lost when the fire-and-forget Task never returned.
         string transcript = BuildSceneTranscriptText(player);

         if (string.IsNullOrWhiteSpace(transcript)) return;

         behavior.ConvSummaryQueue.Enqueue(new PendingSummaryJob {
            ProfileId = profile.Id,
            AnchorDay = baseEvent.gameDay,
            AnchorType = baseEvent.type.ToString(),
            AnchorSummary = baseEvent.summary,
            Transcript = transcript,
            PlayerName = player,
            ReplyLanguage = ModSettings.ReplyLanguage,
            EnqueuedDay = gameDay
         });
      }

      /// <summary>
      ///   Extension Surface: hands the whole attributed transcript to third-party listeners as the conversation
      ///   closes (<see cref="CrConversation.Ended" />), including witness (third-party) and narration lines. Runs
      ///   independently of the memory finalizer: a listener may want the transcript of a conversation CR itself
      ///   judged too slight to record. Once per conversation, and only when the NPC was actually established.
      ///   Called from <see cref="ChatManager.Close" />.
      /// </summary>
      public void RaiseConversationEndedEvent()
      {
         if (_conversationEndedRaised || _npcHero?.StringId == null) return;
         _conversationEndedRaised = true;

         List<CrTranscriptLine> lines = _transcript.Lines
            .Select(l => new CrTranscriptLine(l.SpeakerHeroId, l.SpeakerName, l.IsPlayer, l.IsNarration, l.Text))
            .ToList();

         CrConversation.RaiseEnded(_npcHero.StringId, (int) CampaignTime.Now.ToDays, lines);
      }

      /// <summary>
      ///   The player actually sat and talked with a companion: being heard lifts their spirits (player request,
      ///   SuperGraham339, who wanted conversations to count toward contentment). Rate-limited inside the
      ///   happiness behaviour, so reopening the window cannot be farmed. Called once as the chat closes; a no-op
      ///   for anyone who is not a companion, and for a window the player never actually spoke in.
      /// </summary>
      public void NoteCompanionHeard()
      {
         if (_companionHeardNoted || _mode != ChatMode.Conversation) return;
         _companionHeardNoted = true;

         if (_playerTurnsThisSession < 1) return;
         if (_npcHero is not {IsAlive: true, IsPlayerCompanion: true}) return;

         CompanionHappinessBehavior.Instance?.NoteHeardOut(_npcHero);
      }

      /// <summary>
      ///   Writes a coherent closing line to the story log when the player leaves a plain
      ///   conversation that the NPC did not already close with a farewell — so the transcript
      ///   reads like a scene that ends rather than one cut off mid-page. No-op outside plain
      ///   Conversation mode, when nothing was logged this session, or when a natural farewell /
      ///   end_conversation already closed it. Called from <see cref="ChatManager.Close" />.
      /// </summary>
      public void FinalizeStoryLog()
      {
         if (_mode != ChatMode.Conversation || _conversationEnded || !_storyIntroWritten) return;
         if (!StoryLog.IsEnabled) return;
         StoryLog.WriteNarration($"You take your leave of {_npcName}.");
      }

      public void FinalizeWitnessMemories()
      {
         if (_witnessMemoryFinalized || _mode != ChatMode.Conversation) return;
         _witnessMemoryFinalized = true;

         if (_playerTurnsThisSession < 2 || _witnesses == null || _witnesses.Count == 0) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.Store == null) return;

         int gameDay = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string player = Hero.MainHero?.Name?.ToString() ?? "the player";
         string mainNpc = _npcName;
         string mainNpcId = _npcHero?.StringId;

         var recorded = 0;
         foreach (WitnessEntry w in _witnesses)
         {
            if (string.IsNullOrEmpty(w?.HeroStringId)) continue; // synthetic/bandit witness — no profile to hold a memory
            if (w.HeroStringId == mainNpcId) continue; // the main NPC is recorded separately

            NpcProfile wp = behavior.Store.Get(w.HeroStringId);

            if (wp == null) continue; // never met one-to-one yet — first slice skips strangers

            wp.Events.Add(new NotableEvent(gameDay, NotableEventType.Other,
               $"Was present while {player} spoke with {mainNpc}, and saw what passed between them."));
            behavior.Store.Set(wp);
            recorded++;
         }

         if (recorded > 0)
            ModLog.Info($"[WITNESS MEMORY] recorded a 'was present' memory for {recorded} hero witness(es) of the talk with {mainNpc}.");
      }

      /// <summary>
      ///   Opens the dedicated left PORTRAIT panel showing this chat line's speaker at full size (their custom
      ///   ChatPortraits image when present, else the vanilla 3D render), aspect-preserved and never stretched.
      ///   Fired by a LEFT-click on any chat portrait. Shares the left area with the memory / compose panels, so
      ///   it closes whichever of those is open (one left panel at a time).
      /// </summary>
      public void OpenPortraitPanel(ChatMessageViewModel msg)
      {
         if (msg == null) return;

         // Manuscript skin: the info dossier IS the left page. A portrait click opens it on that speaker (NPC
         // dossier, or the player's own deeds for the player's portrait); clicking the SAME portrait again closes
         // it. The Classic skin keeps its separate left pop-out below.
         if (ChatManager.ActiveSkin == ChatManager.ManuscriptSkin)
         {
            string clickedKey = msg.PortraitKey ?? "";
            if (_isManuscriptLeftPageOpen && clickedKey == _manuscriptDossierKey)
            {
               IsManuscriptLeftPageOpen = false;

               return;
            }

            if (!PopulatePanelData(msg)) return; // a system / narration line has no portrait
            _manuscriptDossierKey = clickedKey;
            IsManuscriptLeftPageOpen = true;

            return;
         }

         if (!PopulatePanelData(msg)) return; // a system / narration line has no portrait: leave the panel closed

         if (_isComposeOpen) IsComposeOpen = false;
         if (_isMemoryPanelOpen) IsMemoryPanelOpen = false;

         IsPortraitPanelOpen = true;
      }

      /// <summary>
      ///   Fills every Panel* property (portrait + encyclopedia dossier) from a chat line's speaker and raises the
      ///   change notifications. Returns false for a system / narration line that carries no portrait, so the caller
      ///   can decline to open the Classic pop-out. Shared by the Classic portrait pop-out and the Manuscript page.
      /// </summary>
      private bool PopulatePanelData(ChatMessageViewModel msg)
      {
         if (msg == null) return false;

         PanelPortraitPath = "";
         ShowPanelCustomPortrait = false;
         ShowPanelVanillaPortrait = false;
         PanelPortraitId = "";
         PanelPortraitTextureProviderName = "";
         PanelPortraitAdditionalArgs = "";
         ResolvePanelInfo(msg);

         // Drawn at the largest size that fits the BOTTOM portrait region WITHOUT distortion, then centred: the
         // player's PNG may be any ratio (4:5, square, wide), so it must never be stretched. The facts block sits
         // above it, so the box is shorter than the panel (report layout: info at top, portrait at the bottom).
         const float boxW = 460f;
         const float boxH = 430f;
         var aspectW = 2f; // vanilla 3D fallback: the character tableau renders WIDE, 2:1 (the same aspect as the
         var aspectH = 1f; // 88x44 chat avatar), so a 2:1 box shows it without stretching (report: 1:1 stretched it)

         if (msg.ShowCustomPortrait && !string.IsNullOrEmpty(msg.PlayerPortraitPath))
         {
            PanelPortraitPath = msg.PlayerPortraitPath;
            ShowPanelCustomPortrait = true;
            if (RuntimePortraitFactory.TryGetDimensions(msg.PlayerPortraitPath, out int iw, out int ih) && iw > 0 && ih > 0)
            {
               aspectW = iw;
               aspectH = ih;
            }
         }
         else if (!string.IsNullOrEmpty(msg.PortraitId))
         {
            PanelPortraitId = msg.PortraitId;
            PanelPortraitTextureProviderName = msg.PortraitTextureProviderName;
            PanelPortraitAdditionalArgs = msg.PortraitAdditionalArgs;
            ShowPanelVanillaPortrait = true;
         }
         else
         {
            return false; // a system or narration line has no portrait to show
         }

         float scale = Math.Min(boxW / aspectW, boxH / aspectH);
         PanelPortraitWidth = aspectW * scale;
         PanelPortraitHeight = aspectH * scale;

         // Same aspect, fitted to the Manuscript left page's smaller portrait box.
         const float mBoxW = 230f;
         const float mBoxH = 272f;
         float mScale = Math.Min(mBoxW / aspectW, mBoxH / aspectH);
         ManuscriptPortraitWidth = aspectW * mScale;
         ManuscriptPortraitHeight = aspectH * mScale;

         PortraitPanelTitle = string.IsNullOrEmpty(msg.SpeakerName)
            ? "Portrait"
            : msg.SpeakerName;

         OnPropertyChanged(nameof(ManuscriptPortraitWidth));
         OnPropertyChanged(nameof(ManuscriptPortraitHeight));
         OnPropertyChanged(nameof(PanelPortraitWidth));
         OnPropertyChanged(nameof(PanelPortraitHeight));
         OnPropertyChanged(nameof(PanelPortraitPath));
         OnPropertyChanged(nameof(ShowPanelCustomPortrait));
         OnPropertyChanged(nameof(ShowPanelVanillaPortrait));
         OnPropertyChanged(nameof(PanelPortraitId));
         OnPropertyChanged(nameof(PanelPortraitTextureProviderName));
         OnPropertyChanged(nameof(PanelPortraitAdditionalArgs));
         OnPropertyChanged(nameof(ShowPanelInfo));
         OnPropertyChanged(nameof(ShowPanelBackground));
         OnPropertyChanged(nameof(ShowPanelCharacter));
         OnPropertyChanged(nameof(ShowPanelAppearance));
         OnPropertyChanged(nameof(ShowPanelDiscoveries));
         OnPropertyChanged(nameof(ShowPanelGrievances));
         OnPropertyChanged(nameof(PanelInfoFacts));
         OnPropertyChanged(nameof(PanelBackground));
         OnPropertyChanged(nameof(PanelCharacter));
         OnPropertyChanged(nameof(PanelAppearance));
         OnPropertyChanged(nameof(PanelInfoDiscoveries));
         OnPropertyChanged(nameof(PanelGrievances));

         return true;
      }

      /// <summary>
      ///   Pushes the typed text into the wrapped preview at most every <see cref="ComposePreviewThrottleTicks" />,
      ///   so a long message does not re-wrap on every keystroke (the FPS-drop report). Clearing (after Send) is
      ///   pushed immediately. Called each frame from <see cref="ChatScrollablePanel" />.
      /// </summary>
      public void PumpComposePreview()
      {
         if (!_composePreviewDirty) return;

         bool cleared = string.IsNullOrEmpty(_inputText);
         long now = DateTime.UtcNow.Ticks;

         if (!cleared && now - _lastComposePreviewTicks < ComposePreviewThrottleTicks) return;

         _lastComposePreviewTicks = now;
         _composePreviewDirty = false;
         ComposePreviewText = _inputText ?? "";
      }

      /// <summary>
      ///   Frees every line's character tableau on teardown so no GPU render-target survives the chat closing.
      ///   Releasing the movie already destroys the widgets (which frees their tableaus), but the guard clears
      ///   the ImageIds first, making the release explicit and independent of engine teardown timing — a hedge
      ///   against tableaus lingering across many open/close cycles in one session. Also detaches the per-insert
      ///   cap handler so nothing fires during teardown. Called by <see cref="ChatManager.Close" />.
      /// </summary>
      public void ReleaseAllPortraits()
      {
         _messages.ListChanged -= OnMessagesListChanged;
         PortraitTextureGuard.ReleaseAll(_messages);
      }

      public void SetNpcHero(Hero npcHero)
      {
         _npcHero = npcHero;
         OnPropertyChanged(nameof(IsEncyclopediaVisible)); // may be set after the prefab loads (captive scenes)
         OnPropertyChanged(nameof(IsMemoryToggleVisible)); // ditto, the memory button shares this population

         // A freshly-minted bandit boss (nemesis) may not have a renderable character tableau on the FIRST
         // encounter (a race: he is minted the same instant the captive scene fires). Keep his band's troop face
         // on hand so a blank Hero portrait can fall back to it, and his lines still carry an avatar instead of
         // running under the player's with no separator (a player-reported bug). Lords always render, so only a
         // bandit-clan captor needs this.
         if (_npcPortraitCharacter == null && npcHero?.Clan?.IsBanditFaction == true)
            _npcPortraitCharacter = npcHero.Clan.BasicTroop;

         if (!string.IsNullOrWhiteSpace(npcHero?.Name?.ToString())) _npcName = npcHero.Name.ToString();
         TitleText = $"Calradia Remembers — {_npcName}";
         _storyIntroWritten = false;
         _storyLocation = CaptiveSceneText.BuildStoryLocation();

         // Extension Surface: announce the conversation to third-party listeners now that the NPC is known and
         // before the first exchange, so a listener can inject relevant memories in time for the first prompt.
         // Once per conversation, and never for a nameless/null hero.
         if (!_conversationStartedRaised && npcHero?.StringId != null)
         {
            _conversationStartedRaised = true;
            CrConversation.RaiseStarted(npcHero.StringId, (int) CampaignTime.Now.ToDays);
         }
      }

      public void SetNpcName(string npcName)
      {
         if (string.IsNullOrWhiteSpace(npcName)) return;
         _npcName = npcName;
         TitleText = $"Calradia Remembers — {npcName}";
      }

      /// <summary>
      ///   Supplies a portrait for a speaker that has no <see cref="Hero" /> (a synthesized
      ///   bandit captor). The given troop's face is rendered for the captor's dialogue lines.
      /// </summary>
      public void SetNpcPortraitCharacter(CharacterObject portraitCharacter)
      {
         _npcPortraitCharacter = portraitCharacter;
      }

      /// <summary>Updates the header subtitle with the two relationship scores.</summary>
      public void SetRelationSummary(int personal, int clan)
      {
         RelationSummary = $"Clan {clan:+#;-#;0}   ·   Personal {personal:+#;-#;0}";
      }

      /// <summary>
      ///   Slides the chat window off the right edge (true) or back into place (false). Called by the focus
      ///   arbiter when a game screen opens over the chat, so the chat gets out of its way without being torn
      ///   down or hidden (the layer keeps ticking, so LLM turns and the arbiter keep running).
      /// </summary>
      public void SetVisualOffscreen(bool off)
      {
         // The narrow 1:1 chat window is anchored to the right, so +800 slides it fully off. The COUNCIL window
         // is FULL SCREEN WIDTH (player report: slid only 800, its left column still covered the pause menu's
         // buttons, so the player could not click Resume and was stuck). A full-width window needs a slide larger
         // than any screen, so it clears entirely; 6000 covers up to a 5K ultrawide.
         float slide = ChatManager.ActiveSkin == ChatManager.CouncilSkin ? 6000f : 800f;
         ChatVisualOffsetX = off
            ? slide
            : -30f;

         // The portrait dossier is anchored to the right of the screen (not inside the sliding window), so it
         // would sit ON TOP of whatever screen just took focus (report: it overlapped the encyclopedia). Close
         // it when the chat yields, so the player reopens it with a left-click when they return to the chat.
         if (off && _isPortraitPanelOpen) IsPortraitPanelOpen = false;
      }

      /// <summary>Replaces the full witness list (called by WitnessResolver at session open).</summary>
      public void SetWitnesses(List<WitnessEntry> witnesses)
      {
         _witnesses.Clear();
         _witnessColors.Clear();
         if (witnesses != null)
            foreach (WitnessEntry w in witnesses)
            {
               if (w == null || AlreadyPresent(w)) continue; // dedupe within the incoming set too
               _witnesses.Add(w);
               _witnessColors[w.Name] = ColorForWitness(w);
            }

         RefreshWitnessUi();
      }

      #region private

      /// <summary>A short, readable occupation label for the facts block (gender-aware for nobles).</summary>
      private static string DescribeOccupation(Hero hero)
      {
         if (hero.IsLord)
            return hero.IsFemale
               ? "Lady"
               : "Lord";
         if (hero.IsWanderer) return "Wanderer";
         if (hero.IsNotable) return "Notable";
         if (hero.IsMerchant) return "Merchant";
         if (hero.IsGangLeader) return "Gang leader";
         if (hero.IsRuralNotable) return "Rural notable";
         if (hero.IsArtisan) return "Artisan";
         if (hero.IsPreacher) return "Preacher";

         return hero.Occupation.ToString();
      }

      /// <summary>
      ///   The grievances the player has EARNED knowledge of against this NPC, one bullet per line, rendered
      ///   through the same pure narrator the encyclopedia's Grievances section uses. Empty when none are known.
      /// </summary>
      private static string ResolveKnownGrievances(Hero hero)
      {
         try
         {
            var behavior = GrudgeBehavior.Instance;

            if (behavior == null) return "";

            var today = (int) CampaignTime.Now.ToDays;
            var lines = new List<string>();
            foreach (Grudge grudge in behavior.KnownGrudgesFor(hero.StringId, today))
            {
               bool targetIsPlayer = grudge.TargetId == Hero.MainHero?.StringId;
               string targetName = targetIsPlayer
                  ? null
                  : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == grudge.TargetId)?.Name?.ToString();

               TraitFacts traits = GrudgeBridge.BuildTraitFacts(hero);
               int mag = GrudgePolicy.EffectiveMagnitude(grudge, today, traits);
               GrudgeResolutionKind kind =
                  GrudgePolicy.ResolutionKindFor(grudge.Source, grudge.Rigidity);

               string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer, targetName, mag, kind);
               if (!string.IsNullOrEmpty(line)) lines.Add("• " + line);
            }

            return string.Join("\n", lines);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PORTRAIT] grievances resolve failed: {ex.GetType().Name}: {ex.Message}");

            return "";
         }
      }

      /// <summary>Runs a possibly-throwing read (engine text lookups) and returns "" on any failure.</summary>
      private static string SafeRead(Func<string> read)
      {
         try { return read() ?? ""; }
         catch { return ""; }
      }

      /// <summary>Strips angle-bracket markup (encyclopedia hyperlink tags), leaving the plain display text.</summary>
      private static string StripMarkup(string s)
         => string.IsNullOrEmpty(s)
            ? s
            : Regex.Replace(s, "<[^>]+>", "").Trim();

      /// <summary>
      ///   How many witnesses are the captor's MEN — the ones who can take an aggressor turn and who form the
      ///   shaming audience. Excludes the bound victim companion: they are present and voiced, but they are the
      ///   target, never an aggressor or part of the crowd that humiliates the player.
      /// </summary>
      private int CountAggressorWitnesses()
         => _witnesses?.Count(w => w != null && !w.IsCaptiveVictim) ?? 0;

      // ── Grudges pillar, Tier 2: CaptiveAbuse (the two live plant sites) ────

      /// <summary>
      ///   Grudges pillar (Tier 2, CaptiveAbuse): the companion-victim revenge driver. Fires once per
      ///   scene, only when this encounter targeted a held companion (<see cref="_captiveVictimName" />,
      ///   set by <see cref="ActivateCaptiveEncounterMode" />). The victim's real Hero is resolved from
      ///   the witness entry <see cref="ChatManager.OpenForCaptiveEncounter" /> adds for them
      ///   (<see cref="WitnessEntry.IsCaptiveVictim" />, carrying their <see cref="WitnessEntry.HeroStringId" />)
      ///   — the only place their Hero identity survives the scene, since the view model itself only
      ///   threads their NAME through. <see cref="CaptiveAbuseFacts.PlayerWasThePerpetrator" /> mirrors
      ///   <see cref="_captivePlayerCoerced" />: true only when the captor forced the PLAYER to act
      ///   (<see cref="CompanionSceneKind.PlayerCoerced" />); otherwise the captor did it themselves, but
      ///   the companion still nurses a grudge at the scene's severity for the player having let it
      ///   happen unpunished. Called from <see cref="FinalizeCaptiveSceneMemory" /> — independent of that
      ///   method's own captor-memory writes below it, so an ephemeral bandit captor never suppresses it.
      /// </summary>
      /// <summary>
      ///   Companion-victim captive scene (Phase 3): the full mechanical aftermath, once when the scene closes.
      ///   The tormented COMPANION (1) resents the PLAYER for coercing them or failing to protect them (the
      ///   existing CaptiveAbuse grudge), (2) has the bond with the player SCARRED by grief (a stance crater),
      ///   (3) holds a REVENGE grudge against a NAMED captor (a nemesis/lord, never a faceless ephemeral bandit),
      ///   and the atrocity is (4) recorded as a world-event, the player's chronicled revenge driver. No-op
      ///   outside a companion-victim scene, or when the scene's intent was not abusive (severity 0).
      /// </summary>
      private void FinalizeCompanionVictimConsequence()
      {
         if (_captiveVictimName == null || Hero.MainHero == null) return;

         string victimId = _witnesses.FirstOrDefault(w => w != null && w.IsCaptiveVictim)?.HeroStringId;
         Hero victim = string.IsNullOrEmpty(victimId)
            ? null
            : Hero.FindFirst(h => h != null && h.StringId == victimId);

         if (victim == null) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         int severity = CaptiveIntentRules.CaptiveAbuseSeverity(_captiveEncounterIntent);
         int today = behavior?.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays;

         // (1) The companion resents the PLAYER (coerced to hurt them, or failed to protect them).
         var playerFacts = new CaptiveAbuseFacts {Severity = severity, PlayerWasThePerpetrator = _captivePlayerCoerced};
         GrudgeProposal playerProposal = Tier2GrudgeRules.CaptiveAbuseGrudge(playerFacts);
         if (playerProposal.Forms)
         {
            string cause = _captivePlayerCoerced
               ? "The player was forced to hurt me with their own hands while I was helpless."
               : "The player let me be broken before them and did nothing to stop it.";
            GrudgeBehavior.Instance?.TryPlant(victim, Hero.MainHero, GrudgeSource.CaptiveAbuse, cause,
               playerProposal.Magnitude, playerProposal.KnownToPlayer, today);
         }

         // (2)-(4) Grief + revenge + chronicle. A NAMED captor (a persistent Hero, not a faceless bandit) earns
         // the lasting revenge grudge; a faceless brigand is ephemeral and earns none.
         Hero captor = _captiveEncounterIsBandit
            ? null
            : _npcHero;
         CompanionAbuseConsequence c = CompanionAbuseConsequencePolicy.Resolve(severity, _captivePlayerCoerced, captor != null);

         if (!c.Forms) return;

         // (2) Grief: the ordeal scars the companion's bond with the player who could not save them.
         NpcProfile victimProfile = behavior?.Store?.Get(victim.StringId);
         if (victimProfile != null)
         {
            StanceService.ApplyDeed(victimProfile, StanceAxis.Trust, c.BondTrustCrater);
            StanceService.ApplyDeed(victimProfile, StanceAxis.Affection, c.BondAffectionCrater);
            behavior.Store.Set(victimProfile);
            ModLog.Info($"[COMPANION SCENE] grief: {victim.Name}'s bond with the player scarred (Trust {c.BondTrustCrater}, Affection {c.BondAffectionCrater}).");
         }

         // (3) Revenge: the companion joins the vendetta against the named captor.
         if (captor != null && c.CaptorRevengeMagnitude > 0)
            GrudgeBehavior.Instance?.TryPlant(victim, captor, GrudgeSource.CaptorCruelty,
               $"{captor.Name} tormented me while I was helpless, and made the one I served watch. I will see them answer for it.",
               c.CaptorRevengeMagnitude, true, today);

         // (4) The atrocity, recorded as the player's chronicled revenge driver.
         RecordCompanionAbuseWorldEvent(victim, captor, c.WorldEventMagnitude, today);
      }

      /// <summary>
      ///   Marks that an NPC-led OPENER is now generating (a captive scene / companion audience where the NPC
      ///   speaks first), so the chat input stays hidden until the introduction is delivered. Cleared in the
      ///   RunLlmExchangeAsync finally when that turn completes.
      /// </summary>
      private void MarkOpenerPending()
      {
         _awaitingOpener = true;
         OnPropertyChanged(nameof(IsChatInputVisible));
         OnPropertyChanged(nameof(IsComposeToggleVisible));
      }

      /// <summary>
      ///   True when the NPC in this conversation is the player's own captive. Such an NPC has no say over who
      ///   stays in the room, so a private audience is granted by force rather than left to the LLM to refuse.
      /// </summary>
      private bool NpcIsPlayerPrisoner()
         => _npcHero is {IsPrisoner: true} && _npcHero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

      /// <summary>
      ///   True when the PLAYER'S captive scene is physically happening at sea. Mirrors
      ///   <c>EncounterContextBuilder.ResolveCaptiveSceneAtSea</c> (same verdict must reach both the
      ///   scene-flavour text built here and the SDK's <see cref="EncounterContext.CaptiveSceneAtSea" />, or
      ///   the opener and the system prompt could disagree). <c>MobileParty.MainParty.IsCurrentlyAtSea</c>
      ///   cannot answer this once the player is a captive (their own party is gone), so this checks the
      ///   party actually HOLDING them captive, and the captor's own live party as a fallback.
      /// </summary>
      private bool ResolveCaptiveSceneAtSea()
      {
         try
         {
            bool holdingPartyAtSea = Hero.MainHero?.PartyBelongedToAsPrisoner?.MobileParty?.IsCurrentlyAtSea == true;
            bool captorPartyAtSea = _npcHero?.PartyBelongedTo?.IsCurrentlyAtSea == true;

            return holdingPartyAtSea || captorPartyAtSea;
         }
         catch { return false; }
      }

      /// <summary>
      ///   On each inserted line, keep VRAM bounded by the number of participants (not the transcript length):
      ///   the guard collapses the transcript to at most one live character tableau per distinct speaker, freeing
      ///   older duplicates. Only the render-target is freed; each line's text stays.
      /// </summary>
      private void OnMessagesListChanged(object sender, ListChangedEventArgs e)
      {
         if (EnforcePortraitCapInChat && e.ListChangedType == ListChangedType.ItemAdded)
            PortraitTextureGuard.EnforceOnePerParticipant(_messages);
      }

      /// <summary>
      ///   Chronicle pillar: a captive lord or lady of standing taken in an intimate captor scene earns
      ///   a chronicle line, via the pure <see cref="IntimateChroniclePolicy" /> (gated on
      ///   <see cref="ModSettings.AdultContentLevel" />; a tasteful line at Mature/Explicit, bawdier at
      ///   Hardcore, nothing at Off). Never a random bandit-camp captive (<c>prisoner.IsLord</c> gates
      ///   it), and only when the scene's intent was actually intimate (reuses
      ///   <see cref="CaptiveIntentRules.IsForcedSexual" /> as the "this was an intimate scene" signal —
      ///   the same classification already used for the player-as-captive body toll, applied here in the
      ///   reverse captor direction). Deduplicated per unique captive so repeat scenes with the same
      ///   prisoner do not bloat the chronicle. Never throws — a flavour line must never risk a save.
      /// </summary>
      private void RecordCaptiveConquestChronicleIfNeeded(Hero prisoner)
      {
         try
         {
            if (prisoner == null || Hero.MainHero == null) return;
            if (!prisoner.IsLord) return;
            if (!CaptiveIntentRules.IsForcedSexual(_captorSceneIntent)) return;

            var store = WorldEventStore.Instance;

            if (store == null) return;

            bool already = store.GetPlayerChronicle()
                                .Any(e => e.Status == "captive_conquest" && e.ActorIds != null && e.ActorIds.Contains(prisoner.StringId));

            if (already) return;

            string line = IntimateChroniclePolicy.BuildLine(
               ModSettings.AdultContentLevel, true, IntimateChronicleSceneKind.CaptiveConquest,
               Hero.MainHero.Name?.ToString(), prisoner.Name?.ToString(), prisoner.IsFemale);

            if (string.IsNullOrWhiteSpace(line)) return;

            var factionIds = new List<string>();
            if (Hero.MainHero.MapFaction != null) factionIds.Add(Hero.MainHero.MapFaction.StringId);
            if (prisoner.MapFaction != null) factionIds.Add(prisoner.MapFaction.StringId);

            store.Record(new WorldEvent {
               Type = WorldEventType.PlayerFeat,
               Day = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.GetCurrentGameDay() ?? (int) CampaignTime.Now.ToDays,
               Magnitude = 40,
               InvolvesPlayer = true,
               Status = "captive_conquest",
               Summary = $"{Hero.MainHero.Name} took {prisoner.Name} as a captive lover.",
               ActorIds = new List<string> {Hero.MainHero.StringId, prisoner.StringId},
               FactionIds = factionIds,
               ChronicleLine = line
            });
            ModLog.Info($"[CHRONICLE] captive conquest recorded for {prisoner.Name}.");
         }
         catch (Exception ex) { ModLog.Error($"[CHRONICLE] captive conquest feeder failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Records the companion-abuse atrocity into the world-event store: the player's lasting grievance against the
      ///   captor. Never throws.
      /// </summary>
      private void RecordCompanionAbuseWorldEvent(Hero victim, Hero captor, int magnitude, int today)
      {
         try
         {
            var store = WorldEventStore.Instance;

            if (store == null || victim == null || Hero.MainHero == null) return;

            string captorName = captor?.Name?.ToString() ?? _npcName ?? "the captor";
            string playerName = Hero.MainHero.Name?.ToString() ?? "the player";

            var actorIds = new List<string>();
            if (captor?.StringId != null) actorIds.Add(captor.StringId);
            actorIds.Add(victim.StringId);
            if (Hero.MainHero.StringId != null) actorIds.Add(Hero.MainHero.StringId);

            store.Record(new WorldEvent {
               Type = WorldEventType.Other,
               Day = today,
               Magnitude = magnitude,
               InvolvesPlayer = true,
               Summary = $"{captorName} broke {victim.Name} before {playerName}'s eyes, and {playerName} could not stop it.",
               ChronicleLine = $"I was made to watch {captorName} break {victim.Name}, and could do nothing, a debt I have not forgotten",
               ActorIds = actorIds,
               FactionIds = Hero.MainHero.MapFaction != null
                  ? new List<string> {Hero.MainHero.MapFaction.StringId}
                  : new List<string>()
            });
            ModLog.Info($"[COMPANION SCENE] atrocity recorded: {captorName} vs {victim.Name}.");
         }
         catch (Exception ex) { ModLog.Error($"[COMPANION SCENE] world-event record failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Rebuilds the shared-memories list from the current NPC's persisted profile: a read-only mirror of
      ///   cr.memory, oldest first, so the panel reads like a journal. Called each time the panel opens so it is
      ///   always current. An NPC with no memory (or a heroless speaker with no persistent profile) shows a
      ///   single gentle placeholder. Never throws; a reference panel must not risk the conversation.
      /// </summary>
      private void RefreshMemories()
      {
         try
         {
            _memories.Clear();

            NpcProfile profile = MainSpeaker().Profile;
            MemoryPanelTitle = _npcHero != null
               ? $"What {_npcName} remembers"
               : "Shared memories";

            if (profile?.Events == null || profile.Events.Count == 0)
            {
               _memories.Add(new MemoryEntryViewModel(
                  "", "No shared memories yet. They begin to form as the two of you speak and act together."));

               return;
            }

            var today = (int) CampaignTime.Now.ToDays;
            foreach (NotableEvent e in profile.Events)
            {
               string meta = MemoryDayFormatter.Describe(e.gameDay, today);
               if (e.type != NotableEventType.Other) meta += $"   ·   {e.type}";
               _memories.Add(new MemoryEntryViewModel(meta, e.summary));
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"RefreshMemories failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Fills the portrait panel's encyclopedia-style facts from the clicked line's speaker. Shown only for a
      ///   living NPC hero we can resolve (never for the player's own line or a heroless bandit face): culture,
      ///   age, occupation, the vanilla clan relation, personal regard (descriptor + score), and every trait or
      ///   kink the player has discovered about them. Any resolution gap simply hides the facts block.
      /// </summary>
      private void ResolvePanelInfo(ChatMessageViewModel msg)
      {
         ShowPanelInfo = false;
         ShowPanelBackground = false;
         ShowPanelCharacter = false;
         ShowPanelAppearance = false;
         ShowPanelDiscoveries = false;
         ShowPanelGrievances = false;
         PanelInfoFacts = "";
         PanelBackground = "";
         PanelCharacter = "";
         PanelAppearance = "";
         PanelInfoDiscoveries = "";
         PanelGrievances = "";

         string key = msg.PortraitKey;

         if (string.IsNullOrEmpty(key)) return;

         Hero hero = Hero.FindFirst(h => h.StringId == key);

         if (hero == null) return;
         if (hero == Hero.MainHero) // the player's own portrait: show THEIR panel (identity + their notable deeds)
         {
            ResolvePlayerPanelInfo(hero);

            return;
         }

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId);

         // Inline facts, one "Label: value" per line (no table alignment, per the design note).
         int regard = profile?.ReputationWithPlayer ?? 0;
         var facts = new StringBuilder();
         facts.Append("Culture: ").Append(hero.Culture?.Name?.ToString() ?? "Unknown").Append('\n');
         facts.Append("Age: ").Append((int) hero.Age).Append('\n');
         facts.Append("Role: ").Append(DescribeOccupation(hero)).Append('\n');
         facts.Append("Relation: ").Append(hero.GetRelationWithPlayer().ToString("+#;-#;0")).Append('\n');
         facts.Append("Personal regard: ").Append($"{ThirdPartyDescriptor.DescribeRelation(regard)} ({regard:+#;-#;0})");
         PanelInfoFacts = facts.ToString();

         // Vanilla encyclopedia background (the "Mina is a member of the Pethros…" paragraph). Hero.EncyclopediaText
         // is populated lazily by the encyclopedia manager, so it is empty when read cold from the chat: the static
         // generator builds it on demand. Strip the encyclopedia hyperlink markup (names become plain text).
         PanelBackground = StripMarkup(SafeRead(() => Hero.SetHeroEncyclopediaTextAndLinks(hero)?.ToString()));
         ShowPanelBackground = !string.IsNullOrWhiteSpace(PanelBackground);

         // The mod's authored backstory: from the override files, or the value synced onto the profile.
         PanelCharacter = SafeRead(() => CharacterOverrideLoader.BackstoryFor(hero));
         if (string.IsNullOrWhiteSpace(PanelCharacter)) PanelCharacter = profile?.AuthoredBackstory ?? "";
         ShowPanelCharacter = !string.IsNullOrWhiteSpace(PanelCharacter);

         // Physical description from body/appearance stats.
         PanelAppearance = SafeRead(() => AppearanceProseBehavior.Instance?.Get(hero.StringId));
         ShowPanelAppearance = !string.IsNullOrWhiteSpace(PanelAppearance);

         if (profile?.DiscoveredTraits != null && profile.DiscoveredTraits.Count > 0)
         {
            PanelInfoDiscoveries = string.Join("\n",
               profile.DiscoveredTraits.Select(t => "• " + t.Description));
            ShowPanelDiscoveries = PanelInfoDiscoveries.Length > 0;
         }

         PanelGrievances = ResolveKnownGrievances(hero);
         ShowPanelGrievances = !string.IsNullOrEmpty(PanelGrievances);

         ShowPanelInfo = true;
      }

      /// <summary>
      ///   The PLAYER's own info panel (their portrait was clicked): a short identity line plus their notable
      ///   DEEDS, read from the world chronicle (WorldEventStore, the same source the Chronicle window uses),
      ///   most recent first and capped so they fit. Any gap simply leaves that section hidden; never throws.
      /// </summary>
      private void ResolvePlayerPanelInfo(Hero player)
      {
         try
         {
            var facts = new StringBuilder();
            facts.Append("Culture: ").Append(player.Culture?.Name?.ToString() ?? "Unknown").Append('\n');
            facts.Append("Age: ").Append((int) player.Age).Append('\n');
            facts.Append("Clan: ").Append(player.Clan?.Name?.ToString() ?? "None").Append('\n');
            facts.Append("Renown: ").Append((int) (player.Clan?.Renown ?? 0));
            PanelInfoFacts = facts.ToString();
            ShowPanelInfo = true;

            IReadOnlyList<WorldEvent> deeds = WorldEventStore.Instance?.GetPlayerChronicle();
            if (deeds != null && deeds.Count > 0)
            {
               var today = (int) CampaignTime.Now.ToDays;
               var sb = new StringBuilder();
               foreach (WorldEvent e in deeds.Reverse().Take(8)) // most recent first, capped to what the page can hold
               {
                  string text = string.IsNullOrWhiteSpace(e.ChronicleLine) ? e.Summary : e.ChronicleLine;
                  if (string.IsNullOrWhiteSpace(text)) continue;
                  sb.Append("• ").Append(text).Append("  (").Append(MemoryDayFormatter.Describe(e.Day, today)).Append(")\n");
               }

               PanelBackground = sb.ToString().TrimEnd();
               ShowPanelBackground = !string.IsNullOrWhiteSpace(PanelBackground);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"ResolvePlayerPanelInfo failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }
}