// Code written by Gabriel Mailhot, 03/08/2026.
// PartyRoleParser is the only bridge between what the LLM writes in a [RESOLUTION]'s target_role field (carried
// through AssemblyResolutionEntry.TargetHint, the same single grounding slot appoint_governor uses for a fief
// name) and the real engine role CouncilLift.SettleAssignPartyRole assigns. The stake: a role must be resolved
// from a STRUCTURED value, never prose-parsed at the lift, and an unrecognised role must fail cleanly (None)
// rather than guess a role the model did not actually name, or a companion could be assigned a role nobody asked for.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class PartyRoleParserTests
    {
        // Baseline: every real role the council may assign must round-trip from its plain name.
        [TestCase("Scout", CouncilPartyRole.Scout)]
        [TestCase("Engineer", CouncilPartyRole.Engineer)]
        [TestCase("Quartermaster", CouncilPartyRole.Quartermaster)]
        [TestCase("Surgeon", CouncilPartyRole.Surgeon)]
        public void GIVEN_a_recognised_role_name_WHEN_parsed_THEN_it_resolves_to_the_matching_role(string role, CouncilPartyRole expected)
        {
            PartyRoleParser.Parse(role).Should().Be(expected);
        }

        // The model is not guaranteed to match case exactly; a role parsed only on exact case would silently
        // drop a perfectly good "scout" or "SCOUT" and fail an assignment the player and the LLM both intended.
        [TestCase("scout")]
        [TestCase("SCOUT")]
        [TestCase("ScOuT")]
        public void GIVEN_mixed_case_WHEN_parsed_THEN_it_still_resolves(string role)
        {
            PartyRoleParser.Parse(role).Should().Be(CouncilPartyRole.Scout);
        }

        // Surrounding whitespace (a stray newline or padding from the [RESOLUTION] block's key-value parsing)
        // must not defeat the match, mirroring how other TargetHint-style fields are read tolerantly.
        [Test]
        public void GIVEN_surrounding_whitespace_WHEN_parsed_THEN_it_still_resolves()
        {
            PartyRoleParser.Parse("  Engineer  \n").Should().Be(CouncilPartyRole.Engineer);
        }

        // The stake this whole parser exists for: a role the model did not actually emit (blank, garbage, or a
        // role outside the four the council supports) must fail CLEANLY as None, never be guessed at the lift.
        // CouncilLift.SettleAssignPartyRole treats None as a guard failure, not a silent default assignment.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Physician")]
        [TestCase("governor")]
        public void GIVEN_blank_or_unrecognised_text_WHEN_parsed_THEN_it_resolves_to_none(string role)
        {
            PartyRoleParser.Parse(role).Should().Be(CouncilPartyRole.None);
        }
    }
}
