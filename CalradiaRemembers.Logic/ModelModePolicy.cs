// Code written by Gabriel Mailhot, 01/07/2026.
// LLM model modes: the player configures a roleplay model (the everyday one) and, optionally, models tuned for
// two kinds of adult scene: consensual-intimate scenes between willing partners, and CNC / captive scenes
// (captor, captive, companion-victim). A scene opens on the model matching its content kind; ordinary
// conversations open on the roleplay model; an in-chat button lets the player flip the mode mid-conversation
// (e.g. a consensual scene that escalates). Both explicit modes share the one "explicit" reasoning effort and
// provider pin for now (a later increment may split them). Pure and unit-tested so the selection and fallback
// rules stay protected; the MCM fields, the button, and the live resolution are host glue.

namespace CalradiaRemembers.Logic
{
    /// <summary>Which of the configured LLM models a request should use.</summary>
    public enum ModelMode
    {
        /// <summary>The everyday model: ordinary conversations, letters, background summaries.</summary>
        Roleplay,

        /// <summary>The consensual-intimate model: adult scenes between willing partners.</summary>
        ConsensualIntimate,

        /// <summary>The CNC / captive model: captor, captive, and companion-victim scenes.</summary>
        Cnc
    }

    /// <summary>The kind of scene that is opening, before the adult-content dial is consulted.</summary>
    public enum SceneContentKind
    {
        /// <summary>An ordinary, non-explicit scene.</summary>
        Ordinary,

        /// <summary>An intimate scene between willing partners.</summary>
        ConsensualIntimate,

        /// <summary>A captive / captor / companion-victim scene (consent-non-consent framing).</summary>
        Cnc
    }

    /// <summary>How a chat reply is composed. Declared here so callers share one vocabulary.</summary>
    public enum ChatCompositionMode
    {
        /// <summary>One LLM call produces prose and structured tags together (today's behaviour).</summary>
        Integrated,

        /// <summary>A prose call followed by a separate extraction call (a future increment consumes this).</summary>
        ProseExtract
    }

    /// <summary>Decides which model mode a scene starts in and which model id a mode resolves to.</summary>
    public static class ModelModePolicy
    {
        /// <summary>
        ///   The mode a conversation starts in, with the adult-content setting honoured (adult-prompt audit M10).
        ///   The "never switch at Off" guard lived only in the UI, which decided whether the toggle BUTTON was
        ///   visible: every automatic flip bypassed it, so a player who had turned adult content off could still
        ///   be moved silently onto a specialised explicit model, changing their cost and their censorship with
        ///   no button in sight to change it back. The content dial is the authority: adult off always forces
        ///   Roleplay. With adult on, a CNC scene opens on <see cref="ModelMode.Cnc" />, a consensual-intimate
        ///   scene on <see cref="ModelMode.ConsensualIntimate" />, and everything else on Roleplay.
        /// </summary>
        /// <param name="kind">What kind of scene is opening (see the intent rules, not merely "captive").</param>
        /// <param name="adultContentEnabled">The player's adult-content setting is anything other than Off.</param>
        public static ModelMode DefaultForScene(SceneContentKind kind, bool adultContentEnabled)
        {
            if (!adultContentEnabled) return ModelMode.Roleplay;
            if (kind == SceneContentKind.Cnc) return ModelMode.Cnc;
            if (kind == SceneContentKind.ConsensualIntimate) return ModelMode.ConsensualIntimate;

            return ModelMode.Roleplay;
        }

        /// <summary>
        ///   The model id a mode resolves to: the consensual-intimate model for
        ///   <see cref="ModelMode.ConsensualIntimate" />, the CNC model for <see cref="ModelMode.Cnc" />, each
        ///   only when the player actually configured one (blank or whitespace counts as not configured) and
        ///   otherwise falling back to the roleplay model; the roleplay model for <see cref="ModelMode.Roleplay" />.
        /// </summary>
        public static string ResolveModelId(ModelMode mode, string roleplayModel, string consensualModel, string cncModel)
        {
            switch (mode)
            {
                case ModelMode.ConsensualIntimate:
                    return string.IsNullOrWhiteSpace(consensualModel) ? roleplayModel : consensualModel.Trim();
                case ModelMode.Cnc:
                    return string.IsNullOrWhiteSpace(cncModel) ? roleplayModel : cncModel.Trim();
                default:
                    return roleplayModel;
            }
        }

        /// <summary>
        ///   True when the in-chat mode toggle button should be shown: adult content must be on, and the flip
        ///   must actually CHANGE something. Either an explicit model or an explicit provider pin is enough, and
        ///   the pin alone is a real case: a player may want the same model for everything and merely route
        ///   explicit scenes to an uncensored provider (providers of the same model charge different rates, and
        ///   some moderate their own output). Gating on the model alone would have hidden the button from exactly
        ///   that player, leaving them no way to flip a conversation that turns explicit on its own.
        /// </summary>
        public static bool ToggleVisible(bool anyExplicitModelConfigured, bool anyExplicitProviderPinConfigured, bool adultContentOn)
        {
            return (anyExplicitModelConfigured || anyExplicitProviderPinConfigured) && adultContentOn;
        }

        /// <summary>
        ///   The sentinel dropdown value meaning an explicit mode reuses the roleplay reasoning effort.
        ///   Shared between the MCM dropdown and the resolution rule so they can never drift.
        /// </summary>
        public const string SameAsRoleplay = "Same as roleplay";

        /// <summary>
        ///   The reasoning-effort keyword a mode resolves to. Roleplay keeps its own effort; both explicit modes
        ///   (consensual-intimate and CNC) SHARE the one "explicit" effort for now: adult scenes typically run
        ///   LOW or OFF (fewer moralizing refusals, faster scene beats) while roleplay may want more thinking for
        ///   finer dialogue. The explicit value falls back to the roleplay one when it is blank or the
        ///   "Same as roleplay" sentinel (the default, so existing configs are unchanged).
        /// </summary>
        public static string ResolveReasoningEffort(ModelMode mode, string roleplayEffort, string explicitEffort)
        {
            if (mode == ModelMode.Roleplay) return roleplayEffort;
            if (string.IsNullOrWhiteSpace(explicitEffort)) return roleplayEffort;
            if (string.Equals(explicitEffort.Trim(), SameAsRoleplay, System.StringComparison.OrdinalIgnoreCase)) return roleplayEffort;

            return explicitEffort.Trim();
        }

        /// <summary>
        ///   The OpenRouter provider pin a mode resolves to. Providers charge different rates, so a player
        ///   typically wants the cheapest routing for ordinary roleplay (a `:floor` model id with NO pin at all,
        ///   leaving OpenRouter free to pick) and a specific uncensored provider pinned for explicit scenes. Both
        ///   explicit modes SHARE the one "explicit" pin for now. Mirrors <see cref="ResolveReasoningEffort" />,
        ///   but this field is free text (not a dropdown), so blank already means "same as roleplay" and there is
        ///   no sentinel to check. A blank explicit pin falls back to the roleplay pin so a player who already set
        ///   one pin keeps it on both modes, and existing single-pin configs are unchanged.
        /// </summary>
        public static string ResolveProviderPin(ModelMode mode, string roleplayPin, string explicitPin)
        {
            if (mode == ModelMode.Roleplay) return roleplayPin;
            if (string.IsNullOrWhiteSpace(explicitPin)) return roleplayPin;

            return explicitPin.Trim();
        }
    }
}
