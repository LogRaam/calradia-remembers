// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module: arrange_marriage (Partie 1, COUNCIL_ACTIONS.md, "juste arrange_mariage qui est
// valide") is the one resolution kind that needs TWO grounding names (the player-clan kin AND the seated ally's
// own kin), while AssemblyResolutionEntry.TargetHint is a single string, the one grounding slot every other kind
// already reuses (a role name, a mission name, an amount, a faction, a rival). Rather than growing a second
// AssemblyResolutionEntry field (a new mod save key for a single kind), this composes both names into ONE
// TargetHint-shaped string at record time (ChatViewModel.RecordCouncilResolution) and splits them apart again at
// the lift (CouncilLift.SettleArrangeMarriage), a structured composite this codebase controls end to end, never
// prose-parsing a free-text detail line.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Composes/splits arrange_marriage's two kin names into <see cref="AssemblyResolutionEntry.TargetHint" />'s one slot.</summary>
    public static class ArrangeMarriageGroundingCodec
    {
        private const char Delimiter = '|';

        /// <summary>
        ///   Composes both kin names into one grounding string, "playerKin|targetKin". Either half may be blank
        ///   (the caller, ChatViewModel.RecordCouncilResolution, composes whatever the model actually named,
        ///   even an incomplete pair): a blank half simply fails <see cref="TryDecompose" />'s own round-trip
        ///   at the lift instead of being rejected here.
        /// </summary>
        public static string Compose(string playerKinName, string targetKinName)
            => $"{playerKinName ?? ""}{Delimiter}{targetKinName ?? ""}";

        /// <summary>
        ///   Splits a composed grounding string back into its two kin names. False (both out params null) when
        ///   the composite is blank, carries no delimiter, or either half is blank once split: an arrange_marriage
        ///   resolution with an unreadable or incomplete grounding must never invent a name to fill the gap, it
        ///   must simply fail to decompose so the lift refuses the pledge honestly. Only the FIRST delimiter
        ///   splits the string (a target kin name that itself happened to contain the delimiter character stays
        ///   whole on the right-hand side), so a stray extra delimiter in a name never truncates it.
        /// </summary>
        public static bool TryDecompose(string composite, out string playerKinName, out string targetKinName)
        {
            playerKinName = null;
            targetKinName = null;

            if (string.IsNullOrWhiteSpace(composite)) return false;

            int idx = composite.IndexOf(Delimiter);
            if (idx < 0) return false;

            string left = composite.Substring(0, idx);
            string right = composite.Substring(idx + 1);

            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;

            playerKinName = left;
            targetKinName = right;

            return true;
        }
    }
}
