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
        public static bool ShouldRedo(bool enhancedMode, string beatModel, string nsfwModel, string reply, string finishReason)
        {
            if (!enhancedMode || string.IsNullOrWhiteSpace(nsfwModel)) return false;
            if (string.Equals(nsfwModel.Trim(), beatModel?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(reply) || DialogueHeuristics.LooksLikeRefusal(reply)) return true;

            return !string.IsNullOrWhiteSpace(finishReason) && !string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase);
        }
    }
}
