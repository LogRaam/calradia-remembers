// Code written by Gabriel Mailhot, 03/08/2026.
// MissionTypeParser is the only bridge between what the LLM writes in a [RESOLUTION]'s target_mission field
// (carried through AssemblyResolutionEntry.TargetHint, the same single grounding slot assign_party_role uses for
// a party role) and the real CompanionMissionBehavior errand CouncilLift.SettleDispatchMission dispatches. The
// stake: a mission type must be resolved from a STRUCTURED value, never prose-parsed at the lift, and an
// unrecognised type must fail cleanly (None) rather than guess an errand the model did not actually name, or a
// companion could be sent on an errand nobody asked for.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class MissionTypeParserTests
    {
        // Baseline: every errand CompanionMissionBehavior already supports (CompanionMissionType) must round-trip
        // from its plain name, or the council could never ground a dispatch the mission system actually knows how
        // to run.
        [TestCase("GatherNews", CouncilMissionType.GatherNews)]
        [TestCase("Spy", CouncilMissionType.Spy)]
        [TestCase("Steal", CouncilMissionType.Steal)]
        [TestCase("Barter", CouncilMissionType.Barter)]
        [TestCase("Envoy", CouncilMissionType.Envoy)]
        public void GIVEN_a_recognised_mission_name_WHEN_parsed_THEN_it_resolves_to_the_matching_type(string type, CouncilMissionType expected)
        {
            MissionTypeParser.Parse(type).Should().Be(expected);
        }

        // The model is not guaranteed to match case exactly; a type parsed only on exact case would silently drop
        // a perfectly good "spy" or "SPY" and fail a dispatch the player and the LLM both intended.
        [TestCase("spy")]
        [TestCase("SPY")]
        [TestCase("SpY")]
        public void GIVEN_mixed_case_WHEN_parsed_THEN_it_still_resolves(string type)
        {
            MissionTypeParser.Parse(type).Should().Be(CouncilMissionType.Spy);
        }

        // Surrounding whitespace (a stray newline or padding from the [RESOLUTION] block's key-value parsing)
        // must not defeat the match, mirroring how other TargetHint-style fields are read tolerantly.
        [Test]
        public void GIVEN_surrounding_whitespace_WHEN_parsed_THEN_it_still_resolves()
        {
            MissionTypeParser.Parse("  Barter  \n").Should().Be(CouncilMissionType.Barter);
        }

        // The stake this whole parser exists for: a mission type the model did not actually emit (blank, garbage,
        // or an errand outside the set CompanionMissionBehavior supports) must fail CLEANLY as None, never be
        // guessed at the lift. CouncilLift.SettleDispatchMission treats None as a guard failure, not a silent
        // default dispatch.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Assassinate")]
        [TestCase("scout")]
        public void GIVEN_blank_or_unrecognised_text_WHEN_parsed_THEN_it_resolves_to_none(string type)
        {
            MissionTypeParser.Parse(type).Should().Be(CouncilMissionType.None);
        }
    }
}
