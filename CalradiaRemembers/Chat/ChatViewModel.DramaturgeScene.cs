// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 7 of the group-scene plan, the dramaturge in the Dramaturge mode's chat (docs/design/group-chat/
// integration-plan-2026-11-28.md). Gabriel, 28/11/2026: "le mode dramaturge avec DRAMA+PROSE+INTERPRET+NSFW", and for the
// dramaturge model "je pourrais vouloir DeepSeek, et Mimo en PROSE". NOT A/B-validated: it runs only on the turns stage 6 runs
// on (DramaturgeModePolicy), so Standard and Enhanced do not change.
//
// The engine half:
// - the events: the stage 3 composer, in the Dramaturge mode, proposes up to three from the facts it reads and has them checked
//   (ComposedOpeningComposer); those it keeps wait here, per chat (DramaturgeEvents). With the composed opening off, none.
// - after each Dramaturge beat from the second on, one call on the DRAMATURGE client (LlmServices.DramaturgeClient, the prose
//   model unless another is set) reads the scene, the inner life and the events waiting, in the background, after the inner
//   life of the last reply has settled. Its reply is acted on (DramaturgeScenePolicy) on the main thread, and only if the
//   chat is still this one, still open, and the player has not spoken since it was asked: a stale reading is dropped.
// - an event is shown as the narrator's purple line (AppendModelNarration; none is brought with Scene Narration off, so the
//   player never sees a character react to something hidden), joins the keeper's record, and the next reply is told it as a
//   fact (EncounterContext.SceneDirectionNote, Tail section scene_direction).
// - "end": the next reply is told the scene has run its course and may close it in character. Nothing is locked; whether the
//   character may end the conversation stays NpcCanEndConversation's. The dramaturge reads no more in this chat.
// Every step writes a "dramaturge" line to the conversation log (stage 1).

