// Code written by Gabriel Mailhot, 03/10/2026.
// Exploration scenes: the chat with a narrator and no character (Gabriel, 03/10/2026, for authored quests: "le chat
// ouvre avec une description ... et on demande au joueur ce qu'il veut faire. La scene se deroule avec le LLM").
// Built like the envoy's audience: its own prompt, one call a turn, its own transcript. What the scene is and what
// counts is the caller's (an ExplorationSession): the narration comes back with markers, which the session checks
// against its file and turns into what the game carries out; the player reads the narration without them.
// 04/10/2026, branching tales (Gabriel: "like an LLM does in the CLI: Choice 1, Choice 2, Chat about that"): under a
// narration the player has not answered, the scene's open ways out are listed by number, then "Something else...". A
// click, or the number typed alone, sends the way's words as the player's line; the narrator tells it and writes the
// marker, and only the marker moves the story. Logic.AuthoredQuests.SceneChoiceMenu decides what the list holds.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using TaleWorlds.Library;

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

      /// <summary>The words of each way out of the scene open now, names filled (branching tales); none for a scene without.</summary>
      public Func<IReadOnlyList<string>> Ways { get; set; }

      /// <summary>The player's prisoners may be invited into the scene (the tale's choice, off unless it says so: fkasad, 07/10/2026).</summary>
      public Func<bool> PrisonersWelcome { get; set; }

      /// <summary>The full path of the picture shown beside the chat when the scene opens, or null (Gabriel, 08/10/2026).</summary>
      public string ImagePath { get; set; }

      /// <summary>The picture the last narration called for (a finding, a danger, a person), taken once; null when none.</summary>
      public Func<string> TakeImage { get; set; }

      /// <summary>The scene has nothing left the tale needs: the player may end it, unasked (tashmetu, 07/10/2026).</summary>
      public Func<bool> MayEnd { get; set; }

      /// <summary>What has been found in the scene so far, as the file words it (for the companions' memory).</summary>
      public Func<IReadOnlyList<string>> FoundSoFar { get; set; }
   }

   public sealed partial class ChatViewModel
   {
      private ExplorationSession _exploration;
      private readonly List<string> _sceneLines = new();
      private MBBindingList<SceneChoiceItemViewModel> _sceneChoices = new();
      private bool _isSceneChoiceListVisible;
      private bool _sceneNarrationIsLast;
      private bool _sceneWroteFreely;
      private bool _sceneTurnInFlight;
      private bool _sceneEndNoted;

      /// <summary>The scene may be left without asking: it ended by itself, or nothing the tale needs is left in it.</summary>
      internal bool SceneIsDone => _conversationEnded || _exploration?.MayEnd?.Invoke() == true;

      internal bool IsExplorationScene => _exploration != null;

      /// <summary>The numbered ways out listed under the narration (both chat skins bind it).</summary>
      [DataSourceProperty]
      public MBBindingList<SceneChoiceItemViewModel> SceneChoices
      {
         get => _sceneChoices;
         set
         {
            if (value == _sceneChoices) return;
            _sceneChoices = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public bool IsSceneChoiceListVisible
      {
         get => _isSceneChoiceListVisible;
         set
         {
            if (value == _isSceneChoiceListVisible) return;
            _isSceneChoiceListVisible = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>What the list is decided from, as things stand now.</summary>
      private SceneChoiceMenuFacts SceneMenuFacts()
      {
         IReadOnlyList<string> ways = Array.Empty<string>();
         try { ways = _exploration?.Ways?.Invoke() ?? Array.Empty<string>(); }
         catch (Exception ex) { ModLog.Error($"[SCENE] the ways out could not be read: {ex.GetType().Name}: {ex.Message}"); }

         return new SceneChoiceMenuFacts {
            OpenWays = ways, NarrationIsLast = _sceneNarrationIsLast, Waiting = _sceneTurnInFlight, Over = _conversationEnded, WroteFreely = _sceneWroteFreely
         };
      }

      /// <summary>Rebuilds the list under the narration from the facts; empty and hidden when there is nothing to choose.</summary>
      private void RefreshSceneChoices()
      {
         IReadOnlyList<SceneChoiceMenuItem> items = _exploration == null ? Array.Empty<SceneChoiceMenuItem>() : SceneChoiceMenu.Items(SceneMenuFacts());
         _sceneChoices.Clear();
         foreach (SceneChoiceMenuItem item in items) _sceneChoices.Add(new SceneChoiceItemViewModel(item, ChooseSceneWay));
         IsSceneChoiceListVisible = _sceneChoices.Count > 0;
         if (IsSceneChoiceListVisible) ChatManager.ScrollToBottom();
      }

      /// <summary>
      ///   A line of the list was clicked: a way's words go as the player's line (what was being typed stays in the
      ///   input); "Something else..." puts the cursor in the input to write freely.
      /// </summary>
      private void ChooseSceneWay(SceneChoiceMenuItem item)
      {
         if (_exploration == null || item == null || !IsSendEnabled || _conversationEnded) return;
         if (item.WriteFreely)
         {
            _sceneWroteFreely = true;
            RefreshSceneChoices();
            ChatInputWidget.RequestFocus();

            return;
         }

         SendExplorationLine(item.Sends);
      }

      /// <summary>Opens the scene: the narrator speaks first, with the description.</summary>
      public void ActivateExploration(ExplorationSession session)
      {
         if (session == null) return;

         _mode = ChatMode.Conversation;
         _exploration = session;
         _sceneLines.Clear();
         _sceneNarrationIsLast = false;
         _sceneWroteFreely = false;
         RefreshSceneChoices();

         ChatMessageViewModel placeholder = MakeSpeakerMessage(NarratorSpeaker(), PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;
         _ = RunExplorationTurnAsync(null, placeholder);
      }

      // Gabriel (26/10/2026): the voice telling the scene was named after it ("The Cell Door"), which read as if the door
      // spoke. The place stays in the title bar; the voice is the narrator's.
      private ChatSpeaker NarratorSpeaker() => new() {IsMain = true, Hero = null, Name = "Narrator", Profile = null};

      /// <summary>
      ///   The player said what they do: a number alone picks that way of the list shown, the number of "Something else..."
      ///   leaves the cursor in the input, anything else is their words; either way the list stays gone for the scene.
      /// </summary>
      private void ExecuteSendExplorationMessage(string text)
      {
         InputText = "";
         string line = SceneChoiceMenu.Read(text, SceneMenuFacts());
         if (line == null || line == text) _sceneWroteFreely = true;
         if (line == null)
         {
            RefreshSceneChoices();
            ChatInputWidget.RequestFocus();

            return;
         }

         SendExplorationLine(line);
      }

      private void SendExplorationLine(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return;
         AppendPlayerMessage(text);
         _sceneLines.Add("You: " + text);
         IsSendEnabled = false;
         _sceneNarrationIsLast = false;
         RefreshSceneChoices();
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
         _sceneTurnInFlight = true;
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
            _sceneNarrationIsLast = true;
            _sceneLines.Add("(narrator) " + markers.Text);
            StoryLog.WriteNpcLine(_exploration.Title, markers.Text);

            foreach (string note in _exploration.Apply?.Invoke(markers) ?? Array.Empty<string>())
               AppendSystemMessage(note, false);

            // Gabriel (08/10/2026): a finding revealed, a danger sprung or a person met may bring its picture beside the chat.
            string picture = _exploration.TakeImage?.Invoke();
            if (picture != null) ShowSceneImage(picture, _exploration.Title);

            // tashmetu (07/10/2026): once everything needed is found, say so once, so the player knows they may end it.
            if (!_sceneEndNoted && _exploration.MayEnd?.Invoke() == true)
            {
               _sceneEndNoted = true;
               AppendSystemMessage("Everything this place had to show you is found. You may end the scene when you wish.", true);
            }

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
            _sceneTurnInFlight = false;
            if (ReferenceEquals(ChatManager.ActiveViewModel, this)) RefreshSceneChoices();
         }
      }

      /// <summary>
      ///   fkasad (07/10/2026): the companions who walked the scene "talked, gave their opinion, advices", and kept no
      ///   memory of it (the witness memory needs ordinary turns, which a scene never counts). Each hero witness the
      ///   player brought in remembers going through it with the player, and what was found.
      /// </summary>
      private void RememberSceneForCompanions()
      {
         if (_exploration == null || _witnesses == null || _witnesses.Count == 0) return;
         if (!_sceneLines.Any(l => l.StartsWith("You:", StringComparison.Ordinal))) return; // the player never took part

         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.Store == null) return;

         int day = behavior.GameStateBridge?.GetCurrentGameDay() ?? 0;
         string memory = SceneCompanionMemory.Compose(_exploration.Title, TaleWorlds.CampaignSystem.Hero.MainHero?.Name?.ToString(),
            _exploration.FoundSoFar?.Invoke() ?? new List<string>());
         var recorded = 0;
         foreach (WitnessEntry w in _witnesses)
         {
            if (string.IsNullOrEmpty(w?.HeroStringId)) continue;
            NpcProfile profile = behavior.Store.Get(w.HeroStringId);
            if (profile == null) continue;

            profile.Events.Add(new NotableEvent(day, NotableEventType.Collaboration, memory));
            behavior.Store.Set(profile);
            recorded++;
         }

         if (recorded > 0) ModLog.Info($"[SCENE] {recorded} companion(s) remember {_exploration.Title}.");
      }

      /// <summary>On close: the session decides what the scene leaves behind.</summary>
      private void FinalizeExploration()
      {
         _sceneChoices.Clear();
         IsSceneChoiceListVisible = false;
         // Before the session's close moves the tale on: what was found is still this scene's.
         try { RememberSceneForCompanions(); }
         catch (Exception ex) { ModLog.Error($"[SCENE] companions' memory failed: {ex.GetType().Name}: {ex.Message}"); }
         try { _exploration?.OnClose?.Invoke(); }
         catch (Exception ex) { ModLog.Error($"[SCENE] closing failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
