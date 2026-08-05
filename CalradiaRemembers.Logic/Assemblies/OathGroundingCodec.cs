// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module: swear_oath (R7-light, COUNCIL_ACTIONS.md's Partie 8) needs TWO pieces of
// grounding in AssemblyResolutionEntry.TargetHint's one slot: WHICH oath kind (pay_gold/keep_peace/protect,
// OathKindParser's own whitelist) and that kind's own target value (a denars figure for pay_gold, a faction name
// for keep_peace, nothing at all for protect, which names no target beyond the player). Mirrors
// ArrangeMarriageGroundingCodec's/FiefSwapGroundingCodec's own "a|b" composite shape, with ONE deliberate
// difference: those two kinds require BOTH halves non-blank (an incomplete pair is meaningless), but protect's
// own target half is legitimately EMPTY by design, so only the LEFT half (the oath kind) is mandatory here.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Composes/splits swear_oath's (oath kind, target value) pair into <see cref="AssemblyResolutionEntry.TargetHint" />'s one slot.</summary>
    public static class OathGroundingCodec
    {
        private const char Delimiter = '|';

        /// <summary>
        ///   Composes the oath kind and its own target value into one grounding string, "oathKind|targetValue".
        ///   <paramref name="targetValue" /> may legitimately be blank (protect names no target beyond the
        ///   player): the caller (ChatViewModel.RecordCouncilResolution) composes whatever the model actually
        ///   named, even a bare oath kind with nothing else.
        /// </summary>
        public static string Compose(string oathKind, string targetValue)
            => $"{oathKind ?? ""}{Delimiter}{targetValue ?? ""}";

        /// <summary>
        ///   Splits a composed grounding string back into the oath kind and its target value. False (both out
        ///   params null) when the composite is blank, carries no delimiter, or the OATH KIND half is blank once
        ///   split: a swear_oath resolution with no readable kind must never guess one at the lift, it must fail
        ///   to decompose so the lift refuses the pledge honestly. UNLIKE ArrangeMarriageGroundingCodec's/
        ///   FiefSwapGroundingCodec's own two-mandatory-halves rule, <paramref name="targetValue" /> alone being
        ///   blank does NOT fail here (it comes back as "", never null, once the kind itself is readable): a
        ///   protect oath legitimately carries no second value, and forcing one would refuse every protect oath
        ///   the model ever proposes. Only the FIRST delimiter splits the string, so a target value that itself
        ///   happened to contain the delimiter character stays whole on the right-hand side.
        /// </summary>
        public static bool TryDecompose(string composite, out string oathKind, out string targetValue)
        {
            oathKind = null;
            targetValue = null;

            if (string.IsNullOrWhiteSpace(composite)) return false;

            int idx = composite.IndexOf(Delimiter);
            if (idx < 0) return false;

            string left = composite.Substring(0, idx);
            string right = composite.Substring(idx + 1);

            if (string.IsNullOrWhiteSpace(left)) return false;

            oathKind = left;
            targetValue = right;

            return true;
        }
    }
}
