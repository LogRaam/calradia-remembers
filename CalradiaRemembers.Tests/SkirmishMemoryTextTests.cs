// Code written by Gabriel Mailhot, 15/10/2026.
// Decides what a companion remembers of the small fights shared with the player: those the chronicle does not record
// (no siege, no armies, under 150 men, or no lord leading either side). Thoragoros1 (Nexus, 15/10/2026) fought bandits,
// a patrol and lords' parties with three companions and none of them remembered any of it; Gabriel ruled that a
// companion remembers every fight shared with the player. If this is wrong, either the companions forget again, or a
// looter-hunting campaign writes one memory per fight into every companion's profile (the save bloat the folding
// exists to prevent), or a folded line swallows a real battle memory.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SkirmishMemoryTextTests
    {
        private static SkirmishFacts Fight(bool won = true, string foe = "a band of Looters", string place = "")
            => new SkirmishFacts {PlayerName = "Adam", Foe = foe, PlayerWon = won, PlaceClause = place};

        // Thoragoros1's report: a first fight against bandits must leave a memory the companion can speak of, naming
        // the player (never "you", reminensce 19/09/2026) and who was fought.
        [Test]
        public void GIVEN_one_small_fight_WHEN_writing_the_memory_THEN_the_companion_remembers_who_was_fought_and_how_it_went()
        {
            SkirmishMemoryText.Line(1, Fight())
                .Should().Be("Rode with Adam in a small fight against a band of Looters, and we won it.");
        }

        // A lost fight is still a shared fight; a companion who was beaten beside the player remembers it.
        [Test]
        public void GIVEN_a_lost_small_fight_WHEN_writing_the_memory_THEN_it_says_it_was_lost()
        {
            SkirmishMemoryText.Line(1, Fight(won: false)).Should().EndWith("and we lost it.");
        }

        // The fold: many fights become one line that keeps its count and the last of them, so the companion can say
        // "we have fought often of late" without a memory per looter band.
        [Test]
        public void GIVEN_several_small_fights_WHEN_writing_the_memory_THEN_one_line_counts_them_and_tells_the_last()
        {
            SkirmishMemoryText.Line(3, Fight(foe: "men of Vlandia", place: " near Pravend"))
                .Should().Be("Rode with Adam in 3 small fights of late; the last against men of Vlandia near Pravend, and we won it.");
        }

        // The fold must read back its own count, or the line would say "2 small fights" forever.
        [Test]
        public void GIVEN_a_line_the_fold_wrote_WHEN_reading_its_count_THEN_it_reads_back_exactly_the_count_written()
        {
            SkirmishMemoryText.CountIn(SkirmishMemoryText.Line(1, Fight()), "Adam").Should().Be(1);
            SkirmishMemoryText.CountIn(SkirmishMemoryText.Line(7, Fight()), "Adam").Should().Be(7);
        }

        // A notable battle's own memory ("Fought at Adam's side ...") is a separate, detailed memory; folding a
        // skirmish into it would erase the siege the companion lived through.
        [Test]
        public void GIVEN_a_notable_battle_memory_WHEN_a_small_fight_ends_THEN_it_is_never_folded_into()
        {
            var siege = new SkirmishLine {Day = 10, PlayerName = "Adam", Summary = BattleMemoryText.AllySummary(true, " at the siege of Pravend", true, "Adam")};

            SkirmishMemoryText.FoldsInto(siege, 11).Should().BeFalse();
        }

        // Folding keeps a busy stretch to one line per companion.
        [Test]
        public void GIVEN_a_recent_small_fight_line_WHEN_another_small_fight_ends_THEN_it_folds_into_that_line()
        {
            var line = new SkirmishLine {Day = 10, PlayerName = "Adam", Summary = SkirmishMemoryText.Line(2, Fight())};

            SkirmishMemoryText.FoldsInto(line, 10 + SkirmishMemoryText.FoldWindowDays).Should().BeTrue();
        }

        // "Of late" must stay true: a line from a past season is left as it was, and a new one starts.
        [Test]
        public void GIVEN_an_old_small_fight_line_WHEN_another_small_fight_ends_THEN_a_new_line_starts()
        {
            var line = new SkirmishLine {Day = 10, PlayerName = "Adam", Summary = SkirmishMemoryText.Line(2, Fight())};

            SkirmishMemoryText.FoldsInto(line, 11 + SkirmishMemoryText.FoldWindowDays).Should().BeFalse();
        }

        // A player who never stops hunting bandits must not keep one line that counts forever and never says when:
        // at the cap a fresh line starts.
        [Test]
        public void GIVEN_a_line_at_the_cap_WHEN_another_small_fight_ends_THEN_a_new_line_starts()
        {
            var line = new SkirmishLine {Day = 10, PlayerName = "Adam", Summary = SkirmishMemoryText.Line(SkirmishMemoryText.FoldCap, Fight())};

            SkirmishMemoryText.FoldsInto(line, 10).Should().BeFalse();
        }

        // Thoragoros1 fought "named characters, bandits, and a patrol": each must be named as what it was.
        [Test]
        public void GIVEN_each_kind_of_foe_WHEN_naming_it_THEN_a_lord_bandits_and_a_realm_read_as_themselves()
        {
            SkirmishMemoryText.Foe(new SkirmishFoe {LordName = "Derthert of the dey Meroc", PartyName = "Derthert's Party", FactionName = "Vlandia"})
                .Should().Be("Derthert of the dey Meroc's men");
            SkirmishMemoryText.Foe(new SkirmishFoe {PartyName = "Looters", IsBandit = true}).Should().Be("a band of Looters");
            SkirmishMemoryText.Foe(new SkirmishFoe {PartyName = "Patrol", FactionName = "Vlandia"}).Should().Be("men of Vlandia");
            SkirmishMemoryText.Foe(new SkirmishFoe()).Should().Be("the enemy");
        }
    }
}
