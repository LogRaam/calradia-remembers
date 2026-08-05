// Code written by Gabriel Mailhot, 04/08/2026.
// R7-light (COUNCIL_ACTIONS.md's swear_oath, Partie 8): the council's swear_oath resolution grounds on a SECOND
// piece of data beyond the usual single TargetHint slot, WHICH oath kind is being sworn (oath_kind: pay_gold /
// keep_peace / protect, see NpcMemoryService.Core.Models.ResolutionProposal.OathKind), composed alongside that
// kind's own target value by Assemblies.OathGroundingCodec. This parser is the ONE place that string is resolved
// to a real CommitmentKind, and it is deliberately a WHITELIST: move_against (pledge_against's own kind, a
// separate resolution with its own scheme-launching executor) is never parseable here, so a swear_oath naming it
// simply fails to resolve rather than silently doing what pledge_against does under a different name.

namespace CalradiaRemembers.Logic
{
    /// <summary>Resolves a swear_oath resolution's oath_kind string to the whitelisted <see cref="CommitmentKind" /> it names.</summary>
    public static class OathKindParser
    {
        /// <summary>
        ///   Case-insensitive, tolerant of surrounding whitespace. False (kind left at its default) for null,
        ///   blank, or unrecognised text, including "move_against", deliberately never mapped here since that
        ///   kind is pledge_against's own separate resolution, not something swear_oath may also promise under a
        ///   different name.
        /// </summary>
        public static bool TryParse(string text, out CommitmentKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            switch (text.Trim().ToLowerInvariant())
            {
                case "pay_gold":
                    kind = CommitmentKind.PayGold;

                    return true;
                case "keep_peace":
                    kind = CommitmentKind.KeepPeace;

                    return true;
                case "protect":
                    kind = CommitmentKind.Protect;

                    return true;
                default:
                    return false;
            }
        }
    }
}
