// Code written by Gabriel Mailhot, 17/10/2026.
// The generated shared past of two lords (docs/design/NPC_REGARD.md section 7). Gabriel's ruling: a pair with nothing
// between them may receive a past, drawn once and true forever after, and a good share get nothing; on 17/10/2026 he
// ruled that it is written into the world's long-term memory (7.7a), and that vanilla relation biases the draw (a pair
// at a warm vanilla relation cannot draw a quarrel, and the reverse). Everything is decided here from facts the game
// holds now and a stable seed of the pair, so the same pair always draws the same past; the engine persists the
// outcome, nothing included, so a pair is never re-rolled when its facts change during the campaign.
//
// What it never claims (7.6): a death, a marriage, a child, a fief's owner or conquest, a realm allegiance, a third
// person. It is dated before the campaign began, when the player saw nothing, and both were adults then.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What two lords may have shared before the campaign (design 7.3). Values are only ever appended.</summary>
    public enum SharedPastKind
    {
        None,
        BoundaryQuarrel,
        UnforgivenInsult,
        OldFriendship,
        RodeTogether,
        Rescue,
        UnpaidDebt,
        TourneyRivalry,
        RefusedSuit
    }

    /// <summary>One side of the pair, as the game knows them now.</summary>
    public sealed class SharedPastSide
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public bool IsFemale { get; init; }

        /// <summary>Birth, as an absolute day (CampaignTime.ToDays).</summary>
        public int BirthDay { get; init; }

        public int Valor { get; init; }
        public int Generosity { get; init; }

        /// <summary>Has never married (no spouse now, none before): a refused suit cannot contradict a marriage.</summary>
        public bool NeverMarried { get; init; }

        /// <summary>Is a lord (a tourney among lords).</summary>
        public bool IsLord { get; init; }
    }

    /// <summary>The pair and the facts its draw reads.</summary>
    public sealed class SharedPastPairFacts
    {
        public SharedPastSide A { get; init; }
        public SharedPastSide B { get; init; }

        /// <summary>Their houses hold fiefs near each other.</summary>
        public bool Neighbours { get; init; }

        /// <summary>CompanionRapport.Compatibility of their traits, about -9..+9.</summary>
        public int Compatibility { get; init; }

        public bool SameRealm { get; init; }
        public bool SameCulture { get; init; }

        /// <summary>Vanilla relation between them: a warm one forbids a quarrel, a bitter one a friendship.</summary>
        public int VanillaRelation { get; init; }

        /// <summary>The town the past is placed near (never an owner, never a conquest).</summary>
        public string PlaceName { get; init; }

        /// <summary>The settlement id of that town, for the record.</summary>
        public string PlaceId { get; init; }

        /// <summary>The campaign's first day, absolute.</summary>
        public int CampaignStartDay { get; init; }

        public int DaysInYear { get; init; }

        /// <summary>The age at which a hero comes of age (AgeModel.HeroComesOfAge).</summary>
        public int ComesOfAgeYears { get; init; }
    }

    /// <summary>A drawn past: who stands first (the one the kind's first weight applies to), when, and its line.</summary>
    public sealed class SharedPastDraw
    {
        public SharedPastKind Kind { get; init; }
        public string FirstId { get; init; }
        public string SecondId { get; init; }
        public int Day { get; init; }
        public string Summary { get; init; }
    }

    /// <summary>The kind of shared past, the two people and the place.</summary>
    public sealed class SharedPastNames
    {
        /// <summary>The kind.</summary>
        public SharedPastKind Kind { get; init; }

        /// <summary>The first.</summary>
        public string First { get; init; }

        /// <summary>The second.</summary>
        public string Second { get; init; }

        /// <summary>The place.</summary>
        public string Place { get; init; }
    }

    /// <summary>Draws, dates, words and weighs the shared past of two lords.</summary>
    public static class SharedPastPolicy
    {
        /// <summary>Chance in a hundred that anything happened at all ("a good share get nothing").</summary>
        public const int BaseChance = 30;

        /// <summary>Added when the pair fits two kinds or more.</summary>
        public const int WellMatchedBonus = 10;

        /// <summary>The past lies between one and this many years before the campaign.</summary>
        public const int MaxYearsBefore = 20;

        /// <summary>Compatibility at or below this: an insult that was never forgiven fits.</summary>
        public const int OpposedTraits = -4;

        /// <summary>Compatibility at or above this: an old friendship fits.</summary>
        public const int AlikeTraits = 4;

        /// <summary>An old friendship is between lords no more than this many years apart.</summary>
        public const int FriendsAgeGap = 10;

        /// <summary>A generated past weighs full for this many years before it starts to fade.</summary>
        public const int FullWeightYears = 2;

        /// <summary>A quarrel, an insult, a debt or a rivalry halves every this many years after that.</summary>
        public const int GrievanceHalfLife = 8;

        /// <summary>A friendship or a rescue halves every this many years after that.</summary>
        public const int BondHalfLife = 15;

        /// <summary>Its magnitude in the long-term memory: known to the two, their kin, their houses and the place.</summary>
        public const int Magnitude = 30;

        /// <summary>The status a drawn past carries in the world's memory ("shared_past_" + kind).</summary>
        public const string StatusPrefix = "shared_past_";

        /// <summary>The kind a status names, or None.</summary>
        public static SharedPastKind KindOf(string status)
            => status != null && status.StartsWith(StatusPrefix, StringComparison.Ordinal)
               && Enum.TryParse(status.Substring(StatusPrefix.Length), out SharedPastKind k)
                ? k
                : SharedPastKind.None;

        /// <summary>The seed of a pair: the same two lords always draw the same past ("v1" leaves room for a new catalog).</summary>
        public static string Seed(string a, string b)
            => (string.CompareOrdinal(a ?? "", b ?? "") <= 0 ? $"{a}|{b}" : $"{b}|{a}") + "|past|v1";

        /// <summary>The past this pair draws, or null when nothing happened between them (or nothing could have).</summary>
        public static SharedPastDraw Draw(SharedPastPairFacts f)
        {
            if (f?.A == null || f.B == null || string.IsNullOrEmpty(f.A.Id) || string.IsNullOrEmpty(f.B.Id) || f.DaysInYear <= 0) return null;

            string seed = Seed(f.A.Id, f.B.Id);
            List<(SharedPastKind Kind, SharedPastSide First, SharedPastSide Second)> fits = Fitting(f, seed);
            if (fits.Count == 0) return null;

            int chance = BaseChance + (fits.Count >= 2 ? WellMatchedBonus : 0);
            if (Roll(seed) >= chance) return null;

            int latest = LatestYearsBefore(f);
            if (latest < 1) return null; // the younger came of age after the campaign began: they shared nothing then

            var pick = fits[Roll(seed + "|kind") % fits.Count];
            int years = 1 + Roll(seed + "|years") % latest;

            return new SharedPastDraw {
                Kind = pick.Kind, FirstId = pick.First.Id, SecondId = pick.Second.Id,
                Day = f.CampaignStartDay - years * f.DaysInYear,
                Summary = Summary(pick.Kind, pick.First.Name, pick.Second.Name, f.PlaceName)
            };
        }

        /// <summary>
        ///   The weight of a past on one side's regard for the other, aged from today (design 7.5): full for two years,
        ///   then halved every 8 years for a grievance and every 15 for a bond.
        /// </summary>
        public static int WeightToward(SharedPastKind kind, bool isFirst, int yearsAgo)
        {
            (int first, int second) = kind switch
            {
                SharedPastKind.BoundaryQuarrel => (-30, -30),
                SharedPastKind.UnforgivenInsult => (-35, -20), // the first was insulted by the second
                SharedPastKind.OldFriendship => (35, 35),
                SharedPastKind.RodeTogether => (25, 25),
                SharedPastKind.Rescue => (45, 15), // the first was rescued by the second
                SharedPastKind.UnpaidDebt => (-25, -10), // the first lent, the second never repaid
                SharedPastKind.TourneyRivalry => (-20, -20),
                SharedPastKind.RefusedSuit => (-20, 0), // the first asked, the second refused
                _ => (0, 0)
            };

            int weight = isFirst ? first : second;
            int halfLife = weight > 0 ? BondHalfLife : GrievanceHalfLife;
            int fading = Math.Max(0, yearsAgo - FullWeightYears);

            return PairRegardPolicy.Aged(new RegardAgeing {Weight = weight, YearsAgo = fading, HalfLifeYears = halfLife});
        }

        /// <summary>The line, naming both and the town it happened near, never anything the world could contradict.</summary>
        public static string Summary(SharedPastNames facts) => Summary(facts.Kind, facts.First, facts.Second, facts.Place);

        // The positional form behind the facts, private so no caller can pass two of its same-typed arguments the wrong
        // way round and still compile (swap-risk ratchet, 14/11/2026).
        private static string Summary(SharedPastKind kind, string first, string second, string place)
        {
            string near = string.IsNullOrWhiteSpace(place) ? "" : $" near {place}";
            string at = string.IsNullOrWhiteSpace(place) ? "" : $" at {place}";

            return kind switch
            {
                SharedPastKind.BoundaryQuarrel => $"{first} and {second} quarrelled over grazing land{near}.",
                SharedPastKind.UnforgivenInsult => $"{second} insulted {first} at a feast{at}, and {first} never forgot it.",
                SharedPastKind.OldFriendship => $"{first} and {second} have been friends since they were young{near}.",
                SharedPastKind.RodeTogether => $"{first} and {second} rode together against raiders{near}.",
                SharedPastKind.Rescue => $"{second} saved {first}'s life in a skirmish{near}.",
                SharedPastKind.UnpaidDebt => $"{first} lent {second} a sum that was never repaid.",
                SharedPastKind.TourneyRivalry => $"{first} and {second} met in the lists{at}, and neither has forgotten it.",
                SharedPastKind.RefusedSuit => $"{first} asked for {second}'s hand and was refused.",
                _ => null
            };
        }

        #region private

        private static int Roll(string seed) => FirstImpressionPolicy.StableRoll(seed);

        // The kinds whose conditions hold now (design 7.3), each with its sides oriented, less those the vanilla relation
        // forbids (a warm pair draws no grievance, a bitter one no bond).
        private static List<(SharedPastKind, SharedPastSide, SharedPastSide)> Fitting(SharedPastPairFacts f, string seed)
        {
            var fits = new List<(SharedPastKind, SharedPastSide, SharedPastSide)>();
            SharedPastSide a = f.A, b = f.B;
            SharedPastSide younger = a.BirthDay >= b.BirthDay ? a : b;
            SharedPastSide elder = younger == a ? b : a;
            bool flip = Roll(seed + "|side") % 2 == 1;
            SharedPastSide one = flip ? b : a, other = flip ? a : b;

            if (f.Neighbours) fits.Add((SharedPastKind.BoundaryQuarrel, one, other));
            if (f.Compatibility <= OpposedTraits && (f.SameRealm || f.Neighbours)) fits.Add((SharedPastKind.UnforgivenInsult, one, other));
            if (f.Compatibility >= AlikeTraits && f.SameCulture && Math.Abs(a.BirthDay - b.BirthDay) <= FriendsAgeGap * f.DaysInYear)
                fits.Add((SharedPastKind.OldFriendship, one, other));
            if (f.SameRealm && f.SameCulture) fits.Add((SharedPastKind.RodeTogether, one, other));
            if (elder.Valor >= 1) fits.Add((SharedPastKind.Rescue, younger, elder));
            if (other.Generosity <= 0) fits.Add((SharedPastKind.UnpaidDebt, one, other));
            else if (one.Generosity <= 0) fits.Add((SharedPastKind.UnpaidDebt, other, one));
            if (a.IsLord && b.IsLord && a.Valor >= 0 && b.Valor >= 0) fits.Add((SharedPastKind.TourneyRivalry, one, other));
            if (a.IsFemale != b.IsFemale && a.NeverMarried && b.NeverMarried) fits.Add((SharedPastKind.RefusedSuit, one, other));

            bool warm = f.VanillaRelation >= global::NpcMemoryService.Core.Standing.VanillaRelation.CloseTier;
            bool bitter = f.VanillaRelation <= global::NpcMemoryService.Core.Standing.VanillaRelation.BitterTier;

            return fits.Where(x => !(warm && WeightToward(x.Item1, true, 0) < 0) && !(bitter && WeightToward(x.Item1, true, 0) > 0)).ToList();
        }

        // How many years before the campaign the younger was already of age (at most MaxYearsBefore).
        private static int LatestYearsBefore(SharedPastPairFacts f)
        {
            int youngerBirth = Math.Max(f.A.BirthDay, f.B.BirthDay);
            int cameOfAge = youngerBirth + f.ComesOfAgeYears * f.DaysInYear;

            return Math.Min(MaxYearsBefore, (f.CampaignStartDay - cameOfAge) / f.DaysInYear);
        }

        #endregion
    }
}
