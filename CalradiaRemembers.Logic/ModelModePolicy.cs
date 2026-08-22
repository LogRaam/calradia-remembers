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
//
// THREE-STATE IN-CHAT SWITCH (21/08/2026): the binary manual toggle (prose=Roleplay / prose=NSFW) is replaced by
// a THREE-STATE switch the player cycles in chat: Enhanced / Roleplay / NSFW (ManualModelMode). It is now the SOLE
// authority for both the main role AND the composition mode - content-kind-based auto-routing (Standard + a
// captivity scene forcing NSFW) is retired: Roleplay always writes on PROSE, even for a captivity scene, so a
// player who wants the NSFW model must choose it explicitly. MainRoleFor/CompositionFor are the two projections;
// Cycle defines the fixed order the button advances through.

using System;

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The kind of scene that is opening. The host computes and tracks it (<see cref="ServiceRole" /> selection
    ///   no longer reads it since the three-state in-chat switch replaced content-kind auto-routing); kept as a
    ///   diagnostic/log value describing what content is active.
    /// </summary>
    public enum SceneContentKind
    {
        /// <summary>An ordinary, non-explicit scene.</summary>
        Ordinary,

        /// <summary>An intimate scene between willing partners.</summary>
        ConsensualIntimate,

        /// <summary>A captive / captor / companion-victim scene.</summary>
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

    /// <summary>
    ///   The three-state in-chat model switch (21/08/2026, replaces the old binary manual toggle). The player
    ///   cycles through these with one button; each is a fixed (main role, composition) pair - see
    ///   <see cref="ModelModePolicy.MainRoleFor" />/<see cref="ModelModePolicy.CompositionFor" />.
    /// </summary>
    public enum ManualModelMode
    {
        /// <summary>PROSE writes, INTERPRET tags, NSFW is the hard-failure fallback. Two calls. The default.</summary>
        Enhanced,

        /// <summary>PROSE writes, even for a captivity scene (no NSFW routing). One call, no interpreter, no fallback.</summary>
        Roleplay,

        /// <summary>NSFW writes every scene. One call, no interpreter, no fallback.</summary>
        Nsfw
    }

    /// <summary>Decides which model a scene's main call uses, and which id/effort/pin each role resolves to.</summary>
    public static class ModelModePolicy
    {
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

        /// <summary>
        ///   The role the in-chat three-state switch selects: NSFW writes every scene on the NSFW model; Enhanced
        ///   and Roleplay both write on PROSE (Enhanced because the interpreter/fallback still reach NSFW only on
        ///   a hard failure; Roleplay because content-kind auto-routing is retired - a captivity scene stays on
        ///   PROSE unless the player explicitly picks NSFW).
        /// </summary>
        public static ServiceRole MainRoleFor(ManualModelMode mode) => mode == ManualModelMode.Nsfw ? ServiceRole.Nsfw : ServiceRole.Prose;

        /// <summary>
        ///   The composition the in-chat three-state switch selects: only Enhanced runs the full PROSE + INTERPRET
        ///   + hard-failure-fallback pipeline; Roleplay and NSFW both run the cheap single integrated call (their
        ///   main role already fixes which model writes it, so Standard's own content-routing never triggers).
        /// </summary>
        public static ChatCompositionMode CompositionFor(ManualModelMode mode) => mode == ManualModelMode.Enhanced ? ChatCompositionMode.Enhanced : ChatCompositionMode.Standard;

        /// <summary>The in-chat button label for a state ("Model: Enhanced" / "Model: Roleplay" / "Model: NSFW").</summary>
        public static string DisplayName(ManualModelMode mode)
            => mode == ManualModelMode.Enhanced ? "Enhanced"
             : mode == ManualModelMode.Roleplay ? "Roleplay"
             : "NSFW";

        /// <summary>The fixed order the in-chat button advances through, wrapping from NSFW back to Enhanced.</summary>
        public static ManualModelMode Cycle(ManualModelMode mode)
            => mode == ManualModelMode.Enhanced ? ManualModelMode.Roleplay
             : mode == ManualModelMode.Roleplay ? ManualModelMode.Nsfw
             : ManualModelMode.Enhanced;

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
