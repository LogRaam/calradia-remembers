// Code written by Gabriel Mailhot, 20/08/2026.
// Nemesis pillar, discoverability piece (player report, SuperGraham339): captured/tortured by a nemesis,
// escaped, killed the party, ran into him again (the pillar's designed payoff), then "tried to look him
// up in the encyclopedia but didn't see any mention." A nemesis the player has ALREADY encountered is no
// longer secret to him, so this is the pure text builder for the encyclopedia hero-page "Nemesis:" dossier
// (mirrors GrudgeNarrator's role for Grievances: numbers ARE wanted here, unlike Grievances' numberless
// rule, because "times encountered, captures, your escape, your victories" is exactly what the player
// asked to see). Un-met (latent) nemeses stay HIDDEN by construction: every method gates on
// timesEncountered > 0, the same signal NemesisBehavior only ever raises once a real encounter (a capture
// or an active-hunt materialize) has happened, never at mere minting.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure formatting for the encyclopedia hero-page nemesis dossier. Takes plain facts only (no engine
    ///   types) so it can be unit-tested in isolation; the mixin owns resolving a Hero/NemesisIdentity down
    ///   to these strings/ints and feeding them in. Two entry points mirror the two DataSourceProperties
    ///   the mixin exposes (a cream header, a gold bullet list), both returning an empty string when the
    ///   hero is not an encountered nemesis so the AutoHideRichTextWidget prefabs collapse.
    /// </summary>
    public static class NemesisDossierBuilder
    {
        /// <summary>The cream header text shown above the dossier.</summary>
        public const string Label = "Nemesis:";

        /// <summary>
        ///   The header line, or empty when this hero is not an encountered nemesis
        ///   (<paramref name="timesEncountered" /> at or below zero) so the widget auto-hides.
        /// </summary>
        public static string BuildLabel(int timesEncountered)
            => timesEncountered > 0 ? Label : string.Empty;

        /// <summary>
        ///   The gold bullet list: who he is (name, culture, region), your shared history (times
        ///   encountered, recaptures), then any extra history sentences the caller supplies (e.g. the
        ///   escape/release memory <c>NemesisBehavior.RecordCaptivityEndMemory</c> writes, or a captive
        ///   scene summary), oldest supplied first. Empty when <paramref name="timesEncountered" /> is at
        ///   or below zero (a latent nemesis stays hidden) or <paramref name="displayName" /> is missing
        ///   (never render a nameless entry).
        /// </summary>
        public static string BuildItems(
            string displayName,
            string cultureDisplay,
            string regionDisplay,
            int timesEncountered,
            int recaptureCount,
            IReadOnlyList<string> historySentences)
        {
            if (timesEncountered <= 0 || string.IsNullOrWhiteSpace(displayName)) return string.Empty;

            var lines = new List<string> {
                IdentityLine(displayName, cultureDisplay, regionDisplay),
                EncounterLine(timesEncountered)
            };

            string recaptureLine = RecaptureLine(recaptureCount);
            if (recaptureLine != null) lines.Add(recaptureLine);

            if (historySentences != null)
                foreach (string sentence in historySentences)
                    if (!string.IsNullOrWhiteSpace(sentence))
                        lines.Add(sentence.Trim());

            return string.Join("\n", lines.Select(l => "• " + l));
        }

        #region private

        /// <summary>Who he is: name plus culture/region, tolerant of either being missing (a "wilds"/unresolved anchor).</summary>
        private static string IdentityLine(string displayName, string cultureDisplay, string regionDisplay)
        {
            bool hasCulture = !string.IsNullOrWhiteSpace(cultureDisplay);
            bool hasRegion = !string.IsNullOrWhiteSpace(regionDisplay);

            if (hasCulture && hasRegion)
                return $"{displayName} leads a band of {cultureDisplay}, haunting the country around {regionDisplay}.";
            if (hasCulture)
                return $"{displayName} leads a band of {cultureDisplay}.";
            if (hasRegion)
                return $"{displayName} is a bandit boss haunting the country around {regionDisplay}.";

            return $"{displayName} is a bandit boss you have crossed paths with.";
        }

        /// <summary>Singular wording for one encounter, plural count otherwise (a raw "1 times" reads wrong).</summary>
        private static string EncounterLine(int timesEncountered)
            => timesEncountered == 1
                ? "You have crossed his path once."
                : $"You have crossed his path {timesEncountered} times.";

        /// <summary>Null when he has never recaptured the player (no escape to speak of yet, so no line at all).</summary>
        private static string RecaptureLine(int recaptureCount)
        {
            if (recaptureCount <= 0) return null;

            return recaptureCount == 1
                ? "He recaptured you once, after you had escaped his hold."
                : $"He has recaptured you {recaptureCount} times, after you had escaped his hold.";
        }

        #endregion
    }
}
