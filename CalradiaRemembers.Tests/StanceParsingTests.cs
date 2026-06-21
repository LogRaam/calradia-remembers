// Code written by Gabriel Mailhot, 21/06/2026.
// Protects parsing of the LLM's [STANCE] block into a StanceShiftData — the spoken nudges to
// trust/respect/fear. A parsing drift silently drops the player's spoken influence, or leaks the
// block into the displayed line.

#region

using FluentAssertions;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StanceParsingTests
    {
        [Test]
        public void GIVEN_a_stance_block_WHEN_parsing_THEN_the_signed_deltas_are_read()
        {
            //Arrange
            const string raw = "[DIALOGUE]You speak well.[/DIALOGUE]\n[STANCE]\ntrust: +2\nrespect: -1\nfear: 0\n[/STANCE]";

            //Act
            ParsedResponse r = new SectionResponseParser().Parse(raw);

            //Assert
            r.StanceShift.Should().NotBeNull();
            r.StanceShift!.Trust.Should().Be(2);
            r.StanceShift.Respect.Should().Be(-1);
            r.StanceShift.Fear.Should().Be(0);
        }

        [Test]
        public void GIVEN_no_stance_block_WHEN_parsing_THEN_there_is_no_shift()
        {
            ParsedResponse r = new SectionResponseParser().Parse("[DIALOGUE]Good day.[/DIALOGUE]");
            r.StanceShift.Should().BeNull();
        }

        [Test]
        public void GIVEN_an_all_zero_stance_block_WHEN_parsing_THEN_it_is_treated_as_nothing()
        {
            //Arrange — a block that moves nothing is the same as no block.
            const string raw = "[DIALOGUE]Hm.[/DIALOGUE]\n[STANCE]\ntrust: 0\nrespect: 0\nfear: 0\n[/STANCE]";

            //Act/Assert
            new SectionResponseParser().Parse(raw).StanceShift.Should().BeNull();
        }

        [Test]
        public void GIVEN_a_stance_block_WHEN_parsing_THEN_it_does_not_leak_into_the_dialogue()
        {
            //Arrange
            const string raw = "[DIALOGUE]Mind your tongue.[/DIALOGUE]\n[STANCE]\nfear: +2\n[/STANCE]";

            //Act
            ParsedResponse r = new SectionResponseParser().Parse(raw);

            //Assert — the spoken line is clean; the block is metadata.
            r.Dialogue.Should().Be("Mind your tongue.");
            r.Dialogue.Should().NotContain("fear");
        }

        [Test]
        public void GIVEN_only_one_axis_moved_WHEN_parsing_THEN_the_others_default_to_zero()
        {
            ParsedResponse r = new SectionResponseParser().Parse("[DIALOGUE]…[/DIALOGUE]\n[STANCE]\nrespect: +1\n[/STANCE]");
            r.StanceShift.Should().NotBeNull();
            r.StanceShift!.Respect.Should().Be(1);
            r.StanceShift.Trust.Should().Be(0);
            r.StanceShift.Fear.Should().Be(0);
        }
    }
}
