// Code written by Gabriel Mailhot, 20/08/2026.

namespace CalradiaRemembers.Logic.Providers
{
    /// <summary>
    ///   Resolves which OpenAI-Compatible API key a service role should send. Twin of <see cref="OpenAiEndpointPolicy" />:
    ///   a player who splits roles across two local servers (per-role endpoint overrides) may also need each role to
    ///   authenticate with a DIFFERENT key, since two servers rarely share one. A single shared key sent every such
    ///   role to the same account, so a role may carry its OWN key override; blank means "use the shared key". Pure
    ///   so the fallback and trimming rules are the same everywhere the key is resolved.
    /// </summary>
    public static class OpenAiApiKeyPolicy
    {
        /// <summary>
        ///   The key a role should send: its own <paramref name="roleOverride" /> when set (non-blank), else the
        ///   <paramref name="sharedKey" />. Both are trimmed; a whitespace-only value counts as unset, and the
        ///   result is trimmed so a stray space never corrupts the Authorization header.
        /// </summary>
        public static string Resolve(string sharedKey, string roleOverride)
        {
            if (!string.IsNullOrWhiteSpace(roleOverride)) return roleOverride.Trim();

            return sharedKey?.Trim() ?? string.Empty;
        }
    }
}
