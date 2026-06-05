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
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Chat
{
    public sealed class ChatViewModel : ViewModel
    {
        /// <summary>
        ///   When true, the full system prompt and full NPC response are logged
        ///   on every LLM exchange. Useful for verifying that romantic sketches,
        ///   traits, and other prompt sections are correctly injected.
        ///   Toggle this off once you're done diagnosing — it floods the log.
        /// </summary>
        private const bool LogFullPromptAndResponse = false;

        private const string PlaceholderText = "(considering...)";

        // System notification line colors (relation/gold outcomes shown inline in chat).
        private const string SystemGainColor = "#9fe0a0FF"; // soft green — favorable
        private const string SystemLossColor = "#e0a0a0FF"; // soft red — unfavorable

        private string _closeButtonText = "End conversation";
        private string _inputText = "";
        private bool _isSendEnabled = true;
        private MBBindingList<ChatMessageViewModel> _messages;
        private Hero _npcHero;
        private string _npcName = "the lord";
        private string _sendButtonText = "Send";
        private string _titleText = "Calradia Remembers";
        private string _relationSummary = "";

        // ── Letter reply mode ─────────────────────────────────────────────────
        // When true, the chat is composing a reply to a received letter.
        // After the player sends, the NPC's LLM response is queued as a new
        // PendingLetter rather than displayed in the chat window.
        private bool _isLetterReplyMode;
        private PendingLetter _replyLetter;
        private NpcProfile _replyProfile;

        // ── Letter compose mode ───────────────────────────────────────────────
        // When true, the chat is used to write a NEW letter to an NPC.
        // No LLM call is made — the typed message is sealed and queued directly.
        private bool _isComposeMode;
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

        public ChatViewModel()
        {
            _messages = new MBBindingList<ChatMessageViewModel>();
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
        public void AppendWitnessReaction(string witnessName, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                // Resolve portrait hero and color from the live witness list.
                Hero witnessHero = null;
                string color = WitnessPalette[0]; // fallback
                foreach (WitnessEntry w in _witnesses)
                {
                    if (!string.Equals(w.Name, witnessName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(w.HeroStringId))
                        witnessHero = Hero.FindFirst(h => h.StringId == w.HeroStringId);
                    if (_witnessColors.TryGetValue(w.Name, out string c)) color = c;
                    break;
                }

                _messages.Add(new ChatMessageViewModel(witnessHero, witnessName, text, color));
                ChatManager.ScrollToBottom();

                // Inject into the active session so the main NPC's next LLM turn can
                // reference what the witness said. Skip letter-reply and compose modes.
                if (!_isLetterReplyMode && !_isComposeMode)
                {
                    NpcMemoryService.Core.Models.ChatSession session =
                        NpcMemoryCampaignBehavior.Instance?.DialogFlow?.Session;
                    session?.AddWitnessStatement(witnessName, text);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"AppendWitnessReaction threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void AppendNpcMessage(string text)
        {
            ModLog.Info($"AppendNpcMessage called with text='{text?.Substring(0, Math.Min(40, text?.Length ?? 0))}...'");

            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _messages.Add(new ChatMessageViewModel(_npcHero, _npcName, text, false));
                ModLog.Info("AppendNpcMessage: added to _messages");
                ChatManager.ScrollToBottom();
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

        public void AppendPlayerMessage(string text)
        {
            ModLog.Info($"AppendPlayerMessage called with text='{text?.Substring(0, Math.Min(40, text?.Length ?? 0))}...'");

            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _messages.Add(new ChatMessageViewModel(Hero.MainHero, "You", text, true));
                ModLog.Info("AppendPlayerMessage: added to _messages");
                ChatManager.ScrollToBottom();
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
            _isLetterReplyMode = true;
            _replyLetter       = letter;
            _replyProfile      = profile;

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
        ///   Switches the chat into compose mode: no LLM call, no NPC portrait.
        ///   The player types a message and "Seal &amp; Send" queues it as a
        ///   <see cref="PlayerLetter" /> with delivery delay. When <paramref name="courierCost" />
        ///   is positive, that many denars are charged on send (insufficient gold aborts).
        /// </summary>
        public void ActivateComposeMode(NpcProfile recipientProfile, int courierCost = 0)
        {
            _isComposeMode           = true;
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
            if (_isComposeMode)
            {
                SealAndSendLetter(text);
                return;
            }

            AppendPlayerMessage(text);
            ChatMessageViewModel placeholder = new ChatMessageViewModel(_npcHero, _npcName, PlaceholderText, false);
            _messages.Add(placeholder);
            IsSendEnabled = false;
            InputText = "";
            _ = RunLlmExchangeAsync(text, placeholder);
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

            ModLog.Info($"[PLAYER LETTER] sealed to {_composeRecipientProfile.Name} ({deliveryDays}d delivery).");
            InputText = "";
            ChatManager.Close();
        }

        public void SetNpcHero(Hero npcHero)
        {
            _npcHero = npcHero;
            if (!string.IsNullOrWhiteSpace(npcHero?.Name?.ToString())) _npcName = npcHero.Name.ToString();
            TitleText = $"Calradia Remembers — {_npcName}";
        }

        public void SetNpcName(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)) return;
            _npcName = npcName;
            TitleText = $"Calradia Remembers — {npcName}";
        }

        #region private

        private string ColorForWitness(WitnessEntry entry)
        {
            string key = entry.HeroStringId ?? entry.Name ?? "";
            int hash = System.Math.Abs(key.GetHashCode());
            return WitnessPalette[hash % WitnessPalette.Length];
        }

        private void RefreshWitnessUi()
        {
            bool any = _witnesses.Count > 0;
            WitnessesText        = any ? "Present: " + string.Join(" · ", _witnesses.Select(w => w.Name)) : "";
            HasWitnesses         = any;
            IsPrivateRequestVisible = any && !_privacyRequestUsed && !_isComposeMode && !_isLetterReplyMode;
        }

        private void ReplacePlaceholder(ChatMessageViewModel placeholder, string newText)
        {
            var idx = _messages.IndexOf(placeholder);

            if (idx < 0) return;
            ChatMessageViewModel replacement = new ChatMessageViewModel(_npcHero, _npcName, newText, false);
            _messages.RemoveAt(idx);
            _messages.Insert(idx, replacement);
            ChatManager.ScrollToBottom();
        }

        private async Task RunLlmExchangeAsync(string playerMessage, ChatMessageViewModel placeholder)
        {
            try
            {
                NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;

                // In letter reply mode the chat was opened outside of normal dialogue:
                // use the pre-loaded reply profile and a fresh session.
                NpcProfile profile = _isLetterReplyMode
                    ? _replyProfile
                    : NpcMemoryCampaignBehavior.Instance.DialogFlow.Profile;
                ChatSession session = _isLetterReplyMode
                    ? new ChatSession()
                    : NpcMemoryCampaignBehavior.Instance.DialogFlow.Session;
                // Rebuild context each turn so it reflects events added during this session
                // (avoids stale "never met" after the first exchange adds a FirstMeeting event).
                // Pass current witnesses and any pending privacy request for prompt injection.
                var witnessSnapshot = _witnesses.Count > 0
                    ? _witnesses.AsReadOnly()
                    : null;
                EncounterContext context = EncounterContextBuilder.Build(
                    _npcHero, profile, behavior.GameStateBridge, playerMessage,
                    witnessSnapshot, _privacyRequestPending);

                if (behavior == null || profile == null || session == null)
                {
                    ReplacePlaceholder(placeholder, "(I can't seem to gather my thoughts...)");
                    ModLog.Error("LLM pipeline: behavior/profile/session not initialized.");

                    return;
                }

                if (behavior.LlmServices == null || !behavior.LlmServices.IsAvailable)
                {
                    ReplacePlaceholder(placeholder, "(My voice fails me. The connection is lost.)");
                    ModLog.Error("LLM pipeline: LLM services unavailable.");

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
                profile.ClanRelationWithPlayer = behavior.GameStateBridge.GetPlayerRelation(_npcHero);
                SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer.Value);

                // ── Diagnostic: log the full system prompt sent to the LLM ─────
                if (LogFullPromptAndResponse)
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
                    result = await Task.Run(() =>
                                           behavior.LlmServices.ChatService.ChatAsync(
                                               profile, world, session, playerMessage, context, cts.Token))
                                       .ConfigureAwait(false);
                }

                var elapsedMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - startMs;

                if (!result.IsSuccess)
                {
                    ReplacePlaceholder(placeholder, "(My words escape me...)");
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
                if (_isLetterReplyMode && _replyLetter != null && _replyProfile != null)
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

                    ModLog.Info($"[LETTER] reply sent to {_replyProfile.Name}; NPC response queued as new letter.");
                    ChatManager.Close();
                    return;
                }
                // ── End letter reply intercept ─────────────────────────────────

                ReplacePlaceholder(placeholder, npcLine);

                // Witness reactions — displayed as separate messages with their own portraits,
                // immediately after the NPC's line and before any system messages.
                if (response.WitnessReactions != null && response.WitnessReactions.Count > 0)
                {
                    foreach (WitnessReaction wr in response.WitnessReactions)
                    {
                        AppendWitnessReaction(wr.Name, wr.Text);
                        ModLog.Info($"  [WITNESS] {wr.Name}: \"{wr.Text}\"");
                    }
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
                        // request_privacy and end_conversation are chat-flow controls,
                        // handled below — not game-world actions for the bridge.
                        if (string.Equals(action.Type, "request_privacy",
                                StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.Equals(action.Type, "end_conversation",
                                StringComparison.OrdinalIgnoreCase)) continue;
                        ActionOutcome outcome =
                            behavior.GameStateBridge.ExecuteAction(action, _npcHero, actionContext);
                        if (outcome != null) AppendSystemMessage(outcome.Message, outcome.IsPositive);
                    }
                    // The personal score may have moved — refresh the header.
                    SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer ?? 0);
                }

                // ── Privacy request resolution ────────────────────────────────
                if (_privacyRequestPending)
                {
                    _privacyRequestPending = false;
                    bool privacyAccepted = false;
                    if (response.Actions != null)
                        foreach (GameAction action in response.Actions)
                        {
                            if (!string.Equals(action.Type, "request_privacy",
                                    StringComparison.OrdinalIgnoreCase)) continue;
                            action.Parameters.TryGetValue("result", out var privacyResult);
                            privacyAccepted = string.Equals(privacyResult, "accepted",
                                StringComparison.OrdinalIgnoreCase);
                            break;
                        }
                    if (privacyAccepted)
                    {
                        _witnesses.Clear();
                        RefreshWitnessUi();
                        ModLog.Info("[WITNESSES] private audience accepted — witnesses cleared.");
                    }
                    else
                    {
                        ModLog.Info("[WITNESSES] private audience refused — witnesses remain.");
                    }
                }
                // ─────────────────────────────────────────────────────────────

                // ── Conversation end resolution ───────────────────────────────
                // The NPC may end the exchange (grave insult, refused advance pressed
                // again, or a natural farewell). Lock input; the player reads and leaves.
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
                // ─────────────────────────────────────────────────────────────

                // ── Informal quests: issue a newly offered task, pay out a verified one ──
                Quests.QuestService.ApplyResponse(
                    profile, _npcHero, response, gameDay,
                    behavior.GameStateBridge, behavior.Store,
                    (text, positive) => AppendSystemMessage(text, positive));

                behavior.Store.Set(profile);

                // Mark any delivered player letters read now that the NPC has responded.
                PlayerLetterService.MarkLettersRead(profile, behavior.Store);

                // ── Diagnostic: log full response (no truncation) ──────────────
                if (LogFullPromptAndResponse)
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
            }
            catch (Exception ex)
            {
                ReplacePlaceholder(placeholder, "(My voice falters...)");
                ModLog.Error($"RunLlmExchangeAsync threw {ex.GetType().Name}: {ex.Message}");
                ModLog.Error($"Stack: {ex.StackTrace}");
            }
            finally
            {
                // Keep input locked if the NPC ended the conversation.
                IsSendEnabled = !_conversationEnded;
            }
        }

        #endregion
    }
}