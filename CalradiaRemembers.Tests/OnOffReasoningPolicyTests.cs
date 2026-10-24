// Code written by Gabriel Mailhot, 24/10/2026.

// What the reasoning setting sends to a model that only thinks or does not (Grok 4.20, probed 24/10/2026: minimal, low,
// high and a 300-token budget all thought 1,400 to 2,300 tokens; only off did not). Gabriel (24/10/2026): "off par
// défaut". If this breaks, every Grok player on the default Low pays about twice per reply and waits five times longer
// for thinking they never asked for; or a model WITH levels loses the little thinking Low asks of it.

#region

using CalradiaRemembers.Logic.Providers;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OnOffReasoningPolicyTests
    {
        // STAKE: the default setting (Low) on the default model (Grok 4.20) must not think: Grok cannot think "a little".
        [Test]
        public void GIVEN_grok_and_the_default_low_WHEN_resolved_THEN_it_does_not_think()
        {
            OnOffReasoningPolicy.ForModel("x-ai/grok-4.20", "Low").Should().Be("Off");
            OnOffReasoningPolicy.ForModel("x-ai/grok-4.20", "Minimal").Should().Be("Off");
        }

        // STAKE: a player who asks Grok to think (Medium or High) still gets thinking: the setting is the player's.
        [Test]
        public void GIVEN_grok_and_a_setting_that_asks_for_thinking_WHEN_resolved_THEN_it_still_thinks()
        {
            OnOffReasoningPolicy.ForModel("x-ai/grok-4.20", "Medium").Should().Be("Medium");
            OnOffReasoningPolicy.ForModel("x-ai/grok-4.20", "High").Should().Be("High");
            OnOffReasoningPolicy.ForModel("x-ai/grok-4.20", "Default").Should().Be("Default");
        }

        // STAKE: a model with levels (Gemini, DeepSeek) keeps Low: it thinks a little there, and some refuse off outright.
        [Test]
        public void GIVEN_a_model_with_levels_WHEN_resolved_THEN_low_stays_low()
        {
            OnOffReasoningPolicy.ForModel("google/gemini-3.8-flash", "Low").Should().Be("Low");
            OnOffReasoningPolicy.ForModel("deepseek/deepseek-v4.1-flash", "Low").Should().Be("Low");
            OnOffReasoningPolicy.ForModel(null, "Low").Should().Be("Low");
        }
    }
}
