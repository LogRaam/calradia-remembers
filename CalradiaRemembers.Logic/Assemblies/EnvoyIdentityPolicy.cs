// Code written by Gabriel Mailhot, 29/08/2026.
// Convergence Inc 4 of 5: the pure text-identity half of the SYNTHETIC ENVOY (ambassador). Ratified design: an
// ambassador is ALWAYS a light, generated, rankless herald (historically you never risked sending a real lord who
// could be captured), never a real Hero, never a clan member or companion. It speaks IN the represented lord's
// name and binds nothing itself. This policy composes the two plain-string pieces that make that identity
// unmistakable to the player (the display name) and legible to the LLM (the persona line); the mod's
// EnvoyFactory (Assemblies/EnvoyFactory.cs) wires these into an ephemeral NpcProfile. Engine-free by design, like
// every other policy in this namespace, so the wording is unit-tested without a live campaign.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   Composes a synthetic ambassador's text identity from plain strings: the display name that must read
    ///   unmistakably as "not the real lord, but speaking for them" (<see cref="DisplayName" />), and the persona
    ///   line the prompt shows the LLM so it plays the herald's constrained role rather than the lord's own
    ///   voice (<see cref="Persona" />). Ambassadors are ONLY ever for other factions (a parley); the player's
    ///   own faction never sends a stand-in to its own table, a guarantee this policy takes no part in enforcing
    ///   (that is <see cref="CouncilAttendancePolicy.Decide" />'s job) but whose OUTPUT this policy must never
    ///   contradict by naming the player's own house as foreign.
    /// </summary>
    public static class EnvoyIdentityPolicy
    {
        /// <summary>Used when no faction name is available at all, so the seat never reads as blank or broken.</summary>
        public const string UnknownFactionFallback = "A foreign herald";

        /// <summary>
        ///   The seat's display name: "Herald of {factionName}", trimmed. A blank/null faction name degrades to
        ///   <see cref="UnknownFactionFallback" /> rather than emitting "Herald of " with nothing after it, which
        ///   would read as broken rather than merely unnamed. This is the ONE line the player reads at the table,
        ///   so it carries the whole "this is a stand-in, not the lord himself" signal by itself.
        /// </summary>
        public static string DisplayName(string factionName)
        {
            string trimmed = factionName?.Trim();

            return string.IsNullOrEmpty(trimmed) ? UnknownFactionFallback : $"Herald of {trimmed}";
        }

        /// <summary>
        ///   The persona line injected into the prompt (NpcProfile.Personality, see EnvoyFactory): names both the
        ///   faction and the leader the herald speaks for, and states the constraint that makes an ambassador
        ///   safe to seat at all: it carries words, not authority, so nothing the herald says can bind the real
        ///   lord beyond what was actually sent. Degrades gracefully when either name is missing (a faction with
        ///   no resolvable leader, or the reverse) rather than ever emitting "null" or an empty "of {}" clause.
        /// </summary>
        public static string Persona(string factionName, string leaderName)
        {
            string faction = factionName?.Trim();
            string leader = leaderName?.Trim();
            bool hasFaction = !string.IsNullOrEmpty(faction);
            bool hasLeader = !string.IsNullOrEmpty(leader);

            string sentBy = hasLeader && hasFaction
                ? $"sent by {leader} of {faction}"
                : hasLeader
                    ? $"sent by {leader}"
                    : hasFaction
                        ? $"sent by the court of {faction}"
                        : "sent by a foreign court";

            return "A rankless herald " + sentBy + ". You speak in their name and carry their words, and you " +
                   "bring their answer back; you bind nothing by your own authority.";
        }
    }
}
