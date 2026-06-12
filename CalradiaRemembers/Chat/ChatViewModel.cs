// Code written by Gabriel Mailhot, 26/05/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Captivity;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Chat
{
    public sealed class ChatViewModel : ViewModel
    {
        private const string PlaceholderText = "(considering...)";

        // System notification line colors (relation/gold outcomes shown inline in chat).
        private const string SystemGainColor = "#9fe0a0FF"; // soft green — favorable
        private const string SystemLossColor = "#e0a0a0FF"; // soft red — unfavorable

        // Scene narration (second-person prose during captive/CNC scenes): pale violet,
        // distinct from dialogue, no portrait — reads like a novel's narration.
        private const string SceneNarrationColor = "#c0a8d0FF";

        private string _closeButtonText = "End conversation";
        private string _inputText = "";
        private bool _isSendEnabled = true;
        private MBBindingList<ChatMessageViewModel> _messages;
        private Hero _npcHero;
        private string _npcName = "the lord";
        private string _sendButtonText = "Send";
        private string _titleText = "Calradia Remembers";
        private string _relationSummary = "";

        // ── Chat mode ─────────────────────────────────────────────────────────
        // One window, four behaviors. A single mode value (instead of a trio of
        // booleans) so adding a mode (v1.1 commoners) means extending the enum and
        // reviewing the named predicates below — not hunting scattered flag checks.
        private enum ChatMode
        {
            Conversation,    // live dialogue with an NPC — witnesses, actions, story log
            LetterReply,     // replying to a received letter — send seals a new letter
            Compose,         // writing a NEW letter — no LLM call, sealed directly
            CaptiveEncounter // captor-led scene — the player is a prisoner (Sprint 17)
        }

        private ChatMode _mode = ChatMode.Conversation;

        // A spoken exchange happening "now" — transcript and story log apply.
        // Letter modes are correspondence: nothing is said aloud, nothing witnessed.
        private bool IsLiveConversation => _mode == ChatMode.Conversation
                                        || _mode == ChatMode.CaptiveEncounter;
        private bool IsCaptiveEncounter => _mode == ChatMode.CaptiveEncounter;
        // A captive does not command the room — the captor's soldiers leave only if
        // the captor wills it. Letters have no room to clear.
        private bool AllowsPrivacyRequest => _mode == ChatMode.Conversation;

        // ── Letter reply mode payload ─────────────────────────────────────────
        // The received letter being answered; after the player sends, the NPC's LLM
        // response is queued as a new PendingLetter rather than displayed in chat.
        private PendingLetter _replyLetter;
        private NpcProfile _replyProfile;

        // ── Letter compose mode payload ───────────────────────────────────────
        private NpcProfile _composeRecipientProfile;
        // Gold charged when the letter is sealed (0 = free, e.g. console test path).
        private int _composeCourierCost;

        // ── Multi-NPC witnesses ───────────────────────────────────────────────
        // Mutable list — authoritative for the session; cleared on privacy-accepted.
        // Passed into EncounterContext each turn so PromptBuilder can inject them.
        private readonly List<WitnessEntry> _witnesses = new List<WitnessEntry>();

        // Hash-based palette: each witness gets a deterministic color from their
        // HeroStringId so Martira is always the same shade across conversations.
        private static readonly string[] WitnessPalette = {
            "#88c890FF",  // soft green
            "#d4a84bFF",  // warm gold
            "#c87878FF",  // muted rose
            "#a08ac8FF"   // light purple
        };
        private readonly Dictionary<string, string> _witnessColors = new Dictionary<string, string>();
        // Set when the player clicks "Request Private" — injected once, then cleared.
        private bool _privacyRequestPending;
        // Set once the request is used (accepted or refused) — button never reappears.
        private bool _privacyRequestUsed;

        private string _witnessesText = "";
        private bool _hasWitnesses;
        private bool _isPrivateRequestVisible;

        // Set when the NPC emits an end_conversation action — locks input so the
        // player can only read the farewell and leave.
        private bool _conversationEnded;

        // ── Fallback conversation memory ──────────────────────────────────────
        // The LLM mostly emits [EVENT] on a natural farewell; if the player closes
        // the window mid-exchange, nothing was recorded and the NPC forgets the
        // conversation ever happened (tester report). These track whether the
        // fallback summary is needed when the chat closes.
        private bool _eventRecordedThisSession;   // any non-empty [EVENT] this session
        private int  _playerTurnsThisSession;     // completed player→NPC exchanges
        private bool _conversationMemoryFinalized; // idempotency guard

        // ── Sprint 15C: NPC→witness auto-exchange ─────────────────────────────
        // Maximum NPC→witness auto-exchanges per player turn. Each exchange is one
        // LLM call; the chain terminates early if witnesses fall silent.
        private const int WitnessExchangeCap = 3;
        // How many auto-exchanges have fired this player turn. Reset on every send.
        private int _witnessExchangesThisRound;
        // True while RunNpcWitnessExchangeAsync is in flight; prevents the
        // RunLlmExchangeAsync finally block from re-enabling input prematurely.
        private bool _npcWitnessExchangePending;

        // ── Sprint 17: captive encounter mode payload ────────────────────────
        // When opened from CaptiveEncounterService, the chat runs with the captor
        // as main NPC. The profile and CNC context are stored here rather than via
        // DialogFlow (which is only populated during vanilla conversations).
        private NpcProfile         _captiveEncounterProfile;
        private bool               _captiveEncounterIsCollective;
        private CaptiveSceneIntent _captiveEncounterIntent;
        private bool               _captiveMemoryFinalized; // idempotency guard for the end-of-scene memory record

        // Captive scene continuation — fires when the NPC emits NARRATION without asking a question.
        // Between iterations the player sees "Continue / Try something" buttons (IsScenePaused).
        // Pressing Continue fires one more LLM turn; Try something restores normal text input.
        // At the last beat (cap-1) the prompt is told to conclude and dismiss the prisoner so the
        // scene resolves gracefully rather than looping.
        private const int MaxSceneContinuations = 4;
        private int       _sceneContinuations   = 0;
        private bool      _sceneContinuationPending = false;

        // Pause state — stored context so ExecuteContinue can resume without parameters.
        private bool                      _isScenePaused;
        private ChatSpeaker               _scenePauseSpeaker;
        private NpcMemoryCampaignBehavior _scenePauseBehavior;

        public void ActivateCaptiveEncounterMode(
            NpcProfile captorProfile, bool isCollective,
            CaptiveSceneIntent intent = CaptiveSceneIntent.Interrogation)
        {
            _mode                          = ChatMode.CaptiveEncounter;
            _captiveEncounterProfile       = captorProfile;
            _captiveEncounterIsCollective  = isCollective;
            _captiveEncounterIntent        = intent;

            // Public humiliation: being abased before witnesses stains the player's standing.
            // Only collective scenes with a degrading intent count — there is no audience to
            // shame the player otherwise, and an interrogation/reward is not an abasement.
            if (isCollective && IsHumiliatingIntent(intent))
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

            string opener = BuildCaptiveOpener(isCollective, intent);
            _ = RunLlmExchangeAsync(opener, captor, placeholder, suppressPlayerLine: true);
        }

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
                default:
                    return "[The bound prisoner stands before you. Begin in your own voice.]";
            }
        }

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
        ///   Applies the standing penalty for being abased before witnesses and surfaces it
        ///   in the chat. The bridge only mutates the real campaign when the player is truly
        ///   a prisoner; console test scenes still show the message for verification.
        /// </summary>
        private void ApplyPublicHumiliationPenalty()
        {
            int witnessCount = _witnesses?.Count ?? 0;
            if (witnessCount <= 0) return;

            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.GameStateBridge == null) return;

            (int renownLoss, int influenceLoss) = behavior.GameStateBridge.ApplyPlayerPublicHumiliation(witnessCount);
            if (renownLoss <= 0 && influenceLoss <= 0) return;

            AppendSystemMessage(
                $"Word of this will spread. Your standing suffers — renown -{renownLoss}, influence -{influenceLoss}.",
                false);
        }

        // ── Sprint 17: captive scene memory ───────────────────────────────────

        /// <summary>
        ///   Records the captive scene in the captor's memory. Called once when the scene
        ///   closes (from <see cref="ChatManager.Close" />). Two stages:
        ///   (1) a base Captivity event recorded synchronously — guaranteed even if the
        ///       async summary never returns;
        ///   (2) a richer first-person summary generated asynchronously from the scene
        ///       transcript, which replaces the base summary on arrival.
        ///   Idempotent and a no-op outside captive mode.
        /// </summary>
        public void FinalizeCaptiveSceneMemory()
        {
            if (_captiveMemoryFinalized || !IsCaptiveEncounter) return;
            _captiveMemoryFinalized = true;

            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile profile = _captiveEncounterProfile;
            if (behavior?.Store == null || profile == null) return;

            int    gameDay     = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
            string intentVerb  = CaptiveIntentVerb(_captiveEncounterIntent);
            string baseSummary = BuildCaptiveBaseSummary(intentVerb);

            // Stage 1 — synchronous base capture.
            var baseEvent = new NotableEvent(gameDay, NotableEventType.Captivity, baseSummary);
            profile.Events.Add(baseEvent);
            behavior.Store.Set(profile);
            ModLog.Info($"[CAPTIVE MEMORY] base event recorded for {profile.Name}: \"{baseSummary}\"");

            // Stage 2 — async rich summary (fire-and-forget; replaces the base on success).
            string transcript = BuildSceneTranscriptText("Prisoner");
            if (string.IsNullOrWhiteSpace(transcript) || behavior.LlmServices?.LlmClient == null) return;

            bool playerIsFemale = Services.LlmServices.TestPlayerGenderOverride ?? Hero.MainHero?.IsFemale ?? false;
            _ = GenerateAndApplyCaptiveSummaryAsync(behavior, profile.Id, baseEvent, transcript, intentVerb, playerIsFemale);
        }

        private static async Task GenerateAndApplyCaptiveSummaryAsync(
            NpcMemoryCampaignBehavior behavior, string captorId, NotableEvent baseEvent,
            string transcript, string intentVerb, bool playerIsFemale)
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

                fresh.Events[idx] = fresh.Events[idx] with { summary = rich.Trim() };
                behavior.Store.Set(fresh);
                ModLog.Info($"[CAPTIVE MEMORY] rich summary applied for {captor.Name}: \"{rich.Trim()}\"");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[CAPTIVE MEMORY] summary generation failed: {ex.GetType().Name}: {ex.Message}");
            }
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

            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            NpcProfile profile = behavior?.DialogFlow?.Profile;
            if (behavior?.Store == null || profile == null) return;

            int    gameDay = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
            string player  = Hero.MainHero?.Name?.ToString() ?? "the player";

            // Stage 1 — synchronous base capture (guaranteed even if the async summary
            // never returns). Plain but truthful; better than the NPC forgetting.
            var baseEvent = new NotableEvent(gameDay, NotableEventType.Other,
                $"Spoke with {player}; the conversation broke off without a formal parting.");
            profile.Events.Add(baseEvent);
            behavior.Store.Set(profile);
            ModLog.Info($"[CONV MEMORY] fallback event recorded for {profile.Name} " +
                        $"({_playerTurnsThisSession} player turn(s), no [EVENT] emitted).");

            // Stage 2 — async rich summary (fire-and-forget; replaces the base on success).
            string transcript = BuildSceneTranscriptText(player);
            if (string.IsNullOrWhiteSpace(transcript) || behavior.LlmServices?.LlmClient == null) return;

            _ = GenerateAndApplyConversationSummaryAsync(behavior, profile.Id, baseEvent, transcript, player);
        }

        private static async Task GenerateAndApplyConversationSummaryAsync(
            NpcMemoryCampaignBehavior behavior, string npcId, NotableEvent baseEvent,
            string transcript, string playerName)
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

                fresh.Events[idx] = fresh.Events[idx] with { summary = rich.Trim() };
                behavior.Store.Set(fresh);
                ModLog.Info($"[CONV MEMORY] rich summary applied for {npc.Name}: \"{rich.Trim()}\"");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[CONV MEMORY] summary generation failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string CaptiveIntentVerb(CaptiveSceneIntent intent) => intent switch {
            CaptiveSceneIntent.Interrogation  => "interrogated",
            CaptiveSceneIntent.PersonalDesire => "took my pleasure from",
            CaptiveSceneIntent.Domination     => "dominated and subdued",
            CaptiveSceneIntent.Torture        => "tortured",
            CaptiveSceneIntent.Training       => "trained and conditioned",
            CaptiveSceneIntent.Reward         => "rewarded",
            _                                 => "kept as my prisoner"
        };

        private string BuildCaptiveBaseSummary(string intentVerb)
        {
            string player   = Hero.MainHero?.Name?.ToString() ?? "the player";
            string audience = _captiveEncounterIsCollective && (_witnesses?.Count ?? 0) > 0
                ? ", before my soldiers"
                : ", in private";
            return $"Held {player} as my captive and {intentVerb} them{audience}.";
        }

        /// <summary>
        ///   Renders the attributed transcript as plain text for a summarizer.
        ///   <paramref name="playerLabel" /> names the player's lines ("Prisoner" in
        ///   captive scenes, their actual name in ordinary conversations).
        /// </summary>
        private string BuildSceneTranscriptText(string playerLabel)
        {
            if (_transcript == null || _transcript.Count == 0) return "";

            var sb = new System.Text.StringBuilder();
            foreach (TranscriptEntry e in _transcript)
            {
                if (string.IsNullOrWhiteSpace(e.Text)) continue;
                string who = e.IsPlayer ? playerLabel : e.SpeakerName;
                sb.AppendLine($"{who}: {e.Text}");
            }
            return sb.ToString();
        }

        // ── Story Log ─────────────────────────────────────────────────────────
        // True once the conversation intro has been written this session (lazy —
        // deferred until the first player message so witnesses are already set).
        private bool   _storyIntroWritten;
        // Settlement or location description captured when the conversation opens.
        private string _storyLocation;

        // ── Sprint 15B: addressing a witness directly ─────────────────────────
        // Cream color matching ChatMessageViewModel's default NPC hue, used when the
        // main NPC appears as a reacting witness (while another participant speaks).
        private const string NpcCreamColor = "#e8d8b8FF";

        // Attributed conversation history — the single source of truth for rebuilding
        // each speaker's ChatSession per turn (their own lines as assistant, everyone
        // else's as "[Name]: …" user lines). Kept in lockstep with the displayed
        // conversational messages (player, main NPC, witnesses); system lines excluded.
        private readonly List<TranscriptEntry> _transcript = new List<TranscriptEntry>();

        // The witness the player is currently addressing (sticky). Null = main NPC.
        private string _currentSpeakerHeroId;

        public ChatViewModel()
        {
            _messages = new MBBindingList<ChatMessageViewModel>();
        }

        /// <summary>One attributed line of conversation history.</summary>
        private sealed class TranscriptEntry
        {
            public bool   IsPlayer;
            public string SpeakerName;
            public string SpeakerHeroId;
            public string Text;
        }

        /// <summary>
        ///   Resolved identity of whoever speaks this turn — the main NPC by default,
        ///   or a witness the player addressed. Carries the profile/hero/colour needed
        ///   to route the LLM call and render the reply under the right portrait.
        /// </summary>
        private sealed class ChatSpeaker
        {
            public bool       IsMain;
            public Hero       Hero;
            public string     Name;
            public NpcProfile Profile;
            public string     Color;   // null → default NPC cream
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

        /// <summary>Inverse of <see cref="IsScenePaused"/> — drives the normal InputBox + SendButton.</summary>
        [DataSourceProperty]
        public bool IsNormalInputVisible => !_isScenePaused;

        /// <summary>Updates the header subtitle with the two relationship scores.</summary>
        public void SetRelationSummary(int personal, int clan)
        {
            RelationSummary = $"Clan {clan:+#;-#;0}   ·   Personal {personal:+#;-#;0}";
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
                Hero   witnessHero = null;
                string color       = WitnessPalette[0]; // fallback
                string heroId      = null;

                if (string.Equals(witnessName, _npcName, StringComparison.OrdinalIgnoreCase))
                {
                    witnessHero = _npcHero;
                    color       = NpcCreamColor;
                    heroId      = _npcHero?.StringId;
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

                _messages.Add(new ChatMessageViewModel(witnessHero, witnessName, text, color));
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
                _messages.Add(new ChatMessageViewModel(_npcHero, _npcName, text, false));
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
        ///   Adds a system notification line to the chat (relation change, gold
        ///   transfer). Color-coded by whether the outcome favors the player.
        /// </summary>
        public void AppendSystemMessage(string text, bool isPositive)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _messages.Add(new ChatMessageViewModel(text, isPositive ? SystemGainColor : SystemLossColor));
                ChatManager.ScrollToBottom();
            }
            catch (Exception ex)
            {
                ModLog.Error($"AppendSystemMessage threw {ex.GetType().Name}: {ex.Message}");
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
        ///   Switches the chat into letter-reply mode. The received letter is shown as
        ///   the first message; when the player sends their reply the chat closes and the
        ///   NPC's generated response becomes a new <see cref="PendingLetter" />.
        /// </summary>
        public void ActivateLetterReplyMode(PendingLetter letter, NpcProfile profile)
        {
            _mode         = ChatMode.LetterReply;
            _replyLetter  = letter;
            _replyProfile = profile;

            TitleText       = $"Correspondence with {profile.Name}";
            CloseButtonText = "Discard";
            SendButtonText  = "Send reply";

            // Pre-load the received letter as the opening NPC message.
            if (!string.IsNullOrWhiteSpace(letter.Content))
                _messages.Add(new ChatMessageViewModel(_npcHero, profile.Name, letter.Content, false));
        }

        public void ExecuteClose()
        {
            OnCloseRequested?.Invoke();
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

        /// <summary>
        ///   Player requests a private audience — sets the injection flag then immediately
        ///   fires an LLM exchange so the NPC can accept or refuse in the same turn.
        ///   The synthetic player line is shown in the chat and sent to the LLM.
        /// </summary>
        public void ExecuteRequestPrivate()
        {
            if (_privacyRequestUsed || _witnesses.Count == 0) return;
            _privacyRequestPending = true;
            _privacyRequestUsed    = true;
            RefreshWitnessUi();
            ModLog.Info("[WITNESSES] player requested private audience.");
            // Trigger immediate exchange; _privacyRequestPending is already true so
            // EncounterContextBuilder will inject the privacy request into the prompt.
            InputText = "I would speak with you privately.";
            ExecuteSendMessage();
        }

        /// <summary>
        ///   Player chose "Continue" during a captive scene pause. Fires one more NPC-led
        ///   narration turn. Input stays locked until the NPC asks a question or the scene ends.
        /// </summary>
        public void ExecuteContinue()
        {
            if (!_isScenePaused || _scenePauseSpeaker == null || _scenePauseBehavior == null) return;
            IsScenePaused             = false;
            _sceneContinuations++;
            _sceneContinuationPending = true;
            IsSendEnabled             = false;
            _ = RunCaptiveSceneContinuationAsync();
        }

        /// <summary>
        ///   Player chose "Try something" during a captive scene pause. Restores the normal
        ///   text input so the player can type a response and break the scene's flow.
        /// </summary>
        public void ExecuteTrySomething()
        {
            if (!_isScenePaused) return;
            IsScenePaused        = false;
            _sceneContinuations  = 0;
            _scenePauseSpeaker   = null;
            _scenePauseBehavior  = null;
            IsSendEnabled        = !_conversationEnded;
        }

        /// <summary>
        ///   Switches the chat into compose mode: no LLM call, no NPC portrait.
        ///   The player types a message and "Seal &amp; Send" queues it as a
        ///   <see cref="PlayerLetter" /> with delivery delay. When <paramref name="courierCost" />
        ///   is positive, that many denars are charged on send (insufficient gold aborts).
        /// </summary>
        public void ActivateComposeMode(NpcProfile recipientProfile, int courierCost = 0)
        {
            _mode                    = ChatMode.Compose;
            _composeRecipientProfile = recipientProfile;
            _composeCourierCost      = courierCost;

            TitleText = courierCost > 0
                ? $"Compose Letter to {recipientProfile.Name} — {courierCost} denars"
                : $"Compose Letter to {recipientProfile.Name}";
            CloseButtonText = "Cancel";
            SendButtonText  = "Seal & Send";
        }

        public void ExecuteSendMessage()
        {
            var text = (InputText ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (!IsSendEnabled) return;

            // Compose mode: seal the letter without any LLM call.
            if (_mode == ChatMode.Compose)
            {
                SealAndSendLetter(text);
                return;
            }

            // Sprint 15B: route this turn to whoever the player is addressing — the main
            // NPC by default, or a witness named in a stage direction / vocative (sticky).
            ChatSpeaker speaker = _mode == ChatMode.LetterReply ? MainSpeaker() : ResolveAddressedSpeaker(text);
            RefreshWitnessUi(); // refresh the "Speaking to …" indicator

            // Display only — the player line is committed to the transcript after the call
            // so BuildSessionFor doesn't double-count it (ChatAsync adds it itself).
            AppendPlayerMessage(text, recordTranscript: false);
            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            IsSendEnabled = false;
            InputText = "";
            _witnessExchangesThisRound = 0; // reset the 15C cap for this new player turn
            _sceneContinuations        = 0; // reset scene-continuation count for this new player turn
            _ = RunLlmExchangeAsync(text, speaker, placeholder);
        }

        private void SealAndSendLetter(string content)
        {
            NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior == null || _composeRecipientProfile == null)
            {
                ChatManager.Close();
                return;
            }

            // Courier hire fee: refuse if the player cannot afford it (keep the window
            // open so the message isn't lost), otherwise deduct before dispatching.
            if (_composeCourierCost > 0)
            {
                if (Hero.MainHero.Gold < _composeCourierCost)
                {
                    MBInformationManager.AddQuickInformation(new TextObject(
                        $"You cannot afford the courier ({_composeCourierCost} denars)."));
                    return;
                }
                Hero.MainHero.ChangeHeroGold(-_composeCourierCost);
                ModLog.Info($"[PLAYER LETTER] courier fee charged: {_composeCourierCost} denars.");
            }

            int today        = behavior.GameStateBridge.GetCurrentGameDay();
            int deliveryDays = PlayerLetterService.CreateAndQueue(
                _composeRecipientProfile, content, today, behavior.Store);

            string days = deliveryDays == 1 ? "1 day" : $"{deliveryDays} days";
            MBInformationManager.AddQuickInformation(
                new TextObject($"Your courier departs. The letter will reach" +
                               $" {_composeRecipientProfile.Name} in approximately {days}."));

            StoryLog.WriteLetterSent(_composeRecipientProfile.Name, content, today);
            ModLog.Info($"[PLAYER LETTER] sealed to {_composeRecipientProfile.Name} ({deliveryDays}d delivery).");
            InputText = "";
            ChatManager.Close();
        }

        public void SetNpcHero(Hero npcHero)
        {
            _npcHero = npcHero;
            if (!string.IsNullOrWhiteSpace(npcHero?.Name?.ToString())) _npcName = npcHero.Name.ToString();
            TitleText = $"Calradia Remembers — {_npcName}";
            _storyIntroWritten = false;
            _storyLocation     = BuildStoryLocation();
        }

        public void SetNpcName(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)) return;
            _npcName = npcName;
            TitleText = $"Calradia Remembers — {npcName}";
        }

        #region private

        // ── Story Log helpers ─────────────────────────────────────────────────

        // Writes the conversation intro once — deferred until the first player
        // message so _witnesses is already populated by SetWitnesses.
        private void EnsureStoryIntro()
        {
            if (_storyIntroWritten) return;
            _storyIntroWritten = true;
            if (!StoryLog.IsEnabled) return;
            int day = (int)TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays;
            string[] witnesses = _witnesses.Select(w => w.Name).ToArray();
            StoryLog.WriteConversationIntro(_npcName, witnesses, _storyLocation, day);
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
                            : s.IsTown   ? "the town of"
                                         : "the village of";
                return $"{kind} {s.Name}";
            }
            catch { return null; }
        }

        private string ColorForWitness(WitnessEntry entry)
        {
            string key = entry.HeroStringId ?? entry.Name ?? "";
            int hash = System.Math.Abs(key.GetHashCode());
            return WitnessPalette[hash % WitnessPalette.Length];
        }

        private void RefreshWitnessUi()
        {
            bool any = _witnesses.Count > 0;
            string baseList = any ? "Present: " + string.Join(" · ", _witnesses.Select(w => w.Name)) : "";

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

            HasWitnesses            = any;
            IsPrivateRequestVisible = any && !_privacyRequestUsed && AllowsPrivacyRequest;
        }

        private void ReplacePlaceholder(ChatMessageViewModel placeholder, string newText, ChatSpeaker speaker)
        {
            var idx = _messages.IndexOf(placeholder);

            if (idx < 0) return;
            _messages.RemoveAt(idx);
            _messages.Insert(idx, MakeSpeakerMessage(speaker, newText));
            ChatManager.ScrollToBottom();
        }

        // ── Sprint 15B helpers ────────────────────────────────────────────────

        private void RecordTranscript(bool isPlayer, string name, string heroId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _transcript.Add(new TranscriptEntry {
                IsPlayer      = isPlayer,
                SpeakerName   = name,
                SpeakerHeroId = heroId,
                Text          = text
            });
        }

        private ChatMessageViewModel MakeSpeakerMessage(ChatSpeaker speaker, string text)
            => speaker.IsMain || string.IsNullOrEmpty(speaker.Color)
                ? new ChatMessageViewModel(speaker.Hero, speaker.Name, text, false)
                : new ChatMessageViewModel(speaker.Hero, speaker.Name, text, speaker.Color);

        private ChatSpeaker MainSpeaker()
        {
            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Profile
                ?? (IsCaptiveEncounter ? _captiveEncounterProfile : null);
            return new ChatSpeaker {
                IsMain  = true,
                Hero    = _npcHero,
                Name    = _npcName,
                Profile = profile,
                Color   = null
            };
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
                _currentSpeakerHeroId = detectedId == _npcHero?.StringId ? null : detectedId;

            return SpeakerForCurrent();
        }

        private ChatSpeaker SpeakerForCurrent()
        {
            if (string.IsNullOrEmpty(_currentSpeakerHeroId)) return MainSpeaker();

            WitnessEntry w = _witnesses.FirstOrDefault(x => x.HeroStringId == _currentSpeakerHeroId);
            Hero h = w?.HeroStringId != null ? Hero.FindFirst(x => x.StringId == w.HeroStringId) : null;
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
                IsMain  = false,
                Hero    = h,
                Name    = w.Name,
                Profile = p,
                Color   = color ?? WitnessPalette[0]
            };
        }

        /// <summary>
        ///   Builds the witness list to inject into <paramref name="speaker" />'s prompt:
        ///   every other participant. When a witness holds the floor, the main NPC is
        ///   added as a witness so the speaker knows whose audience they share.
        /// </summary>
        private System.Collections.Generic.IReadOnlyList<WitnessEntry> BuildContextWitnesses(ChatSpeaker speaker)
        {
            if (speaker.IsMain)
                return _witnesses.Count > 0 ? _witnesses.AsReadOnly() : null;

            var list = new List<WitnessEntry>();
            foreach (WitnessEntry w in _witnesses)
                if (w.HeroStringId != speaker.Hero?.StringId) list.Add(w);

            if (_npcHero != null)
                list.Add(new WitnessEntry {
                    Name              = _npcName,
                    RelationToNpc     = "the host of this conversation, standing beside you",
                    HeroStringId      = _npcHero.StringId,
                    IsPlayerCompanion = false,
                    Persona           = null
                });

            return list.Count > 0 ? list : null;
        }

        /// <param name="excludeLastN">
        ///   When non-zero, skips the last N entries of the transcript.
        ///   Used by Sprint 15C to build the NPC's session before the witness reactions
        ///   are committed, so those reactions appear only as the stimulus playerMessage
        ///   and are not double-counted in the session history.
        /// </param>
        private NpcMemoryService.Core.Models.ChatSession BuildSessionFor(ChatSpeaker speaker, int excludeLastN = 0)
        {
            var session = new NpcMemoryService.Core.Models.ChatSession();
            string speakerId = speaker.Hero?.StringId;
            int limit = System.Math.Max(0, _transcript.Count - excludeLastN);

            for (int i = 0; i < limit; i++)
            {
                TranscriptEntry e = _transcript[i];
                if (e.IsPlayer)
                    session.AddPlayerMessage(e.Text);
                else if (!string.IsNullOrEmpty(speakerId) && e.SpeakerHeroId == speakerId)
                    session.AddNpcMessage(e.Text);          // this speaker's own prior line
                else
                    session.AddWitnessStatement(e.SpeakerName, e.Text); // anyone else, attributed
            }

            return session;
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

        private async Task RunLlmExchangeAsync(string playerMessage, ChatSpeaker speaker, ChatMessageViewModel placeholder,
            bool suppressPlayerLine = false)
        {
            try
            {
                NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;

                // In letter reply mode the chat was opened outside of normal dialogue:
                // use the pre-loaded reply profile and a fresh session. Otherwise route to
                // whoever the player addressed (main NPC or a witness) and rebuild that
                // speaker's session from the attributed transcript (correct attribution).
                NpcProfile profile = _mode == ChatMode.LetterReply ? _replyProfile : speaker.Profile;
                ChatSession session = _mode == ChatMode.LetterReply
                    ? new ChatSession()
                    : BuildSessionFor(speaker);

                if (behavior == null || profile == null || session == null)
                {
                    ReplacePlaceholder(placeholder, "(I can't seem to gather my thoughts...)", speaker);
                    ModLog.Error("LLM pipeline: behavior/profile/session not initialized.");

                    return;
                }

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
                    playerStatusOverride: IsCaptiveEncounter ? PlayerStatusVsNpc.Captive : (PlayerStatusVsNpc?)null,
                    captiveIntent: _captiveEncounterIntent);

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
                    CurrentDay      = behavior.GameStateBridge.GetCurrentGameDay(),
                    Season          = behavior.GameStateBridge.GetSeasonName(),
                    ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
                    Rumors          = ""
                };

                // ── Mirror the clan-level game relation into the prompt's clan standing.
                //    The personal score (ReputationWithPlayer) is our own per-NPC value
                //    and is left untouched here — only change_relation moves it. ──
                profile.ClanRelationWithPlayer = behavior.GameStateBridge.GetPlayerRelation(speaker.Hero);
                if (speaker.IsMain)
                    SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer.Value);

                // ── Diagnostic: log the full system prompt sent to the LLM ─────
                if (ModSettings.VerboseLogging)
                    try
                    {
                        IPromptBuilder promptBuilder = behavior.LlmServices.ChatService.PromptBuilder;
                        var systemPrompt = promptBuilder.BuildSystemPrompt(profile, world, context);
                        ModLog.Info("=== SYSTEM PROMPT START ===");
                        ModLog.Info(systemPrompt);
                        ModLog.Info("=== SYSTEM PROMPT END ===");
                    }
                    catch (Exception ex)
                    {
                        ModLog.Info($"(prompt logging failed: {ex.Message})");
                    }

                var startMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

                NpcChatResult result;
                using (CancellationTokenSource cts = new CancellationTokenSource())
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

                var elapsedMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - startMs;

                if (!result.IsSuccess)
                {
                    ReplacePlaceholder(placeholder, "(My words escape me...)", speaker);
                    ModLog.Error($"LLM call failed in {elapsedMs} ms: {result.ErrorMessage}");

                    return;
                }

                ParsedResponse response = result.Response;
                var npcLine = !string.IsNullOrWhiteSpace(response?.Dialogue)
                    ? response.Dialogue
                    : "(silence...)";

                // ── Letter reply intercept ─────────────────────────────────────
                // In letter reply mode we don't show the NPC's response in chat.
                // Instead we package it as a new PendingLetter and close the window.
                if (_mode == ChatMode.LetterReply && _replyLetter != null && _replyProfile != null)
                {
                    _replyLetter.PlayerReply = playerMessage;

                    Hero giverHero = Hero.FindFirst(h => h.StringId == _replyProfile.Id);
                    LetterGenerationService.QueueReplyLetter(
                        _replyProfile, giverHero, _replyLetter,
                        behavior.LlmServices, behavior.Store,
                        behavior.GameStateBridge.GetCurrentGameDay());

                    ProfileMutator.Apply(profile, response, behavior.GameStateBridge.GetCurrentGameDay());
                    behavior.Store.Set(profile);

                    MBInformationManager.AddQuickInformation(
                        new TextObject($"Your reply to {_replyProfile.Name} has been sealed and sent."));

                    StoryLog.WriteLetterSent(_replyProfile.Name, playerMessage, behavior.GameStateBridge.GetCurrentGameDay());
                    ModLog.Info($"[LETTER] reply sent to {_replyProfile.Name}; NPC response queued as new letter.");
                    // We are inside a dispatched continuation (the pump is on the stack); defer
                    // the close one frame so it never disposes the panel mid-drain.
                    MainThreadDispatcher.RequestClose();
                    return;
                }
                // ── End letter reply intercept ─────────────────────────────────

                ReplacePlaceholder(placeholder, npcLine, speaker);

                // Commit this turn to the attributed transcript — player first, then the
                // speaker's line — so every participant's next rebuilt session sees it
                // correctly attributed. (Done here, after the call, so BuildSessionFor
                // above did not double-count the current player message.)
                // suppressPlayerLine: the captive-opening turn carries a synthetic scene
                // cue as playerMessage that must never enter the history or story log.
                if (!suppressPlayerLine)
                    RecordTranscript(true, "You", Hero.MainHero?.StringId, playerMessage);
                RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);

                // Fallback-memory bookkeeping: a completed exchange counts as a turn;
                // a substantive [EVENT] means no fallback summary is needed at close.
                if (!suppressPlayerLine) _playerTurnsThisSession++;
                if (response.NewEventData != null
                    && !string.IsNullOrWhiteSpace(response.NewEventData.Summary))
                    _eventRecordedThisSession = true;

                // Story log: intro (lazy, once per conversation) + player line + NPC line.
                // Skipped in letter modes — the player's message becomes a letter, which
                // is logged separately in the letter-reply intercept above.
                if (IsLiveConversation)
                {
                    EnsureStoryIntro();
                    if (!suppressPlayerLine) StoryLog.WritePlayerLine(playerMessage);
                    StoryLog.WriteNpcLine(speaker.Name, npcLine);
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
                var witnessReactions = CollectWitnessReactions(response);
                foreach (var (wn, _, wt) in witnessReactions)
                {
                    AppendWitnessReaction(wn, wt, recordTranscript: false);
                    ModLog.Info($"  [WITNESS] {wn}: \"{wt}\"");
                }

                var gameDay = behavior.GameStateBridge.GetCurrentGameDay();
                ProfileMutator.Apply(profile, response, gameDay);

                // ── Execute game actions emitted by the LLM ───────────────────
                // RelationGate may stamp profile.LastRelationGainHour, so persist
                // AFTER actions run (below), not before.
                if (response.Actions != null && response.Actions.Count > 0)
                {
                    ActionContext actionContext = new ActionContext(profile, response.NewEventData?.Type);
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
                                StoryLog.WriteGoldTransfer(profile.Name, ag, npcGives: true);
                        }
                        else if (string.Equals(action.Type, "take_gold", StringComparison.OrdinalIgnoreCase))
                        {
                            if (action.Parameters.TryGetValue("amount", out string ats) && int.TryParse(ats, out int at))
                                StoryLog.WriteGoldTransfer(profile.Name, at, npcGives: false);
                        }
                    }
                    // The personal score may have moved — refresh the header (main NPC only;
                    // the header reflects the conversation's host, not a routed witness).
                    if (speaker.IsMain)
                        SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer ?? 0);
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
                        action.Parameters.TryGetValue("result", out var privacyResult);
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
                        _conversationEnded      = true;
                        IsSendEnabled           = false;
                        IsPrivateRequestVisible = false;
                        CloseButtonText         = "Leave";
                        AppendSystemMessage($"{profile.Name} has ended the conversation.", false);
                        ModLog.Info($"[CONVERSATION] {profile.Name} ended the conversation.");
                        break;
                    }

                if (!_conversationEnded
                    && response.NewEventData?.Type == NotableEventType.Farewell)
                {
                    _conversationEnded      = true;
                    IsSendEnabled           = false;
                    IsPrivateRequestVisible = false;
                    CloseButtonText         = "Leave";
                    ModLog.Info("[CONVERSATION] farewell event — input locked.");
                }
                // ─────────────────────────────────────────────────────────────

                // ── Informal quests: issue a newly offered task, pay out a verified one ──
                Quests.QuestService.ApplyResponse(
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
                if (witnessReactions.Count > 0 && speaker.IsMain
                    && _witnessExchangesThisRound < WitnessExchangeCap && !_conversationEnded)
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

                // ── Captive scene continuation ────────────────────────────────────
                // When the NPC emitted a NARRATION block but did NOT end their dialogue
                // with a question, the scene is still unfolding — fire another NPC-led
                // turn automatically. The player cannot interrupt mid-act.
                // Cap prevents runaway loops; question detection uses the trailing '?'
                // heuristic (robust for the structured captive scene flow).
                if (IsCaptiveEncounter && !_conversationEnded && !_npcWitnessExchangePending
                    && !string.IsNullOrWhiteSpace(response?.Narration)
                    && !NpcAskedQuestion(npcLine)
                    && _sceneContinuations < MaxSceneContinuations)
                {
                    // Pause: show Continue / Try something buttons; wait for player choice.
                    _scenePauseSpeaker  = MainSpeaker();
                    _scenePauseBehavior = behavior;
                    IsScenePaused       = true;
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
                // Keep input locked if the NPC ended the conversation, if a 15C witness-exchange
                // is in flight, if a scene continuation is pending, or if we are paused waiting
                // for the player to press Continue / Try something. Each async path re-enables
                // input in its own finally when it terminates.
                if (!_npcWitnessExchangePending && !_sceneContinuationPending && !_isScenePaused)
                    IsSendEnabled = !_conversationEnded;
            }
        }

        /// <summary>
        ///   Sprint 15C: automatic NPC counter-reply loop for witness reactions.
        ///   Iterates up to <see cref="WitnessExchangeCap" /> times; each iteration is
        ///   one LLM call. The loop terminates early when witnesses fall silent.
        ///   On the final exchange the prompt instructs the NPC to close the beat
        ///   rather than ask the witness another question.
        ///   Reactions are displayed immediately and committed to the transcript just
        ///   before each LLM call so the session history never double-counts them.
        ///   Re-enables player input in its <c>finally</c> block.
        /// </summary>
        private async Task RunNpcWitnessExchangeAsync(
            System.Collections.Generic.List<(string name, string heroId, string text)> firstReactions,
            ChatSpeaker mainSpeaker,
            NpcMemoryCampaignBehavior behavior)
        {
            var currentReactions = firstReactions;

            try
            {
                NpcProfile profile = mainSpeaker.Profile;

                if (profile == null || behavior?.LlmServices == null || !behavior.LlmServices.IsAvailable)
                {
                    foreach (var (n, h, t) in currentReactions) RecordTranscript(false, n, h, t);
                    return;
                }

                WorldState world = new WorldState {
                    CurrentDay      = behavior.GameStateBridge.GetCurrentGameDay(),
                    Season          = behavior.GameStateBridge.GetSeasonName(),
                    ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
                    Rumors          = ""
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
                        playerMessage:          null,
                        witnesses:              BuildContextWitnesses(mainSpeaker),
                        privacyRequested:       false,
                        isWitnessExchangeTurn:  true,
                        isLastWitnessExchange:  isLast,
                        captiveIntent:          _captiveEncounterIntent);

                    ChatMessageViewModel placeholder = MakeSpeakerMessage(mainSpeaker, PlaceholderText);
                    _messages.Add(placeholder);
                    ChatManager.ScrollToBottom();

                    NpcChatResult result;
                    using (System.Threading.CancellationTokenSource cts =
                        new System.Threading.CancellationTokenSource())
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
                            _conversationEnded      = true;
                            IsPrivateRequestVisible = false;
                            CloseButtonText         = "Leave";
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
                    var nextReactions = CollectWitnessReactions(result.Response);
                    if (nextReactions.Count == 0) break; // witnesses fell silent — natural end

                    foreach (var (n, _, t) in nextReactions)
                        AppendWitnessReaction(n, t, recordTranscript: false);

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
        ///   Captive scene continuation loop (Sprint 17).
        ///   Fires when the NPC emitted a NARRATION block but did not end their dialogue with a
        ///   question — the scene is still unfolding and the player cannot interrupt.
        ///   Each iteration sends a synthetic "continue" cue; the loop stops when the NPC asks a
        ///   question, emits end_conversation, emits no narration, or the cap is reached.
        /// </summary>
        /// <summary>
        ///   Fires ONE narration continuation turn (called by <see cref="ExecuteContinue"/>).
        ///   Uses the speaker/behavior stored at pause time. After the call, either pauses
        ///   again (NPC still mid-act) or releases input to the player (NPC asked a question
        ///   or the act is complete).
        /// </summary>
        private async Task RunCaptiveSceneContinuationAsync()
        {
            ChatSpeaker               speaker  = _scenePauseSpeaker;
            NpcMemoryCampaignBehavior behavior = _scenePauseBehavior;

            try
            {
                NpcProfile profile = speaker?.Profile;
                if (profile == null || behavior?.LlmServices == null || !behavior.LlmServices.IsAvailable)
                    return;

                // The stimulus is the LAST thing the model sees before generating, so it carries
                // the most weight. Offer three explicit exits and force a wrap on the final beat.
                bool isFinalBeat = _sceneContinuations >= MaxSceneContinuations - 1;
                string stimulus = isFinalBeat
                    ? "[Bring the scene to its conclusion NOW. If a peak has not yet been reached, reach it "
                      + "this turn; then resolve it with a final remark, dismiss the prisoner, and emit "
                      + "end_conversation. Do not escalate further and do not repeat previous actions.]"
                    : "[The scene continues from where you left off — do not restart. Move it FORWARD: "
                      + "introduce a NEW action or escalate; do not repeat what you already described. "
                      + "Then choose ONE: (a) continue the act with something new; (b) if you want the "
                      + "prisoner to speak, end your dialogue with a direct question; (c) if you have reached "
                      + "your satisfaction or a natural peak, conclude — a final remark, dismiss the prisoner, "
                      + "and emit end_conversation.]";

                ChatSession session = BuildSessionFor(speaker);
                WorldState world = new WorldState {
                    CurrentDay      = behavior.GameStateBridge.GetCurrentGameDay(),
                    Season          = behavior.GameStateBridge.GetSeasonName(),
                    ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
                    Rumors          = ""
                };
                EncounterContext context = EncounterContextBuilder.Build(
                    speaker.Hero, profile, behavior.GameStateBridge,
                    playerMessage:            null,
                    witnesses:                BuildContextWitnesses(speaker),
                    privacyRequested:         false,
                    isCollectiveCaptiveScene: _captiveEncounterIsCollective,
                    playerStatusOverride:     PlayerStatusVsNpc.Captive,
                    captiveIntent:            _captiveEncounterIntent,
                    isFinalSceneBeat:         isFinalBeat);

                ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
                _messages.Add(placeholder);
                ChatManager.ScrollToBottom();

                NpcChatResult result;
                using (System.Threading.CancellationTokenSource cts =
                    new System.Threading.CancellationTokenSource())
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
                    : "(silence...)";

                ReplacePlaceholder(placeholder, npcLine, speaker);
                RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
                StoryLog.WriteNpcLine(speaker.Name, npcLine);

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
                        _conversationEnded      = true;
                        IsPrivateRequestVisible = false;
                        CloseButtonText         = "Leave";
                        AppendSystemMessage($"{profile.Name} has ended the conversation.", false);
                        ModLog.Info($"[SCENE] {profile.Name} ended the conversation.");
                        break;
                    }

                if (_conversationEnded) return;

                // NPC asked the player a question → hand control back
                if (NpcAskedQuestion(npcLine)) { _sceneContinuations = 0; return; }

                // No narration → act is complete, hand control back
                if (string.IsNullOrWhiteSpace(result.Response?.Narration)) { _sceneContinuations = 0; return; }

                // Still mid-act and under cap → pause again for the next player choice
                if (_sceneContinuations < MaxSceneContinuations)
                {
                    _scenePauseSpeaker  = speaker;
                    _scenePauseBehavior = behavior;
                    IsScenePaused       = true;
                }
                else
                {
                    _sceneContinuations = 0; // cap reached — give player control
                }
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
        ///   Returns true when the NPC's dialogue ends with a question mark, signalling that the
        ///   prisoner may now respond. Ignores trailing whitespace and common stage-direction wrappers.
        /// </summary>
        private static bool NpcAskedQuestion(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return false;
            // Strip trailing stage directions (*...*) so a real question before them is found.
            string stripped = System.Text.RegularExpressions.Regex.Replace(dialogue, @"\*[^*]*\*\s*$", "").TrimEnd();
            return stripped.EndsWith("?");
        }

        /// <summary>
        ///   Extracts witness reactions from a <see cref="ParsedResponse" />, resolving
        ///   each witness's hero id from the current session's witness list and NPC hero.
        ///   Returns an empty list when there are no reactions.
        /// </summary>
        private System.Collections.Generic.List<(string name, string heroId, string text)>
            CollectWitnessReactions(ParsedResponse response)
        {
            var result = new System.Collections.Generic.List<(string name, string heroId, string text)>();
            if (response?.WitnessReactions == null) return result;

            foreach (WitnessReaction wr in response.WitnessReactions)
            {
                if (string.IsNullOrWhiteSpace(wr.Text)) continue;

                // The LLM sometimes copies the whole prompt label ("A soldier (one of your
                // men…)") into the name. Strip any parenthetical role so we match — and
                // DISPLAY — only the canonical short name, never the POV-NPC role text.
                string emitted = StripWitnessRole(wr.Name);

                string heroId        = null;
                string canonicalName = emitted;
                if (string.Equals(emitted, _npcName, StringComparison.OrdinalIgnoreCase))
                {
                    heroId        = _npcHero?.StringId;
                    canonicalName = _npcName;
                }
                else
                    foreach (WitnessEntry we in _witnesses)
                        if (string.Equals(we.Name, emitted, StringComparison.OrdinalIgnoreCase))
                        {
                            heroId        = we.HeroStringId;
                            canonicalName = we.Name; // canonical casing/spelling
                            break;
                        }

                result.Add((canonicalName, heroId, wr.Text));
            }

            return result;
        }

        /// <summary>
        ///   Strips a trailing parenthetical role from a witness name, e.g.
        ///   "A soldier (one of your men, present on your orders)" → "A soldier".
        ///   Defends against the LLM echoing the full prompt label as the reaction name.
        /// </summary>
        private static string StripWitnessRole(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            int paren = name.IndexOf('(');
            return paren > 0 ? name.Substring(0, paren).Trim() : name.Trim();
        }

        #endregion
    }
}