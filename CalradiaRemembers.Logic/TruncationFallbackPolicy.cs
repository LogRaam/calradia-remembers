// Code written by Gabriel Mailhot, 15/08/2026.
// PROSE FALLBACK (Prose + Interpreter composition): decides whether a beat the PROSE model just produced should
// be redone, in one INTEGRATED call, on the configured fallback model. The prose call is issued with the SDK's
// internal double-budget retry disabled (NpcMemoryService.Core.Models.LlmParameters.AllowTruncationRetry = false)
// so a truncated reply comes back immediately instead of paying for a slow retry the mod is about to discard
// anyway; this policy is what decides, from the finish reason alone, whether that fast failure earns the fallback.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Decides whether a PROSE turn should be redone on the configured fallback model. "stop" is the ONLY finish
    ///   reason treated as a clean completion, mirroring NpcMemoryService.Core.LlmClient.OpenRouter.OpenRouterClient's
    ///   own retry gate (IsLengthTruncated matches "length"; IsContentFiltered matches "content_filter"/
    ///   "content-filter"/"moderation"; an empty or null reply is treated as incomplete regardless of what finish
    ///   reason came with it). Every other value, including one this rule does not recognise, is treated as the
    ///   reply not having finished normally, since a genuinely complete OpenAI-compatible reply always reports "stop".
    /// </summary>
    public static class TruncationFallbackPolicy
    {
        /// <summary>
        ///   True when a fallback model is configured AND <paramref name="finishReason" /> is anything other than
        ///   "stop" (length, content_filter/content-filter/moderation, an empty or null reason, or any other
        ///   unrecognised value). <paramref name="fallbackModelConfigured" /> false never falls back, whatever the
        ///   finish reason, since there is nowhere to send the redo.
        /// </summary>
        public static bool ShouldFallBack(string finishReason, bool fallbackModelConfigured)
        {
            if (!fallbackModelConfigured) return false;

            bool cleanFinish = string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase);

            return !cleanFinish;
        }
    }
}
