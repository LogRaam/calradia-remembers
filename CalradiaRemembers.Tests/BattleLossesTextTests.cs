// Code written by Gabriel Mailhot, 05/11/2026.
// The toll of a battle in the memory of those who fought it. 3chmidt (Nexus, 05/11/2026) wanted characters to know "how
// many your army killed and how many of your army was wounded and killed", and found that lords who stood on the same
// field took his word for a lie. If this is wrong, a lord remembers the enemy's dead as his own, recites a clerk's tally,
// or speaks of losses in a fight where no man fell.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BattleLossesTextTests
    {
        // STAKE: a lord who fought there knows what each side paid, so the player's account of the slaughter is no tall tale.
        [Test]
        public void GIVEN_both_sides_bled_WHEN_remembered_THEN_each_sides_dead_and_wounded_are_told()
        {
            BattleLossesText.Line(new BattleLossFacts {OurFallen = 38, OurWounded = 91, TheirFallen = 262, TheirWounded = 310})
                            .Should().Be("On our side some 40 fell and some 90 were wounded; on theirs some 250 fell and some 300 were wounded.");
        }

        // STAKE: the rememberer's own side comes first, so a beaten foe does not read the victor's light losses as his own.
        [Test]
        public void GIVEN_the_side_that_lost_few_WHEN_the_foe_remembers_THEN_his_own_heavy_losses_come_first()
        {
            BattleLossesText.Line(new BattleLossFacts {OurFallen = 400, TheirFallen = 12})
                            .Should().Be("On our side some 400 fell; on theirs some 10 fell.");
        }

        // STAKE: a rout where no blood was shed leaves no talk of losses.
        [Test]
        public void GIVEN_no_man_lost_on_either_side_WHEN_remembered_THEN_nothing_is_said_of_losses()
        {
            BattleLossesText.Line(new BattleLossFacts()).Should().BeNull();
        }

        // STAKE: a side that lost no one says so, rather than leaving the toll lopsided and unexplained.
        [Test]
        public void GIVEN_one_side_lost_no_one_WHEN_remembered_THEN_that_side_lost_no_man()
        {
            BattleLossesText.Line(new BattleLossFacts {TheirFallen = 5, TheirWounded = 3})
                            .Should().Be("On our side no man was lost; on theirs a handful fell and a handful were wounded.");
        }

        // STAKE: nobody counts the dead to the man on the field: small numbers stay words, large ones round.
        [Test]
        public void GIVEN_tolls_of_every_size_WHEN_counted_THEN_they_read_as_told_after_a_battle()
        {
            BattleLossesText.Count(1).Should().Be("one");
            BattleLossesText.Count(6).Should().Be("a handful");
            BattleLossesText.Count(47).Should().Be("some 50");
            BattleLossesText.Count(1340).Should().Be("some 1,300");
        }
    }
}
