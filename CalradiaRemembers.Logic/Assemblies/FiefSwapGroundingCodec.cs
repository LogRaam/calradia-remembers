// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module: swap_fiefs (Partie 8, COUNCIL_ACTIONS.md's Kimi review) is the SECOND
// resolution kind that needs TWO grounding names (the player's own fief AND the seated vassal's own fief), while
// AssemblyResolutionEntry.TargetHint is a single string. Mirrors ArrangeMarriageGroundingCodec's own shape
// exactly (Compose/TryDecompose "a|b"), a dedicated codec rather than growing a second TargetHint-shaped field on
// the entry (a new mod save key for a single kind) or generalizing a shared two-part composite utility only two
// kinds would ever use: one small, self-documented codec per two-value kind keeps each kind's own grounding
// readable on its own, the same precedent arrange_marriage already set.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Composes/splits swap_fiefs' two fief names into <see cref="AssemblyResolutionEntry.TargetHint" />'s one slot.</summary>
    public static class FiefSwapGroundingCodec
    {
        private const char Delimiter = '|';

        /// <summary>
        ///   Composes both fief names into one grounding string, "playerFief|targetFief". Either half may be
        ///   blank (the caller, ChatViewModel.RecordCouncilResolution, composes whatever the model actually
        ///   named, even an incomplete pair): a blank half simply fails <see cref="TryDecompose" />'s own
        ///   round-trip at the lift instead of being rejected here.
        /// </summary>
        public static string Compose(string playerFiefName, string targetFiefName)
            => $"{playerFiefName ?? ""}{Delimiter}{targetFiefName ?? ""}";

        /// <summary>
        ///   Splits a composed grounding string back into its two fief names. False (both out params null) when
        ///   the composite is blank, carries no delimiter, or either half is blank once split: a swap_fiefs
        ///   resolution with an unreadable or incomplete grounding must never invent a fief to fill the gap, it
        ///   must simply fail to decompose so the lift refuses the pledge honestly (never transferring one side
        ///   of the trade alone). Only the FIRST delimiter splits the string (a target fief name that itself
        ///   happened to contain the delimiter character stays whole on the right-hand side), so a stray extra
        ///   delimiter in a name never truncates it.
        /// </summary>
        public static bool TryDecompose(string composite, out string playerFiefName, out string targetFiefName)
        {
            playerFiefName = null;
            targetFiefName = null;

            if (string.IsNullOrWhiteSpace(composite)) return false;

            int idx = composite.IndexOf(Delimiter);
            if (idx < 0) return false;

            string left = composite.Substring(0, idx);
            string right = composite.Substring(idx + 1);

            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;

            playerFiefName = left;
            targetFiefName = right;

            return true;
        }
    }
}
