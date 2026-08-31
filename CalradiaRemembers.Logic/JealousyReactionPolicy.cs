// Code written by Gabriel Mailhot, 17/07/2026.
// Jealousy, witnessed-reaction gate (romance audit M-J1/M-J2). When the player courts someone in front of
// witnesses, word travels, but "not every flirt travels": a probability roll decides. Two bugs lived here. M-J2:
// that roll was applied even to a MARRIAGE-tier act (a public wedding, a consort openly taken), so a spouse had
// a 1-in-4 chance of never learning their partner was taken, which is absurd for so public a betrayal. M-J1: the
// pairing's reaction cooldown was consumed BEFORE the roll, so a failed flirt roll still put the pair on a 20-day
// cooldown and swallowed a real marriage that happened days later. This pure gate answers only "does a witnessed
// act react", so the host can consume the cooldown ONLY when it does, and a wedding always wakes the jealous.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a romantic act, witnessed or private, draws a jealous reaction this time.</summary>
    public static class JealousyReactionPolicy
    {
        /// <summary>
        ///   True when a witnessed act reacts: a <see cref="RomanticActTier.Marriage" /> act ALWAYS does (a public
        ///   wedding or consort-taking is never a "maybe someone noticed" event, romance audit M-J2), and any
        ///   lesser act reacts when the travel roll lands within <paramref name="witnessedChance" /> (roll drawn
        ///   in [0,1], reacts when <paramref name="roll" /> is at or below the chance). The host must consume the
        ///   pairing cooldown only when this returns true (romance audit M-J1), never on a failed roll.
        /// </summary>
        public static bool WitnessedActReacts(RomanticActTier tier, float roll, float witnessedChance)
            => tier == RomanticActTier.Marriage || roll <= witnessedChance;

        /// <summary>
        ///   True when a PRIVATE (unwitnessed) act reacts. A <see cref="RomanticActTier.Marriage" /> act ALWAYS
        ///   does, exactly as a witnessed one: a marriage is telegraphed whether or not anyone watched. Any lesser
        ///   act reacts only when <paramref name="roll" /> lands within <paramref name="chance" />, a private
        ///   flirt has no witnesses to carry the tale, so it does not always leave a rumour behind at all. As
        ///   above, the host must consume the pairing cooldown only when this returns true.
        /// </summary>
        public static bool PrivateActReacts(RomanticActTier tier, float roll, float chance)
            => tier == RomanticActTier.Marriage || roll < chance;
    }
}
