// Code written by Gabriel Mailhot, 11/09/2026.
// Player report (Nexus, 2026-09-11): "spent the last few hours pulling my hair out trying to set up an
// interpret model, you have the endpoints flippy floppied and criss crossed. the only way it works is if you
// reverse the endpoints, but then the models are just doing each others jobs."
//
// The endpoints are not crossed. What is crossed is which ROLES are protected from a trap the PROSE role has
// been protected from all along. Every model field ships holding the same OpenRouter id, and a model id only
// means anything inside its own provider's namespace: "x-ai/grok-4.20" is a real model on OpenRouter and a
// string a local LM Studio or Ollama server has never heard of. So an untouched field is not a CHOICE for a
// local server, it is the absence of one, and sending it produces a refusal from a machine the player knows
// is running perfectly.
//
// LlmServices.ResolveRoleplayModelForKind already knew this and applied it, in one place, to the prose field
// only. The interpret and NSFW fields sent their default verbatim. A player pointing the interpreter at his
// own server therefore watched the prose role behave and the interpreter role fail, and reasonably concluded
// the mod had wired the two the wrong way round.
//
// The rule now lives here, once, for all three.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Whether a configured model id is a real choice for the provider a role is pointed at, and what to send
    ///   when it is not. Pure, so the rule cannot be applied to one role and forgotten on another, which is
    ///   exactly how it came to be reported.
    /// </summary>
    public static class ProviderModelPolicy
    {
        /// <summary>
        ///   One model field as the player left it, and the provider the role carrying it now points at.
        /// </summary>
        public sealed class ModelChoice
        {
            /// <summary>The id in the MCM field, exactly as typed. Blank means the player cleared it.</summary>
            public string ConfiguredId { get; set; }

            /// <summary>The provider this ROLE resolves to, which may differ from every other role's.</summary>
            public ProviderKind Kind { get; set; }

            /// <summary>
            ///   The id the field ships holding. It is an OpenRouter id, so on any other provider finding it here
            ///   means the field was never touched rather than deliberately set to it.
            /// </summary>
            public string ShippedDefaultId { get; set; }
        }

        /// <summary>
        ///   True when the provider names its models in its own namespace, so an id meant for OpenRouter cannot
        ///   be assumed to mean anything to it. A local server and NanoGPT both do; OpenRouter is where the
        ///   shipped default came from in the first place.
        /// </summary>
        public static bool OwnsItsOwnModelNamespace(ProviderKind kind)
            => kind == ProviderKind.OpenAiCompatible || kind == ProviderKind.NanoGpt;

        /// <summary>
        ///   True when this field actually says something for this provider. False for a blank field, and false
        ///   for a field still holding the shipped OpenRouter default on a provider that names its own models:
        ///   a wrong id is worse than an admitted blank, because a blank lets the caller fall back to something
        ///   that works and lets the in-chat guidance explain itself.
        /// </summary>
        public static bool IsConfigured(ModelChoice choice)
        {
            if (choice == null || string.IsNullOrWhiteSpace(choice.ConfiguredId)) return false;

            return !OwnsItsOwnModelNamespace(choice.Kind)
                || !string.Equals(choice.ConfiguredId.Trim(), (choice.ShippedDefaultId ?? "").Trim(),
                                  System.StringComparison.Ordinal);
        }

        /// <summary>
        ///   The id to send: the trimmed field when it is a real choice for this provider, else EMPTY. Empty is
        ///   the signal every caller already understands as "fall back", so nothing new has to be taught to the
        ///   role-fallback rules that read this.
        /// </summary>
        public static string Resolve(ModelChoice choice)
            => IsConfigured(choice) ? choice.ConfiguredId.Trim() : "";
    }
}
