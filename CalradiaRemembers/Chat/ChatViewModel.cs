// Code written by Gabriel Mailhot, 26/05/2026.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

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

        /// <summary>Updates the header subtitle with the two relationship scores.</summary>
        public void SetRelationSummary(int personal, int clan)
        {
            RelationSummary = $"Clan {clan:+#;-#;0}   ·   Personal {personal:+#;-#;0}";
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

        public void ExecuteClose()
        {
            OnCloseRequested?.Invoke();
        }

        public void ExecuteSendMessage()
        {
            var text = (InputText ?? "").Trim();

            if (string.IsNullOrEmpty(text)) return;
            if (!IsSendEnabled) return;
            AppendPlayerMessage(text);
            ChatMessageViewModel placeholder = new ChatMessageViewModel(_npcHero, _npcName, PlaceholderText, false);
            _messages.Add(placeholder);
            IsSendEnabled = false;
            InputText = "";
            _ = RunLlmExchangeAsync(text, placeholder);
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
                NpcProfile profile = NpcMemoryCampaignBehavior.Instance.DialogFlow.Profile;
                ChatSession session = NpcMemoryCampaignBehavior.Instance.DialogFlow.Session;
                // Rebuild context each turn so it reflects events added during this session
                // (avoids stale "never met" after the first exchange adds a FirstMeeting event).
                EncounterContext context = EncounterContextBuilder.Build(
                    _npcHero, profile, behavior.GameStateBridge, playerMessage);

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
                ReplacePlaceholder(placeholder, npcLine);

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
                        ActionOutcome outcome =
                            behavior.GameStateBridge.ExecuteAction(action, _npcHero, actionContext);
                        if (outcome != null) AppendSystemMessage(outcome.Message, outcome.IsPositive);
                    }
                    // The personal score may have moved — refresh the header.
                    SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer ?? 0);
                }

                behavior.Store.Set(profile);

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
                IsSendEnabled = true;
            }
        }

        #endregion
    }
}