// Code written by Gabriel Mailhot, 08/08/2026.
// Pins the one "fought beside you" line that WorldEventFeeders now writes into TWO profiles: the
// allied lords who led their own party, and the companions who rode inside the player's party. A
// player reported (08/08/2026) that party companions never got the memory allied lords did; the fix
// routes both through this single string, so the wording must stay identical for both and match the
// enemy-side reading in EncounterContextBuilder (the win phrase "broke the enemy" is its own text and
// deliberately NOT the enemy-lost phrase that builder parses).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BattleMemoryTextTests
    {
        // The victory wording is what earns the Respect stance bump alongside it; if this phrase drifts,
        // a companion's own memory of a won battle would read wrong even though the stance still moved.
        [Test]
        public void GIVEN_a_won_battle_WHEN_writing_the_ally_line_THEN_it_reads_as_a_shared_victory()
        {
            BattleMemoryText.AllySummary(playerWon: true, near: "")
                .Should().Be("Fought at your side when you broke the enemy.");
        }

        // A loss must still leave a "stood with you" memory, not silence: fighting a losing battle
        // together is exactly the shared hardship a companion should remember, so the branch is pinned.
        [Test]
        public void GIVEN_a_lost_battle_WHEN_writing_the_ally_line_THEN_it_reads_as_standing_together()
        {
            BattleMemoryText.AllySummary(playerWon: false, near: "")
                .Should().Be("Fought at your side when you stood against the enemy.");
        }

        // The place clause arrives pre-shaped as " near X" (leading space, no trailing period) so it slots
        // in before the sentence's own period; this fixes that the two pieces join without a double space
        // or a misplaced full stop when a settlement is known.
        [Test]
        public void GIVEN_a_named_place_WHEN_writing_the_ally_line_THEN_the_place_clause_is_appended_cleanly()
        {
            BattleMemoryText.AllySummary(playerWon: true, near: " near Pravend")
                .Should().Be("Fought at your side when you broke the enemy near Pravend.");
        }
    }
}
