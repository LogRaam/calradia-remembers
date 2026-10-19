// Code written by Gabriel Mailhot, 01/08/2026.
// A persisted LetterCandidateIntent competes for the mailbox budget on every tick it survives, but nothing
// guarantees it ever wins: a steady stream of fresher, higher-weighted candidates can outcompete the same
// congratulation for weeks. We do NOT need to deliver every letter, only to show the player the ones that
// still matter, so a candidate that goes stale is simply RETIRED (removed, logged) rather than force-delivered
// or left to linger in the save forever. No anti-starvation ramp: staleness is a flat per-reason window.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>
    ///   How long a queued <see cref="LetterCandidateIntent" /> stays worth delivering before it is quietly
    ///   retired instead. A congratulation that was outcompeted for weeks reads as worse stale news than as no
    ///   news at all; a birth announcement stays meaningful longer than a battle brag, so it gets a longer window.
    /// </summary>
    public static class LetterCandidateFreshness
    {
        /// <summary>Window for a battle or tournament congratulation: a brag is only worth reading while recent.</summary>
        public const int CongratulationFreshnessDays = 15;

        /// <summary>Window for a birth announcement: a birth stays meaningful longer than a battle brag.</summary>
        public const int BirthFreshnessDays = 30;

        /// <summary>Fallback window for every other reason not called out above.</summary>
        public const int DefaultFreshnessDays = 15;

        /// <summary>
        ///   The number of days a candidate of this <paramref name="reason" /> stays fresh before it is
        ///   eligible for retirement.
        /// </summary>
        public static int FreshnessDays(LetterReason reason)
        {
            switch (reason)
            {
                case LetterReason.BattleVictory:
                case LetterReason.TournamentVictory:
                    return CongratulationFreshnessDays;

                case LetterReason.BirthAnnouncement:
                    return BirthFreshnessDays;

                default:
                    return DefaultFreshnessDays;
            }
        }

        /// <summary>
        ///   True once a candidate has sat unchosen past its freshness window. Strictly greater than the
        ///   window: a candidate exactly at the edge is still fresh, so the boundary itself never flips a
        ///   candidate stale a day earlier than promised.
        /// </summary>
        public static bool IsStale(LetterReason reason, int eligibleSinceDay, int today)
        {
            return today - eligibleSinceDay > FreshnessDays(reason);
        }

        /// <summary>
        ///   Whether the premise a candidate was queued on still holds at delivery. A battle congratulation is
        ///   queued only from a sender in the player's own realm, and can wait up to
        ///   <see cref="CongratulationFreshnessDays" /> for the mailbox; tashmetu (Nexus, 19/09/2026) was expelled
        ///   from his kingdom and kept getting letters "as if I were still part of it". A comrade's congratulation
        ///   from a realm the player no longer shares is retired rather than delivered. Every other reason is
        ///   indifferent to the realm.
        /// </summary>
        public static bool PremiseStillHolds(LetterReason reason, bool senderSharesPlayerRealm)
            => reason != LetterReason.BattleVictory || senderSharesPlayerRealm;
    }
}
