// Code written by Gabriel Mailhot, 15/11/2026.
// VonLemberg (Discord, 15/11/2026, RFC "Headless NPC Persona Invocation API"): a diplomacy mod wants a ruler's words
// "without initiating a player conversation", through "the configured CR/Relay provider infrastructure where possible".
// Gabriel (15/11/2026): the other mod runs its own LLM calls from the persona CR hands it (CrPrompt.Persona), and may
// instead "lancer l'appel a partir de CR". This is that door: one plain completion through the player's own CR connection
// (the prose model, key and provider the player chose), and nothing else. It reads no memory, writes no memory, opens no
// conversation, parses no [ACTION] and changes nothing in the campaign; what the caller does with the text is the caller's.
// The player can shut it in the MCM ("Other mods may use my connection"); every call is logged with the caller's name and
// counts in CR's own usage figures like any other.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Extension;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>One turn of the conversation a mod sends through <see cref="CrLlm" />. Since 2.8.12.</summary>
   public sealed class CrLlmTurn
   {
      /// <summary>True for a line the model itself said before (assistant), false for a line put to it (user).</summary>
      public bool FromModel { get; set; }

      /// <summary>The line.</summary>
      public string Text { get; set; }
   }

   /// <summary>One request a mod sends through <see cref="CrLlm" />. Since 2.8.12.</summary>
   public sealed class CrLlmRequest
   {
      /// <summary>The calling mod's name (required): it signs the request in CR's log.</summary>
      public string SourceMod { get; set; }

      /// <summary>Your own correlation id, handed back unchanged on the result.</summary>
      public string RequestId { get; set; }

      /// <summary>The system prompt: typically your instructions, then the persona from CrPrompt.Persona.</summary>
      public string SystemPrompt { get; set; }

      /// <summary>The conversation so far, oldest first; the last turn is usually the line to answer.</summary>
      public IList<CrLlmTurn> Turns { get; set; } = new List<CrLlmTurn>();

      /// <summary>The answer length in tokens; 0 = 600, held to at most 4000.</summary>
      public int MaxOutputTokens { get; set; }

      /// <summary>Sampling temperature, 0 to 2; default 0.8.</summary>
      public float Creativity { get; set; } = 0.8f;

      /// <summary>
      ///   The model id to use instead of the player's prose model, sent as is on the player's connection (same endpoint,
      ///   same key): an OpenRouter id such as "z-ai/glm-5.2", or whatever name a relay the player runs maps to a model.
      ///   Null or blank: the player's prose model, as before. An id the provider does not know fails; CR never falls back
      ///   to another model. Since 2.8.15.
      /// </summary>
      public string Model { get; set; }
   }

   /// <summary>What came back from <see cref="CrLlm.CompleteAsync" />. Since 2.8.12.</summary>
   public sealed class CrLlmResult
   {
      /// <summary>Your RequestId, unchanged.</summary>
      public string RequestId { get; set; }

      /// <summary>True when the model answered.</summary>
      public bool Succeeded { get; set; }

      /// <summary>True when nothing was sent (the player shut the door, no connection, a malformed request): nothing was spent.</summary>
      public bool Refused { get; set; }

      /// <summary>True when your token cancelled it.</summary>
      public bool Cancelled { get; set; }

      /// <summary>True when the campaign that asked is gone (a load or a return to the menu) by the time the answer came: do not apply it.</summary>
      public bool CampaignChanged { get; set; }

      /// <summary>The model's raw answer; empty unless Succeeded.</summary>
      public string Text { get; set; } = "";

      /// <summary>Why it did not succeed, in plain English; empty when it did.</summary>
      public string Error { get; set; } = "";

      /// <summary>Prompt tokens billed, when the provider says.</summary>
      public int PromptTokens { get; set; }

      /// <summary>Answer tokens billed, when the provider says.</summary>
      public int CompletionTokens { get; set; }

      /// <summary>The cost in dollars, when the provider reports it (OpenRouter does).</summary>
      public double? Cost { get; set; }

      /// <summary>The model id the request was sent to: the request's Model, or the player's prose model. Empty when nothing was sent. Since 2.8.15.</summary>
      public string Model { get; set; } = "";
   }

   /// <summary>
   ///   Sends one plain completion through the player's own Calradia Remembers connection, for a mod that wants CR's model,
   ///   key and provider instead of its own. No memory, no conversation, no action, no campaign change. Since 2.8.12.
   /// </summary>
   public static class CrLlm
   {
      /// <summary>Whether a request would be sent right now: the player allows it and CR's connection is ready. Game thread.</summary>
      public static bool IsReady => Unavailable() == "";

      /// <summary>Why a request would not be sent right now, in plain English; empty when it would. Game thread.</summary>
      public static string Unavailable()
         => ExternalLlmRequestPolicy.Explain(ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
            SourceMod = "?", HasSomethingToAnswer = true, PlayerAllowsOtherMods = ModSettings.AllowOtherModsLlm, ConnectionReady = ConnectionReady()
         }).Refusal);

      /// <summary>
      ///   Sends <paramref name="request" /> and returns the model's answer. Call it from the game thread; the returned task
      ///   may complete on a worker thread, so bring the result back to the game thread before touching game objects. Never
      ///   throws: every failure comes back as a result with Succeeded false and an Error.
      /// </summary>
      public static async Task<CrLlmResult> CompleteAsync(CrLlmRequest request, CancellationToken cancellationToken = default)
      {
         var result = new CrLlmResult {RequestId = request?.RequestId};
         try
         {
            List<LlmMessage> messages = (request?.Turns ?? new List<CrLlmTurn>())
                                        .Where(t => !string.IsNullOrWhiteSpace(t?.Text))
                                        .Select(t => new LlmMessage(t.FromModel ? MessageRole.Assistant : MessageRole.User, t.Text))
                                        .ToList();
            ExternalLlmRuling ruling = ExternalLlmRequestPolicy.Rule(new ExternalLlmRequestFacts {
               SourceMod = request?.SourceMod,
               HasSomethingToAnswer = messages.Count > 0 || !string.IsNullOrWhiteSpace(request?.SystemPrompt),
               AskedMaxOutputTokens = request?.MaxOutputTokens ?? 0,
               PlayerAllowsOtherMods = ModSettings.AllowOtherModsLlm,
               ConnectionReady = ConnectionReady(),
               AskedModel = request?.Model
            });
            if (!ruling.Allowed)
            {
               result.Refused = true;
               result.Error = ExternalLlmRequestPolicy.Explain(ruling.Refusal);
               ModLog.Info($"[EXT LLM] {request?.SourceMod ?? "(unnamed)"} refused: {result.Error}");

               return result;
            }

            // Providers want a user turn to answer; a request made only of a system prompt gets a neutral one.
            if (messages.Count == 0) messages.Add(new LlmMessage(MessageRole.User, "Continue."));

            ILlmClient client = NpcMemoryCampaignBehavior.Instance.LlmServices.LlmClient;
            result.Model = ruling.Model ?? NpcMemoryCampaignBehavior.Instance.LlmServices.ModelId ?? "";
            Campaign asked = Campaign.Current;
            LlmResponse reply = await client.CompleteAsync(new LlmRequest {
               SystemPrompt = request.SystemPrompt ?? "",
               Messages = messages,
               Parameters = new LlmParameters {MaxTokens = ruling.MaxOutputTokens, Creativity = Math.Max(0f, Math.Min(2f, request.Creativity))},
               // VonLemberg (17/11/2026): a model of the caller's own; the SDK drops the player's provider pin for it.
               ModelOverride = ruling.Model,
               OmitProviderPin = ruling.Model != null
            }, cancellationToken).ConfigureAwait(false);

            result.PromptTokens = reply?.Usage?.PromptTokens ?? 0;
            result.CompletionTokens = reply?.Usage?.CompletionTokens ?? 0;
            result.Cost = reply?.Usage?.Cost;
            result.CampaignChanged = !ReferenceEquals(asked, Campaign.Current);
            result.Succeeded = reply != null && reply.IsSuccess && !result.CampaignChanged;
            result.Text = result.Succeeded ? reply.Content ?? "" : "";
            result.Error = result.Succeeded ? ""
               : result.CampaignChanged ? "The campaign that asked is no longer loaded."
               : reply?.ErrorMessage ?? "No answer.";
            ModLog.Info($"[EXT LLM] {request.SourceMod} id={request.RequestId ?? "-"} model={result.Model}{(ruling.Model != null ? " (asked)" : "")}: {(result.Succeeded ? "answered" : "failed: " + result.Error)}, " +
                        $"{result.PromptTokens}+{result.CompletionTokens} tokens{(result.Cost.HasValue ? $", ${result.Cost.Value:0.0000}" : "")}");
         }
         catch (OperationCanceledException)
         {
            result.Cancelled = true;
            result.Error = "Cancelled.";
         }
         catch (Exception ex)
         {
            result.Error = ex.Message;
            ModLog.Fault($"[EXT LLM] {request?.SourceMod}", ex);
         }

         return result;
      }

      private static bool ConnectionReady()
         => Campaign.Current != null && NpcMemoryCampaignBehavior.Instance?.LlmServices is { IsAvailable: true, LlmClient: not null };
   }
}
