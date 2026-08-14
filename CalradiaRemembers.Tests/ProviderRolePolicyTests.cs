// Code written by Gabriel Mailhot, 13/08/2026.
// Documents the per-role provider routing rules: each model role (Roleplay/Prose, ConsensualIntimate, Cnc, and
// the ActionInterpreter) may be sent to its OWN provider; a role left on "(default)" (or blank, or an
// unrecognised value) inherits the global provider; a concrete selection resolves to its own kind; and each
// ModelMode maps to the role whose provider a turn of that mode should use.
//
// WHY IT MATTERS: this is the one place the migration guarantee lives. Every player upgrading from the
// single-provider era has every role dropdown at "(default)"; if ResolveRoleKind ever returned anything but the
// global provider for that sentinel, an existing config would silently start routing a role to the wrong service
// (a dead endpoint, a key that does not match, a model id in the wrong namespace) with no setting changed. The
// concrete-selection and ParseKind-fallback rules protect the new capability itself: a typo or stale value must
// fall back safely rather than guess. RoleForMode ties the provider choice to the SAME dial that already picks
// the model id, so a turn's provider and model can never disagree about which scene kind is playing.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ProviderRolePolicyTests
    {
        // MIGRATION GUARANTEE: the whole point of the "(default)" sentinel is that an untouched role inherits the
        // global provider. If this flips, every upgraded single-provider config starts sending a role somewhere
        // the player never chose, with no dropdown changed to explain it.
        [Test]
        public void GIVEN_the_default_sentinel_WHEN_resolving_a_role_kind_THEN_it_inherits_the_global_provider()
        {
            ProviderRolePolicy.ResolveRoleKind(ProviderRolePolicy.DefaultSelection, ProviderKind.NanoGpt)
                              .Should().Be(ProviderKind.NanoGpt);
        }

        // A never-loaded MCM (null) or an empty stored value must behave exactly like "(default)": inherit, never
        // guess. Otherwise a config read before MCM populates the dropdowns could route to the wrong provider.
        [Test]
        public void GIVEN_a_blank_or_null_selection_WHEN_resolving_a_role_kind_THEN_it_inherits_the_global_provider()
        {
            ProviderRolePolicy.ResolveRoleKind("", ProviderKind.OpenAiCompatible).Should().Be(ProviderKind.OpenAiCompatible);
            ProviderRolePolicy.ResolveRoleKind("   ", ProviderKind.OpenAiCompatible).Should().Be(ProviderKind.OpenAiCompatible);
            ProviderRolePolicy.ResolveRoleKind(null, ProviderKind.OpenAiCompatible).Should().Be(ProviderKind.OpenAiCompatible);
        }

        // A value the policy does not recognise (a renamed option, a hand-edited config) must fall back to the
        // inherited provider, not throw and not silently pick one arbitrarily.
        [Test]
        public void GIVEN_an_unrecognised_selection_WHEN_resolving_a_role_kind_THEN_it_inherits_the_global_provider()
        {
            ProviderRolePolicy.ResolveRoleKind("Groq", ProviderKind.OpenRouter).Should().Be(ProviderKind.OpenRouter);
        }

        // The new capability itself: a role explicitly set to a concrete provider must actually resolve to THAT
        // provider, overriding the global default, or per-role routing does nothing.
        [Test]
        public void GIVEN_a_concrete_selection_WHEN_resolving_a_role_kind_THEN_it_uses_that_kind_over_the_default()
        {
            ProviderRolePolicy.ResolveRoleKind(ProviderRolePolicy.OpenRouterValue, ProviderKind.NanoGpt).Should().Be(ProviderKind.OpenRouter);
            ProviderRolePolicy.ResolveRoleKind(ProviderRolePolicy.NanoGptValue, ProviderKind.OpenRouter).Should().Be(ProviderKind.NanoGpt);
            ProviderRolePolicy.ResolveRoleKind(ProviderRolePolicy.OpenAiCompatibleValue, ProviderKind.OpenRouter).Should().Be(ProviderKind.OpenAiCompatible);
        }

        // The three concrete constants MUST equal the existing global Provider dropdown's option strings exactly,
        // or a role set to "the same provider" as the global one would parse to a different kind and route away
        // from where the player thinks it points. ParseKind is the shared parser both dropdowns rely on.
        [Test]
        public void GIVEN_each_concrete_provider_string_WHEN_parsing_THEN_it_maps_to_the_matching_kind()
        {
            ProviderRolePolicy.ParseKind("OpenRouter", ProviderKind.NanoGpt).Should().Be(ProviderKind.OpenRouter);
            ProviderRolePolicy.ParseKind("NanoGPT", ProviderKind.OpenRouter).Should().Be(ProviderKind.NanoGpt);
            ProviderRolePolicy.ParseKind("OpenAI-Compatible", ProviderKind.OpenRouter).Should().Be(ProviderKind.OpenAiCompatible);
        }

        // ParseKind treats "(default)" as UNRECOGNISED and returns the fallback: it is the low-level string->kind
        // mapper, unaware of the inherit sentinel (that meaning lives in ResolveRoleKind). If it ever mapped the
        // sentinel to a real kind, the global Provider dropdown, which uses ParseKind directly, could accept a
        // meaningless value.
        [Test]
        public void GIVEN_the_default_sentinel_WHEN_parsing_a_kind_THEN_it_returns_the_fallback()
        {
            ProviderRolePolicy.ParseKind(ProviderRolePolicy.DefaultSelection, ProviderKind.NanoGpt).Should().Be(ProviderKind.NanoGpt);
        }

        // ParseKind must fall back on blank/unknown input for the same reason ResolveRoleKind does: an unset or
        // stale value can never be allowed to send a request to a provider the player did not pick.
        [Test]
        public void GIVEN_blank_or_unknown_input_WHEN_parsing_a_kind_THEN_it_returns_the_fallback()
        {
            ProviderRolePolicy.ParseKind("", ProviderKind.OpenAiCompatible).Should().Be(ProviderKind.OpenAiCompatible);
            ProviderRolePolicy.ParseKind(null, ProviderKind.OpenAiCompatible).Should().Be(ProviderKind.OpenAiCompatible);
            ProviderRolePolicy.ParseKind("something-else", ProviderKind.OpenRouter).Should().Be(ProviderKind.OpenRouter);
        }

        // A turn's provider must be chosen from the SAME dial that picks its model id, or a consensual scene could
        // draw the consensual model while billing the roleplay provider (or vice-versa). Each explicit mode maps
        // to its own role; everything else (Roleplay) to the Roleplay role.
        [Test]
        public void GIVEN_each_model_mode_WHEN_mapping_to_a_service_role_THEN_it_matches_the_scene_kind()
        {
            ProviderRolePolicy.RoleForMode(ModelMode.Roleplay).Should().Be(ServiceRole.Roleplay);
            ProviderRolePolicy.RoleForMode(ModelMode.ConsensualIntimate).Should().Be(ServiceRole.ConsensualIntimate);
            ProviderRolePolicy.RoleForMode(ModelMode.Cnc).Should().Be(ServiceRole.Cnc);
        }
    }
}
