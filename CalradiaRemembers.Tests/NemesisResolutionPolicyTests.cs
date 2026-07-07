// Code written by Gabriel Mailhot, 06/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisResolutionPolicyTests
    {
        [Test]
        public void GIVEN_the_kill_was_a_deliberate_execution_WHEN_resolving_the_kind_THEN_it_is_Executed()
        {
            NemesisResolutionPolicy.ResolutionKindFor(wasExecuted: true).Should().Be(NemesisResolutionKind.Executed);
        }

        [Test]
        public void GIVEN_the_kill_was_not_an_execution_WHEN_resolving_the_kind_THEN_it_is_KilledInBattle()
        {
            NemesisResolutionPolicy.ResolutionKindFor(wasExecuted: false).Should().Be(NemesisResolutionKind.KilledInBattle);
        }

        [Test]
        public void GIVEN_an_executed_nemesis_WHEN_building_the_closure_summary_THEN_it_names_the_player_and_the_nemesis()
        {
            string summary = NemesisResolutionPolicy.ClosureSummary("Roswitha", "Grimolf the Faceless Fox", NemesisResolutionKind.Executed);

            summary.Should().Contain("Roswitha");
            summary.Should().Contain("Grimolf the Faceless Fox");
        }

        [Test]
        public void GIVEN_an_executed_nemesis_and_a_battle_killed_nemesis_WHEN_building_the_closure_summary_THEN_the_two_flavours_read_differently()
        {
            string executedSummary = NemesisResolutionPolicy.ClosureSummary("Roswitha", "Grimolf the Faceless Fox", NemesisResolutionKind.Executed);
            string battleSummary = NemesisResolutionPolicy.ClosureSummary("Roswitha", "Grimolf the Faceless Fox", NemesisResolutionKind.KilledInBattle);

            executedSummary.Should().NotBe(battleSummary);
        }

        [Test]
        public void GIVEN_an_executed_nemesis_and_a_battle_killed_nemesis_WHEN_building_the_chronicle_line_THEN_the_two_flavours_read_differently()
        {
            string executedLine = NemesisResolutionPolicy.ClosureChronicleLine("Grimolf the Faceless Fox", NemesisResolutionKind.Executed);
            string battleLine = NemesisResolutionPolicy.ClosureChronicleLine("Grimolf the Faceless Fox", NemesisResolutionKind.KilledInBattle);

            executedLine.Should().NotBe(battleLine);
            executedLine.Should().Contain("Grimolf the Faceless Fox");
            battleLine.Should().Contain("Grimolf the Faceless Fox");
        }
    }
}
