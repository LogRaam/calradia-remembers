// Code written by Gabriel Mailhot, 21/08/2026.
// Nemesis pillar, discoverability piece: the last open bit noted in the ROADMAP ("a standalone
// tavern-keeper rumour variant remains an optional minor follow-up" to the dossier + NemesisBounty
// interception trigger shipped in v2.1.2). This is the pure sentence composer for the tavern-keeper's
// rumour reply: it NAMES the nemesis and his region and points the player at the existing bounty channel
// ("some lord hereabouts would pay to see him gone"), but it never itself states a reward or settles
// anything, since NemesisBountyDialogFlow alone owns issuing/settling a bounty (no duplication of that
// machinery). Mirrors NemesisDossierBuilder's role for the encyclopedia dossier: engine glue
// (NemesisTavernRumorDialogFlow) resolves the plain facts, this class only turns them into the one
// sentence actually shown.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure text for the tavern-keeper's nemesis rumour line: takes the nemesis's display label and his
    ///   region's display string (both already resolved by the caller, exactly like the lord bounty
    ///   announcement resolves them) and composes the one sentence the keeper speaks.
    /// </summary>
    public static class NemesisRumorLine
    {
        /// <summary>
        ///   The keeper's reply, or empty when <paramref name="nemesisLabel" /> is missing (nothing to name,
        ///   so nothing to say; the caller's own eligibility gate should prevent this in practice). A missing
        ///   <paramref name="regionDisplay" /> falls back to "these lands" rather than leaving a dangling
        ///   "raiding near ..." fragment (mirrors NemesisDossierBuilder's own tolerance of an unresolved region).
        /// </summary>
        public static string Compose(string nemesisLabel, string regionDisplay)
        {
            if (string.IsNullOrWhiteSpace(nemesisLabel)) return string.Empty;

            string region = string.IsNullOrWhiteSpace(regionDisplay) ? "these lands" : regionDisplay;

            return $"They say {nemesisLabel} has been raiding near {region}... some lord hereabouts would pay well to see him gone.";
        }
    }
}
