// Code written by Gabriel Mailhot, 02/09/2026.
// QuestRecoveryPolicy turns the number a player types after cr.quests_all into the quest cr.quest_void cancels.
// Get it wrong and the recovery command either voids the WRONG quest (an off-by-one destroying a good task) or
// rejects a valid number, leaving the player still stuck. These tests pin the 1-based-to-0-based mapping and
// that every bad input (empty, non-numeric, below 1, above the count, or nothing outstanding) is refused with a
// message rather than silently resolving to a quest.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestRecoveryPolicyTests
    {
        // STAKE: the boundary that makes the list usable. "1" is the FIRST quest (0-based 0) and the count is the
        // LAST (0-based count-1). An off-by-one here voids a different quest than the one the player read.
        [Test]
        public void GIVEN_the_first_and_last_numbers_WHEN_resolving_THEN_they_map_to_the_first_and_last_index()
        {
            QuestRecoveryPolicy.Resolve("1", 3).Index.Should().Be(0);
            QuestRecoveryPolicy.Resolve("3", 3).Index.Should().Be(2);
        }

        // STAKE: a number past the end must be refused, never clamped to the last quest, or the player voids one
        // they did not mean to. The message names the real count so they can retry.
        [Test]
        public void GIVEN_a_number_above_the_count_WHEN_resolving_THEN_it_is_refused_with_the_count()
        {
            QuestRecoveryPolicy.Selection s = QuestRecoveryPolicy.Resolve("4", 3);

            s.Ok.Should().BeFalse();
            s.Error.Should().Contain("3 outstanding");
        }

        // STAKE: zero and negatives are not valid quest numbers (the list is 1-based); they must be refused, not
        // wrapped into a valid index.
        [TestCase("0")]
        [TestCase("-1")]
        public void GIVEN_a_number_below_one_WHEN_resolving_THEN_it_is_refused(string arg)
        {
            QuestRecoveryPolicy.Resolve(arg, 3).Ok.Should().BeFalse();
        }

        // STAKE: a typo or a word instead of a number must not throw or resolve to quest #0; it is refused with a
        // hint to use the number cr.quests_all shows.
        [Test]
        public void GIVEN_a_non_numeric_argument_WHEN_resolving_THEN_it_is_refused_with_a_hint()
        {
            QuestRecoveryPolicy.Selection s = QuestRecoveryPolicy.Resolve("second", 3);

            s.Ok.Should().BeFalse();
            s.Error.Should().Contain("cr.quests_all");
        }

        // STAKE: running cr.quest_void with no number must not guess a quest; it prompts for the number instead.
        [Test]
        public void GIVEN_no_argument_WHEN_resolving_THEN_it_asks_for_the_number()
        {
            QuestRecoveryPolicy.Resolve(null, 3).Ok.Should().BeFalse();
            QuestRecoveryPolicy.Resolve("   ", 3).Ok.Should().BeFalse();
        }

        // STAKE: when nothing is outstanding, even a well-formed "1" must be refused: there is no quest #1 to void,
        // and voiding into an empty list would be a crash or a lie.
        [Test]
        public void GIVEN_nothing_outstanding_WHEN_resolving_THEN_it_is_refused_whatever_the_number()
        {
            QuestRecoveryPolicy.Selection s = QuestRecoveryPolicy.Resolve("1", 0);

            s.Ok.Should().BeFalse();
            s.Error.Should().Contain("No outstanding quests");
        }
    }
}
