// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 2 of the group-scene plan, "what a meeting leaves" (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "go pour tout le système ... je testerai demain". In the simulator a second meeting scored 4.2 blind with no
// memory, 5.2 with the events the game already gives, and 6.8 with lasting changes plus the bond.
//
// The engine half: on the durable summary queue's success path (ConversationSummaryService.ProcessConversationJobAsync), one
// background call to the Background model updates the character's NpcProfile.MeetingMemory through the SDK's
// MeetingMemoryUpdater. It rides the summary job, so it runs once per conversation, survives a save and quit like the summary
// does, and a chat too slight for a summary leaves nothing here either. The client is a parameter: the queue passes the
// housekeeping client, cr.test meeting_memory a canned one.
//
// The bond it writes is prompt texture only: nothing here touches ReputationWithPlayer, RelationGate or any gate.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Logic.Providers;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Updates what a character carries away from a chat with the player, when "Lasting memory of meetings" is on.</summary>
   public static class MeetingMemoryService
   {
      /// <summary>
      ///   Runs the update for one finished summary job. Never throws, and returns whether the record was updated: a failure
      ///   leaves the record as it was and never costs the summary its success.
      /// </summary>
      public static async Task<bool> UpdateAsync(NpcMemoryCampaignBehavior behavior, PendingSummaryJob job, ILlmClient client)
      {
         if (job == null) return false;

         // Checked BEFORE anything else: with the switch off the call is never made at all.
         if (!MeetingMemoryPolicy.ShouldUpdate(new MeetingMemoryFacts {
                SwitchOn = ModSettings.MeetingMemory,
                IsWitness = !string.IsNullOrWhiteSpace(job.HostName),
                HasTranscript = !string.IsNullOrWhiteSpace(job.Transcript)
             })) return false;

         try
         {
            NpcProfile npc = behavior?.Store?.Get(job.ProfileId);
            if (npc == null || client == null) return false;

            await Services.BackgroundCallHold.WhileConversationOpenAsync(Services.LlmServices.RunsLocally(ServiceRole.Housekeeping), $"meeting memory for {npc.Name}");

            bool updated;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CalradiaRemembers.Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds))))
            {
               bool? playerIsFemale = Hero.MainHero?.IsFemale;
               var updater = new MeetingMemoryUpdater(client);
               // The day of the meeting, not of the update: a job replayed after a reload keeps its own day.
               updated = await Task.Run(() => updater.UpdateAsync(npc, job.Transcript, job.PlayerName, job.AnchorDay, job.AnchorId,
                                                                  job.ReplyLanguage, playerIsFemale, cts.Token))
                                   .ConfigureAwait(false);
            }

            if (!updated)
            {
               ModLog.Info($"[MEETING MEMORY] {npc.Name}: nothing readable came back; the record is unchanged.");

               return false;
            }

            behavior.Store.Set(npc);
            MeetingMemory m = npc.MeetingMemory;
            ModLog.Info($"[MEETING MEMORY] {npc.Name}: {m?.Changes?.Count ?? 0} lasting change(s), bond "
                      + $"\"{(m?.Bond == null ? "" : MeetingMemoryUpdater.BondLine(m.Bond))}\", {m?.Unsettled?.Count ?? 0} unsettled, "
                      + $"{m?.Secrets?.Count ?? 0} secret(s).");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MEETING MEMORY] update failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }
   }
}
