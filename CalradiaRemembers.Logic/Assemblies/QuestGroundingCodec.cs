// Code written by Gabriel Mailhot, 26/08/2026.
// ASSEMBLY pillar, Council module: a council "quest" resolution needs TWO pieces of grounding in
// AssemblyResolutionEntry.TargetHint's one slot: WHICH deed is pledged (quest_type, resolved by
// CouncilQuestTypePolicy) and the settlement it concerns. Mirrors OathGroundingCodec's own "a|b" composite
// shape, with ONE deliberate difference: the settlement half stays mandatory at the lift, but the quest_type
// half is legitimately ABSENT on any entry recorded before quest_type existed (a bare settlement name, no
// delimiter) or when the model simply omitted it, so a delimiter-less hint decomposes to (null, whole string)
// rather than failing, and Compose writes the plain legacy shape when no type was named.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Composes/splits a council quest's (quest_type, settlement) pair into <see cref="AssemblyResolutionEntry.TargetHint" />'s one slot.</summary>
    public static class QuestGroundingCodec
    {
        private const char Delimiter = '|';

        /// <summary>
        ///   Composes the quest_type token and the settlement into one grounding string, "questType|settlement".
        ///   A blank <paramref name="questType" /> (the model named none) writes the bare settlement, the exact
        ///   legacy shape every pre-quest_type entry carries, so an un-typed pledge stays byte-identical to what
        ///   it always was.
        /// </summary>
        public static string Compose(string questType, string settlement)
            => string.IsNullOrWhiteSpace(questType)
                ? settlement ?? ""
                : $"{questType.Trim()}{Delimiter}{settlement ?? ""}";

        /// <summary>
        ///   Splits a grounding string back into the quest_type token and the settlement. False (both out
        ///   params null) only when the hint is blank: a quest pledge with no place named must still fail at
        ///   the lift exactly as before. A DELIMITER-LESS hint (every entry recorded before quest_type existed)
        ///   decomposes to (null, the whole string): the lift's <see cref="CouncilQuestTypePolicy" /> fallback
        ///   then grounds it as the legacy bandit-clear. Only the FIRST delimiter splits the string, so a
        ///   settlement name that itself contained the delimiter character would stay whole on the right.
        /// </summary>
        public static bool TryDecompose(string composite, out string questType, out string settlement)
        {
            questType = null;
            settlement = null;

            if (string.IsNullOrWhiteSpace(composite)) return false;

            int idx = composite.IndexOf(Delimiter);

            if (idx < 0)
            {
                settlement = composite;

                return true;
            }

            string left = composite.Substring(0, idx).Trim();
            questType = left.Length > 0 ? left : null;
            settlement = composite.Substring(idx + 1);

            return true;
        }
    }
}
