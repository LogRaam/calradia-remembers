// Code written by Gabriel Mailhot, 06/09/2026.
// Two player reports named the same failure from opposite ends. Reporter A (Bannerlord 1.5.2 beta,
// CR 2.5.1): picked "A word with you...", the LLM fired, but the notable walked away and no CR overlay
// opened; not reproducible on reload. Reporter B (Bannerlord 1.3.15, CR 2.5.0, running the TOR total
// conversion): the CR option never appears for some wanderers, though it appears for another of the
// same occupation; relation score ruled out. Neither is reproducible on demand, so a console command is
// nearly useless. LlmDialogFlow registers the CR entry option on exactly two hub tokens
// (EntryHubTokens: hero_main_options, alley_player_owned_start) and IsHeroEligible() is already
// maximally permissive, so a conversation that never reaches one of those hubs silently has no CR
// option. This composes the single diagnostic line the collector writes at the end of every hero
// conversation, so "offered = YES" points investigation at the scene-opening code (reporter A) and
// "offered = NO" plus a foreign token in the trail names the other dialogue tree's own hub for us
// (reporter B), with nothing more to ask the player.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Composes the "[DIALOGUE] conversation with ..." line the dialogue collector logs once per hero
    ///   conversation. Pure: the collector supplies plain data (a partner label, an occupation/kind
    ///   string, whether the CR entry option was ever offered, the ordered distinct token trail, and the
    ///   hub tokens CR is registered on) and this decides the wording, whether the trail's own evidence
    ///   agrees with the offered flag, how an empty trail reads, and how a long trail is capped.
    /// </summary>
    public static class DialogueEntryTracePolicy
    {
        /// <summary>
        ///   How many tokens the rendered trail shows before truncating. Kept here (not only at the
        ///   collector's storage layer) so "how many tokens" is decided in exactly one place; the
        ///   collector's own accumulation cap reads this same constant.
        /// </summary>
        public const int MaxTrailTokens = 12;

        /// <summary>
        ///   Builds the diagnostic line. Never throws: a missing partner label, kind, trail or hub set
        ///   still renders a readable line rather than losing the one log entry the report depends on.
        /// </summary>
        public static string ComposeLine(
            string partnerLabel,
            string kind,
            bool entryOffered,
            IReadOnlyList<string> tokenTrail,
            IEnumerable<string> registeredHubTokens)
        {
            string partner = string.IsNullOrWhiteSpace(partnerLabel) ? "an unknown partner" : partnerLabel.Trim();
            string kindSuffix = string.IsNullOrWhiteSpace(kind) ? "" : $", {kind.Trim()}";
            string verdict = entryOffered ? "YES" : "NO";
            string trailText = RenderTrail(tokenTrail);

            string line = $"[DIALOGUE] conversation with {partner}{kindSuffix}: CR entry offered = {verdict}; tokens seen: {trailText}";

            string mismatch = DescribeMismatch(entryOffered, ReachedRegisteredHub(tokenTrail, registeredHubTokens));
            if (!string.IsNullOrEmpty(mismatch))
                line += $"; {mismatch}";

            return line;
        }

        /// <summary>
        ///   True when at least one token in the trail matches a hub CR is registered on. Computed
        ///   independently of <paramref name="entryOffered" /> (an engine-side proof set elsewhere) so a
        ///   disagreement between the two is itself something worth logging.
        /// </summary>
        private static bool ReachedRegisteredHub(IReadOnlyList<string> tokenTrail, IEnumerable<string> registeredHubTokens)
        {
            if (tokenTrail == null || tokenTrail.Count == 0) return false;

            HashSet<string> hubs = registeredHubTokens == null
                ? new HashSet<string>()
                : new HashSet<string>(registeredHubTokens);

            return tokenTrail.Any(hubs.Contains);
        }

        /// <summary>
        ///   Flags a disagreement between the "entry offered" proof and what the trail itself shows. Both
        ///   directions are worth knowing: offered-but-no-hub-in-trail means the trail is missing evidence
        ///   (capped, or a gap in collection); not-offered-but-hub-reached means CR reached a registered
        ///   hub yet IsHeroEligible still refused the option, a different bug class than a foreign hub.
        /// </summary>
        private static string DescribeMismatch(bool entryOffered, bool trailReachedHub)
        {
            if (entryOffered && !trailReachedHub)
                return "note: offered but no registered hub token appears in the trail (trail may be capped or incomplete)";

            if (!entryOffered && trailReachedHub)
                return "note: trail reached a registered hub but the entry was never offered (check IsHeroEligible)";

            return null;
        }

        private static string RenderTrail(IReadOnlyList<string> tokenTrail)
        {
            if (tokenTrail == null || tokenTrail.Count == 0) return "(none captured)";

            string joined = string.Join(" > ", tokenTrail.Take(MaxTrailTokens));
            int overflow = tokenTrail.Count - MaxTrailTokens;

            return overflow > 0 ? $"{joined} (+{overflow} more)" : joined;
        }
    }
}
