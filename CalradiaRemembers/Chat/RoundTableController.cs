// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
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
      internal static readonly bool Enabled = false;

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
      public bool TryConsumeForMainReply(bool speakerIsMain, bool isCaptiveEncounter, bool conversationEnded)
         => _gate.TryConsumeForMainReply(speakerIsMain, isCaptiveEncounter, conversationEnded);

      /// <summary>
      ///   Round-table group mode: gives each of <paramref name="participants" /> a deterministic turn (round robin
      ///   from the gate's rotation start, capped by <see cref="RoundTablePolicy.DefaultCap" />), each told it is a
      ///   round-table turn (speak more, may address the others), then advances the rotation. The running transcript
      ///   carries what everyone said, so each speaker can respond to the previous ones. The caller (the host's
      ///   wrapper) owns re-enabling player input in its finally, mirroring the original method.
      /// </summary>
      public async Task RunAsync(IRoundTableHost host, List<ChatSpeaker> participants)
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

            IReadOnlyList<int> order = RoundTablePolicy.SpeakerOrder(
               participants.Count, _gate.Start, RoundTablePolicy.DefaultCap);

            foreach (int idx in order)
            {
               if (host.IsConversationEnded) break;

               if (!host.IsStillActiveView) return;

               if (idx < 0 || idx >= participants.Count) continue;

               ChatSpeaker speaker = participants[idx];
               NpcProfile profile = speaker.Profile;

               if (profile == null) continue;

               ChatSession session = host.BuildSession(speaker);
               const string stimulus = "(The floor is open to the group. Take your turn in the discussion now.)";

               EncounterContext context = EncounterContextBuilder.Build(
                  speaker.Hero, profile, behavior.GameStateBridge,
                  null,
                  host.BuildContextWitnesses(speaker),
                  isRoundTableTurn: true);

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

               string reply = host.RenderTurn(placeholder, speaker, result.Response);
               if (!string.IsNullOrEmpty(reply))
               {
                  host.RecordTranscriptLine(false, speaker.Name, speaker.Hero?.StringId, reply);
                  StoryLog.WriteNpcLine(speaker.Name, reply);
               }

               if (!string.IsNullOrWhiteSpace(result.Response?.Narration))
                  host.AppendNarration(result.Response.Narration);

               ModLog.Info($"[ROUNDTABLE] {speaker.Name}: \"{reply}\"");
               host.LogVram("roundtable");

               if (result.Response?.Actions != null)
                  foreach (GameAction action in result.Response.Actions)
                  {
                     if (!string.Equals(action.Type, "end_conversation", StringComparison.OrdinalIgnoreCase)) continue;
                     host.MarkConversationEndedBySpeaker(speaker.Name);

                     break;
                  }

               host.HandleWitnessLeaves(result.Response);
            }

            _gate.Start = RoundTablePolicy.NextStartIndex(participants.Count, _gate.Start, order.Count);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ROUNDTABLE] RunRoundTableAsync threw {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