#region

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Diagnostics;
using NpcMemoryService.Core.LlmClient;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private readonly DramaturgeEvents _dramaEvents = new();
      private int _dramaBeats;
      private int _dramaLastEventBeat = -1;
      private bool _dramaSceneEnded;
      private string _dramaDirection;
      private int _dramaPlayerLines;
      private Task _dramaReading = Task.CompletedTask;

      /// <summary>cr.test dramaturge_scene: the stand-in for the DRAMATURGE client. Null in play.</summary>
      internal static ILlmClient DramaturgeDirectorForDiagnostics;

      /// <summary>cr.test dramaturge_scene: the events the composer left in this chat.</summary>
      internal DramaturgeEvents DramaturgeEventsForDiagnostics => _dramaEvents;

      /// <summary>cr.test dramaturge_scene: the note the next reply would be given, or null.</summary>
      internal string SceneDirectionForDiagnostics => _dramaDirection;

      /// <summary>cr.test dramaturge_scene: the dramaturge said the scene has run its course.</summary>
      internal bool DramaturgeSceneEndedForDiagnostics => _dramaSceneEnded;

      private static ILlmClient DramaturgeDirectorClient => DramaturgeDirectorForDiagnostics ?? NpcMemoryCampaignBehavior.Instance?.LlmServices?.DramaturgeClient;

      /// <summary>The composed opening was shown: its checked events wait for the dramaturge (none outside the Dramaturge mode).</summary>
      private void KeepComposedEvents(IReadOnlyList<string> events)
      {
         _dramaEvents.Reset(events);
         if (events != null && events.Count > 0) NoteWhy(ChatLogWhyKind.Dramaturge, $"Dramaturge: {events.Count} event(s) drawn from the facts wait: {string.Join(" / ", events)}.");
      }

      /// <summary>
      ///   A Dramaturge turn begins (main thread): the player has spoken, so a reading still out is stale, and the note the
      ///   dramaturge left is handed to this one reply and cleared.
      /// </summary>
      private string TakeSceneDirection()
      {
         _dramaPlayerLines++;
         string note = _dramaDirection;
         _dramaDirection = null;

         return note;
      }

      /// <summary>A Dramaturge beat ended with a reply (main thread): count it, and read the scene when it is time.</summary>
      private void AfterDramaturgeBeat(ChatSpeaker speaker)
      {
         _dramaBeats++;
         bool read = DramaturgeScenePolicy.ShouldRead(new DramaturgeReadFacts {
            DramaturgeTurn = true, BeatsPlayed = _dramaBeats, SceneEnded = _dramaSceneEnded, ReadInFlight = !_dramaReading.IsCompleted
         });
         if (read) StartDramaturgeRead(speaker);
      }

      /// <summary>
      ///   Starts the dramaturge's reading (main thread in: the facts, the scene and the events are read here). Waits a few seconds
      ///   at most for the inner life of the last reply, calls the DRAMATURGE client off the main thread, and acts on the reply back
      ///   on it. Never throws.
      /// </summary>
      private void StartDramaturgeRead(ChatSpeaker speaker)
      {
         try
         {
            DramaturgeSheet sheet = DramaturgeSheetFor(speaker);
            string scene = DramaturgeSceneText(null);
            string record = _dramaRecord.Text();
            IReadOnlyList<KeyValuePair<int, string>> waiting = _dramaEvents.Waiting();
            int beats = _dramaBeats;
            int askedAt = _dramaPlayerLines;
            ILlmClient client = DramaturgeDirectorClient;
            Task mind = _dramaMind;
            CancellationToken token = _windowClosing.Token;
            TimeSpan limit = TimeSpan.FromSeconds(ComposedOpeningPolicy.ComposerTimeoutSeconds(ModSettings.LlmResponseTimeoutSeconds));
            _dramaReading = Task.Run(async () =>
            {
               // The reading goes on the newest inner life: the update from the reply just shown, if it comes in time.
               if (!mind.IsCompleted) await Task.WhenAny(mind, Task.Delay(TimeSpan.FromSeconds(DramaturgeModePolicy.InnerLifeWaitSeconds), token)).ConfigureAwait(false);
               DramaturgeReading reading = await DramaturgeTurnComposer.ReadSceneAsync(client, sheet, _dramaState, scene, record, waiting, beats, limit, token).ConfigureAwait(false);
               await MainThreadDispatcher.SwitchToMainThread();
               ApplyDramaturgeReading(reading, beats, askedAt);
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DRAMATURGE] the scene could not be read: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Acts on a reading (main thread): an event shown and told to the next reply, or the scene's end. Never throws.</summary>
      private void ApplyDramaturgeReading(DramaturgeReading reading, int beats, int askedAt)
      {
         try
         {
            if (!ReferenceEquals(ChatManager.ActiveViewModel, this) || _conversationEnded) return;
            if (reading == null)
            {
               NoteDramaturge($"after beat {beats}: the dramaturge failed or timed out; the scene goes on as it is");

               return;
            }

            if (askedAt != _dramaPlayerLines || !IsSendEnabled)
            {
               NoteDramaturge($"after beat {beats}: {DramaturgeDirector.Describe(reading)}; set aside, the scene moved on before it came");

               return;
            }

            DramaturgeDecision decision = DramaturgeScenePolicy.Decide(reading, new DramaturgeDecisionFacts {
               BeatsPlayed = beats, LastEventBeat = _dramaLastEventBeat, EventCount = _dramaEvents.Count, UsedEvents = _dramaEvents.Used,
               SceneNarrationOn = ModSettings.SceneNarration
            });

            if (decision.End)
            {
               _dramaSceneEnded = true;
               _dramaDirection = DramaturgeDirector.EndNote;
            }
            else if (decision.EventNumber > 0)
            {
               string text = _dramaEvents.Take(decision.EventNumber);
               if (!string.IsNullOrWhiteSpace(text))
               {
                  _dramaLastEventBeat = beats;
                  // The event itself is the spoken line for the policy: Scene Narration governs it like any narration.
                  AppendModelNarration(text, text);
                  _dramaRecord.Establish(new KeeperVerdict {Facts = new List<string> {text}});
                  _dramaDirection = DramaturgeDirector.EventNote(text);
               }
            }

            NoteDramaturge($"after beat {beats}: {DramaturgeDirector.Describe(reading)}; {decision.Why}");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DRAMATURGE] the reading could not be applied: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void NoteDramaturge(string text)
      {
         ModLog.Info($"[DRAMATURGE] scene: {text}");
         NoteWhy(ChatLogWhyKind.Dramaturge, $"Dramaturge {text}.");
      }

      /// <summary>
      ///   cr.test dramaturge_scene: reads the scene now as the chat does after a beat, with <paramref name="beatsPlayed" /> beats
      ///   counted, through the real path (stand-in client, real decision, real display). Main thread in.
      /// </summary>
      internal Task ReadSceneForDiagnostics(int beatsPlayed)
      {
         _dramaBeats = beatsPlayed;
         StartDramaturgeRead(MainSpeaker());

         return _dramaReading;
      }

      /// <summary>cr.test dramaturge_scene: the note the next Dramaturge reply takes (and clears), as a turn takes it.</summary>
      internal string TakeSceneDirectionForDiagnostics() => TakeSceneDirection();
   }
}
