// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "Go scout a city (actually entering the city not like the vanilla where you just have to
// spend some time nearby). Make contact with local gang leader, negotiate with him for intel." If these rules are
// wrong, a lord sends the player to scout a friend's town or a castle with no gang to talk to (a task that can never
// complete), or the report he reads back says something the town is not.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Knowledge;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScoutTownPolicyTests
    {
        // ── Which town ────────────────────────────────────────────────────────────────────────────

        // An enemy town with a gang leader in it: the errand fkasad described.
        [Test]
        public void GIVEN_an_enemy_town_with_a_gang_leader_WHEN_named_THEN_it_may_be_scouted()
        {
            ScoutTownPolicy.MayAsk(new ScoutTownFacts {IsTown = true, GiverAtWarWithIt = true, HasGangLeader = true}).Should().BeTrue();
        }

        // A friend's town is not scouted; a castle or a town with no gang leader has nobody to give the intel, so the
        // task could never complete.
        [Test]
        public void GIVEN_a_friendly_town_a_castle_or_no_gang_leader_WHEN_named_THEN_no_such_task_exists()
        {
            ScoutTownPolicy.MayAsk(new ScoutTownFacts {IsTown = true, GiverAtWarWithIt = false, HasGangLeader = true}).Should().BeFalse();
            ScoutTownPolicy.MayAsk(new ScoutTownFacts {IsTown = false, GiverAtWarWithIt = true, HasGangLeader = true}).Should().BeFalse();
            ScoutTownPolicy.MayAsk(new ScoutTownFacts {IsTown = true, GiverAtWarWithIt = true, HasGangLeader = false}).Should().BeFalse();
        }

        // ── What the gang leader tells ────────────────────────────────────────────────────────────

        // The report names the informant and the town, and says what the town truly is, band by band: the lord reads
        // it back as the verified evidence of the task.
        [Test]
        public void GIVEN_the_towns_true_state_WHEN_reported_THEN_it_names_the_informant_and_says_each_band()
        {
            var town = new SeatFacts {
                PlaceName = "Pravend", Garrison = GarrisonBand.UnderStrength, FoodStores = FoodBand.Thin, FoodDirection = FoodTrend.Falling,
                Loyalty = LoyaltyBand.Restless, Security = SecurityBand.Lawless
            };

            string report = ScoutTownPolicy.Report(town, "Ingrith the Knife", 140);

            report.Should().StartWith("Learned from Ingrith the Knife, inside Pravend, on day 140:");
            report.Should().Contain("a garrison under strength").And.Contain("thin stores, and falling")
                  .And.Contain("its people restless").And.Contain("lawless streets");
        }

        // A band the game could not read is left out rather than guessed.
        [Test]
        public void GIVEN_bands_the_game_could_not_read_WHEN_reported_THEN_they_are_left_out()
        {
            string report = ScoutTownPolicy.Report(new SeatFacts {PlaceName = "Pravend", Garrison = GarrisonBand.Strong}, "Ingrith", 1);

            report.Should().EndWith(": a strong garrison");
        }
    }
}
