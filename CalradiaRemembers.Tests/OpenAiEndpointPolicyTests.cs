// Code written by Gabriel Mailhot, 17/08/2026.
// Player request: run two local servers (one model each) and point different roles at different endpoints. This
// policy decides which OpenAI-Compatible endpoint a role calls: its own override, or the shared one. If it drifted,
// a role would silently hit the wrong server (or none), so these pin the fallback and the whitespace/trim handling
// the live client resolution relies on.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OpenAiEndpointPolicyTests
    {
        // The whole point of the feature: a role with its own endpoint calls THAT server, not the shared one, so
        // two roles can be split across two machines.
        [Test]
        public void GIVEN_a_role_override_WHEN_resolving_THEN_the_override_wins()
        {
            OpenAiEndpointPolicy.Resolve("http://pc1:1234/v1", "http://pc2:1234/v1")
                .Should().Be("http://pc2:1234/v1");
        }

        // The default that keeps every existing setup working: a role with no override inherits the shared endpoint,
        // so a player who never touches the new fields sees exactly the old single-endpoint behaviour.
        [Test]
        public void GIVEN_no_role_override_WHEN_resolving_THEN_the_shared_endpoint_is_used()
        {
            OpenAiEndpointPolicy.Resolve("http://pc1:1234/v1", "").Should().Be("http://pc1:1234/v1");
            OpenAiEndpointPolicy.Resolve("http://pc1:1234/v1", "   ").Should().Be("http://pc1:1234/v1");
            OpenAiEndpointPolicy.Resolve("http://pc1:1234/v1", null).Should().Be("http://pc1:1234/v1");
        }

        // A stray space around a pasted URL must never corrupt the call, whichever side it comes from.
        [Test]
        public void GIVEN_surrounding_whitespace_WHEN_resolving_THEN_the_result_is_trimmed()
        {
            OpenAiEndpointPolicy.Resolve("  http://pc1:1234/v1  ", null).Should().Be("http://pc1:1234/v1");
            OpenAiEndpointPolicy.Resolve("http://pc1:1234/v1", "  http://pc2:1234/v1  ").Should().Be("http://pc2:1234/v1");
        }

        // Nothing configured at all resolves to empty (never null), so callers can test IsNullOrWhiteSpace uniformly.
        [Test]
        public void GIVEN_nothing_configured_WHEN_resolving_THEN_it_is_empty()
        {
            OpenAiEndpointPolicy.Resolve(null, null).Should().BeEmpty();
        }
    }
}
