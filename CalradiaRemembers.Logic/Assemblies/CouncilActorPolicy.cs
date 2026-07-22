// Code written by Gabriel Mailhot, 21/07/2026.
// ASSEMBLY pillar, Council module: the whole-table single call yields ONE response for the whole room, but any
// seated member may commit to a deed inside it ("I'll see that you get the gold" from a companion, not the
// anchor speaking). Without attribution the action-dispatch loop has always executed every [ACTION] against
// whoever carries the turn's spoken reply (the anchor), so a member's own promise silently happened to someone
// else, or to nobody. PromptBuilder now teaches the model to name the actor; this decides WHICH seated member
// that name refers to, so the mod can dispatch (or safely drop) it, and never guess.
//
// The resolution is tiered, mirroring the tolerant witness-name matching already in ChatViewModel.Helpers
// (HandleWitnessLeaves) and DialogueHeuristics.StripWitnessRole: an exact full-name match (after stripping any
// parenthetical role the model echoed) wins outright, even if the SAME fragment also happens to be a substring
// of another seated name (the model gave the whole name, so trust it). Failing that, a tolerant bidirectional
// substring match (a first name, a partial name) is accepted only when it points at exactly ONE seat. An
// actor naming nobody seated, or one that fits two or more seats equally, resolves to -1: a silent guess here
// would mean the wrong hero's gold, or the wrong hero married off, which is worse than the deed never landing.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Resolves an [ACTION] block's "actor" name against the seated members of a council.</summary>
    public static class CouncilActorPolicy
    {
        /// <summary>
        ///   Index into <paramref name="seatedNames" /> that <paramref name="actor" /> refers to, or -1 when
        ///   the actor is blank, names nobody seated, or matches more than one seat ambiguously. Never guesses:
        ///   the caller must drop the action rather than dispatch it to a fallback.
        /// </summary>
        public static int ResolveActor(string actor, IReadOnlyList<string> seatedNames)
        {
            if (string.IsNullOrWhiteSpace(actor) || seatedNames == null || seatedNames.Count == 0) return -1;

            string cleaned = DialogueHeuristics.StripWitnessRole(actor);

            if (string.IsNullOrWhiteSpace(cleaned)) return -1;

            // Tier 1: an exact full-name match is unambiguous even when the same fragment is ALSO a substring
            // of another seated name (e.g. "Mira" exactly matches "Mira" and would also substring-match
            // "Miranda"), the model gave the whole name, so this tier trusts it outright rather than call
            // that a collision.
            int exactIndex = -1;
            var exactCount = 0;

            for (var i = 0; i < seatedNames.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(seatedNames[i])) continue;
                if (!string.Equals(seatedNames[i].Trim(), cleaned, StringComparison.OrdinalIgnoreCase)) continue;

                exactIndex = i;
                exactCount++;
            }

            if (exactCount == 1) return exactIndex;
            if (exactCount > 1) return -1; // two seats share the exact name, cannot tell them apart

            // Tier 2: tolerant partial / first-name match, bidirectional substring (the same tolerance
            // HandleWitnessLeaves already applies to a player companion's name). Accepted only when it
            // singles out exactly one seat; a fragment shared by two seated members (e.g. two "Aldric"s) is
            // ambiguous.
            int partialIndex = -1;
            var partialCount = 0;

            for (var i = 0; i < seatedNames.Count; i++)
            {
                string seated = seatedNames[i]?.Trim();

                if (string.IsNullOrEmpty(seated)) continue;

                bool matches = seated.IndexOf(cleaned, StringComparison.OrdinalIgnoreCase) >= 0
                            || cleaned.IndexOf(seated, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!matches) continue;

                partialIndex = i;
                partialCount++;
            }

            return partialCount == 1 ? partialIndex : -1;
        }
    }
}
