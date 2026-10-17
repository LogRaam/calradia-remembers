// Code written by Gabriel Mailhot, 17/10/2026.
// How a council's dispatch carries the place of a patrol through the ledger's one grounding slot (jaserms1, 16/10/2026:
// a war council's patrol order did nothing). Wrong, the patrol rides to the wrong place or nowhere, or every errand
// pledged before the patrol existed stops resolving after an update.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class DispatchGroundingCodecTests
    {
        // STAKE: the patrol order reaches the lift with the place the council named, intact.
        [Test]
        public void GIVEN_a_patrol_of_a_named_place_WHEN_recorded_and_read_back_THEN_both_the_mission_and_the_place_survive()
        {
            DispatchGroundingCodec.Decompose(DispatchGroundingCodec.Compose("patrol", "Ebereth"), out string mission, out string place);

            mission.Should().Be("patrol");
            place.Should().Be("Ebereth");
        }

        // STAKE: an errand pledged in an older save (a bare name) still settles as that errand after the update.
        [Test]
        public void GIVEN_an_errand_recorded_before_patrols_existed_WHEN_read_back_THEN_it_is_the_errand_alone()
        {
            DispatchGroundingCodec.Compose("spy", null).Should().Be("spy");
            DispatchGroundingCodec.Decompose("spy", out string mission, out string place);

            mission.Should().Be("spy");
            place.Should().BeNull();
        }

        // STAKE: a council patrol with no place named reads as a patrol with no place, which the lift then refuses
        // honestly, never as a patrol of some place it guessed.
        [Test]
        public void GIVEN_a_patrol_with_no_place_WHEN_read_back_THEN_the_place_is_missing_not_guessed()
        {
            DispatchGroundingCodec.Decompose("patrol|  ", out string mission, out string place);

            mission.Should().Be("patrol");
            place.Should().BeNull();
        }

        // STAKE: the council parser knows the patrol, so the order is no longer dropped as an unknown errand.
        [Test]
        public void GIVEN_the_word_patrol_WHEN_parsed_THEN_it_is_the_patrol_mission()
        {
            MissionTypeParser.Parse("Patrol").Should().Be(CouncilMissionType.Patrol);
        }
    }
}
