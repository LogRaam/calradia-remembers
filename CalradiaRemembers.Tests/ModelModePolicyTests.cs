// Code written by Gabriel Mailhot, 01/07/2026.
// Documents the dual-model selection rules: explicit scenes open on the NSFW model and ordinary conversations on
// the roleplay model; a blank (or whitespace-only) NSFW model always falls back to the roleplay model; and the
// in-chat toggle button shows only when an NSFW model is configured AND adult content is enabled.
//
// WHY IT MATTERS: NSFW mode exists because a lower reasoning effort measurably cuts moralizing refusals on
// consensual adult fiction (ModelModePolicy.ResolveReasoningEffort). Get DefaultForScene or ResolveModelId wrong
// and an explicit scene opens on the roleplay model instead (stock refusals), or an ordinary chat silently burns
// the NSFW model for nothing. A wrong fallback (blank/unconfigured NSFW model not caught) sends an empty model
// id straight to the API.

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
        // An ordinary chat must not silently spend the NSFW model (a separate, differently-tuned configuration)
        // on content that never needed it.
        [Test]
        public void GIVEN_an_ordinary_conversation_WHEN_choosing_the_default_mode_THEN_the_roleplay_model_is_used()
        {
            ModelModePolicy.DefaultForScene(isExplicitScene: false).Should().Be(ModelMode.Roleplay);
        }

        // Captive, player-as-captor, and companion-victim scenes default here specifically so they land on the
        // model tuned to not moralize-refuse consensual adult fiction; if this flips, explicit scenes open on
        // the roleplay model and hit its stock refusals instead.
        [Test]
        public void GIVEN_an_explicit_scene_WHEN_choosing_the_default_mode_THEN_the_nsfw_model_is_used()
        {
            ModelModePolicy.DefaultForScene(isExplicitScene: true).Should().Be(ModelMode.Nsfw);
        }

        // Adult-prompt audit M10: the "never switch at Off" guard lived only in the UI, which decided whether
        // the toggle BUTTON was visible. Every automatic flip bypassed it, so a player who had turned adult
        // content off could still be moved silently onto the specialised NSFW model, changing their cost and
        // their censorship with no visible button to change it back. The content dial is the authority.
        [Test]
        public void GIVEN_adult_content_is_off_WHEN_an_explicit_scene_opens_THEN_the_model_never_switches()
        {
            ModelModePolicy.DefaultForScene(isExplicitScene: true, adultContentEnabled: false)
                           .Should().Be(ModelMode.Roleplay);
        }

        // The same scene with the dial on must still reach the NSFW model, or the guard would simply disable
        // the feature for everyone.
        [Test]
        public void GIVEN_adult_content_is_on_WHEN_an_explicit_scene_opens_THEN_the_nsfw_model_is_chosen()
        {
            ModelModePolicy.DefaultForScene(isExplicitScene: true, adultContentEnabled: true)
                           .Should().Be(ModelMode.Nsfw);
        }

        // The whole point of configuring a second model: once NSFW mode is active it must actually be used,
        // not silently ignored in favour of the roleplay model.
        [Test]
        public void GIVEN_nsfw_mode_with_a_configured_nsfw_model_WHEN_resolving_THEN_the_nsfw_model_wins()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "thedrummer/anubis-70b")
                .Should().Be("thedrummer/anubis-70b");
        }

        // An MCM textbox is easy to copy-paste with leading/trailing spaces; an untrimmed id sent to the API
        // can be rejected as an unknown model.
        [Test]
        public void GIVEN_nsfw_mode_with_a_padded_nsfw_model_WHEN_resolving_THEN_the_id_is_trimmed()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "  thedrummer/anubis-70b  ")
                .Should().Be("thedrummer/anubis-70b");
        }

        // A player who enabled NSFW mode but never configured a second model must not end up sending an
        // empty model id to the API; the fallback keeps the request valid.
        [Test]
        public void GIVEN_nsfw_mode_but_a_blank_nsfw_model_WHEN_resolving_THEN_it_falls_back_to_the_roleplay_model()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "").Should().Be("x-ai/grok-4.20");
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", null).Should().Be("x-ai/grok-4.20");
        }

        // A field left as spaces (not truly empty) must be treated the same as unconfigured, or the blank-model
        // fallback above only half-works and an all-whitespace id still reaches the API.
        [Test]
        public void GIVEN_nsfw_mode_but_a_whitespace_only_nsfw_model_WHEN_resolving_THEN_it_counts_as_blank()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Nsfw, "x-ai/grok-4.20", "   ").Should().Be("x-ai/grok-4.20");
        }

        // Having an NSFW model configured must not leak into ordinary conversations; only the active mode
        // decides, never mere availability of a second model.
        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_THEN_the_roleplay_model_is_used_even_if_an_nsfw_model_exists()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Roleplay, "x-ai/grok-4.20", "thedrummer/anubis-70b")
                .Should().Be("x-ai/grok-4.20");
        }

        // The player needs the button precisely when there is a real second model to switch to and adult
        // content is actually allowed.
        [Test]
        public void GIVEN_an_nsfw_model_and_adult_content_on_WHEN_gating_the_button_THEN_the_toggle_is_visible()
        {
            ModelModePolicy.ToggleVisible(nsfwModelConfigured: true, nsfwProviderPinConfigured: false, adultContentOn: true)
                .Should().BeTrue();
        }

        // A pin with no second model is a REAL configuration, not an oversight: the player keeps one model for
        // everything and merely routes explicit scenes to an uncensored provider (the same model costs different
        // rates at different providers, and some of them moderate their own output). Gating the button on the
        // MODEL alone would hide it from exactly that player, leaving them no way to flip a conversation that
        // turns explicit on its own, which is the one moment the toggle exists for.
        [Test]
        public void GIVEN_only_an_nsfw_provider_pin_WHEN_gating_the_button_THEN_the_toggle_is_still_visible()
        {
            ModelModePolicy.ToggleVisible(nsfwModelConfigured: false, nsfwProviderPinConfigured: true, adultContentOn: true)
                .Should().BeTrue();
        }

        // Showing a mode toggle with nothing to toggle to would just confuse the player: with neither a model
        // nor a pin, both ResolveModelId and ResolveProviderPin resolve back to the roleplay values either way,
        // so the button would be a lie.
        [Test]
        public void GIVEN_neither_an_nsfw_model_nor_a_pin_WHEN_gating_the_button_THEN_the_toggle_is_hidden()
        {
            ModelModePolicy.ToggleVisible(nsfwModelConfigured: false, nsfwProviderPinConfigured: false, adultContentOn: true)
                .Should().BeFalse();
        }

        // The toggle must not offer an NSFW mode when adult content itself is disabled at the mod level. This
        // outranks any NSFW setting: a configured model or pin does not reopen a door the player closed.
        [Test]
        public void GIVEN_adult_content_off_WHEN_gating_the_button_THEN_the_toggle_is_hidden()
        {
            ModelModePolicy.ToggleVisible(nsfwModelConfigured: true, nsfwProviderPinConfigured: true, adultContentOn: false)
                .Should().BeFalse();
        }

        // Roleplay conversations must keep their own reasoning effort regardless of whatever the player set for
        // NSFW, or tuning the NSFW effort would silently change ordinary chat too.
        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_the_reasoning_effort_THEN_the_roleplay_effort_is_used_whatever_the_nsfw_setting()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Roleplay, "High", "Off").Should().Be("High");
        }

        // The actual lever behind the reported effect: a lower reasoning effort measurably cuts moralizing
        // refusals on consensual adult fiction, so an explicit NSFW effort override must actually take hold.
        [Test]
        public void GIVEN_nsfw_mode_with_its_own_effort_WHEN_resolving_THEN_the_nsfw_effort_wins()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Nsfw, "High", "Off").Should().Be("Off");
        }

        // The sentinel is the default dropdown value, so an existing config that never touched this setting
        // must keep behaving exactly as before (roleplay effort everywhere), not silently pick some other value.
        [Test]
        public void GIVEN_nsfw_mode_left_on_the_same_as_roleplay_sentinel_WHEN_resolving_THEN_the_roleplay_effort_is_reused()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Nsfw, "Medium", ModelModePolicy.SameAsRoleplay).Should().Be("Medium");
        }

        // A blank field (as opposed to the explicit sentinel) must fall back the same way, or an unset dropdown
        // could resolve to an empty reasoning-effort parameter sent to the API.
        [Test]
        public void GIVEN_nsfw_mode_with_a_blank_effort_WHEN_resolving_THEN_the_roleplay_effort_is_reused()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Nsfw, "Low", "  ").Should().Be("Low");
        }

        // An explicit-scene provider must never be billed for ordinary chatter, which is the whole point of
        // pinning a cheap :floor route for roleplay and a specific uncensored provider only for NSFW.
        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_the_provider_pin_THEN_the_roleplay_pin_is_used_even_if_an_nsfw_pin_exists()
        {
            ModelModePolicy.ResolveProviderPin(ModelMode.Roleplay, "openai", "chutes/uncensored")
                .Should().Be("openai");
        }

        // SAVE-MIGRATION: every player who already pinned one provider must keep that exact behaviour on both
        // modes after this ships; a blank field must not silently un-pin their explicit scenes and reintroduce
        // the mid-sentence censoring cut that pinning was built to fix.
        [Test]
        public void GIVEN_nsfw_mode_but_a_blank_nsfw_pin_WHEN_resolving_THEN_it_falls_back_to_the_roleplay_pin()
        {
            ModelModePolicy.ResolveProviderPin(ModelMode.Nsfw, "chutes/uncensored", "").Should().Be("chutes/uncensored");
            ModelModePolicy.ResolveProviderPin(ModelMode.Nsfw, "chutes/uncensored", null).Should().Be("chutes/uncensored");
            ModelModePolicy.ResolveProviderPin(ModelMode.Nsfw, "chutes/uncensored", "   ").Should().Be("chutes/uncensored");
        }

        // The whole point of configuring a second pin: once NSFW mode is active it must actually route to the
        // specific uncensored provider, not silently fall back to the cheap roleplay routing.
        [Test]
        public void GIVEN_nsfw_mode_with_a_configured_nsfw_pin_WHEN_resolving_THEN_the_nsfw_pin_wins_trimmed()
        {
            ModelModePolicy.ResolveProviderPin(ModelMode.Nsfw, "openai", "  chutes/uncensored  ")
                .Should().Be("chutes/uncensored");
        }

        // An empty pin sends NO provider block at all, which is what lets a :floor model id find the cheapest
        // provider; if the policy ever substituted something for blank, the player's cost optimisation would be
        // silently disabled.
        [Test]
        public void GIVEN_a_blank_roleplay_pin_WHEN_resolving_in_roleplay_mode_THEN_it_stays_blank()
        {
            ModelModePolicy.ResolveProviderPin(ModelMode.Roleplay, "", "chutes/uncensored").Should().Be("");
        }
    }
}
