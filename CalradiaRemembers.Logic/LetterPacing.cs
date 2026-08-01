// Code written by Gabriel Mailhot, 01/08/2026.
// The player-facing dial for the mailbox scheduler: how much unprompted correspondence should actually
// reach them. Modelled on InterceptionPacing, but with no "Never": the player loves letters and wants the
// VOLUME turned down on a busy evening, not the whole relationship layer switched off.

namespace CalradiaRemembers.Logic
{
    /// <summary>How much unprompted NPC correspondence reaches the player. The player's taste, not a difficulty.</summary>
    public enum LetterPacing
    {
        /// <summary>The lightest post: a letter now and then, each one worth opening.</summary>
        Rarely,

        /// <summary>The shipped feel: enough correspondence to keep the world alive without burying the player. The default.</summary>
        Occasionally,

        /// <summary>A full mailbox: the realm writes often, and the player wants to hear from it.</summary>
        Often
    }

    /// <summary>Turns the player's chosen pacing into the budget that actually governs the mailbox.</summary>
    public static class LetterPacingRules
    {
        /// <summary>The shipped per-window cap, and the value Occasionally must keep returning so the default feel never drifts.</summary>
        public const int DefaultMaxPerWindow = 2;

        /// <summary>The shipped window length, for the same reason.</summary>
        public const int DefaultWindowDays = 7;

        /// <summary>The shipped diversity chance, for the same reason.</summary>
        public const int DefaultDiversityChancePercent = 25;

        /// <summary>Rarely's cap: fewer letters allowed per window than the default.</summary>
        public const int RarelyMaxPerWindow = 1;

        /// <summary>Rarely's window: longer than the default, so the same cap stretches further.</summary>
        public const int RarelyWindowDays = 6;

        /// <summary>Rarely's diversity chance: a touch less variety, since fewer letters arrive at all.</summary>
        public const int RarelyDiversityChancePercent = 20;

        /// <summary>Often's cap: the same single-letter cap as Rarely, but see <see cref="OftenWindowDays" /> for why it hits far more often.</summary>
        public const int OftenMaxPerWindow = 1;

        /// <summary>Often's window: much shorter than the default, so the cap resets far more frequently.</summary>
        public const int OftenWindowDays = 2;

        /// <summary>Often's diversity chance: a little more variety, since the mailbox is fuller overall.</summary>
        public const int OftenDiversityChancePercent = 30;

        /// <summary>
        ///   The budget for a chosen pacing. Occasionally returns exactly the Default* consts (a
        ///   non-regression anchor: a player who never opens the menu must keep the shipped feel), and each
        ///   other setting is a genuine dial away from it, not merely a relabelling.
        /// </summary>
        public static LetterBudget BudgetFor(LetterPacing pacing)
        {
            switch (pacing)
            {
                case LetterPacing.Rarely:
                    return new LetterBudget
                    {
                        MaxPerWindow = RarelyMaxPerWindow,
                        WindowDays = RarelyWindowDays,
                        DiversityChancePercent = RarelyDiversityChancePercent
                    };

                case LetterPacing.Often:
                    return new LetterBudget
                    {
                        MaxPerWindow = OftenMaxPerWindow,
                        WindowDays = OftenWindowDays,
                        DiversityChancePercent = OftenDiversityChancePercent
                    };

                default: // Occasionally
                    return new LetterBudget
                    {
                        MaxPerWindow = DefaultMaxPerWindow,
                        WindowDays = DefaultWindowDays,
                        DiversityChancePercent = DefaultDiversityChancePercent
                    };
            }
        }
    }
}
