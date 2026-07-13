// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 2 (resolution layer): the pure wording/classification for how a tracked
// nemesis's vendetta closes, whether cut down in battle, deliberately executed, or spared and turned.
//
// WHY IT MATTERS: NemesisResolutionBehavior consumes this to write the closure deed into the world-event
// store (third-person rumour for other NPCs to reference, first-person chronicle prose for the player's own
// record). The whole point of splitting Executed from KilledInBattle, and Turned from either, is that these
// are three very different STORIES: a deliberate reckoning, a clean battlefield end, and a rare recruitment.
// If the wording ever collapsed to one generic line, a turned nemesis (now riding at the player's side) could
// read in the chronicle as if he had been killed, which is exactly backwards.

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
        // The engine only knows one raw fact (was this a deliberate execution?); this pins the mapping so a
        // battlefield kill can never be misclassified as an execution, which would misattribute deliberate cruelty.
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

        // The rumour line is what other NPCs/the world-event feed reference; if it dropped either name the
        // deed would read as an anonymous rumour instead of a specific, attributable feat.
        [Test]
        public void GIVEN_an_executed_nemesis_WHEN_building_the_closure_summary_THEN_it_names_the_player_and_the_nemesis()
        {
            string summary = NemesisResolutionPolicy.ClosureSummary("Roswitha", "Grimolf the Faceless Fox", NemesisResolutionKind.Executed);

            summary.Should().Contain("Roswitha");
            summary.Should().Contain("Grimolf the Faceless Fox");
        }

        // Per the doc comment, an execution must read as "a deliberate reckoning" (head on a pike) while a
        // battle death reads as "a cleaner, impersonal end"; if the two kinds ever produced the same text, that
        // narrative distinction (the whole reason ResolutionKind exists) would be lost.
        [Test]
        public void GIVEN_an_executed_nemesis_and_a_battle_killed_nemesis_WHEN_building_the_closure_summary_THEN_the_two_flavours_read_differently()
        {
            string executedSummary = NemesisResolutionPolicy.ClosureSummary("Roswitha", "Grimolf the Faceless Fox", NemesisResolutionKind.Executed);
            string battleSummary = NemesisResolutionPolicy.ClosureSummary("Roswitha", "Grimolf the Faceless Fox", NemesisResolutionKind.KilledInBattle);

            executedSummary.Should().NotBe(battleSummary);
        }

        // Same flavour split, but for the first-person chronicle voice: the player's own record of the hunt
        // must distinguish "I struck his head off" from "I cut him down in battle" just as the rumour does.
        [Test]
        public void GIVEN_an_executed_nemesis_and_a_battle_killed_nemesis_WHEN_building_the_chronicle_line_THEN_the_two_flavours_read_differently()
        {
            string executedLine = NemesisResolutionPolicy.ClosureChronicleLine("Grimolf the Faceless Fox", NemesisResolutionKind.Executed);
            string battleLine = NemesisResolutionPolicy.ClosureChronicleLine("Grimolf the Faceless Fox", NemesisResolutionKind.KilledInBattle);

            executedLine.Should().NotBe(battleLine);
            executedLine.Should().Contain("Grimolf the Faceless Fox");
            battleLine.Should().Contain("Grimolf the Faceless Fox");
        }

        // A turning must never be worded as a death: a player who spares and recruits a nemesis (increment 4)
        // reading a "struck his head off" rumour about their own new companion would be a glaring contradiction.
        [Test]
        public void GIVEN_a_nemesis_spared_and_turned_WHEN_building_the_turned_summary_THEN_it_names_both_and_reads_as_a_turning_not_a_death()
        {
            string summary = NemesisResolutionPolicy.TurnedSummary("Roswitha", "Grimolf the Faceless Fox");

            summary.Should().Contain("Roswitha");
            summary.Should().Contain("Grimolf the Faceless Fox");
            summary.Should().NotBe(NemesisResolutionPolicy.ClosureSummary("Roswitha", "Grimolf the Faceless Fox", NemesisResolutionKind.Executed));
        }

        // Same attribution requirement as the closure lines: an unnamed chronicle entry for a turning would
        // be useless to a player re-reading their own history later.
        [Test]
        public void GIVEN_a_nemesis_spared_and_turned_WHEN_building_the_turned_chronicle_line_THEN_it_names_the_nemesis()
        {
            NemesisResolutionPolicy.TurnedChronicleLine("Grimolf the Faceless Fox").Should().Contain("Grimolf the Faceless Fox");
        }
    }
}
