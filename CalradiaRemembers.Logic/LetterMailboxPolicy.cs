// Code written by Gabriel Mailhot, 01/08/2026.
// The mailbox scheduler itself: the gate that stands between "an NPC has a reason to write" and "a letter
// actually reaches a 100h player who may have dozens of correspondents at once." Without this, every
// eligible letter lands the moment it is written, and the player's mailbox becomes noise instead of news.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Chooses which single unprompted letter, if any, reaches the player right now: a budgeted,
    ///   weight-ordered, anti-starvation, occasionally-diverse pick over a set of candidates the caller has
    ///   already decided are eligible to send. Pure over its inputs; the caller owns eligibility, the RNG
    ///   roll, and actually delivering the winner.
    /// </summary>
    public static class LetterMailboxPolicy
    {
        /// <summary>How many weight points a candidate gains per day it has waited, once past the free grace of zero days.</summary>
        public const int AgingWeightPerDay = 1;

        /// <summary>The ceiling on how many days of aging can accumulate, so an ancient candidate cannot climb forever.</summary>
        public const int AgingCapDays = 20;

        /// <summary>
        ///   True for a letter reason that must ALWAYS reach the player and so never competes for the
        ///   mailbox budget at all: these are mechanical or solicited (a divorce deadline, a child's upkeep,
        ///   a companion's final goodbye, a reply to a letter the player themselves sent), and the player
        ///   must never miss one because a livelier subject happened to win the day's budget slot. The
        ///   caller is expected to filter these out before building the candidate list passed to
        ///   <see cref="SelectNext" />; they are never special-cased inside it.
        /// </summary>
        public static bool BypassesBudget(LetterReason reason)
        {
            return reason == LetterReason.SpouseDivorceDemand
                || reason == LetterReason.SpouseDivorceEscalation
                || reason == LetterReason.ChildSupportRequest
                || reason == LetterReason.CompanionFarewell
                || reason == LetterReason.PlayerLetterReply
                // The vassal war duty (15/10/2026): a summons and a levy demand carry a deadline the game holds the
                // player to, so they can never lose the day's slot to a livelier subject.
                || reason == LetterReason.WarSummons
                || reason == LetterReason.WarLevyDemand
                // The patrol's monthly letter (Gabriel, 16/10/2026): the player decides on it whether the men come home.
                || reason == LetterReason.PatrolReport
                // A letter another mod wrote and paces itself (CrLetters.Deliver, 18/10/2026): never ours to hold back.
                || reason == LetterReason.FromAnotherMod;
        }

        /// <summary>
        ///   True when an already-sent letter should count against the rolling-window budget: it must be a
        ///   reason that competes for the mailbox at all (<see cref="BypassesBudget" /> is false for it), and
        ///   it must have actually reached the player rather than being lost to a waylaid courier. A lost
        ///   letter consumed no attention, so counting it would shrink the player's real budget for a courier
        ///   mishap that was never their correspondent's fault; a bypass reason was never competing for the
        ///   budget in the first place, so it must not silently eat into it either. Pure so the derivation in
        ///   the mod's <c>GatherRecentReleaseDays</c> stays a one-line call over persisted data.
        /// </summary>
        public static bool CountsTowardBudget(LetterReason reason, bool isLost)
        {
            return !isLost && !BypassesBudget(reason);
        }

        /// <summary>
        ///   The candidate's weight as it stands today: its base <see cref="LetterCandidate.Weight" /> plus
        ///   a slow anti-starvation climb for every day it has waited past <see cref="LetterCandidate.EligibleSinceDay" />,
        ///   capped at <see cref="AgingCapDays" /> days so a candidate that has been overtaken again and
        ///   again eventually forces its way to the front rather than waiting forever behind louder subjects.
        /// </summary>
        public static int EffectiveWeight(LetterCandidate c, int today)
        {
            int waitedDays = Math.Min(Math.Max(0, today - c.EligibleSinceDay), AgingCapDays);
            return c.Weight + waitedDays * AgingWeightPerDay;
        }

        /// <summary>
        ///   Picks the single candidate that should be released today, or <c>null</c> if none should be.
        ///   First enforces the budget (no pick if the window's quota is already spent), then orders every
        ///   candidate by <see cref="EffectiveWeight" /> (highest first, oldest-eligible first on a tie,
        ///   then sender id for a fully deterministic order), then usually returns the top candidate, but
        ///   occasionally (per <paramref name="diversityRoll" /> against <see cref="LetterBudget.DiversityChancePercent" />)
        ///   returns the best of the strictly-lower-weight candidates instead, so the mailbox does not
        ///   always surface the single loudest subject and nothing else.
        /// </summary>
        /// <param name="candidates">
        ///   Eligible letters to choose among. Must already exclude every <see cref="BypassesBudget" />
        ///   reason; this method does not filter them out itself.
        /// </param>
        /// <param name="recentReleaseDays">The game days on which a letter was already released within recent memory.</param>
        /// <param name="today">The current game day.</param>
        /// <param name="budget">The pacing budget in force (see <see cref="LetterPacingRules" />).</param>
        /// <param name="diversityRoll">
        ///   A 0-99 value the caller supplies (typically from MBRandom); kept out of this method so the
        ///   policy itself stays pure and deterministic given its inputs.
        /// </param>
        public static LetterCandidate SelectNext(
            IReadOnlyList<LetterCandidate> candidates,
            IReadOnlyList<int> recentReleaseDays,
            int today,
            LetterBudget budget,
            int diversityRoll)
        {
            int releasedInWindow = recentReleaseDays == null
                ? 0
                : recentReleaseDays.Count(day => today - day < budget.WindowDays);

            if (releasedInWindow >= budget.MaxPerWindow) return null;

            if (candidates == null || candidates.Count == 0) return null;

            List<LetterCandidate> ordered = candidates
                .OrderByDescending(c => EffectiveWeight(c, today))
                .ThenBy(c => c.EligibleSinceDay)
                .ThenBy(c => c.SenderId, StringComparer.Ordinal)
                .ToList();

            LetterCandidate top = ordered[0];
            int topWeight = EffectiveWeight(top, today);

            if (diversityRoll < budget.DiversityChancePercent)
            {
                LetterCandidate diversityPick = ordered.FirstOrDefault(c => EffectiveWeight(c, today) < topWeight);
                if (diversityPick != null) return diversityPick;
            }

            return top;
        }
    }
}
