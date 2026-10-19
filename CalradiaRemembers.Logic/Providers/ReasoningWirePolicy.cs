// Code written by Gabriel Mailhot, 29/08/2026.
// A reasoning-effort keyword can be sent over the wire in two shapes: OpenRouter's nested "reasoning" object, or a
// top-level "reasoning_effort" string. This is the single place that decides which shape a given ProviderKind wants,
// so the decision cannot drift between the main chat client, the interpreter client, and the diagnostic log (it
// used to live as three separate inline checks).

namespace CalradiaRemembers.Logic.Providers
{
    /// <summary>
    ///   Decides whether a provider kind expects the reasoning-effort keyword serialized as the top-level
    ///   <c>reasoning_effort</c> string rather than OpenRouter's nested <c>reasoning</c> object.
    /// </summary>
    public static class ReasoningWirePolicy
    {
        /// <summary>
        ///   True when <paramref name="kind" /> reads its reasoning effort from the top-level
        ///   <c>reasoning_effort</c> string (OpenAI-Compatible endpoints, and NanoGPT which treats that field as
        ///   authoritative and ignores the OpenRouter object form). False for OpenRouter itself, which uses the
        ///   nested object.
        /// </summary>
        public static bool UsesEffortString(ProviderKind kind)
        {
            return kind == ProviderKind.OpenAiCompatible || kind == ProviderKind.NanoGpt;
        }
    }
}
