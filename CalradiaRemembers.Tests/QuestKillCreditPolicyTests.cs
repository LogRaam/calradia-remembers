// Code written by Gabriel Mailhot, 06/08/2026.
// Guards the v1.34.2 fault/no-fault hardening (audit Pattern 2, finding 4) for kill-target quests.
// QuestDeedTracker.OnHeroKilled returned early on ANY non-player kill, so an ExecuteEnemy quest (a named target
// the giver simply wanted dead) was NEVER satisfied when a third party killed the target: the quest went zombie
// and later expired with an unjust penalty. QuestKillCreditPolicy encodes the established design split: a paid
// NemesisBounty pays ONLY for the player's own hand, while an ExecuteEnemy is satisfied by the target's death
// whoever dealt it. If this drifts, either the zombie-quest penalty returns (ExecuteEnemy ignores a third-party
// kill), or the opposite failure, a NemesisBounty pays out for a kill the player did not earn.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestKillCreditPolicyTests
    {
        // THE BUG THIS FIXES: an ExecuteEnemy target killed by a THIRD PARTY must still satisfy the quest, or it
        // goes zombie and later expires with a penalty the player never earned. The giver just wanted the target
        // dead; the target is dead.
        [Test]
        public void GIVEN_an_execute_enemy_quest_WHEN_a_third_party_kills_the_target_THEN_it_is_satisfied()
        {
            QuestKillCreditPolicy.IsSatisfiedByKill(QuestType.ExecuteEnemy, killerIsPlayer: false).Should().BeTrue();
        }

        // The player's own kill obviously satisfies an ExecuteEnemy too; the point of the fix is that causation
        // does not matter for this kind, so both hands must read as satisfied.
        [Test]
        public void GIVEN_an_execute_enemy_quest_WHEN_the_player_kills_the_target_THEN_it_is_satisfied()
        {
            QuestKillCreditPolicy.IsSatisfiedByKill(QuestType.ExecuteEnemy, killerIsPlayer: true).Should().BeTrue();
        }

        // STAKE: a NemesisBounty is a PAID bounty. It must pay ONLY for the player's own hand, so a nemesis cut
        // down by another lord does not hand the player a reward he did not earn (that now-uncompletable bounty is
        // cancelled elsewhere by NemesisResolutionBehavior).
        [Test]
        public void GIVEN_a_nemesis_bounty_WHEN_a_third_party_kills_the_target_THEN_it_is_NOT_satisfied()
        {
            QuestKillCreditPolicy.IsSatisfiedByKill(QuestType.NemesisBounty, killerIsPlayer: false).Should().BeFalse();
        }

        // The bounty's own success case: the PLAYER dealt the kill, so the hunt ends and the reward is earned.
        [Test]
        public void GIVEN_a_nemesis_bounty_WHEN_the_player_kills_the_target_THEN_it_is_satisfied()
        {
            QuestKillCreditPolicy.IsSatisfiedByKill(QuestType.NemesisBounty, killerIsPlayer: true).Should().BeTrue();
        }

        // STAKE: a death satisfies ONLY the two kill-target kinds. A CapturePrisoner or AttackLord quest is not
        // completed by the target simply dying (it demands a capture or a defeat in battle), so no other kind may
        // ever read as satisfied by a kill, whoever dealt it.
        [TestCase(QuestType.CapturePrisoner)]
        [TestCase(QuestType.AttackLord)]
        [TestCase(QuestType.RescuePrisoner)]
        [TestCase(QuestType.DeliverLetter)]
        [TestCase(QuestType.BanditClear)]
        public void GIVEN_a_non_kill_target_kind_WHEN_the_target_dies_by_any_hand_THEN_it_is_NOT_satisfied(QuestType type)
        {
            QuestKillCreditPolicy.IsSatisfiedByKill(type, killerIsPlayer: true).Should().BeFalse();
            QuestKillCreditPolicy.IsSatisfiedByKill(type, killerIsPlayer: false).Should().BeFalse();
        }
    }
}
