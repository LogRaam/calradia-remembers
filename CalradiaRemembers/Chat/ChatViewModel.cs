// Code written by Gabriel Mailhot, 24/06/2026.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Items;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel : ViewModel
   {
      // Externalized scene STRUCTURE: a director walks an explicit stage machine (Intro → Initiate
      // → Intensify → Climax → Conclude, plus collective sub-acts) and feeds the model ONE named
      // beat per turn with a reminder of what is already done. This is what stops the model looping
      // a beat or cramming the arc. A paranoia beat cap force-ends a scene that somehow runs away.
      private const int CaptiveBeatSafetyCap = 16;

      // Cream color matching ChatMessageViewModel's default NPC hue, used when the
      // main NPC appears as a reacting witness (while another participant speaks).
      private const string NpcCreamColor = "#e8d8b8FF";
      private const string PlaceholderText = "(considering...)";

      // Scene narration (second-person prose during captive/CNC scenes): pale violet,
      // distinct from dialogue, no portrait — reads like a novel's narration.
      private const string SceneNarrationColor = "#c0a8d0FF";

      // System notification line colors (relation/gold outcomes shown inline in chat).
      private const string SystemGainColor = "#9fe0a0FF"; // soft green — favorable
      private const string SystemLossColor = "#e0a0a0FF"; // soft red — unfavorable

      // ── Sprint 15C: NPC→witness auto-exchange ─────────────────────────────
      // Maximum NPC→witness auto-exchanges per player turn. Each exchange is one
      // LLM call; the chain terminates early if witnesses fall silent.
      private const int WitnessExchangeCap = 3;

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

      // Last reaction text shown per witness (canonical name → text). Used to drop a witness's
      // verbatim-repeated line: weaker models copy their own [WITNESS_REACTION] turn after turn,
      // which soft prompt rules do not stop — so we filter the duplicate mechanically.
      private readonly Dictionary<string, string> _lastWitnessReaction = new(StringComparer.OrdinalIgnoreCase);

      // Attributed conversation history — the single source of truth for rebuilding
      // each speaker's ChatSession per turn (their own lines as assistant, everyone
      // else's as "[Name]: …" user lines). Kept in lockstep with the displayed
      // conversational messages (player, main NPC, witnesses); system lines excluded.
      private readonly List<TranscriptEntry> _transcript = new();
      private readonly Dictionary<string, string> _witnessColors = new();

      // ── Multi-NPC witnesses ───────────────────────────────────────────────
      // Mutable list — authoritative for the session; cleared on privacy-accepted.
      // Passed into EncounterContext each turn so PromptBuilder can inject them.
      private readonly List<WitnessEntry> _witnesses = new();

      // Portrait troop per heroless (synthetic bandit) witness, so the chief's men show distinct
      // faces instead of a blank avatar. Keyed by witness name.
      private readonly Dictionary<string, CharacterObject> _witnessPortraits = new(StringComparer.OrdinalIgnoreCase);

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

      // ── Player-as-captor scene (the inverse): the NPC is the player's prisoner and the player drives. A normal
      //    live conversation (full memory + actions), flagged so the prompt frames the NPC as the captive. ──
      private bool _captorScene;
      private CaptiveSceneIntent _captorSceneIntent;
      private NpcProfile _captorSceneProfile;

      private string _closeButtonText = "End conversation";

      // ── Commoner mode payload (v1.1) ──────────────────────────────────────
      // Synthetic profile + settlement snapshot created per session; never stored.
      private NpcProfile _commonerProfile;
      private ChatSession _commonerSession;
      private CommonsKnowledge _commonsKnowledge;

      // Set when the NPC emits an end_conversation action — locks input so the
      // player can only read the farewell and leave.
      private bool _conversationEnded;
      private bool _conversationMemoryFinalized; // idempotency guard
      private bool _witnessMemoryFinalized; // idempotency guard for the per-witness "I was present" memory

      // The witness the player is currently addressing (sticky). Null = main NPC.
      private string _currentSpeakerHeroId;
      private CaptiveSceneDirector _director;

      // ── Fallback conversation memory ──────────────────────────────────────
      // The LLM mostly emits [EVENT] on a natural farewell; if the player closes
      // the window mid-exchange, nothing was recorded and the NPC forgets the
      // conversation ever happened (tester report). These track whether the
      // fallback summary is needed when the chat closes.
      private bool _eventRecordedThisSession; // any non-empty [EVENT] this session
      private bool _hasWitnesses;
      private string _inputText = "";
      private bool _isPleaseContinueVisible;
      private bool _isPrivateRequestVisible;

      // Pause state — stored context so ExecuteContinue can resume without parameters.
      private bool _isScenePaused;
      private bool _isSendEnabled = true;
      private MBBindingList<ChatMessageViewModel> _messages;

      private ChatMode _mode = ChatMode.Conversation;
      private Hero _npcHero;
      private string _npcName = "the lord";

      // Portrait fallback for a heroless speaker (a synthesized bandit captor): the face of a
      // real bandit troop stands in for the brigand chief, who has no Hero of their own.
      private CharacterObject _npcPortraitCharacter;

      // True while RunNpcWitnessExchangeAsync is in flight; prevents the
      // RunLlmExchangeAsync finally block from re-enabling input prematurely.
      private bool _npcWitnessExchangePending;

      // StringId of the item the player picked from the bag this turn — give_item transfers exactly
      // this, so a freely-given gift never depends on the LLM echoing the item's display name back.
      private string _offeredItemId;

      private int _playerTurnsThisSession; // completed player→NPC exchanges

      // Set when the player clicks "Request Private" — injected once, then cleared.
      private bool _privacyRequestPending;

      // Set once the request is used (accepted or refused) — button never reappears.
      private bool _privacyRequestUsed;
      private string _relationSummary = "";
      private bool _sceneContinuationPending;
      private int _sceneContinuations;
      private NpcMemoryCampaignBehavior _scenePauseBehavior;
      private ChatSpeaker _scenePauseSpeaker;
      private string _sendButtonText = "Send";

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

      public ChatViewModel()
      {
         _messages = new MBBindingList<ChatMessageViewModel>();
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
         }
      }

      /// <summary>Inverse of <see cref="IsScenePaused" /> — drives the normal InputBox + SendButton.</summary>
      [DataSourceProperty]
      public bool IsNormalInputVisible => !_isScenePaused;

      /// <summary>
      ///   True in plain Conversation mode while the exchange is still open — hides
      ///   the "Offer item" button in letter/captive modes and after the NPC says goodbye.
      /// </summary>
      [DataSourceProperty]
      public bool IsOfferItemVisible => _mode == ChatMode.Conversation && !_conversationEnded;

      /// <summary>
      ///   Always hidden now: gold is folded into the unified "give" control (the gift button),
      ///   so the separate "Give Gold" button is retired. Kept for the existing XML binding.
      /// </summary>
      [DataSourceProperty]
      public bool IsOfferGoldVisible => false;

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

      public Action OnCloseRequested { get; set; }

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
         bool captorIsBandit = false)
      {
         _mode = ChatMode.CaptiveEncounter;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         _captiveEncounterProfile = captorProfile;
         _captiveEncounterIsCollective = isCollective;
         _captiveEncounterIsBandit = captorIsBandit;
         _captiveEncounterIntent = intent;
         _captiveBeatCount = 0;
         // Build the scene's structure now: the lead's full arc, plus collective sub-acts when
         // others are present. extraAggressors = the witnesses who can take a turn.
         _director = new CaptiveSceneDirector(isCollective, _witnesses?.Count ?? 0, MBRandomSource.Instance);

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
         bool nonSexual = intent is CaptiveSceneIntent.Reckoning
            or CaptiveSceneIntent.Extortion or CaptiveSceneIntent.Intimidation or CaptiveSceneIntent.Revenge;
         string opener = CaptiveSceneText.BuildCaptiveOpener(isCollective, intent)
                       + (nonSexual ? "" : CaptiveSceneText.BuildSceneSetup());
         _ = RunLlmExchangeAsync(opener, captor, placeholder, true);
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
      ///   Opens the chat as a PLAYER-AS-CAPTOR scene: the NPC is the player's prisoner and the player drives.
      ///   Runs as a normal live conversation (full memory + actions), flagged so the prompt frames the NPC as the
      ///   captive reacting to the player. The player speaks first; there is no NPC-led opener.
      /// </summary>
      public void ActivateCaptorSceneMode(NpcProfile prisonerProfile, CaptiveSceneIntent intent)
      {
         _mode = ChatMode.Conversation;
         _captorScene = true;
         _captorSceneProfile = prisonerProfile;
         _captorSceneIntent = intent;

         TitleText = $"Calradia Remembers — {prisonerProfile?.Name ?? "your prisoner"}";
         CloseButtonText = "End";
         SendButtonText = "Send";
      }

      /// <summary>Adds a single witness to the session. Used by the console command for testing.</summary>
      public void AddWitness(WitnessEntry entry)
      {
         if (entry == null) return;
         _witnesses.Add(entry);
         _witnessColors[entry.Name] = ColorForWitness(entry);
         RefreshWitnessUi();
         ModLog.Info($"[WITNESSES] added: {entry.Name} ({entry.RelationToNpc}) color={_witnessColors[entry.Name]}");
      }

      /// <summary>
      ///   Displays a scene-narration line (second-person prose, no portrait) and mirrors
      ///   it to the story log and attributed transcript. Used for [NARRATION] blocks in
      ///   captive/CNC scenes. Recorded as a "Scene" statement so the speaker's next
      ///   rebuilt session sees what physically happened.
      /// </summary>
      public void AppendNarration(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         try
         {
            _messages.Add(new ChatMessageViewModel(text, SceneNarrationColor));
            ChatManager.ScrollToBottom();
            StoryLog.WriteNarration(text);
            if (IsLiveConversation)
               RecordTranscript(false, "Scene", null, text);
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
            // A heroless captor (synthesized bandit) falls back to a bandit-troop portrait.
            _messages.Add(_npcHero?.CharacterObject == null && _npcPortraitCharacter != null
               ? new ChatMessageViewModel(_npcPortraitCharacter, _npcName, text, false)
               : new ChatMessageViewModel(_npcHero, _npcName, text, false));
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
      ///   Opens the item picker so the player can select something from the party
      ///   inventory to offer. On selection the item name and value are pre-filled
      ///   into the chat input — the player can then edit and send as usual.
      /// </summary>
      public void ExecuteOfferItem()
      {
         if (_mode != ChatMode.Conversation || _conversationEnded) return;

         // When this NPC is awaiting payment-in-kind for a bargain, the bag settles it:
         // multi-select goods, the bridge enforces the value and honors the favor.
         InformalQuest bargain = FindOutstandingItemBargain();
         if (bargain != null)
         {
            ItemPickerService.ShowBargainPicker(
               bargain.RequiredItemValue, offers => SettleItemBargain(bargain, offers));

            return;
         }

         // Otherwise the bag is a spontaneous gift: one unified control gives gold OR an item.
         // Remember the exact item the player picked so give_item transfers THAT item by id,
         // rather than re-matching whatever name the LLM echoes back (which fails on any rewording).
         ItemPickerService.ShowItemPicker(
            (itemId, name, value) =>
            {
               _offeredItemId = itemId;
               InputText = $"I would like to offer you {name} (worth {value:N0} denars).";
               ModLog.Info($"[OFFER] player pre-filled offer: {name} ({value} denars, id={itemId}).");
            },
            PromptGoldOffer);
      }

      /// <summary>
      ///   Legacy entry point kept for the old "Give Gold" binding: routes to the same gold
      ///   prompt the unified give control now uses.
      /// </summary>
      public void ExecuteOfferGold()
      {
         if (_mode != ChatMode.Conversation || _conversationEnded) return;
         PromptGoldOffer();
      }

      /// <summary>
      ///   Asks the player how many denars to give, then pre-fills the chat input so the
      ///   NPC can respond with take_gold — which transfers the gold and stamps any outstanding
      ///   ProvideGold quest as satisfied (any amount counts).
      /// </summary>
      private void PromptGoldOffer()
      {
         int carried = Hero.MainHero?.Gold ?? 0;
         InformationManager.ShowTextInquiry(new TextInquiryData(
            "Give Gold",
            $"How many denars? (You carry {carried:N0}.)",
            true, true,
            "Give", "Cancel",
            amountText =>
            {
               if (!int.TryParse(amountText?.Trim(), out int amount) || amount <= 0) return;
               amount = Math.Min(amount, carried);
               InputText = $"I would like to give you {amount:N0} denars.";
               ModLog.Info($"[OFFER GOLD] pre-filled: {amount} denars.");
            },
            null));
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
         _director = new CaptiveSceneDirector(_captiveEncounterIsCollective, _witnesses?.Count ?? 0, MBRandomSource.Instance);
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

         string opener = CaptiveSceneText.BuildResumeOpener() + CaptiveSceneText.BuildResumeSetup();
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
         _privacyRequestPending = true;
         _privacyRequestUsed = true;
         RefreshWitnessUi();
         ModLog.Info("[WITNESSES] player requested private audience.");
         // Trigger immediate exchange; _privacyRequestPending is already true so
         // EncounterContextBuilder will inject the privacy request into the prompt.
         InputText = "I would speak with you privately.";
         ExecuteSendMessage();
      }

      public void ExecuteSendMessage()
      {
         string text = (InputText ?? "").Trim();

         if (string.IsNullOrEmpty(text)) return;
         if (!IsSendEnabled) return;

         // Sprint 15B: route this turn to whoever the player is addressing — the main
         // NPC by default, or a witness named in a stage direction / vocative (sticky).
         ChatSpeaker speaker = ResolveAddressedSpeaker(text);
         RefreshWitnessUi(); // refresh the "Speaking to …" indicator

         // Display only — the player line is committed to the transcript after the call
         // so BuildSessionFor doesn't double-count it (ChatAsync adds it itself).
         AppendPlayerMessage(text, false);
         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;
         InputText = "";
         _witnessExchangesThisRound = 0; // reset the 15C cap for this new player turn
         _sceneContinuations = 0; // reset scene-continuation count for this new player turn
         _ = RunLlmExchangeAsync(text, speaker, placeholder);
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

         // A committed escape is applied now, as the scene closes — deferred from the moment it
         // was rolled so freeing the player never popped an encounter over the open scene window.
         behavior?.GameStateBridge?.FinalizePendingEscape();

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

      // ── Fallback conversation memory (tester report: memories only "clicked"
      //    when the NPC formally ended the conversation) ──────────────────────

      /// <summary>
      ///   Ensures an ordinary conversation leaves a memory even when the LLM never
      ///   emitted an [EVENT] block (player closed the window mid-exchange). Called
      ///   once from <see cref="ChatManager.Close" />. Same two-stage pattern as
      ///   <see cref="FinalizeCaptiveSceneMemory" />: a plain base event recorded
      ///   synchronously, replaced by a richer first-person transcript summary when
      ///   the async call succeeds. No-op when an [EVENT] was already recorded, the
      ///   exchange had no substance (fewer than two completed player turns), or the
      ///   chat is not in plain Conversation mode (letters keep their own records;
      ///   captive scenes have their own finalizer).
      /// </summary>
      public void FinalizeConversationMemory()
      {
         if (_conversationMemoryFinalized || _mode != ChatMode.Conversation) return;
         _conversationMemoryFinalized = true;

         if (_eventRecordedThisSession || _playerTurnsThisSession < 2) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.DialogFlow?.Profile;

         if (behavior?.Store == null || profile == null) return;

         int gameDay = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string player = Hero.MainHero?.Name?.ToString() ?? "the player";

         // Stage 1 — synchronous base capture (guaranteed even if the async summary
         // never returns). Plain but truthful; better than the NPC forgetting.
         var baseEvent = new NotableEvent(gameDay, NotableEventType.Other,
            $"Spoke with {player}.");
         profile.Events.Add(baseEvent);
         behavior.Store.Set(profile);
         ModLog.Info($"[CONV MEMORY] fallback event recorded for {profile.Name} " + $"({_playerTurnsThisSession} player turn(s), no [EVENT] emitted).");

         // Stage 2 — async rich summary (fire-and-forget; replaces the base on success).
         string transcript = BuildSceneTranscriptText(player);

         if (string.IsNullOrWhiteSpace(transcript) || behavior.LlmServices?.LlmClient == null) return;

         _ = ConversationSummaryService.GenerateAndApplyConversationSummaryAsync(behavior, profile.Id, baseEvent, transcript, player);
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

      public void SetNpcHero(Hero npcHero)
      {
         _npcHero = npcHero;
         if (!string.IsNullOrWhiteSpace(npcHero?.Name?.ToString())) _npcName = npcHero.Name.ToString();
         TitleText = $"Calradia Remembers — {_npcName}";
         _storyIntroWritten = false;
         _storyLocation = CaptiveSceneText.BuildStoryLocation();
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

      /// <summary>Replaces the full witness list (called by WitnessResolver at session open).</summary>
      public void SetWitnesses(List<WitnessEntry> witnesses)
      {
         _witnesses.Clear();
         _witnessColors.Clear();
         if (witnesses != null)
            foreach (WitnessEntry w in witnesses)
            {
               _witnesses.Add(w);
               _witnessColors[w.Name] = ColorForWitness(w);
            }

         RefreshWitnessUi();
      }

      /// <summary>
      ///   Resolved identity of whoever speaks this turn — the main NPC by default,
      ///   or a witness the player addressed. Carries the profile/hero/colour needed
      ///   to route the LLM call and render the reply under the right portrait.
      /// </summary>
      private sealed class ChatSpeaker
      {
         public string Color; // null → default NPC cream
         public Hero Hero;
         public bool IsMain;
         public string Name;
         public NpcProfile Profile;
      }

      /// <summary>One attributed line of conversation history.</summary>
      private sealed class TranscriptEntry
      {
         public bool IsPlayer;
         public string SpeakerHeroId;
         public string SpeakerName;
         public string Text;
      }
   }
}