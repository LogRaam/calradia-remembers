// Code written by Gabriel Mailhot, 31/07/2026.
// Documents the durable memory wording the Engagements apply-at-End seals onto a hero at council close. Real
// world-effect executors do not exist yet for a council engagement (only a bandit-clear quest is executable
// elsewhere in this codebase), so this phrasing IS the applied effect: what breaks if it drifts is either a
// decision misattributed as the player's own promise when it was collective, or the reverse.
#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class EngagementSealTextTests
    {
        // STAKE: a decision bound to a specific seated member must read in the FIRST PERSON, matching the mod's
        // first-person [EVENT] memory convention (see CouncilPledgeMemory), so the hero's own memory says "I
        // agreed", not a third-party report of something that happened near them.
        [Test]
        public void GIVEN_a_decision_bound_to_a_seated_subject_WHEN_sealed_THEN_it_reads_in_the_first_person()
        {
            EngagementSealText.Summarize("lead the raid on Sturgia", true)
                .Should().Be("At the council, I agreed to lead the raid on Sturgia.");
        }

        // STAKE: a decision the subject matcher could not bind to a seated member (a group, or unresolved) must
        // NOT claim a first-person promise on the anchor it falls back to; it reads as a collective settlement
        // instead, or the anchor's memory would misrepresent them as having personally sworn to it.
        [Test]
        public void GIVEN_a_decision_with_no_bound_subject_WHEN_sealed_THEN_it_reads_as_a_collective_settlement()
        {
            EngagementSealText.Summarize("raise a new levy", false)
                .Should().Be("At the council, it was agreed that raise a new levy.");
        }

        // STAKE: a blank engagement text is not a decision to seal at all; returning "" (rather than a sentence
        // wrapped around nothing) is what lets the caller skip it instead of planting an empty memory row.
        [Test]
        public void GIVEN_blank_text_WHEN_sealed_THEN_it_returns_empty()
        {
            EngagementSealText.Summarize("", true).Should().Be("");
            EngagementSealText.Summarize("   ", false).Should().Be("");
            EngagementSealText.Summarize(null, true).Should().Be("");
        }

        // STAKE: engagement text read off the conversation may already end with a period; wrapping it verbatim
        // would read as "...Sturgia.." which is exactly the kind of rough edge Gabriel notices in shipped copy.
        [Test]
        public void GIVEN_text_already_ending_in_a_period_WHEN_sealed_THEN_the_period_is_not_doubled()
        {
            EngagementSealText.Summarize("lead the raid on Sturgia.", true)
                .Should().Be("At the council, I agreed to lead the raid on Sturgia.");
        }
    }
}
