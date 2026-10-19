// Code written by Gabriel Mailhot, 17/08/2026.

namespace CalradiaRemembers.Logic.Providers
{
    /// <summary>
    ///   Resolves which OpenAI-Compatible endpoint URL a service role should call. Phase 2 lets each role (prose,
    ///   NSFW, interpreter) pick the OpenAI-Compatible provider, but a single shared endpoint sent every such role
    ///   to the same local server. A player running two machines (one model each) wants to split the roles across
    ///   two servers, so a role may carry its OWN endpoint override; blank means "use the shared endpoint". Pure so
    ///   the fallback and trimming rules are the same everywhere the endpoint is resolved.
    /// </summary>
    public static class OpenAiEndpointPolicy
    {
        /// <summary>
        ///   The endpoint a role should use: its own <paramref name="roleOverride" /> when set (non-blank), else the
        ///   <paramref name="sharedEndpoint" />. Both are trimmed; a whitespace-only value counts as unset, and the
        ///   result is trimmed so a stray space never corrupts the URL.
        /// </summary>
        public static string Resolve(string sharedEndpoint, string roleOverride)
        {
            if (!string.IsNullOrWhiteSpace(roleOverride)) return roleOverride.Trim();

            return sharedEndpoint?.Trim() ?? string.Empty;
        }
    }
}
