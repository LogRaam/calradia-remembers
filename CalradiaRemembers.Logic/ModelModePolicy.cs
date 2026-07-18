// Code written by Gabriel Mailhot, 01/07/2026.
// Dual LLM model modes: the player configures a roleplay model (the everyday one) and an optional NSFW model.
// Structurally-explicit scenes (captive encounters, player-as-captor scenes, companion-victim scenes) default to
// the NSFW model; ordinary conversations default to the roleplay model; an in-chat button lets the player flip
// the mode mid-conversation (e.g. a consensual scene that escalates). Pure and unit-tested so the selection and
// fallback rules stay protected; the MCM fields, the button, and the live resolution are host glue.

namespace CalradiaRemembers.Logic
{
    /// <summary>Which of the two configured LLM models a request should use.</summary>
    public enum ModelMode
    {
        /// <summary>The everyday model: ordinary conversations, letters, background summaries.</summary>
        Roleplay,

        /// <summary>The explicit-content model: captive, captor, and companion-victim scenes.</summary>
        Nsfw
    }

    /// <summary>Decides which model mode a scene starts in and which model id a mode resolves to.</summary>
    public static class ModelModePolicy
    {
        /// <summary>
        ///   The mode a conversation starts in: explicit scenes (captive encounter, player-as-captor,
        ///   companion-victim) open on the NSFW model, everything else on the roleplay model.
        /// </summary>
        public static ModelMode DefaultForScene(bool isExplicitScene)
        {
            return isExplicitScene ? ModelMode.Nsfw : ModelMode.Roleplay;
        }

        /// <summary>
        ///   The mode a scene starts in, with the adult-content setting honoured (adult-prompt audit M10).
        ///   The "never switch at Off" guard lived only in the UI, which decided whether the toggle BUTTON
        ///   was visible: every automatic flip bypassed it, so a player who had turned adult content off
        ///   could still be moved silently onto the specialised NSFW model, changing their cost and their
        ///   censorship with no button in sight to change it back. The content dial is the authority.
        /// </summary>
        /// <param name="isExplicitScene">This scene is a sexual one (see the intent rules, not merely "captive").</param>
        /// <param name="adultContentEnabled">The player's adult-content setting is anything other than Off.</param>
        public static ModelMode DefaultForScene(bool isExplicitScene, bool adultContentEnabled)
        {
            return isExplicitScene && adultContentEnabled ? ModelMode.Nsfw : ModelMode.Roleplay;
        }

        /// <summary>
        ///   The model id a mode resolves to: the NSFW model only when the mode asks for it AND the player
        ///   actually configured one (blank or whitespace counts as not configured); the roleplay model otherwise.
        /// </summary>
        public static string ResolveModelId(ModelMode mode, string roleplayModel, string nsfwModel)
        {
            if (mode == ModelMode.Nsfw && !string.IsNullOrWhiteSpace(nsfwModel))
            {
                return nsfwModel.Trim();
            }

            return roleplayModel;
        }

        /// <summary>
        ///   True when the in-chat mode toggle button should be shown: adult content must be on, and the flip
        ///   must actually CHANGE something. Either an NSFW model or an NSFW provider pin is enough, and the
        ///   pin alone is a real case: a player may want the same model for everything and merely route explicit
        ///   scenes to an uncensored provider (providers of the same model charge different rates, and some
        ///   moderate their own output). Gating on the model alone would have hidden the button from exactly
        ///   that player, leaving them no way to flip a conversation that turns explicit on its own.
        /// </summary>
        public static bool ToggleVisible(bool nsfwModelConfigured, bool nsfwProviderPinConfigured, bool adultContentOn)
        {
            return (nsfwModelConfigured || nsfwProviderPinConfigured) && adultContentOn;
        }

        /// <summary>
        ///   The sentinel dropdown value meaning the NSFW mode reuses the roleplay reasoning effort.
        ///   Shared between the MCM dropdown and the resolution rule so they can never drift.
        /// </summary>
        public const string SameAsRoleplay = "Same as roleplay";

        /// <summary>
        ///   The reasoning-effort keyword a mode resolves to. Each mode can want a different effort:
        ///   NSFW typically runs LOW or OFF (less moralizing refusals, faster scene beats) while roleplay
        ///   may want more thinking for finer dialogue. The NSFW value falls back to the roleplay one when
        ///   it is blank or the "Same as roleplay" sentinel (the default, so existing configs are unchanged).
        /// </summary>
        public static string ResolveReasoningEffort(ModelMode mode, string roleplayEffort, string nsfwEffort)
        {
            if (mode != ModelMode.Nsfw) return roleplayEffort;
            if (string.IsNullOrWhiteSpace(nsfwEffort)) return roleplayEffort;
            if (string.Equals(nsfwEffort.Trim(), SameAsRoleplay, System.StringComparison.OrdinalIgnoreCase)) return roleplayEffort;

            return nsfwEffort.Trim();
        }

        /// <summary>
        ///   The OpenRouter provider pin a mode resolves to. Providers charge different rates, so a player
        ///   typically wants the cheapest routing for ordinary roleplay (a `:floor` model id with NO pin at
        ///   all, leaving OpenRouter free to pick) and a specific uncensored provider pinned for explicit
        ///   scenes. Mirrors <see cref="ResolveReasoningEffort"/>, but this field is free text (not a
        ///   dropdown), so blank already means "same as roleplay" and there is no sentinel to check. A blank
        ///   NSFW pin falls back to the roleplay pin so a player who already set one pin keeps it on both
        ///   modes, and existing single-pin configs are unchanged.
        /// </summary>
        public static string ResolveProviderPin(ModelMode mode, string roleplayPin, string nsfwPin)
        {
            if (mode != ModelMode.Nsfw) return roleplayPin;
            if (string.IsNullOrWhiteSpace(nsfwPin)) return roleplayPin;

            return nsfwPin.Trim();
        }
    }
}
