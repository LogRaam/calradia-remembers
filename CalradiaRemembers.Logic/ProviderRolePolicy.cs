// Code written by Gabriel Mailhot, 13/08/2026.
// Per-role LLM provider routing. Until now every model role shared ONE provider (the global "Provider" dropdown)
// and only the model id varied per role. This lets the player point each role at a DIFFERENT provider, e.g. Prose
// on OpenRouter, the consensual model on NanoGPT, and the action interpreter on a LOCAL OpenAI-Compatible server.
// Pure and unit-tested so the selection and fallback rules stay protected; the MCM dropdowns and the live client
// wiring (LlmServices) are host glue.
//
// MIGRATION: every new per-role dropdown defaults to "(default)", which inherits the global provider. So a config
// upgraded from the single-provider era resolves every role to the same provider as before (a strict no-op).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The concrete OpenAI-compatible service a request is sent to.</summary>
    public enum ProviderKind
    {
        /// <summary>openrouter.ai (the default aggregator).</summary>
        OpenRouter,

        /// <summary>nano-gpt.com (a second aggregator).</summary>
        NanoGpt,

        /// <summary>A direct OpenAI-compatible endpoint (LM Studio, Ollama, api.openai.com, a proxy...).</summary>
        OpenAiCompatible
    }

    /// <summary>
    ///   A model ROLE the player can route to its own provider. These are all TEXT-generation categories.
    ///   TTS and image-generation are deliberately FUTURE categories: the enum is kept open for them, but they
    ///   are NOT added or wired now (this round is scoped to provider + model id per text role only).
    /// </summary>
    public enum ServiceRole
    {
        /// <summary>Ordinary conversations, letters, background summaries, and the prose call in Prose + Interpreter.</summary>
        Roleplay,

        /// <summary>Consensual-intimate adult scenes between willing partners.</summary>
        ConsensualIntimate,

        /// <summary>CNC / captive scenes (captor, captive, companion-victim).</summary>
        Cnc,

        /// <summary>The small, cheap model that reads structured actions out of prose in Prose + Interpreter mode.</summary>
        ActionInterpreter
    }

    /// <summary>Maps the per-role provider dropdown strings to a <see cref="ProviderKind" />, honouring the "(default)" inherit sentinel.</summary>
    public static class ProviderRolePolicy
    {
        /// <summary>The dropdown value meaning "use whatever the global Provider dropdown selects" (the default for every role, so migration is a no-op).</summary>
        public const string DefaultSelection = "(default)";

        /// <summary>OpenRouter dropdown value. MUST equal the existing global Provider dropdown's OpenRouter option exactly, so the two can never drift.</summary>
        public const string OpenRouterValue = "OpenRouter";

        /// <summary>NanoGPT dropdown value. MUST equal the existing global Provider dropdown's NanoGPT option exactly.</summary>
        public const string NanoGptValue = "NanoGPT";

        /// <summary>OpenAI-Compatible dropdown value. MUST equal the existing global Provider dropdown's OpenAI-Compatible option exactly.</summary>
        public const string OpenAiCompatibleValue = "OpenAI-Compatible";

        /// <summary>
        ///   Maps a concrete provider dropdown string to its <see cref="ProviderKind" />. A blank or unrecognised
        ///   value degrades to <paramref name="fallback" /> rather than guessing, so a stale or hand-edited config
        ///   can never send a request to an unintended provider.
        /// </summary>
        public static ProviderKind ParseKind(string selectedValue, ProviderKind fallback)
        {
            if (string.IsNullOrWhiteSpace(selectedValue)) return fallback;

            string v = selectedValue.Trim();

            if (string.Equals(v, OpenRouterValue, StringComparison.OrdinalIgnoreCase)) return ProviderKind.OpenRouter;
            if (string.Equals(v, NanoGptValue, StringComparison.OrdinalIgnoreCase)) return ProviderKind.NanoGpt;
            if (string.Equals(v, OpenAiCompatibleValue, StringComparison.OrdinalIgnoreCase)) return ProviderKind.OpenAiCompatible;

            return fallback;
        }

        /// <summary>
        ///   The provider kind a role resolves to. This is the heart of the migration guarantee: "(default)",
        ///   blank, or an unrecognised selection all inherit <paramref name="defaultKind" /> (the global provider),
        ///   so a role left untouched behaves exactly as it did before per-role routing existed. A concrete
        ///   selection resolves to its own kind.
        /// </summary>
        public static ProviderKind ResolveRoleKind(string roleSelection, ProviderKind defaultKind)
        {
            if (string.IsNullOrWhiteSpace(roleSelection)) return defaultKind;
            if (string.Equals(roleSelection.Trim(), DefaultSelection, StringComparison.OrdinalIgnoreCase)) return defaultKind;

            return ParseKind(roleSelection, defaultKind);
        }

        /// <summary>
        ///   The <see cref="ServiceRole" /> an active <see cref="ModelMode" /> belongs to, so a turn's provider is
        ///   chosen from the same dial that already picks its model id: an ordinary turn uses the Roleplay role, a
        ///   consensual scene the ConsensualIntimate role, a CNC scene the Cnc role.
        /// </summary>
        public static ServiceRole RoleForMode(ModelMode mode)
        {
            switch (mode)
            {
                case ModelMode.ConsensualIntimate: return ServiceRole.ConsensualIntimate;
                case ModelMode.Cnc: return ServiceRole.Cnc;
                default: return ServiceRole.Roleplay;
            }
        }
    }
}
