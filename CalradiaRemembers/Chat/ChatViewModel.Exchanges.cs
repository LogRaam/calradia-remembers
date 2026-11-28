// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.Logic.Romance;
using CalradiaRemembers.Logic.Witnesses;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Stance;
using CalradiaRemembers.Styles;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Actions;
using NpcMemoryService.Core.Compression;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

using NpcMemoryService.Core.Calendar;
namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      /// <summary>
      ///   Cancelled when the window closes (fkasad, Nexus 16/10/2026: a hostile lord's opener went on reasoning for 30
      ///   seconds on a local server after the player had closed the chat to attack him). A reply the closed window would
      ///   only throw away is no longer left running; every call linked to it returns as a failure the stale check drops.
      /// </summary>
      private readonly CancellationTokenSource _windowClosing = new();

      /// <summary>Stops every reply still being written for this window. Called once, at the start of the close.</summary>
      internal void CancelPendingReplies()
      {
         try
         {
            if (_windowClosing.IsCancellationRequested) return;
            _windowClosing.Cancel();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CLOSE] cancelling the pending replies failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The dedicated log for unknown-verb [ACTION] attempts, written next to the main mod log so the whole
      ///   record can be sent to the author as one file (see <see cref="LogUnknownAction" />).
      /// </summary>
      /// <summary>The verbs that settle a task of men and pay its reward themselves (SettleTroopHandOver), give_troops when read as one.</summary>
      private static readonly HashSet<string> HandOverTypes = new(StringComparer.OrdinalIgnoreCase) {"reinforce_garrison", "deliver_recruits", "give_troops"};

      private static readonly string UnknownActionLogPath = Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
         "Mount and Blade II Bannerlord",
         "Logs",
         "CalradiaRemembers_unknown_actions.log");

      #region private

      /// <summary>
      ///   Appends one timestamped block describing an invented [ACTION] type and its context (the verb, its
      ///   parameters, who attempted it, and the surrounding lines) to <see cref="UnknownActionLogPath" />, so
      ///   the author can see WHAT the model tried and decide whether to support the verb in a future update.
      ///   Runs on every unknown-verb refusal regardless of the report MCM toggle. Best-effort: a file fault is
      ///   logged and swallowed, never surfacing to the player. Mirrors <see cref="Diagnostics.RoleplayBench" />.
      /// </summary>
      /// <summary>
      ///   A shared night's warmth on this turn's profile, once a day through the intimacy bonus's door
      ///   (<see cref="Logic.RelationGate.SharedNightWarmth" />; Lordfadooboo, Nexus 09/11/2026). Returns what it added.
      ///   cr.test shared_night_once calls this same method.
      /// </summary>
      internal static int ApplySharedNightWarmth(NpcProfile profile, double currentHour)
      {
         int warmth = Logic.RelationGate.SharedNightWarmth(profile, currentHour);
         if (warmth <= 0) return 0;

         ProfileMutator.ApplyReputationDelta(profile, warmth, NpcMemoryService.Core.Standing.RegardCause.Deed);
         ModLog.Info($"[REGARD] {profile.Name}: a shared night warms by +{warmth} (regard now {profile.ReputationWithPlayer}); next in {Logic.RelationGate.IntimacyCooldownHours:0} hours at the earliest.");
         ChatManager.ActiveViewModel?.NoteWhy(Logic.Diagnostics.ChatLogWhyKind.Regard, $"{profile.Name}: a shared night warms regard by +{warmth} (now {profile.ReputationWithPlayer}).");

         return warmth;
      }

      private static void LogUnknownAction(GameAction action, string npcLabel, string npcLine, string playerLine)
      {
         try
         {
            string header = $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}";
            string body = UnknownActionNotice.FormatLogEntry(new UnknownActionNotice.UnknownActionFacts {ActionType = action?.Type, Parameters = action?.Parameters, NpcLabel = npcLabel, NpcLine = npcLine, PlayerLine = playerLine});

            string dir = Path.GetDirectoryName(UnknownActionLogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(UnknownActionLogPath, header + body + Environment.NewLine);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] could not write {UnknownActionLogPath}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Fires ONE narration continuation turn (called by <see cref="ExecuteContinue" />) when the NPC
      ///   emitted a NARRATION block without ending on a question — the scene is still unfolding. Uses the
      ///   speaker/behavior stored at pause time. After the call, either pauses again (NPC still mid-act) or
      ///   releases input to the player (NPC asked a question or the act is complete).
      /// </summary>
      private async Task RunCaptiveSceneContinuationAsync()
      {
         ChatSpeaker speaker = _scenePauseSpeaker;
         NpcMemoryCampaignBehavior behavior = _scenePauseBehavior;

         try
         {
            NpcProfile profile = speaker?.Profile;

            if (profile == null || behavior?.LlmServices is not {IsAvailable: true})
               return;

            // "Continue" = the player let the scene proceed: advance the director to the next
            // beat. The specifics of that beat live in the stage directive (system prompt); the
            // stimulus only nudges the model to perform it and not restart or repeat.
            _director?.AdvanceStory();

            string stimulus = CaptiveSceneText.BuildContinuationStimulus();
            ModLog.Info($"[SCENE] continuation {_sceneContinuations} → {behavior.LlmServices.ModelId} " +
                        $"(scene {LlmServices.ActiveSceneKind})...");

            await EnsureTranscriptCompressedAsync();
            ChatSession session = BuildSessionFor(speaker);
            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetCalendarDate(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Realms = behavior.GameStateBridge.GetRealmStanding(),
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
               aggressorKind: _director?.CurrentAggressor ?? CaptiveAggressorKind.Lead,
               captiveVictimName: _captiveVictimName,
               playerCoercedAgainstCompanion: _captivePlayerCoerced,
               captiveVictimIsFemale: _captiveVictimIsFemale,
               hideGeneratedDepth: GeneratedDepthHidden(profile));

            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            ChatManager.ScrollToBottom();

            NpcChatResult result;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_windowClosing.Token))
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
            if (DialogueHeuristics.LooksLikeRefusal(npcLine) || DialogueHeuristics.LooksLikeRefusal(result.Response?.Narration))
            {
               ConcludeCaptiveSceneAfterRefusal(placeholder, speaker, profile);

               return;
            }

            // An empty beat (no dialogue, no narration) must fail LOUDLY: rendering it would leave
            // the placeholder silently removed and the player staring at nothing with no log trail.
            if (string.IsNullOrWhiteSpace(npcLine) && string.IsNullOrWhiteSpace(result.Response?.Narration))
            {
               ReplacePlaceholder(placeholder, "(The moment passes.)", speaker);
               ModLog.Error($"[SCENE] continuation {_sceneContinuations}: the model returned an empty beat " +
                            "(no dialogue, no narration).");

               return;
            }

            // Render the beat: dialogue bubble, or a narration-only turn (no empty bubble).
            // A beat the model LED with its narration keeps that order at render (and in the
            // transcript/story log, which AppendNarration writes ahead of the spoken line's).
            bool narrationFirst = result.Response?.NarrationBeforeDialogue == true
                                  && !string.IsNullOrWhiteSpace(result.Response.Narration);
            if (narrationFirst)
               AppendNarration(result.Response.Narration, placeholder);

            npcLine = RenderNpcTurn(placeholder, speaker, result.Response);
            _captiveBeatCount++;
            if (!string.IsNullOrEmpty(npcLine))
            {
               RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
            }

            if (!narrationFirst && !string.IsNullOrWhiteSpace(result.Response?.Narration))
               AppendNarration(result.Response.Narration);

            // Witness reactions during a Continue beat — chiefly the captive companion's OWN voice (a sob, a
            // plea, a curse) under their portrait. The main per-turn path collects these; the continuation
            // never did, so a held companion fell silent the moment the player let the scene run. Display +
            // commit them so the victim stays a person with a voice across the whole scene.
            List<(string name, string heroId, string text)> sceneReactions = CollectWitnessReactions(result.Response);
            foreach (var (wn, wh, wt) in sceneReactions)
            {
               AppendWitnessReaction(wn, wt, false);
               RecordTranscript(false, wn, wh, wt);
               ModLog.Info($"  [SCENE WITNESS] {wn}: \"{wt}\"");
            }

            ModLog.Info($"[SCENE:{_sceneContinuations}] {speaker.Name}: \"{npcLine}\"");
            if (result.Usage != null)
               ModLog.Info($"  [SCENE TOKENS] prompt={result.Usage.PromptTokens} (cached:{result.Usage.CachedPromptTokens ?? 0}) | completion={result.Usage.CompletionTokens}" +
                           (result.WasRetried
                              ? " | RETRY fired (first attempt was empty or truncated)"
                              : ""));

            LogVramSample("scene");

            if (result.Response?.Actions != null)
               foreach (GameAction action in result.Response.Actions)
               {
                  if (!string.Equals(action.Type, "end_conversation",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  _conversationEnded = true;
                  IsPrivateRequestVisible = false;
                  CloseButtonText = "Leave";
                  if (IsCaptiveEncounter) IsPleaseContinueVisible = true;
                  NotifyConversationEndedChanged();
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
            if (behavior?.LlmServices is not {IsAvailable: true})
            {
               ReplacePlaceholder(placeholder, behavior?.LlmServices?.DescribeUnavailability() ?? "(The AI services are not available.)", null);

               return;
            }

            if (_commonerProfile == null || _commonerSession == null)
            {
               ReplacePlaceholder(placeholder, "(Seems this stranger has nothing to say...)", null);
               RestoreInputAfterFailedTurn(playerMessage);

               return;
            }

            ModLog.Info($"[COMMONER] player → {_commonerProfile.Name}: \"{playerMessage}\"");

            NpcChatResult result;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_windowClosing.Token))
            {
               try
               {
                  string commonerStyle = StyleService.CurrentRules();
                  result = await Task.Run(() =>
                                        behavior.LlmServices.ChatService.ChatCommonerAsync(
                                           _commonerProfile, _commonsKnowledge, _commonerSession,
                                           playerMessage, cts.Token, commonerStyle))
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
               NotifyLlmFailure(result.ErrorMessage);
               RestoreInputAfterFailedTurn(playerMessage);

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

                  ArchiveDeed(action, outcome, _commonerProfile.Name, null);
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
            if (profile == null || behavior?.LlmServices is not {IsAvailable: true})
            {
               ApplyEscapeOutcomeFallback(speaker, freed, profile);

               return;
            }

            string stimulus = freed
               ? "[The prisoner has just torn free of your grip and bolted into the dark — and they are GONE, " + "beyond any chance of recapture tonight. You FAILED to hold them. React in character to losing " + "them this very instant: fury, a shout, a curse, a too-late order to give chase. A few lines " + "only. Then it is over — emit end_conversation.]"
               : "[The prisoner lunged to break free and FAILED — you caught them before they got anywhere. " + "Reassert your control, make them feel the cost of the attempt, and carry the scene on.]";

            await EnsureTranscriptCompressedAsync();
            ChatSession session = BuildSessionFor(speaker);
            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetCalendarDate(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Realms = behavior.GameStateBridge.GetRealmStanding(),
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
               sceneStage: CaptiveSceneStage.Conclude, // the escape beat is the scene's end
               captiveVictimName: _captiveVictimName,
               playerCoercedAgainstCompanion: _captivePlayerCoerced,
               captiveVictimIsFemale: _captiveVictimIsFemale,
               hideGeneratedDepth: GeneratedDepthHidden(profile));

            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            ChatManager.ScrollToBottom();

            NpcChatResult result;
            using (var cts =
                   CancellationTokenSource.CreateLinkedTokenSource(_windowClosing.Token))
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
            bool usable = !string.IsNullOrWhiteSpace(npcLine) && !DialogueHeuristics.LooksLikeRefusal(npcLine) && !DialogueHeuristics.LooksLikeRefusal(result.Response?.Narration);

            if (usable)
            {
               ReplacePlaceholder(placeholder, NovelShown(npcLine), speaker);
               RecordTranscript(false, speaker.Name, speaker.Hero?.StringId, npcLine);
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

      /// <summary>
      ///   Puts the player's just-sent text back into the input box after a turn that produced NO reply (a
      ///   request timeout, a network error, or the AI services being unavailable), so a message lost to a
      ///   provider hiccup can be resent with one click instead of retyped (player request, ApocalyptikGreen
      ///   2026-07-13). A no-op for a synthetic scene cue (suppressPlayerLine), and it never clobbers text the
      ///   player has already begun typing while the call was in flight.
      /// </summary>
      private void RestoreInputAfterFailedTurn(string playerMessage, bool suppressPlayerLine = false)
      {
         if (suppressPlayerLine || string.IsNullOrWhiteSpace(playerMessage)) return;
         if (!string.IsNullOrWhiteSpace(InputText)) return; // the player already began a new message, so leave it

         InputText = playerMessage;
         AppendOocMessage("Your message did not reach the AI. It has been restored below. Press Send to try again.");
      }

      /// <summary>
      ///   Marriage-memory audit: a shallow copy of <paramref name="source" /> with
      ///   <see cref="ParsedResponse.NewEventData" /> cleared. Used to run <see cref="ProfileMutator.Apply" />
      ///   for every OTHER effect of the turn (discovery, reputation gate, stance) without letting it store or
      ///   advance the romantic arc on an [EVENT] this turn withheld pending confirmation against real engine
      ///   state (<c>Hero.Spouse</c>). Every other field is passed through untouched, so a field ADDED to
      ///   <see cref="ParsedResponse" /> must be added here too, or this turn would silently drop it.
      /// </summary>
      private static ParsedResponse WithoutEventData(ParsedResponse source) => new() {
         Dialogue = source.Dialogue,
         Narration = source.Narration,
         NarrationBeforeDialogue = source.NarrationBeforeDialogue,
         Memory = source.Memory,
         DiscussedMemories = source.DiscussedMemories,
         NewEventData = null,
         Reputation = source.Reputation,
         StanceShift = source.StanceShift,
         Actions = source.Actions,
         Discovery = source.Discovery,
         QuestGiven = source.QuestGiven,
         QuestBlockMalformed = source.QuestBlockMalformed,
         QuestCompleted = source.QuestCompleted,
         QuestAbandoned = source.QuestAbandoned,
         WitnessReactions = source.WitnessReactions
      };

      /// <summary>
      ///   The one junction (ROADMAP, one path for actions): the reply's actions as the chosen reader reads them. The
      ///   same call's own tags are the reply as it came; the interpreter replaces them with what it read over the prose.
      ///   Everything after this is one path for both compositions.
      /// </summary>
      private async Task<ParsedResponse> ReadActionsAsync(ActionReader reader, ParsedResponse response, EncounterContext context,
                                                          NpcProfile profile, NpcMemoryCampaignBehavior behavior, bool conversationInProgress,
                                                          string tradeBackground = null)
      {
         if (reader != ActionReader.Interpreter) return response;

         return await ComposeByInterpretationAsync(response, context, profile, behavior, conversationInProgress, tradeBackground);
      }

      /// <summary>
      ///   PROSE + INTERPRETER composition: the prose reply is already on screen; this runs the MCM-configured
      ///   ACTION INTERPRETER over that prose ONE shot and returns a <see cref="ParsedResponse" /> that keeps every
      ///   prose field but REPLACES the machine tags (<see cref="ParsedResponse.Actions" /> and
      ///   <see cref="ParsedResponse.NewEventData" />) with what the interpreter read, for the caller to feed into
      ///   the SAME consumption pipeline. On ANY failure (empty prose, prompt build, call, or parse) it returns
      ///   the prose with NO tags: the prose already showed, so a missed tag is applied as nothing rather than
      ///   disturbing the scene, and v1 never falls back to the prose model's own tags (the two modes stay cleanly
      ///   separated). Resumes on the main thread before returning, so the caller's game-state work is safe.
      /// </summary>
      private async Task<ParsedResponse> ComposeByInterpretationAsync(
         ParsedResponse proseResponse, EncounterContext context, NpcProfile profile,
         NpcMemoryCampaignBehavior behavior, bool conversationInProgress, string tradeBackground)
      {
         // Always in-turn: the player waits the interpreter call. fkasad (15/10/2026) found the MCM "Actions Interpreter
         // Timing" dropdown offered an Asynchronous choice that silently ran this same path; the dropdown was removed
         // (2.7.12) and the async apply-back is a ROADMAP entry.
         // The interpreter core lives in the shared ActionInterpreterComposer so the live path and cr.rp_bench feed
         // the ACTION INTERPRETER through ONE code path. It does not switch threads; resume the main thread here,
         // exactly as this method always did, before the caller touches any UI / game state.
         ParsedResponse result = await ActionInterpreterComposer.InterpretAsync(
            proseResponse, context, profile, ActionInterpreterComposer.ResolveDefaultPlayerLabel(),
            behavior.LlmServices, CancellationToken.None, conversationInProgress, tradeBackground);
         await MainThreadDispatcher.SwitchToMainThread();

         return result;
      }

      /// <summary>
      ///   v1.34.2 log-only execution audit (Pattern 1): maps the live <paramref name="context" /> onto the pure
      ///   <see cref="TaughtActionFacts" /> and returns the set of action types this ordinary 1:1 turn taught, so
      ///   an [ACTION] that runs yet was never taught can be observed. Best-effort and fully guarded: any fault
      ///   yields null, which disables the audit for the turn rather than ever disturbing the exchange.
      /// </summary>
      private static ISet<string> TryBuildTaughtActionSet(EncounterContext context)
      {
         if (context == null) return null;

         try
         {
            var facts = new TaughtActionFacts {
               PlayerIsCaptive = context.PlayerStatus == PlayerStatusVsNpc.Captive,
               PlayerHoldsThisNpcCaptive = context.PlayerStatus == PlayerStatusVsNpc.NpcIsCaptive,
               NpcCanBeAppointedGovernor = context.NpcCanBeAppointedGovernor,
               NpcCanBeAssignedPartyRole = context.NpcCanBeAssignedPartyRole,
               NpcCanRejoinParty = context.NpcCanRejoinParty,
               NpcCanBeDispatchedOnMission = context.NpcCanBeDispatchedOnMission,
               NpcCanBeGrantedStipend = context.NpcCanBeGrantedStipend,
               NpcCanSwearOath = context.NpcCanSwearOath,
               NpcCanBeGrantedFief = context.NpcCanBeGrantedFief,
               NpcCanHaveFiefRevoked = context.NpcCanHaveFiefRevoked,
               NpcCanBeExpelledFromClan = context.NpcCanBeExpelledFromClan,
               CompanionRecruitable = context.CompanionAskingPrice.HasValue,
               MercenaryOffered = !string.IsNullOrWhiteSpace(context.MercenaryOfferKingdom),
               PlayerIsMercenary = context.PlayerIsMercenary,
               VassalOffered = !string.IsNullOrWhiteSpace(context.VassalOfferKingdom),
               NpcCanEscortPlayer = context.NpcCanEscortPlayer,
               NpcIsEscortingPlayer = context.NpcIsEscortingPlayer,
               NpcCanAttackTogether = context.NpcCanAttackTogether && context.JointAttackTargets?.Count > 0,
               NpcCanRideWithPlayer = context.NpcCanRideWithPlayer,
               NpcIsRidingWithPlayer = context.NpcIsRidingWithPlayer,
               LordRecruitEligible = context.LordRecruitEligible,
               CompanionOnErrand = context.CompanionOnErrand,
               OpenRelationshipEligible = context.OpenRelationshipEligible,
               AcceptOtherPartnersEligible = context.AcceptOtherPartnersEligible,
               ConsortEligible = context.ConsortEligible,
               SecretLoverEligible = context.SecretLoverEligible,
               LoveMatchEligible = context.LoveMatchEligible,
               MatchmakerAvailable = !string.IsNullOrWhiteSpace(context.MatchmakerNpcKin)
                                     && !string.IsNullOrWhiteSpace(context.MatchmakerPlayerKin),
               DuelChallengeAvailable = context.DuelChallengeAvailable,
               PlayerHoldsLordPrisoners = !string.IsNullOrWhiteSpace(context.HeldLordPrisoners),
               CaptorAllowsPersuasionRelease = context.CaptorAllowsPersuasionRelease,
               CaptorMayExecutePlayer = context.CaptorMayExecutePlayer,
               CaptorOffersRansom = context.CaptorRansomPrice > 0,
               CaptorOffersParole = context.CaptorParoleDays > 0,
               CaptorOffersExchange = !string.IsNullOrEmpty(context.CaptorExchangeCaptive),
               SchemeAgentActive = !string.IsNullOrWhiteSpace(context.SchemeAgentTargetName),
               SchemeTargetsThisNpc = context.SchemeTargetsThisNpc,
               SpouseDivorceDemandActive = !string.IsNullOrWhiteSpace(context.SpouseDivorceDemandNote),
               PlayerEndOwnMarriageOffered = !string.IsNullOrWhiteSpace(context.PlayerEndOwnMarriageNote),
               SpouseEstrangementActive = !string.IsNullOrWhiteSpace(context.SpouseEstrangementNote),
               CourtVerbsOffered = !string.IsNullOrWhiteSpace(context.ExtraActionTeachings),
               // Mirrors AppendTeachSkill's own gate: the SDK offers the verb when the NPC has mastery to pass on.
               TeachSkillOffered = context.TeachableSkills is {Count: > 0}
            };

            ISet<string> taught = TaughtActionSet.ForTurn(facts);

            // Extension Surface (CrActions.Register): union in every externally-registered action still
            // eligible/taught this way is out of this audit's reach, so unioning the whole registered set
            // in avoids a false "executed but never taught" flag on a real, registered third-party verb.
            // Built-ins are excluded: they are already covered above by TaughtActionFacts. In normal play
            // nothing is registered, so this loop adds nothing and the set is unchanged.
            foreach (string type in GameActionCatalog.Types)
               if (!GameActionCatalog.BuiltInTypes.Contains(type))
                  taught.Add(type);

            return taught;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION-AUDIT] taught-set build failed (audit skipped this turn): {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Log-only (v1.34.2, Pattern 1): leaves a single trace line when <paramref name="action" /> is about to
      ///   execute yet was not in <paramref name="taughtThisTurn" /> and is not a base/flow type the audit never
      ///   reports on. Never throws, never refuses, never alters control flow. A null set (a council turn or a
      ///   build fault) simply skips the check, and every path is wrapped so the audit can never disturb the turn.
      /// </summary>
      /// <summary>
      ///   The turn's actions with a sale written in parts read as the sale (3chmidt, 06/11/2026): when the character
      ///   hands coin over and the player hands an item over in the same turn, the give_gold goes and the give_item becomes
      ///   a sell_item at that coin, which the player confirms and <c>ItemSalePolicy</c> prices. The list as it came
      ///   otherwise, and while the character owes coin (an oath, a verified task) or a task of theirs asks for goods.
      /// </summary>
      internal static IReadOnlyList<GameAction> ReadPartsAsASale(IReadOnlyList<GameAction> actions, string offeredItemId, Hero giver, NpcProfile profile)
      {
         GiftFacts owed = ExchangeService.GiftFactsFor(giver, profile);
         SaleFromParts parts = TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {
            ItemPickedFromBag = !string.IsNullOrWhiteSpace(offeredItemId),
            CoinOwed = owed.OwedByOath + owed.OwedForTask > 0,
            TaskAsksForGoods = profile?.ActiveQuests?.Any(q => q != null && (q.Type == QuestType.ProvideGoods || q.Type == QuestType.CraftWeapon) && q.IsOutstanding) == true,
            Actions = actions.Select(a => new TradeBatchAction {Type = a?.Type, Item = Param(a, "item"), Amount = Param(a, "amount"), Count = Param(a, "count"), Actor = Param(a, "actor")}).ToList()
         });
         if (parts == null) return actions;

         var sale = new GameAction {
            Type = "sell_item",
            Context = actions[parts.ItemIndex].Context,
            Parameters = new Dictionary<string, string> {["item"] = parts.Item ?? "", ["price"] = parts.Price.ToString()}
         };
         ModLog.Info($"[ACTION] give_gold {parts.Price} and give_item '{parts.Item}' in one turn read as one sell_item.");

         return actions.Where((_, i) => i != parts.GoldIndex).Select(a => ReferenceEquals(a, actions[parts.ItemIndex]) ? sale : a).ToList();

         static string Param(GameAction a, string key) => a?.Parameters != null && a.Parameters.TryGetValue(key, out string v) ? v : null;
      }

      private void AuditActionTaught(GameAction action, ISet<string> taughtThisTurn, NpcProfile profile)
      {
         try
         {
            if (taughtThisTurn == null || action == null) return;

            string type = action.Type;
            if (string.IsNullOrEmpty(type)) return;
            if (TaughtActionSet.IsNeverAudited(type)) return;
            if (taughtThisTurn.Contains(type)) return;

            string npcName = profile?.Name ?? _npcHero?.Name?.ToString() ?? "this NPC";
            ModLog.Info($"[ACTION-AUDIT] '{type}' executed but was not taught this turn (NPC: {npcName}).");
         }
         catch
         {
            /* the audit must never disturb the exchange */
         }
      }

      // The plain-fact recap an external ActionResult (or CrConversation.PromptReply) asked the NPC to voice this
      // turn, captured during the action loop and consumed once at turn end to fire ONE voice-only follow-up. Only
      // ever set on a normal top-level turn (never a recap turn, never an ensemble voice), and cleared at the start
      // of every top-level turn so a recap stranded by an early return can never fire stale on a later turn.
      private string _pendingOutcomeRecap;

      private async Task RunLlmExchangeAsync(string playerMessage,
                                             ChatSpeaker speaker,
                                             ChatMessageViewModel placeholder,
                                             bool suppressPlayerLine = false,
                                             bool suppressGroupTail = false,
                                             bool isOutcomeReaction = false,
                                             bool isSceneOpener = false)
      {
         // A recap follow-up is itself a turn; clear any prior capture so THIS top-level turn starts clean and a
         // recap left dangling by an early return (a witness exchange, a scene pause) never fires on a later turn.
         if (!isOutcomeReaction && !suppressGroupTail) _pendingOutcomeRecap = null;

         // The character's opening line in a meeting a tale brought: taken at the start of THIS turn, so a call that fails
         // never leaves the mark on the player's first real exchange (which the arc would then not count).
         bool isTaleOpener = _taleOpenerTurn;
         _taleOpenerTurn = false;

         // Backstop: any other turn that starts while the character's scene is still being written takes the floor from
         // it (a send path missed, the round table, another mod's PromptReply), so two turns never run side by side.
         if (!isSceneOpener && _sceneOpenerInFlight && !_sceneOpenerCommitted)
         {
            ModLog.Info("[SCENE] another turn began before the scene arrived; the scene is set aside.");
            SetSceneOpenerAside();
         }

         // A line the character speaks first (an opening, a voice in the ensemble, a reaction) has no player message to
         // set the language, only an English stage direction: the direction names it (RaveN017Mod, 17/10/2026: whoever
         // came to him on the map opened in English).
         if (suppressPlayerLine) playerMessage = ReplyLanguagePolicy.WithLanguage(playerMessage, ModSettings.ReplyLanguage);

         // suppressGroupTail: this turn is ONE voice inside an ensemble turn (RunEnsembleTurnAsync). It still
         // renders its reply and applies its own actions/events/memory (a full participant), but skips the group
         // machinery that must run ONCE per player turn, not once per active voice: passive-witness reactions,
         // the 15C auto-exchange, the round-table lap, and the final input re-enable (the orchestrator owns that).
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
            await EnsureTranscriptCompressedAsync();
            ChatSession session = BuildSessionFor(speaker);

            // PROSE + INTERPRETER (ComposeByInterpretationAsync below): whether this speaker already has at least
            // one prior turn in the session BEFORE this one is added. Captured HERE, before ChatAsync mutates
            // `session` in place by appending this turn's own player/NPC messages, so it reads the state as it was
            // going into this turn, not after. This is the hard "already under way" fact the interpreter has no
            // other way to know (a stranger scene reads "regard +0, never met before" on every turn alike).
            bool conversationInProgress = !session.IsEmpty;

            if (behavior == null || profile == null || session == null)
            {
               if (isSceneOpener) { SettleSceneOpener(SceneOpenerOutcome.Failed); return; }
               if (FellSilentAsEnsembleVoice(suppressGroupTail, placeholder, speaker, "not initialized")) return;
               ReplacePlaceholder(placeholder, "(I can't seem to gather my thoughts...)", speaker);
               ModLog.Error("LLM pipeline: behavior/profile/session not initialized.");
               RestoreInputAfterFailedTurn(playerMessage, suppressPlayerLine);

               return;
            }

            // Captive scene structure: a real player turn here only happens via "Try something
            // else" — the player is developing/reorienting the roleplay at THIS stage, so the
            // director stays put and flags the captor to react. The story only advances on
            // "Continue" (the continuation path).
            if (IsCaptiveEncounter && !suppressPlayerLine)
               _director?.DevelopInPlace();

            // The player spoke while the scene was waiting on the transcript: his turn opens the scene, this one is over.
            if (isSceneOpener && _sceneOpenerAbandoned) return;

            // Composition reads the THREE-STATE in-chat switch (LlmServices.ActiveComposition), not the raw MCM
            // "Chat Composition" dropdown: only that switch decides, live, whether this turn is Enhanced.
            // Who reads this turn's actions is decided in one place, ActionReaderPolicy (Gabriel, 25/10/2026: "la seule
            // jonction devrait etre a quel service je demande l'interpretation"). Decided before the context is built,
            // because the prose prompt is taught the turn's own action blocks only when it is the reader.
            var readingFacts = new ActionReadingFacts {
               Composition = LlmServices.ActiveComposition, IsMainSpeaker = speaker.IsMain, IsSyntheticPlayerLine = suppressPlayerLine,
               IsCaptiveEncounter = IsCaptiveEncounter, IsCaptorScene = _captorScene
            };
            bool proseInterpretEligible = ActionReaderPolicy.IsInterpreterTurn(readingFacts);
            // Stage 6 (28/11/2026): the Dramaturge mode's inner life and keeper, NOT A/B-validated, so only in that mode
            // (DramaturgeModePolicy); on every other turn this is false and nothing below changes (ChatViewModel.Dramaturge.cs).
            bool dramaturgeTurn = !isSceneOpener && proseInterpretEligible && DramaturgeModePolicy.Runs(DramaturgeFactsFor(speaker, suppressPlayerLine, suppressGroupTail));

            // Stage 4 (28/11/2026): the first model turn gives a character with no depth one, in the background; the chat never
            // waits, and what lands shows from the next conversation on (the turn below hides it).
            StartGeneratedDepthIfDue(speaker);

            // Rebuild context each turn so it reflects events added during this session
            // (avoids stale "never met" after the first exchange adds a FirstMeeting event).
            // The witness list is everyone EXCEPT the current speaker (main NPC included
            // when a witness holds the floor).
            EncounterContext context = EncounterContextBuilder.Build(
               speaker.Hero, profile, behavior.GameStateBridge, playerMessage,
               BuildContextWitnesses(speaker), _privacyRequestPending,
               isCollectiveCaptiveScene: IsCaptiveEncounter && _captiveEncounterIsCollective,
               isCaptorScene: _captorScene,
               // Bring-participants-to-a-captor-scene, increment 1: this turn's speaker is a brought companion
               // the player has addressed or handed the prisoner to, inside this captor scene (see
               // IsCompanionActingOnCaptiveTurn). False on every other turn (the prisoner's own, an ordinary
               // conversation, or a witness outside a captor scene), so nothing here changes for them.
               companionActingOnCaptive: IsCompanionActingOnCaptiveTurn(speaker),
               // Bring-participants-to-a-captor-scene, increment 2: the mirror case, this turn's speaker is a
               // BROUGHT CAPTIVE (another of the player's own prisoners) the player has addressed, inside this
               // captor scene (see IsBroughtCaptiveTurn). Mutually exclusive with the flag above by construction.
               isBroughtCaptiveTurn: IsBroughtCaptiveTurn(speaker),
               // Force the captivity status: Captive when the player is the NPC's captive, NpcIsCaptive when this
               // is a player-as-captor scene (robust for the console test path too, where it isn't truly set).
               playerStatusOverride: IsCaptiveEncounter
                  ? PlayerStatusVsNpc.Captive
                  : _captorScene
                     ? PlayerStatusVsNpc.NpcIsCaptive
                     : null,
               captiveIntent: _captorScene
                  ? _captorSceneIntent
                  : _captiveEncounterIntent,
               captorIsBandit: IsCaptiveEncounter && _captiveEncounterIsBandit,
               sceneStage: _director?.CurrentStage ?? CaptiveSceneStage.Intro,
               aggressorKind: _director?.CurrentAggressor ?? CaptiveAggressorKind.Lead,
               reactToPlayerIntervention: _director?.ReactToIntervention ?? false,
               captiveVictimName: IsCaptiveEncounter
                  ? _captiveVictimName
                  : null,
               playerCoercedAgainstCompanion: IsCaptiveEncounter && _captivePlayerCoerced,
               captiveVictimIsFemale: IsCaptiveEncounter && _captiveVictimIsFemale,
               captiveSceneFocus: IsCaptiveEncounter
                  ? _captiveSceneFocus
                  : null,
               // Passed whenever a scene drew a lens (captive, player-as-captor, or companion-victim); null on
               // an ordinary chat where nothing set it (Q5: the lens is not gated to the captive-encounter path).
               captiveSceneStyle: _captiveSceneStyle,
               companionAudience: _audienceReason,
               audienceRetirementIsLanded: _audienceLanded,
               offeredItemId: _offer.ItemOnTheTable,
               // The scene has not been established yet, so this turn sets it (Gabriel 2026-07-19: an
               // opening narration, then sporadic). Consumed ONCE per conversation rather than derived from
               // the turn counter: a captor's opening turn carries suppressPlayerLine and so never
               // increments it, which asked for the opening narration twice in every captive scene.
               isConversationOpening: ConsumeOpeningNarration(),
               // Where this character's REMEMBERED past ends and this conversation begins. Captured once, on
               // the first turn, so every later turn cuts at the same place.
               historyKnownThrough: HistoryKnownThrough(profile),
               questsKnownThrough: QuestsKnownThrough(profile),
               memoriesDiscussedAtOpening: MemoriesDiscussedAtOpening(profile),
               freezeMeetingMemory: true,
               meetingMemoryAtOpening: MeetingMemoryAtOpening(profile),
               composedOpening: ComposedOpeningFor(speaker),
               hideGeneratedDepth: GeneratedDepthHidden(profile),
               offerMoneyTokens: true,
               // The only council flag the chat prompt path still carries: the cr.council_test spike's whole-table
               // framing (RoundTableController.CouncilTurnsForced), so that single call yields the anchor's reply
               // plus a [WITNESS_REACTION] per present member. The REAL council (2026-08-22, Council Increment 4)
               // runs entirely through its own one-call group-scene pipeline (RunCouncilTurnAsync), never this
               // path, so the narrator / addressed-member / resolution-offering flags it once set are gone.
               isRoundTableTurn: RoundTableController.CouncilTurnsForced,
               // Post-duel: keep the victor/loser register on every turn, not only the opener (a beaten NPC does
               // not fanfare, a victor does not grovel), and name the contested hero for a courtship duel.
               isPostDuel: _postDuelActive,
               postDuelPlayerWon: _postDuelPlayerWon,
               postDuelWasBrawl: _postDuelWasBrawl,
               postDuelCourtedHeroName: _postDuelCourtedHeroName,
               // Player agency: draw THIS turn's verdict for any physical attempt the player makes, so the model
               // narrates an outcome it did not choose. Only on a real player turn in a captive scene; a synthetic
               // scene cue has no attempt to judge.
               playerAttemptSucceeds: RollCaptiveAttemptVerdict(suppressPlayerLine),
               // Companion-briefing pillar, revised per Gabriel's ruling: every turn built inside the private
               // briefing chat itself (main speaker or a promoted/addressed witness alike) must carry the scene
               // framing, or a companion answers as though the named target were standing right there.
               isCompanionBriefingScene: IsCompanionBriefingChat,
               companionBriefingTargetName: _companionBriefingTargetName,
               proseReadsItsOwnActions: !proseInterpretEligible);

            if (behavior.LlmServices is not {IsAvailable: true})
            {
               if (isSceneOpener) { SettleSceneOpener(SceneOpenerOutcome.Failed); return; }
               if (FellSilentAsEnsembleVoice(suppressGroupTail, placeholder, speaker, "AI unavailable")) return;
               ReplacePlaceholder(placeholder,
                  behavior.LlmServices?.DescribeUnavailability() ?? "(The AI services are not available — check CalradiaRemembers.log.)", speaker);
               ModLog.Error("LLM pipeline: LLM services unavailable (no API key or model?).");
               RestoreInputAfterFailedTurn(playerMessage, suppressPlayerLine);

               return;
            }

            ModLog.Info($"Player → {profile.Name}: \"{playerMessage}\"");

            WorldState world = new() {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetCalendarDate(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Realms = behavior.GameStateBridge.GetRealmStanding(),
               Rumors = ""
            };

            // ── Mirror the clan-level game relation into the prompt's clan standing.
            //    The personal score (ReputationWithPlayer) is our own per-NPC value
            //    and is left untouched here — only change_relation moves it. ──
            // The clan's standing is the clan leader's relation; a character's own relation, when a per-hero relation
            // mod gives them one that differs, is said apart (tashmetu, Personal Relations, 04/10/2026).
            GameRelationFacts relations = GameRelations.Of(speaker.Hero);
            profile.ClanRelationWithPlayer = GameRelationReading.ClanStanding(relations);
            profile.OwnGameRelationWithPlayer = GameRelationReading.OwnRelationIfDistinct(relations);

            // ... and, once, the warm start that standing is worth (tashmetu, 30/09/2026), before the prompt reads regard.
            if (speaker.Hero != null)
               Standing.ClanStandingSeedService.Apply(profile, profile.ClanRelationWithPlayer.Value, behavior.GameStateBridge.GetCurrentGameDay());
            if (speaker.IsMain)
               SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer.Value, profile.OwnGameRelationWithPlayer);

            // Re-sync the speaker's sex from the live hero every turn: the identity/pronoun lines of the
            // prompt read profile.IsFemale, and a profile whose sex went stale (created before the load-time
            // RefreshPersonality reached it, or seeded wrong) would otherwise make the NPC mis-gender itself.
            // A faceless synthesized bandit captor has NO hero (Gabriel, in play 2026-07-19: a NullReference
            // crashed the whole scene on the first such captor); its profile already carries the right sex
            // from BuildBanditCaptor, so there is simply nothing to re-sync.
            if (speaker.Hero != null) profile.IsFemale = speaker.Hero.IsFemale;

            // Sync the player-authored backstory (voice only) and conviction (what they hold true, and therefore
            // pursue) from the overrides file each turn, so neither is ever persisted stale and both reflect a
            // live edit + the MCM toggle. cr.test conviction runs this same call, so dropping it fails the test.
            CharacterOverrideLoader.SyncAuthoredProse(profile, speaker.Hero);

            // Sync an authored orientation override (player request: enable e.g. a female-female romance the
            // rolled "heterosexual" blocked). Applied live each turn so an edit takes effect without a reload.
            SexualOrientation? authoredOrientation = CharacterOverrideLoader.OrientationFor(speaker.Hero);
            if (authoredOrientation.HasValue && profile.Romantic != null && profile.Romantic.Orientation != authoredOrientation.Value)
            {
               profile.Romantic.Orientation = authoredOrientation.Value;
               ModLog.Info($"[OVERRIDE] {profile.Name} orientation set to {authoredOrientation.Value} by authored char data.");
            }

            // Sync an authored kink LOCK the same way: player request (kinks added with cr.add_kink were lost
            // on cr.reload_lore because that path re-derives kinks from current traits). An authored list here
            // REPLACES whatever was rolled/frozen, every turn, so an edit to the override file takes effect
            // without needing to reopen the chat. An empty authored list is a deliberate lock to no kinks.
            IReadOnlyList<Kink> authoredKinks = CharacterOverrideLoader.KinksFor(speaker.Hero);
            if (authoredKinks != null && profile.Romantic != null)
            {
               // Kinks is init-only; the list it points to is mutable (same pattern as CharacterLoreBehavior.RederiveKinks).
               profile.Romantic.Kinks.Clear();
               profile.Romantic.Kinks.AddRange(authoredKinks);
               ModLog.Info($"[OVERRIDE] {profile.Name} kinks locked to [{string.Join(", ", authoredKinks)}] by authored char data.");
            }

            // The gestures loved and disliked, locked the same way (Gabriel, 26/10/2026), through the one sync every
            // other reader goes through too (Tene21, 03/11/2026), so an edit to the file still lands on the next turn.
            CharacterOverrideLoader.SyncAuthoredTastes(profile, speaker.Hero);

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

            // Dramaturge mode: the reply is written from where the character stands now (the update from its last reply, waited
            // for a few seconds at most), and the update from the player's line runs beside the prose call.
            if (dramaturgeTurn)
            {
               // Read once per chat on the main thread; it also seeds the bond from the meeting record, so the first reply has it.
               DramaturgeSheetFor(speaker);
               context.InnerLifeNote = await DramaturgeNoteForTurnAsync();
               await MainThreadDispatcher.SwitchToMainThread();
               if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;
               StartDramaturgeMindUpdate(speaker, playerMessage, "the player's line");
            }

            long startMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            // PROSE FALLBACK eligibility (same gate ComposeByInterpretationAsync's call site checks below): only
            // an ordinary main-NPC 1:1 turn on a real player message, never captive/captor scenes or councils.
            // Computed here (not just at the interpret call site) because the PROSE call itself needs it: only
            // an eligible turn is issued with allowTruncationRetry:false (fail fast instead of the SDK's own
            // slow double-budget retry), so the mod's own fallback can take over immediately.
            // (readingFacts and proseInterpretEligible are decided above, before the context is built.)
            bool usedProseFallback = false;

            NpcChatResult result;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_windowClosing.Token, isSceneOpener ? SceneOpenerToken : CancellationToken.None))
            {
               // Absorb any fault into a Failure result so the SwitchToMainThread below
               // ALWAYS runs — otherwise a throw here would land the catch off-thread.
               try
               {
                  result = await Task.Run(() =>
                                        behavior.LlmServices.ChatService.ChatAsync(
                                           profile, world, session, playerMessage, context, cts.Token,
                                           allowTruncationRetry: !proseInterpretEligible))
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

            // A scene the player spoke over is dropped whole: the greeting already stands, nothing of it is applied.
            // One that arrived holds the floor until its reply is applied, so no player turn runs alongside it.
            if (isSceneOpener)
            {
               if (_sceneOpenerAbandoned) return;
               _sceneOpenerCommitted = true;
               IsSendEnabled = false;
            }

            long elapsedMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - startMs;

            if (!result.IsSuccess)
            {
               // In an ensemble, one voice's LLM hiccup (a timeout, a rate limit) must not paint an error line:
               // that voice simply stays quiet this beat while the others speak. The turn made N sequential calls,
               // so a single slow model is far likelier to trip here than on a lone 1:1 reply (player report,
               // Ira's second voice timed out at 120s while Itaria's had already answered).
               if (isSceneOpener)
               {
                  // No error line for a scene nobody asked for: the greeting stays as it was.
                  ModLog.Info($"[SCENE] the scene did not come ({result.ErrorMessage}); the greeting stands.");
                  SettleSceneOpener(SceneOpenerOutcome.Failed);

                  return;
               }

               if (FellSilentAsEnsembleVoice(suppressGroupTail, placeholder, speaker, result.ErrorMessage)) return;
               ReplacePlaceholder(placeholder, "(My words escape me...)", speaker);
               ModLog.Error($"LLM call failed in {elapsedMs} ms: {result.ErrorMessage}");
               // Windmolen (29/09/2026): a provider pin with no endpoint left read as "the AI did not respond".
               NotifyLlmFailure(result.ErrorMessage, ProviderPinFailurePolicy.Explain(
                  result.ErrorMessage, !string.IsNullOrWhiteSpace(Services.LlmServices.ActiveProviderPin()), ModSettings.AllowOpenRouterProviderFallbacks));
               RestoreInputAfterFailedTurn(playerMessage, suppressPlayerLine);

               return;
            }

            if (_chatLog != null)
               NoteWhy(Logic.Diagnostics.ChatLogWhyKind.Reply,
                    $"{speaker.Name}'s reply took {(elapsedMs / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s on {LlmServices.ActiveModelId}"
                    + $"{(string.IsNullOrEmpty(result.FinishReason) ? "" : $" (finish {result.FinishReason})")}{(result.WasRetried ? ", retried" : "")}.");

            // ── PROSE FALLBACK (ADDITIVE; the clean-finish path below is byte-for-byte unchanged) ──
            // The prose call above failed fast (allowTruncationRetry:false) on an eligible turn; decide from its
            // FinishReason alone whether the beat needs completing on the fallback model. A "stop" finish (the
            // ordinary case) never enters this block, so nothing here runs for a clean reply.
            if (proseInterpretEligible)
            {
               string fallbackModel = behavior.LlmServices.ResolveProseFallbackModel();
               bool fallbackConfigured = !string.IsNullOrWhiteSpace(fallbackModel);

               bool hasSpokenDialogue = !string.IsNullOrWhiteSpace(result.Response?.Dialogue);

               // A beat the prose model REFUSED goes to the NSFW model on its own provider (Gabriel, 04/10/2026: the
               // MCM promises the NSFW model is the fallback in Enhanced; until then only a truncated beat reached it).
               string beatText = $"{result.Response?.Dialogue} {result.Response?.Narration}";
               string nsfwModel = behavior.LlmServices.ResolveNsfwModel();
               bool refusedBeat = DialogueHeuristics.LooksLikeRefusal(beatText)
                                  && RefusalFallbackPolicy.ShouldRedo(new RefusalRedoFacts {EnhancedMode = true, BeatModel = LlmServices.ActiveModelId, NsfwModel = nsfwModel, Reply = beatText, FinishReason = result.FinishReason});

               if (refusedBeat || TruncationFallbackPolicy.ShouldFallBack(result.FinishReason, fallbackConfigured, hasSpokenDialogue))
               {
                  if (refusedBeat) fallbackModel = nsfwModel;
                  string why = refusedBeat ? "refused" : hasSpokenDialogue ? $"truncated (finish={result.FinishReason ?? "empty"})" : "produced no spoken line";
                  ModLog.Info($"[PROSE-FALLBACK] prose model {why}; " +
                              $"completing this beat on {fallbackModel} in Integrated mode.");
                  NoteWhy(Logic.Diagnostics.ChatLogWhyKind.Fallback, $"the prose model {why}; this beat was redone on {fallbackModel}.");

                  // The fallback is one call that reads its own tags (ActionReaderPolicy), so its prompt carries the turn's
                  // action blocks (a tale's choices and its arc's signals) that the interpreter would have been taught.
                  context.ProseReadsItsOwnActions = ActionReaderPolicy.ReaderFor(readingFacts, usedProseFallback: true) == ActionReader.SameCall;
                  NpcChatResult fallbackResult = await ProseFallbackComposer.RunIntegratedFallbackAsync(
                     profile, world, session, playerMessage, result.Response?.Dialogue,
                     context, behavior.LlmServices, fallbackModel, CancellationToken.None,
                     refusedBeat ? behavior.LlmServices.NsfwClient : null);

                  // The fallback call added an await: the chat may have closed or been replaced meanwhile, so run
                  // the same stale-VM guard the main call uses before any game state below is touched.
                  await MainThreadDispatcher.SwitchToMainThread();
                  if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

                  if (fallbackResult.IsSuccess)
                  {
                     result = fallbackResult;
                     usedProseFallback = true;
                  }
                  else
                  {
                     ModLog.Error($"[PROSE-FALLBACK] fallback call on {fallbackModel} failed too; keeping the truncated prose reply: {fallbackResult.ErrorMessage}");
                  }
               }
            }
            // ─────────────────────────────────────────────────────────────────────────────────────

            // Dramaturge mode: the keeper reads the reply before it is shown; a lie, a repeat, a contradiction or an invention
            // gets one rewrite. A beat the fallback redid (an integrated call on another model, its tags already read) is left.
            if (dramaturgeTurn && !usedProseFallback && result.IsSuccess
                && (!string.IsNullOrWhiteSpace(result.Response?.Dialogue) || !string.IsNullOrWhiteSpace(result.Response?.Narration)))
            {
               var kept = await KeepDramaturgeReplyAsync(speaker, profile, world, session, playerMessage, context, result, !proseInterpretEligible);
               await MainThreadDispatcher.SwitchToMainThread();
               if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;
               result = kept.Result;
               NoteDramaturgeKeeper(kept.Kept);
            }

            ParsedResponse response = TakeBackTheOtherPeoplesLines(result.Response, speaker);
            // Empty when the model produced no spoken line; RenderNpcTurn below turns that into
            // a narration-only turn (no "(silence...)" bubble) when [NARRATION] is present.
            string npcLine = !string.IsNullOrWhiteSpace(response?.Dialogue)
               ? response.Dialogue
               : string.Empty;

            // ── Model refusal guard (captive scenes) ───────────────────────
            // A Hardcore scene can trip the model's safety, returning an out-of-character
            // refusal ("I must decline..."). Never show that under the captor's name —
            // conclude the scene in character instead, so immersion survives the guardrail.
            if (IsCaptiveEncounter && (DialogueHeuristics.LooksLikeRefusal(npcLine) || DialogueHeuristics.LooksLikeRefusal(response?.Narration)))
            {
               ConcludeCaptiveSceneAfterRefusal(placeholder, speaker, profile);

               return;
            }

            // ── Escape attempt (captive scenes) ────────────────────────────
            // The captor flagged the player trying to break free. The OUTCOME is fate's,
            // not the LLM's: the mod rolls it, frees the player on success, and owns the
            // resolution narration so the captor can never "win" the escape by narration.
            // An escape is a PLAYER deed: it may only be armed on a turn the player actually spoke. A synthetic
            // scene cue (any captor-led opener, and above all the one that RESUMES the scene after a LOST escape,
            // which necessarily narrates the failed break) must never re-arm it. Without this guard the resume
            // opener made the captor emit escape_attempt again and the scene looped straight back into the fight
            // (Gabriel, in play 2026-07-20: the "Break for it" button and its line came back on the resumed scene).
            if (IsCaptiveEncounter && !suppressPlayerLine && DialogueHeuristics.HasAction(response, "escape_attempt"))
            {
               ResolveEscapeAttempt(placeholder, speaker, profile, npcLine, response);

               return;
            }

            // ── Negotiated release (captive scenes) ────────────────────────
            // The captor chose to free the prisoner. Only taught when the gate allows it, and the
            // bridge re-validates before it commits: the prompt proposes, the bridge frees for real.
            if (IsCaptiveEncounter && DialogueHeuristics.HasAction(response, "release_player"))
            {
               ResolveCaptorRelease(placeholder, speaker, profile, npcLine, response);

               return;
            }

            // A captor's terms: each is a release that costs the player something real, re-validated where it is granted.
            if (IsCaptiveEncounter && CaptiveSceneIntentRules.IsAudience(_captiveEncounterIntent) && DialogueHeuristics.HasAction(response, "ransom_player"))
            {
               ResolveCaptorRelease(placeholder, speaker, profile, npcLine, response, Quests.CaptorTermsBehavior.GrantRansom);

               return;
            }

            if (IsCaptiveEncounter && Logic.Captivity.CaptorAudiencePolicy.OffersParole(_captiveEncounterIntent) && DialogueHeuristics.HasAction(response, "parole_player"))
            {
               ResolveCaptorRelease(placeholder, speaker, profile, npcLine, response, Quests.CaptorTermsBehavior.GrantParole);

               return;
            }

            if (IsCaptiveEncounter && CaptiveSceneIntentRules.IsAudience(_captiveEncounterIntent) && DialogueHeuristics.HasAction(response, "exchange_for_player"))
            {
               ResolveCaptorRelease(placeholder, speaker, profile, npcLine, response, Quests.CaptorTermsBehavior.GrantExchange);

               return;
            }

            // ── Duel challenge ─────────────────────────────────────────────
            // Steel has been called for. The prompt stopped the NPC AT the challenge, so whether they take
            // the field is decided here, from their traits and stance, never by the line they just spoke.
            // Same "a deed is the player's" guard as escape: a synthetic cue (a captor-led opener, an
            // interception opener) must never call a duel the player did not provoke.
            if (!suppressPlayerLine && DialogueHeuristics.HasAction(response, "challenge_duel"))
            {
               ResolveDuelChallenge(placeholder, speaker, profile, npcLine, response);

               return;
            }

            // A scene that came back with nothing to show, or a refusal, is no scene: the greeting stays as it was.
            if (isSceneOpener && (string.IsNullOrWhiteSpace(npcLine) && string.IsNullOrWhiteSpace(response?.Narration)
                                  || DialogueHeuristics.LooksLikeRefusal(npcLine) || DialogueHeuristics.LooksLikeRefusal(response?.Narration)))
            {
               ModLog.Info("[SCENE] the scene came back empty or refused; the greeting stands.");
               SettleSceneOpener(SceneOpenerOutcome.Failed);

               return;
            }

            // Scene narration emitted BEFORE the dialogue keeps its emitted order at render:
            // it lands ahead of the speaker's bubble, and its transcript/story entries are
            // written before the spoken line's below (AppendNarration records all three).
            bool narrationFirst = response.NarrationBeforeDialogue && !string.IsNullOrWhiteSpace(response.Narration);
            if (narrationFirst)
               AppendModelNarration(response.Narration, response.Dialogue, placeholder);

            // Render the turn: dialogue bubble, or a narration-only turn (no empty bubble), or
            // a "(silence...)" fallback. npcLine is empty for a narration-only turn.
            npcLine = RenderNpcTurn(placeholder, speaker, response, allowSilentSpeaker: false);
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
            // Dramaturge mode: the inner life after the reply, in the background, for the next turn.
            if (dramaturgeTurn && !string.IsNullOrEmpty(npcLine)) StartDramaturgeMindUpdate(speaker, null, $"{speaker.Name}'s reply");

            // Fallback-memory bookkeeping: a completed exchange counts as a turn, and the player's text
            // length feeds the one-substantial-turn rule (a single meaningful exchange deserves a memory;
            // player-reported: an NPC forgot a one-turn conversation that closed their whole Lucon affair).
            // The recap now runs regardless of whether an [EVENT] also fired that session (a player-stated
            // plan the model did not flag as a dramatic beat must still survive as a conversation memory).
            if (!suppressPlayerLine)
            {
               _playerTurnsThisSession++;
               _playerCharsThisSession += playerMessage?.Trim().Length ?? 0;
               MaybeLearnPlayerNameFromCaptive(playerMessage);
            }

            // The lines themselves reach the story archive (and the Story Log) through RecordTranscript above.
            if (IsLiveConversation) EnsureStoryIntro();

            // Scene narration ([NARRATION]) — neutral-narrator physical action,
            // shown after the speaker's dialogue unless the model led with it
            // (handled above). Recorded to transcript + story log.
            if (!narrationFirst && !string.IsNullOrWhiteSpace(response.Narration))
               AppendModelNarration(response.Narration, response.Dialogue);

            // Witness reactions — displayed immediately after the speaker's line.
            // recordTranscript: false here; the 15C path commits them inside
            // RunNpcWitnessExchangeAsync AFTER the auto-reply, so they are not
            // double-counted in the session that feeds the follow-up LLM call.
            // The normal (no 15C) path commits them explicitly below.
            // Round-table audit D7: when the player has opened the floor, the lap below gives each of these
            // people a real turn in their OWN voice, so the main NPC's ventriloquized version of them is
            // dropped. The rule itself lives in VentriloquizedReactionPolicy where a net10 test can reach it.
            // (IsRequestPending reads without consuming; TryConsumeForMainReply below still owns the request.)
            List<(string name, string heroId, string text)> witnessReactions =
               suppressGroupTail // an ensemble co-voice never renders passive-witness reactions; the group tail runs once, on the orchestrator's pass
               || VentriloquizedReactionPolicy.DropsReactions(new VentriloquizedReactionPolicy.ReactionTurnFacts {FloorRequestPending = _roundTable.IsRequestPending, SpeakerIsMain = speaker.IsMain, IsCaptiveEncounter = IsCaptiveEncounter, ConversationEnded = _conversationEnded})
                  ? new List<(string name, string heroId, string text)>()
                  : CollectWitnessReactions(response);

            foreach (var (wn, _, wt) in witnessReactions)
            {
               AppendWitnessReaction(wn, wt, false);
               ModLog.Info($"  [WITNESS] {wn}: \"{wt}\"");
            }

            // ── PROSE + INTERPRETER composition (ADDITIVE; the Integrated default is byte-for-byte unchanged) ──
            // Integrated mode (the shipped default) skips this block entirely and consumes the prose model's own
            // [ACTION]/[EVENT] below exactly as before. In ProseInterpret mode a separate ACTION INTERPRETER reads
            // the prose just displayed and SUPPLIES those tags instead; the merged response then flows through the
            // SAME consumption pipeline below (no duplicated execution code). Confined to an ordinary main-NPC 1:1
            // turn on a real player message: captive/captor scenes, councils, routed-witness turns and synthetic
            // openers deliberately stay Integrated for now (a later increment), so those guarded paths are untouched.
            // usedProseFallback: the PROSE FALLBACK block above already redid this turn as a single INTEGRATED call
            // (prose + tags together) on the fallback model, so `response` already carries real tags: running the
            // interpreter over them again would be redundant (and would target the wrong prose, the fallback's).
            ActionReader reader = ActionReaderPolicy.ReaderFor(readingFacts, usedProseFallback);
            string tradeBackground = reader == ActionReader.Interpreter
               ? ActionInterpreterComposer.TradeBackgroundFor(session.Messages, playerMessage,
                                                              ActionInterpreterComposer.CombineProseForInterpretation(response.Dialogue, response.Narration))
               : null;
            response = await ReadActionsAsync(reader, response, context, profile, behavior, conversationInProgress, tradeBackground);

            // The interpreter call added an await: the chat may have closed or been replaced meanwhile, so run the same
            // stale-VM guard the main call uses before any game state is touched below.
            if (reader == ActionReader.Interpreter && !ReferenceEquals(ChatManager.ActiveViewModel, this)) return;
            // ───────────────────────────────────────────────────────────────────────────────────────────────

            int gameDay = behavior.GameStateBridge.GetCurrentGameDay();
            // M-R3: the romantic arc may only advance when the romance system is enabled (it is entirely gated off
            // at content Off), so a hallucinated intimacy [EVENT] at Off never accrues hidden romantic state.
            // M-R2: POSITIVE advancement also requires an orientation-plausible pair, the same filter jealousy
            // uses, so the recorded status stays coherent with whether a jealous party would ever react.
            bool orientationCompatible = profile.Romantic?.IsCompatibleWith(Hero.MainHero?.IsFemale ?? false) ?? true;
            // M3: the consent bar the prompt printed a verdict for also governs the ARC, so an intimacy below
            // it cannot mint an "Intimate" status the player never earned (and which would then colour every
            // later refusal as a former lover's).
            //
            // Marriage-memory audit (player report, Nexus, v1.31.4): the model can narrate and EMIT an [EVENT]
            // claiming a completed wedding ("Mesui and Huan Yi married under Khuzait law...") whatever
            // BannerlordGameStateBridge.ExecuteMarry actually decided (the prompt is advice, the bridge is
            // law), but ProfileMutator.Apply stored ANY [EVENT] unconditionally and also let it advance the
            // romantic arc (AdvanceRomanticStatus) off the same unbacked claim. A withheld claim is stashed
            // here and only committed once the action loop below has run and Hero.Spouse can be checked for
            // real (see the confirm/drop block right after that loop).
            // Intimacy guard (Gabriel 2026-08-02): coercion is not intimacy. A captive or captor scene is
            // non-consensual by construction, so an [EVENT] the model tagged Intimacy/Flirt inside one must NEVER
            // become a consensual memory, grant the intimacy regard gain, or rouse jealousy as a courtship. Strip
            // it here so every path below (ProfileMutator, the regard/jealousy block) sees no romantic event; the
            // scene's own Captivity/grudge finalizers hold the harm. Pure CoerciveSceneEventPolicy keeps it tested.
            if (CoerciveSceneEventPolicy.ShouldDropRomanticEvent(response.NewEventData?.Type, IsCaptiveEncounter || _captorScene))
            {
               ModLog.Info($"[SCENE] dropped a {response.NewEventData.Type} [EVENT] in a coercive scene (coercion is not intimacy).");
               response = WithoutEventData(response);
            }

            // Companion-briefing pillar: the private briefing chat must never write ANY persisted memory event,
            // on the anchor's profile or a witness companion's. Not a content filter like the coercive-scene
            // drop above: a stored event here would resurface through the exact same witness-memory recall
            // (BuildWitnessMemory/WithMemories) the instant this companion is later a witness in the target
            // conversation the standing brief actually rides into, handing the private word to the very person
            // it must stay blind to (the load-bearing property of the whole pillar). Dropped unconditionally,
            // whatever type the model tagged it, on EVERY turn of the scene (main speaker or an addressed/
            // ensemble witness alike, since IsCompanionBriefingChat is a whole-scene flag).
            if (IsCompanionBriefingChat && response.NewEventData != null)
            {
               ModLog.Info($"[BRIEFING] dropped a {response.NewEventData.Type} [EVENT] inside the private briefing chat (never persisted).");
               response = WithoutEventData(response);
            }

            ParsedEventData withheldMarriageClaim = null;
            ParsedResponse responseForMutator = response;
            if (response.NewEventData != null &&
                MarriageClaimPolicy.ClaimsMarriageWithPlayer(new MarriageClaimPolicy.MarriageClaimFacts {Summary = response.NewEventData.Summary, NpcName = profile.Name, PlayerName = Hero.MainHero?.Name?.ToString()}))
            {
               withheldMarriageClaim = response.NewEventData;
               responseForMutator = WithoutEventData(response);
               ModLog.Info($"[EVENT] withheld a marriage claim from {profile.Name} pending Hero.Spouse confirmation: \"{withheldMarriageClaim.Summary}\"");
            }

            // Discovery-subject audit (player report, Nexus): the Encyclopedia's discovery section is
            // meant to hold ONLY what the player learned ABOUT the NPC. ProfileMutator.Apply drops any
            // [DISCOVERY] DiscoverySubjectGuard flags as being about the player instead, but that drop is
            // silent to the player, so it is logged here (same treatment as the withheld marriage claim
            // above) to keep a future report diagnosable.
            string playerName = Hero.MainHero?.Name?.ToString();
            if (responseForMutator.Discovery != null &&
                DiscoverySubjectGuard.IsAboutPlayer(responseForMutator.Discovery.Key, responseForMutator.Discovery.Description, playerName))
            {
               ModLog.Info($"[DISCOVERY] dropped a player-subject entry from {profile.Name}: key=\"{responseForMutator.Discovery.Key}\" description=\"{responseForMutator.Discovery.Description}\"");
            }

            // Memories panel action tag: remember the [EVENT] memory this turn creates (if any), so a
            // mechanical deed the bridge actually applies below can be stamped onto it (ActionMemoryLabel),
            // and the journal highlights a real turning point in blue rather than the LLM's narrative type.
            int eventsBeforeApply = profile.Events.Count;

            ProfileMutator.Apply(profile, responseForMutator, gameDay,
               romanticContentEnabled: ModSettings.AdultContentLevel != AdultContentLevel.Off,
               orientationCompatible: orientationCompatible,
               applyReputation: false, // C2: [REPUTATION] is disabled in the mod; never apply a block the model emitted unprompted.
               intimacyPermitted: behavior.GameStateBridge.PermitsIntimacyWith(_npcHero, profile),
               playerName: playerName);

            if (responseForMutator.DiscussedMemories.Count > 0)
            {
               ModLog.Info($"[MEMORY] {profile.Name} spoke of {string.Join(", ", responseForMutator.DiscussedMemories)}.");
               // The story archive links this talk to every other one that spoke of the same memory (Gabriel, 17/11/2026).
               ArchiveThreads(responseForMutator.DiscussedMemories.Select(code => $"memory:{profile.Id}:{code}"));
            }

            NotableEvent turnEvent = profile.Events.Count > eventsBeforeApply
               ? profile.Events[profile.Events.Count - 1]
               : null;

            // Stance: the LLM's spoken nudges to trust/respect/fear — capped hard and rate-limited
            // (words only colour; deeds prove). Persisted by the Store.Set below after actions run.
            StanceService.ApplyWordShift(profile, response.StanceShift);
            ConversationMood.Record(speaker.Hero?.StringId, response.StanceShift?.Mood ?? 0);
            // A low man lifted by the player is reassured (self-esteem, 30/09/2026): once a turn, only when he needed it.
            if (response.StanceShift?.Mood > 0 && Standing.SelfEsteemService.BandFor(speaker.Hero, profile) is NpcMemoryService.Core.Mood.SelfEsteemBand.Shaken or NpcMemoryService.Core.Mood.SelfEsteemBand.Broken)
               Standing.SelfEsteemService.Record(speaker.Hero, Logic.SelfEsteemEvent.Reassured);

            // Jealousy (Sprint 24): a romantic act recorded this turn may provoke a staked
            // third party. Intimacy only when adult content is enabled. Witnesses present mean
            // the act is telegraphed and the word spreads quickly. Plausibility gate (player
            // report: a lord's wife threatened a MALE player over a quest turn-in the model had
            // stamped as flirt): a same-sex pair is never a rival courtship in this romance
            // model, spoken flirtation counts only as a PATTERN and never in the same breath as
            // a quest completion; intimacy is a deed and counts at once for a plausible pair.
            NotableEventType? actType = response.NewEventData?.Type;
            if (actType == NotableEventType.Flirt || actType == NotableEventType.Intimacy && ModSettings.AdultContentLevel != AdultContentLevel.Off)
            {
               Hero courted = Hero.FindFirst(h => h.StringId == profile.Id);
               RomanticActTier tier = actType == NotableEventType.Intimacy
                  ? RomanticActTier.Intimacy
                  : RomanticActTier.Flirt;
               // Pair plausibility follows the courted NPC's ORIENTATION (authorable since v1.30.x), so a
               // deliberately enabled same-sex romance rouses jealousy like any other; the default world
               // (heterosexual orientations) keeps the original opposite-sex behaviour. No romantic profile
               // falls back to the opposite-sex rule.
               bool plausiblePair = courted != null &&
                                    Hero.MainHero != null &&
                                    (profile.Romantic?.IsCompatibleWith(Hero.MainHero.IsFemale) ?? courted.IsFemale != Hero.MainHero.IsFemale);
               // Minor (romance audit): a "pattern" of flirtation must be RECENT, not a lifetime tally. Counting
               // every flirt ever meant two flirts three years apart read as an ongoing courtship. Window it to
               // roughly a Calradian year so only sustained recent interest establishes a pattern. (Includes
               // today's, just applied.)
               int romanticPatternWindowDays = WorldCalendar.DaysPerYear;
               int romanticEvents = profile.Events.Count(e =>
                  (e.type is NotableEventType.Flirt or NotableEventType.Intimacy)
                  && gameDay - e.gameDay <= romanticPatternWindowDays);
               bool questBeat = response.QuestCompleted != null;

               if (courted != null && JealousyTriggerPolicy.IsPlausibleCourtship(tier, plausiblePair, romanticEvents, questBeat))
                  JealousyBehavior.Instance?.OnRomanticAct(courted, tier, _witnesses.Count > 0, gameDay);
               else
                  ModLog.Info($"[JEALOUSY] act ignored as courtship ({tier}{(plausiblePair ? "" : ", implausible pair")}, {romanticEvents} romantic event(s){(questBeat ? ", quest turn-in" : "")}).");
            }

            // Negotiation Phase 3: a leveraged intimacy with the exploiter archetype — he now holds it
            // over the female player and may press the advantage in later talks (persisted on the profile).
            if (actType == NotableEventType.Intimacy)
            {
               Hero partner = Hero.FindFirst(h => h.StringId == profile.Id);
               if (partner != null)
               {
                  bool exploited = IntimacyBargainEligibility.IsEligible(partner);

                  if (exploited)
                     profile.IntimacyLeverageHeld = true;

                  // Appreciations pillar (KindnessInScene): a consensual tenderness the partner remembers
                  // fondly. Not when the dynamic is the exploiter pressing an advantage (leverage, not a
                  // kindness), and NOT when the model tagged a violent or coerced scene as intimacy: a turn that
                  // LOWERED the relation, or a captive/captor scene, leaves the memory but no fond mark (player
                  // report: a violent encounter at relation -20 still read "a tender moment we shared").
                  else if (ModSettings.AdultContentLevel != AdultContentLevel.Off)
                  {
                     bool relationLoweredThisTurn = response.Actions != null && response.Actions.Any(a =>
                        string.Equals(a.Type, "change_relation", StringComparison.OrdinalIgnoreCase)
                        && a.Parameters != null
                        && a.Parameters.TryGetValue("delta", out string ds)
                        && int.TryParse(ds, out int d) && d < 0);

                     if (AppreciationTonePolicy.LeavesSceneKindness(relationLoweredThisTurn, IsCaptiveEncounter || _captorScene))
                     {
                        AppreciationBehavior.Instance?.TryPlant(partner, Hero.MainHero,
                           AppreciationSource.KindnessInScene, "A tender moment we shared.",
                           AppreciationBackfillRules.KindnessInSceneMagnitude, true, gameDay);
                        // Gabriel (23/10/2026): every deed moves regard. A night the conversation itself warmed is
                        // counted there (RelationGate's intimacy bonus); one it did not, warms a little here.
                        bool relationRaisedThisTurn = response.Actions != null && response.Actions.Any(a =>
                           string.Equals(a.Type, "change_relation", StringComparison.OrdinalIgnoreCase)
                           && a.Parameters != null && a.Parameters.TryGetValue("delta", out string rs) && int.TryParse(rs, out int r) && r > 0);
                        // On this turn's own profile, which the exchange saves after. Once a day, through the intimacy
                        // bonus's own door (Lordfadooboo, Nexus 09/11/2026: "+3 regard bonus with every message").
                        if (!relationRaisedThisTurn)
                           ApplySharedNightWarmth(profile, CampaignTime.Now.ToHours);
                     }
                  }
               }
            }

            // ── Execute game actions emitted by the LLM ───────────────────
            // RelationGate may stamp profile.LastRelationGainHour, so persist
            // AFTER actions run (below), not before.
            var prisonerExecuted = false;
            var nemesisTurned = false;
            if (response.Actions is {Count: > 0})
            {
               // A sale written in parts (3chmidt, 06/11/2026): the character's coin and the player's item crossing in this
               // turn are one sale, paid and confirmed as one, not a gift capped by regard plus a free hand-over.
               IReadOnlyList<GameAction> turnActions = ReadPartsAsASale(response.Actions, _offer.OfferedItemId, speaker.Hero, profile);

               // Double-payment guard: when the LLM emits join_party it sometimes ALSO
               // emits take_gold for the same payment (the player mimed handing coins
               // over) — join_party already transfers the settled price, so honoring
               // both charges the player twice (observed in testing: 550 + 546).
               bool hasJoinParty = turnActions.Any(a =>
                  string.Equals(a.Type, "join_party", StringComparison.OrdinalIgnoreCase));

               // Same shape for every deal that moves its own coin (tashmetu, 27/09/2026: a captive ransomed): a
               // give_gold or take_gold beside it ("I will pay it now") is the same payment written twice.
               List<string> actionTypesThisTurn = turnActions.Select(a => a?.Type).ToList();
               // One regard cost per deed (09/11/2026): an extort charges its own regard hit whether paid or refused, so
               // the anger written beside it is the same cost. An extort the game will refuse (no hold) charges nothing.
               List<string> deedsTheGameWillCarryOut = actionTypesThisTurn
                  .Where(t => !string.Equals(t, "extort", StringComparison.OrdinalIgnoreCase) || Verbs.ExtortReadiness.WillRun(speaker.Hero))
                  .ToList();
               var saleContext = new ActionContext(profile, null) {OfferedItemId = _offer.OfferedItemId, ItemOnTheTableId = _offer.ItemOnTheTable};
               bool saleThisTurn = turnActions.Any(a => string.Equals(a?.Type, "sell_item", StringComparison.OrdinalIgnoreCase));
               var supersededBySale = new HashSet<GameAction>(TradeBatchPolicy.Superseded(new TradeBatchFacts {
                     ItemPickedFromBag = !string.IsNullOrWhiteSpace(_offer.OfferedItemId),
                     Actions = turnActions.Select(a => new TradeBatchAction {
                        Type = a?.Type,
                        Item = a?.Parameters != null && a.Parameters.TryGetValue("item", out string it) ? it : null,
                        // Only worth resolving when a sale is in the turn: the roster item each names decides, not words.
                        ResolvedId = saleThisTurn ? BannerlordGameStateBridge.PreviewItem(a, saleContext)?.Id : null
                     }).ToList()
                  })
                  .Select(i => turnActions[i]));

               // counter_offer settles its own terms (14/10/2026): fealty rides its own "fealty" term, and its
               // executor frees her itself, so a free_prisoner or join_clan beside it is the same deed read twice.
               var supersededByCounterOffer = new HashSet<GameAction>(
                  TradeBatchPolicy.SupersededByCounterOffer(actionTypesThisTurn).Select(i => turnActions[i]));

               // M-R3: at content Off an intimacy [EVENT] is a hallucination (the romance system is disabled), so
               // it must not grant the RelationGate's intimacy-tier boost (+3, cooldown bypassed). Downgrade it so
               // change_relation is gated as a routine gain, matching the status arc being frozen off above.
               NotableEventType? emittedForGate = response.NewEventData?.Type;
               if (emittedForGate == NotableEventType.Intimacy && ModSettings.AdultContentLevel == AdultContentLevel.Off)
                  emittedForGate = null;

               string playerWords = string.Join("\n", _transcript.Lines.Where(l => l != null && l.IsPlayer && !string.IsNullOrWhiteSpace(l.Text)).Select(l => l.Text));
               var actionContext = new ActionContext(profile, emittedForGate) {
                  OfferedItemId = _offer.OfferedItemId,
                  OfferedBundle = _offer.OfferedBundle,
                  ItemOnTheTableId = _offer.ItemOnTheTable,
                  PlayerWordsThisConversation = playerWords,
                  PresentHeroIds = SeatedMembers().HeroIds.Where(id => !string.IsNullOrEmpty(id)).ToList(),
                  RegardTakenThisConversation = _regardTaken.TakenFrom(speaker.Hero?.StringId)
               };
               // B2 (quest audit): at most one change_relation applies per turn, so a weak model that emits
               // several negative blocks cannot pile the RelationGate's per-call -15 floor into -75.
               var relationChangedThisTurn = false;
               var worstAppliedRelation = 0; // 2.1: the steepest regard change actually applied this turn, for the affront rumor.
               // v1.34.2 log-only execution audit (Pattern 1, observability): the set of action types this
               // turn actually taught, so an [ACTION] that runs yet was never taught can be LOGGED (never
               // refused, no control-flow change). Null on a mapping fault, which disables the audit for the turn.
               ISet<string> taughtThisTurn = TryBuildTaughtActionSet(context);
               foreach (GameAction action in turnActions)
               {
                  // request_privacy, end_conversation and witness_leaves are
                  // chat-flow controls, handled below — not bridge actions.
                  if (string.Equals(action.Type, "request_privacy",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  if (string.Equals(action.Type, "end_conversation",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  if (string.Equals(action.Type, "witness_leaves",
                         StringComparison.OrdinalIgnoreCase)) continue;
                  // retire: a companion audience where the player granted their leave. Flagged now and executed
                  // at CLOSE (never remove a hero mid-conversation), so the departure/step-back is deferred.
                  if (string.Equals(action.Type, "retire", StringComparison.OrdinalIgnoreCase))
                  {
                     if (_audienceReason == CompanionAudienceReason.Retirement)
                     {
                        _retireGranted = true;
                        AppendSystemMessage(
                           _audienceLanded
                              ? $"{profile.Name} will step back from the field, with your blessing."
                              : $"{profile.Name} will retire from your service, with your blessing.", true);
                     }

                     continue;
                  }

                  // A BRIEFING PLANS; IT DOES NOT ACT (Gabriel, 13/09/2026). He briefed Nabb to charm Ira and
                  // the game answered "Nabb the Bloody Handed swears to stand at your side in battle within 21
                  // days" - a persisted commitment about a war, born in a conversation about a seduction.
                  //
                  // Three things had to line up and all three are worth naming, because only the last one can be
                  // fixed here. The prose model was NOT taught swear_oath this turn; the ACTION INTERPRETER, which
                  // re-reads the prose and re-derives the tags, is given the WHOLE GameActionCatalog as a static
                  // cacheable prefix and so knows nothing of this turn's gating; and it read "I'll do it" as a
                  // vow, which is a fair reading of the words. The audit line below already SAID so ("'swear_oath'
                  // executed but was not taught this turn") and the action ran anyway, because that audit is
                  // deliberately log-only - the taught-set is a mirror of the SDK's gating and a mirror can be
                  // wrong, so refusing on it would turn every mirror bug into a broken feature.
                  //
                  // This gate needs none of that. IsCompanionBriefingChat is a hard scene flag, not a mirror. The
                  // briefing is the player and his own people planning off to one side; it is not the meeting, and
                  // nothing decided in it happens until the meeting it is FOR. That is already why no [EVENT] is
                  // persisted here (see the drop above); a mechanical deed is the same rule with more at stake,
                  // since a deed writes the world and a memory only writes a mind.
                  //
                  // No OOC refusal is shown. The prose stands as written - Nabb agreeing to a plan is exactly what
                  // happened - and the player is told nothing was refused, because from inside the fiction nothing
                  // was attempted. Whatever he commits to, he commits to in front of Ira, where the verb is taught
                  // or it is not. Logged, never silent.
                  //
                  // The chat-flow controls above (request_privacy, end_conversation, witness_leaves, retire) are
                  // already past this point: they steer the window, they do not touch the world.
                  if (IsCompanionBriefingChat)
                  {
                     ModLog.Info($"[BRIEFING] dropped the [ACTION] '{action.Type}' inside the private briefing "
                               + "chat (a briefing plans; the meeting acts).");
                     NoteLoggedAction($"{action.Type} dropped: a briefing plans, the meeting acts.");

                     continue;
                  }

                  // v1.34.2 log-only audit: this action is about to be dispatched. If it was never taught this
                  // turn, leave a trace so hallucinated / untaught executions are visible for a future enforcing
                  // pass. Purely observational: it does not skip, refuse, or reorder anything.
                  AuditActionTaught(action, taughtThisTurn, profile);

                  if (hasJoinParty &&
                      string.Equals(action.Type, "take_gold",
                         StringComparison.OrdinalIgnoreCase))
                  {
                     ModLog.Info("[ACTION] take_gold dropped — join_party in the same response already transfers the hire payment.");
                     NoteLoggedAction("take_gold dropped: join_party in the same response already transfers the hire payment.");

                     continue;
                  }

                  // A sale read twice (3chmidt, 13/10/2026): beside a sell_item, a give_item of the same thing is the sale.
                  if (supersededBySale.Contains(action))
                  {
                     ModLog.Info($"[ACTION] {action.Type} dropped: a sale in the same response already moves this item.");
                     NoteLoggedAction($"{action.Type} dropped: a sale in the same response already moves this item.");

                     continue;
                  }

                  // A counter_offer settled read twice (14/10/2026): beside it, a free_prisoner or join_clan is the
                  // same bargain the counter_offer already carries.
                  if (supersededByCounterOffer.Contains(action))
                  {
                     ModLog.Info($"[ACTION] {action.Type} dropped: a counter_offer in the same response already settles this.");
                     NoteLoggedAction($"{action.Type} dropped: a counter_offer in the same response already settles this.");

                     continue;
                  }

                  if (TurnPaymentPolicy.IsSamePaymentAsADealThisTurn(action.Type, actionTypesThisTurn))
                  {
                     ModLog.Info($"[ACTION] {action.Type} dropped: a deal in the same response already moves the coin.");
                     NoteLoggedAction($"{action.Type} dropped: a deal in the same response already moves the coin.");

                     continue;
                  }

                  // Council actor attribution: an [ACTION] carrying "actor: Name" belongs to that SEATED
                  // member, not to whoever carries this turn's spoken reply. No "actor" parameter (every
                  // ordinary, non-council conversation) resolves to the fallback unchanged, so this is a no-op
                  // outside a council. An actor the mod cannot place (unknown or ambiguous) drops the action
                  // outright rather than ever guessing (see TryResolveActionActor).
                  if (!TryResolveActionActor(action, speaker.Hero, profile, behavior, out Hero actorHero, out NpcProfile actorProfile))
                     continue;

                  ActionContext actorActionContext = ReferenceEquals(actorProfile, profile)
                     ? actionContext
                     : new ActionContext(actorProfile, emittedForGate) {OfferedItemId = _offer.OfferedItemId, OfferedBundle = _offer.OfferedBundle, ItemOnTheTableId = _offer.ItemOnTheTable, PlayerWordsThisConversation = playerWords, PresentHeroIds = actionContext.PresentHeroIds, RegardTakenThisConversation = _regardTaken.TakenFrom(actorHero?.StringId)};

                  // Coin/commit actions are preprocessed: a repeat of one already resolved this conversation is
                  // dropped (the LLM re-emitting the same transfer), and a player-outgoing spend (take_gold /
                  // join_party) is queued for in-chat confirmation. give_gold and everything else run inline.
                  ActionDisposition disposition =
                     _confirmation.Preprocess(action, actorHero, actorProfile, actorActionContext);

                  if (disposition != ActionDisposition.ExecuteInline) continue;

                  // B2: the first regard change this turn wins; extras are dropped (see relationChangedThisTurn).
                  if (string.Equals(action.Type, "change_relation", StringComparison.OrdinalIgnoreCase))
                  {
                     int? regardDelta = action.Parameters != null && action.Parameters.TryGetValue("delta", out string rds) && int.TryParse(rds, out int rdv) ? rdv : (int?) null;
                     if (TurnRegardPolicy.IsSameCostAsADeedThisTurn(new TurnRegardFacts {ActionType = action.Type, Delta = regardDelta, DeedsTheGameWillCarryOut = deedsTheGameWillCarryOut}))
                     {
                        ModLog.Info("[ACTION] change_relation dropped: a deed in the same response already charges its own regard cost.");
                        NoteLoggedAction("change_relation dropped: a deed in the same response already charges its own regard cost.");

                        continue;
                     }

                     // M-R3 (regard audit): a quest completed this turn already pays its fixed, verified
                     // RewardRelation. A POSITIVE change_relation the model also emits for "completing the task"
                     // would double-count the same deed, so drop it on a quest-completion turn. Negatives still
                     // apply: an offense given during a turn-in is real. Only when the reward is REALLY paid
                     // (Aporo, 16/11/2026: a refused claim lost him the warmth and paid nothing): QuestClaimRegardPolicy.
                     QuestCompletionClaim claim = response.QuestCompleted;
                     if (claim != null
                         && action.Parameters != null
                         && action.Parameters.TryGetValue("delta", out string qds)
                         && int.TryParse(qds, out int qdv)
                         && QuestClaimRegardPolicy.IsPaidByTheQuestReward(new QuestClaimRegardFacts {
                            CompletionClaimed = true,
                            Delta = qdv,
                            ClaimWillBePaid = !claim.TypeUnrecognized && profile?.ActiveQuests != null
                                                                      && profile.ActiveQuests.Any(q => (q.IsAwaitingReward || EscapedTargetPolicy.MaySettleByVictory(q))
                                                                                                       && (claim.Type == null || q.Type == claim.Type.Value)),
                            HandOverSettlesATask = actionTypesThisTurn.Any(t => HandOverTypes.Contains(t ?? ""))
                                                   && profile?.ActiveQuests != null
                                                   && profile.ActiveQuests.Any(q => (q.IsOutstanding || q.SatisfiedOnDay == (int) CampaignTime.Now.ToDays)
                                                                                    && (q.Type == QuestType.ReinforceGarrison || q.Type == QuestType.RecruitTroops))
                         }))
                     {
                        ModLog.Info("[ACTION] change_relation (positive) dropped: the quest reward this turn already covers the goodwill (M-R3).");
                        NoteLoggedAction("change_relation (positive) dropped: the quest reward this turn already covers the goodwill (M-R3).");

                        continue;
                     }

                     if (relationChangedThisTurn)
                     {
                        ModLog.Info("[ACTION] change_relation dropped: at most one regard change applies per turn.");
                        NoteLoggedAction("change_relation dropped: at most one regard change applies per turn.");

                        continue;
                     }

                     relationChangedThisTurn = true;
                  }

                  // B3 (quest audit): an executor that throws must not abort the whole turn (skipping the
                  // remaining actions AND the Store.Set that persists this turn's mutations). Isolate each call.
                  // The conversation log: the regard as it stood, to say what the deed moved (the gate applies it silently).
                  int? regardBefore = _chatLog != null ? actorProfile?.ReputationWithPlayer : null;
                  ActionOutcome outcome;
                  try
                  {
                     outcome = behavior.GameStateBridge.ExecuteAction(action, actorHero, actorActionContext);
                  }
                  catch (Exception ex)
                  {
                     ModLog.Error($"[ACTION] '{action.Type}' threw and was skipped so the rest of the turn still applies: {ex.GetType().Name}: {ex.Message}");

                     continue;
                  }

                  if (outcome != null)
                  {
                     // Unknown-verb refusal (the model invented an [ACTION] type with no executor): ALWAYS log
                     // the invented verb with its context to a dedicated file so the author can add it later,
                     // and show the deed-scoped notice (plus a report hint only when the player opted in) rather
                     // than the raw refusal line, which read as contradicting a scene where real effects landed.
                     // A known verb refused by a guard keeps its own message exactly as before.
                     if (outcome.WasUnknownVerb)
                     {
                        LogUnknownAction(action, actorProfile?.Name, npcLine, playerMessage);
                        AppendSystemMessage(
                           UnknownActionNotice.Compose(ModSettings.ReportUnsupportedActions), outcome.IsPositive);
                     }
                     else
                     {
                        AppendSystemMessage(outcome.Message, outcome.IsPositive);
                     }
                  }

                  // The conversation log (Gabriel, 28/11/2026): the deed as the game reported it, refused or done.
                  if (_chatLog != null)
                  {
                     NoteLoggedAction(Logic.Diagnostics.ChatLogActionText.For(new Logic.Diagnostics.ChatLogActionFacts {
                        Verb = action.Type, Reported = outcome != null, WasRefused = outcome?.WasRefused == true,
                        WasUnknownVerb = outcome?.WasUnknownVerb == true, Message = outcome?.Message
                     }));
                     int? regardAfter = actorProfile?.ReputationWithPlayer;
                     if (regardBefore != null && regardAfter != regardBefore)
                        NoteWhy(Logic.Diagnostics.ChatLogWhyKind.Regard, $"{actorProfile.Name}'s regard {regardBefore} -> {regardAfter} ({action.Type}).");
                     else if (string.Equals(action.Type, "change_relation", StringComparison.OrdinalIgnoreCase))
                        NoteWhy(Logic.Diagnostics.ChatLogWhyKind.Regard, $"change_relation moved nothing: {actorProfile?.Name}'s regard stays {regardAfter} (the gate banked or held it).");
                  }

                  // Extension Surface (ActionResult.Recap): an external deed resolved to a fact the NPC's
                  // already-spoken reply this turn could not know (a ruling upheld or denied, a decision taken).
                  // Capture the first one; a voice-only follow-up turn fired at turn end has the NPC speak it in
                  // its own words. Never on a recap turn itself (isOutcomeReaction) or an ensemble voice, so it can
                  // neither chain into another recap nor fire from a partial group voice.
                  if (!isOutcomeReaction && !suppressGroupTail && outcome != null
                      && string.IsNullOrEmpty(_pendingOutcomeRecap) && !string.IsNullOrWhiteSpace(outcome.Recap))
                     _pendingOutcomeRecap = outcome.Recap;

                  // The bridge REFUSED the action (an active war, a captivity, a gate the prompt let
                  // through): the refusal must reach the TRANSCRIPT, not only the UI line — otherwise the
                  // model never learns the marriage/transfer it just celebrated never happened, believes
                  // it did, and the next turns build on the lie. Same OOC pattern as a declined
                  // confirmation (ActionConfirmationController.DeclineAction).
                  // bdionysis80 (21/11/2026): an invented verb's note names the verb, so the model cannot pin it on
                  // a real deed of the same turn (the coin it had just been paid).
                  string refusalNote = outcome?.WasUnknownVerb == true
                     ? ActionRefusalNote.ForUnknownVerb(action.Type)
                     : ActionRefusalNote.For(outcome?.WasRefused == true, outcome?.Message);
                  if (refusalNote != null)
                  {
                     // LOGGED, because it was not. A refused action is the single most diagnostic thing an
                     // action turn produces and it left no trace whatever: Gabriel watched Ira agree to take
                     // Nabb into her service and nothing happen, and the log showed only that the verb ran.
                     // Now the reason is in the log next to the verb that gave it.
                     ModLog.Info($"[ACTION] {action.Type} REFUSED by the bridge: {outcome?.Message}");

                     try { RecordTranscript(true, "You", Hero.MainHero?.StringId, refusalNote); }
                     catch (Exception ex) { ModLog.Error($"[ACTION] refusal note failed: {ex.GetType().Name}: {ex.Message}"); }
                  }

                  // Memories panel: stamp this executed deed onto the turn's memory (if one was recorded), so the
                  // journal marks a REAL mechanical consequence in blue. Only actions the bridge actually applied
                  // (not refused, not an invented verb) earn a tag; the relation verbs colour by direction.
                  if (turnEvent != null && outcome != null && !outcome.WasRefused && !outcome.WasUnknownVerb)
                  {
                     string deedTag = ActionMemoryLabel.Describe(action.Type, outcome.IsPositive);
                     if (!string.IsNullOrEmpty(deedTag))
                        turnEvent.ActionTag = string.IsNullOrEmpty(turnEvent.ActionTag)
                           ? deedTag
                           : $"{turnEvent.ActionTag}, {deedTag}";
                  }

                  // execute_prisoner (Nemesis pillar, R2): the prisoner is dead, so the scene locks
                  // afterward exactly like a normal end_conversation. The bridge already re-validated
                  // custody, so IsPositive true here means the killing genuinely happened.
                  if (string.Equals(action.Type, "execute_prisoner", StringComparison.OrdinalIgnoreCase) &&
                      outcome?.IsPositive == true)
                     prisonerExecuted = true;

                  // turn_nemesis (Nemesis pillar, increment 4): a positive outcome means the bridge's gate
                  // accepted the turn and he is now sworn to the player. The captor scene is over (he is an
                  // ally, not a captive), so it locks afterward exactly like execute_prisoner does.
                  if (string.Equals(action.Type, "turn_nemesis", StringComparison.OrdinalIgnoreCase) &&
                      outcome?.IsPositive == true)
                     nemesisTurned = true;

                  // Story archive (Gabriel, 17/11/2026): the deed as the game did it, told as a sentence, attributed to the
                  // ACTOR (the anchor's own profile when no "actor" was named). The regard and coin ACTUALLY applied, never
                  // the raw proposal (StoryArchiveDeed); a change gated to 0 returns a null outcome and leaves no line.
                  ArchiveDeed(action, outcome, actorProfile?.Name, actorHero?.StringId);
                  if (string.Equals(action.Type, "change_relation", StringComparison.OrdinalIgnoreCase)
                      && outcome?.AppliedRelationDelta is int applied && applied != 0 && applied < worstAppliedRelation)
                     worstAppliedRelation = applied;
                  // Lordfadooboo (26/11/2026): what this character took, and gave back, in this conversation.
                  if (string.Equals(action.Type, "change_relation", StringComparison.OrdinalIgnoreCase) && outcome?.AppliedRelationDelta is int moved && moved != 0)
                     _regardTaken.Note(actorHero?.StringId, moved);
               }

               // The personal score may have moved — refresh the header (main NPC only;
               // the header reflects the conversation's host, not a routed witness).
               if (speaker.IsMain)
                  SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer ?? 0, profile.OwnGameRelationWithPlayer);

               // Conversation-outcome rumor (v1.2): a steep affront before witnesses spreads
               // as gossip through the world-event store instead of evaporating at close.
               if (speaker.IsMain && _witnesses.Count > 0)
                  ConversationEventFeeder.RecordPublicAffront(speaker.Hero, worstAppliedRelation, _witnesses.Count);
            }

            // ── Marriage-memory audit: confirm or drop a withheld wedding claim ──
            // The action loop above just had its one chance to run a real [ACTION] type: marry
            // (BannerlordGameStateBridge.ExecuteMarry re-validates every guard before calling
            // MarriageAction.Apply), so Hero.Spouse now tells the truth whatever the model narrated.
            if (withheldMarriageClaim != null)
            {
               bool actuallyMarried = speaker.Hero != null &&
                  (speaker.Hero.Spouse == Hero.MainHero || Hero.MainHero?.Spouse == speaker.Hero);

               if (actuallyMarried)
               {
                  ProfileMutator.ApplyNotableEvent(profile, withheldMarriageClaim.Type, withheldMarriageClaim.Summary, gameDay);
                  ModLog.Info($"[EVENT] marriage claim confirmed by Hero.Spouse, recorded for {profile.Name}.");
               }
               else
               {
                  ModLog.Info($"[EVENT] marriage claim from {profile.Name} refused as unbacked (Hero.Spouse does not confirm it): \"{withheldMarriageClaim.Summary}\"");
                  AppendSystemMessage(
                     $"({profile.Name} spoke of a marriage that was never actually sealed, no wedding took place. " +
                     $"Run cr.marry_check {profile.Name} to see why.)", false);
               }
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
                  // A prisoner cannot refuse the player: whatever the LLM emitted, the player's own captive
                  // yields the room (player suggestion, ApocalyptikGreen 2026-07-10).
                  // whiskeypatt (Discord, 25/11/2026): a witness who says yes cannot clear a room that is not theirs.
                  bool privacyAccepted = PrivateAudiencePolicy.ClearsTheRoom(new PrivacyAnswerFacts {
                     FromMainCharacter = speaker.IsMain,
                     Accepted = string.Equals(privacyResult, "accepted", StringComparison.OrdinalIgnoreCase),
                     MainIsPlayersPrisoner = NpcIsPlayerPrisoner()
                  });
                  if (!speaker.IsMain) ModLog.Info($"[WITNESSES] {speaker.Name} answered a request for privacy that was not theirs to grant; ignored.");
                  if (privacyAccepted)
                  {
                     bool hadWitnesses = _witnesses.Count > 0;
                     _witnesses.Clear();
                     _currentSpeakerHeroId = null; // no witnesses left — back to the main NPC
                     RefreshWitnessUi();
                     // Said, not only done (IdentityCrysys, 05/10/2026): the player sees the room is theirs now.
                     if (hadWitnesses) AppendSystemMessage($"The others are out of earshot. You are alone with {profile.Name}.", true);
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
            // Three paths lock input so the player can only leave:
            // 1. end_conversation action (NPC refuses — anger, insult, etc.)
            // 2. farewell event (NPC naturally closes the exchange)
            // 3. execute_prisoner action (the prisoner is dead; see below)
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

                  if (!NpcEndLocksConversation())
                  {
                     // MCM "NPCs can end conversations" is off: the NPC's parting is advisory, the player keeps
                     // the last word and closes the chat themselves. Captive/captor scenes never reach here.
                     ModLog.Info($"[CONVERSATION] {profile.Name} tried to end; kept open (MCM: NPCs may not end).");

                     break;
                  }

                  _conversationEnded = true;
                  IsSendEnabled = false;
                  IsPrivateRequestVisible = false;
                  CloseButtonText = "Leave";
                  if (IsCaptiveEncounter) IsPleaseContinueVisible = true;
                  NotifyConversationEndedChanged();
                  AppendSystemMessage($"{profile.Name} has ended the conversation.", false);
                  ModLog.Info($"[CONVERSATION] {profile.Name} ended the conversation.");

                  break;
               }

            if (!_conversationEnded && response.NewEventData?.Type == NotableEventType.Farewell
                && !NpcEndLocksConversation())
            {
               // MCM off: a farewell no longer closes the chat; the player replies or leaves on their own.
               ModLog.Info("[CONVERSATION] farewell event; kept open (MCM: NPCs may not end).");
            }
            else if (!_conversationEnded && response.NewEventData?.Type == NotableEventType.Farewell)
            {
               _conversationEnded = true;
               IsSendEnabled = false;
               IsPrivateRequestVisible = false;
               CloseButtonText = "Leave";
               NotifyConversationEndedChanged();
               // The lock used to be silent, on the reasoning that the NPC had just said goodbye in
               // character. In play it reads as the chat going dead: Gabriel reported having to leave a
               // conversation Mina HAD already closed (2026-07-18), because a greyed input and a changed
               // button caption are easy to miss at the end of a long reply. An explicit parting NOTICE
               // (never the red loss colour: she took her leave, she did not rebuff him) states it.
               AppendSystemNotice($"{profile.Name} has taken their leave. You may close the conversation.");
               ModLog.Info("[CONVERSATION] farewell event — input locked.");
            }

            // 3. execute_prisoner action (Nemesis pillar, R2): the prisoner is dead, there is no one
            //    left to converse with, so the scene locks like any other close. turn_nemesis (increment 4)
            //    ends it the same way: the captive is now a sworn ally, not a prisoner to keep menacing.
            if (!_conversationEnded && (prisonerExecuted || nemesisTurned))
            {
               _conversationEnded = true;
               IsSendEnabled = false;
               IsPrivateRequestVisible = false;
               CloseButtonText = "Leave";
               NotifyConversationEndedChanged();
               ModLog.Info(nemesisTurned
                  ? "[CAPTOR SCENE] turn_nemesis ended the scene; the nemesis is turned."
                  : "[CAPTOR SCENE] execute_prisoner ended the scene.");
            }
            // ─────────────────────────────────────────────────────────────

            // ── Informal quests: issue a newly offered task, pay out a verified one ──
            QuestService.ApplyResponse(
               profile, speaker.Hero, response, gameDay,
               behavior.GameStateBridge, behavior.Store,
               (text, positive) => AppendSystemMessage(text, positive));

            behavior.Store.Set(profile);
            RefreshOpenDossier();

            // Mark any delivered player letters read now that the NPC has responded.
            PlayerLetterService.MarkLettersRead(profile, behavior.Store);

            // A tale's arc paces this conversation (Gabriel, 25/10/2026: "ca pourrait etre trop court ou trop long"): each
            // reply of the character it names is one exchange, counted once its deeds are applied. Its last closes the chat
            // softly, so the player reads the farewell and leaves; the stage then completes on the close as before.
            // A turn that is not counted drops the signals reported in it, so none leaks into the next exchange.
            Quests.AuthoredQuestBehavior tales = Quests.AuthoredQuestBehavior.Instance;
            if (speaker.IsMain && tales != null)
            {
               if (!isSceneOpener && !isTaleOpener && !IsCaptiveEncounter && !_conversationEnded)
               {
                  if (tales.ArcExchanged(speaker.Hero)) EndTaleArcSoftly();
               }
               else
               {
                  tales.ArcSignalsDropped();
               }
            }

            // ── Round table: the player opened the floor to the group this turn ──
            // Takes precedence over the model-driven 15C auto-exchange: instead of hoping the model
            // volunteered [WITNESS_REACTION]s, give each present witness a deterministic turn (they may
            // address one another), capped, then return control to the player. Model-independent.
            // The scene opener never opens a lap or an exchange: one more call nobody asked for, and the player waiting.
            if (!suppressGroupTail && !isSceneOpener && _roundTable.TryConsumeForMainReply(new RoundTableGate.FloorTurnFacts {SpeakerIsMain = speaker.IsMain, IsCaptiveEncounter = IsCaptiveEncounter, ConversationEnded = _conversationEnded}))
            {
               foreach (var (wName, wHeroId, wText) in witnessReactions)
                  RecordTranscript(false, wName, wHeroId, wText); // preserve order: any reactions precede the lap
               _npcWitnessExchangePending = true;
               _ = RunRoundTableLapAsync(PresentWitnessSpeakers());

               return; // finally runs but input stays locked (_npcWitnessExchangePending)
            }

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
            // A gesture-only reaction (a wordless *smirk*, silence) must never trigger this on its own:
            // player report showed the main NPC treating a bystander's mute gesture as being blown off
            // and escalating ("silent treatment") against a silence the same model just invented. So the
            // auto counter-reply requires at least one witness who actually SPOKE (WitnessSpeechDetector);
            // a purely wordless reaction still renders above and still reaches the transcript below,
            // it just never drives this loop.
            bool anyWitnessSpoke = witnessReactions.Exists(r => WitnessSpeechDetector.HasSpokenWords(r.text));

            // Lordfadooboo (20/10/2026): a line that ends on a question to the player hands the floor to the player;
            // answering the bystander from there only re-asks it. The bystander's line stands and the player answers.
            bool waitsForThePlayer = WitnessExchangePolicy.WaitsForThePlayer(NovelSpoken(npcLine), _witnesses.Select(w => w.Name));
            if (waitsForThePlayer && anyWitnessSpoke && speaker.IsMain)
               ModLog.Info($"[15C] {speaker.Name} asked the player a question; the bystander exchange waits for the answer.");

            if (!suppressGroupTail && !isSceneOpener && anyWitnessSpoke && speaker.IsMain && !IsCaptiveEncounter && !waitsForThePlayer
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
            if (response.Actions is {Count: > 0})
               ModLog.Info($"  [ACTIONS] {response.Actions.Count} action(s) dispatched.");
            if (result.Usage != null)
               ModLog.Info($"  [TOKENS] prompt={result.Usage.PromptTokens} (cached:{result.Usage.CachedPromptTokens ?? 0}) | completion={result.Usage.CompletionTokens}" +
                           (result.WasRetried
                              ? " | RETRY fired (first attempt was empty or truncated)"
                              : ""));

            LogVramSample("main");
            // Surface a non-"stop" finish reason — "length" means the model cut the reply
            // off (a known DeepSeek quirk); this makes truncated-message reports diagnosable.
            if (!string.IsNullOrEmpty(result.FinishReason) && !string.Equals(result.FinishReason, "stop", StringComparison.OrdinalIgnoreCase))
            {
               ModLog.Info($"  [FINISH] reason={result.FinishReason} (non-stop — reply may be truncated by the model/provider)");

               // A PROVIDER content-filter cut (players read a mid-sentence stop as the MOD censoring):
               // say plainly whose scissors these are. Only after the SDK's automatic re-roll also came
               // back filtered, so a quietly recovered reply never carries a scary banner.
               string normalizedFinish = result.FinishReason.Replace("-", "").Replace("_", "");
               if (string.Equals(normalizedFinish, "contentfilter", StringComparison.OrdinalIgnoreCase) || string.Equals(normalizedFinish, "moderation", StringComparison.OrdinalIgnoreCase))
                  AppendSystemMessage("The model host's own content filter cut this reply short. This is the provider, not the mod; another attempt, model, or provider usually passes.", false);
            }
         }
         catch (Exception ex)
         {
            if (isSceneOpener) SettleSceneOpener(SceneOpenerOutcome.Failed);
            else ReplacePlaceholder(placeholder, "(My voice falters...)", speaker);
            ModLog.Error($"RunLlmExchangeAsync threw {ex.GetType().Name}: {ex.Message}");
            ModLog.Error($"Stack: {ex.StackTrace}");
         }
         finally
         {
            // A bag offer is consumed by the turn it was sent with — clear it so a later, unrelated
            // give_item never re-transfers the previously picked item.
            // A scene opener never carried the offer: one the player made meanwhile belongs to his own turn.
            if (!isSceneOpener) _offer.ClearOfferedItem();

            // A scene the player spoke over leaves the input to his turn, which owns it now.
            bool sceneSetAside = isSceneOpener && _sceneOpenerAbandoned;
            if (isSceneOpener) EndSceneOpenerFlight();

            // The NPC's opening turn (if any) is delivered now — reveal the chat input (unless the scene paused,
            // which hides it in its own right). Notify so the InputBox/Send/"<" reappear.
            if (_awaitingOpener)
            {
               _awaitingOpener = false;
               OnPropertyChanged(nameof(IsChatInputVisible));
               OnPropertyChanged(nameof(IsComposeToggleVisible));
            }

            // Keep input locked if the NPC ended the conversation, if a 15C witness-exchange
            // is in flight, if a scene continuation is pending, or if we are paused waiting
            // for the player to press Continue / Try something. Each async path re-enables
            // input in its own finally when it terminates.
            if (!suppressGroupTail && !sceneSetAside && !_npcWitnessExchangePending && !_sceneContinuationPending && !_isScenePaused)
            {
               // Extension Surface: a deed resolved to an outcome AFTER the NPC's spoken reply this turn (an
               // external ActionResult.Recap). Fire ONE voice-only follow-up so the NPC speaks it, and leave input
               // LOCKED (that follow-up turn re-enables it in its own finally, exactly like a witness exchange).
               // Never chains off a recap turn (isOutcomeReaction) and only from a live, unfinished conversation.
               var firedRecap = false;
               if (!isOutcomeReaction && !_conversationEnded && !string.IsNullOrWhiteSpace(_pendingOutcomeRecap))
               {
                  string recap = _pendingOutcomeRecap;
                  _pendingOutcomeRecap = null;
                  firedRecap = BeginOutcomeReactionTurn(recap);
               }

               if (!firedRecap)
               {
                  _pendingOutcomeRecap = null;
                  IsSendEnabled = !_conversationEnded;
                  // Reply delivered and control is back with the player: raise any queued action confirmation now
                  // (PromptNext drops them instead if the conversation just ended).
                  if (_confirmation.HasPending && !_confirmation.IsAwaiting) _confirmation.PromptNext(this);
               }
            }
         }
      }

      /// <summary>
      ///   Spotlight pillar: the ENSEMBLE turn. Once a witness has been drawn in (the cast has two or more active
      ///   voices), the player's turn is answered by EACH active participant in sequence, the one they addressed
      ///   leading. Every active voice runs the SAME per-speaker exchange the 1:1 path uses (its own reply,
      ///   actions, events, memory), but with the group tail suppressed so the passive-witness reactions, the 15C
      ///   auto-exchange, and the input re-enable happen ONCE here, not once per voice. Sequential (awaited) so
      ///   each voice hears the ones before it through the shared transcript. Passive-witness reactions inside an
      ///   ensemble turn are a later increment.
      /// </summary>
      private async Task RunEnsembleTurnAsync(string text)
      {
         try
         {
            // The one addressed leads the beat; the rest of the active cast follow, the anchor among them.
            ChatSpeaker primary = ResolveAddressedSpeaker(text); // sets the sticky speaker and promotes a freshly-addressed witness
            RefreshWitnessUi();

            var order = new List<ChatSpeaker> {primary};
            foreach (string id in _cast.ActiveIds)
            {
               if (id == primary.Hero?.StringId) continue;
               ChatSpeaker s = SpeakerForActive(id);
               if (s != null) order.Add(s);
            }

            ModLog.Info($"[SPOTLIGHT] ensemble turn: {order.Count} active voice(s) [{string.Join(", ", order.Select(o => o.Name))}], '{primary.Name}' leads.");

            // Player line + per-turn resets, once (mirrors the ordinary ExecuteSendMessage branch).
            AppendPlayerMessage(text, false);
            IsSendEnabled = false;
            InputText = "";
            if (_isComposeOpen) IsComposeOpen = false;
            _witnessExchangesThisRound = 0;
            _sceneContinuations = 0;
            _hangingAnswersThisBeat = 0;

            // Where this beat starts in the transcript, so the answering pass below reads only what THIS beat
            // produced and can never be triggered by a question left over from an earlier one.
            int beatStart = _transcript.Count;

            for (var i = 0; i < order.Count; i++)
            {
               ChatSpeaker s = order[i];
               ChatMessageViewModel placeholder = MakeSpeakerMessage(s, PlaceholderText);
               _messages.Add(placeholder);

               // WHAT THIS VOICE IS ANSWERING, and getting it wrong produced the worst leak of 13/09/2026.
               //
               // Every voice used to be handed the PLAYER's line as its stimulus, which is right for the one
               // who leads and wrong for everyone after. Gabriel typed "*I shift my attention to Ira, waiting
               // for her reaction*". Ira answered it and closed the deal. Then Nabb was handed the SAME line -
               // a line asking for Ira's reaction - and the model gave the only completion that sentence has:
               // it wrote Ira's reaction, in Ira's voice, ending "Do we have a bargain?". Gabriel answered
               // "We do", and the real Ira then closed the same deal a second time. That is the repetition he
               // reported, and the voice hijack, and they are one fault.
               //
               // A co-voice is answering the ROOM as it stands, so its stimulus is the last thing said in it,
               // attributed. The player's line is already in the shared transcript (the lead voice committed
               // it), so nothing is lost - what changes is which message the model is completing. Same shape
               // the 15C exchange has always used.
               string stimulus = i == 0 ? text : (LastThingSaidInTheRoom() ?? text);

               // The first voice commits the player line to the transcript; the rest suppress it (committed once).
               // Every voice suppresses the group tail: the orchestrator owns it below.
               await RunLlmExchangeAsync(stimulus, s, placeholder, suppressPlayerLine: i > 0, suppressGroupTail: true);

               if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return; // chat closed/replaced mid-sequence
               if (_conversationEnded) break;                                   // a voice ended the meeting (e.g. a duel armed)
            }

            // ── THE BEAT MUST NOT END ON A HANGING QUESTION (Gabriel, 13/09/2026) ────────────────────────
            // The ordered pass above is decided BEFORE anyone has spoken, so a voice late in the order can put
            // a question to a voice whose turn is already spent, and the floor then goes back to the player
            // with it hanging. Gabriel hit exactly that: Nabb turned to Ira and asked "Which ones do you
            // remember?", and to get an answer he had to spend his own turn typing "*I watch and wait*".
            // One answering turn closes it. See EnsembleFloorPolicy for why a question mark, and nothing
            // looser, is the signal.
            await AnswerAnyHangingQuestionAsync(beatStart);

            if (!ReferenceEquals(ChatManager.ActiveViewModel, this)) return;

            // The group tail, once: return the floor to the player and raise any queued action confirmation.
            if (!_npcWitnessExchangePending && !_sceneContinuationPending && !_isScenePaused)
            {
               IsSendEnabled = !_conversationEnded;
               if (_confirmation.HasPending && !_confirmation.IsAwaiting) _confirmation.PromptNext(this);
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SPOTLIGHT] RunEnsembleTurnAsync threw {ex.GetType().Name}: {ex.Message}");
            IsSendEnabled = !_conversationEnded;
         }
      }

      /// <summary>
      ///   The most recent spoken line in the room, attributed the way a third-party voice reaches a model
      ///   (<c>[Name]: content</c>). Null when nobody has spoken yet, which leaves the caller on the player's
      ///   own line.
      /// </summary>
      private string LastThingSaidInTheRoom()
      {
         for (int i = _transcript.Count - 1; i >= 0; i--)
         {
            TranscriptLine line = _transcript.Lines[i];
            if (line == null || line.IsPlayer || string.IsNullOrWhiteSpace(line.Text)) continue;
            if (string.IsNullOrWhiteSpace(line.SpeakerName)) continue;

            return $"[{line.SpeakerName}]: {line.Text}";
         }

         return null;
      }

      /// <summary>
      ///   Gives one participant the floor back when the beat closed on a question addressed to them, then
      ///   returns. No-op when nothing was asked, when the asker was the only voice, or when this beat has
      ///   already spent its answer (<see cref="EnsembleFloorPolicy.RepliesPerBeat" />).
      ///   <para>
      ///     The question is passed as the answering voice's stimulus even though it is already in the shared
      ///     transcript, the same shape the 15C exchange uses: the last user turn is what a model actually
      ///     answers, and without it the reply lands on the player's older message instead of the question.
      ///   </para>
      ///   <para>
      ///     Fail-soft in the strongest sense: this runs after a beat the player has already watched, so any
      ///     trouble here must cost the answer and nothing else. It never rethrows and never touches the input
      ///     state - the orchestrator's own tail owns that, and runs whatever happens in here.
      ///   </para>
      /// </summary>
      private async Task AnswerAnyHangingQuestionAsync(int beatStart)
      {
         try
         {
            if (_conversationEnded || _cast == null) return;

            var beatLines = new List<TranscriptLine>();
            for (int i = beatStart; i < _transcript.Count; i++) beatLines.Add(_transcript.Lines[i]);

            string answererId = EnsembleFloorPolicy.WhoAnswers(
               beatLines, _cast.ActiveIds, _cast.AnchorId, _hangingAnswersThisBeat, novelSpeech: NovelStyleOn);

            if (answererId == null) return;

            ChatSpeaker answerer = SpeakerForActive(answererId);
            if (answerer == null) return;

            // The asking line, attributed, exactly as the 15C stimulus is built.
            TranscriptLine question = null;
            for (int i = beatLines.Count - 1; i >= 0; i--)
            {
               TranscriptLine line = beatLines[i];
               if (line is {IsPlayer: false, IsNarration: false} && !string.IsNullOrWhiteSpace(line.SpeakerHeroId))
               {
                  question = line;

                  break;
               }
            }

            if (question == null) return;

            _hangingAnswersThisBeat++;
            ModLog.Info($"[SPOTLIGHT] the beat closed on a question from {question.SpeakerName}; "
                      + $"{answerer.Name} answers it.");

            ChatMessageViewModel placeholder = MakeSpeakerMessage(answerer, PlaceholderText);
            _messages.Add(placeholder);

            await RunLlmExchangeAsync($"[{question.SpeakerName}]: {question.Text}", answerer, placeholder,
                                      suppressPlayerLine: true, suppressGroupTail: true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SPOTLIGHT] answering the hanging question threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      // How many hanging questions this beat has already answered. Reset per player turn, alongside the 15C
      // and scene-continuation counters, so the cap bounds a BEAT rather than the whole conversation.
      private int _hangingAnswersThisBeat;

      /// <summary>
      ///   Cuts out any turn the reply wrote for SOMEBODY ELSE, above all the player's.
      ///   <para>
      ///     Gabriel's play-test, 13:31:32: Nabb's reply came back as a full script - "[Nabb]: ...", then
      ///     "[Arwa]: ..." putting a long speech in the PLAYER's mouth, then "[Nabb]: ..." again. The speech
      ///     offered Ira an alliance and Nabb's service "and whatever else may grow from that": the briefing
      ///     plan, said aloud, in the player's own voice, to the one person it existed to stay hidden from.
      ///   </para>
      ///   <para>
      ///     The cause is structural, not a bad roll. Third-party voices reach the model as "[Name]: content"
      ///     (ChatSession.AddWitnessStatement), which is a SCRIPT format; shown enough of it a model writes the
      ///     scene instead of a character, and the player is then just another part to write. That is why this
      ///     is enforced here rather than asked for in the prompt.
      ///   </para>
      ///   <para>
      ///     WHEN THE PLAYER WAS SPOKEN FOR, THIS TURN'S [EVENT] GOES TOO. The event that came back read "Arwa
      ///     formally proposed an alliance with Ira of Onira, offering Nabb's service..." - a memory of a thing
      ///     the player never did, written from words the model invented for them. Cutting the speech and
      ///     keeping the memory of it would leave the leak in the only place that outlives the conversation.
      ///     Narrow on purpose: a reply that merely scripted another NPC keeps its event.
      ///   </para>
      /// </summary>
      private static ParsedResponse TakeBackTheOtherPeoplesLines(ParsedResponse response, ChatSpeaker speaker)
      {
         if (response == null) return null;

         try
         {
            string speakerName = speaker?.Name;
            string playerName = Hero.MainHero?.Name?.ToString();

            SpeakerScriptVerdict spoken = SpeakerScriptGuard.Cut(response.Dialogue, speakerName, playerName);
            SpeakerScriptVerdict narrated = SpeakerScriptGuard.Cut(response.Narration, speakerName, playerName);

            if (!spoken.CutAnything && !narrated.CutAnything) return response;

            var who = new List<string>(spoken.CutSpeakers);
            who.AddRange(narrated.CutSpeakers);
            bool forThePlayer = spoken.SpokeForThePlayer || narrated.SpokeForThePlayer;

            ModLog.Info($"[SCRIPT] {speakerName ?? "a speaker"} wrote lines for {string.Join(", ", who)}"
                      + $"{(forThePlayer ? " INCLUDING THE PLAYER" : "")}; taken back out of the reply.");

            bool dropEvent = forThePlayer && response.NewEventData != null;
            if (dropEvent)
               ModLog.Info("[SCRIPT] dropped this turn's [EVENT] too - it was written from words the model "
                         + $"invented for the player: \"{response.NewEventData.Summary}\"");

            // ParsedResponse is a plain class, so a copy is written out field by field, the same way
            // WithoutEventData above does it. Every field not named here travels unchanged.
            return new ParsedResponse {
               Dialogue = spoken.Text,
               Narration = string.IsNullOrWhiteSpace(narrated.Text) ? null : narrated.Text,
               NarrationBeforeDialogue = response.NarrationBeforeDialogue,
               Memory = response.Memory,
               DiscussedMemories = response.DiscussedMemories,
               NewEventData = dropEvent ? null : response.NewEventData,
               Reputation = response.Reputation,
               StanceShift = response.StanceShift,
               Actions = response.Actions,
               Discovery = response.Discovery,
               QuestGiven = response.QuestGiven,
               QuestBlockMalformed = response.QuestBlockMalformed,
               QuestCompleted = response.QuestCompleted,
               QuestAbandoned = response.QuestAbandoned,
               WitnessReactions = response.WitnessReactions
            };
         }
         catch (Exception ex)
         {
            // A reply that reaches the player unfiltered is better than a turn lost to this guard.
            ModLog.Error($"[SCRIPT] guard threw {ex.GetType().Name}: {ex.Message}; the reply stands as written.");

            return response;
         }
      }

      /// <summary>
      ///   An ensemble co-voice whose LLM turn failed simply stays quiet this beat: drop its "thinking"
      ///   placeholder and log it, rather than painting a "(My words escape me...)" line the way a 1:1 reply
      ///   would. Returns true when it handled the failure (the caller then returns); false on an ordinary turn,
      ///   which keeps its usual error line. <paramref name="suppressGroupTail" /> marks an ensemble voice.
      /// </summary>
      private bool FellSilentAsEnsembleVoice(bool suppressGroupTail, ChatMessageViewModel placeholder, ChatSpeaker speaker, string reason)
      {
         if (!suppressGroupTail) return false;

         if (placeholder != null) _messages.Remove(placeholder);
         ModLog.Info($"[SPOTLIGHT] {speaker?.Name ?? "a voice"} stayed silent this beat ({reason}).");

         return true;
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

            if (profile == null || behavior?.LlmServices is not {IsAvailable: true})
            {
               foreach (var (n, h, t) in currentReactions) RecordTranscript(false, n, h, t);

               return;
            }

            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetCalendarDate(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Realms = behavior.GameStateBridge.GetRealmStanding(),
               Rumors = ""
            };

            while (true)
            {
               bool isLast = _witnessExchangesThisRound >= WitnessExchangeCap;

               // A gesture-only reaction (wordless *smirk*, silence) must never be the thing the main NPC
               // is made to answer: that is exactly the reported "silent treatment" bug, where the model
               // escalates against a bystander's silence it invented itself. If NONE of this iteration's
               // reactions actually spoke, there is nothing to reply to: commit them (same as the no-15C
               // path would) and end the exchange quietly rather than firing a counter-reply against silence.
               List<(string name, string heroId, string text)> spokenReactions =
                  currentReactions.Where(r => WitnessSpeechDetector.HasSpokenWords(r.text)).ToList();

               if (spokenReactions.Count == 0)
               {
                  foreach (var (n, h, t) in currentReactions) RecordTranscript(false, n, h, t);

                  break;
               }

               // Session built WITHOUT currentReactions — they are not committed yet.
               // They travel as the stimulus so the LLM sees them as the final user turn.
               await EnsureTranscriptCompressedAsync();
               ChatSession session = BuildSessionFor(mainSpeaker);
               // Stimulus is built ONLY from reactions that actually spoke: a gesture-only reaction
               // mixed in with a spoken one is still committed to the transcript further down, it just
               // never becomes part of what the main NPC is prompted to answer.
               string stimulus = string.Join("\n",
                  spokenReactions.Select(r => $"[{r.name}]: {r.text}"));

               EncounterContext context = EncounterContextBuilder.Build(
                  mainSpeaker.Hero, profile, behavior.GameStateBridge,
                  null,
                  BuildContextWitnesses(mainSpeaker),
                  false,
                  true,
                  isLast,
                  captiveIntent: _captiveEncounterIntent,
                  captorIsBandit: IsCaptiveEncounter && _captiveEncounterIsBandit,
                  captiveVictimName: IsCaptiveEncounter
                     ? _captiveVictimName
                     : null,
                  playerCoercedAgainstCompanion: IsCaptiveEncounter && _captivePlayerCoerced,
                  captiveVictimIsFemale: IsCaptiveEncounter && _captiveVictimIsFemale,
                  captiveSceneFocus: IsCaptiveEncounter
                     ? _captiveSceneFocus
                     : null,
                  captiveSceneStyle: _captiveSceneStyle,
                  // Companion-briefing pillar, revised per Gabriel's ruling: this auto-exchange (the anchor
                  // replying to another present companion's reaction) is still inside the same private chat.
                  isCompanionBriefingScene: IsCompanionBriefingChat,
                  // The main character answering a witness keeps what the composed opening left them (Stage 3).
                  composedOpening: ComposedOpeningFor(mainSpeaker),
                  hideGeneratedDepth: GeneratedDepthHidden(profile),
                  companionBriefingTargetName: _companionBriefingTargetName);
               // (15C witness exchange is gated off for captive scenes, so no stage is fed here.)

               ChatMessageViewModel placeholder = MakeSpeakerMessage(mainSpeaker, PlaceholderText);
               _messages.Add(placeholder);
               ChatManager.ScrollToBottom();

               NpcChatResult result;
               using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_windowClosing.Token))
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

               ReplacePlaceholder(placeholder, NovelShown(npcReply), mainSpeaker);
               RecordTranscript(false, mainSpeaker.Name, mainSpeaker.Hero?.StringId, npcReply);

               if (!string.IsNullOrWhiteSpace(result.Response?.Narration))
                  AppendModelNarration(result.Response.Narration, npcReply);

               ModLog.Info($"[15C:{_witnessExchangesThisRound}] {mainSpeaker.Name}: \"{npcReply}\"");
               if (result.Usage != null)
                  ModLog.Info($"  [15C TOKENS] prompt={result.Usage.PromptTokens} (cached:{result.Usage.CachedPromptTokens ?? 0}) | completion={result.Usage.CompletionTokens}");

               LogVramSample("witness");

               // End-conversation action is honored even in auto-exchange.
               if (result.Response?.Actions != null)
                  foreach (GameAction action in result.Response.Actions)
                  {
                     if (!string.Equals(action.Type, "end_conversation",
                            StringComparison.OrdinalIgnoreCase)) continue;
                     if (!NpcEndLocksConversation())
                     {
                        // MCM off: the NPC's end is advisory even in a witness exchange; the player keeps control.
                        ModLog.Info($"[15C] {profile.Name} tried to end; kept open (MCM: NPCs may not end).");

                        break;
                     }

                     _conversationEnded = true;
                     IsPrivateRequestVisible = false;
                     CloseButtonText = "Leave";
                     NotifyConversationEndedChanged();
                     AppendSystemMessage($"{profile.Name} has ended the conversation.", false);
                     ModLog.Info($"[15C] {profile.Name} ended the conversation.");

                     break;
                  }

               // A companion may take their leave mid-beat too (e.g. the player
               // asked them to step out and the NPC acknowledged it this turn).
               HandleWitnessLeaves(result.Response);

               if (_conversationEnded || isLast) break;

               // The reply asked the player something: stop here, or the next round re-asks it (Lordfadooboo, 20/10/2026).
               if (WitnessExchangePolicy.WaitsForThePlayer(NovelSpoken(npcReply), _witnesses.Select(w => w.Name)))
               {
                  ModLog.Info($"[15C] {mainSpeaker.Name} asked the player a question; the bystander exchange waits for the answer.");

                  break;
               }

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
            // Control is back with the player after the witness lap: raise any confirmation queued from the
            // main reply that was deferred while the lap ran.
            if (_confirmation.HasPending && !_confirmation.IsAwaiting && !_isScenePaused && !_sceneContinuationPending)
               _confirmation.PromptNext(this);
         }
      }

      /// <summary>
      ///   Runs one round-table lap through the controller, then restores player input. The lap body (turn order,
      ///   per-speaker LLM turns, the rotation update) lives in <see cref="RoundTableController.RunAsync" />; this
      ///   thin wrapper owns the finally that re-enables the input the button/consume path locked, mirroring the
      ///   original RunRoundTableAsync's finally exactly (clears the witness-exchange guard, re-enables send unless
      ///   the conversation ended, and refreshes the witness UI).
      /// </summary>
      /// <param name="participants">Everyone present, main speaker first.</param>
      /// <param name="playerMessageToRecord">
      ///   Council per-member mode replaces the single anchor call outright rather than layering on top of it
      ///   (unlike the button/free-text "open the floor" lap, which always follows an anchor turn that already
      ///   committed the player's line), so THIS caller must record the player's current message to the
      ///   transcript itself, exactly once, before any participant's session is built. Null for the ordinary
      ///   layered lap, which already has it.
      /// </param>
      /// <param name="cap">Forwarded to <see cref="RoundTableController.RunAsync" />, null keeps the button's 3-speaker cap.</param>
      private async Task RunRoundTableLapAsync(List<ChatSpeaker> participants, string playerMessageToRecord = null, int? cap = null)
      {
         try
         {
            if (!string.IsNullOrEmpty(playerMessageToRecord))
               RecordTranscript(true, "You", Hero.MainHero?.StringId, playerMessageToRecord);

            await _roundTable.RunAsync(this, participants, cap);
         }
         finally
         {
            _npcWitnessExchangePending = false;
            IsSendEnabled = !_conversationEnded;
            RefreshWitnessUi();
            // Round-table audit D10: the 15C lap raises a deferred confirmation here and this one did not, so
            // a take_gold emitted by the triggering turn was not put to the player until AFTER the next full
            // exchange, by which time the model had already carried on as though it had been paid.
            if (_confirmation.HasPending && !_confirmation.IsAwaiting && !_isScenePaused && !_sceneContinuationPending)
               _confirmation.PromptNext(this);
         }
      }

      #region IRoundTableHost

      /// <summary>True once the conversation has ended (NPC farewell / end_conversation) so the lap stops.</summary>
      bool IRoundTableHost.IsConversationEnded => _conversationEnded;

      /// <summary>True while this view model is still the active chat (guards a stale async continuation).</summary>
      bool IRoundTableHost.IsStillActiveView => ReferenceEquals(ChatManager.ActiveViewModel, this);

      bool IRoundTableHost.IsStillPresent(ChatSpeaker speaker)
      {
         if (speaker == null) return false;
         if (speaker.IsMain) return true; // the host of the conversation never "leaves" their own room

         // m4: a member killed or captured mid-council is no longer at the table, even though the roster snapshot
         // still lists them; drop them from the lap now instead of letting them speak on until the lift re-checks.
         Hero hero = speaker.Hero;
         if (hero != null && (!hero.IsAlive || hero.IsPrisoner)) return false;

         string id = hero?.StringId;

         return !string.IsNullOrEmpty(id)
            ? _witnesses.Any(w => w.HeroStringId == id)
            : _witnesses.Any(w => string.Equals(w.Name, speaker.Name, StringComparison.OrdinalIgnoreCase));
      }

      ChatSession IRoundTableHost.BuildSession(ChatSpeaker speaker) => BuildSessionFor(speaker);

      Task IRoundTableHost.EnsureHistoryCompressedAsync() => EnsureTranscriptCompressedAsync();

      IReadOnlyList<WitnessEntry> IRoundTableHost.BuildContextWitnesses(ChatSpeaker speaker) => BuildContextWitnesses(speaker);

      ChatMessageViewModel IRoundTableHost.AddPlaceholder(ChatSpeaker speaker)
      {
         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         ChatManager.ScrollToBottom();

         return placeholder;
      }

      void IRoundTableHost.RemovePlaceholder(ChatMessageViewModel placeholder) => RemovePlaceholder(placeholder);

      string IRoundTableHost.RenderTurn(ChatMessageViewModel placeholder, ChatSpeaker speaker, ParsedResponse response)
         => RenderNpcTurn(placeholder, speaker, response);

      void IRoundTableHost.RecordTranscriptLine(bool isPlayer, string name, string heroId, string text)
         => RecordTranscript(isPlayer, name, heroId, text);

      void IRoundTableHost.AppendNarration(string narration, ChatMessageViewModel insertBefore)
         => AppendNarration(narration, insertBefore);

      void IRoundTableHost.HandleWitnessLeaves(ParsedResponse response) => HandleWitnessLeaves(response);

      void IRoundTableHost.ProcessCouncilResolutions(ParsedResponse response) => ProcessCouncilResolutions(response);

      /// <summary>Locks the conversation exactly as an NPC's end_conversation does during a round-table lap.</summary>
      void IRoundTableHost.MarkConversationEndedBySpeaker(string speakerName)
      {
         _conversationEnded = true;
         IsPrivateRequestVisible = false;
         CloseButtonText = "Leave";
         NotifyConversationEndedChanged();
         AppendSystemMessage($"{speakerName} has ended the conversation.", false);
      }

      void IRoundTableHost.LogVram(string tag) => LogVramSample(tag);

      #endregion

      #region IActionConfirmationHost

      /// <summary>True once the conversation has ended so queued spends are dropped rather than applied unconfirmed.</summary>
      bool IActionConfirmationHost.IsConversationEnded => _conversationEnded;

      void IActionConfirmationHost.AppendOoc(string text) => AppendOocMessage(text);

      void IActionConfirmationHost.AppendSystemMessageLine(string text, bool positive) => AppendSystemMessage(text, positive);

      void IActionConfirmationHost.RecordTranscriptLine(bool isPlayer, string name, string heroId, string text)
         => RecordTranscript(isPlayer, name, heroId, text);

      /// <summary>The recompose path: restore the typed text as the player's input and send it as a fresh LLM turn.</summary>
      void IActionConfirmationHost.SendPlayerMessage(string raw)
      {
         InputText = raw;
         ExecuteSendMessage();
      }

      #endregion

      #region IOfferHost

      /// <summary>The current conversation partner (the recipient of any offer).</summary>
      Hero IOfferHost.Npc => _npcHero;

      /// <summary>The speaker's own profile, as MainSpeaker resolves it (never the DialogFlow cache first).</summary>
      NpcProfile IOfferHost.Profile => MainSpeaker()?.Profile;

      /// <summary>True while an offer may be made: a plain live conversation that has not yet ended.</summary>
      bool IOfferHost.CanOffer => _mode == ChatMode.Conversation && !_conversationEnded;

      void IOfferHost.SetInputText(string text) => InputText = text;

      void IOfferHost.AppendSystemMessageLine(string text, bool positive) => AppendSystemMessage(text, positive);

      void IOfferHost.NotifyOfferItemVisibilityChanged() => OnPropertyChanged(nameof(IsOfferItemVisible));

      /// <summary>Starts one NPC acceptance turn for a just-closed item bargain (the NPC accepts the goods in their own voice).</summary>
      void IOfferHost.BeginBargainAcceptanceTurn(string stimulus)
      {
         ChatSpeaker speaker = MainSpeaker();
         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;
         _ = RunLlmExchangeAsync(stimulus, speaker, placeholder, true);
      }

      /// <summary>
      ///   Fires ONE voice-only NPC reaction turn from an outcome hint (an external <c>ActionResult.Recap</c>, or a
      ///   <see cref="CrConversation.PromptReply" /> from an out-of-band decision): no player line is shown, and the
      ///   NPC speaks the outcome its previous reply could not yet know. Returns false when there is no live
      ///   conversation to speak into. The raw fact is wrapped here as an out-of-character "voice this now"
      ///   directive; the model speaks it in the NPC's own words. Does NOT itself check whether a turn is already in
      ///   flight (the auto-recap path calls it precisely while input is still locked); the manual entry
      ///   <see cref="TryPromptReply" /> guards that.
      /// </summary>
      public bool BeginOutcomeReactionTurn(string hint)
      {
         if (string.IsNullOrWhiteSpace(hint) || _mode != ChatMode.Conversation || _conversationEnded) return false;

         ChatSpeaker speaker = MainSpeaker();
         if (speaker?.Hero == null) return false;

         ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
         _messages.Add(placeholder);
         IsSendEnabled = false;
         _ = RunLlmExchangeAsync(OutcomeReactionStimulus(hint), speaker, placeholder, suppressPlayerLine: true, isOutcomeReaction: true);

         return true;
      }

      /// <summary>
      ///   Public entry for <see cref="CrConversation.PromptReply" />: make the NPC voice a reaction to an outcome
      ///   that resolved outside a player message, but only when the conversation is idle (no turn in flight). Call
      ///   on the main thread. Returns false when a turn is already running, the conversation ended, or none is open.
      /// </summary>
      public bool TryPromptReply(string hint)
         => IsSendEnabled && BeginOutcomeReactionTurn(hint);

      /// <summary>Wraps a plain outcome fact as the synthetic, out-of-character user stimulus that drives a voice-only recap turn.</summary>
      private static string OutcomeReactionStimulus(string hint)
         => "[OUT OF CHARACTER, not spoken by anyone: the matter is now settled. " + hint.Trim() +
            " Voice this outcome to your conversation partner now, in your own words and fully in character, as the "
          + "person who has just learned or decided it. Do not wait for them to speak, and do not quote this note.]";

      #endregion

      #region IManualActionMenuHost

      /// <summary>The current conversation partner (the target of any manually-injected action).</summary>
      Hero IManualActionMenuHost.Npc => _npcHero;

      /// <summary>This NPC's stored profile, when one exists yet.</summary>
      NpcProfile IManualActionMenuHost.Profile => MainSpeaker()?.Profile; // the speaker's own, not the DialogFlow cache (Lordfadooboo, 02/10/2026)

      /// <summary>True while the menu may be opened: a plain live conversation that has not yet ended (mirrors IsActionMenuVisible).</summary>
      bool IManualActionMenuHost.CanOpenActionMenu => _mode == ChatMode.Conversation && !_conversationEnded;

      #endregion

      // ── Running-transcript compression (Gabriel 2026-07-30) ────────────────

      private const int ConversationCompressTimeoutSeconds = 60;

      /// <summary>
      ///   Before a turn is built, fold the older part of a long conversation into a compressed running recap
      ///   (Gabriel 2026-07-30), so every participant stays coherent with the whole exchange without replaying an
      ///   ever-growing raw transcript. A no-op below the policy threshold; never throws (a failed fold just keeps
      ///   the raw history). LLM off-thread, then the state mutation back on the main thread.
      /// </summary>
      private async Task EnsureTranscriptCompressedAsync(Func<int, int, ConversationFoldPlan> planFor = null)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.LlmServices is not {IsAvailable: true}) return;

            // Compact folds sooner. The raw transcript was the one growing part of a Compact prompt that
            // nothing had ever shrunk, which is why a small model's FIRST message could fit and its followups
            // could not (DuskSymphony, Nexus). A council brings its own pacing (CouncilSittingPolicy.Plan).
            ConversationFoldPlan plan = planFor != null
               ? planFor(_transcript.Count, _transcript.SummarizedThrough)
               : ConversationCompressionPolicy.Plan(new ConversationCompressionPolicy.TranscriptFold {
                  TotalLines = _transcript.Count,
                  SummarizedThrough = _transcript.SummarizedThrough,
                  Threshold = ConversationCompressionPolicy.ThresholdFor(ModSettings.LeanPrompt)
               });
            if (!plan.ShouldFold) return;

            string newLines = _transcript.RenderRange(plan.FoldFrom, plan.FoldTo, PlayerTranscriptLabel());
            if (string.IsNullOrWhiteSpace(newLines)) return;

            string existing = _transcript.RunningSummary;
            var compressor = new ConversationHistoryCompressor(behavior.LlmServices.HousekeepingClient, ModSettings.ReplyLanguage);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ConversationCompressTimeoutSeconds));

            string updated = null;
            try
            {
               updated = await Task.Run(() => compressor.CompressAsync(existing, newLines, cts.Token)).ConfigureAwait(false);
            }
            catch (Exception netEx)
            {
               ModLog.Error($"[CONV COMPRESS] LLM call failed: {netEx.Message}");
            }

            await MainThreadDispatcher.SwitchToMainThread();

            if (!string.IsNullOrWhiteSpace(updated))
            {
               _transcript.ApplySummary(updated, plan.FoldTo);
               ModLog.Info($"[CONV COMPRESS] folded lines [{plan.FoldFrom},{plan.FoldTo}) into the running recap.");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"EnsureTranscriptCompressedAsync failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The label the player's lines carry in the recap: "Prisoner" in a captive scene, else their name.</summary>
      private string PlayerTranscriptLabel()
         => IsCaptiveEncounter ? "Prisoner" : Hero.MainHero?.Name?.ToString() ?? "You";

      #endregion
   }
}