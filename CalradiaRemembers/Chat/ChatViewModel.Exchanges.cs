// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Stance;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      #region private

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
                        $"(mode {LlmServices.ActiveModelMode})...");

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
               aggressorKind: _director?.CurrentAggressor ?? CaptiveAggressorKind.Lead,
               captiveVictimName: _captiveVictimName,
               playerCoercedAgainstCompanion: _captivePlayerCoerced,
               captiveVictimIsFemale: _captiveVictimIsFemale);

            ChatMessageViewModel placeholder = MakeSpeakerMessage(speaker, PlaceholderText);
            _messages.Add(placeholder);
            ChatManager.ScrollToBottom();

            NpcChatResult result;
            using (var cts = new CancellationTokenSource())
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
               StoryLog.WriteNpcLine(speaker.Name, npcLine);
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

                  if (action.Parameters.TryGetValue("amount", out string ats) && int.TryParse(ats, out int at))
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
            if (profile == null || behavior?.LlmServices is not {IsAvailable: true})
            {
               ApplyEscapeOutcomeFallback(speaker, freed, profile);

               return;
            }

            string stimulus = freed
               ? "[The prisoner has just torn free of your grip and bolted into the dark — and they are GONE, " + "beyond any chance of recapture tonight. You FAILED to hold them. React in character to losing " + "them this very instant: fury, a shout, a curse, a too-late order to give chase. A few lines " + "only. Then it is over — emit end_conversation.]"
               : "[The prisoner lunged to break free and FAILED — you caught them before they got anywhere. " + "Reassert your control, make them feel the cost of the attempt, and carry the scene on.]";

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
               sceneStage: CaptiveSceneStage.Conclude, // the escape beat is the scene's end
               captiveVictimName: _captiveVictimName,
               playerCoercedAgainstCompanion: _captivePlayerCoerced,
               captiveVictimIsFemale: _captiveVictimIsFemale);

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
            bool usable = !string.IsNullOrWhiteSpace(npcLine) && !DialogueHeuristics.LooksLikeRefusal(npcLine) && !DialogueHeuristics.LooksLikeRefusal(result.Response?.Narration);

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
               RestoreInputAfterFailedTurn(playerMessage, suppressPlayerLine);

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
               isCaptorScene: _captorScene,
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
               companionAudience: _audienceReason,
               audienceRetirementIsLanded: _audienceLanded,
               // The scene has not been established yet, so this turn sets it (Gabriel 2026-07-19: an
               // opening narration, then sporadic). Consumed ONCE per conversation rather than derived from
               // the turn counter: a captor's opening turn carries suppressPlayerLine and so never
               // increments it, which asked for the opening narration twice in every captive scene.
               isConversationOpening: ConsumeOpeningNarration(),
               // Assembly spike: when armed by cr.council_test, a normal player turn takes the COUNCIL framing,
               // so this single call yields the anchor's reply + a [WITNESS_REACTION] per present member.
               // A real council carries the whole-table framing for its WHOLE session (_councilScope); the
               // console spike arms the same framing globally on an ordinary chat, for validation only.
               isRoundTableTurn: RoundTableController.CouncilTurnsForced || _councilScope != null,
               // Player agency: draw THIS turn's verdict for any physical attempt the player makes, so the model
               // narrates an outcome it did not choose. Only on a real player turn in a captive scene; a synthetic
               // scene cue has no attempt to judge.
               playerAttemptSucceeds: RollCaptiveAttemptVerdict(suppressPlayerLine));

            if (behavior.LlmServices is not {IsAvailable: true})
            {
               ReplacePlaceholder(placeholder,
                  behavior.LlmServices?.DescribeUnavailability() ?? "(The AI services are not available — check CalradiaRemembers.log.)", speaker);
               ModLog.Error("LLM pipeline: LLM services unavailable (no API key or model?).");
               RestoreInputAfterFailedTurn(playerMessage, suppressPlayerLine);

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
               RestoreInputAfterFailedTurn(playerMessage, suppressPlayerLine);

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

            // Scene narration emitted BEFORE the dialogue keeps its emitted order at render:
            // it lands ahead of the speaker's bubble, and its transcript/story entries are
            // written before the spoken line's below (AppendNarration records all three).
            bool narrationFirst = response.NarrationBeforeDialogue && !string.IsNullOrWhiteSpace(response.Narration);
            if (narrationFirst)
               AppendNarration(response.Narration, placeholder);

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

            // Fallback-memory bookkeeping: a completed exchange counts as a turn, and the player's text
            // length feeds the one-substantial-turn rule (a single meaningful exchange deserves a memory;
            // player-reported: an NPC forgot a one-turn conversation that closed their whole Lucon affair).
            // A substantive [EVENT] means no fallback summary is needed at close.
            if (!suppressPlayerLine)
            {
               _playerTurnsThisSession++;
               _playerCharsThisSession += playerMessage?.Trim().Length ?? 0;
               MaybeLearnPlayerNameFromCaptive(playerMessage);
            }

            if (response.NewEventData != null && !string.IsNullOrWhiteSpace(response.NewEventData.Summary))
               _eventRecordedThisSession = true;

            // Story log: intro (lazy, once per conversation) + player line + NPC line.
            if (IsLiveConversation)
            {
               EnsureStoryIntro();
               if (!suppressPlayerLine) StoryLog.WritePlayerLine(playerMessage);
               if (!string.IsNullOrEmpty(npcLine)) StoryLog.WriteNpcLine(speaker.Name, npcLine);
            }

            // Scene narration ([NARRATION]) — neutral-narrator physical action,
            // shown after the speaker's dialogue unless the model led with it
            // (handled above). Recorded to transcript + story log.
            if (!narrationFirst && !string.IsNullOrWhiteSpace(response.Narration))
               AppendNarration(response.Narration);

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
               VentriloquizedReactionPolicy.DropsReactions(_roundTable.IsRequestPending, speaker.IsMain,
                  IsCaptiveEncounter, _conversationEnded)
                  ? new List<(string name, string heroId, string text)>()
                  : CollectWitnessReactions(response);

            foreach (var (wn, _, wt) in witnessReactions)
            {
               AppendWitnessReaction(wn, wt, false);
               ModLog.Info($"  [WITNESS] {wn}: \"{wt}\"");
            }

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
            ParsedEventData withheldMarriageClaim = null;
            ParsedResponse responseForMutator = response;
            if (response.NewEventData != null &&
                MarriageClaimPolicy.ClaimsMarriageWithPlayer(
                   response.NewEventData.Summary, profile.Name, Hero.MainHero?.Name?.ToString()))
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

            ProfileMutator.Apply(profile, responseForMutator, gameDay,
               romanticContentEnabled: ModSettings.AdultContentLevel != AdultContentLevel.Off,
               orientationCompatible: orientationCompatible,
               applyReputation: false, // C2: [REPUTATION] is disabled in the mod; never apply a block the model emitted unprompted.
               intimacyPermitted: behavior.GameStateBridge.PermitsIntimacyWith(_npcHero, profile),
               playerName: playerName);

            // Stance: the LLM's spoken nudges to trust/respect/fear — capped hard and rate-limited
            // (words only colour; deeds prove). Persisted by the Store.Set below after actions run.
            StanceService.ApplyWordShift(profile, response.StanceShift);

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
               const int romanticPatternWindowDays = 84;
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
                  // fondly. Not when the dynamic is the exploiter pressing an advantage, which is leverage,
                  // not a kindness done to them. Mirrors AppreciationBackfillRules' Intimacy-event rule.
                  else if (ModSettings.AdultContentLevel != AdultContentLevel.Off)
                     AppreciationBehavior.Instance?.TryPlant(partner, Hero.MainHero,
                        AppreciationSource.KindnessInScene, "A tender moment we shared.",
                        AppreciationBackfillRules.KindnessInSceneMagnitude, true, gameDay);
               }
            }

            // ── Execute game actions emitted by the LLM ───────────────────
            // RelationGate may stamp profile.LastRelationGainHour, so persist
            // AFTER actions run (below), not before.
            var prisonerExecuted = false;
            var nemesisTurned = false;
            if (response.Actions is {Count: > 0})
            {
               // Double-payment guard: when the LLM emits join_party it sometimes ALSO
               // emits take_gold for the same payment (the player mimed handing coins
               // over) — join_party already transfers the settled price, so honoring
               // both charges the player twice (observed in testing: 550 + 546).
               bool hasJoinParty = response.Actions.Any(a =>
                  string.Equals(a.Type, "join_party", StringComparison.OrdinalIgnoreCase));

               // M-R3: at content Off an intimacy [EVENT] is a hallucination (the romance system is disabled), so
               // it must not grant the RelationGate's intimacy-tier boost (+3, cooldown bypassed). Downgrade it so
               // change_relation is gated as a routine gain, matching the status arc being frozen off above.
               NotableEventType? emittedForGate = response.NewEventData?.Type;
               if (emittedForGate == NotableEventType.Intimacy && ModSettings.AdultContentLevel == AdultContentLevel.Off)
                  emittedForGate = null;

               var actionContext = new ActionContext(profile, emittedForGate) {
                  OfferedItemId = _offer.OfferedItemId
               };
               // B2 (quest audit): at most one change_relation applies per turn, so a weak model that emits
               // several negative blocks cannot pile the RelationGate's per-call -15 floor into -75.
               var relationChangedThisTurn = false;
               var worstAppliedRelation = 0; // 2.1: the steepest regard change actually applied this turn, for the affront rumor.
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

                  // Council: deeds are NOT sealed at the table (ratified 2026-07-24). Only PERCEPTION runs on
                  // the spot here (change_relation): a lord you anger IS angrier at once, and that shift must
                  // colour how he weighs every request for the rest of the sitting. World transfers and
                  // commitments (gold, troops, prisoners, marriage, schemes, missions) are promises, recorded
                  // through the [RESOLUTION] channel and carried out only when the council rises. Dropping them
                  // here is what makes the prompt's "nothing is sealed at this table" TRUE in code (the bridge
                  // is law), closing the guard gap the audit found (whole-table dispatch had none).
                  if (_councilScope != null && !CouncilResolutionPolicy.ExecutesImmediatelyAtCouncil(action.Type))
                  {
                     ModLog.Info($"[COUNCIL] '{action.Type}' is not sealed at the table; deferred (only perception applies live; deeds belong to [RESOLUTION]).");

                     continue;
                  }

                  if (hasJoinParty &&
                      string.Equals(action.Type, "take_gold",
                         StringComparison.OrdinalIgnoreCase))
                  {
                     ModLog.Info("[ACTION] take_gold dropped — join_party in the same response already transfers the hire payment.");

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
                     : new ActionContext(actorProfile, emittedForGate) {OfferedItemId = _offer.OfferedItemId};

                  // Coin/commit actions are preprocessed: a repeat of one already resolved this conversation is
                  // dropped (the LLM re-emitting the same transfer), and a player-outgoing spend (take_gold /
                  // join_party) is queued for in-chat confirmation. give_gold and everything else run inline.
                  ActionDisposition disposition =
                     _confirmation.Preprocess(action, actorHero, actorProfile, actorActionContext);

                  if (disposition != ActionDisposition.ExecuteInline) continue;

                  // B2: the first regard change this turn wins; extras are dropped (see relationChangedThisTurn).
                  if (string.Equals(action.Type, "change_relation", StringComparison.OrdinalIgnoreCase))
                  {
                     // M-R3 (regard audit): a quest completed this turn already pays its fixed, verified
                     // RewardRelation. A POSITIVE change_relation the model also emits for "completing the task"
                     // would double-count the same deed, so drop it on a quest-completion turn. Negatives still
                     // apply: an offense given during a turn-in is real.
                     if (response.QuestCompleted != null
                         && action.Parameters != null
                         && action.Parameters.TryGetValue("delta", out string qds)
                         && int.TryParse(qds, out int qdv) && qdv > 0)
                     {
                        ModLog.Info("[ACTION] change_relation (positive) dropped: the quest reward this turn already covers the goodwill (M-R3).");

                        continue;
                     }

                     if (relationChangedThisTurn)
                     {
                        ModLog.Info("[ACTION] change_relation dropped: at most one regard change applies per turn.");

                        continue;
                     }

                     relationChangedThisTurn = true;
                  }

                  // B3 (quest audit): an executor that throws must not abort the whole turn (skipping the
                  // remaining actions AND the Store.Set that persists this turn's mutations). Isolate each call.
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

                  if (outcome != null) AppendSystemMessage(outcome.Message, outcome.IsPositive);

                  // The bridge REFUSED the action (an active war, a captivity, a gate the prompt let
                  // through): the refusal must reach the TRANSCRIPT, not only the UI line — otherwise the
                  // model never learns the marriage/transfer it just celebrated never happened, believes
                  // it did, and the next turns build on the lie. Same OOC pattern as a declined
                  // confirmation (ActionConfirmationController.DeclineAction).
                  string refusalNote = ActionRefusalNote.For(outcome?.WasRefused == true, outcome?.Message);
                  if (refusalNote != null)
                  {
                     try { RecordTranscript(true, "You", Hero.MainHero?.StringId, refusalNote); }
                     catch (Exception ex) { ModLog.Error($"[ACTION] refusal note failed: {ex.GetType().Name}: {ex.Message}"); }
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

                  // Story log: narrative description of game-world consequences. Attributed to the ACTOR
                  // (which is the anchor's own profile when no "actor" was named, see actorProfile above).
                  if (string.Equals(action.Type, "change_relation", StringComparison.OrdinalIgnoreCase))
                  {
                     // 2.1: log the regard ACTUALLY applied (from the outcome), never the raw proposal. A change
                     // gated to 0 returns a null outcome, so it leaves no "seems to appreciate your words" line.
                     if (outcome?.AppliedRelationDelta is int applied && applied != 0)
                     {
                        StoryLog.WriteRelationChange(actorProfile.Name, applied);
                        if (applied < worstAppliedRelation) worstAppliedRelation = applied;
                     }
                  }
                  else if (string.Equals(action.Type, "give_gold", StringComparison.OrdinalIgnoreCase))
                  {
                     if (action.Parameters.TryGetValue("amount", out string ags) && int.TryParse(ags, out int ag))
                        StoryLog.WriteGoldTransfer(actorProfile.Name, ag, true);
                  }
                  else if (string.Equals(action.Type, "take_gold", StringComparison.OrdinalIgnoreCase))
                  {
                     if (action.Parameters.TryGetValue("amount", out string ats) && int.TryParse(ats, out int at))
                        StoryLog.WriteGoldTransfer(actorProfile.Name, at, false);
                  }
               }

               // The personal score may have moved — refresh the header (main NPC only;
               // the header reflects the conversation's host, not a routed witness).
               if (speaker.IsMain)
                  SetRelationSummary(profile.ReputationWithPlayer, profile.ClanRelationWithPlayer ?? 0);

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
                  bool privacyAccepted = NpcIsPlayerPrisoner() || string.Equals(privacyResult, "accepted", StringComparison.OrdinalIgnoreCase);
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

            if (!_conversationEnded && response.NewEventData?.Type == NotableEventType.Farewell)
            {
               _conversationEnded = true;
               IsSendEnabled = false;
               IsPrivateRequestVisible = false;
               CloseButtonText = "Leave";
               OnPropertyChanged(nameof(IsOfferItemVisible));
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
               OnPropertyChanged(nameof(IsOfferItemVisible));
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

            // ── Council resolutions: record or withdraw what the table decided this turn ──
            // A no-op outside a council (ProcessCouncilResolutions itself gates on _councilScope), settled
            // only at the lift (ChatManager.Close -> FinalizeCouncilResolutions), never here.
            ProcessCouncilResolutions(response);

            // Mark any delivered player letters read now that the NPC has responded.
            PlayerLetterService.MarkLettersRead(profile, behavior.Store);

            // ── Round table: the player opened the floor to the group this turn ──
            // Takes precedence over the model-driven 15C auto-exchange: instead of hoping the model
            // volunteered [WITNESS_REACTION]s, give each present witness a deterministic turn (they may
            // address one another), capped, then return control to the player. Model-independent.
            if (_roundTable.TryConsumeForMainReply(speaker.IsMain, IsCaptiveEncounter, _conversationEnded))
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
            if (witnessReactions.Count > 0 && speaker.IsMain && !IsCaptiveEncounter && _witnessExchangesThisRound < WitnessExchangeCap && !_conversationEnded)
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
            ReplacePlaceholder(placeholder, "(My voice falters...)", speaker);
            ModLog.Error($"RunLlmExchangeAsync threw {ex.GetType().Name}: {ex.Message}");
            ModLog.Error($"Stack: {ex.StackTrace}");
         }
         finally
         {
            // A bag offer is consumed by the turn it was sent with — clear it so a later, unrelated
            // give_item never re-transfers the previously picked item.
            _offer.ClearOfferedItem();

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
            if (!_npcWitnessExchangePending && !_sceneContinuationPending && !_isScenePaused)
            {
               IsSendEnabled = !_conversationEnded;
               // Reply delivered and control is back with the player: raise any queued action confirmation now
               // (PromptNext drops them instead if the conversation just ended).
               if (_confirmation.HasPending && !_confirmation.IsAwaiting) _confirmation.PromptNext(this);
            }
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

            if (profile == null || behavior?.LlmServices is not {IsAvailable: true})
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
                  captorIsBandit: IsCaptiveEncounter && _captiveEncounterIsBandit,
                  captiveVictimName: IsCaptiveEncounter
                     ? _captiveVictimName
                     : null,
                  playerCoercedAgainstCompanion: IsCaptiveEncounter && _captivePlayerCoerced,
                  captiveVictimIsFemale: IsCaptiveEncounter && _captiveVictimIsFemale,
                  captiveSceneFocus: IsCaptiveEncounter
                     ? _captiveSceneFocus
                     : null);
               // (15C witness exchange is gated off for captive scenes, so no stage is fed here.)

               ChatMessageViewModel placeholder = MakeSpeakerMessage(mainSpeaker, PlaceholderText);
               _messages.Add(placeholder);
               ChatManager.ScrollToBottom();

               NpcChatResult result;
               using (var cts = new CancellationTokenSource())
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
                  ModLog.Info($"  [15C TOKENS] prompt={result.Usage.PromptTokens} (cached:{result.Usage.CachedPromptTokens ?? 0}) | completion={result.Usage.CompletionTokens}");

               LogVramSample("witness");

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

         string id = speaker.Hero?.StringId;

         return !string.IsNullOrEmpty(id)
            ? _witnesses.Any(w => w.HeroStringId == id)
            : _witnesses.Any(w => string.Equals(w.Name, speaker.Name, StringComparison.OrdinalIgnoreCase));
      }

      ChatSession IRoundTableHost.BuildSession(ChatSpeaker speaker) => BuildSessionFor(speaker);

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
         OnPropertyChanged(nameof(IsOfferItemVisible));
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

      #endregion

      #endregion
   }
}