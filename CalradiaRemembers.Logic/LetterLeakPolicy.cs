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
        ///   What decides whether an intercepted letter reaches an antagonist. Four booleans then two doubles,
        ///   and the doubles are the dangerous pair: <c>Chance</c> and <c>Roll</c> are both in [0,1) and
        ///   swapping them inverts the odds, turning a rare leak into an almost-certain one.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class LeakFacts
        {
           /// <summary>The leak feature is on at all.</summary>
           public bool Enabled { get; set; }

           /// <summary>The loss was a BANDIT waylay. A letter merely lost on the road reaches no one.</summary>
           public bool ByBandit { get; set; }

           /// <summary>The body says something worth leaking.</summary>
           public bool HasSubstance { get; set; }

           /// <summary>Somebody exists who would want to read it.</summary>
           public bool AntagonistAvailable { get; set; }

           /// <summary>The probability the leak fires, in [0,1).</summary>
           public double Chance { get; set; }

           /// <summary>The caller's own draw, in [0,1). Injected so the decision stays pure and testable.</summary>
           public double Roll { get; set; }
        }

        /// <summary>
        ///   The leak fires only when the feature is enabled, the loss was a BANDIT waylay (a letter merely lost on
        ///   the road reaches no one), the body has substance, an antagonist exists to receive it, and the injected
        ///   <c>Roll</c> (in [0,1)) clears <c>Chance</c>.
        /// </summary>
        public static bool ShouldLeak(LeakFacts facts)
        {
           if (facts == null) return false;

           bool enabled = facts.Enabled;
           bool byBandit = facts.ByBandit;
           bool hasSubstance = facts.HasSubstance;
           bool antagonistAvailable = facts.AntagonistAvailable;
           double chance = facts.Chance;
           double roll = facts.Roll;

           return enabled && byBandit && hasSubstance && antagonistAvailable && roll < chance;
        }
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
