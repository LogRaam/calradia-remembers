// Code written by Gabriel Mailhot, 20/08/2026.
// Player request: "The ability to have separate endpoints for the NSFW and Interpret models is VERY helpful, but I
// also need the ability to have separate API keys for those endpoints, too, otherwise I can't use them." This
// policy decides which OpenAI-Compatible key a role sends: its own override, or the shared one. If it drifted, a
// role split onto its own server would keep sending the WRONG key (or none) and fail to authenticate, so these pin
// the fallback and the whitespace/trim handling the live client resolution relies on. Twin of
// OpenAiEndpointPolicyTests, same shape, one field later.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OpenAiApiKeyPolicyTests
    {
        // The whole point of the feature: a role with its own key authenticates against ITS server, not the shared
        // account, so two roles split across two machines can each use the key their own server expects.
        [Test]
        public void GIVEN_a_role_override_WHEN_resolving_THEN_the_override_wins()
        {
            OpenAiApiKeyPolicy.Resolve("shared-key-1", "role-key-2")
                .Should().Be("role-key-2");
        }

        // The default that keeps every existing setup working: a role with no key override inherits the shared key,
        // so a player who never touches the new fields sees exactly the old single-key behaviour.
        [Test]
        public void GIVEN_no_role_override_WHEN_resolving_THEN_the_shared_key_is_used()
        {
            OpenAiApiKeyPolicy.Resolve("shared-key-1", "").Should().Be("shared-key-1");
            OpenAiApiKeyPolicy.Resolve("shared-key-1", "   ").Should().Be("shared-key-1");
            OpenAiApiKeyPolicy.Resolve("shared-key-1", null).Should().Be("shared-key-1");
        }

        // A stray space around a pasted key must never corrupt the Authorization header, whichever side it comes from.
        [Test]
        public void GIVEN_surrounding_whitespace_WHEN_resolving_THEN_the_result_is_trimmed()
        {
            OpenAiApiKeyPolicy.Resolve("  shared-key-1  ", null).Should().Be("shared-key-1");
            OpenAiApiKeyPolicy.Resolve("shared-key-1", "  role-key-2  ").Should().Be("role-key-2");
        }

        // Nothing configured at all resolves to empty (never null), matching a keyless local server and letting
        // callers test IsNullOrWhiteSpace uniformly.
        [Test]
        public void GIVEN_nothing_configured_WHEN_resolving_THEN_it_is_empty()
        {
            OpenAiApiKeyPolicy.Resolve(null, null).Should().BeEmpty();
        }
    }
}
