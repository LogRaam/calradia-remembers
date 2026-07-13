// Code written by Gabriel Mailhot, 29/06/2026.
// Documents which target models switch the chat-completion request onto the OpenAI reasoning contract
// (max_completion_tokens, no temperature) and which keep the classic one (max_tokens + temperature).
// The flagship case is the reported bug: a player set the OpenAI-Compatible endpoint to gpt-5.4-mini
// pointing straight at api.openai.com and got HTTP 400 "max_tokens is not supported with this model".

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LlmParameterPolicyTests
    {
        // Reproduces the exact bug report: this model/endpoint pairing must resolve to the reasoning contract,
        // or the player is back to the HTTP 400 "max_tokens is not supported with this model" crash.
        [Test]
        public void GIVEN_the_reported_model_in_auto_WHEN_resolving_THEN_it_uses_the_reasoning_contract()
        {
            // gpt-5.4-mini straight at api.openai.com — the exact case from the bug report.
            LlmWireParameters p = LlmParameterPolicy.Resolve("gpt-5.4-mini", LlmCompatibilityMode.Auto);

            p.UseMaxCompletionTokens.Should().BeTrue();
            p.IncludeTemperature.Should().BeFalse();
        }

        // Pins the whole reasoning-family id list; drop any one of these from the detector and that model
        // silently keeps sending max_tokens/temperature, reproducing the same crash on a different id.
        [Test]
        public void GIVEN_various_reasoning_ids_in_auto_WHEN_detecting_THEN_each_is_recognized()
        {
            LlmParameterPolicy.IsOpenAiReasoningModel("gpt-5").Should().BeTrue();
            LlmParameterPolicy.IsOpenAiReasoningModel("gpt-5-mini").Should().BeTrue();
            LlmParameterPolicy.IsOpenAiReasoningModel("o1").Should().BeTrue();
            LlmParameterPolicy.IsOpenAiReasoningModel("o1-preview").Should().BeTrue();
            LlmParameterPolicy.IsOpenAiReasoningModel("o3-mini").Should().BeTrue();
            LlmParameterPolicy.IsOpenAiReasoningModel("o4-mini").Should().BeTrue();
            // A provider prefix is stripped before the id is examined.
            LlmParameterPolicy.IsOpenAiReasoningModel("openai/gpt-5.4-mini").Should().BeTrue();
        }

        // Guards against over-matching: gpt-4o must stay on the classic contract, or a too-broad "gpt-" style
        // rule would misfire on every OpenAI model, not just the actual reasoning families.
        [Test]
        public void GIVEN_a_non_reasoning_id_in_auto_WHEN_resolving_THEN_it_keeps_the_classic_contract()
        {
            // gpt-4o is NOT a reasoning model — it must keep max_tokens + temperature.
            LlmWireParameters p = LlmParameterPolicy.Resolve("gpt-4o", LlmCompatibilityMode.Auto);

            p.UseMaxCompletionTokens.Should().BeFalse();
            p.IncludeTemperature.Should().BeTrue();
        }

        // Pins the false-positive guards named in the policy's own docs (gpt-4o/gpt-4o-mini vs. gpt-5*, olmo/
        // openchat vs. the o-series): a naive substring match would misclassify these and send the wrong
        // parameter shape to models that never asked for it.
        [Test]
        public void GIVEN_ids_that_merely_look_similar_WHEN_detecting_THEN_they_are_not_reasoning_models()
        {
            LlmParameterPolicy.IsOpenAiReasoningModel("gpt-4o").Should().BeFalse();
            LlmParameterPolicy.IsOpenAiReasoningModel("gpt-4o-mini").Should().BeFalse();
            LlmParameterPolicy.IsOpenAiReasoningModel("openai/gpt-4-turbo").Should().BeFalse();
            LlmParameterPolicy.IsOpenAiReasoningModel("x-ai/grok-4.20").Should().BeFalse();
            LlmParameterPolicy.IsOpenAiReasoningModel("olmo-7b").Should().BeFalse();
            LlmParameterPolicy.IsOpenAiReasoningModel("openchat-3.5").Should().BeFalse();
        }

        // Fail-safe for a missing/unset model id (e.g. before the player finishes configuring the OpenAI-
        // Compatible endpoint): must not accidentally trip the reasoning contract on nothing.
        [Test]
        public void GIVEN_an_empty_or_null_id_WHEN_detecting_THEN_it_is_never_a_reasoning_model()
        {
            LlmParameterPolicy.IsOpenAiReasoningModel(null).Should().BeFalse();
            LlmParameterPolicy.IsOpenAiReasoningModel("").Should().BeFalse();
            LlmParameterPolicy.IsOpenAiReasoningModel("   ").Should().BeFalse();
        }

        // The manual override exists for an endpoint the heuristic gets wrong (e.g. a proxy in front of a
        // reasoning id that still accepts max_tokens); this pins that the override actually beats auto-detection.
        [Test]
        public void GIVEN_standard_mode_WHEN_resolving_a_reasoning_id_THEN_the_override_forces_the_classic_contract()
        {
            // The player can force the classic shape even on a reasoning id, for an endpoint that wants it.
            LlmWireParameters p = LlmParameterPolicy.Resolve("gpt-5.4-mini", LlmCompatibilityMode.Standard);

            p.UseMaxCompletionTokens.Should().BeFalse();
            p.IncludeTemperature.Should().BeTrue();
        }

        // The symmetric escape hatch: a strict endpoint whose model id the auto heuristic does not recognize
        // (a newer/unlisted reasoning model) would otherwise leave the player stuck until the id list is updated.
        [Test]
        public void GIVEN_reasoning_mode_WHEN_resolving_a_classic_id_THEN_the_override_forces_the_reasoning_contract()
        {
            // ...and force the reasoning shape on any id, for a strict endpoint the heuristic does not know.
            LlmWireParameters p = LlmParameterPolicy.Resolve("some-custom-model", LlmCompatibilityMode.Reasoning);

            p.UseMaxCompletionTokens.Should().BeTrue();
            p.IncludeTemperature.Should().BeFalse();
        }
    }
}
