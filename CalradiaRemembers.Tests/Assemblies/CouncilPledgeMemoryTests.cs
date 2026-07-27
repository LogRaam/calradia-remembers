// Code written by Gabriel Mailhot, 27/07/2026.
// Documents the council pledge memory (audit M7). What breaks without it: a member who pledged at the council
// and then withdrew, or whose pledge could not be carried out, kept NO memory of it, so next time the player
// spoke to them they had forgotten a promise the whole table heard. These pin that every outcome yields a
// first-person line that names what was pledged, so the promise is remembered whatever became of it.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilPledgeMemoryTests
    {
        // The gap this closes: a withdrawn pledge used to leave no trace. The memory must record that the member
        // gave their word AND took it back, in their own voice, carrying what the pledge was.
        [Test]
        public void GIVEN_a_withdrawn_pledge_WHEN_remembered_THEN_it_reads_as_a_word_taken_back()
        {
            string s = CouncilPledgeMemory.Summary("Ira will ride to Pravend and clear the bandits", CouncilPledgeOutcome.Withdrawn);

            s.Should().Contain("took back");
            s.Should().Contain("council");
            s.Should().Contain("Ira will ride to Pravend and clear the bandits");
        }

        // A pledge that failed at the lift (the member became unavailable, or the deed could not be grounded) is
        // remembered as coming to nothing, not silently dropped.
        [Test]
        public void GIVEN_a_failed_pledge_WHEN_remembered_THEN_it_reads_as_having_come_to_nothing()
        {
            string s = CouncilPledgeMemory.Summary("send fifty riders to the border", CouncilPledgeOutcome.Failed);

            s.Should().Contain("came to nothing");
            s.Should().Contain("send fifty riders to the border");
        }

        // A kept pledge line exists for completeness (the mod uses the quest memory for the success case today,
        // but the outcome must still map to a sensible first-person line rather than the failed default).
        [Test]
        public void GIVEN_a_kept_pledge_WHEN_remembered_THEN_it_reads_as_made_good()
        {
            CouncilPledgeMemory.Summary("govern Pravend well", CouncilPledgeOutcome.Kept)
                .Should().Contain("made good");
        }

        // Defensive: a blank detail must not produce a dangling "council: " line; it falls back to a neutral
        // phrase so the memory still reads as a whole sentence.
        [Test]
        public void GIVEN_a_blank_detail_WHEN_remembered_THEN_it_falls_back_to_a_neutral_phrase()
        {
            string s = CouncilPledgeMemory.Summary("   ", CouncilPledgeOutcome.Failed);

            s.Should().Contain("a matter put before the table");
            s.Should().NotEndWith(": ");
        }
    }
}
