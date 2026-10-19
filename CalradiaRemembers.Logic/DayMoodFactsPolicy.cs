// Code written by Gabriel Mailhot, 30/09/2026.
// THE MOOD OF THE DAY, increment 2: what the game says about a character's day, read into MoodFacts (SDK), which the
// draw weighs. ROADMAP Ruling 2: a CONSTRAINED draw, not a random roll; the v2.5.9 knowledge packs already read the
// body (own_body), the purse (house_means) and the hour (here_and_now), so this reuses their bands rather than
// reading the engine again. The realm's fortunes are measured the way the mercenary grid measures them
// (MercenaryDeparturePolicy.IsLosing), so "losing a war" means one thing in the mod.
//
// A personal blow or gladness is recorded on the profile by self-esteem's events (E2); self-esteem itself leans the
// draw (E4): the ground the day stands on.
//
// fkasad (19/10/2026): "the game, CR and the model just can't see the opportunities that a new situation offers but the
// player can", and a player cannot answer "the war is going badly". When the war is the day's cause it is given in the
// host's words: against whom, and how far behind or ahead (WarWhat), so the player has something true to argue with.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.Mood;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What the host reads off the live game for a character's day. Every field is optional.</summary>
    public sealed class DayMoodReadings
    {
        /// <summary>The body, as the own_body knowledge pack banded it.</summary>
        public HurtBand Hurt { get; set; } = HurtBand.Unknown;

        /// <summary>A permanent maiming (own_body).</summary>
        public bool IsMaimed { get; set; }

        /// <summary>The house's purse, as house_means banded it.</summary>
        public TreasuryBand Treasury { get; set; } = TreasuryBand.Unknown;

        /// <summary>Their realm is at war.</summary>
        public bool RealmAtWar { get; set; }

        /// <summary>Their realm's total strength.</summary>
        public float RealmStrength { get; set; }

        /// <summary>The combined strength of the realms at war with theirs.</summary>
        public float EnemiesStrength { get; set; }

        /// <summary>The names of the realms at war with theirs, as the game shows them; empty when none or unknown.</summary>
        public IReadOnlyList<string> EnemyRealmNames { get; set; } = new string[0];

        /// <summary>Inside a town or castle (a hall), not a village or the open field.</summary>
        public bool InTownOrCastle { get; set; }

        /// <summary>That town or castle is under siege.</summary>
        public bool UnderSiege { get; set; }

        /// <summary>The hour, 0-23.</summary>
        public int HourOfDay { get; set; } = 12;

        /// <summary>Held prisoner.</summary>
        public bool IsPrisoner { get; set; }

        /// <summary>A blow of his own lately (a fief lost, a death in the house, a defeat, a captivity).</summary>
        public bool RecentBlow { get; set; }

        /// <summary>A gladness of his own lately (a fief gained, a tournament or a battle won).</summary>
        public bool RecentGladness { get; set; }

        /// <summary>What the recent blow was; null when unknown (a save from before it was kept).</summary>
        public SelfEsteemEvent? RecentBlowKind { get; set; }

        /// <summary>Whom or what the blow concerned ("Pravend", "your brother Olek"); null when none.</summary>
        public string RecentBlowSubject { get; set; }

        /// <summary>What the recent gladness was; null when unknown.</summary>
        public SelfEsteemEvent? RecentGladnessKind { get; set; }

        /// <summary>Whom or what the gladness concerned ("Lageta"); null when none.</summary>
        public string RecentGladnessSubject { get; set; }

        /// <summary>How he regards himself lately; null when unknown.</summary>
        public SelfEsteemBand? SelfEsteem { get; set; }
    }

    /// <summary>Reads a character's day into the facts the mood draw weighs. Pure.</summary>
    public static class DayMoodFactsPolicy
    {
        /// <summary>The small hours end here: from midnight up to (not including) this hour it is deep night.</summary>
        public const int DeepNightEndsAtHour = 5;

        /// <summary>The facts the mood of the day weighs, from what the host read.</summary>
        public static MoodFacts From(DayMoodReadings r)
        {
            if (r == null) return new MoodFacts();

            bool losing = r.RealmAtWar && MercenaryDeparturePolicy.IsLosing(r.RealmStrength, r.EnemiesStrength);
            // The mirror of IsLosing: the enemies are the ones losing by the same measure.
            bool winning = r.RealmAtWar && MercenaryDeparturePolicy.IsLosing(r.EnemiesStrength, r.RealmStrength);

            return new MoodFacts {
                IsHurt = r.Hurt == HurtBand.Wounded || r.Hurt == HurtBand.Grievous,
                IsMaimed = r.IsMaimed,
                CoffersAreEmpty = r.Treasury == TreasuryBand.Empty,
                RealmHasLatelyLost = losing,
                RealmHasLatelyWon = winning,
                AtEase = r.InTownOrCastle && !r.UnderSiege,
                IsDeepNight = r.HourOfDay >= 0 && r.HourOfDay < DeepNightEndsAtHour,
                IsCaptive = r.IsPrisoner,
                PersonalBlow = r.RecentBlow,
                PersonalGladness = r.RecentGladness,
                PersonalBlowWhat = r.RecentBlow ? BlowWhat(r.RecentBlowKind, r.RecentBlowSubject) : null,
                PersonalGladnessWhat = r.RecentGladness ? GladnessWhat(r.RecentGladnessKind, r.RecentGladnessSubject) : null,
                RealmWarWhat = WarWhat(r),
                SelfRegardLow = r.SelfEsteem == SelfEsteemBand.Shaken || r.SelfEsteem == SelfEsteemBand.Broken,
                SelfRegardHigh = r.SelfEsteem == SelfEsteemBand.Assured || r.SelfEsteem == SelfEsteemBand.Proud
            };
        }

        /// <summary>
        ///   A blow, from his side, for the day's cause (Lordfadooboo, 18/10/2026; Gabriel: the real cause is always
        ///   named). Null when its kind was never kept: the SDK then names it vaguely, never wrongly.
        /// </summary>
        public static string BlowWhat(SelfEsteemEvent? kind, string subject)
        {
            bool named = !string.IsNullOrWhiteSpace(subject);
            switch (kind)
            {
                case SelfEsteemEvent.FiefLost: return named ? $"the loss of {subject.Trim()}" : "the loss of a fief of yours";
                case SelfEsteemEvent.KinDied: return named ? $"the death of {subject.Trim()}" : "a death in your family";
                case SelfEsteemEvent.Captured: return named ? $"your capture by {subject.Trim()}" : "your recent captivity";
                case SelfEsteemEvent.BattleLost: return "a battle you lost these last days";
                default: return null;
            }
        }

        /// <summary>
        ///   His realm's war, from his side, when it is going badly or well by the mod's one measure of losing
        ///   (<see cref="MercenaryDeparturePolicy.IsLosing" />): against whom, and how far behind or ahead (fkasad,
        ///   19/10/2026). Null when the war is neither, or its enemies are not named: the SDK then says it plainly.
        /// </summary>
        public static string WarWhat(DayMoodReadings r)
        {
            if (r == null || !r.RealmAtWar) return null;

            List<string> names = (r.EnemyRealmNames ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (names.Count == 0) return null;

            bool losing = MercenaryDeparturePolicy.IsLosing(r.RealmStrength, r.EnemiesStrength);
            bool winning = MercenaryDeparturePolicy.IsLosing(r.EnemiesStrength, r.RealmStrength);
            if (!losing && !winning) return null;

            string against = Join(names);
            float stronger = losing ? r.EnemiesStrength : r.RealmStrength;
            float weaker = losing ? r.RealmStrength : r.EnemiesStrength;
            string band = StrengthBand(weaker <= 0 ? float.MaxValue : stronger / weaker);

            if (losing)
            {
                string whose = names.Count == 1 ? names[0] + "'s hosts are" : "their hosts together are";

                return "your realm's war against " + against + " going badly: " + whose + " " + band + " your realm's";
            }

            string theirs = names.Count == 1 ? names[0] + "'s" : "theirs together";

            return "your realm's war against " + against + " going well: your realm's hosts are " + band + " " + theirs;
        }

        // How much the stronger side outweighs the weaker, in words: the edge IsLosing already found, then two steps.
        private static string StrengthBand(float ratio)
            => ratio < 1.5f ? "stronger than" : ratio < 2.5f ? "much stronger than" : "several times stronger than";

        private static string Join(List<string> names)
        {
            if (names.Count == 1) return names[0];
            if (names.Count > 3) return string.Join(", ", names.Take(3)) + " and others";

            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }

        /// <summary>A gladness, from his side, for the day's cause. Null when its kind was never kept.</summary>
        public static string GladnessWhat(SelfEsteemEvent? kind, string subject)
        {
            bool named = !string.IsNullOrWhiteSpace(subject);
            switch (kind)
            {
                case SelfEsteemEvent.FiefGained: return named ? $"{subject.Trim()} newly yours" : "a fief newly yours";
                case SelfEsteemEvent.TournamentWon: return named ? $"the tournament you won at {subject.Trim()}" : "a tournament you won";
                case SelfEsteemEvent.BattleWon: return "a battle you won these last days";
                case SelfEsteemEvent.BattleWonAgainstOdds: return "a battle you won against the odds";
                default: return null;
            }
        }
    }
}
