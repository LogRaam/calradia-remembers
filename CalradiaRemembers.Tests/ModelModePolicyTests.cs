// Code written by Gabriel Mailhot, 16/08/2026.
// Documents the Phase 2 model routing: three configured models (PROSE / NSFW / INTERPRET) and two composition
// modes (STANDARD = one integrated call routed by content, ENHANCED = PROSE writes + INTERPRET tags + NSFW
// hard-failure fallback). STANDARD sends a captivity (Cnc) scene to the NSFW model and everything else to PROSE;
// ENHANCED always writes on PROSE so a captivity scene keeps its variety (a soft-dodge is left as-is, since the
// NSFW model is reached only as a HARD-failure fallback, never as a content route).
//
// WHY IT MATTERS: get ActiveMainRole wrong and either an ordinary chat burns the explicit model, or a captivity
// scene in Standard opens on a tamer PROSE model that refuses. A wrong blank-fallback sends an empty model id to
// the API. UsesInterpreter/UsesHardFailureFallback decide whether the second (interpret) call and the fallback
// run at all - the whole cost difference between the two modes.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ModelModePolicyTests
    {
        // Standard's whole point: a captivity scene is where explicit content lives, so it must open on the NSFW
        // model. Route it to PROSE and the cheap mode loses its one reason to have an NSFW model at all.
        [Test]
        public void GIVEN_standard_mode_and_a_captivity_scene_WHEN_choosing_the_main_role_THEN_it_uses_nsfw()
        {
            ModelModePolicy.ActiveMainRole(ChatCompositionMode.Standard, SceneContentKind.Cnc)
                .Should().Be(ServiceRole.Nsfw);
        }

        // Consensual intimacy is "chat", not a captivity event (Gabriel's rule): in Standard it stays on the PROSE
        // model, so only true CNC/captive scenes spend the NSFW model.
        [Test]
        public void GIVEN_standard_mode_and_a_non_captivity_scene_WHEN_choosing_the_main_role_THEN_it_uses_prose()
        {
            ModelModePolicy.ActiveMainRole(ChatCompositionMode.Standard, SceneContentKind.ConsensualIntimate)
                .Should().Be(ServiceRole.Prose);
            ModelModePolicy.ActiveMainRole(ChatCompositionMode.Standard, SceneContentKind.Ordinary)
                .Should().Be(ServiceRole.Prose);
        }

        // Enhanced preserves the PROSE model's variety on EVERY scene, captivity included: the NSFW model is a
        // hard-failure fallback, never a content route. A captivity scene routed to NSFW here would erase the
        // soft-dodge diversity Gabriel wants (2026-08-16).
        [Test]
        public void GIVEN_enhanced_mode_WHEN_choosing_the_main_role_THEN_it_always_uses_prose_even_for_captivity()
        {
            ModelModePolicy.ActiveMainRole(ChatCompositionMode.Enhanced, SceneContentKind.Cnc)
                .Should().Be(ServiceRole.Prose);
            ModelModePolicy.ActiveMainRole(ChatCompositionMode.Enhanced, SceneContentKind.Ordinary)
                .Should().Be(ServiceRole.Prose);
        }

        // Each role resolves to its own configured id; the NSFW/Interpret slots fall back to the PROSE model when
        // left blank, so a single-model config still routes to a real id instead of an empty string sent to the API.
        [Test]
        public void GIVEN_configured_models_WHEN_resolving_a_role_THEN_each_role_yields_its_own_id()
        {
            ModelModePolicy.ResolveModelId(ServiceRole.Prose, "prose-x", "nsfw-y", "interp-z").Should().Be("prose-x");
            ModelModePolicy.ResolveModelId(ServiceRole.Nsfw, "prose-x", "nsfw-y", "interp-z").Should().Be("nsfw-y");
            ModelModePolicy.ResolveModelId(ServiceRole.Interpret, "prose-x", "nsfw-y", "interp-z").Should().Be("interp-z");
        }

        // A blank NSFW or Interpret model must fall back to PROSE, never send an empty id to the API.
        [Test]
        public void GIVEN_a_blank_explicit_or_interpret_model_WHEN_resolving_THEN_it_falls_back_to_prose()
        {
            ModelModePolicy.ResolveModelId(ServiceRole.Nsfw, "prose-x", "   ", null).Should().Be("prose-x");
            ModelModePolicy.ResolveModelId(ServiceRole.Interpret, "prose-x", "nsfw-y", "").Should().Be("prose-x");
        }

        // The cost difference between the two modes is exactly the interpret call and the fallback: Enhanced runs
        // both, Standard runs neither (its single integrated call self-tags and has no safety net).
        [Test]
        public void GIVEN_a_mode_WHEN_asked_about_interpreter_and_fallback_THEN_only_enhanced_uses_them()
        {
            ModelModePolicy.UsesInterpreter(ChatCompositionMode.Enhanced).Should().BeTrue();
            ModelModePolicy.UsesHardFailureFallback(ChatCompositionMode.Enhanced).Should().BeTrue();
            ModelModePolicy.UsesInterpreter(ChatCompositionMode.Standard).Should().BeFalse();
            ModelModePolicy.UsesHardFailureFallback(ChatCompositionMode.Standard).Should().BeFalse();
        }

        // PROSE keeps its own reasoning effort; NSFW and Interpret each carry their own, and a blank or the
        // "Same as prose" sentinel falls back to the PROSE effort so existing single-effort configs are unchanged.
        [Test]
        public void GIVEN_reasoning_efforts_WHEN_resolving_THEN_each_role_uses_its_own_or_falls_back_to_prose()
        {
            ModelModePolicy.ResolveReasoningEffort(ServiceRole.Prose, "high", "off", "low").Should().Be("high");
            ModelModePolicy.ResolveReasoningEffort(ServiceRole.Nsfw, "high", "off", "low").Should().Be("off");
            ModelModePolicy.ResolveReasoningEffort(ServiceRole.Interpret, "high", "off", "low").Should().Be("low");
            ModelModePolicy.ResolveReasoningEffort(ServiceRole.Nsfw, "high", "  ", "low").Should().Be("high");
            ModelModePolicy.ResolveReasoningEffort(ServiceRole.Nsfw, "high", ModelModePolicy.SameAsProse, "low").Should().Be("high");
        }

        // Provider pins mirror efforts: PROSE keeps its pin, the others carry their own, and blank means "same as
        // prose" so a player who set a single pin keeps it on every role.
        [Test]
        public void GIVEN_provider_pins_WHEN_resolving_THEN_each_role_uses_its_own_or_falls_back_to_prose()
        {
            ModelModePolicy.ResolveProviderPin(ServiceRole.Prose, "openai", "novita", "deepinfra").Should().Be("openai");
            ModelModePolicy.ResolveProviderPin(ServiceRole.Nsfw, "openai", "novita", "deepinfra").Should().Be("novita");
            ModelModePolicy.ResolveProviderPin(ServiceRole.Interpret, "openai", "novita", "deepinfra").Should().Be("deepinfra");
            ModelModePolicy.ResolveProviderPin(ServiceRole.Nsfw, "openai", "   ", "deepinfra").Should().Be("openai");
        }
    }
}
