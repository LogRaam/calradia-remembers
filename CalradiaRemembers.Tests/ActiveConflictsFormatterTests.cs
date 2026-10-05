// Code written by Gabriel Mailhot, 15/09/2026.
// What these pin: that a kingdom at war with four neighbours is named once, not four times.
//
// fkasad (Nexus, 15/09/2026), on a total conversion with many more factions than vanilla: "CURRENT WORLD
// STATE, Active conflicts: Kingdom vs X; Kingdom vs Y; Kingdom vs Z; should be replaced by Kingdom vs X,
// Y, Z; A 50% gain in size for this wars list in my case."
//
// The pairs and their order are unchanged - only the repeated name goes - so these also pin that nothing
// is merged that should not be.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ActiveConflictsFormatterTests
    {
        // THE REPORT: one side, three enemies, named once.
        [Test]
        public void GIVEN_one_kingdom_at_war_with_three_WHEN_described_THEN_it_is_named_once()
        {
            string line = ActiveConflictsFormatter.Describe(new[] {
                ("Vlandia", "Battania"), ("Vlandia", "Sturgia"), ("Vlandia", "the Aserai")
            });

            line.Should().Be("Vlandia vs Battania, Sturgia, the Aserai");
        }

        // Separate sides stay separate wars, joined the way they always were.
        [Test]
        public void GIVEN_several_sides_WHEN_described_THEN_each_opens_its_own_entry()
        {
            string line = ActiveConflictsFormatter.Describe(new[] {
                ("Vlandia", "Battania"), ("Vlandia", "Sturgia"), ("the Aserai", "the Khuzait")
            });

            line.Should().Be("Vlandia vs Battania, Sturgia; the Aserai vs the Khuzait");
        }

        // Only a CONSECUTIVE run collapses, so the caller's reading order is never rearranged behind its
        // back. If the engine ever stops emitting a side's wars together, this reads worse - it does not
        // read WRONG, which is the property worth having.
        [Test]
        public void GIVEN_a_side_that_recurs_after_another_WHEN_described_THEN_the_order_is_left_alone()
        {
            string line = ActiveConflictsFormatter.Describe(new[] {
                ("Vlandia", "Battania"), ("the Aserai", "the Khuzait"), ("Vlandia", "Sturgia")
            });

            line.Should().Be("Vlandia vs Battania; the Aserai vs the Khuzait; Vlandia vs Sturgia");
        }

        // One war reads exactly as it did before any of this.
        [Test]
        public void GIVEN_a_single_war_WHEN_described_THEN_it_reads_as_it_always_did()
        {
            ActiveConflictsFormatter.Describe(new[] {("Vlandia", "Battania")})
                                    .Should().Be("Vlandia vs Battania");
        }

        // Peace says nothing at all: the prompt drops the whole line on an empty string, and a stray "vs"
        // would be a world fact nobody asked for.
        [Test]
        public void GIVEN_no_wars_WHEN_described_THEN_the_line_is_empty()
        {
            ActiveConflictsFormatter.Describe(new (string, string)[0]).Should().BeEmpty();
            ActiveConflictsFormatter.Describe(null).Should().BeEmpty();
        }

        // A faction whose name failed to resolve must not produce "vs" against a blank.
        [Test]
        public void GIVEN_an_unnamed_side_WHEN_described_THEN_that_war_is_skipped()
        {
            string line = ActiveConflictsFormatter.Describe(new[] {
                ("Vlandia", "Battania"), ("", "Sturgia"), ("the Aserai", (string) null)
            });

            line.Should().Be("Vlandia vs Battania");
        }

        // fkasad (Nexus, 05/10/2026): the player's own realm's wars sat in the middle of a long list and lost their
        // weight. They now open the line, told from the player's side, whatever order the engine walked the realms in.
        [Test]
        public void GIVEN_the_players_realm_at_war_WHEN_described_THEN_its_wars_come_first_from_its_side()
        {
            string line = ActiveConflictsFormatter.Describe(new[] {
                ("the Aserai", "the Khuzait"), ("Battania", "Vlandia"), ("Sturgia", "Vlandia"), ("Vlandia", "the Aserai")
            }, playersRealm: "Vlandia");

            line.Should().Be("Vlandia vs Battania, Sturgia, the Aserai; the Aserai vs the Khuzait");
        }

        // A player in no realm, or a realm at peace: the list is exactly as before.
        [Test]
        public void GIVEN_no_realm_of_the_players_at_war_WHEN_described_THEN_the_order_is_unchanged()
        {
            var wars = new[] {("Vlandia", "Battania"), ("the Aserai", "the Khuzait")};

            ActiveConflictsFormatter.Describe(wars, playersRealm: null).Should().Be(ActiveConflictsFormatter.Describe(wars));
            ActiveConflictsFormatter.Describe(wars, playersRealm: "Sturgia").Should().Be(ActiveConflictsFormatter.Describe(wars));
        }
    }
}
