// Code written by Gabriel Mailhot, 01/08/2026.
// The mailbox scheduler itself: the gate that stands between "an NPC has a reason to write" and "a letter
// actually reaches a 100h player who may have dozens of correspondents at once." Until 31/10/2026 it released one letter
// a day under a budget. The letter box changed that (Gabriel, 30/10/2026: "plus besoin de reduire la frequence ou le nombre
// de lettres, c'est le joueur qui classe, elimine, abonne et desabonne"; 31/10: step 4 "sans reglage pour le moment"): a
// letter now costs nothing until opened and waits unopened, so every letter ready today is sent, one per sender.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Which of today's ready letters are sent: every one, at most one per sender (a sender's own wait between letters
    ///   then holds the rest), the weightiest of each sender's first. Pure over its inputs.
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
        ///   correspondence-interest dampers never cool these either (a thrown or acknowledged one teaches nothing).
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
        ///   The letters sent today: one per sender, the one of highest <see cref="EffectiveWeight" /> (oldest eligible, then
        ///   reason, on a tie), listed with the awaited correspondents (the ribbon) first, then by weight. Deterministic.
        /// </summary>
        public static IReadOnlyList<LetterCandidate> ReleasedToday(IEnumerable<LetterCandidate> candidates, int today)
        {
            if (candidates == null) return new List<LetterCandidate>();

            return candidates
                .Where(c => c != null)
                .GroupBy(c => c.SenderId ?? "", StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(c => EffectiveWeight(c, today)).ThenBy(c => c.EligibleSinceDay).ThenBy(c => (int)c.Reason).First())
                .OrderByDescending(c => c.Awaited)
                .ThenByDescending(c => EffectiveWeight(c, today))
                .ThenBy(c => c.SenderId, StringComparer.Ordinal)
                .ToList();
        }
    }
}
