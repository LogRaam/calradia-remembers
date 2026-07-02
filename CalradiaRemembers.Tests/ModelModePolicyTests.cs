// Code written by Gabriel Mailhot, 01/07/2026.
// Documents the dual-model selection rules: explicit scenes open on the NSFW model and ordinary conversations on
// the roleplay model; a blank (or whitespace-only) NSFW model always falls back to the roleplay model; and the
// in-chat toggle button shows only when an NSFW model is configured AND adult content is enabled.

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
        [Test]
        public void GIVEN_an_ordinary_conversation_WHEN_choosing_the_default_mode_THEN_the_roleplay_model_is_used()
        {
            ModelModePolicy.DefaultForScene(isExplicitScene: false).Should().Be(ModelMode.Roleplay);
        }

        [Test]
        public void GIVEN_an_explicit_scene_WHEN_choosing_the_default_mode_THEN_the_nsfw_model_is_used()
        {
            ModelModePolicy.DefaultForScene(isExplicitScene: true).Should().Be(ModelMode.Nsfw);
        }

        [Test]
        public void GIVEN_nsfw_mode_with_a_configured_nsfw_model_WHEN_resolving_THEN_the_nsfw_model_wins()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "thedrummer/anubis-70b")
                .Should().Be("thedrummer/anubis-70b");
        }

        [Test]
        public void GIVEN_nsfw_mode_with_a_padded_nsfw_model_WHEN_resolving_THEN_the_id_is_trimmed()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "  thedrummer/anubis-70b  ")
                .Should().Be("thedrummer/anubis-70b");
        }

        [Test]
        public void GIVEN_nsfw_mode_but_a_blank_nsfw_model_WHEN_resolving_THEN_it_falls_back_to_the_roleplay_model()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "").Should().Be("x-ai/grok-4.20");
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", null).Should().Be("x-ai/grok-4.20");
        }

        [Test]
        public void GIVEN_nsfw_mode_but_a_whitespace_only_nsfw_model_WHEN_resolving_THEN_it_counts_as_blank()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "   ").Should().Be("x-ai/grok-4.20");
        }

        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_THEN_the_roleplay_model_is_used_even_if_an_nsfw_model_exists()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Roleplay, "x-ai/grok-4.20", "thedrummer/anubis-70b")
                .Should().Be("x-ai/grok-4.20");
        }

        [Test]
        public void GIVEN_an_nsfw_model_and_adult_content_on_WHEN_gating_the_button_THEN_the_toggle_is_visible()
        {
            ModelModePolicy.ToggleVisible(nsfwModelConfigured: true, adultContentOn: true).Should().BeTrue();
        }

        [Test]
        public void GIVEN_no_nsfw_model_WHEN_gating_the_button_THEN_the_toggle_is_hidden()
        {
            ModelModePolicy.ToggleVisible(nsfwModelConfigured: false, adultContentOn: true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_adult_content_off_WHEN_gating_the_button_THEN_the_toggle_is_hidden()
        {
            ModelModePolicy.ToggleVisible(nsfwModelConfigured: true, adultContentOn: false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_the_reasoning_effort_THEN_the_roleplay_effort_is_used_whatever_the_nsfw_setting()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Roleplay, "High", "Off").Should().Be("High");
        }

        [Test]
        public void GIVEN_nsfw_mode_with_its_own_effort_WHEN_resolving_THEN_the_nsfw_effort_wins()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Nsfw, "High", "Off").Should().Be("Off");
        }

        [Test]
        public void GIVEN_nsfw_mode_left_on_the_same_as_roleplay_sentinel_WHEN_resolving_THEN_the_roleplay_effort_is_reused()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Nsfw, "Medium", ModelModePolicy.SameAsRoleplay).Should().Be("Medium");
        }

        [Test]
        public void GIVEN_nsfw_mode_with_a_blank_effort_WHEN_resolving_THEN_the_roleplay_effort_is_reused()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Nsfw, "Low", "  ").Should().Be("Low");
        }
    }
}
