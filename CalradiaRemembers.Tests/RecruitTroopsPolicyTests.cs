// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "recruitment quests". A lord asks for fresh recruits of his own people. If these
// rules are wrong, the player hands over veterans he trained (or his companions) and calls them recruits, a Battanian
// lord takes Imperial levies as his own people, or a lord asks for more men than his party can take.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RecruitTroopsPolicyTests
    {
        // A tier-1 or tier-2 man of the culture asked for: what a recruiter brings back from the villages.
        [Test]
        public void GIVEN_a_raw_soldier_of_the_culture_asked_WHEN_counted_THEN_he_is_a_recruit()
        {
            RecruitTroopsPolicy.Counts(new TroopFacts {Tier = 1, CultureId = "battania"}, "battania").Should().BeTrue();
            RecruitTroopsPolicy.Counts(new TroopFacts {Tier = RecruitTroopsPolicy.MaxRecruitTier, CultureId = "battania"}, "battania").Should().BeTrue();
        }

        // A veteran, a hero, or a man of another people is not what was asked.
        [Test]
        public void GIVEN_a_veteran_a_hero_or_another_people_WHEN_counted_THEN_he_is_not_a_recruit()
        {
            RecruitTroopsPolicy.Counts(new TroopFacts {Tier = 4, CultureId = "battania"}, "battania").Should().BeFalse();
            RecruitTroopsPolicy.Counts(new TroopFacts {Tier = 1, CultureId = "battania", IsHero = true}, "battania").Should().BeFalse();
            RecruitTroopsPolicy.Counts(new TroopFacts {Tier = 1, CultureId = "empire"}, "battania").Should().BeFalse();
        }

        // Asking for "recruits" without a people takes raw men of any culture.
        [Test]
        public void GIVEN_no_culture_asked_WHEN_counted_THEN_any_raw_soldier_counts()
        {
            RecruitTroopsPolicy.Counts(new TroopFacts {Tier = 1, CultureId = "empire"}, null).Should().BeTrue();
        }

        // The same band as a garrison ask: never past the room left in his party.
        [Test]
        public void GIVEN_a_proposed_number_WHEN_the_ask_is_set_THEN_it_is_the_garrison_band_against_the_room_in_his_party()
        {
            RecruitTroopsPolicy.TroopsToAsk(proposed: 40, roomLeft: 25).Should().Be(25);
            RecruitTroopsPolicy.TroopsToAsk(proposed: 40, roomLeft: 2).Should().BeNull();
        }
    }
}
