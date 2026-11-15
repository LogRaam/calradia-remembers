// Code written by Gabriel Mailhot, 04/10/2026.
// NSFW FALLBACK FOR A REFUSED BEAT: decides whether a beat the scene's model just wrote is redone, once, on the NSFW
// model. Gabriel (04/10/2026): in Enhanced mode the werewolf tale's narrator answered an explicit turn with its own
// safety refusal, and he had to switch the in-chat model to NSFW by hand, where the MCM hint promises the NSFW model
// is "the fallback in Enhanced". TruncationFallbackPolicy keeps deciding the 1:1 truncation redo; this adds the
// refusal, and serves the scene narrator, which had no fallback at all.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>A reply and the models it may be redone with.</summary>
    public sealed class RefusalRedoFacts
    {
        /// <summary>The enhanced mode.</summary>
        public bool EnhancedMode { get; init; }

        /// <summary>The beat model.</summary>
        public string BeatModel { get; init; }

        /// <summary>The nsfw model.</summary>
        public string NsfwModel { get; init; }

        /// <summary>The reply.</summary>
        public string Reply { get; init; }

        /// <summary>The finish reason.</summary>
        public string FinishReason { get; init; }
    }

    /// <summary>
    ///   Whether a beat is redone on the NSFW model. Only in Enhanced mode (Roleplay and NSFW are the player's own
    ///   choice of model), only when the NSFW model is a different model from the one that wrote the beat (asking
    ///   the same model again buys the same refusal), and only when the beat failed: an out-of-character refusal
    ///   (<see cref="DialogueHeuristics.LooksLikeRefusal" />), an empty reply, or a reply the provider cut or filtered
    ///   (any finish reason other than "stop"). A full reply with NO finish reason is not a failure: local servers
    ///   often send none.
    /// </summary>
    public static class RefusalFallbackPolicy
    {
        public static bool ShouldRedo(RefusalRedoFacts facts) => ShouldRedo(facts.EnhancedMode, facts.BeatModel, facts.NsfwModel, facts.Reply, facts.FinishReason);

        // The positional form behind the facts, private so no caller can pass two of its same-typed arguments the wrong
        // way round and still compile (swap-risk ratchet, 14/11/2026).
        private static bool ShouldRedo(bool enhancedMode, string beatModel, string nsfwModel, string reply, string finishReason)
        {
            if (!enhancedMode || string.IsNullOrWhiteSpace(nsfwModel)) return false;
            if (string.Equals(nsfwModel.Trim(), beatModel?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(reply) || DialogueHeuristics.LooksLikeRefusal(reply)) return true;

            return !string.IsNullOrWhiteSpace(finishReason) && !string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase);
        }
    }
}
