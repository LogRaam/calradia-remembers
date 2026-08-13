// Code written by Gabriel Mailhot, 01/07/2026.
// Documents the three-way model selection rules: a scene opens on the model matching its content kind
// (ordinary -> roleplay, consensual-intimate -> the consensual model, CNC/captive -> the CNC model); a blank
// (or whitespace-only) explicit model always falls back to the roleplay model; the two explicit modes SHARE the
// one "explicit" reasoning effort and provider pin for now; and the in-chat toggle button shows only when SOME
// explicit model or pin is configured AND adult content is enabled.
//
// WHY IT MATTERS: the explicit modes exist because a lower reasoning effort measurably cuts moralizing refusals
// on adult fiction (ModelModePolicy.ResolveReasoningEffort). Get DefaultForScene or ResolveModelId wrong and an
// explicit scene opens on the roleplay model instead (stock refusals), or an ordinary chat silently burns an
// explicit model for nothing. A wrong fallback (blank/unconfigured explicit model not caught) sends an empty
// model id straight to the API. Migration: the consensual and CNC slots start blank and, via the ModSettings
// facade, fall back to the existing NsfwModel, so a config that only set the old NSFW model behaves exactly as
// before for both explicit modes.

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
        // An ordinary chat must not silently spend an explicit model (a separate, differently-tuned
        // configuration) on content that never needed it.
        [Test]
        public void GIVEN_an_ordinary_scene_WHEN_choosing_the_default_mode_THEN_the_roleplay_mode_is_used()
        {
            ModelModePolicy.DefaultForScene(SceneContentKind.Ordinary, adultContentEnabled: true)
                           .Should().Be(ModelMode.Roleplay);
        }

        // A consensual-intimate scene must land on its own model so the writing is tuned for willing partners,
        // distinct from the CNC/captive register.
        [Test]
        public void GIVEN_a_consensual_intimate_scene_WHEN_choosing_the_default_mode_THEN_the_consensual_mode_is_used()
        {
            ModelModePolicy.DefaultForScene(SceneContentKind.ConsensualIntimate, adultContentEnabled: true)
                           .Should().Be(ModelMode.ConsensualIntimate);
        }

        // Captive, captor, and companion-victim scenes carry a consent-non-consent framing and must reach the
        // CNC model tuned not to moralize-refuse; if this flips, they open on the roleplay model and hit its
        // stock refusals instead.
        [Test]
        public void GIVEN_a_cnc_scene_WHEN_choosing_the_default_mode_THEN_the_cnc_mode_is_used()
        {
            ModelModePolicy.DefaultForScene(SceneContentKind.Cnc, adultContentEnabled: true)
                           .Should().Be(ModelMode.Cnc);
        }

        // Adult-prompt audit M10: the "never switch at Off" guard lived only in the UI, which decided whether the
        // toggle BUTTON was visible. Every automatic flip bypassed it, so a player who had turned adult content
        // off could still be moved silently onto a specialised explicit model, changing their cost and their
        // censorship with no visible button to change it back. The content dial is the authority, for BOTH
        // explicit kinds.
        [Test]
        public void GIVEN_adult_content_is_off_WHEN_a_cnc_scene_opens_THEN_the_mode_stays_roleplay()
        {
            ModelModePolicy.DefaultForScene(SceneContentKind.Cnc, adultContentEnabled: false)
                           .Should().Be(ModelMode.Roleplay);
        }

        // The same guard must hold for a consensual-intimate scene, or the adult-off promise only half-works.
        [Test]
        public void GIVEN_adult_content_is_off_WHEN_a_consensual_scene_opens_THEN_the_mode_stays_roleplay()
        {
            ModelModePolicy.DefaultForScene(SceneContentKind.ConsensualIntimate, adultContentEnabled: false)
                           .Should().Be(ModelMode.Roleplay);
        }

        // The whole point of configuring the consensual model: once its mode is active it must actually be used,
        // not silently ignored in favour of the roleplay model.
        [Test]
        public void GIVEN_consensual_mode_with_a_configured_consensual_model_WHEN_resolving_THEN_the_consensual_model_wins()
        {
            ModelModePolicy.ResolveModelId(ModelMode.ConsensualIntimate, "x-ai/grok-4.20", "sao10k/euryale", "thedrummer/anubis-70b")
                .Should().Be("sao10k/euryale");
        }

        // Likewise the CNC mode must pick its OWN slot, never the consensual one: the two explicit models can be
        // tuned differently and picking the wrong slot sends a scene to a model the player never meant for it.
        [Test]
        public void GIVEN_cnc_mode_with_a_configured_cnc_model_WHEN_resolving_THEN_the_cnc_model_wins()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Cnc, "x-ai/grok-4.20", "sao10k/euryale", "thedrummer/anubis-70b")
                .Should().Be("thedrummer/anubis-70b");
        }

        // An MCM textbox is easy to copy-paste with leading/trailing spaces; an untrimmed id sent to the API can
        // be rejected as an unknown model. Both explicit slots must trim.
        [Test]
        public void GIVEN_padded_explicit_models_WHEN_resolving_THEN_the_ids_are_trimmed()
        {
            ModelModePolicy.ResolveModelId(ModelMode.ConsensualIntimate, "x-ai/grok-4.20", "  sao10k/euryale  ", "")
                .Should().Be("sao10k/euryale");
            ModelModePolicy.ResolveModelId(ModelMode.Cnc, "x-ai/grok-4.20", "", "  thedrummer/anubis-70b  ")
                .Should().Be("thedrummer/anubis-70b");
        }

        // A player who is in an explicit mode but never configured that slot must not end up sending an empty
        // model id to the API; the fallback to the roleplay model keeps the request valid. (This is also the
        // migration seam: the facade back-fills a blank consensual/CNC slot from the old NSFW model upstream, so
        // the ONLY way this fires is a genuinely blank effective slot.)
        [Test]
        public void GIVEN_an_explicit_mode_but_a_blank_slot_WHEN_resolving_THEN_it_falls_back_to_the_roleplay_model()
        {
            ModelModePolicy.ResolveModelId(ModelMode.ConsensualIntimate, "x-ai/grok-4.20", "", "cnc").Should().Be("x-ai/grok-4.20");
            ModelModePolicy.ResolveModelId(ModelMode.ConsensualIntimate, "x-ai/grok-4.20", null, "cnc").Should().Be("x-ai/grok-4.20");
            ModelModePolicy.ResolveModelId(ModelMode.Cnc, "x-ai/grok-4.20", "consensual", "").Should().Be("x-ai/grok-4.20");
            ModelModePolicy.ResolveModelId(ModelMode.Cnc, "x-ai/grok-4.20", "consensual", null).Should().Be("x-ai/grok-4.20");
        }

        // A field left as spaces (not truly empty) must be treated the same as unconfigured, or the blank-model
        // fallback only half-works and an all-whitespace id still reaches the API.
        [Test]
        public void GIVEN_an_explicit_mode_but_a_whitespace_only_slot_WHEN_resolving_THEN_it_counts_as_blank()
        {
            ModelModePolicy.ResolveModelId(ModelMode.ConsensualIntimate, "x-ai/grok-4.20", "   ", "cnc").Should().Be("x-ai/grok-4.20");
            ModelModePolicy.ResolveModelId(ModelMode.Cnc, "x-ai/grok-4.20", "consensual", "   ").Should().Be("x-ai/grok-4.20");
        }

        // Having explicit models configured must not leak into ordinary conversations; only the active mode
        // decides, never mere availability of a second or third model.
        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_THEN_the_roleplay_model_is_used_even_if_explicit_models_exist()
        {
            ModelModePolicy.ResolveModelId(ModelMode.Roleplay, "x-ai/grok-4.20", "sao10k/euryale", "thedrummer/anubis-70b")
                .Should().Be("x-ai/grok-4.20");
        }

        // The player needs the button precisely when there is a real explicit model to switch to and adult
        // content is actually allowed.
        [Test]
        public void GIVEN_an_explicit_model_and_adult_content_on_WHEN_gating_the_button_THEN_the_toggle_is_visible()
        {
            ModelModePolicy.ToggleVisible(anyExplicitModelConfigured: true, anyExplicitProviderPinConfigured: false, adultContentOn: true)
                .Should().BeTrue();
        }

        // A pin with no explicit model is a REAL configuration, not an oversight: the player keeps one model for
        // everything and merely routes explicit scenes to an uncensored provider (the same model costs different
        // rates at different providers, and some moderate their own output). Gating the button on the MODEL alone
        // would hide it from exactly that player, leaving them no way to flip a conversation that turns explicit
        // on its own, which is the one moment the toggle exists for.
        [Test]
        public void GIVEN_only_an_explicit_provider_pin_WHEN_gating_the_button_THEN_the_toggle_is_still_visible()
        {
            ModelModePolicy.ToggleVisible(anyExplicitModelConfigured: false, anyExplicitProviderPinConfigured: true, adultContentOn: true)
                .Should().BeTrue();
        }

        // Showing a mode toggle with nothing to toggle to would just confuse the player: with neither a model nor
        // a pin, both ResolveModelId and ResolveProviderPin resolve back to the roleplay values either way, so the
        // button would be a lie.
        [Test]
        public void GIVEN_neither_an_explicit_model_nor_a_pin_WHEN_gating_the_button_THEN_the_toggle_is_hidden()
        {
            ModelModePolicy.ToggleVisible(anyExplicitModelConfigured: false, anyExplicitProviderPinConfigured: false, adultContentOn: true)
                .Should().BeFalse();
        }

        // The toggle must not offer an explicit mode when adult content itself is disabled at the mod level. This
        // outranks any explicit setting: a configured model or pin does not reopen a door the player closed.
        [Test]
        public void GIVEN_adult_content_off_WHEN_gating_the_button_THEN_the_toggle_is_hidden()
        {
            ModelModePolicy.ToggleVisible(anyExplicitModelConfigured: true, anyExplicitProviderPinConfigured: true, adultContentOn: false)
                .Should().BeFalse();
        }

        // Roleplay conversations must keep their own reasoning effort regardless of whatever the player set for
        // explicit scenes, or tuning the explicit effort would silently change ordinary chat too.
        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_the_reasoning_effort_THEN_the_roleplay_effort_is_used_whatever_the_explicit_setting()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Roleplay, "High", "Off").Should().Be("High");
        }

        // The actual lever behind the reported effect: a lower reasoning effort measurably cuts moralizing
        // refusals on adult fiction, so an explicit effort override must actually take hold, on BOTH explicit
        // modes (they share the one explicit effort for now).
        [Test]
        public void GIVEN_an_explicit_mode_with_its_own_effort_WHEN_resolving_THEN_the_explicit_effort_wins()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.ConsensualIntimate, "High", "Off").Should().Be("Off");
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Cnc, "High", "Off").Should().Be("Off");
        }

        // The sentinel is the default dropdown value, so an existing config that never touched this setting must
        // keep behaving exactly as before (roleplay effort everywhere), not silently pick some other value.
        [Test]
        public void GIVEN_an_explicit_mode_left_on_the_same_as_roleplay_sentinel_WHEN_resolving_THEN_the_roleplay_effort_is_reused()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.ConsensualIntimate, "Medium", ModelModePolicy.SameAsRoleplay).Should().Be("Medium");
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Cnc, "Medium", ModelModePolicy.SameAsRoleplay).Should().Be("Medium");
        }

        // A blank field (as opposed to the explicit sentinel) must fall back the same way, or an unset dropdown
        // could resolve to an empty reasoning-effort parameter sent to the API.
        [Test]
        public void GIVEN_an_explicit_mode_with_a_blank_effort_WHEN_resolving_THEN_the_roleplay_effort_is_reused()
        {
            ModelModePolicy.ResolveReasoningEffort(ModelMode.ConsensualIntimate, "Low", "  ").Should().Be("Low");
            ModelModePolicy.ResolveReasoningEffort(ModelMode.Cnc, "Low", "  ").Should().Be("Low");
        }

        // An explicit-scene provider must never be billed for ordinary chatter, which is the whole point of
        // pinning a cheap :floor route for roleplay and a specific uncensored provider only for explicit scenes.
        [Test]
        public void GIVEN_roleplay_mode_WHEN_resolving_the_provider_pin_THEN_the_roleplay_pin_is_used_even_if_an_explicit_pin_exists()
        {
            ModelModePolicy.ResolveProviderPin(ModelMode.Roleplay, "openai", "chutes/uncensored")
                .Should().Be("openai");
        }

        // SAVE-MIGRATION: every player who already pinned one provider must keep that exact behaviour on both
        // explicit modes after this ships; a blank field must not silently un-pin their explicit scenes and
        // reintroduce the mid-sentence censoring cut that pinning was built to fix.
        [Test]
        public void GIVEN_an_explicit_mode_but_a_blank_pin_WHEN_resolving_THEN_it_falls_back_to_the_roleplay_pin()
        {
            ModelModePolicy.ResolveProviderPin(ModelMode.ConsensualIntimate, "chutes/uncensored", "").Should().Be("chutes/uncensored");
            ModelModePolicy.ResolveProviderPin(ModelMode.ConsensualIntimate, "chutes/uncensored", null).Should().Be("chutes/uncensored");
            ModelModePolicy.ResolveProviderPin(ModelMode.Cnc, "chutes/uncensored", "   ").Should().Be("chutes/uncensored");
        }

        // The whole point of configuring an explicit pin: once an explicit mode is active it must actually route
        // to the specific uncensored provider, not silently fall back to the cheap roleplay routing.
        [Test]
        public void GIVEN_an_explicit_mode_with_a_configured_pin_WHEN_resolving_THEN_the_explicit_pin_wins_trimmed()
        {
            ModelModePolicy.ResolveProviderPin(ModelMode.ConsensualIntimate, "openai", "  chutes/uncensored  ")
                .Should().Be("chutes/uncensored");
            ModelModePolicy.ResolveProviderPin(ModelMode.Cnc, "openai", "  chutes/uncensored  ")
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
