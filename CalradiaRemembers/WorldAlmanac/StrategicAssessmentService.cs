// Code written by Gabriel Mailhot, 04/09/2026.
// Strategic Assessment orchestrator: fills the Balance of Power panel's assessment. A companion (or a soldier)
// voices an LLM counsel; a lone player gets an offline factual summary; every LLM result is cached by a signature
// of the world + the player's position, so reopening the same state is instant and never re-calls. The async call
// follows the ChronicleIlluminationService recipe: return a "the council debates.." line at once, then swap in the
// real counsel on the main thread when it lands (and it lands even if the panel was closed meanwhile).

#region

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic.WorldAlmanac;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Populates a Balance of Power view model's Strategic Assessment: offline summary, cached counsel, or a fresh async LLM call.</summary>
   public static class StrategicAssessmentService
   {
      private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();

      private static readonly LlmParameters CounselParameters = new LlmParameters
      {
         MaxTokens = 220,
         Creativity = 0.6f,
         ReasoningOverride = "off" // a few sentences of prose counsel, never chain-of-thought
      };

      /// <summary>
      ///   Sets the view model's StrategicAssessment. Offline summary when the feature is off or the player travels
      ///   alone; a cached counsel when this exact world+position was assessed before; otherwise a "debating.." line
      ///   now and the real counsel swapped in when the async LLM call returns.
      /// </summary>
      public static void Populate(BalanceOfPowerViewModel vm, PowerIndexResult result)
      {
         if (vm == null) return;

         try
         {
            StrategicAssessmentContext ctx = StrategicAssessmentContextBuilder.Build(result);

            ILlmClient client = NpcMemoryCampaignBehavior.Instance?.LlmServices?.LlmClient;

            // Offline path: feature off, no advisor (player alone), or no AI configured -> the deterministic summary.
            if (!ModSettings.EnableStrategicAssessment || !ctx.HasAdvisor || client == null)
            {
               vm.StrategicAssessment = StrategicAssessmentSummary.Compose(result, ctx.Player);

               return;
            }

            string signature = Signature(result, ctx);
            if (Cache.TryGetValue(signature, out string cached) && !string.IsNullOrWhiteSpace(cached))
            {
               vm.StrategicAssessment = cached;

               return;
            }

            vm.StrategicAssessment = LoadingLine(ctx.Advisor);
            FireAsync(vm, result, ctx, client, signature);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STRAT_ASSESS] populate failed: {ex.GetType().Name}: {ex.Message}");
            vm.StrategicAssessment = StrategicAssessmentSummary.Compose(result, new PlayerStrategicContext("", 0, null));
         }
      }

      #region private

      private static void FireAsync(BalanceOfPowerViewModel vm, PowerIndexResult result, StrategicAssessmentContext ctx, ILlmClient client, string signature)
      {
         string prompt = StrategicAssessmentPromptBuilder.Build(result, ctx.Player, ctx.Advisor);

         var request = new LlmRequest
         {
            SystemPrompt = prompt,
            Messages = new List<LlmMessage> {new LlmMessage(MessageRole.User, "Give your counsel now.")},
            Parameters = CounselParameters
         };

         _ = Task.Run(async () =>
         {
            string counsel = null;
            try
            {
               LlmResponse response = await client.CompleteAsync(request).ConfigureAwait(false);
               if (response is {IsSuccess: true} && !string.IsNullOrWhiteSpace(response.Content))
                  counsel = response.Content.Trim();
            }
            catch (Exception ex)
            {
               ModLog.Error($"[STRAT_ASSESS] LLM counsel failed: {ex.GetType().Name}: {ex.Message}");
            }

            // Fall back to the offline summary on any failure, so the panel never sits on the "debating.." line.
            string final = string.IsNullOrWhiteSpace(counsel)
               ? StrategicAssessmentSummary.Compose(result, ctx.Player)
               : counsel;

            if (!string.IsNullOrWhiteSpace(counsel)) Cache[signature] = counsel;

            // Swap the bound text on the main thread; it lands even if the panel was closed before the call returned.
            MainThreadDispatcher.Enqueue(() => vm.StrategicAssessment = final);
         });
      }

      private static string LoadingLine(StrategicAdvisor advisor)
         => advisor != null && advisor.Kind == AdvisorKind.CommonSoldier
            ? "Your veteran squints over the maps of Calradia.."
            : "Your war council confers over the maps of Calradia..";

      // A stable key for the world + the player's position + who is assessing: the same inputs reuse the same
      // counsel, a conquest or a new war or a different advisor mints a new one.
      private static string Signature(PowerIndexResult result, StrategicAssessmentContext ctx)
      {
         var sb = new StringBuilder();
         IReadOnlyList<KingdomPowerScore> ranked = result?.Ranked ?? new List<KingdomPowerScore>();
         foreach (KingdomPowerScore s in ranked)
            if (s != null)
               sb.Append(s.KingdomId).Append(':').Append(s.Rank).Append(':').Append((int) Math.Round(s.PowerIndex)).Append('|');

         sb.Append("P=").Append(ctx.Player.PlayerKingdomName).Append(':').Append(ctx.Player.PlayerKingdomRank);
         sb.Append(";W=").Append(string.Join(",", ctx.Player.AtWarWith));
         sb.Append(";A=").Append(ctx.Advisor?.Kind).Append(':').Append(ctx.Advisor?.Tier).Append(':').Append(ctx.Advisor?.Name);

         return sb.ToString();
      }

      #endregion
   }
}
