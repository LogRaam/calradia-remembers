// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Owns the round-table (group-lap) feature so it no longer threads through the shared ChatViewModel partials
   ///   (which forced a surgical-commit dance every release). Holds the pure decision state (whether the floor was
   ///   opened, and the round-robin rotation) in a testable <see cref="RoundTableGate" />, and drives the
   ///   deterministic lap through the <see cref="IRoundTableHost" /> seam. The lap body is a straight move of the
   ///   former ChatViewModel.RunRoundTableAsync; runtime behaviour is identical.
   /// </summary>
   internal sealed class RoundTableController
   {
      // Master kill-switch for the whole round-table (assembly pillar) feature. OFF: the button never shows and a
      // free-text "open the floor" never triggers, so the feature is inert (committed but disabled) until it has
      // been play-tested. Flip to true (one line) to re-enable it. static readonly, not const, so the guards below
      // are not folded into unreachable code.
      /// <summary>
      ///   The kill-switch: the council ships OFF and stays off until it has been play-tested. Settable only
      ///   so <c>cr.roundtable on</c> can arm it for a single session (audit 0.10): the glue has no unit tests
      ///   (net10 tests cannot reference the net472 mod) and no way to run at all without a rebuild, which
      ///   made "play-test it before flipping" circular. Never persisted, so a save can never carry it on, and
      ///   every entry point checks it (audit D6), not merely the button's visibility.
      /// </summary>
      internal static bool Enabled { get; set; } = false;

      /// <summary>
      ///   Assembly spike flag (2026-07-20): when armed (via <c>cr.council_test</c>), EVERY normal player turn is
      ///   built with the COUNCIL framing (`IsRoundTableTurn`), so ONE LLM call produces the anchor's reply plus a
      ///   [WITNESS_REACTION] from each present member who speaks or gestures, others silent, the whole-table
      ///   single-call model. This is distinct from <see cref="Enabled" /> (which drives the per-NPC round-table
      ///   LAP). Never persisted; a test/validation switch, not a shipped path.
      /// </summary>
      internal static bool CouncilTurnsForced { get; set; } = false;

      private readonly RoundTableGate _gate = new RoundTableGate();

      /// <summary>True when the round-table button/UI should be shown (a group is present, not captive, not ended), and the feature is enabled.</summary>
      public bool ComputeVisible(int witnessCount, bool isCaptiveEncounter, bool conversationEnded)
         => Enabled && _gate.ComputeVisible(witnessCount, isCaptiveEncounter, conversationEnded);

      /// <summary>Records whether the player's own message opened the floor to the whole group this turn (no-op while disabled).</summary>
      public void OnPlayerSend(string text, int witnessCount, bool isCaptiveEncounter)
      {
         if (Enabled) _gate.OnPlayerSend(text, witnessCount, isCaptiveEncounter);
      }

      /// <summary>Consumes a pending "open the floor" request once, after the main NPC's reply.</summary>
      public bool TryConsumeForMainReply(RoundTableGate.FloorTurnFacts turn)
         => _gate.TryConsumeForMainReply(turn);

      /// <summary>
      ///   True while an "open the floor" request is pending, without consuming it: the host reads it to drop
      ///   the main NPC's ventriloquized witness reactions on the triggering turn (round-table audit D7).
      /// </summary>
      public bool IsRequestPending => Enabled && _gate.IsRequestPending;

      /// <summary>
      ///   Round-table group mode: gives each of <paramref name="participants" /> a deterministic turn (round robin
      ///   from the gate's rotation start, capped by <paramref name="cap" />, or <see cref="RoundTablePolicy.DefaultCap" />
      ///   when null), each told it is a round-table turn (speak more, may address the others), then advances the
      ///   rotation. The running transcript carries what everyone said, so each speaker can respond to the previous
      ///   ones. The caller (the host's wrapper) owns re-enabling player input in its finally, mirroring the
      ///   original method.
      /// </summary>
      /// <param name="host">The chat view model, through the seam.</param>
      /// <param name="participants">Everyone present, main speaker first.</param>
      /// <param name="cap">
      ///   Max turns this burst. Null keeps the in-chat button's deliberate 3-speaker cap
      ///   (<see cref="RoundTablePolicy.DefaultCap" />); a council passes <c>participants.Count</c> so the FULL
      ///   seated roster speaks every turn, uncapped: a real council has no "some of the table" concept.
      /// </param>
      public async Task RunAsync(IRoundTableHost host, List<ChatSpeaker> participants, int? cap = null)
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;

            if (participants == null || participants.Count < 2 || behavior?.LlmServices is not {IsAvailable: true})
               return;

            var world = new WorldState {
               CurrentDay = behavior.GameStateBridge.GetCurrentGameDay(),
               Season = behavior.GameStateBridge.GetSeasonName(),
               TimeOfDay = behavior.GameStateBridge.GetTimeOfDay(),
               ActiveConflicts = behavior.GameStateBridge.GetActiveConflicts(),
               Rumors = ""
            };

            int effectiveCap = cap ?? RoundTablePolicy.DefaultCap;
            IReadOnlyList<int> order = RoundTablePolicy.SpeakerOrder(new RoundTablePolicy.RoundTableFacts {ParticipantCount = participants.Count, StartIndex = _gate.Start, Cap = effectiveCap});

            foreach (int idx in order)
            {
               if (host.IsConversationEnded) break;

               if (!host.IsStillActiveView) return;

               if (idx < 0 || idx >= participants.Count) continue;

               ChatSpeaker speaker = participants[idx];
               NpcProfile profile = speaker.Profile;

               if (profile == null) continue;

               // Round-table audit D3: the lap walks a SNAPSHOT taken when the floor opened. A participant
               // who left mid-lap (a witness_leaves honoured during someone else's turn) was removed from the
               // room but not from the snapshot, so with the wrap-around they spoke again afterwards, while
               // the other speakers' prompts already described them as gone. Re-check presence each turn.
               if (!host.IsStillPresent(speaker))
               {
                  ModLog.Info($"[ROUNDTABLE] skipping {speaker.Name}: they have left the conversation.");

                  continue;
               }

               await host.EnsureHistoryCompressedAsync();
               ChatSession session = host.BuildSession(speaker);
               const string stimulus = "(The floor is open to the group. Take your turn in the discussion now.)";

               EncounterContext context = EncounterContextBuilder.Build(
                  speaker.Hero, profile, behavior.GameStateBridge,
                  null,
                  host.BuildContextWitnesses(speaker),
                  isRoundTableTurn: true);

               // Bespoke council window: highlight this seat while their turn is taken (a no-op outside a
               // council, and harmless for the button/free-text lap over the ordinary chat prefab).
               host.SetActiveCouncilSpeaker(speaker);
               ChatMessageViewModel placeholder = host.AddPlaceholder(speaker);

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

               await MainThreadDispatcher.SwitchToMainThread();

               if (!host.IsStillActiveView) return;

               if (!result.IsSuccess)
               {
                  host.RemovePlaceholder(placeholder);
                  ModLog.Error($"[ROUNDTABLE] {speaker.Name} turn failed: {result.ErrorMessage}");

                  continue;
               }

               // A turn the model LED with its narration keeps that order at render (and in the
               // transcript/story entries AppendNarration writes ahead of the spoken line's).
               bool narrationFirst = result.Response?.NarrationBeforeDialogue == true
                                     && !string.IsNullOrWhiteSpace(result.Response.Narration);
               if (narrationFirst)
                  host.AppendNarration(result.Response.Narration, placeholder);

               string reply = host.RenderTurn(placeholder, speaker, result.Response);
               if (!string.IsNullOrEmpty(reply))
               {
                  host.RecordTranscriptLine(false, speaker.Name, speaker.Hero?.StringId, reply);
                  StoryLog.WriteNpcLine(speaker.Name, reply);
               }

               if (!narrationFirst && !string.IsNullOrWhiteSpace(result.Response?.Narration))
                  host.AppendNarration(result.Response.Narration);

               ModLog.Info($"[ROUNDTABLE] {speaker.Name}: \"{reply}\"");
               // Round-table audit 0.10: the lap costs one FULL prompt per speaker (identity sits in the
               // cached prefix, so nothing is shared between participants) and logged nothing, which made a
               // council's real price invisible. Mirrors the [15C TOKENS] line so both bursts are comparable.
               if (result.Usage != null)
                  ModLog.Info($"  [ROUNDTABLE TOKENS] {speaker.Name}: prompt={result.Usage.PromptTokens} "
                            + $"(cached:{result.Usage.CachedPromptTokens ?? 0}) | completion={result.Usage.CompletionTokens}");
               host.LogVram("roundtable");

               if (result.Response?.Actions != null)
                  foreach (GameAction action in result.Response.Actions)
                  {
                     if (!string.Equals(action.Type, "end_conversation", StringComparison.OrdinalIgnoreCase)) continue;
                     host.MarkConversationEndedBySpeaker(speaker.Name);

                     break;
                  }

               // kurushimi00 (06/11/2026): characters make their peace with each other here more than anywhere, and the lap
               // teaches reconcile_with (the per-speaker witnesses reach EncounterContextBuilder), so it must carry it out too,
               // as this speaker, with those still in the room.
               MakePeace(result.Response, speaker, participants.Where(host.IsStillPresent).Select(p => p.Hero?.StringId).ToList(), behavior);

               host.HandleWitnessLeaves(result.Response);

               // Council audit C1: the lap teaches the [RESOLUTION] channel (isRoundTableTurn -> the RECORDING
               // block), so it must also CONSUME it, or every pledge a seated member makes in per-member voice
               // mode is parsed and silently dropped. A no-op outside a council (the host gates on _councilScope).
               // The whole-table path records its resolutions in RunLlmExchangeAsync; in per-member mode that
               // path is never taken, so this is the ONLY place the non-anchor members' pledges are recorded.
               host.ProcessCouncilResolutions(result.Response);
            }

            host.ClearActiveCouncilSpeaker();
            _gate.Start = RoundTablePolicy.NextStartIndex(new RoundTablePolicy.RoundTableFacts {ParticipantCount = participants.Count, StartIndex = _gate.Start, Cap = order.Count});
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROUNDTABLE] RunRoundTableAsync threw {ex.GetType().Name}: {ex.Message}");
         }
      }

      // reconcile_with emitted on a lap turn, run as the speaker of that turn among those present; nothing else is carried
      // out here (the lap's own design, audit 15C), so this is the one verb between two people in the room.
      private static void MakePeace(ParsedResponse response, ChatSpeaker speaker, List<string> presentIds, NpcMemoryCampaignBehavior behavior)
      {
         if (response?.Actions == null || speaker?.Hero == null || speaker.Profile == null || behavior?.GameStateBridge == null) return;

         var acted = false;
         foreach (GameAction action in response.Actions.Where(a => string.Equals(a?.Type, "reconcile_with", StringComparison.OrdinalIgnoreCase)))
         {
            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(action, speaker.Hero, new ActionContext(speaker.Profile, null) {PresentHeroIds = presentIds});
            ModLog.Info($"[ROUNDTABLE] {speaker.Name} reconcile_with: {outcome?.Message ?? "nothing"}");
            acted = true;
         }

         if (acted) behavior.Store?.Set(speaker.Profile);
      }
   }
}
