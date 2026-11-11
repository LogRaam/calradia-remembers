// Code written by Gabriel Mailhot, 11/11/2026.
// What a lord is told of the army he marches in: its leader, the lords in it with their men, and the total. Schmidt
// (Discord, 11/11/2026): "Lords know which lords they invited to their army when they form a grand army, as well as the
// army size of the lords and the total number of men in the grand army." If this is wrong, a lord asked who marches with
// him invents names, or cannot say how large his own host is.

#region

using CalradiaRemembers.Logic.War;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ArmyRosterTests
    {
        // THE REPORT: the leader knows whom he summoned, how many each brought, and the whole.
        [Test]
        public void GIVEN_a_lord_leading_an_army_WHEN_told_THEN_he_knows_each_lord_their_men_and_the_total()
        {
            var lines = ArmyRoster.Describe(new ArmyRosterFacts {
                Parties = new[] {
                    new ArmyRosterParty {Lord = "Derthert", Men = 212, IsSpeaker = true},
                    new ArmyRosterParty {Lord = "Aldric", Men = 88},
                    new ArmyRosterParty {Lord = "Ingalther", Men = 140}
                }
            });

            lines.Should().Equal(
                "You lead it, with some 200 men in your own party.",
                "Ingalther, some 150 men.",
                "Aldric, some 90 men.",
                "In all, some 450 men in 3 parties.");
        }

        // A lord who answered the summons knows his leader and his fellows, not only his own men.
        [Test]
        public void GIVEN_a_lord_who_is_a_member_WHEN_told_THEN_he_names_the_leader_and_sees_himself_and_the_player()
        {
            var lines = ArmyRoster.Describe(new ArmyRosterFacts {
                Parties = new[] {
                    new ArmyRosterParty {Lord = "Derthert", Men = 212},
                    new ArmyRosterParty {Lord = "Aldric", Men = 88, IsSpeaker = true},
                    new ArmyRosterParty {Lord = "Gabriel", Men = 60, IsPlayer = true}
                }
            });

            lines[0].Should().Be("Led by Derthert, with some 200 men in their own party.");
            lines.Should().Contain("you, some 90 men.").And.Contain("Gabriel (the player), some 60 men.");
        }

        // A grand army of twenty parties names the largest and counts the rest, so the prompt does not grow without end,
        // while the total still counts every man.
        [Test]
        public void GIVEN_a_grand_army_WHEN_told_THEN_the_largest_are_named_and_the_rest_counted_in_the_total()
        {
            var parties = new ArmyRosterParty[ArmyRoster.MaxNamed + 3];
            for (var i = 0; i < parties.Length; i++) parties[i] = new ArmyRosterParty {Lord = $"Lord{i}", Men = 100};

            var lines = ArmyRoster.Describe(new ArmyRosterFacts {Parties = parties});

            lines.Should().Contain("And 2 lesser parties.").And.Contain($"In all, some 1,300 men in {parties.Length} parties.");
        }

        // A lord alone is not an army: his own command already says how many ride with him.
        [Test]
        public void GIVEN_no_army_of_two_parties_WHEN_told_THEN_nothing_is_said()
        {
            ArmyRoster.Describe(new ArmyRosterFacts {Parties = new[] {new ArmyRosterParty {Lord = "Aldric", Men = 88}}}).Should().BeEmpty();
            ArmyRoster.Describe(null).Should().BeEmpty();
        }
    }
}
