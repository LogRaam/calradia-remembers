// Code written by Gabriel Mailhot, 21/07/2026.
// Decides which seated member speaks the reply when the player addresses a council. Feeds the council session:
// the chat renders one primary respondent per player turn (Gabriel's whole-table model is ONE call yielding
// that spoken reply plus a reaction from each other member who speaks or gestures), and this names them.
//
// WHY IT MATTERS: the anchor must be STABLE across the turns of one council. If it drifted, the table's first
// voice would change from turn to turn for no reason the player can see, which reads as the council changing
// who leads it mid-sentence. That is why the tie-break exists at all and why it is pinned here.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CouncilAnchorPolicyTests
    {
        // The rule itself: the member who thinks best of the player answers when the player addresses the room.
        // Picking arbitrarily instead would routinely hand the first word to someone who despises the player,
        // which misreads the table for anyone watching it.
        [Test]
        public void GIVEN_members_of_differing_regard_WHEN_choosing_THEN_the_warmest_answers()
        {
            int anchor = CouncilAnchorPolicy.SelectAnchor(new List<CouncilSeatFacts> {
                new("hero_a", 10),
                new("hero_b", 55),
                new("hero_c", 30)
            });

            anchor.Should().Be(1);
        }

        // The stability guarantee, and the reason the tie-break is ordinal rather than "first seen": the roster
        // is rebuilt each turn from live engine state, and nothing promises it arrives in the same ORDER. Two
        // equally-warm members must still yield the same anchor every time, or the first voice would rotate on
        // its own between turns of a single council.
        [Test]
        public void GIVEN_two_members_of_equal_regard_WHEN_the_roster_order_changes_THEN_the_same_one_answers()
        {
            var oneWay = new List<CouncilSeatFacts> {new("hero_b", 40), new("hero_a", 40)};
            var theOther = new List<CouncilSeatFacts> {new("hero_a", 40), new("hero_b", 40)};

            oneWay[CouncilAnchorPolicy.SelectAnchor(oneWay)].HeroId
                .Should().Be(theOther[CouncilAnchorPolicy.SelectAnchor(theOther)].HeroId)
                .And.Be("hero_a");
        }

        // A council of one is legitimate (a player with a single companion), and must still name that member as
        // the one who answers rather than falling through to the empty case.
        [Test]
        public void GIVEN_a_single_seated_member_WHEN_choosing_THEN_they_answer()
        {
            CouncilAnchorPolicy.SelectAnchor(new List<CouncilSeatFacts> {new("hero_a", -20)}).Should().Be(0);
        }

        // Regard runs negative, and a table of people who all resent the player is a scene worth playing, not a
        // crash: the least hostile still carries the reply.
        [Test]
        public void GIVEN_every_member_resents_the_player_WHEN_choosing_THEN_the_least_hostile_answers()
        {
            int anchor = CouncilAnchorPolicy.SelectAnchor(new List<CouncilSeatFacts> {
                new("hero_a", -60),
                new("hero_b", -5),
                new("hero_c", -40)
            });

            anchor.Should().Be(1);
        }

        // The guard the caller depends on: no seats means no council to open. Returning an index here would put
        // the session on a member who does not exist.
        [Test]
        public void GIVEN_nobody_is_seated_WHEN_choosing_THEN_there_is_no_anchor()
        {
            CouncilAnchorPolicy.SelectAnchor(new List<CouncilSeatFacts>()).Should().Be(-1);
            CouncilAnchorPolicy.SelectAnchor(null).Should().Be(-1);
        }
    }
}
