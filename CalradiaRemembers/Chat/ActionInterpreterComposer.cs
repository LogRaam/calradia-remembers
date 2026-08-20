// Code written by Gabriel Mailhot, 13/08/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   The UI-agnostic core of the PROSE + INTERPRETER composition, shared by BOTH the live chat
   ///   (<see cref="ChatViewModel" />) and the cr.rp_bench roleplay bench so the two feed the ACTION INTERPRETER
   ///   through ONE code path (same prompt, same context projection, same client/model, same failure = prose with
   ///   no tags). It deliberately owns NO UI concern: the caller decides when to resume the main thread and how to
   ///   consume the merged <see cref="ParsedResponse" />.
   /// </summary>
   public static class ActionInterpreterComposer
   {
      /// <summary>
      ///   Runs the MCM-configured ACTION INTERPRETER over the already-produced <paramref name="proseResponse" /> ONE
      ///   shot and returns a <see cref="ParsedResponse" /> that keeps every prose field but REPLACES the machine tags
      ///   (<see cref="ParsedResponse.Actions" /> and <see cref="ParsedResponse.NewEventData" />) with what the
      ///   interpreter read, for the caller to feed into the SAME consumption pipeline. On ANY failure (empty prose,
      ///   prompt build, call, or parse) it returns the prose with NO tags: a missed tag is applied as nothing rather
      ///   than disturbing the scene, and v1 never falls back to the prose model's own tags (the two modes stay
      ///   cleanly separated). It does NOT switch threads: the caller resumes the main thread before touching UI or
      ///   game state. The interpreter is routed to <paramref name="services" />'s dedicated InterpreterClient (its
      ///   own provider, key, base URL AND model) and named by <paramref name="playerLabel" />.
      ///   <paramref name="conversationInProgress" /> is the hard fact the interpreter otherwise has no way to
      ///   read (a stranger scene reads "regard +0, never met before" on EVERY turn): the CALLER knows the turn
      ///   index / session state, so it computes this and passes it through to
      ///   <see cref="ActionInterpreterContextBuilder.Project(EncounterContext, NpcProfile, string, bool)" />,
      ///   fixing the rp_bench scene-1 bug where every turn of an ongoing exchange kept being tagged first_meeting.
      /// </summary>
      public static async Task<ParsedResponse> InterpretAsync(
         ParsedResponse proseResponse, EncounterContext context, NpcProfile profile,
         string playerLabel, LlmServices services, CancellationToken ct, bool conversationInProgress)
      {
         string prose = CombineProseForInterpretation(proseResponse.Dialogue, proseResponse.Narration);
         if (string.IsNullOrWhiteSpace(prose))
            return WithInterpretedTags(proseResponse, null); // nothing to read -> apply no tags

         string prompt;
         try
         {
            string contextFacts = ActionInterpreterContextBuilder.Project(context, profile, playerLabel, conversationInProgress);
            prompt = ActionInterpreterPromptBuilder.Build(prose, contextFacts);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PROSE-INTERPRET] interpreter prompt build failed; applying no tags: {ex.GetType().Name}: {ex.Message}");

            return WithInterpretedTags(proseResponse, null);
         }

         var request = new LlmRequest {
            SystemPrompt = prompt,
            Messages = new[] {new LlmMessage(MessageRole.User, ActionInterpreterPromptBuilder.FinalInstruction)},
            // 900, not 600: the two-step FinalInstruction makes the model write a CHECK grounding line per
            // candidate action BEFORE the tag block. A reply carrying several deeds can spend 600 on the CHECK
            // preamble and truncate the tags (bench 2026-08-19 16:06: empty emits + a "no content" call). Even
            // with reasoning pinned off (LlmServices.InterpreterReasoningEffort) some models emit stray reasoning
            // tokens too, so the headroom is doubly earned. Must match the bench (ActionBenchRunner) so the bench
            // measures what the game does.
            Parameters = new LlmParameters {MaxTokens = 900, Creativity = 0.2f},
            // The dedicated InterpreterClient below owns the interpreter's provider, key, base URL AND model
            // (ResolveInterpreterModel: the configured Actions Interpreter model, or the scene's model when blank),
            // so no ModelOverride is needed here. Its ModelProvider still honours the TestModelOverride bench seam.
            ModelOverride = null
         };

         string content = null;
         string error = null;
         int budgetSeconds = Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 5;
         using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
         {
            cts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
            try
            {
               // Route to the interpreter's OWN client: its base URL / key / model are the interpreter provider's,
               // which may differ from the scene (prose) provider. Never null (falls back to the main client).
               LlmResponse resp = await Task.Run(() => services.InterpreterClient.CompleteAsync(request, cts.Token))
                                            .ConfigureAwait(false);
               if (resp is {IsSuccess: true}) content = resp.Content;
               else error = resp?.ErrorMessage ?? "no result";
            }
            catch (Exception ex)
            {
               error = $"{ex.GetType().Name}: {ex.Message}";
            }
         }

         if (error != null || string.IsNullOrWhiteSpace(content))
         {
            ModLog.Error($"[PROSE-INTERPRET] interpreter call produced no tags; applying none: {error ?? "empty reply"}");

            return WithInterpretedTags(proseResponse, null);
         }

         ParsedResponse interpreted;
         try
         {
            interpreted = new SectionResponseParser().Parse(content);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PROSE-INTERPRET] interpreter output could not be parsed; applying no tags: {ex.GetType().Name}: {ex.Message}");

            return WithInterpretedTags(proseResponse, null);
         }

         ModLog.Info($"[PROSE-INTERPRET] interpreter supplied {interpreted.Actions?.Count ?? 0} action(s)"
                     + (interpreted.NewEventData != null ? $" + 1 event ({interpreted.NewEventData.Type})" : "")
                     + $" (model: {services.ResolveInterpreterModel() ?? "resolved"}).");

         return WithInterpretedTags(proseResponse, interpreted);
      }

      /// <summary>
      ///   Splices <paramref name="interpreted" />'s machine tags (<see cref="ParsedResponse.Actions" /> and
      ///   <see cref="ParsedResponse.NewEventData" />) onto the prose turn, keeping every OTHER field of
      ///   <paramref name="prose" /> (dialogue, narration, stance, discovery, quests, witness reactions) exactly
      ///   as the prose model wrote it. A null <paramref name="interpreted" /> means "apply no tags": Actions empty
      ///   and NewEventData null, so the turn runs the prose with no ACTION/EVENT effect. Mirrors the field list
      ///   of <c>ChatViewModel.WithoutEventData</c> (plus Resolutions), so a field ADDED to
      ///   <see cref="ParsedResponse" /> must be added here too, or this path would silently drop it.
      /// </summary>
      internal static ParsedResponse WithInterpretedTags(ParsedResponse prose, ParsedResponse interpreted) => new() {
         Dialogue = prose.Dialogue,
         Narration = prose.Narration,
         NarrationBeforeDialogue = prose.NarrationBeforeDialogue,
         Memory = prose.Memory,
         NewEventData = interpreted?.NewEventData,
         Reputation = prose.Reputation,
         StanceShift = prose.StanceShift,
         Actions = interpreted?.Actions ?? new List<GameAction>(),
         Discovery = prose.Discovery,
         QuestGiven = prose.QuestGiven,
         QuestBlockMalformed = prose.QuestBlockMalformed,
         QuestCompleted = prose.QuestCompleted,
         QuestAbandoned = prose.QuestAbandoned,
         WitnessReactions = prose.WitnessReactions,
         Resolutions = prose.Resolutions
      };

      /// <summary>
      ///   Combines the NPC's narration (physical action) then dialogue into one prose block for the interpreter to
      ///   read, exactly as the cr.prose_interpret_test bench's CombineProse does, so the live path and the bench
      ///   feed the interpreter identically.
      /// </summary>
      internal static string CombineProseForInterpretation(string dialogue, string narration)
      {
         var sb = new StringBuilder();
         if (!string.IsNullOrWhiteSpace(narration)) sb.AppendLine(narration.Trim());
         if (!string.IsNullOrWhiteSpace(dialogue)) sb.Append(dialogue.Trim());

         return sb.ToString().Trim();
      }

      /// <summary>
      ///   The prompt-safe player label the interpreter names the player by, resolved exactly as
      ///   <see cref="LlmServices" /> feeds <c>PromptBuilder.PlayerName</c> (the test override when set, else the
      ///   live hero name), so the interpreted memory names the player the same way the prose prompt did. This also
      ///   honours the bench's forced player name (LlmServices.TestPlayerNameOverride).
      /// </summary>
      public static string ResolveDefaultPlayerLabel()
         => string.IsNullOrWhiteSpace(LlmServices.TestPlayerNameOverride)
            ? Hero.MainHero?.Name?.ToString() ?? ""
            : LlmServices.TestPlayerNameOverride;
   }
}
