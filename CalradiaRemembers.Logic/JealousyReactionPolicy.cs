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

using System;

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a romantic act, witnessed or private, draws a jealous reaction this time.</summary>
    public static class JealousyReactionPolicy
    {
        /// <summary>
        ///   How low a SHARING-CULTURE partner's regard may be driven by the player taking further partners.
        ///   <para>
        ///     Player report, 2026-09-12: "Even if an NPC from the Empire accepts polyamory, their affinity
        ///     continues to drop. Since there is no specific information regarding jealousy, it is possible
        ///     that a recent update - intended to align views on romance with the culture - has caused an
        ///     internal conflict."
        ///   </para>
        ///   <para>
        ///     He was half right, and the half he was right about is this one. The culture change works: a
        ///     tolerant partner already takes 3/6/8 where an exclusive one takes 10/25/40, because by their
        ///     custom the grievance is RANK AND ATTENTION rather than betrayal. But nothing bounded it, so a
        ///     long campaign grinds a partner who ACCEPTS the arrangement all the way to -100, one small
        ///     slight at a time. Being set below another consort should cost warmth; it must not manufacture
        ///     hatred for a practice their own people keep.
        ///   </para>
        ///   <para>
        ///     Zero, not a negative: they may cool completely and still not become an enemy over it. A real
        ///     betrayal is a different stake and keeps the old floor of -100.
        ///   </para>
        /// </summary>
        public const int SharingRankFloor = 0;

        /// <summary>
        ///   The regard a sharing-culture partner actually loses, after the floor. Returns 0 once they have
        ///   already cooled to it, so the grievance stops costing regard and starts costing only the scene -
        ///   the memory and the letter still happen, because being overlooked is still worth saying.
        /// </summary>
        public static int SharingRankDelta(int currentRegard, int rawDelta)
        {
            if (rawDelta >= 0) return rawDelta;
            if (currentRegard <= SharingRankFloor) return 0;

            return Math.Max(rawDelta, SharingRankFloor - currentRegard);
        }

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
