// Code written by Gabriel Mailhot, 29/08/2026.
// Documents which reasoning-effort wire shape each provider kind expects: OpenRouter's nested "reasoning" object,
// or the top-level "reasoning_effort" string. LlmServices reads this at three sites (the main chat client, the
// interpreter client, and the verbose diagnostic log) - a wrong answer here silently drifts one of those sites
// from the other two, or sends a provider a shape it ignores.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ReasoningWirePolicyTests
    {
        // OpenRouter is the one kind that reads the nested "reasoning" object; if this ever flips to true,
        // OpenRouter starts receiving a bare "reasoning_effort" string it does not parse, and the player's
        // Reasoning Effort dial silently stops doing anything.
        [Test]
        public void GIVEN_openrouter_WHEN_asking_the_wire_shape_THEN_it_does_not_use_the_effort_string()
        {
            ReasoningWirePolicy.UsesEffortString(ProviderKind.OpenRouter).Should().BeFalse();
        }

        // OpenAI-Compatible endpoints (Ollama et al.) ignore the OpenRouter object form and only honour the
        // top-level string; this is the existing behaviour the fix must not regress.
        [Test]
        public void GIVEN_openai_compatible_WHEN_asking_the_wire_shape_THEN_it_uses_the_effort_string()
        {
            ReasoningWirePolicy.UsesEffortString(ProviderKind.OpenAiCompatible).Should().BeTrue();
        }

        // The bug this task fixes: NanoGPT was sent no reasoning field at all, so a player on NanoGPT (e.g. GLM
        // models) could never move the Reasoning Effort dial off its provider default. NanoGPT's own API treats
        // the top-level reasoning_effort string as authoritative, so it must land in the string bucket, not be
        // silently dropped nor sent as the OpenRouter object it does not read.
        [Test]
        public void GIVEN_nanogpt_WHEN_asking_the_wire_shape_THEN_it_uses_the_effort_string()
        {
            ReasoningWirePolicy.UsesEffortString(ProviderKind.NanoGpt).Should().BeTrue();
        }
    }
}
