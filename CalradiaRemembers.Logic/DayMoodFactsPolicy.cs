// Code written by Gabriel Mailhot, 30/09/2026.
// THE MOOD OF THE DAY, increment 2: what the game says about a character's day, read into MoodFacts (SDK), which the
// draw weighs. ROADMAP Ruling 2: a CONSTRAINED draw, not a random roll; the v2.5.9 knowledge packs already read the
// body (own_body), the purse (house_means) and the hour (here_and_now), so this reuses their bands rather than
// reading the engine again. The realm's fortunes are measured the way the mercenary grid measures them
// (MercenaryDeparturePolicy.IsLosing), so "losing a war" means one thing in the mod.
//
// A personal blow or gladness is recorded on the profile by self-esteem's events (E2); self-esteem itself leans the
// draw (E4): the ground the day stands on.

#region

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
                SelfRegardLow = r.SelfEsteem == SelfEsteemBand.Shaken || r.SelfEsteem == SelfEsteemBand.Broken,
                SelfRegardHigh = r.SelfEsteem == SelfEsteemBand.Assured || r.SelfEsteem == SelfEsteemBand.Proud
            };
        }
    }
}
