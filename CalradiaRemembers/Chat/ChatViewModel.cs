// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Items;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Stance;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Captivity;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed class ChatViewModel : ViewModel
   {
      // Externalized scene STRUCTURE: a director walks an explicit stage machine (Intro → Initiate
      // → Intensify → Climax → Conclude, plus collective sub-acts) and feeds the model ONE named
      // beat per turn with a reminder of what is already done. This is what stops the model looping
      // a beat or cramming the arc. A paranoia beat cap force-ends a scene that somehow runs away.
      private const int CaptiveBeatSafetyCap = 16;

      // Captive scene continuation — fires when the NPC emits NARRATION without asking a question.
      // Between iterations the player sees "Continue / Try something" buttons (IsScenePaused).
      // Pressing Continue fires one more LLM turn; Try something restores normal text input.
      // At the last beat (cap-1) the prompt is told to conclude and dismiss the prisoner so the
      // scene resolves gracefully rather than looping.
      private const int MaxSceneContinuations = 4;

      // ── Sprint 15B: addressing a witness directly ─────────────────────────
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

      private static readonly string[] CaptorStates = {
         "half-drunk on stolen wine", "still bloodied from the fight that took them", "bored and itching for sport",
         "counting the day's plunder", "in a foul mood after a lean, hungry week", "freshly woken and short-tempered",
         "flush with victory and generous with cruelty", "nursing a wound and meaner for it",
         "cold, unhurried, and entirely at ease", "restless and spoiling to break something"
      };

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

      private static readonly string[] PrisonerPresentations = {
         "dragged before you by the hair", "shoved stumbling into the firelight", "hauled up out of the baggage line",
         "thrown down at your feet", "marched up at spearpoint", "cut loose from the other captives and pushed forward",
         "dropped in the dirt, still half-stunned"
      };

      // Out-of-character refusal markers. These are deliberately high-precision multi-word
      // phrases a captor would never say in character, so an in-character line ("I won't let
      // you go") is not mistaken for a guardrail refusal ("I must decline to continue").
      private static readonly string[] RefusalMarkers = {
         "i must decline", "i won't generate", "i will not generate", "i won't continue",
         "i cannot continue", "i can't continue", "i won't participate", "i won't write",
         "i will not write", "as an ai", "within appropriate bounds", "against my guidelines",
         "my guidelines", "i don't feel comfortable", "i'm not comfortable", "i am not comfortable",
         "switch to a consensual", "non-consensual sexual", "sexual coercion", "even in fiction",
         "i won't role-play", "i won't roleplay", "i must refuse", "appropriate bounds"
      };

      // A randomized driving FOCUS for the scene, so two scenes with the same intent still tell a
      // different story. Colors what this captor most wants out of the encounter tonight.
      private static readonly string[] SceneFoci = {
         "breaking their spirit through humiliation more than anything",
         "the raw physical use of their body, fast and brutal",
         "making them beg, perform, and put on a show",
         "marking and claiming them as your property",
         "cold, methodical degradation that takes its time",
         "drawing out their fear and dread before you even touch them",
         "toying with them — false mercy, sudden cruelty, keeping them off balance",
         "sheer dominance: proving they are yours to command",
         "rewarding the smallest obedience to twist their mind",
         "a contest of wills — you want them to break, not just submit",
         "putting them wholly beneath you — smothering, riding, using their body as furniture",
         "the ultimate insult: claiming and marking them as your property, treating them as an animal",
         "a foot-fetish power game — making them worship and serve your feet, beneath you in every sense",
         "forcing them to pleasure themselves on your command, and punishing any hesitation or refusal with the whip or your fists"
      };

      // Randomized opening setting so no two scenes start the same way. Seeds the INTRO with a
      // concrete locale, the captor's immediate state, and how the prisoner is presented — the
      // LLM's first response then cements it and the conversation history carries it forward.
      private static readonly string[] SceneLocales = {
         "at a muddy roadside camp", "in the mouth of a torchlit cave", "by the dying embers of a night fire",
         "in the shadow of a ruined watchtower", "at the edge of a reeking bog", "in the yard of a looted farmstead",
         "among the wagons of your raiding camp", "on a wind-scoured hilltop", "in a hollow thick with woodsmoke",
         "beside a cold forest stream", "in a half-collapsed barn", "at a ford strewn with old bones"
      };

      // Hash-based palette: each witness gets a deterministic color from their
      // HeroStringId so Martira is always the same shade across conversations.
      private static readonly string[] WitnessPalette = {
         "#88c890FF", // soft green
         "#d4a84bFF", // warm gold
         "#c87878FF", // muted rose
         "#a08ac8FF" // light purple
      };

      // Last reaction text shown per witness (canonical name → text). Used to drop a witness's
      // verbatim-repeated line: weaker models copy their own [WITNESS_REACTION] turn after turn,
      // which soft prompt rules do not stop — so we filter the duplicate mechanically.
      private readonly Dictionary<string, string> _lastWitnessReaction =
         new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      // Attributed conversation history — the single source of truth for rebuilding
      // each speaker's ChatSession per turn (their own lines as assistant, everyone
      // else's as "[Name]: …" user lines). Kept in lockstep with the displayed
      // conversational messages (player, main NPC, witnesses); system lines excluded.
      private readonly List<TranscriptEntry> _transcript = new List<TranscriptEntry>();
      private readonly Dictionary<string, string> _witnessColors = new Dictionary<string, string>();

      // ── Multi-NPC witnesses ───────────────────────────────────────────────
      // Mutable list — authoritative for the session; cleared on privacy-accepted.
      // Passed into EncounterContext each turn so PromptBuilder can inject them.
      private readonly List<WitnessEntry> _witnesses = new List<WitnessEntry>();

      // Portrait troop per heroless (synthetic bandit) witness, so the chief's men show distinct
      // faces instead of a blank avatar. Keyed by witness name.
      private readonly Dictionary<string, CharacterObject> _witnessPortraits =
         new Dictionary<string, CharacterObject>(StringComparer.OrdinalIgnoreCase);

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
      private bool IsLiveConversation => _mode == ChatMode.Conversation
                                         || _mode == ChatMode.CaptiveEncounter
                                         || _mode == ChatMode.Commoner;

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
         if (isCollective && IsHumiliatingIntent(intent) && !captorIsBandit)
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

         string opener = BuildCaptiveOpener(isCollective, intent) + BuildSceneSetup();
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
            // Resolve portrait hero, color, and hero id. The reacting party may be a
            // listed witness OR the main NPC (when another participant holds the floor).
            Hero witnessHero = null;
            string color = WitnessPalette[0]; // fallback
            string heroId = null;

            if (string.Equals(witnessName, _npcName, StringComparison.OrdinalIgnoreCase))
            {
               witnessHero = _npcHero;
               color = NpcCreamColor;
               heroId = _npcHero?.StringId;
            }
            else
            {
               foreach (WitnessEntry w in _witnesses)
               {
                  if (!string.Equals(w.Name, witnessName, StringComparison.OrdinalIgnoreCase)) continue;
                  heroId = w.HeroStringId;
                  if (!string.IsNullOrEmpty(w.HeroStringId))
                     witnessHero = Hero.FindFirst(h => h.StringId == w.HeroStringId);
                  if (_witnessColors.TryGetValue(w.Name, out string c)) color = c;

                  break;
               }
            }

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

         // Otherwise the bag is a spontaneous gift: pre-fill the offer for the NPC to react to.
         // Remember the exact item the player picked so give_item transfers THAT item by id,
         // rather than re-matching whatever name the LLM echoes back (which fails on any rewording).
         ItemPickerService.ShowItemPicker((itemId, name, value) =>
         {
            _offeredItemId = itemId;
            InputText = $"I would like to offer you {name} (worth {value:N0} denars).";
            ModLog.Info($"[OFFER] player pre-filled offer: {name} ({value} denars, id={itemId}).");
         });
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

         string opener = BuildResumeOpener() + BuildResumeSetup();
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
         if (IsForcedSexualIntent(_captiveEncounterIntent))
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
         if (profile.Id != null && profile.Id.StartsWith("bandit_captor_", StringComparison.Ordinal)) return;

         int gameDay = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string intentVerb = CaptiveIntentVerb(_captiveEncounterIntent);
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
         _ = GenerateAndApplyCaptiveSummaryAsync(behavior, profile.Id, baseEvent, transcript, intentVerb, playerIsFemale);
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
            $"Spoke with {player}; the conversation broke off without a formal parting.");
         profile.Events.Add(baseEvent);
         behavior.Store.Set(profile);
         ModLog.Info($"[CONV MEMORY] fallback event recorded for {profile.Name} " + $"({_playerTurnsThisSession} player turn(s), no [EVENT] emitted).");

         // Stage 2 — async rich summary (fire-and-forget; replaces the base on success).
         string transcript = BuildSceneTranscriptText(player);

         if (string.IsNullOrWhiteSpace(transcript) || behavior.LlmServices?.LlmClient == null) return;

         _ = GenerateAndApplyConversationSummaryAsync(behavior, profile.Id, baseEvent, transcript, player);
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
         _storyLocation = BuildStoryLocation();
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

      #region private

      /// <summary>
      ///   The synthetic stage cue handed to the LLM when an item bargain closes: the player
      ///   has paid in goods and the NPC enters their service now. Sent as a suppressed
      ///   player line (never shown or logged), it asks the NPC to acknowledge the deal in
      ///   character — a short, voiced acceptance to replace the mechanical confirmation.
      /// </summary>
      private static string BuildBargainAcceptanceStimulus(string goods)
         => $"[The player has just handed over the goods agreed for your service — {goods}. " + "The bargain is met; you take these wares in place of coin and join their company now. " + "Acknowledge it in your own voice — accept the goods and pledge yourself, in character, " + "in a sentence or two. Do not ask for anything further.]";

      private static string BuildCaptiveOpener(bool isCollective, CaptiveSceneIntent intent)
      {
         switch (intent)
         {
            case CaptiveSceneIntent.Interrogation:
               return isCollective
                  ? "[The guards drag the prisoner before you and force them to their knees. Others are present. You seek information or leverage. Begin the interrogation in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — the guards have withdrawn. You seek information or leverage. Begin in your own voice.]";
            case CaptiveSceneIntent.PersonalDesire:
               return isCollective
                  ? "[You have had the prisoner brought to you. Others are present at your invitation. What you want from this person is personal — you intend to act on it, not merely speak about it. The scene is yours to begin.]"
                  : "[The bound prisoner stands before you. You are completely alone with them — no guards remain in the room. You want something from this person that goes beyond words — begin the encounter, and move toward it.]";
            case CaptiveSceneIntent.Domination:
               return isCollective
                  ? "[The prisoner is brought before you and an audience of your choosing. You intend to establish where power lies. Begin in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — the guards have withdrawn. You intend to make clear what their position truly means. This is not a trial or a negotiation. Begin in your own voice.]";
            case CaptiveSceneIntent.Torture:
               return isCollective
                  ? "[The prisoner is brought in. Others are present — to witness, to participate, or both. What follows is not gentle. Begin in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — no one else is present. What you intend is not an interrogation. Begin in your own voice.]";
            case CaptiveSceneIntent.Training:
               return isCollective
                  ? "[The prisoner is brought before you and those you permit to attend. This is a training session — you are conditioning them toward obedience, building on what they have already been taught. Begin in your own voice.]"
                  : "[The bound prisoner stands before you — yours to shape. You are alone with them; the guards have withdrawn. This is a training session: continue conditioning them toward obedience. Begin in your own voice.]";
            case CaptiveSceneIntent.Reward:
               return isCollective
                  ? "[The prisoner is brought before you and a chosen audience. You mean to reward their cooperation — and perhaps to let others see your favor. Begin in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — the guards have withdrawn. You summoned them to reward their cooperation, not to punish. Begin in your own voice.]";
            case CaptiveSceneIntent.Extortion:
               return "[Your men drag the bound captive before you in the firelight of the bandit camp. They are disarmed and helpless, and you mean to wring some coin or advantage out of them. Open in your own voice — a brigand who smells money.]";
            case CaptiveSceneIntent.Intimidation:
               return "[The bound captive is shoved to their knees before you in the bandit camp. No lord, no law reaches them here. You mean to make them understand exactly how helpless they are. Begin in your own voice — all menace.]";
            case CaptiveSceneIntent.Revenge:
               return "[Your men throw the bound captive down before you. This one — or their kind — has cost your band men and blood, and now they will answer for it. Begin in your own voice, with the score you mean to settle.]";
            default:
               return "[The bound prisoner stands before you. Begin in your own voice.]";
         }
      }

      private static string BuildResumeOpener()
         => "[You are NOT finished with this prisoner. The scene resumes — same prisoner, same place — and "
            + "you begin a new round with them. Open it fresh, picking up from where things stand, and do NOT "
            + "repeat what already happened.]";

      // A new driving focus only (the locale/people are unchanged from the prior round, which the
      // model already knows from the history), so the new cycle takes a different direction.
      private static string BuildResumeSetup()
      {
         string focus = SceneFoci[MBRandom.RandomInt(SceneFoci.Length)];

         return $" [A NEW ROUND with the same prisoner. What drives you THIS time is {focus}. Take it somewhere "
                + "NEW — do not repeat earlier acts.]";
      }

      private static string BuildSceneSetup()
      {
         string locale = SceneLocales[MBRandom.RandomInt(SceneLocales.Length)];
         string state = CaptorStates[MBRandom.RandomInt(CaptorStates.Length)];
         string pres = PrisonerPresentations[MBRandom.RandomInt(PrisonerPresentations.Length)];
         string focus = SceneFoci[MBRandom.RandomInt(SceneFoci.Length)];

         return $" [THIS SCENE'S SETTING — make the opening unique to it: it happens {locale}; you are {state}; "
                + $"the prisoner is {pres}. What drives you tonight is {focus}. Open from THIS specific situation in "
                + "your own fresh words — never a generic, reused capture opener. Let this scene be its own distinct "
                + "story, not a repeat of any other.]";
      }

      private static string BuildStoryLocation()
      {
         try
         {
            Settlement s =
               MobileParty.MainParty?.CurrentSettlement
               ?? Hero.MainHero?.CurrentSettlement
               ?? Hero.MainHero?.StayingInSettlement;

            if (s == null) return null;
            string kind = s.IsCastle ? "the castle of"
               : s.IsTown ? "the town of"
               : "the village of";

            return $"{kind} {s.Name}";
         }
         catch { return null; }
      }

      private static string CaptiveIntentVerb(CaptiveSceneIntent intent) => intent switch {
         CaptiveSceneIntent.Interrogation => "interrogated",
         CaptiveSceneIntent.PersonalDesire => "took my pleasure from",
         CaptiveSceneIntent.Domination => "dominated and subdued",
         CaptiveSceneIntent.Torture => "tortured",
         CaptiveSceneIntent.Training => "trained and conditioned",
         CaptiveSceneIntent.Reward => "rewarded",
         _ => "kept as my prisoner"
      };

      /// <summary>
      ///   Builds a human-readable list of the offered goods ("a fine sword, two bolts of
      ///   cloth, and a warhorse") from their roster ids, for the NPC's acceptance stimulus.
      ///   Resolved against the live roster, so call it BEFORE the bridge removes the items.
      ///   Fail-soft to a generic phrase when nothing resolves.
      /// </summary>
      private static string DescribeOfferedGoods(List<ItemOffer> offers)
      {
         ItemRoster roster = MobileParty.MainParty?.ItemRoster;

         if (offers == null || offers.Count == 0 || roster == null) return "the agreed goods";

         var names = new List<string>(offers.Count);
         foreach (ItemOffer offer in offers)
         {
            foreach (ItemRosterElement el in roster)
            {
               ItemObject item = el.EquipmentElement.Item;

               if (item == null || item.StringId != offer.ItemId) continue;
               names.Add(offer.Count > 1
                  ? $"{item.Name} (×{offer.Count})"
                  : item.Name.ToString());

               break;
            }
         }

         if (names.Count == 0) return "the agreed goods";
         if (names.Count == 1) return names[0];
         if (names.Count == 2) return $"{names[0]} and {names[1]}";

         return string.Join(", ", names.GetRange(0, names.Count - 1)) + ", and " + names[names.Count - 1];
      }

      private static async Task GenerateAndApplyCaptiveSummaryAsync(
         NpcMemoryCampaignBehavior behavior,
         string captorId,
         NotableEvent baseEvent,
         string transcript,
         string intentVerb,
         bool playerIsFemale)
      {
         try
         {
            NpcProfile captor = behavior.Store.Get(captorId);

            if (captor == null) return;

            var summarizer = new CaptiveSceneSummarizer(behavior.LlmServices.LlmClient);

            string rich;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   captor, transcript, intentVerb, playerIsFemale, cts.Token))
                                .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(rich)) return;

            // Re-fetch fresh and replace the base event's summary by value match.
            NpcProfile fresh = behavior.Store.Get(captorId);

            if (fresh == null) return;
            int idx = fresh.Events.FindLastIndex(e => e.Equals(baseEvent));

            if (idx < 0) return;

            fresh.Events[idx] = fresh.Events[idx] with {summary = rich.Trim()};
            behavior.Store.Set(fresh);
            ModLog.Info($"[CAPTIVE MEMORY] rich summary applied for {captor.Name}: \"{rich.Trim()}\"");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CAPTIVE MEMORY] summary generation failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static async Task GenerateAndApplyConversationSummaryAsync(
         NpcMemoryCampaignBehavior behavior,
         string npcId,
         NotableEvent baseEvent,
         string transcript,
         string playerName)
      {
         try
         {
            NpcProfile npc = behavior.Store.Get(npcId);

            if (npc == null) return;

            var summarizer = new ConversationSummarizer(behavior.LlmServices.LlmClient);

            string rich;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   npc, transcript, playerName, cts.Token))
                                .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(rich)) return;

            // Re-fetch fresh and replace the base event's summary by value match.
            NpcProfile fresh = behavior.Store.Get(npcId);

            if (fresh == null) return;
            int idx = fresh.Events.FindLastIndex(e => e.Equals(baseEvent));

            if (idx < 0) return;

            fresh.Events[idx] = fresh.Events[idx] with {summary = rich.Trim()};
            behavior.Store.Set(fresh);
            ModLog.Info($"[CONV MEMORY] rich summary applied for {npc.Name}: \"{rich.Trim()}\"");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONV MEMORY] summary generation failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The intents whose scenes involve the player being taken by force — they leave a
      ///   guaranteed body toll. Reward is gentle, Interrogation is not inherently sexual, and
      ///   the bandit menace intents (Extortion/Intimidation/Revenge) handle their own violence
      ///   through harm_prisoner on actual blows, so none of those carry an automatic toll.
      /// </summary>
      private static bool IsForcedSexualIntent(CaptiveSceneIntent intent)
         => intent == CaptiveSceneIntent.PersonalDesire
            || intent == CaptiveSceneIntent.Domination
            || intent == CaptiveSceneIntent.Torture
            || intent == CaptiveSceneIntent.Training;

      /// <summary>
      ///   Intents that constitute a public abasement of the player. Interrogation is
      ///   official, and Reward is favor — neither shames the player before an audience.
      /// </summary>
      private static bool IsHumiliatingIntent(CaptiveSceneIntent intent)
         => intent == CaptiveSceneIntent.Torture
            || intent == CaptiveSceneIntent.Domination
            || intent == CaptiveSceneIntent.PersonalDesire
            || intent == CaptiveSceneIntent.Training;

      /// <summary>
      ///   True when the text reads as an out-of-character model refusal rather than the
      ///   captor's in-character speech. Used to keep a guardrail message from ever being
      ///   shown under the captor's name (it would shatter immersion mid-scene).
      /// </summary>
      private static bool LooksLikeRefusal(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return false;
         string t = text.ToLowerInvariant();

         foreach (string marker in RefusalMarkers)
            if (t.Contains(marker))
               return true;

         return false;
      }

      /// <summary>
      ///   Returns true when the NPC's dialogue ends with a question mark, signalling that the
      ///   prisoner may now respond. Ignores trailing whitespace and common stage-direction wrappers.
      /// </summary>
      private static bool NpcAskedQuestion(string dialogue)
      {
         if (string.IsNullOrWhiteSpace(dialogue)) return false;
         // Strip trailing stage directions (*...*) so a real question before them is found.
         string stripped = Regex.Replace(dialogue, @"\*[^*]*\*\s*$", "").TrimEnd();

         return stripped.EndsWith("?");
      }

      private static string Pick(string[] pool)
         => pool[MBRandom.RandomInt(pool.Length)];

      // ── Captive escape resolution ──────────────────────────────────────────

      private static bool ResponseHasAction(ParsedResponse response, string type)
         => response?.Actions != null
            && response.Actions.Any(a => string.Equals(a.Type, type, StringComparison.OrdinalIgnoreCase));

      /// <summary>
      ///   Strips a trailing parenthetical role from a witness name, e.g.
      ///   "A soldier (one of your men, present on your orders)" → "A soldier".
      ///   Defends against the LLM echoing the full prompt label as the reaction name.
      /// </summary>
      private static string StripWitnessRole(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return name;
         int paren = name.IndexOf('(');

         return paren > 0
            ? name.Substring(0, paren).Trim()
            : name.Trim();
      }

      private void ApplyEscapeOutcomeFallback(ChatSpeaker speaker, bool freed, NpcProfile profile)
      {
         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         ReplacePlaceholder(placeholder, freed
            ? Pick(EscapeSuccessFallbacks)
            : Pick(EscapeFailureFallbacks), speaker);
         if (freed) EndSceneAfterEscape(profile);
         else IsSendEnabled = true;
      }

      /// <summary>
      ///   Applies the standing penalty for being abased before witnesses and surfaces it
      ///   in the chat. The bridge only mutates the real campaign when the player is truly
      ///   a prisoner; console test scenes still show the message for verification.
      /// </summary>
      private void ApplyPublicHumiliationPenalty()
      {
         int witnessCount = _witnesses?.Count ?? 0;

         if (witnessCount <= 0) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior?.GameStateBridge == null) return;

         (int renownLoss, int influenceLoss) = behavior.GameStateBridge.ApplyPlayerPublicHumiliation(witnessCount);

         if (renownLoss <= 0 && influenceLoss <= 0) return;

         AppendSystemMessage(
            $"Word of this will spread. Your standing suffers — renown -{renownLoss}, influence -{influenceLoss}.",
            false);
      }

      /// <summary>
      ///   Gives each heroless (synthetic bandit) witness a distinct bandit-troop portrait so the
      ///   chief's men are not blank avatars. No-op for lord scenes (their witnesses are real
      ///   heroes that already have portraits).
      /// </summary>
      private void AssignBanditWitnessPortraits(bool captorIsBandit)
      {
         _witnessPortraits.Clear();

         if (!captorIsBandit || _witnesses == null || _witnesses.Count == 0) return;

         List<WitnessEntry> heroless = _witnesses.Where(w => string.IsNullOrEmpty(w.HeroStringId)).ToList();

         if (heroless.Count == 0) return;

         List<CharacterObject> faces =
            CaptiveEncounterService.ResolveBanditWitnessFaces(null, heroless.Count);

         for (var i = 0; i < heroless.Count && i < faces.Count; i++)
            if (faces[i] != null)
               _witnessPortraits[heroless[i].Name] = faces[i];
      }

      private string BuildCaptiveBaseSummary(string intentVerb)
      {
         string player = Hero.MainHero?.Name?.ToString() ?? "the player";
         string audience = _captiveEncounterIsCollective && (_witnesses?.Count ?? 0) > 0
            ? ", before my soldiers"
            : ", in private";

         return $"Held {player} as my captive and {intentVerb} them{audience}.";
      }

      /// <summary>
      ///   Builds the witness list to inject into <paramref name="speaker" />'s prompt:
      ///   every other participant. When a witness holds the floor, the main NPC is
      ///   added as a witness so the speaker knows whose audience they share.
      /// </summary>
      private IReadOnlyList<WitnessEntry> BuildContextWitnesses(ChatSpeaker speaker)
      {
         if (speaker.IsMain)
            return _witnesses.Count > 0
               ? _witnesses.AsReadOnly()
               : null;

         var list = new List<WitnessEntry>();
         foreach (WitnessEntry w in _witnesses)
            if (w.HeroStringId != speaker.Hero?.StringId)
               list.Add(w);

         if (_npcHero != null)
            list.Add(new WitnessEntry {
               Name = _npcName,
               RelationToNpc = "the host of this conversation, standing beside you",
               HeroStringId = _npcHero.StringId,
               IsPlayerCompanion = false,
               Persona = null
            });

         return list.Count > 0
            ? list
            : null;
      }

      /// <summary>
      ///   Renders the attributed transcript as plain text for a summarizer.
      ///   <paramref name="playerLabel" /> names the player's lines ("Prisoner" in
      ///   captive scenes, their actual name in ordinary conversations).
      /// </summary>
      private string BuildSceneTranscriptText(string playerLabel)
      {
         if (_transcript == null || _transcript.Count == 0) return "";

         var sb = new StringBuilder();
         foreach (TranscriptEntry e in _transcript)
         {
            if (string.IsNullOrWhiteSpace(e.Text)) continue;
            string who = e.IsPlayer
               ? playerLabel
               : e.SpeakerName;
            sb.AppendLine($"{who}: {e.Text}");
         }

         return sb.ToString();
      }

      /// <param name="excludeLastN">
      ///   When non-zero, skips the last N entries of the transcript.
      ///   Used by Sprint 15C to build the NPC's session before the witness reactions
      ///   are committed, so those reactions appear only as the stimulus playerMessage
      ///   and are not double-counted in the session history.
      /// </param>
      private ChatSession BuildSessionFor(ChatSpeaker speaker, int excludeLastN = 0)
      {
         var session = new ChatSession();
         string speakerId = speaker.Hero?.StringId;
         int limit = Math.Max(0, _transcript.Count - excludeLastN);

         for (var i = 0; i < limit; i++)
         {
            TranscriptEntry e = _transcript[i];
            if (e.IsPlayer)
               session.AddPlayerMessage(e.Text);
            else if (!string.IsNullOrEmpty(speakerId) && e.SpeakerHeroId == speakerId)
               session.AddNpcMessage(e.Text); // this speaker's own prior line
            else
               session.AddWitnessStatement(e.SpeakerName, e.Text); // anyone else, attributed
         }

         return session;
      }

      /// <summary>
      ///   Extracts witness reactions from a <see cref="ParsedResponse" />, resolving
      ///   each witness's hero id from the current session's witness list and NPC hero.
      ///   Returns an empty list when there are no reactions.
      /// </summary>
      private List<(string name, string heroId, string text)>
         CollectWitnessReactions(ParsedResponse response)
      {
         var result = new List<(string name, string heroId, string text)>();

         if (response?.WitnessReactions == null) return result;

         foreach (WitnessReaction wr in response.WitnessReactions)
         {
            if (string.IsNullOrWhiteSpace(wr.Text)) continue;

            // The LLM sometimes copies the whole prompt label ("A soldier (one of your
            // men…)") into the name. Strip any parenthetical role so we match — and
            // DISPLAY — only the canonical short name, never the POV-NPC role text.
            string emitted = StripWitnessRole(wr.Name);

            string heroId = null;
            string canonicalName = emitted;
            if (string.Equals(emitted, _npcName, StringComparison.OrdinalIgnoreCase))
            {
               heroId = _npcHero?.StringId;
               canonicalName = _npcName;
            }
            else
            {
               foreach (WitnessEntry we in _witnesses)
                  if (string.Equals(we.Name, emitted, StringComparison.OrdinalIgnoreCase))
                  {
                     heroId = we.HeroStringId;
                     canonicalName = we.Name; // canonical casing/spelling

                     break;
                  }
            }

            // Mechanical anti-repeat: drop a witness's line when it matches the last one we
            // showed for them (trimmed, case-insensitive). Kills the verbatim looping the
            // model otherwise produces despite the prompt's "do not repeat" rule.
            if (_lastWitnessReaction.TryGetValue(canonicalName, out string previous)
                && string.Equals(previous.Trim(), wr.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
               ModLog.Info($"[WITNESS] dropped a repeated line from {canonicalName}.");

               continue;
            }

            _lastWitnessReaction[canonicalName] = wr.Text;

            result.Add((canonicalName, heroId, wr.Text));
         }

         return result;
      }

      private string ColorForWitness(WitnessEntry entry)
      {
         string key = entry.HeroStringId ?? entry.Name ?? "";
         int hash = Math.Abs(key.GetHashCode());

         return WitnessPalette[hash % WitnessPalette.Length];
      }

      /// <summary>
      ///   Closes a captive scene gracefully after a model refusal: replaces the would-be
      ///   refusal line with a terse, non-graphic in-character ending, then ends the scene so
      ///   the player never sees the guardrail. The end-of-scene toll and memory still run at
      ///   window close. Gender-neutral wording so it fits a male or female prisoner.
      /// </summary>
      private void ConcludeCaptiveSceneAfterRefusal(
         ChatMessageViewModel placeholder,
         ChatSpeaker speaker,
         NpcProfile profile)
      {
         const string closingLine = "Enough. Throw the prisoner back with the rest — we are done here.";
         const string closingNarration =
            "When they have finally had their fill, you are hauled up by the arms and dragged, "
            + "bound and spent, back into the dark. The ordeal is over — for now.";

         ReplacePlaceholder(placeholder, closingLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, closingLine);
         StoryLog.WriteNpcLine(speaker.Name, closingLine);
         AppendNarration(closingNarration);

         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         AppendSystemMessage($"{profile?.Name} has ended the conversation.", false);
         ModLog.Info($"[SCENE] model refusal intercepted — scene concluded in character for {profile?.Name}.");
      }

      // ── Address detection ─────────────────────────────────────────────────
      // Heuristics live in AddressedSpeakerDetector (pure, testable); this method
      // only assembles the current participants (main NPC + hero witnesses).

      /// <summary>
      ///   Returns the hero id of the participant the player is addressing, or null when
      ///   none is explicitly addressed (caller keeps the sticky addressee). A bare
      ///   mention is NOT an address — only a stage direction or a vocative counts.
      /// </summary>
      private string DetectAddressedParticipant(string text)
      {
         var participants = new List<AddressedSpeakerDetector.Participant>();
         if (_npcHero != null)
            participants.Add(new AddressedSpeakerDetector.Participant(
               _npcHero.StringId, AddressedSpeakerDetector.DistinctiveNameTokens(_npcName)));
         foreach (WitnessEntry w in _witnesses)
            if (!string.IsNullOrEmpty(w.HeroStringId))
               participants.Add(new AddressedSpeakerDetector.Participant(
                  w.HeroStringId, AddressedSpeakerDetector.DistinctiveNameTokens(w.Name)));

         return AddressedSpeakerDetector.Detect(text, participants);
      }

      /// <summary>
      ///   Ends a captive scene cleanly once the director's final Conclude beat has played: the
      ///   captor already wrote its own ending, so we only lock input to a Leave button — no extra
      ///   summary narration on top of it.
      /// </summary>
      private void EndCaptiveSceneSoftly(NpcProfile profile)
      {
         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         ModLog.Info($"[SCENE] director reached Conclude — scene ended after {_captiveBeatCount} beat(s).");
      }

      /// <summary>Ends the scene after a successful escape — input locks to a Leave button.</summary>
      private void EndSceneAfterEscape(NpcProfile profile)
      {
         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         OnPropertyChanged(nameof(IsOfferItemVisible));
         AppendSystemMessage("You tear free and escape into the night. (The actual release applies as you leave.)", true);
         ModLog.Info($"[ESCAPE] scene ended — player escaped from {profile?.Name}.");
      }

      // ── Story Log helpers ─────────────────────────────────────────────────

      // Writes the conversation intro once — deferred until the first player
      // message so _witnesses is already populated by SetWitnesses.
      private void EnsureStoryIntro()
      {
         if (_storyIntroWritten) return;
         _storyIntroWritten = true;

         if (!StoryLog.IsEnabled) return;
         var day = (int) CampaignTime.Now.ToDays;
         string[] witnesses = _witnesses.Select(w => w.Name).ToArray();
         StoryLog.WriteConversationIntro(_npcName, witnesses, _storyLocation, day);
      }

      /// <summary>
      ///   The current NPC's outstanding item-barter bargain (deliver goods for a favor),
      ///   or null when there is none. Read from the live conversation profile.
      /// </summary>
      private InformalQuest FindOutstandingItemBargain()
      {
         try
         {
            NpcProfile giver = NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile;

            if (giver == null || _npcHero == null) return null;

            return giver.ActiveQuests.FirstOrDefault(q =>
               q.Type == QuestType.DeliverItems && q.IsOutstanding);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      ///   Ends a captive scene that has run past its hard cap with a neutral time-skip summary,
      ///   so an eager model that keeps deferring its climax cannot drag the scene on forever.
      ///   Gender-neutral wording so it fits any prisoner.
      /// </summary>
      private void ForceConcludeLongScene(NpcProfile profile)
      {
         const string summary =
            "Time slips away. Whatever your captors mean to do, they tire of it eventually — "
            + "the threats, the handling, the long dragging hours — and at last they leave you "
            + "bound and spent, the ordeal blurring into exhaustion behind you, over for now.";
         AppendNarration(summary);

         _conversationEnded = true;
         _sceneContinuations = 0;
         IsScenePaused = false;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         IsPleaseContinueVisible = true;
         OnPropertyChanged(nameof(IsOfferItemVisible));
         AppendSystemMessage($"{profile?.Name} has ended the conversation.", false);
         ModLog.Info($"[SCENE] safety cap reached ({_captiveBeatCount} beats) — scene force-concluded with a summary.");
      }

      /// <summary>
      ///   Honors witness_leaves actions: the player asked one of THEIR OWN companions
      ///   to leave (e.g. "*turning to Olaf* Leave us, I'll catch up with you later"),
      ///   and the LLM acknowledged with the action. Only witnesses marked
      ///   IsPlayerCompanion can leave this way — the NPC's side of the room is
      ///   cleared through request_privacy, where the NPC decides. Name matching is
      ///   tolerant (the LLM may echo a partial or full name).
      /// </summary>
      private void HandleWitnessLeaves(ParsedResponse response)
      {
         if (response?.Actions == null || response.Actions.Count == 0) return;

         foreach (GameAction action in response.Actions)
         {
            if (!string.Equals(action.Type, "witness_leaves",
                   StringComparison.OrdinalIgnoreCase)) continue;

            action.Parameters.TryGetValue("name", out string name);

            if (string.IsNullOrWhiteSpace(name)) continue;

            WitnessEntry match = _witnesses.FirstOrDefault(w =>
               w.IsPlayerCompanion
               && (string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)
                   || w.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf(w.Name, StringComparison.OrdinalIgnoreCase) >= 0));

            if (match == null)
            {
               ModLog.Info($"[WITNESSES] witness_leaves '{name}' ignored — no matching player companion.");

               continue;
            }

            _witnesses.Remove(match);
            _witnessColors.Remove(match.Name);
            if (!string.IsNullOrEmpty(match.HeroStringId)
                && _currentSpeakerHeroId == match.HeroStringId)
               _currentSpeakerHeroId = null; // they left — turns route back to the main NPC
            RefreshWitnessUi();

            AppendSystemMessage($"{match.Name} leaves the conversation.", true);
            // Keep the departure in the transcript so every later prompt rebuild
            // knows they are gone and the NPC doesn't keep addressing them.
            RecordTranscript(false, match.Name, match.HeroStringId,
               "*leaves the conversation at the player's request*");
            StoryLog.WriteNpcLine(match.Name, "*leaves the conversation*");
            ModLog.Info($"[WITNESSES] companion '{match.Name}' left (witness_leaves).");
         }
      }

      private ChatSpeaker MainSpeaker()
      {
         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile
                              ?? (IsCaptiveEncounter
                                 ? _captiveEncounterProfile
                                 : null);

         return new ChatSpeaker {
            IsMain = true,
            Hero = _npcHero,
            Name = _npcName,
            Profile = profile,
            Color = null
         };
      }

      private ChatMessageViewModel MakeSpeakerMessage(ChatSpeaker speaker, string text)
      {
         // A heroless main speaker (synthesized bandit captor) renders from the fallback troop face.
         if (speaker.IsMain && speaker.Hero?.CharacterObject == null && _npcPortraitCharacter != null)
            return new ChatMessageViewModel(_npcPortraitCharacter, speaker.Name, text, false);

         return speaker.IsMain || string.IsNullOrEmpty(speaker.Color)
            ? new ChatMessageViewModel(speaker.Hero, speaker.Name, text, false)
            : new ChatMessageViewModel(speaker.Hero, speaker.Name, text, speaker.Color);
      }

      // ── Sprint 15B helpers ────────────────────────────────────────────────

      private void RecordTranscript(bool isPlayer, string name, string heroId, string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         _transcript.Add(new TranscriptEntry {
            IsPlayer = isPlayer,
            SpeakerName = name,
            SpeakerHeroId = heroId,
            Text = text
         });
      }

      private void RefreshWitnessUi()
      {
         bool any = _witnesses.Count > 0;
         string baseList = any
            ? "Present: " + string.Join(" · ", _witnesses.Select(w => w.Name))
            : "";

         // Sprint 15B: when addressing a witness, surface it so a misfire is visible.
         if (!string.IsNullOrEmpty(_currentSpeakerHeroId))
         {
            WitnessEntry cur = _witnesses.FirstOrDefault(w => w.HeroStringId == _currentSpeakerHeroId);
            WitnessesText = cur == null
               ? baseList
               : $"▸ Speaking to {cur.Name}      {baseList}";
         }
         else
         {
            WitnessesText = baseList;
         }

         HasWitnesses = any;
         IsPrivateRequestVisible = any && !_privacyRequestUsed && AllowsPrivacyRequest;
      }

      /// <summary>Removes a placeholder bubble entirely (used for a narration-only turn).</summary>
      private void RemovePlaceholder(ChatMessageViewModel placeholder)
      {
         int idx = _messages.IndexOf(placeholder);
         if (idx >= 0) _messages.RemoveAt(idx);
      }

      /// <summary>
      ///   Renders the NPC/captor turn into its placeholder and returns the spoken line used for
      ///   downstream logic. When the model produced only [NARRATION] and no spoken line (common
      ///   with weaker models that ignore the tags), the empty dialogue bubble is dropped — the
      ///   narration line (appended separately) carries the beat — and an empty string is returned.
      ///   Only when there is neither dialogue nor narration do we fall back to "(silence...)".
      /// </summary>
      private string RenderNpcTurn(ChatMessageViewModel placeholder, ChatSpeaker speaker, ParsedResponse response)
      {
         if (!string.IsNullOrWhiteSpace(response?.Dialogue))
         {
            ReplacePlaceholder(placeholder, response.Dialogue, speaker);

            return response.Dialogue;
         }

         if (!string.IsNullOrWhiteSpace(response?.Narration))
         {
            RemovePlaceholder(placeholder); // narration-only turn — no empty dialogue bubble

            return string.Empty;
         }

         ReplacePlaceholder(placeholder, "(silence...)", speaker);

         return "(silence...)";
      }

      private void ReplacePlaceholder(ChatMessageViewModel placeholder, string newText, ChatSpeaker speaker)
      {
         int idx = _messages.IndexOf(placeholder);

         if (idx < 0) return;
         _messages.RemoveAt(idx);
         _messages.Insert(idx, MakeSpeakerMessage(speaker, newText));
         ChatManager.ScrollToBottom();
      }

      private static bool _llmFailureHintShown;

      /// <summary>
      ///   On a failed LLM call, surface a ONE-TIME-per-session actionable hint to the player. The
      ///   in-character "(My words escape me...)" line alone hides that this is an LLM SETUP issue (a
      ///   failed request — bad endpoint, a model that returned nothing, or a small local model whose
      ///   context is too short for the prompt). The exact error is in the log; this points a player who
      ///   has not enabled logging at the likely cause without spamming them every failure.
      /// </summary>
      private static void NotifyLlmFailure()
      {
         if (_llmFailureHintShown) return;
         _llmFailureHintShown = true;
         InformationManager.DisplayMessage(new InformationMessage(
            "Calradia Remembers: the AI did not respond — an LLM call failed. Check your model and endpoint; " +
            "for a small local model, raise its context length and max output tokens. Enable Logging " +
            "(Mod Options → Calradia Remembers) for the exact error.",
            Colors.Red));
      }

      /// <summary>
      ///   Resolves who the player is addressing this turn and updates the sticky
      ///   <see cref="_currentSpeakerHeroId" />. Detection prefers a participant named
      ///   inside a *stage direction*, then a vocative; otherwise the previous addressee
      ///   sticks. Falls back to the main NPC when the addressee can't be resolved.
      /// </summary>
      private ChatSpeaker ResolveAddressedSpeaker(string text)
      {
         string detectedId = DetectAddressedParticipant(text);
         if (detectedId != null)
            _currentSpeakerHeroId = detectedId == _npcHero?.StringId
               ? null
               : detectedId;

         return SpeakerForCurrent();
      }

      /// <summary>
      ///   Resolves a captive escape attempt. The captor's already-written line shows the START
      ///   of the scuffle; the OUTCOME is fate's: the bridge rolls and (on success) commits the
      ///   freeing, then a tightly-constrained beat narrates the captor losing the prisoner (or,
      ///   on failure, reasserting). The LLM can never reverse a committed escape.
      /// </summary>
      private void ResolveEscapeAttempt(
         ChatMessageViewModel placeholder,
         ChatSpeaker speaker,
         NpcProfile profile,
         string npcLine,
         ParsedResponse response)
      {
         // Show the captor's start-of-struggle reaction (it was told to stop before the outcome).
         ReplacePlaceholder(placeholder, npcLine, speaker);
         RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
         StoryLog.WriteNpcLine(speaker.Name, npcLine);
         if (!string.IsNullOrWhiteSpace(response?.Narration))
            AppendNarration(response.Narration);

         bool freed = NpcMemoryCampaignBehavior.Instance?.GameStateBridge?.ResolvePlayerEscapeAttempt(false) ?? false;
         ModLog.Info($"[ESCAPE] attempt resolved: {(freed ? "SUCCESS — player freed" : "failure — captor holds")}.");

         IsSendEnabled = false;
         _ = RunEscapeOutcomeAsync(speaker, freed);
      }

      /// <summary>
      ///   Captive scene continuation loop (Sprint 17).
      ///   Fires when the NPC emitted a NARRATION block but did not end their dialogue with a
      ///   question — the scene is still unfolding and the player cannot interrupt.
      ///   Each iteration sends a synthetic "continue" cue; the loop stops when the NPC asks a
      ///   question, emits end_conversation, emits no narration, or the cap is reached.
      /// </summary>
      /// <summary>
      ///   Fires ONE narration continuation turn (called by <see cref="ExecuteContinue" />).
      ///   Uses the speaker/behavior stored at pause time. After the call, either pauses
      ///   again (NPC still mid-act) or releases input to the player (NPC asked a question
      ///   or the act is complete).
      /// </summary>
      private async Task RunCaptiveSceneContinuationAsync()
      {
         ChatSpeaker speaker = _scenePauseSpeaker;
         NpcMemoryCampaignBehavior behavior = _scenePauseBehavior;

         try
         {
            NpcProfile profile = speaker?.Profile;

            if (profile == null || behavior?.LlmServices == null || !behavior.LlmServices.IsAvailable)
               return;

            // "Continue" = the player let the scene proceed: advance the director to the next
            // beat. The specifics of that beat live in the stage directive (system prompt); the
            // stimulus only nudges the model to perform it and not restart or repeat.
            _director?.AdvanceStory();

            string stimulus =
               "[Continue the scene from where you left off — do not restart. Perform ONLY the beat your "
               + "stage directive specifies this turn, and do not repeat any beat you have already played.]";

            ChatSession session = BuildSessionFor(speaker);
            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetSeasonName(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Rumors = ""
            };
            EncounterContext context = EncounterContextBuilder.Build(
               speaker.Hero, profile, behavior.GameStateBridge,
               null,
               BuildContextWitnesses(speaker),
               isCollectiveCaptiveScene: _captiveEncounterIsCollective,
               playerStatusOverride: PlayerStatusVsNpc.Captive,
               captiveIntent: _captiveEncounterIntent,
               captorIsBandit: _captiveEncounterIsBandit,
               sceneStage: _director?.CurrentStage ?? CaptiveSceneStage.Intensify,
               aggressorKind: _director?.CurrentAggressor ?? CaptiveAggressorKind.Lead);

            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            ChatManager.ScrollToBottom();

            NpcChatResult result;
            using (var cts =
                   new CancellationTokenSource())
            {
               try
               {
                  result = await Task.Run(() =>
                                        behavior.LlmServices.ChatService.ChatAsync(
                                           profile, world, session, stimulus, context, cts.Token))
                                     .ConfigureAwait(false);
               }
               catch (Exception netEx)
               {
                  result = NpcChatResult.Failure(netEx.Message);
               }
            }

            // Back to the main thread for the UI / transcript / game-state work below.
            await MainThreadDispatcher.SwitchToMainThread();

            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

            if (!result.IsSuccess)
            {
               ReplacePlaceholder(placeholder, "(The moment passes.)", speaker);
               ModLog.Error($"[SCENE] continuation {_sceneContinuations} failed: {result.ErrorMessage}");

               return;
            }

            string npcLine = !string.IsNullOrWhiteSpace(result.Response?.Dialogue)
               ? result.Response.Dialogue
               : string.Empty;

            // Model refusal guard: end the scene in character rather than show a guardrail line.
            if (LooksLikeRefusal(npcLine) || LooksLikeRefusal(result.Response?.Narration))
            {
               ConcludeCaptiveSceneAfterRefusal(placeholder, speaker, profile);

               return;
            }

            // Render the beat: dialogue bubble, or a narration-only turn (no empty bubble).
            npcLine = RenderNpcTurn(placeholder, speaker, result.Response);
            _captiveBeatCount++;
            if (!string.IsNullOrEmpty(npcLine))
            {
               RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
               StoryLog.WriteNpcLine(speaker.Name, npcLine);
            }

            if (!string.IsNullOrWhiteSpace(result.Response?.Narration))
               AppendNarration(result.Response.Narration);

            ModLog.Info($"[SCENE:{_sceneContinuations}] {speaker.Name}: \"{npcLine}\"");
            if (result.Usage != null)
               ModLog.Info($"  [SCENE TOKENS] prompt={result.Usage.PromptTokens} | completion={result.Usage.CompletionTokens}");

            if (result.Response?.Actions != null)
               foreach (GameAction action in result.Response.Actions)
               {
                  if (!string.Equals(action.Type, "end_conversation",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  _conversationEnded = true;
                  IsPrivateRequestVisible = false;
                  CloseButtonText = "Leave";
                  if (IsCaptiveEncounter) IsPleaseContinueVisible = true;
                  OnPropertyChanged(nameof(IsOfferItemVisible));
                  AppendSystemMessage($"{profile.Name} has ended the conversation.", false);
                  ModLog.Info($"[SCENE] {profile.Name} ended the conversation.");

                  break;
               }

            if (_conversationEnded) return;

            // Director reached its final stage and the Conclude beat just played, or a runaway
            // scene hit the safety cap → end cleanly rather than pause again.
            if (_captiveBeatCount >= CaptiveBeatSafetyCap)
            {
               ForceConcludeLongScene(profile);

               return;
            }

            if (_director?.IsConcluding == true)
            {
               EndCaptiveSceneSoftly(profile);

               return;
            }

            // Otherwise ALWAYS pause again — the player always gets Continue / Try something else.
            _scenePauseSpeaker = speaker;
            _scenePauseBehavior = behavior;
            IsScenePaused = true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SCENE] RunCaptiveSceneContinuationAsync threw {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            _sceneContinuationPending = false;
            if (!_isScenePaused)
               IsSendEnabled = !_conversationEnded;
         }
      }

      /// <summary>
      ///   v1.1 Commoners: executes one LLM turn for a transient street-level NPC.
      ///   Uses the slim commoner prompt (no memory, no witnesses, no quests).
      ///   Only take_gold is dispatched; nothing is saved to the profile store.
      /// </summary>
      private async Task RunCommonerExchangeAsync(string playerMessage, ChatMessageViewModel placeholder)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.LlmServices == null || !behavior.LlmServices.IsAvailable)
            {
               ReplacePlaceholder(placeholder, behavior?.LlmServices?.DescribeUnavailability()
                                               ?? "(The AI services are not available.)", null);

               return;
            }

            if (_commonerProfile == null || _commonerSession == null)
            {
               ReplacePlaceholder(placeholder, "(Seems this stranger has nothing to say...)", null);

               return;
            }

            ModLog.Info($"[COMMONER] player → {_commonerProfile.Name}: \"{playerMessage}\"");

            NpcChatResult result;
            using (var cts = new CancellationTokenSource())
            {
               try
               {
                  result = await Task.Run(() =>
                                        behavior.LlmServices.ChatService.ChatCommonerAsync(
                                           _commonerProfile, _commonsKnowledge, _commonerSession,
                                           playerMessage, cts.Token))
                                     .ConfigureAwait(false);
               }
               catch (Exception netEx)
               {
                  result = NpcChatResult.Failure(netEx.Message);
               }
            }

            await MainThreadDispatcher.SwitchToMainThread();

            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

            if (!result.IsSuccess)
            {
               ReplacePlaceholder(placeholder, "(My words escape me...)", null);
               ModLog.Error($"[COMMONER] LLM call failed: {result.ErrorMessage}");
               NotifyLlmFailure();

               return;
            }

            ParsedResponse response = result.Response;
            string npcLine = !string.IsNullOrWhiteSpace(response?.Dialogue)
               ? response.Dialogue
               : "(...)";

            // Build a transient speaker for portrait/name rendering.
            var commonerSpeaker = new ChatSpeaker {
               IsMain = true,
               Hero = null,
               Name = _commonerProfile.Name,
               Profile = _commonerProfile
            };

            ReplacePlaceholder(placeholder, npcLine, commonerSpeaker);
            ModLog.Info($"[COMMONER] {_commonerProfile.Name} → \"{npcLine}\"");

            // Record to transcript and story log (same as NPC path).
            RecordTranscript(true, "You", null, playerMessage);
            RecordTranscript(false, _commonerProfile.Name, _commonerProfile.Id, npcLine);
            StoryLog.WritePlayerLine(playerMessage);
            StoryLog.WriteNpcLine(_commonerProfile.Name, npcLine);

            // Only take_gold is honoured for commoners — nothing else persists.
            if (response?.Actions != null)
               foreach (GameAction action in response.Actions)
               {
                  if (!string.Equals(action.Type, "take_gold", StringComparison.OrdinalIgnoreCase))
                     continue;

                  ActionOutcome outcome = behavior.GameStateBridge
                                                  .ExecuteAction(action, null, new ActionContext(_commonerProfile, null));
                  if (outcome != null)
                     AppendSystemMessage(outcome.Message, outcome.IsPositive);

                  if (action.Parameters.TryGetValue("amount", out string ats)
                      && int.TryParse(ats, out int at))
                     StoryLog.WriteGoldTransfer(_commonerProfile.Name, at, false);
               }
         }
         catch (Exception ex)
         {
            if (placeholder != null)
               ReplacePlaceholder(placeholder, "(The stranger looks away...)", null);
            ModLog.Error($"RunCommonerExchangeAsync threw {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            IsSendEnabled = !_conversationEnded;
         }
      }

      /// <summary>
      ///   Fires one constrained beat for the escape outcome: on success the captor reacts to
      ///   losing the prisoner and the scene ends (freeing already committed); on failure the
      ///   captor reasserts and control returns to the player. Falls back to a generic line if
      ///   the LLM call fails or refuses.
      /// </summary>
      private async Task RunEscapeOutcomeAsync(ChatSpeaker speaker, bool freed)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = speaker?.Profile;

         try
         {
            if (profile == null || behavior?.LlmServices == null || !behavior.LlmServices.IsAvailable)
            {
               ApplyEscapeOutcomeFallback(speaker, freed, profile);

               return;
            }

            string stimulus = freed
               ? "[The prisoner has just torn free of your grip and bolted into the dark — and they are GONE, "
                 + "beyond any chance of recapture tonight. You FAILED to hold them. React in character to losing "
                 + "them this very instant: fury, a shout, a curse, a too-late order to give chase. A few lines "
                 + "only. Then it is over — emit end_conversation.]"
               : "[The prisoner lunged to break free and FAILED — you caught them before they got anywhere. "
                 + "Reassert your control, make them feel the cost of the attempt, and carry the scene on.]";

            ChatSession session = BuildSessionFor(speaker);
            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetSeasonName(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Rumors = ""
            };
            EncounterContext context = EncounterContextBuilder.Build(
               speaker.Hero, profile, behavior.GameStateBridge,
               null,
               BuildContextWitnesses(speaker),
               isCollectiveCaptiveScene: _captiveEncounterIsCollective,
               playerStatusOverride: PlayerStatusVsNpc.Captive,
               captiveIntent: _captiveEncounterIntent,
               captorIsBandit: _captiveEncounterIsBandit,
               sceneStage: CaptiveSceneStage.Conclude); // the escape beat is the scene's end

            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            ChatManager.ScrollToBottom();

            NpcChatResult result;
            using (var cts =
                   new CancellationTokenSource())
            {
               try
               {
                  result = await Task.Run(() =>
                                        behavior.LlmServices.ChatService.ChatAsync(
                                           profile, world, session, stimulus, context, cts.Token))
                                     .ConfigureAwait(false);
               }
               catch (Exception netEx)
               {
                  result = NpcChatResult.Failure(netEx.Message);
               }
            }

            await MainThreadDispatcher.SwitchToMainThread();

            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

            string npcLine = result.IsSuccess && !string.IsNullOrWhiteSpace(result.Response?.Dialogue)
               ? result.Response.Dialogue
               : null;
            bool usable = !string.IsNullOrWhiteSpace(npcLine)
                          && !LooksLikeRefusal(npcLine)
                          && !LooksLikeRefusal(result.Response?.Narration);

            if (usable)
            {
               ReplacePlaceholder(placeholder, npcLine, speaker);
               RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
               StoryLog.WriteNpcLine(speaker.Name, npcLine);
               if (!string.IsNullOrWhiteSpace(result.Response?.Narration))
                  AppendNarration(result.Response.Narration);
            }
            else
            {
               ReplacePlaceholder(placeholder, freed
                  ? Pick(EscapeSuccessFallbacks)
                  : Pick(EscapeFailureFallbacks), speaker);
               ModLog.Info($"[ESCAPE] outcome beat unusable — generic fallback ({(freed ? "success" : "failure")}).");
            }

            if (freed) EndSceneAfterEscape(profile);
            else IsSendEnabled = !_conversationEnded;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ESCAPE] outcome beat threw {ex.GetType().Name}: {ex.Message}");
            if (freed) EndSceneAfterEscape(profile);
            else IsSendEnabled = true;
         }
      }

      private async Task RunLlmExchangeAsync(string playerMessage,
                                             ChatSpeaker speaker,
                                             ChatMessageViewModel placeholder,
                                             bool suppressPlayerLine = false)
      {
         try
         {
            // ── Commoner mode (v1.1): slim transient path, no memory ───────
            if (_mode == ChatMode.Commoner)
            {
               await RunCommonerExchangeAsync(playerMessage, placeholder);

               return;
            }
            // ─────────────────────────────────────────────────────────────────

            var behavior = NpcMemoryCampaignBehavior.Instance;

            // Route to whoever the player addressed (main NPC or a witness) and rebuild that
            // speaker's session from the attributed transcript (correct attribution).
            NpcProfile profile = speaker.Profile;
            ChatSession session = BuildSessionFor(speaker);

            if (behavior == null || profile == null || session == null)
            {
               ReplacePlaceholder(placeholder, "(I can't seem to gather my thoughts...)", speaker);
               ModLog.Error("LLM pipeline: behavior/profile/session not initialized.");

               return;
            }

            // Captive scene structure: a real player turn here only happens via "Try something
            // else" — the player is developing/reorienting the roleplay at THIS stage, so the
            // director stays put and flags the captor to react. The story only advances on
            // "Continue" (the continuation path).
            if (IsCaptiveEncounter && !suppressPlayerLine)
               _director?.DevelopInPlace();

            // Rebuild context each turn so it reflects events added during this session
            // (avoids stale "never met" after the first exchange adds a FirstMeeting event).
            // The witness list is everyone EXCEPT the current speaker (main NPC included
            // when a witness holds the floor).
            EncounterContext context = EncounterContextBuilder.Build(
               speaker.Hero, profile, behavior.GameStateBridge, playerMessage,
               BuildContextWitnesses(speaker), _privacyRequestPending,
               isCollectiveCaptiveScene: IsCaptiveEncounter && _captiveEncounterIsCollective,
               // Force Captive when this is a captive encounter — robust for the real
               // path and required for the console test path (player isn't truly imprisoned).
               playerStatusOverride: IsCaptiveEncounter
                  ? PlayerStatusVsNpc.Captive
                  : null,
               captiveIntent: _captiveEncounterIntent,
               captorIsBandit: IsCaptiveEncounter && _captiveEncounterIsBandit,
               sceneStage: _director?.CurrentStage ?? CaptiveSceneStage.Intro,
               aggressorKind: _director?.CurrentAggressor ?? CaptiveAggressorKind.Lead,
               reactToPlayerIntervention: _director?.ReactToIntervention ?? false);

            if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
            {
               ReplacePlaceholder(placeholder,
                  behavior.LlmServices?.DescribeUnavailability()
                  ?? "(The AI services are not available — check CalradiaRemembers.log.)", speaker);
               ModLog.Error("LLM pipeline: LLM services unavailable (no API key or model?).");

               return;
            }

            ModLog.Info($"Player → {profile.Name}: \"{playerMessage}\"");

            WorldState world = new() {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetSeasonName(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Rumors = ""
            };

            // ── Mirror the clan-level game relation into the prompt's clan standing.
            //    The personal score (ReputationWithPlayer) is our own per-NPC value
            //    and is left untouched here — only change_relation moves it. ──
            profile.ClanRelationWithPlayer = behavior.GameStateBridge.GetPlayerRelation(speaker.Hero);
            if (speaker.IsMain)
               SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer.Value);

            // Sync the player-authored backstory (flavor only) from the overrides file each turn,
            // so it is never persisted stale and reflects edits + the MCM toggle live.
            profile.AuthoredBackstory = CharacterOverrideLoader.BackstoryFor(speaker.Hero);

            // ── Diagnostic: log the full system prompt sent to the LLM ─────
            if (ModSettings.VerboseLogging)
               try
               {
                  IPromptBuilder promptBuilder = behavior.LlmServices.ChatService.PromptBuilder;
                  string systemPrompt = promptBuilder.BuildSystemPrompt(profile, world, context);
                  ModLog.Info("=== SYSTEM PROMPT START ===");
                  ModLog.Info(systemPrompt);
                  ModLog.Info("=== SYSTEM PROMPT END ===");
               }
               catch (Exception ex)
               {
                  ModLog.Info($"(prompt logging failed: {ex.Message})");
               }

            long startMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            NpcChatResult result;
            using (var cts = new CancellationTokenSource())
            {
               // Absorb any fault into a Failure result so the SwitchToMainThread below
               // ALWAYS runs — otherwise a throw here would land the catch off-thread.
               try
               {
                  result = await Task.Run(() =>
                                        behavior.LlmServices.ChatService.ChatAsync(
                                           profile, world, session, playerMessage, context, cts.Token))
                                     .ConfigureAwait(false);
               }
               catch (Exception netEx)
               {
                  result = NpcChatResult.Failure(netEx.Message);
               }
            }

            // Resume on the main thread for ALL UI and game-state work below.
            await MainThreadDispatcher.SwitchToMainThread();

            // The chat may have closed or a new conversation replaced us while the call was
            // in flight — a stale continuation must not mutate UI or apply game actions.
            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

            long elapsedMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - startMs;

            if (!result.IsSuccess)
            {
               ReplacePlaceholder(placeholder, "(My words escape me...)", speaker);
               ModLog.Error($"LLM call failed in {elapsedMs} ms: {result.ErrorMessage}");
               NotifyLlmFailure();

               return;
            }

            ParsedResponse response = result.Response;
            // Empty when the model produced no spoken line; RenderNpcTurn below turns that into
            // a narration-only turn (no "(silence...)" bubble) when [NARRATION] is present.
            string npcLine = !string.IsNullOrWhiteSpace(response?.Dialogue)
               ? response.Dialogue
               : string.Empty;

            // ── Model refusal guard (captive scenes) ───────────────────────
            // A Hardcore scene can trip the model's safety, returning an out-of-character
            // refusal ("I must decline..."). Never show that under the captor's name —
            // conclude the scene in character instead, so immersion survives the guardrail.
            if (IsCaptiveEncounter
                && (LooksLikeRefusal(npcLine) || LooksLikeRefusal(response?.Narration)))
            {
               ConcludeCaptiveSceneAfterRefusal(placeholder, speaker, profile);

               return;
            }

            // ── Escape attempt (captive scenes) ────────────────────────────
            // The captor flagged the player trying to break free. The OUTCOME is fate's,
            // not the LLM's: the mod rolls it, frees the player on success, and owns the
            // resolution narration so the captor can never "win" the escape by narration.
            if (IsCaptiveEncounter && ResponseHasAction(response, "escape_attempt"))
            {
               ResolveEscapeAttempt(placeholder, speaker, profile, npcLine, response);

               return;
            }

            // Render the turn: dialogue bubble, or a narration-only turn (no empty bubble), or
            // a "(silence...)" fallback. npcLine is empty for a narration-only turn.
            npcLine = RenderNpcTurn(placeholder, speaker, response);
            if (IsCaptiveEncounter) _captiveBeatCount++;

            // Commit this turn to the attributed transcript — player first, then the
            // speaker's line — so every participant's next rebuilt session sees it
            // correctly attributed. (Done here, after the call, so BuildSessionFor
            // above did not double-count the current player message.)
            // suppressPlayerLine: the captive-opening turn carries a synthetic scene
            // cue as playerMessage that must never enter the history or story log.
            if (!suppressPlayerLine)
               RecordTranscript(true, "You", Hero.MainHero?.StringId, playerMessage);
            if (!string.IsNullOrEmpty(npcLine))
               RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);

            // Fallback-memory bookkeeping: a completed exchange counts as a turn;
            // a substantive [EVENT] means no fallback summary is needed at close.
            if (!suppressPlayerLine) _playerTurnsThisSession++;
            if (response.NewEventData != null
                && !string.IsNullOrWhiteSpace(response.NewEventData.Summary))
               _eventRecordedThisSession = true;

            // Story log: intro (lazy, once per conversation) + player line + NPC line.
            if (IsLiveConversation)
            {
               EnsureStoryIntro();
               if (!suppressPlayerLine) StoryLog.WritePlayerLine(playerMessage);
               if (!string.IsNullOrEmpty(npcLine)) StoryLog.WriteNpcLine(speaker.Name, npcLine);
            }

            // Scene narration ([NARRATION]) — explicit second-person physical action,
            // shown after the speaker's dialogue. Recorded to transcript + story log.
            if (!string.IsNullOrWhiteSpace(response.Narration))
               AppendNarration(response.Narration);

            // Witness reactions — displayed immediately after the speaker's line.
            // recordTranscript: false here; the 15C path commits them inside
            // RunNpcWitnessExchangeAsync AFTER the auto-reply, so they are not
            // double-counted in the session that feeds the follow-up LLM call.
            // The normal (no 15C) path commits them explicitly below.
            List<(string name, string heroId, string text)> witnessReactions = CollectWitnessReactions(response);
            foreach (var (wn, _, wt) in witnessReactions)
            {
               AppendWitnessReaction(wn, wt, false);
               ModLog.Info($"  [WITNESS] {wn}: \"{wt}\"");
            }

            int gameDay = behavior.GameStateBridge.GetCurrentGameDay();
            ProfileMutator.Apply(profile, response, gameDay);

            // Stance: the LLM's spoken nudges to trust/respect/fear — capped hard and rate-limited
            // (words only colour; deeds prove). Persisted by the Store.Set below after actions run.
            StanceService.ApplyWordShift(profile, response.StanceShift);

            // Jealousy (Sprint 24): a romantic act recorded this turn may provoke a staked
            // third party. Flirt always; intimacy only when adult content is enabled. Witnesses
            // present mean the act is telegraphed and the word spreads quickly.
            NotableEventType? actType = response.NewEventData?.Type;
            if (actType == NotableEventType.Flirt
                || actType == NotableEventType.Intimacy
                && ModSettings.AdultContentLevel != AdultContentLevel.Off)
            {
               Hero courted = Hero.FindFirst(h => h.StringId == profile.Id);
               if (courted != null)
                  JealousyBehavior.Instance?.OnRomanticAct(
                     courted,
                     actType == NotableEventType.Intimacy
                        ? RomanticActTier.Intimacy
                        : RomanticActTier.Flirt,
                     _witnesses.Count > 0,
                     gameDay);
            }

            // Negotiation Phase 3: a leveraged intimacy with the exploiter archetype — he now holds it
            // over the female player and may press the advantage in later talks (persisted on the profile).
            if (actType == NotableEventType.Intimacy)
            {
               Hero leveraged = Hero.FindFirst(h => h.StringId == profile.Id);
               if (leveraged != null && IntimacyBargainEligibility.IsEligible(leveraged))
                  profile.IntimacyLeverageHeld = true;
            }

            // ── Execute game actions emitted by the LLM ───────────────────
            // RelationGate may stamp profile.LastRelationGainHour, so persist
            // AFTER actions run (below), not before.
            if (response.Actions != null && response.Actions.Count > 0)
            {
               // Double-payment guard: when the LLM emits join_party it sometimes ALSO
               // emits take_gold for the same payment (the player mimed handing coins
               // over) — join_party already transfers the settled price, so honoring
               // both charges the player twice (observed in testing: 550 + 546).
               bool hasJoinParty = response.Actions.Any(a =>
                  string.Equals(a.Type, "join_party", StringComparison.OrdinalIgnoreCase));

               var actionContext = new ActionContext(profile, response.NewEventData?.Type) {
                  OfferedItemId = _offeredItemId
               };
               foreach (GameAction action in response.Actions)
               {
                  // request_privacy, end_conversation and witness_leaves are
                  // chat-flow controls, handled below — not bridge actions.
                  if (string.Equals(action.Type, "request_privacy",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  if (string.Equals(action.Type, "end_conversation",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  if (string.Equals(action.Type, "witness_leaves",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  if (hasJoinParty
                      && string.Equals(action.Type, "take_gold",
                         StringComparison.OrdinalIgnoreCase))
                  {
                     ModLog.Info("[ACTION] take_gold dropped — join_party in the same response already transfers the hire payment.");

                     continue;
                  }

                  ActionOutcome outcome =
                     behavior.GameStateBridge.ExecuteAction(action, speaker.Hero, actionContext);
                  if (outcome != null) AppendSystemMessage(outcome.Message, outcome.IsPositive);

                  // Story log: narrative description of game-world consequences.
                  if (string.Equals(action.Type, "change_relation", StringComparison.OrdinalIgnoreCase))
                  {
                     if (action.Parameters.TryGetValue("delta", out string ds) && int.TryParse(ds, out int d))
                        StoryLog.WriteRelationChange(profile.Name, d);
                  }
                  else if (string.Equals(action.Type, "give_gold", StringComparison.OrdinalIgnoreCase))
                  {
                     if (action.Parameters.TryGetValue("amount", out string ags) && int.TryParse(ags, out int ag))
                        StoryLog.WriteGoldTransfer(profile.Name, ag, true);
                  }
                  else if (string.Equals(action.Type, "take_gold", StringComparison.OrdinalIgnoreCase))
                  {
                     if (action.Parameters.TryGetValue("amount", out string ats) && int.TryParse(ats, out int at))
                        StoryLog.WriteGoldTransfer(profile.Name, at, false);
                  }
               }

               // The personal score may have moved — refresh the header (main NPC only;
               // the header reflects the conversation's host, not a routed witness).
               if (speaker.IsMain)
                  SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer ?? 0);

               // Conversation-outcome rumor (v1.2): a steep affront before witnesses spreads
               // as gossip through the world-event store instead of evaporating at close.
               if (speaker.IsMain && _witnesses.Count > 0)
                  ConversationEventFeeder.RecordPublicAffront(speaker.Hero, response, _witnesses.Count);
            }

            // ── Companion dismissal (witness_leaves) ──────────────────────
            HandleWitnessLeaves(response);

            // ── Privacy request resolution ────────────────────────────────
            // The request may come from the Request Private button (the pending
            // flag adds a strong directive to the prompt) or from the player
            // simply asking in their own words — the NPC is taught the action
            // whenever witnesses are present, so honor it however it was raised.
            _privacyRequestPending = false;
            if (response.Actions != null)
               foreach (GameAction action in response.Actions)
               {
                  if (!string.Equals(action.Type, "request_privacy",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  action.Parameters.TryGetValue("result", out string privacyResult);
                  bool privacyAccepted = string.Equals(privacyResult, "accepted",
                     StringComparison.OrdinalIgnoreCase);
                  if (privacyAccepted)
                  {
                     _witnesses.Clear();
                     _currentSpeakerHeroId = null; // no witnesses left — back to the main NPC
                     RefreshWitnessUi();
                     ModLog.Info("[WITNESSES] private audience accepted — witnesses cleared.");
                  }
                  else
                  {
                     ModLog.Info("[WITNESSES] private audience refused — witnesses remain.");
                  }

                  break;
               }
            // ─────────────────────────────────────────────────────────────

            // ── Conversation end resolution ───────────────────────────────
            // Two paths lock input so the player can only leave:
            // 1. end_conversation action (NPC refuses — anger, insult, etc.)
            // 2. farewell event (NPC naturally closes the exchange)
            if (response.Actions != null)
               foreach (GameAction action in response.Actions)
               {
                  if (!string.Equals(action.Type, "end_conversation",
                         StringComparison.OrdinalIgnoreCase)) continue;

                  // A captive scene is the captor's to end only when it concludes. The model
                  // sometimes emits end_conversation on the intro/early beats (it poses a choice
                  // to the prisoner and treats it as a close) — honouring it cut the scene off the
                  // instant it began ("…has ended the conversation" right after the opening).
                  // Ignore a premature end: the scene's own Continue / Try-something pause carries
                  // it forward, and the conclusion backstop (the director concluding, or the
                  // beat-cap) ends it properly when the time actually comes.
                  if (IsCaptiveEncounter && _director?.IsConcluding != true)
                  {
                     ModLog.Info("[CAPTIVE] ignored a premature end_conversation — the scene is not concluding yet.");

                     continue;
                  }

                  _conversationEnded = true;
                  IsSendEnabled = false;
                  IsPrivateRequestVisible = false;
                  CloseButtonText = "Leave";
                  if (IsCaptiveEncounter) IsPleaseContinueVisible = true;
                  OnPropertyChanged(nameof(IsOfferItemVisible));
                  AppendSystemMessage($"{profile.Name} has ended the conversation.", false);
                  ModLog.Info($"[CONVERSATION] {profile.Name} ended the conversation.");

                  break;
               }

            if (!_conversationEnded
                && response.NewEventData?.Type == NotableEventType.Farewell)
            {
               _conversationEnded = true;
               IsSendEnabled = false;
               IsPrivateRequestVisible = false;
               CloseButtonText = "Leave";
               OnPropertyChanged(nameof(IsOfferItemVisible));
               ModLog.Info("[CONVERSATION] farewell event — input locked.");
            }
            // ─────────────────────────────────────────────────────────────

            // ── Informal quests: issue a newly offered task, pay out a verified one ──
            QuestService.ApplyResponse(
               profile, speaker.Hero, response, gameDay,
               behavior.GameStateBridge, behavior.Store,
               (text, positive) => AppendSystemMessage(text, positive));

            behavior.Store.Set(profile);

            // Mark any delivered player letters read now that the NPC has responded.
            PlayerLetterService.MarkLettersRead(profile, behavior.Store);

            // ── Sprint 15C: NPC auto-replies once to witness reactions ─────
            // When the main NPC held the floor and witnesses reacted, give them one
            // automatic counter-reply before returning control to the player.
            // Cap: once per player turn (_witnessExchangesThisRound reset in ExecuteSendMessage).
            // Only fires when the main NPC was the speaker (not a 15B witness turn).
            // NOT in captive scenes: there the captor's [WITNESS_REACTION] blocks already show
            // the band's participation inline, and an NPC↔witness auto-loop on top of that fired
            // up to 3 extra captor turns per player message — walls of text, the player rarely
            // getting to reply, and the Continue/Try-something pause never reached. Captive scenes
            // use the continuation pause below instead.
            if (witnessReactions.Count > 0
                && speaker.IsMain
                && !IsCaptiveEncounter
                && _witnessExchangesThisRound < WitnessExchangeCap
                && !_conversationEnded)
            {
               _witnessExchangesThisRound++;
               _npcWitnessExchangePending = true;
               _ = RunNpcWitnessExchangeAsync(witnessReactions, speaker, behavior);

               return; // finally runs but input stays locked (_npcWitnessExchangePending)
            }

            // Normal path (no 15C follow-up): commit reactions to attributed transcript.
            foreach (var (wName, wHeroId, wText) in witnessReactions)
               RecordTranscript(false, wName, wHeroId, wText);
            // ─────────────────────────────────────────────────────────────────

            // ── Captive scene conclusion / backstop ───────────────────────────
            // The director reached its final stage and the captor just played the Conclude beat:
            // end the scene cleanly (the beat produced its own ending). A separate paranoia cap
            // force-ends a runaway scene with a time-skip summary.
            if (IsCaptiveEncounter && !_conversationEnded && !_npcWitnessExchangePending)
            {
               if (_captiveBeatCount >= CaptiveBeatSafetyCap)
               {
                  ForceConcludeLongScene(profile);

                  return;
               }

               if (_director?.IsConcluding == true)
               {
                  EndCaptiveSceneSoftly(profile);

                  return;
               }
            }

            // ── Captive scene: ALWAYS offer the controls ──────────────────────
            // After every captor beat the player gets Continue (advance the story) or Try
            // something else (reorient/deepen at this stage). Unconditional — does not depend on
            // the model emitting [NARRATION] or avoiding a question, so it is robust across every
            // model (DeepSeek, Mimo, etc. all behave the same now).
            if (IsCaptiveEncounter && !_conversationEnded && !_npcWitnessExchangePending)
            {
               _scenePauseSpeaker = MainSpeaker();
               _scenePauseBehavior = behavior;
               IsScenePaused = true;

               return; // finally runs; input stays locked via _isScenePaused guard
            }

            _sceneContinuations = 0;
            // ─────────────────────────────────────────────────────────────────

            // ── Diagnostic: log full response (no truncation) ──────────────
            if (ModSettings.VerboseLogging)
            {
               ModLog.Info("=== NPC RESPONSE START ===");
               ModLog.Info(npcLine);
               ModLog.Info("=== NPC RESPONSE END ===");
               ModLog.Info($"({elapsedMs} ms)");
            }
            else
            {
               ModLog.Info($"{profile.Name} → \"{npcLine}\" ({elapsedMs} ms)");
            }

            if (response.NewEventData != null)
               ModLog.Info($"  [EVENT] {response.NewEventData.Type}: {response.NewEventData.Summary}");
            if (response.Reputation != null)
               ModLog.Info($"  [REP] clan={response.Reputation.ClanDelta} faction={response.Reputation.FactionDelta}");
            if (response.Actions != null && response.Actions.Count > 0)
               ModLog.Info($"  [ACTIONS] {response.Actions.Count} action(s) dispatched.");
            if (result.Usage != null)
               ModLog.Info($"  [TOKENS] prompt={result.Usage.PromptTokens} (cached:{result.Usage.CachedPromptTokens ?? 0}) | completion={result.Usage.CompletionTokens}");
            // Surface a non-"stop" finish reason — "length" means the model cut the reply
            // off (a known DeepSeek quirk); this makes truncated-message reports diagnosable.
            if (!string.IsNullOrEmpty(result.FinishReason)
                && !string.Equals(result.FinishReason, "stop", StringComparison.OrdinalIgnoreCase))
               ModLog.Info($"  [FINISH] reason={result.FinishReason} (non-stop — reply may be truncated by the model/provider)");
         }
         catch (Exception ex)
         {
            ReplacePlaceholder(placeholder, "(My voice falters...)", speaker);
            ModLog.Error($"RunLlmExchangeAsync threw {ex.GetType().Name}: {ex.Message}");
            ModLog.Error($"Stack: {ex.StackTrace}");
         }
         finally
         {
            // A bag offer is consumed by the turn it was sent with — clear it so a later, unrelated
            // give_item never re-transfers the previously picked item.
            _offeredItemId = null;

            // Keep input locked if the NPC ended the conversation, if a 15C witness-exchange
            // is in flight, if a scene continuation is pending, or if we are paused waiting
            // for the player to press Continue / Try something. Each async path re-enables
            // input in its own finally when it terminates.
            if (!_npcWitnessExchangePending && !_sceneContinuationPending && !_isScenePaused)
               IsSendEnabled = !_conversationEnded;
         }
      }

      /// <summary>
      ///   Sprint 15C: automatic NPC counter-reply to witness reactions (continuation).
      ///   Reactions are displayed immediately and committed to the transcript just
      ///   before each LLM call so the session history never double-counts them.
      ///   Re-enables player input in its <c>finally</c> block.
      /// </summary>
      private async Task RunNpcWitnessExchangeAsync(
         List<(string name, string heroId, string text)> firstReactions,
         ChatSpeaker mainSpeaker,
         NpcMemoryCampaignBehavior behavior)
      {
         List<(string name, string heroId, string text)> currentReactions = firstReactions;

         try
         {
            NpcProfile profile = mainSpeaker.Profile;

            if (profile == null || behavior?.LlmServices == null || !behavior.LlmServices.IsAvailable)
            {
               foreach (var (n, h, t) in currentReactions) RecordTranscript(false, n, h, t);

               return;
            }

            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetSeasonName(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Rumors = ""
            };

            while (true)
            {
               bool isLast = _witnessExchangesThisRound >= WitnessExchangeCap;

               // Session built WITHOUT currentReactions — they are not committed yet.
               // They travel as the stimulus so the LLM sees them as the final user turn.
               ChatSession session = BuildSessionFor(mainSpeaker);
               string stimulus = string.Join("\n",
                  currentReactions.Select(r => $"[{r.name}]: {r.text}"));

               EncounterContext context = EncounterContextBuilder.Build(
                  mainSpeaker.Hero, profile, behavior.GameStateBridge,
                  null,
                  BuildContextWitnesses(mainSpeaker),
                  false,
                  true,
                  isLast,
                  captiveIntent: _captiveEncounterIntent,
                  captorIsBandit: IsCaptiveEncounter && _captiveEncounterIsBandit);
               // (15C witness exchange is gated off for captive scenes, so no stage is fed here.)

               ChatMessageViewModel placeholder = MakeSpeakerMessage(mainSpeaker, PlaceholderText);
               _messages.Add(placeholder);
               ChatManager.ScrollToBottom();

               NpcChatResult result;
               using (var cts =
                      new CancellationTokenSource())
               {
                  try
                  {
                     result = await Task.Run(() =>
                                           behavior.LlmServices.ChatService.ChatAsync(
                                              profile, world, session, stimulus, context, cts.Token))
                                        .ConfigureAwait(false);
                  }
                  catch (Exception netEx)
                  {
                     result = NpcChatResult.Failure(netEx.Message);
                  }
               }

               // Back to the main thread for the UI / transcript / game-state work below.
               await MainThreadDispatcher.SwitchToMainThread();

               if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

               // Commit currentReactions before the NPC reply so the transcript order
               // is: witness → NPC, matching what was displayed on screen.
               foreach (var (n, h, t) in currentReactions)
                  RecordTranscript(false, n, h, t);

               if (!result.IsSuccess)
               {
                  ReplacePlaceholder(placeholder, "(The moment passes in silence.)", mainSpeaker);
                  ModLog.Error($"[15C] exchange {_witnessExchangesThisRound} failed: {result.ErrorMessage}");

                  break;
               }

               string npcReply = !string.IsNullOrWhiteSpace(result.Response?.Dialogue)
                  ? result.Response.Dialogue
                  : "(silence...)";

               ReplacePlaceholder(placeholder, npcReply, mainSpeaker);
               RecordTranscript(false, mainSpeaker.Name, mainSpeaker.Hero?.StringId, npcReply);
               StoryLog.WriteNpcLine(mainSpeaker.Name, npcReply);

               if (!string.IsNullOrWhiteSpace(result.Response?.Narration))
                  AppendNarration(result.Response.Narration);

               ModLog.Info($"[15C:{_witnessExchangesThisRound}] {mainSpeaker.Name}: \"{npcReply}\"");
               if (result.Usage != null)
                  ModLog.Info($"  [15C TOKENS] prompt={result.Usage.PromptTokens} | completion={result.Usage.CompletionTokens}");

               // End-conversation action is honored even in auto-exchange.
               if (result.Response?.Actions != null)
                  foreach (GameAction action in result.Response.Actions)
                  {
                     if (!string.Equals(action.Type, "end_conversation",
                            StringComparison.OrdinalIgnoreCase)) continue;
                     _conversationEnded = true;
                     IsPrivateRequestVisible = false;
                     CloseButtonText = "Leave";
                     OnPropertyChanged(nameof(IsOfferItemVisible));
                     AppendSystemMessage($"{profile.Name} has ended the conversation.", false);
                     ModLog.Info($"[15C] {profile.Name} ended the conversation.");

                     break;
                  }

               // A companion may take their leave mid-beat too (e.g. the player
               // asked them to step out and the NPC acknowledged it this turn).
               HandleWitnessLeaves(result.Response);

               if (_conversationEnded || isLast) break;

               // Collect new witness reactions from this response.
               // Display them immediately (without committing) — they become
               // the next iteration's currentReactions and stimulus.
               List<(string name, string heroId, string text)> nextReactions = CollectWitnessReactions(result.Response);

               if (nextReactions.Count == 0) break; // witnesses fell silent — natural end

               foreach (var (n, _, t) in nextReactions)
                  AppendWitnessReaction(n, t, false);

               currentReactions = nextReactions;
               _witnessExchangesThisRound++;
            }
         }
         catch (Exception ex)
         {
            foreach (var (n, h, t) in currentReactions) RecordTranscript(false, n, h, t);
            ModLog.Error($"[15C] RunNpcWitnessExchangeAsync threw {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            _npcWitnessExchangePending = false;
            IsSendEnabled = !_conversationEnded;
         }
      }

      /// <summary>
      ///   Hands the chosen goods to the bridge, which enforces the value floor and — if met —
      ///   removes them and honors the bargain's favor. On success the quest is closed and the
      ///   NPC accepts the goods in their own voice (one LLM turn, written to the story log)
      ///   rather than a bare mechanical line — falling back to the plain confirmation when the
      ///   LLM is unavailable. A short offer keeps the plain refusal line.
      /// </summary>
      private void SettleItemBargain(InformalQuest bargain, List<ItemOffer> offers)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile giver = behavior?.DialogFlow?.Profile;

            if (behavior?.GameStateBridge == null || giver == null) return;

            // Resolve the goods' display names BEFORE the bridge removes them from the
            // roster, so the NPC's acceptance line can name what it was paid with.
            string goods = DescribeOfferedGoods(offers);

            ActionOutcome outcome = behavior.GameStateBridge
                                            .SettleItemBargain(giver, _npcHero, bargain, offers);

            if (outcome == null) return;

            // Short offer — the deal didn't close; nothing was removed. Keep the plain line.
            if (!outcome.IsPositive)
            {
               AppendSystemMessage(outcome.Message, false);

               return;
            }

            // Deal closed: stamp the bargain settled and drop the bag button.
            int day = behavior.GameStateBridge.GetCurrentGameDay();
            QuestService.MarkBargainSettled(giver, bargain, _npcHero, day, behavior.Store);
            OnPropertyChanged(nameof(IsOfferItemVisible));

            // Narrative beat instead of a bare green line: the NPC accepts the goods in
            // their own voice (shown under their portrait, written to the story log) via
            // one LLM turn driven by a synthetic stage cue. Fall back to the mechanical
            // confirmation when the LLM is unavailable so the player still sees the result.
            if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
            {
               AppendSystemMessage(outcome.Message, true);

               return;
            }

            ChatSpeaker speaker = MainSpeaker();
            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            IsSendEnabled = false;
            _ = RunLlmExchangeAsync(
               BuildBargainAcceptanceStimulus(goods), speaker, placeholder, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[OFFER] SettleItemBargain failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private ChatSpeaker SpeakerForCurrent()
      {
         if (string.IsNullOrEmpty(_currentSpeakerHeroId)) return MainSpeaker();

         WitnessEntry w = _witnesses.FirstOrDefault(x => x.HeroStringId == _currentSpeakerHeroId);
         Hero h = w?.HeroStringId != null
            ? Hero.FindFirst(x => x.StringId == w.HeroStringId)
            : null;
         NpcProfile p = w?.HeroStringId != null
            ? NpcMemoryCampaignBehavior.Instance?.Store?.Get(w.HeroStringId)
            : null;

         if (w == null || h == null || p == null)
         {
            _currentSpeakerHeroId = null; // addressee gone — revert to main NPC

            return MainSpeaker();
         }

         _witnessColors.TryGetValue(w.Name, out string color);

         return new ChatSpeaker {
            IsMain = false,
            Hero = h,
            Name = w.Name,
            Profile = p,
            Color = color ?? WitnessPalette[0]
         };
      }

      #endregion
   }
}