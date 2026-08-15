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
    ///   Decides whether a PROSE turn should be redone on the configured fallback model. Two failure shapes earn the
    ///   redo. FIRST, an unclean finish: "stop" is the ONLY finish reason treated as a clean completion, mirroring
    ///   NpcMemoryService.Core.LlmClient.OpenRouter.OpenRouterClient's own retry gate (IsLengthTruncated matches
    ///   "length"; IsContentFiltered matches "content_filter"/"content-filter"/"moderation"; an empty or null reply
    ///   is treated as incomplete regardless of what finish reason came with it). Every other value, including one
    ///   this rule does not recognise, is treated as the reply not having finished normally, since a genuinely
    ///   complete OpenAI-compatible reply always reports "stop". SECOND, a beat that finished cleanly but produced NO
    ///   spoken dialogue line: in a driven scene (a captor working a captive, an NPC-led beat) a reply that is pure
    ///   narration with the speaker gone silent is a degenerate prose result, so the fallback model redoes it as a
    ///   complete beat rather than letting the character vanish from their own scene.
    /// </summary>
    public static class TruncationFallbackPolicy
    {
        /// <summary>
        ///   Finish-reason-only decision, for callers that have no prose content to inspect: equivalent to
        ///   <see cref="ShouldFallBack(string, bool, bool)" /> with <c>hasSpokenDialogue: true</c> (so only the
        ///   finish reason can trigger the redo). True when a fallback model is configured AND
        ///   <paramref name="finishReason" /> is anything other than "stop".
        /// </summary>
        public static bool ShouldFallBack(string finishReason, bool fallbackModelConfigured)
            => ShouldFallBack(finishReason, fallbackModelConfigured, true);

        /// <summary>
        ///   True when a fallback model is configured AND EITHER the beat carried no spoken dialogue line
        ///   (<paramref name="hasSpokenDialogue" /> false: the speaker went silent, only narration or nothing came
        ///   back) OR <paramref name="finishReason" /> is anything other than "stop" (length, content_filter/
        ///   content-filter/moderation, an empty or null reason, or any other unrecognised value).
        ///   <paramref name="fallbackModelConfigured" /> false never falls back, whatever the reply looked like,
        ///   since there is nowhere to send the redo.
        /// </summary>
        public static bool ShouldFallBack(string finishReason, bool fallbackModelConfigured, bool hasSpokenDialogue)
        {
            if (!fallbackModelConfigured) return false;

            // A beat with no spoken line is a degenerate prose result even on a clean "stop" finish: the driving
            // character produced narration (or nothing) but never spoke, so redo it as a complete beat.
            if (!hasSpokenDialogue) return true;

            bool cleanFinish = string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase);

            return !cleanFinish;
        }
    }
}
