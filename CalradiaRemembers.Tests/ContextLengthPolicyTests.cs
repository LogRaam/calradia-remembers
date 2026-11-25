// Code written by Gabriel Mailhot, 25/11/2026.
// What this pins: the Test button proves a local server can hold a CR-sized request, and a server's "longer than my
// context" refusal is told to the player as the knob to turn.
//
// YubYub02 (Discord, 24/11/2026), LM Studio: the Test button said the AI "responded and is ready to play", and every
// message in the game failed in under a second with "Message did not reach the AI". The button had sent one word;
// the game sends thousands of tokens to a model loaded with a short context. If these fail, a player is told he is
// ready when nothing will pass, or is told to "check your model and endpoint" when the context length is the cause.

#region

using CalradiaRemembers.Logic.Providers;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ContextLengthPolicyTests
    {
        // THE REPORT: with no window declared, the probe must be bigger than the 4,096 tokens LM Studio often loads a
        // model with, or YubYub02's setup passes the test again and fails every message.
        [Test]
        public void GIVEN_no_declared_window_WHEN_sizing_the_probe_THEN_it_exceeds_a_4096_token_context()
        {
            int tokens = ContextLengthPolicy.ProbeTokens(0);

            tokens.Should().BeGreaterThan(4096);
            ContextLengthPolicy.Filler(tokens).Length.Should().BeGreaterThan(4096 * 4);
        }

        // A player who declared his window is told whether the server really holds it: CR will fill that much, and a
        // server loaded shorter than the setting refuses the very requests CR fitted to it.
        [Test]
        public void GIVEN_a_declared_window_WHEN_sizing_the_probe_THEN_it_fills_most_of_that_window()
        {
            ContextLengthPolicy.ProbeTokens(16384).Should().BeInRange(14000, 16384);
        }

        // The probe goes to a server at home, never billed to a cloud account by a click meant to check a key.
        [TestCase("http://localhost:1234/v1", true)]
        [TestCase("http://127.0.0.1:11434/v1", true)]
        [TestCase("http://192.168.1.20:1234/v1", true)]
        [TestCase("http://10.0.0.5:8080/v1", true)]
        [TestCase("http://172.20.1.2:1234/v1", true)]
        [TestCase("http://gaming-pc.local:1234/v1", true)]
        [TestCase("http://[::1]:1234/v1", true)]
        [TestCase("https://api.deepseek.com/v1", false)]
        [TestCase("https://nano-gpt.com/api/v1", false)]
        [TestCase("http://172.40.1.2:1234/v1", false)]
        [TestCase("not a url", false)]
        public void GIVEN_an_endpoint_WHEN_deciding_whether_to_probe_THEN_only_a_server_at_home_is_probed(string url, bool probed)
        {
            ContextLengthPolicy.ShouldProbe(url, 0).Should().Be(probed);
        }

        // A declared window says the player runs a local model wherever it lives (a tunnel, a rented box): probe it.
        [Test]
        public void GIVEN_a_declared_window_on_a_remote_address_WHEN_deciding_THEN_it_is_probed()
        {
            ContextLengthPolicy.ShouldProbe("https://my-box.example.com/v1", 8192).Should().BeTrue();
        }

        // LM Studio's own refusal for a prompt longer than the loaded context: the player is told to raise the context
        // length and to set CR's window to match, with the server's number named.
        [Test]
        public void GIVEN_lm_studios_context_refusal_WHEN_explained_THEN_the_player_is_told_the_context_length_and_its_number()
        {
            string advice = ContextLengthPolicy.ExplainOverflow(
                "HTTP 400: {\"error\":\"Trying to keep the first 9120 tokens when context the overflows. However, the model is loaded with context length of only 4096 tokens, which is not enough. Try to load the model with a larger context length, or provide a shorter input\"}",
                0);

            advice.Should().Contain("4096 tokens");
            advice.Should().Contain("Context Length");
            advice.Should().Contain("Local model context (tokens)");
        }

        // llama.cpp's server words it differently (the line quoted in ActionVocabularyPolicy's header); same advice.
        [Test]
        public void GIVEN_llama_cpps_context_refusal_WHEN_explained_THEN_the_servers_size_is_named()
        {
            ContextLengthPolicy.ExplainOverflow("request (9496 tokens) exceeds the available context size (8192 tokens)", 8192)
                .Should().Contain("8192 tokens").And.Contain("it is set to 8192 now");
        }

        // Any other failure (a wrong model id, a server down) keeps its own message: calling it a context problem
        // would send the player to the wrong knob.
        [Test]
        public void GIVEN_an_unrelated_error_WHEN_explained_THEN_nothing_is_claimed()
        {
            ContextLengthPolicy.ExplainOverflow("HTTP 404: model 'nemotron' not found", 0).Should().BeNull();
            ContextLengthPolicy.ExplainOverflow(null, 0).Should().BeNull();
        }
    }
}
