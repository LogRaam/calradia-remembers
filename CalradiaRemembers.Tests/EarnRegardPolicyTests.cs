// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "a quest to earn the good graces of another lord (earn 10 regard with Mesui of the
// Khuzites) but issued by a different lord". These rules decide who may be named, how far the player must go, and
// when the deed is done. If they are wrong, a lord sends the player to charm a corpse or a child, a task completes
// before the player has done anything, or a task is set that can never complete because there is nothing left to win.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class EarnRegardPolicyTests
    {
        // ── Who may be named ──────────────────────────────────────────────────────────────────────

        // Mesui of the Khuzaits: a living adult who is neither the player nor the giver.
        [Test]
        public void GIVEN_a_living_adult_third_person_WHEN_named_THEN_the_task_may_be_given()
        {
            EarnRegardPolicy.MayBeNamed(new EarnRegardTargetFacts {IsInTheWorld = true, IsAdult = true}).Should().BeTrue();
        }

        // Maifailun's dead father sat at a council (30/09/2026); a dead or set-aside hero is won over by nobody, and a
        // child's regard is no favour a lord needs (Nexusrrr's baby son, 30/09/2026).
        [Test]
        public void GIVEN_someone_dead_or_a_child_WHEN_named_THEN_no_such_task_exists()
        {
            EarnRegardPolicy.MayBeNamed(new EarnRegardTargetFacts {IsInTheWorld = false, IsAdult = true}).Should().BeFalse();
            EarnRegardPolicy.MayBeNamed(new EarnRegardTargetFacts {IsInTheWorld = true, IsAdult = false}).Should().BeFalse();
        }

        // "Issued by a different lord": the giver cannot name himself, and nobody wins their own good graces.
        [Test]
        public void GIVEN_the_giver_or_the_player_WHEN_named_THEN_no_such_task_exists()
        {
            EarnRegardPolicy.MayBeNamed(new EarnRegardTargetFacts {IsTheGiver = true, IsInTheWorld = true, IsAdult = true}).Should().BeFalse();
            EarnRegardPolicy.MayBeNamed(new EarnRegardTargetFacts {IsThePlayer = true, IsInTheWorld = true, IsAdult = true}).Should().BeFalse();
        }

        // ── How far the player must go ────────────────────────────────────────────────────────────

        // "Earn 10 regard": measured from where it stood when the task was given, so regard already won before the
        // task never counts toward it, and a sour start must be climbed out of.
        [Test]
        public void GIVEN_the_regard_when_the_task_is_given_WHEN_the_goal_is_set_THEN_it_lies_ten_beyond_it()
        {
            EarnRegardPolicy.GoalFrom(0).Should().Be(10);
            EarnRegardPolicy.GoalFrom(-12).Should().Be(-2);
            EarnRegardPolicy.GoalFrom(25).Should().Be(35);
        }

        // Someone who already holds the player dear (Devoted, 50) has nothing left to give; the task is not set,
        // rather than set and nearly impossible against the top of the ledger.
        [Test]
        public void GIVEN_a_target_who_already_holds_the_player_dear_WHEN_the_goal_is_set_THEN_there_is_no_task()
        {
            EarnRegardPolicy.GoalFrom(EarnRegardPolicy.NothingLeftToWin).Should().BeNull();
            EarnRegardPolicy.GoalFrom(EarnRegardPolicy.NothingLeftToWin - 1).Should().Be(EarnRegardPolicy.NothingLeftToWin - 1 + EarnRegardPolicy.RequiredGain);
        }

        // Regard rises a point at a time, conversations apart: a lord who says "within the week" would set a task
        // the player cannot meet however well he plays. Lengthened to a month; an open-ended task stays open.
        [Test]
        public void GIVEN_a_short_or_absent_deadline_WHEN_the_task_is_given_THEN_it_runs_at_least_a_month_or_stays_open()
        {
            EarnRegardPolicy.DeadlineDays(7).Should().Be(EarnRegardPolicy.MinimumDays);
            EarnRegardPolicy.DeadlineDays(45).Should().Be(45);
            EarnRegardPolicy.DeadlineDays(null).Should().BeNull();
            EarnRegardPolicy.DeadlineDays(0).Should().BeNull();
        }

        // ── When it is done ───────────────────────────────────────────────────────────────────────

        // The goal reached is the deed; one short is not, and a task with no goal never completes by accident.
        [Test]
        public void GIVEN_the_targets_regard_WHEN_checked_against_the_goal_THEN_only_reaching_it_completes_the_task()
        {
            EarnRegardPolicy.IsMet(10, 10).Should().BeTrue();
            EarnRegardPolicy.IsMet(14, 10).Should().BeTrue();
            EarnRegardPolicy.IsMet(9, 10).Should().BeFalse();
            EarnRegardPolicy.IsMet(99, null).Should().BeFalse();
        }
    }
}
