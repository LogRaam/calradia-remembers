// Code written by Gabriel Mailhot, 15/08/2026.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Services;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Services;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   PROSE FALLBACK (Prose + Interpreter composition, truncation safety net): when the PROSE model returns a
   ///   beat that did not finish cleanly (see <see cref="Logic.TruncationFallbackPolicy" />), the caller skips the
   ///   slow double-budget retry (its own PROSE call is issued with <c>allowTruncationRetry: false</c>) and this
   ///   composer instead redoes THIS SAME turn as a single INTEGRATED call (prose + [ACTION]/[EVENT] tags
   ///   together, no separate interpreter step afterward) forced onto a reliable, explicit fallback model,
   ///   by default the Action Interpreter model (grok), already configured for exactly this reason.
   ///   Shared by the live chat (<see cref="ChatViewModel" />) and cr.rp_bench, so BOTH exercise the identical
   ///   fallback path (mirrors how <see cref="ActionInterpreterComposer" /> shares the ordinary interpreter step).
   ///   Routes to <see cref="LlmServices.InterpreterClient" /> (the interpreter role's own provider/key/base URL)
   ///   with the resolved fallback model forced via <c>modelOverride</c>, so this call NEVER touches
   ///   <see cref="LlmServices.ActiveModelMode" /> or the scene's own resolved model: only this ONE call targets
   ///   the fallback model and provider.
   /// </summary>
   public static class ProseFallbackComposer
   {
      /// <summary>
      ///   Redoes the current turn on <paramref name="fallbackModel" />, via the interpreter's own client so the
      ///   call reaches the fallback's actual provider (key/base URL), not the scene's. First rolls back the
      ///   truncated prose turn just recorded in <paramref name="session" /> (its assistant reply, then the
      ///   player's message, both added by the prose ChatAsync call that preceded this one) so the fallback's own
      ///   turn replaces it cleanly rather than appending a second one. The fallback call keeps
      ///   <c>allowTruncationRetry: true</c> (the default): the fallback model is the reliable one, so if it
      ///   somehow truncates too, one retry is an acceptable cost for completing the beat. On any failure the
      ///   ORIGINAL truncated turn (<paramref name="truncatedDialogue" />) is put back into
      ///   <paramref name="session" />, or the caller's decision to keep showing that truncated reply (its own
      ///   fallback-of-fallback: a beat that fails twice still beats a blank one) would leave the session silently
      ///   missing the very turn just shown to the player. Never throws: any fault surfaces as a failed
      ///   <see cref="NpcChatResult" />, mirroring every other LLM call site in the mod.
      ///   <paramref name="client" /> is the provider the redo goes to: the interpreter's when null (the truncation
      ///   redo, as always), the NSFW role's own for a refused beat (04/10/2026).
      /// </summary>
      public static async Task<NpcChatResult> RunIntegratedFallbackAsync(
         NpcProfile profile, WorldState world, ChatSession session, string playerMessage, string truncatedDialogue,
         EncounterContext context, LlmServices services, string fallbackModel, CancellationToken ct,
         NpcMemoryService.Core.LlmClient.ILlmClient client = null)
      {
         session.RollbackLastMessage(); // the truncated prose reply
         session.RollbackLastMessage(); // the player's message, re-added by ChatAsync below (or restored on failure)

         var fallbackService = new NpcChatService(client ?? services.InterpreterClient, new SectionResponseParser(), services.ChatService.PromptBuilder) {
            ChatParameters = services.ChatService.ChatParameters
         };

         int budgetSeconds = Math.Max(15, ModSettings.LlmResponseTimeoutSeconds) + 5;
         using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
         cts.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));

         // The fallback COMPLETES a beat the primary model started, on the SAME full system prompt the primary got
         // (persona, traits, kinks, romantic profile, history, all built by services.ChatService.PromptBuilder from
         // this profile), so the context is already there. Two out-of-fiction cues are appended to the message the
         // fallback call sees ONLY (never the ORIGINAL playerMessage the restore/UI use): (1) an anti-repeat cue,
         // because on back-to-back fallbacks the fallback model (grok) tended to echo the previous beat's imagery;
         // (2) an in-character reminder, because a completion model weights the persona above less than the primary
         // did, so naming the NPC and their nature here makes the traits it already has land harder.
         string npcName = string.IsNullOrWhiteSpace(profile?.Name) ? "this character" : profile.Name.Trim();
         string nature = string.IsNullOrWhiteSpace(profile?.Trait) ? null : profile.Trait.Trim();
         string fallbackMessage = (playerMessage ?? string.Empty)
            + " [Complete THIS beat FRESH: do NOT reuse any image, phrase, position, or gesture from the previous"
            + " beat; move the action somewhere concretely new. Stay fully in " + npcName + "'s established"
            + " character, voice, and dynamic" + (nature == null ? "" : " (" + nature + ")")
            + "; the persona and traits above and the earlier turns are authoritative.]";

         NpcChatResult result;
         try
         {
            result = await Task.Run(() => fallbackService.ChatAsync(
                                 profile, world, session, fallbackMessage, context, cts.Token,
                                 allowTruncationRetry: true, modelOverride: fallbackModel))
                             .ConfigureAwait(false);
         }
         catch (Exception ex)
         {
            result = NpcChatResult.Failure($"{ex.GetType().Name}: {ex.Message}");
         }

         // ChatAsync's own failure path rolls back only the player message IT added, so a failed fallback call
         // leaves the session with neither the player's line nor any NPC reply for this turn: restore exactly
         // what the two rollbacks above removed, matching what the caller still shows the player.
         if (!result.IsSuccess)
         {
            session.AddPlayerMessage(playerMessage);
            session.AddNpcMessage(truncatedDialogue ?? string.Empty);
         }

         return result;
      }
   }
}
