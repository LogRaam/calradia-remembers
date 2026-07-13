// Code written by Gabriel Mailhot, 12/07/2026.
// Documents the assembly roster tally: which statuses count as a filled seat (Present + Ambassador), which as
// in transit, and when the roster is fully resolved so the player is no longer waiting on anyone.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class AssemblyProgressTests
    {
        [Test]
        public void GIVEN_a_mixed_roster_WHEN_tallied_THEN_each_bucket_counts_the_right_statuses()
        {
            var roster = new List<AssemblyInviteeStatus> {
                AssemblyInviteeStatus.Present,
                AssemblyInviteeStatus.Ambassador,   // a filled seat too
                AssemblyInviteeStatus.Accepted,
                AssemblyInviteeStatus.EnRoute,       // both in transit
                AssemblyInviteeStatus.Declined,
                AssemblyInviteeStatus.Invited        // awaiting a reply
            };

            AssemblyProgress p = AssemblyProgress.From(roster);

            p.Total.Should().Be(6);
            p.Present.Should().Be(2);
            p.InTransit.Should().Be(2);
            p.Declined.Should().Be(1);
            p.Awaiting.Should().Be(1);
        }

        [Test]
        public void GIVEN_someone_still_awaiting_or_in_transit_WHEN_asked_THEN_the_roster_is_not_all_resolved()
        {
            AssemblyProgress.From(new[] {AssemblyInviteeStatus.Present, AssemblyInviteeStatus.Invited})
                .AllResolved.Should().BeFalse();
            AssemblyProgress.From(new[] {AssemblyInviteeStatus.Present, AssemblyInviteeStatus.EnRoute})
                .AllResolved.Should().BeFalse();
        }

        [Test]
        public void GIVEN_everyone_has_landed_on_a_final_outcome_WHEN_asked_THEN_the_roster_is_all_resolved()
        {
            AssemblyProgress.From(new[] {
                AssemblyInviteeStatus.Present, AssemblyInviteeStatus.Ambassador, AssemblyInviteeStatus.Declined
            }).AllResolved.Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_empty_or_null_roster_WHEN_tallied_THEN_it_is_zeroed_and_trivially_resolved()
        {
            AssemblyProgress.From(null).Total.Should().Be(0);
            AssemblyProgress.From(new AssemblyInviteeStatus[0]).AllResolved.Should().BeTrue();
        }
    }
}
