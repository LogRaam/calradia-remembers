// Code written by Gabriel Mailhot, 30/09/2026.
// The mood of the day, increment 2: what the game says about a character's day, read into the facts the draw
// weighs (ROADMAP, THE MOOD OF THE DAY, Ruling 2: a constrained draw, never a random roll; "a lord who has just
// lost a fief must not wake up cheerful"). If a reading is wrong here, a wounded lord at a feast draws high
// spirits, or a captive draws an ordinary day, which is the incoherence the whole feature exists to avoid.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.Mood;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class DayMoodFactsPolicyTests
    {
        private static MoodFacts Read(DayMoodReadings r) => DayMoodFactsPolicy.From(r);

        // A wound that stops a man (the knowledge pack's Wounded or Grievous) weighs on his day; a scratch does not.
        [Test]
        public void GIVEN_a_real_wound_WHEN_the_day_is_read_THEN_he_is_hurt_and_a_scratch_is_not()
        {
            Read(new DayMoodReadings {Hurt = HurtBand.Wounded}).IsHurt.Should().BeTrue();
            Read(new DayMoodReadings {Hurt = HurtBand.Grievous}).IsHurt.Should().BeTrue();
            Read(new DayMoodReadings {Hurt = HurtBand.Scratched}).IsHurt.Should().BeFalse();
        }

        // Ruling 2 names house_means: nobody is open-handed with empty coffers.
        [Test]
        public void GIVEN_an_empty_treasury_WHEN_read_THEN_the_coffers_are_empty()
        {
            Read(new DayMoodReadings {Treasury = TreasuryBand.Empty}).CoffersAreEmpty.Should().BeTrue();
            Read(new DayMoodReadings {Treasury = TreasuryBand.Strained}).CoffersAreEmpty.Should().BeFalse();
        }

        // The realm's war, measured the way the mercenary grid already measures it (MercenaryDeparturePolicy.IsLosing):
        // a realm losing its war weighs on its lords, one winning it lifts them.
        [Test]
        public void GIVEN_a_realm_losing_its_war_WHEN_read_THEN_it_has_lately_lost_and_not_won()
        {
            MoodFacts f = Read(new DayMoodReadings {RealmAtWar = true, RealmStrength = 50f, EnemiesStrength = 100f});

            f.RealmHasLatelyLost.Should().BeTrue();
            f.RealmHasLatelyWon.Should().BeFalse();
        }

        // The mirror: the enemies are the ones losing by the same measure.
        [Test]
        public void GIVEN_a_realm_winning_its_war_WHEN_read_THEN_it_has_lately_won()
        {
            Read(new DayMoodReadings {RealmAtWar = true, RealmStrength = 100f, EnemiesStrength = 50f}).RealmHasLatelyWon.Should().BeTrue();
        }

        // A war nobody is clearly winning moves nobody's day; nor does peace.
        [Test]
        public void GIVEN_an_even_war_or_peace_WHEN_read_THEN_the_realm_neither_lost_nor_won()
        {
            MoodFacts even = Read(new DayMoodReadings {RealmAtWar = true, RealmStrength = 100f, EnemiesStrength = 100f});
            MoodFacts peace = Read(new DayMoodReadings {RealmAtWar = false, RealmStrength = 10f, EnemiesStrength = 0f});

            (even.RealmHasLatelyLost || even.RealmHasLatelyWon).Should().BeFalse();
            (peace.RealmHasLatelyLost || peace.RealmHasLatelyWon).Should().BeFalse();
        }

        // "A feast, a hall at leisure, not a camp at war": inside a town or castle that is not under siege.
        [Test]
        public void GIVEN_a_hall_not_under_siege_WHEN_read_THEN_he_is_at_ease_and_in_the_field_or_a_besieged_hall_he_is_not()
        {
            Read(new DayMoodReadings {InTownOrCastle = true}).AtEase.Should().BeTrue();
            Read(new DayMoodReadings {InTownOrCastle = true, UnderSiege = true}).AtEase.Should().BeFalse();
            Read(new DayMoodReadings {InTownOrCastle = false}).AtEase.Should().BeFalse();
        }

        // "Nobody is expansive at three in the morning": midnight to before five.
        [Test]
        public void GIVEN_the_small_hours_WHEN_read_THEN_it_is_deep_night_and_the_evening_is_not()
        {
            Read(new DayMoodReadings {HourOfDay = 3}).IsDeepNight.Should().BeTrue();
            Read(new DayMoodReadings {HourOfDay = 22}).IsDeepNight.Should().BeFalse();
        }

        // A captive is not having a mood, he is having a captivity.
        [Test]
        public void GIVEN_a_prisoner_WHEN_read_THEN_he_is_captive()
        {
            Read(new DayMoodReadings {IsPrisoner = true}).IsCaptive.Should().BeTrue();
        }

        // A blow of his own lately (a fief lost, a death in the house) or a gladness (a fief gained, a tournament won),
        // recorded by self-esteem's events on his profile.
        [Test]
        public void GIVEN_a_recent_blow_or_gladness_WHEN_read_THEN_the_day_carries_it()
        {
            Read(new DayMoodReadings {RecentBlow = true}).PersonalBlow.Should().BeTrue();
            Read(new DayMoodReadings {RecentGladness = true}).PersonalGladness.Should().BeTrue();
        }

        // STAKE (Lordfadooboo, 18/10/2026; Gabriel: the real cause is always named): the day says what the blow was
        // and whom it concerned, from his side, so "what troubles you?" has a true answer.
        [Test]
        public void GIVEN_a_recent_blow_of_a_known_kind_WHEN_read_THEN_the_day_says_what_it_was()
        {
            Read(new DayMoodReadings {RecentBlow = true, RecentBlowKind = SelfEsteemEvent.FiefLost, RecentBlowSubject = "Pravend"})
                .PersonalBlowWhat.Should().Be("the loss of Pravend");
            Read(new DayMoodReadings {RecentBlow = true, RecentBlowKind = SelfEsteemEvent.KinDied, RecentBlowSubject = "your brother Olek"})
                .PersonalBlowWhat.Should().Be("the death of your brother Olek");
            Read(new DayMoodReadings {RecentGladness = true, RecentGladnessKind = SelfEsteemEvent.TournamentWon, RecentGladnessSubject = "Lageta"})
                .PersonalGladnessWhat.Should().Be("the tournament you won at Lageta");
        }

        // STAKE: a blow kept before its kind was (an old save), or one with no subject, is named vaguely or by its kind,
        // never invented; a blow too old to weigh on the day says nothing at all.
        [Test]
        public void GIVEN_a_blow_without_its_kind_or_subject_or_no_longer_recent_WHEN_read_THEN_nothing_is_invented()
        {
            Read(new DayMoodReadings {RecentBlow = true}).PersonalBlowWhat.Should().BeNull();
            Read(new DayMoodReadings {RecentBlow = true, RecentBlowKind = SelfEsteemEvent.FiefLost}).PersonalBlowWhat.Should().Be("the loss of a fief of yours");
            Read(new DayMoodReadings {RecentBlow = false, RecentBlowKind = SelfEsteemEvent.FiefLost, RecentBlowSubject = "Pravend"})
                .PersonalBlowWhat.Should().BeNull();
        }

        // Self-esteem shaken or broken reads low; assured or proud reads high; steady reads neither.
        [Test]
        public void GIVEN_his_self_esteem_WHEN_read_THEN_low_high_or_neither()
        {
            Read(new DayMoodReadings {SelfEsteem = SelfEsteemBand.Shaken}).SelfRegardLow.Should().BeTrue();
            Read(new DayMoodReadings {SelfEsteem = SelfEsteemBand.Proud}).SelfRegardHigh.Should().BeTrue();
            MoodFacts steady = Read(new DayMoodReadings {SelfEsteem = SelfEsteemBand.Steady});
            (steady.SelfRegardLow || steady.SelfRegardHigh).Should().BeFalse();
        }

        // Nothing read, nothing claimed: no readings is an ordinary day, not a crash.
        [Test]
        public void GIVEN_no_readings_WHEN_read_THEN_it_is_an_ordinary_day()
        {
            MoodFacts f = DayMoodFactsPolicy.From(null);

            (f.IsHurt || f.IsCaptive || f.RealmHasLatelyLost || f.CoffersAreEmpty).Should().BeFalse();
        }
    }
}
