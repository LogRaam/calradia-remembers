// Code written by Gabriel Mailhot, 15/10/2026.
// Pins how a dead relative is worded in an NPC's RELATIONSHIPS block. Born from Maifailun's report (through tashmetu,
// 15/10/2026): the player's wife Debana died in childbirth and her father asked "Which Debana?" about his own daughter.
// If this wording is wrong, a parent speaks of a dead child as living, invents when or how they died, or does not know
// that the daughter he mourns was the wife of the very person before him.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class KinDeathDescriberTests
    {
        // STAKE: the reported case. Doux Oros must read that his daughter was the player's wife and died in childbirth
        // twelve days ago, or he asks the player which Debana they mean.
        [Test]
        public void GIVEN_a_daughter_wed_to_the_player_who_died_in_childbirth_WHEN_worded_THEN_both_the_marriage_and_the_death_are_said()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "DiedInLabor", DaysAgo = 12.4, WasPlayersSpouse = true, IsFemale = true})
                .Should().Be(" (once the player's wife, who died in childbirth 12 days ago)");
        }

        // STAKE: a son fallen in battle, not wed to the player, reads as before the fix (the parents' wording), so no
        // family line that was right changes.
        [Test]
        public void GIVEN_a_son_fallen_in_battle_WHEN_worded_THEN_it_reads_how_and_when()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "DiedInBattle", DaysAgo = 6})
                .Should().Be(" (who fell in battle 6 days ago)");
        }

        // STAKE: news of the day itself must not read "0 days ago".
        [Test]
        public void GIVEN_a_death_today_WHEN_worded_THEN_it_reads_this_very_day()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "Murdered", DaysAgo = 0.3})
                .Should().Be(" (who was murdered this very day)");
        }

        // STAKE: a death long past is counted in years, not in hundreds of days the model would repeat oddly.
        [Test]
        public void GIVEN_a_death_years_ago_WHEN_worded_THEN_it_is_counted_in_years()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "DiedOfOldAge", DaysAgo = 200})
                .Should().Be(" (who died of old age about 2 year(s) ago)");
        }

        // STAKE: a death before the campaign has no real day; saying a false time is worse than saying none (a tester's
        // father dead last week was "three winters ago").
        [Test]
        public void GIVEN_no_usable_death_day_WHEN_worded_THEN_no_time_is_invented()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "DiedInBattle", DaysAgo = null}).Should().BeEmpty();
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "DiedInBattle", DaysAgo = -3}).Should().BeEmpty();
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "DiedInBattle", DaysAgo = 9000}).Should().BeEmpty();
        }

        // STAKE: even with no death day, the player's late husband is still named so, or the parent does not know why the
        // player speaks of him.
        [Test]
        public void GIVEN_the_players_late_husband_with_no_death_day_WHEN_worded_THEN_the_marriage_is_still_said()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {WasPlayersSpouse = true, IsFemale = false})
                .Should().Be(" (once the player's husband)");
        }

        // STAKE: a death mark CR does not word (lost, or none) still reads as a death, never as a made-up cause.
        [Test]
        public void GIVEN_an_unworded_death_mark_WHEN_worded_THEN_it_reads_plainly_died()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "Lost", DaysAgo = 3}).Should().Be(" (who died 3 days ago)");
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = null, DaysAgo = 3}).Should().Be(" (who died 3 days ago)");
        }

        // STAKE: an execution after a battle is still an execution; "died" would hide what the kin will hold against
        // whoever ordered it.
        [Test]
        public void GIVEN_an_execution_after_a_battle_WHEN_worded_THEN_it_reads_executed()
        {
            KinDeathDescriber.Clause(new KinDeathFacts {Cause = "ExecutionAfterMapEvent", DaysAgo = 4}).Should().Be(" (who was executed 4 days ago)");
        }
    }
}
