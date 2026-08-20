// Code written by Gabriel Mailhot, 16/08/2026.
// Phase 2 model routing. The player configures THREE models: a PROSE model (the everyday composition model), an
// NSFW model (default grok, used for explicit captivity content and as the hard-failure fallback), and an
// INTERPRET model (default grok, the action-tag extractor). Two composition modes decide how a reply is built:
//   - STANDARD: one integrated call, content-routed - the PROSE model for ordinary chat and consensual scenes,
//     the NSFW model for captivity (CNC) events. No separate interpret call, no fallback. Cheapest / fastest.
//   - ENHANCED (default): the PROSE model writes every scene, the INTERPRET model tags it, and the NSFW model is
//     the fallback when the prose call fails HARD (truncation / empty / refusal). A soft-dodge (a complete but
//     tame beat) is left as-is on purpose, so the PROSE model's own variety is preserved.
// The old per-scene ModelMode (Roleplay/ConsensualIntimate/Cnc), the DefaultForScene routing and the manual
// in-chat toggle are gone: content routing is now the binary "is this a captivity scene", and only in Standard.
// Pure and unit-tested so selection and fallback rules stay protected; MCM fields and live resolution are host glue.

using System;

namespace CalradiaRemembers.Logic
{
    /// <summary>The kind of scene that is opening. The host computes it; only Cnc (captivity) routes specially.</summary>
    public enum SceneContentKind
    {
        /// <summary>An ordinary, non-explicit scene.</summary>
        Ordinary,

        /// <summary>An intimate scene between willing partners (treated as chat: PROSE model in Standard).</summary>
        ConsensualIntimate,

        /// <summary>A captive / captor / companion-victim scene (the only kind routed to NSFW in Standard).</summary>
        Cnc
    }

    /// <summary>How a chat reply is composed.</summary>
    public enum ChatCompositionMode
    {
        /// <summary>One integrated call, content-routed (PROSE for chat, NSFW for captivity). No interpret, no fallback.</summary>
        Standard,

        /// <summary>PROSE writes, INTERPRET tags, NSFW is the hard-failure fallback. The default.</summary>
        Enhanced
    }

    /// <summary>Decides which model a scene's main call uses, and which id/effort/pin each role resolves to.</summary>
    public static class ModelModePolicy
    {
        /// <summary>
        ///   The role the MAIN chat call uses this turn. When <paramref name="forceNsfwProse" /> is set (the player's
        ///   restored manual in-chat toggle, flipped to prose=NSFW), the main call ALWAYS uses the NSFW model, in
        ///   either composition mode - it wins over content routing so the player can deliberately steer a scene onto
        ///   the NSFW model. Otherwise: STANDARD routes by content (a captivity (Cnc) scene uses the NSFW model,
        ///   everything else uses the PROSE model); ENHANCED always writes on the PROSE model (the NSFW model is
        ///   reached only as the fallback, the INTERPRET model only through the separate interpreter call), so a
        ///   captivity scene still opens on PROSE and its variety - including a soft-dodge - is preserved.
        /// </summary>
        public static ServiceRole ActiveMainRole(ChatCompositionMode mode, SceneContentKind kind, bool forceNsfwProse)
            => forceNsfwProse ? ServiceRole.Nsfw
             : mode == ChatCompositionMode.Standard && kind == SceneContentKind.Cnc ? ServiceRole.Nsfw
             : ServiceRole.Prose;

        /// <summary>
        ///   The model id a role resolves to. Nsfw and Interpret fall back to the PROSE model when the player left
        ///   them blank (blank or whitespace counts as not configured), so a single-model config still works.
        /// </summary>
        public static string ResolveModelId(ServiceRole role, string proseModel, string nsfwModel, string interpretModel)
        {
            switch (role)
            {
                case ServiceRole.Nsfw:
                    return string.IsNullOrWhiteSpace(nsfwModel) ? proseModel : nsfwModel.Trim();
                case ServiceRole.Interpret:
                    return string.IsNullOrWhiteSpace(interpretModel) ? proseModel : interpretModel.Trim();
                default:
                    return proseModel;
            }
        }

        /// <summary>ENHANCED runs the separate interpreter call; STANDARD self-tags in its one integrated call.</summary>
        public static bool UsesInterpreter(ChatCompositionMode mode) => mode == ChatCompositionMode.Enhanced;

        /// <summary>ENHANCED redoes a hard-failed prose beat on the NSFW model; STANDARD's single call has no fallback.</summary>
        public static bool UsesHardFailureFallback(ChatCompositionMode mode) => mode == ChatCompositionMode.Enhanced;

        /// <summary>
        ///   The sentinel dropdown value meaning a role reuses the PROSE reasoning effort. Shared between the MCM
        ///   dropdown and the resolution rule so they can never drift.
        /// </summary>
        public const string SameAsProse = "Same as prose";

        /// <summary>
        ///   The reasoning-effort keyword a role resolves to. PROSE keeps its own effort; NSFW and INTERPRET each
        ///   have their own, falling back to the PROSE effort when blank or the "Same as prose" sentinel. Explicit
        ///   and interpreter calls typically run LOW/OFF (fewer refusals, faster tags) while prose may want more.
        /// </summary>
        public static string ResolveReasoningEffort(ServiceRole role, string proseEffort, string nsfwEffort, string interpretEffort)
        {
            string configured = role == ServiceRole.Nsfw ? nsfwEffort
                              : role == ServiceRole.Interpret ? interpretEffort
                              : proseEffort;
            if (role == ServiceRole.Prose) return proseEffort;
            if (string.IsNullOrWhiteSpace(configured)) return proseEffort;
            if (string.Equals(configured.Trim(), SameAsProse, StringComparison.OrdinalIgnoreCase)) return proseEffort;

            return configured.Trim();
        }

        /// <summary>
        ///   The OpenRouter provider pin a role resolves to. PROSE keeps its own pin; NSFW and INTERPRET each have
        ///   their own free-text pin, blank meaning "same as prose" (a player who set one pin keeps it everywhere).
        /// </summary>
        public static string ResolveProviderPin(ServiceRole role, string prosePin, string nsfwPin, string interpretPin)
        {
            string configured = role == ServiceRole.Nsfw ? nsfwPin
                              : role == ServiceRole.Interpret ? interpretPin
                              : prosePin;
            if (role == ServiceRole.Prose) return prosePin;
            if (string.IsNullOrWhiteSpace(configured)) return prosePin;

            return configured.Trim();
        }
    }
}
