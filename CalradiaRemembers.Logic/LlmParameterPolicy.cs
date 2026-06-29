// Code written by Gabriel Mailhot, 29/06/2026.
// Pure: decides how the chat-completion request must name and shape its sampling parameters so a model
// that is strict about them does not reject the call. The OpenAI reasoning models (the gpt-5 family and
// the o-series) reject the legacy "max_tokens" field (they want "max_completion_tokens") and reject any
// non-default "temperature". Aggregators like OpenRouter normalize this for us; a player pointing the
// OpenAI-Compatible endpoint straight at api.openai.com does not get that normalization, so the host
// asks this policy what to emit. Kept here, behind tests, because the model-id heuristic is the kind of
// thing that drifts as vendors ship new families, and the tests document exactly which ids switch modes.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How the host wants chat-completion sampling parameters shaped for the target model.</summary>
    public enum LlmCompatibilityMode
    {
        /// <summary>Detect a reasoning model from its id and shape the request accordingly.</summary>
        Auto,

        /// <summary>Force the classic contract: <c>max_tokens</c> plus an explicit <c>temperature</c>.</summary>
        Standard,

        /// <summary>Force the reasoning-model contract: <c>max_completion_tokens</c> and no <c>temperature</c>.</summary>
        Reasoning
    }

    /// <summary>The wire shape the request must use for its sampling parameters.</summary>
    public readonly struct LlmWireParameters
    {
        public LlmWireParameters(bool useMaxCompletionTokens, bool includeTemperature)
        {
            UseMaxCompletionTokens = useMaxCompletionTokens;
            IncludeTemperature = includeTemperature;
        }

        /// <summary>True to send the output cap as <c>max_completion_tokens</c>; false for the legacy <c>max_tokens</c>.</summary>
        public bool UseMaxCompletionTokens { get; }

        /// <summary>True to include a <c>temperature</c> field; false to omit it (reasoning models reject a custom value).</summary>
        public bool IncludeTemperature { get; }
    }

    /// <summary>
    ///   Resolves the chat-completion parameter wire shape for a target model. In <see cref="LlmCompatibilityMode.Auto" />
    ///   it recognizes the OpenAI reasoning families (gpt-5*, o1/o3/o4*) from the model id and switches them to the
    ///   reasoning contract; everything else keeps the classic contract.
    /// </summary>
    public static class LlmParameterPolicy
    {
        private static readonly LlmWireParameters Classic = new(useMaxCompletionTokens: false, includeTemperature: true);
        private static readonly LlmWireParameters ReasoningShape = new(useMaxCompletionTokens: true, includeTemperature: false);

        /// <summary>
        ///   Resolves the parameter shape for <paramref name="modelId" /> under <paramref name="mode" />.
        ///   Standard and Reasoning force their respective contracts; Auto decides from the model id.
        /// </summary>
        public static LlmWireParameters Resolve(string modelId, LlmCompatibilityMode mode)
        {
            switch (mode)
            {
                case LlmCompatibilityMode.Standard:
                    return Classic;
                case LlmCompatibilityMode.Reasoning:
                    return ReasoningShape;
                default:
                    return IsOpenAiReasoningModel(modelId) ? ReasoningShape : Classic;
            }
        }

        /// <summary>
        ///   True when <paramref name="modelId" /> names an OpenAI reasoning model that rejects <c>max_tokens</c> and a
        ///   custom <c>temperature</c> — the gpt-5 family or the o-series (o1/o3/o4, with or without a suffix). The id may
        ///   carry a provider prefix (e.g. <c>openai/gpt-5.4-mini</c>); only the part after the last slash is examined.
        ///   Note that gpt-4o is NOT a reasoning model and stays on the classic contract.
        /// </summary>
        public static bool IsOpenAiReasoningModel(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) return false;

            string id = modelId.Trim().ToLowerInvariant();

            int slash = id.LastIndexOf('/');
            if (slash >= 0 && slash < id.Length - 1) id = id.Substring(slash + 1);

            // The whole gpt-5 family (gpt-5, gpt-5-mini, gpt-5.4-mini, ...) uses the reasoning contract.
            if (id.StartsWith("gpt-5", StringComparison.Ordinal)) return true;

            // The o-series: o1, o3, o4 — followed by end-of-id or a '-' suffix (o3-mini, o4-mini, o1-preview).
            // Guarded to a digit right after the 'o' so gpt-4o / olmo / openchat never match.
            return IsOSeries(id);
        }

        #region private

        private static bool IsOSeries(string id)
        {
            if (id.Length < 2 || id[0] != 'o') return false;
            char d = id[1];
            if (d != '1' && d != '3' && d != '4') return false;

            // Nothing more, or a '-' suffix — but not a longer token like "o1x" or "o100k".
            return id.Length == 2 || id[2] == '-';
        }

        #endregion
    }
}
