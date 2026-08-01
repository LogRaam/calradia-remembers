// Code written by Gabriel Mailhot, 01/08/2026.
// The mailbox's per-time-window cap: how many unprompted letters may actually reach the player in a given
// stretch of days, and how often a lower-weight letter gets injected for diversity rather than always the
// single loudest subject.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The budget the mailbox scheduler (<see cref="LetterMailboxPolicy" />) enforces: how many
    ///   unprompted letters may land per rolling window, and how often a lower-weight candidate is let
    ///   through anyway so the mailbox does not become a monotone feed of only the single loudest subject.
    /// </summary>
    public sealed class LetterBudget
    {
        /// <summary>The maximum number of unprompted letters allowed to reach the player within <see cref="WindowDays" />.</summary>
        public int MaxPerWindow { get; set; }

        /// <summary>The rolling window, in days, over which <see cref="MaxPerWindow" /> is enforced.</summary>
        public int WindowDays { get; set; }

        /// <summary>
        ///   The percent chance (0-99, compared against a caller-supplied roll) that a strictly lower-weight
        ///   candidate is chosen instead of the top one, so the mailbox stays varied rather than always
        ///   surfacing whichever subject scores highest.
        /// </summary>
        public int DiversityChancePercent { get; set; }
    }
}
