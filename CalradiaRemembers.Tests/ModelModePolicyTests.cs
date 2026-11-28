// Code written by Gabriel Mailhot, 16/08/2026.
// Documents the Phase 2 model routing: three configured models (PROSE / NSFW / INTERPRET) and two composition
// modes (STANDARD = one integrated call routed by content, ENHANCED = PROSE writes + INTERPRET tags + NSFW
// hard-failure fallback).
//
// WHY IT MATTERS: a wrong blank-fallback sends an empty model id to the API. UsesInterpreter/UsesHardFailureFallback
// decide whether the second (interpret) call and the fallback run at all - the whole cost difference between the
// two modes.
//
// THREE-STATE IN-CHAT SWITCH (21/08/2026): MainRoleFor/CompositionFor/Cycle back the Enhanced/Roleplay/NSFW switch
// that replaced the old binary manual toggle and its content-kind auto-routing (ActiveMainRole(mode, kind, force)
// is gone). Get MainRoleFor wrong and either Roleplay silently burns the explicit model the player did NOT pick, or
// NSFW silently writes on prose and the player's deliberate choice is ignored. Get Cycle wrong and the button gets
// the player stuck on two states, or skips one entirely.

#region

using CalradiaRemembers.Logic.Providers;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ModelModePolicyTests
    {
        // The whole point of picking NSFW explicitly: it must win, unconditionally - there is no scene kind left to
        // check (content-kind auto-routing is retired), so a wrong mapping here silently sends the player's
        // deliberate NSFW choice to the PROSE model instead.
        [Test]
        public void GIVEN_nsfw_state_WHEN_resolving_the_main_role_THEN_it_uses_nsfw()
        {
            ModelModePolicy.MainRoleFor(ManualModelMode.Nsfw).Should().Be(ServiceRole.Nsfw);
        }

        // Roleplay's whole point (2026-08-21 redesign): a captivity scene no longer auto-routes to NSFW just
        // because composition is Standard - the player must pick NSFW explicitly. If Roleplay resolved Nsfw here,
        // the "no NSFW routing" guarantee the mod author asked for would be silently broken.
        [Test]
        public void GIVEN_roleplay_state_WHEN_resolving_the_main_role_THEN_it_uses_prose()
        {
            ModelModePolicy.MainRoleFor(ManualModelMode.Roleplay).Should().Be(ServiceRole.Prose);
        }

        // Enhanced preserves the PROSE model's variety on every scene; the NSFW model is reached only as the
        // hard-failure fallback (UsesHardFailureFallback), never as the main call.
        [Test]
        public void GIVEN_enhanced_state_WHEN_resolving_the_main_role_THEN_it_uses_prose()
        {
            ModelModePolicy.MainRoleFor(ManualModelMode.Enhanced).Should().Be(ServiceRole.Prose);
        }

        // Only Enhanced pays for the interpreter + fallback pipeline; Roleplay and NSFW are both the cheap single
        // call, since their main role is already fixed by the switch itself - Standard's own content-routing
        // (now dead code path-wise) never gets a chance to fire.
        [Test]
        public void GIVEN_a_manual_state_WHEN_resolving_composition_THEN_only_enhanced_yields_enhanced_composition()
        {
            ModelModePolicy.CompositionFor(ManualModelMode.Enhanced).Should().Be(ChatCompositionMode.Enhanced);
            ModelModePolicy.CompositionFor(ManualModelMode.Roleplay).Should().Be(ChatCompositionMode.Standard);
            ModelModePolicy.CompositionFor(ManualModelMode.Nsfw).Should().Be(ChatCompositionMode.Standard);
        }

        // The in-chat button text must name the model actually in effect, not a generic "Mode" label (the same
        // M12 lesson that renamed the old binary toggle's text) - a mislabeled button reads as broken to the player.
        [Test]
        public void GIVEN_each_manual_state_WHEN_asking_its_display_name_THEN_it_names_the_model()
        {
            ModelModePolicy.DisplayName(ManualModelMode.Enhanced).Should().Be("Enhanced");
            ModelModePolicy.DisplayName(ManualModelMode.Roleplay).Should().Be("Roleplay");
            ModelModePolicy.DisplayName(ManualModelMode.Nsfw).Should().Be("NSFW");
        }

        // The button cycles one direction; if the wrap breaks, clicking past NSFW would either throw, freeze on
        // NSFW, or skip Roleplay entirely - any of which strands the player unable to reach Enhanced again.
        [Test]
        public void GIVEN_the_manual_state_WHEN_cycling_THEN_it_advances_enhanced_roleplay_nsfw_and_wraps()
        {
            ModelModePolicy.Cycle(ManualModelMode.Enhanced).Should().Be(ManualModelMode.Roleplay);
            ModelModePolicy.Cycle(ManualModelMode.Roleplay).Should().Be(ManualModelMode.Nsfw);
            ModelModePolicy.Cycle(ManualModelMode.Nsfw).Should().Be(ManualModelMode.Enhanced);
        }

        // Each role resolves to its own configured id; the NSFW/Interpret slots fall back to the PROSE model when
        // left blank, so a single-model config still routes to a real id instead of an empty string sent to the API.
        [Test]
        public void GIVEN_configured_models_WHEN_resolving_a_role_THEN_each_role_yields_its_own_id()
        {
            ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {Role = ServiceRole.Prose, Prose = "prose-x", Nsfw = "nsfw-y", Interpret = "interp-z"}).Should().Be("prose-x");
            ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {Role = ServiceRole.Nsfw, Prose = "prose-x", Nsfw = "nsfw-y", Interpret = "interp-z"}).Should().Be("nsfw-y");
            ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {Role = ServiceRole.Interpret, Prose = "prose-x", Nsfw = "nsfw-y", Interpret = "interp-z"}).Should().Be("interp-z");
        }

        // A blank NSFW or Interpret model must fall back to PROSE, never send an empty id to the API.
        [Test]
        public void GIVEN_a_blank_explicit_or_interpret_model_WHEN_resolving_THEN_it_falls_back_to_prose()
        {
            ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {Role = ServiceRole.Nsfw, Prose = "prose-x", Nsfw = "   ", Interpret = null}).Should().Be("prose-x");
            ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {Role = ServiceRole.Interpret, Prose = "prose-x", Nsfw = "nsfw-y", Interpret = ""}).Should().Be("prose-x");
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
            ModelModePolicy.ResolveReasoningEffort(new ModelModePolicy.RoleSettings {Role = ServiceRole.Prose, Prose = "high", Nsfw = "off", Interpret = "low"}).Should().Be("high");
            ModelModePolicy.ResolveReasoningEffort(new ModelModePolicy.RoleSettings {Role = ServiceRole.Nsfw, Prose = "high", Nsfw = "off", Interpret = "low"}).Should().Be("off");
            ModelModePolicy.ResolveReasoningEffort(new ModelModePolicy.RoleSettings {Role = ServiceRole.Interpret, Prose = "high", Nsfw = "off", Interpret = "low"}).Should().Be("low");
            ModelModePolicy.ResolveReasoningEffort(new ModelModePolicy.RoleSettings {Role = ServiceRole.Nsfw, Prose = "high", Nsfw = "  ", Interpret = "low"}).Should().Be("high");
            ModelModePolicy.ResolveReasoningEffort(new ModelModePolicy.RoleSettings {Role = ServiceRole.Nsfw, Prose = "high", Nsfw = ModelModePolicy.SameAsProse, Interpret = "low"}).Should().Be("high");
        }

        // Provider pins mirror efforts: PROSE keeps its pin, the others carry their own, and blank means "same as
        // prose" so a player who set a single pin keeps it on every role.
        [Test]
        public void GIVEN_provider_pins_WHEN_resolving_THEN_each_role_uses_its_own_or_falls_back_to_prose()
        {
            ModelModePolicy.ResolveProviderPin(new ModelModePolicy.RoleSettings {Role = ServiceRole.Prose, Prose = "openai", Nsfw = "novita", Interpret = "deepinfra"}).Should().Be("openai");
            ModelModePolicy.ResolveProviderPin(new ModelModePolicy.RoleSettings {Role = ServiceRole.Nsfw, Prose = "openai", Nsfw = "novita", Interpret = "deepinfra"}).Should().Be("novita");
            ModelModePolicy.ResolveProviderPin(new ModelModePolicy.RoleSettings {Role = ServiceRole.Interpret, Prose = "openai", Nsfw = "novita", Interpret = "deepinfra"}).Should().Be("deepinfra");
            ModelModePolicy.ResolveProviderPin(new ModelModePolicy.RoleSettings {Role = ServiceRole.Nsfw, Prose = "openai", Nsfw = "   ", Interpret = "deepinfra"}).Should().Be("openai");
        }

        // STAKE: Gabriel (28/11/2026): "pour le dramaturge, je pourrais vouloir DeepSeek, et Mimo en PROSE". Read through
        // the Interpret slot, the dramaturge would run the cheap interpreter model (which failed as a dramaturge in the
        // simulator, 5.40 against 6.61) with reasoning Off; blank must mean the PROSE model.
        [Test]
        public void GIVEN_the_dramaturge_role_WHEN_resolving_model_effort_and_pin_THEN_it_uses_its_own_or_the_prose_value_never_the_interpreters()
        {
            ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {Role = ServiceRole.Dramaturge, Prose = "mimo", Interpret = "flash", Dramaturge = "deepseek"}).Should().Be("deepseek");
            ModelModePolicy.ResolveModelId(new ModelModePolicy.RoleSettings {Role = ServiceRole.Dramaturge, Prose = "mimo", Interpret = "flash", Dramaturge = " "}).Should().Be("mimo");
            ModelModePolicy.ResolveReasoningEffort(new ModelModePolicy.RoleSettings {Role = ServiceRole.Dramaturge, Prose = "low", Interpret = "Off"}).Should().Be("low");
            ModelModePolicy.ResolveProviderPin(new ModelModePolicy.RoleSettings {Role = ServiceRole.Dramaturge, Prose = "openai", Interpret = "deepinfra"}).Should().Be("openai");
        }

        // STAKE: the in-chat switch keeps two positions, the MCM mode or Roleplay (Gabriel, 28/11/2026). With Dramaturge
        // chosen, the first position must say Dramaturge, or the player cannot tell the machinery is on.
        [Test]
        public void GIVEN_the_dramaturge_mode_WHEN_labelling_the_switch_THEN_only_the_enhanced_position_reads_dramaturge()
        {
            ModelModePolicy.DisplayName(ManualModelMode.Enhanced, true).Should().Be("Dramaturge");
            ModelModePolicy.DisplayName(ManualModelMode.Roleplay, true).Should().Be("Roleplay");
            ModelModePolicy.DisplayName(ManualModelMode.Nsfw, true).Should().Be("NSFW");
            ModelModePolicy.DisplayName(ManualModelMode.Enhanced, false).Should().Be("Enhanced");
        }
    }
}
