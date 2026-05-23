// Code written by Gabriel Mailhot, 19/05/2026.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Dialogue;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   View model backing CalradiaRemembersChat.xml.
    ///   Sprint 7.6.E — Real LLM pipeline:
    ///   - ExecuteSendMessage is now async-friendly (fire-and-forget Task).
    ///   - Disables Send button while waiting for LLM response.
    ///   - Shows a "(considering...)" placeholder NPC message that gets
    ///   replaced by the real response on completion.
    ///   - Pulls dependencies from NpcMemoryCampaignBehavior.Instance and
    ///   LlmDialogFlow.Profile / Session / Context (Option B).
    ///   - Persists profile mutations (events, reputation) via ProfileMutator.
    /// </summary>
    public sealed class ChatViewModel : ViewModel
    {
        private const string PlaceholderText = "(considering...)";
        private string _closeButtonText = "End conversation";
        private string _inputText = "";
        private bool _isSendEnabled = true;

        private MBBindingList<ChatMessageViewModel> _messages;

        private string _npcName = "the lord";
        private string _sendButtonText = "Send";

        private string _titleText = "Calradia Remembers";

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

        // ── Commands ──────────────────────────────────────────────────────────

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

        // ── Bound properties ──────────────────────────────────────────────────

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

        public void AppendNpcMessage(string text)
        {
            ModLog.Info($"AppendNpcMessage called with text='{text?.Substring(0, Math.Min(40, text?.Length ?? 0))}...'");

            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _messages.Add(new ChatMessageViewModel(_npcName, text, false));
                ModLog.Info("AppendNpcMessage: added to _messages");
                ChatManager.ScrollToBottom();
            }
            catch (Exception ex)
            {
                ModLog.Error($"AppendNpcMessage threw {ex.GetType().Name}: {ex.Message}");
                ModLog.Error($"Stack: {ex.StackTrace}");
            }
        }

        public void AppendPlayerMessage(string text)
        {
            ModLog.Info($"AppendPlayerMessage called with text='{text?.Substring(0, Math.Min(40, text?.Length ?? 0))}...'");

            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _messages.Add(new ChatMessageViewModel("You", text, true));
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

        /// <summary>
        ///   Sprint 7.6.E: appends the player message, shows a placeholder NPC
        ///   message, calls the LLM off the UI thread, then replaces the
        ///   placeholder with the real response. Disables Send during the call.
        /// </summary>
        public void ExecuteSendMessage()
        {
            var text = (InputText ?? "").Trim();

            if (string.IsNullOrEmpty(text)) return;
            if (!IsSendEnabled) return;

            // 1) Append player message immediately for UX feedback.
            AppendPlayerMessage(text);

            // 2) Show a placeholder NPC message that we'll replace later.
            ChatMessageViewModel placeholder = new ChatMessageViewModel(_npcName, PlaceholderText, false);
            _messages.Add(placeholder);

            // 3) Lock the Send button and clear the input field.
            IsSendEnabled = false;
            InputText = "";

            // 4) Kick off the LLM call. Discard the task — errors are caught
            //    in the continuation and surfaced via the placeholder.
            _ = RunLlmExchangeAsync(text, placeholder);
        }

        // ── Public API for the dialog flow ────────────────────────────────────

        public void SetNpcName(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)) return;
            _npcName = npcName;
            TitleText = $"Calradia Remembers — {npcName}";
        }

        #region private

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max
                ? s
                : s.Substring(0, max) + "...";

        /// <summary>
        ///   Replaces the placeholder message's text by removing it from the list
        ///   and inserting a new ChatMessageViewModel at the same position. We do
        ///   this rather than mutating the placeholder in place because Gauntlet
        ///   does not always refresh inline mutations of items in MBBindingList.
        /// </summary>
        private void ReplacePlaceholder(ChatMessageViewModel placeholder, string newText)
        {
            var idx = _messages.IndexOf(placeholder);
            if (idx < 0)
            {
                // Placeholder was lost (chat closed?) — just append.
                AppendNpcMessage(newText);

                return;
            }

            ChatMessageViewModel replacement = new ChatMessageViewModel(_npcName, newText, false);
            _messages.RemoveAt(idx);
            _messages.Insert(idx, replacement);
        }

        // ── LLM pipeline ──────────────────────────────────────────────────────

        private async Task RunLlmExchangeAsync(string playerMessage, ChatMessageViewModel placeholder)
        {
            try
            {
                NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
                NpcProfile profile = LlmDialogFlow.Profile;
                ChatSession session = LlmDialogFlow.Session;
                EncounterContext context = LlmDialogFlow.Context;

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

                WorldState world = new WorldState {
                    CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
                    ActiveConflicts = "",
                    Rumors = ""
                };

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

                // Persist profile mutations.
                var gameDay = behavior.GameStateBridge.GetCurrentGameDay();
                ProfileMutator.Apply(profile, response, gameDay);
                behavior.Store.Set(profile);

                ModLog.Info($"{profile.Name} → \"{Truncate(npcLine, 120)}\" ({elapsedMs} ms)");
                if (response.NewEventData != null)
                    ModLog.Info($"  [EVENT] {response.NewEventData.Type}: {Truncate(response.NewEventData.Summary, 100)}");
                if (response.Reputation != null)
                    ModLog.Info($"  [REP] clan={response.Reputation.ClanDelta} faction={response.Reputation.FactionDelta}");
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