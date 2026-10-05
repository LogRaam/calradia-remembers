// Code written by Gabriel Mailhot, 03/10/2026.
// Exploration scenes: the chat with a narrator and no character (Gabriel, 03/10/2026, for authored quests: "le chat
// ouvre avec une description ... et on demande au joueur ce qu'il veut faire. La scene se deroule avec le LLM").
// Built like the envoy's audience: its own prompt, one call a turn, its own transcript. What the scene is and what
// counts is the caller's (an ExplorationSession): the narration comes back with markers, which the session checks
// against its file and turns into what the game carries out; the player reads the narration without them.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>What an exploration scene is, supplied by whoever opens it.</summary>
   public sealed class ExplorationSession
   {
      /// <summary>The name over the chat ("The wood by Ortongard").</summary>
      public string Title { get; set; }

      /// <summary>The narrator's prompt as things stand now (what is left to find, who has been met...).</summary>
      public Func<string> BuildPrompt { get; set; }

      /// <summary>Carries out the markers of one narration; returns plain notes to show the player (a wound, someone joining).</summary>
      public Func<SceneMarkers, IReadOnlyList<string>> Apply { get; set; }

      /// <summary>True once the scene should end by itself (a lost night): the chat closes after this turn.</summary>
      public Func<bool> IsOver { get; set; }

      /// <summary>The scene closed, however it closed.</summary>
      public Action OnClose { get; set; }
   }

   public sealed partial class ChatViewModel
   {
      private ExplorationSession _exploration;
      private readonly List<string> _sceneLines = new();

      internal bool IsExplorationScene => _exploration != null;

      /// <summary>Opens the scene: the narrator speaks first, with the description.</summary>
      public void ActivateExploration(ExplorationSession session)
      {
         if (session == null) return;

         _mode = ChatMode.Conversation;
         _exploration = session;
         _sceneLines.Clear();

         ChatMessageViewModel placeholder = MakeSpeakerMessage(NarratorSpeaker(), PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;
         _ = RunExplorationTurnAsync(null, placeholder);
      }

      private ChatSpeaker NarratorSpeaker() => new() {IsMain = true, Hero = null, Name = _exploration?.Title ?? "The scene", Profile = null};

      /// <summary>The player said what they do.</summary>
      private void ExecuteSendExplorationMessage(string text)
      {
         AppendPlayerMessage(text);
         _sceneLines.Add("You: " + text);
         IsSendEnabled = false;
         InputText = "";
         if (_isComposeOpen) IsComposeOpen = false;

         ChatMessageViewModel placeholder = MakeSpeakerMessage(NarratorSpeaker(), PlaceholderText);
         _messages.Add(placeholder);
         _ = RunExplorationTurnAsync(text, placeholder);
      }

      /// <summary>One narrator call on <paramref name="client" />: the text, the finish reason, or why there is none. Never throws.</summary>
      private static async Task<(string Raw, string Finish, string Error)> CallNarratorAsync(ILlmClient client, LlmRequest request)
      {
         using (var cts = new CancellationTokenSource())
         {
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 10));
            try
            {
               LlmResponse resp = await Task.Run(() => client.CompleteAsync(request, cts.Token)).ConfigureAwait(false);

               return resp is {IsSuccess: true} ? (resp.Content, resp.FinishReason, null) : (null, null, resp?.ErrorMessage ?? "no result");
            }
            catch (Exception ex) { return (null, null, $"{ex.GetType().Name}: {ex.Message}"); }
         }
      }

      /// <summary>One turn: the narrator's prompt and the scene so far, one call, the narration shown, the markers carried out.</summary>
      private async Task RunExplorationTurnAsync(string playerAction, ChatMessageViewModel placeholder)
      {
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         try
         {
            if (behavior?.LlmServices?.LlmClient == null)
            {
               ReplacePlaceholder(placeholder, "(The night is still. Nothing more can be told.)", NarratorSpeaker());

               return;
            }

            string prompt = _exploration.BuildPrompt?.Invoke() ?? "";
            if (_sceneLines.Count > 0) prompt += "\n\nTHE SCENE SO FAR:\n" + string.Join("\n", _sceneLines.Skip(Math.Max(0, _sceneLines.Count - 24)));

            var request = new LlmRequest {
               SystemPrompt = prompt,
               Messages = new[] {new LlmMessage(MessageRole.User, playerAction ?? "(The scene begins. Describe it, then ask what the player does.)")},
               Parameters = new LlmParameters {MaxTokens = 3000, Creativity = 0.6f}
            };
            string beatModel = LlmServices.ActiveModelId;
            bool enhanced = LlmServices.ActiveModelMode == ManualModelMode.Enhanced;
            string nsfwModel = behavior.LlmServices.ResolveNsfwModel();
            ModLog.Info($"[SCENE] {_exploration.Title}: turn ({(playerAction == null ? "opening" : "reply")}, prompt {prompt.Length} chars) on {beatModel}.");

            (string raw, string finish, string error) = await CallNarratorAsync(behavior.LlmServices.LlmClient, request).ConfigureAwait(false);

            // Enhanced mode: a refused, filtered or empty beat is redone once on the NSFW model, on its own provider
            // (Gabriel, 04/10/2026: the werewolf scene refused and he had to switch the model by hand).
            if (RefusalFallbackPolicy.ShouldRedo(enhanced, beatModel, nsfwModel, raw, finish))
            {
               string why = string.IsNullOrWhiteSpace(raw) ? $"gave nothing ({error ?? "empty reply"})"
                  : DialogueHeuristics.LooksLikeRefusal(raw) ? "refused" : $"was cut off (finish={finish})";
               ModLog.Info($"[NSFW-FALLBACK] scene narrator on {beatModel} {why}; redoing this turn on {nsfwModel}.");

               (string redone, string redoneFinish, string redoneError) = await CallNarratorAsync(behavior.LlmServices.NsfwClient, request).ConfigureAwait(false);
               if (string.IsNullOrWhiteSpace(redone)) ModLog.Error($"[NSFW-FALLBACK] {nsfwModel} gave nothing either: {redoneError ?? "empty reply"}");
               else (raw, finish) = (redone, redoneFinish);
            }

            await MainThreadDispatcher.SwitchToMainThread();
            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return; // closed mid-call

            if (string.IsNullOrWhiteSpace(raw))
            {
               ModLog.Error($"[SCENE] turn failed: {error ?? "empty reply"}");
               ReplacePlaceholder(placeholder, "(Nothing stirs. Tell what you do.)", NarratorSpeaker());

               return;
            }

            // A refusal that got through (no fallback, or the fallback refused too) is never shown as the narration,
            // nor kept in the scene so far, where every later turn would read it back.
            if (DialogueHeuristics.LooksLikeRefusal(raw))
            {
               ModLog.Info($"[SCENE] the narrator refused this turn; not shown. {(enhanced ? "" : "The in-chat model switch can send the scene to the NSFW model.")}");
               ReplacePlaceholder(placeholder, "(The tale will not go there. Tell what you do instead.)", NarratorSpeaker());

               return;
            }

            SceneMarkers markers = SceneMarkers.Extract(raw);
            _messages.Remove(placeholder);
            AppendNarration(markers.Text);
            _sceneLines.Add("(narrator) " + markers.Text);
            StoryLog.WriteNpcLine(_exploration.Title, markers.Text);

            foreach (string note in _exploration.Apply?.Invoke(markers) ?? Array.Empty<string>())
               AppendSystemMessage(note, false);

            ChatManager.ScrollToBottom();

            if (_exploration.IsOver?.Invoke() == true)
            {
               _conversationEnded = true;
               NotifyConversationEndedChanged();
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SCENE] RunExplorationTurnAsync failed: {ex.GetType().Name}: {ex.Message}");
            try { ReplacePlaceholder(placeholder, "(The scene falls silent.)", NarratorSpeaker()); }
            catch (Exception inner) { ModLog.Error($"[SCENE] placeholder cleanup failed: {inner.Message}"); }
         }
         finally
         {
            IsSendEnabled = !_conversationEnded;
         }
      }

      /// <summary>On close: the session decides what the scene leaves behind.</summary>
      private void FinalizeExploration()
      {
         try { _exploration?.OnClose?.Invoke(); }
         catch (Exception ex) { ModLog.Error($"[SCENE] closing failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
