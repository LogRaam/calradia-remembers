// Code written by Gabriel Mailhot, 17/10/2026.
// What one character thinks of another, and why (docs/design/NPC_REGARD.md, sections 5 and 9). Gabriel, 17/10/2026,
// on the encyclopedia: a friend of the vanilla ledger was the mod's own bitter enemy, and he ruled that every friend or
// enemy shown carries its reason. This increment serves the conversation: when the player names someone, the speaker is
// told what he holds against them, or owes them, from things that really happened (a kinsman they had executed, a
// grudge, a betrayal written in the campaign's past), instead of a bare vanilla number with no story.
//
// Vanilla relation is not a source: it has no reason, so it could never say why (section 5.2). The caller keeps the
// vanilla label when no trace gives a verdict, so a pair with nothing between them reads as it always did.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Friend, enemy, or neither, by what lies between two characters.</summary>
    public enum PairRegardVerdict
    {
        Neither,
        Friend,
        Enemy
    }

    /// <summary>One thing that happened between the speaker and the other, already weighed and aged.</summary>
    public sealed class PairRegardTrace
    {
        /// <summary>Its signed weight on the speaker's regard, after ageing (minus for a wrong, plus for a kindness).</summary>
        public int Weight { get; init; }

        /// <summary>What happened, from the speaker's side, with how long ago ("had your brother Garios executed (9 years ago)").</summary>
        public string Line { get; init; }

        /// <summary>The player could know of it. When false, the speaker knows it and may keep it to himself.</summary>
        public bool KnownToPlayer { get; init; }

        /// <summary>
        ///   The same thing told about the speaker, for his encyclopedia page ("had Garios, his brother, executed (9 years
        ///   ago)"): the page is read by the player, so it never says "you" of the speaker. Falls back to Line when null.
        /// </summary>
        public string PageLine { get; init; }
    }

    /// <summary>What ties the speaker to the other, and what happened between them.</summary>
    public sealed class PairRegardFacts
    {
        /// <summary>The other is the speaker's spouse.</summary>
        public bool IsSpouse { get; init; }

        /// <summary>The other is the speaker's parent or child.</summary>
        public bool IsParentOrChild { get; init; }

        /// <summary>The other is the speaker's brother or sister.</summary>
        public bool IsSibling { get; init; }

        /// <summary>The two share a house (counted only when they are not kin).</summary>
        public bool SameClan { get; init; }

        /// <summary>Their realms are at war.</summary>
        public bool RealmsAtWar { get; init; }

        /// <summary>What happened between them, each already weighed (see the weight helpers).</summary>
        public IReadOnlyList<PairRegardTrace> Traces { get; init; } = Array.Empty<PairRegardTrace>();

        /// <summary>
        ///   What other mods say lies between them (CrRegard sources), weighed by those mods. Counted, but together never
        ///   more than <see cref="PairRegardPolicy.OutsideCap" /> either way.
        /// </summary>
        public IReadOnlyList<PairRegardTrace> OutsideTraces { get; init; } = Array.Empty<PairRegardTrace>();
    }

    /// <summary>The speaker's regard for the other: points on the regard scale, the verdict, and the reasons, strongest first.</summary>
    public sealed class PairRegard
    {
        public int Points { get; init; }
        public PairRegardVerdict Verdict { get; init; }

        /// <summary>The traces, the ones that agree with the verdict first, strongest first.</summary>
        public IReadOnlyList<PairRegardTrace> Reasons { get; init; } = Array.Empty<PairRegardTrace>();
    }

    /// <summary>Weighs what lies between two characters into a verdict with its reason.</summary>
    public static class PairRegardPolicy
    {
        // Ties alone never make a friend or an enemy (section 5.2): every one sits below the Friend line, so a brother
        // stays family, not a friend, unless something happened between them.
        public const int SpouseWeight = 15;
        public const int KinWeight = 10;
        public const int ClanWeight = 5;
        public const int AtWarWeight = -5;

        // What happened, before ageing (proposals of section 5.2, for Gabriel to tune).
        public const int KinExecutedWeight = -70;
        public const int KinKilledInBattleWeight = -45;
        public const int WrongedInThePastWeight = -40;
        public const int TakenCaptiveWeight = -30;
        public const int BeatenInBattleWeight = -15;
        public const int HelpedInThePastWeight = 30;

        /// <summary>
        ///   What other mods say counts, together, for at most this much either way: as much as a betrayal written in the
        ///   past, less than a kinsman executed. tashmetu (18/10/2026): R&amp;S's years of battles side by side showed as
        ///   "neither" or "enemy" beside its own tooltip; another mod may tip a pair, never drown what happened here.
        /// </summary>
        public const int OutsideCap = 40;

        /// <summary>A wrong or a kindness weighs half as much after each this many years (an execution never fades).</summary>
        public const int HalfLifeYears = 10;

        /// <summary>A defeat in battle fades faster: half every this many years.</summary>
        public const int BattleHalfLifeYears = 5;

        /// <summary>A kinsman the other had executed: it never fades.</summary>
        public static int KinExecuted() => KinExecutedWeight;

        /// <summary>A kinsman the other killed in battle, aged.</summary>
        public static int KinKilledInBattle(int yearsAgo) => Aged(KinKilledInBattleWeight, yearsAgo, HalfLifeYears);

        /// <summary>The other wronged the speaker (betrayal, feud, a scheme, a past written that way), aged.</summary>
        public static int WrongedInThePast(int yearsAgo) => Aged(WrongedInThePastWeight, yearsAgo, HalfLifeYears);

        /// <summary>The other took the speaker captive, aged.</summary>
        public static int TakenCaptive(int yearsAgo) => Aged(TakenCaptiveWeight, yearsAgo, HalfLifeYears);

        /// <summary>The other beat the speaker in battle, aged faster.</summary>
        public static int BeatenInBattle(int yearsAgo) => Aged(BeatenInBattleWeight, yearsAgo, BattleHalfLifeYears);

        /// <summary>The other did the speaker a kindness or a rescue, aged.</summary>
        public static int HelpedInThePast(int yearsAgo) => Aged(HelpedInThePastWeight, yearsAgo, HalfLifeYears);

        /// <summary>Halved once per full half-life elapsed.</summary>
        public static int Aged(int weight, int yearsAgo, int halfLifeYears)
        {
            if (yearsAgo <= 0 || halfLifeYears <= 0) return weight;

            int halvings = Math.Min(30, yearsAgo / halfLifeYears);

            return weight / (1 << halvings);
        }

        /// <summary>The speaker's regard for the other.</summary>
        public static PairRegard Assess(PairRegardFacts f)
        {
            if (f == null) return new PairRegard();

            IReadOnlyList<PairRegardTrace> own = f.Traces ?? Array.Empty<PairRegardTrace>();
            IReadOnlyList<PairRegardTrace> outside = (f.OutsideTraces ?? Array.Empty<PairRegardTrace>()).Where(t => t != null).ToList();
            var ownSet = new HashSet<PairRegardTrace>(own);
            List<PairRegardTrace> traces = own.Concat(outside).ToList();
            int outsideSum = Math.Max(-OutsideCap, Math.Min(OutsideCap, outside.Sum(t => Math.Max(-OutsideCap, Math.Min(OutsideCap, t.Weight)))));

            int ties = f.IsSpouse ? SpouseWeight
                     : f.IsParentOrChild || f.IsSibling ? KinWeight
                     : f.SameClan ? ClanWeight
                     : 0;

            int sum = ties + (f.RealmsAtWar ? AtWarWeight : 0) + own.Sum(t => t.Weight) + outsideSum;
            int points = Math.Max(-100, Math.Min(100, sum));

            PairRegardVerdict verdict = traces.Count == 0 ? PairRegardVerdict.Neither
                                      : points >= Regard.WarmTier ? PairRegardVerdict.Friend
                                      : points <= Regard.WaryTier ? PairRegardVerdict.Enemy
                                      : PairRegardVerdict.Neither;

            int sign = verdict == PairRegardVerdict.Enemy ? -1 : 1;
            List<PairRegardTrace> reasons = traces.Where(t => t.Weight != 0 && !string.IsNullOrWhiteSpace(t.Line))
                                                  .OrderByDescending(t => Math.Sign(t.Weight) == sign)
                                                  .ThenByDescending(t => Math.Min(Math.Abs(t.Weight), ownSet.Contains(t) ? int.MaxValue : OutsideCap))
                                                  .ThenByDescending(t => ownSet.Contains(t))
                                                  .ToList();

            return new PairRegard {Points = points, Verdict = verdict, Reasons = reasons};
        }

        /// <summary>
        ///   The reason an encyclopedia page may show for this verdict, or null when the page must not list the pair:
        ///   only a reason the player could know is shown, the strongest that agrees with the verdict (NPC_REGARD.md
        ///   5.3: listing an enemy with a reason the player may not read is a leak or an unverifiable claim).
        /// </summary>
        public static string PageReason(PairRegard r)
        {
            if (r == null || r.Verdict == PairRegardVerdict.Neither) return null;

            int sign = r.Verdict == PairRegardVerdict.Enemy ? -1 : 1;
            PairRegardTrace shown = r.Reasons.FirstOrDefault(t => t.KnownToPlayer && Math.Sign(t.Weight) == sign);

            return shown == null ? null : (shown.PageLine ?? shown.Line).Trim().TrimEnd('.');
        }

        /// <summary>
        ///   The page reason without the pair named again when it opens on both of them ("Halthdar and Hertogi rode
        ///   together against raiders" reads "rode together against raiders" after "Hertogi, friend:"). tashmetu
        ///   (18/10/2026): the page's own lord need not be named again. Only that opening is cut: a reason where one of
        ///   them acts on the other ("Hertogi insulted Halthdar at a feast") keeps both names, since who did what matters.
        /// </summary>
        public static string WithoutThePair(string reason, string oneName, string otherName)
        {
            if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(oneName) || string.IsNullOrWhiteSpace(otherName)) return reason;

            foreach (string opening in new[] {$"{oneName} and {otherName} ", $"{otherName} and {oneName} "})
                if (reason.StartsWith(opening, StringComparison.Ordinal) && reason.Length > opening.Length)
                    return reason.Substring(opening.Length);

            return reason;
        }

        /// <summary>
        ///   How the speaker is told of it, or null when there is no verdict: "an enemy, for what lies between you: had
        ///   your brother Garios executed (9 years ago)", with the strongest opposite reason when it is at least half as
        ///   strong, and a note when the player could not know the main reason. The speaker knows his own grudges; he
        ///   may keep one to himself, never deny it.
        /// </summary>
        public static string Describe(PairRegard r)
        {
            if (r == null || r.Verdict == PairRegardVerdict.Neither || r.Reasons.Count == 0) return null;

            PairRegardTrace main = r.Reasons[0];
            string what = r.Verdict == PairRegardVerdict.Enemy ? "an enemy" : "a friend";
            string line = $"{what}, for what lies between you: {main.Line.Trim().TrimEnd('.')}";

            PairRegardTrace against = r.Reasons.Skip(1).FirstOrDefault(t => Math.Sign(t.Weight) != Math.Sign(main.Weight));
            if (against != null && Math.Abs(against.Weight) * 2 >= Math.Abs(main.Weight))
                line += $"; though {against.Line.Trim().TrimEnd('.')}";

            if (!main.KnownToPlayer) line += " (a matter the player may not know of; you may keep it to yourself)";

            return line;
        }
    }
}
