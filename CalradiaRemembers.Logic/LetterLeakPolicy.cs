// Code written by Gabriel Mailhot, 24/08/2026.
// Intercepted-letter-leak Inc 1: when a player's letter is waylaid by bandits, a bounded chance its contents fall
// into the wrong hands and an antagonist writes to the player to exploit them. These two pure policies hold the
// DECISION (does this loss leak?) and the CHOICE (who gets the letter), so both are unit-tested rather than rolled
// blind in the engine-facing courier code. Everything engine (reading the loss, scanning grudges/enemies, queuing
// the antagonist's letter) stays in CourierService; this only decides on flattened facts + an injected roll.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether an intercepted (bandit-waylaid) player letter leaks its contents to an antagonist.</summary>
    public static class LetterLeakPolicy
    {
        /// <summary>
        ///   The per-loss chance a substantive letter leaks when the feature is enabled. Bounded and modest so a
        ///   leak stays a rare, memorable event rather than routine noise on top of an already-rare bandit waylay.
        /// </summary>
        public const double DefaultLeakChance = 0.35;

        /// <summary>
        ///   The shortest letter body worth leaking: a one-line note ("meet me at Sargot") carries no exploitable
        ///   secret, so only a letter with real substance (used here in place of a per-letter reason, which
        ///   <c>PlayerLetter</c> does not carry) can fall into the wrong hands.
        /// </summary>
        public const int MinLeakableContentLength = 40;

        /// <summary>True when the letter body has enough substance to be worth leaking.</summary>
        public static bool HasSubstance(string content)
            => !string.IsNullOrWhiteSpace(content) && content.Trim().Length >= MinLeakableContentLength;

        /// <summary>
        ///   The leak fires only when the feature is enabled, the loss was a BANDIT waylay (a letter merely lost on
        ///   the road reaches no one), the body has substance, an antagonist exists to receive it, and the injected
        ///   <paramref name="roll" /> (in [0,1)) clears <paramref name="chance" />.
        /// </summary>
        public static bool ShouldLeak(bool enabled, bool byBandit, bool hasSubstance, bool antagonistAvailable, double chance, double roll)
            => enabled && byBandit && hasSubstance && antagonistAvailable && roll < chance;
    }

    /// <summary>Picks which antagonist a leaked letter reaches, from pre-scanned candidate lists.</summary>
    public static class LetterLeakAntagonistPolicy
    {
        /// <summary>
        ///   Prefers a hero who already holds a grudge against the player (a personal enemy makes the sharpest use
        ///   of the secret), passed FIRST in <paramref name="grudgeHolderIdsByMagnitude" /> (the caller sorts by
        ///   grudge magnitude, strongest first); failing that, any hostile lord in
        ///   <paramref name="hostileLordIds" />. Returns null when neither list yields a usable id, so the caller
        ///   simply does not leak. Blank ids are skipped rather than returned.
        /// </summary>
        public static string Pick(IReadOnlyList<string> grudgeHolderIdsByMagnitude, IReadOnlyList<string> hostileLordIds)
        {
            string picked = FirstNonBlank(grudgeHolderIdsByMagnitude);
            if (picked != null) return picked;

            return FirstNonBlank(hostileLordIds);
        }

        private static string FirstNonBlank(IReadOnlyList<string> ids)
        {
            if (ids == null) return null;

            foreach (string id in ids)
                if (!string.IsNullOrEmpty(id))
                    return id;

            return null;
        }
    }
}
