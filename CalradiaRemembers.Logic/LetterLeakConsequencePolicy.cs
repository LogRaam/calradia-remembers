// Code written by Gabriel Mailhot, 25/08/2026.
// Intercepted-letter-leak Inc 2: once a leak has fired (LetterLeakPolicy decided it, an antagonist received the
// text and wrote the exploiting letter), the leak gains LASTING consequences beyond that one note. This pure policy
// holds the two magnitudes those consequences use: how sharp a grudge the antagonist forms against the player, and
// how much renown the player loses as their private words become known. Both scale on whether the antagonist was
// ALREADY a personal enemy (a grudge-holder) rather than a merely hostile lord: an existing enemy makes the sharpest
// use of a secret and spreads it most eagerly. Kept pure so the numbers are unit-tested, not buried in courier code.

namespace CalradiaRemembers.Logic
{
    /// <summary>The grudge magnitude and renown cost a fired letter-leak (Inc 2) applies.</summary>
    public static class LetterLeakConsequencePolicy
    {
        /// <summary>Grudge magnitude when the antagonist ALREADY held a grudge against the player: a bitter enemy turns a secret to the sharpest use.</summary>
        public const int GrudgeMagnitudeEnemy = 25;

        /// <summary>Grudge magnitude when the antagonist was merely a hostile lord (no prior personal grudge): real, but milder.</summary>
        public const int GrudgeMagnitudeHostile = 15;

        /// <summary>Renown the player loses when a bitter enemy holds and spreads their private words: the sharper embarrassment.</summary>
        public const int RenownPenaltyEnemy = 3;

        /// <summary>Renown the player loses when a merely hostile lord holds the leak: a smaller public sting.</summary>
        public const int RenownPenaltyHostile = 2;

        /// <summary>The grudge the antagonist forms, scaled by whether they were already a personal enemy of the player.</summary>
        public static int GrudgeMagnitude(bool antagonistAlreadyHeldGrudge)
            => antagonistAlreadyHeldGrudge ? GrudgeMagnitudeEnemy : GrudgeMagnitudeHostile;

        /// <summary>The renown the player loses as the leaked words become known, scaled the same way.</summary>
        public static int RenownPenalty(bool antagonistAlreadyHeldGrudge)
            => antagonistAlreadyHeldGrudge ? RenownPenaltyEnemy : RenownPenaltyHostile;
    }
}
