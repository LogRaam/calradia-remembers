// Code written by Gabriel Mailhot, 09/08/2026.
// Pins the captivity clause appended to a kinsman in an NPC's RELATIONSHIPS block. Born from a player
// report: a parent called the player a liar for claiming to hold their child, because the relationships
// prompt named the captured child with no captivity marker at all. These tests fix the exact wording that
// closes that denial, and guard the free-kin default so ordinary family lines are never decorated.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptiveKinDescriberTests
    {
        // The whole point of the fix: when the player holds the kin, the clause must name the listener as the
        // captor, so the NPC cannot truthfully deny it. This is the exact line that stops the "you are a liar"
        // reply the player reported.
        [Test]
        public void GIVEN_a_kinsman_the_player_holds_WHEN_marking_THEN_the_listener_is_named_the_captor()
        {
            CaptiveKinDescriber.Marker(isPrisoner: true, heldByPlayer: true)
                .Should().Be(" (whom you currently hold prisoner)");
        }

        // A kin captured by someone OTHER than the player is still captive, and the NPC should know it (word of
        // a taken child travels), but the clause must NOT accuse the listener, who did not take them.
        [Test]
        public void GIVEN_a_kinsman_held_by_someone_else_WHEN_marking_THEN_it_reads_a_plain_captivity()
        {
            CaptiveKinDescriber.Marker(isPrisoner: true, heldByPlayer: false)
                .Should().Be(" (currently held prisoner)");
        }

        // The overwhelmingly common case: a free relative must produce NO clause, so ordinary family lines are
        // byte-for-byte what they were before this feature. A stray marker here would clutter every prompt.
        [Test]
        public void GIVEN_a_free_kinsman_WHEN_marking_THEN_no_clause_is_added()
        {
            CaptiveKinDescriber.Marker(isPrisoner: false, heldByPlayer: false).Should().BeEmpty();
        }

        // A defensive edge: the captor flag is meaningless when the kin is not a prisoner at all (a caller could
        // pass a stale heldByPlayer). Not-a-prisoner must win, so a freed hero never reads as still held.
        [Test]
        public void GIVEN_a_free_kinsman_with_a_stale_captor_flag_WHEN_marking_THEN_still_no_clause()
        {
            CaptiveKinDescriber.Marker(isPrisoner: false, heldByPlayer: true).Should().BeEmpty();
        }
    }
}
