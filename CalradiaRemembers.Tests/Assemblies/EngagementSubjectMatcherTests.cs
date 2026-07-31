// Code written by Gabriel Mailhot, 31/07/2026.
// Documents the tolerant subject-to-seat match the Engagements apply-at-End seal depends on. What breaks without
// it: a wrong match seals a decision onto the wrong hero's durable memory, so an NPC would recall a promise that
// was never theirs. Returning -1 for an unresolved or collective subject is equally load-bearing, since that is
// what routes the seal to the presiding anchor instead of guessing a seated member.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class EngagementSubjectMatcherTests
    {
        // STAKE: the plain case a subject names exactly one seated member. If this failed, no engagement bound
        // to a single named member would ever seal onto them, and every decision would fall back to the anchor.
        [Test]
        public void GIVEN_a_subject_naming_a_single_seated_member_WHEN_matching_THEN_that_members_index_is_returned()
        {
            EngagementSubjectMatcher.MatchIndex("Aeldric", new List<string> {"Aeldric", "Ira"})
                .Should().Be(0);
        }

        // STAKE: a subject is free text read off the conversation, not a bare name ("Aeldric and Ira leads the
        // raid"). The bound member's name still has to be found inside that longer sentence, or a wording
        // extraction would routinely fail to seal onto anyone named.
        [Test]
        public void GIVEN_a_seated_name_inside_a_longer_subject_WHEN_matching_THEN_it_still_matches()
        {
            EngagementSubjectMatcher.MatchIndex("Aeldric and Ira", new List<string> {"Aeldric", "Ira"})
                .Should().Be(0);
        }

        // STAKE: a subject naming a group rather than a seated member ("the clan") must NOT be forced onto
        // someone who happens to share no token with it. Without this, a collective decision could get sealed
        // onto an arbitrary seated hero instead of falling back to the presiding anchor.
        [Test]
        public void GIVEN_a_subject_naming_nobody_seated_WHEN_matching_THEN_no_match_is_returned()
        {
            EngagementSubjectMatcher.MatchIndex("the clan", new List<string> {"Aeldric", "Ira"})
                .Should().Be(-1);
        }

        // STAKE: a blank or null subject, or an empty seated roster, must never throw or guess. This is what
        // keeps the apply-at-End seal safe to call defensively at council close.
        [Test]
        public void GIVEN_a_blank_subject_or_no_participants_WHEN_matching_THEN_no_match_is_returned()
        {
            EngagementSubjectMatcher.MatchIndex("", new List<string> {"Aeldric"}).Should().Be(-1);
            EngagementSubjectMatcher.MatchIndex(null, new List<string> {"Aeldric"}).Should().Be(-1);
            EngagementSubjectMatcher.MatchIndex("Aeldric", new List<string>()).Should().Be(-1);
            EngagementSubjectMatcher.MatchIndex("Aeldric", null).Should().Be(-1);
        }

        // STAKE: when a subject could plausibly match more than one seated name, the FIRST seated match wins
        // deterministically, so the same subject always seals onto the same hero rather than depending on
        // enumeration order noise.
        [Test]
        public void GIVEN_a_subject_that_could_match_two_seated_members_WHEN_matching_THEN_the_first_wins()
        {
            EngagementSubjectMatcher.MatchIndex("Aeldric and Ira ride together", new List<string> {"Ira", "Aeldric"})
                .Should().Be(0);
        }
    }
}
