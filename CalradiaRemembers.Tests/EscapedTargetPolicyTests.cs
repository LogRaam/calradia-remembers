// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): since Bannerlord 1.3 a beaten lord often escapes and stays out of reach for weeks, so a
// "capture that lord" task cannot be finished in time through no fault of the player. "Returning the mission with a
// 'I didn't catch the one you wanted but I've got those two instead' kind of discussion would fix this. Or taking
// the [CR] memory into account. After all you've beaten him and won the battle." Ruled by Gabriel the same day: the
// victory counts (a reduced reward, at the giver's word), a lord of the same realm may stand in for him (if the giver
// takes him), and a deadline that runs out while the target is a fugitive, or after the player beat him, costs the
// player nothing. If these rules are wrong, the player is punished for the game's own escape mechanic, or paid in
// full for a capture that never happened, or a giver is forced to take a substitute he never agreed to.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class EscapedTargetPolicyTests
    {
        private static InformalQuest Capture(int? beatenOn = null, string faction = "empire_s")
            => new InformalQuest {Type = QuestType.CapturePrisoner, TargetHeroId = "lord_x", TargetFactionId = faction, TargetBeatenOnDay = beatenOn,
                                  RewardGold = 1000, RewardRelation = 6};

        // ── The victory is recorded ──────────────────────────────────────────────────────────────────

        // "After all you've beaten him and won the battle": a capture or delivery task whose man the player beat is
        // marked so; an attack task needs no mark (beating him IS that task, already credited).
        [Test]
        public void GIVEN_the_player_beat_the_target_WHEN_the_battle_is_read_THEN_capture_and_delivery_tasks_record_it_and_attack_needs_none()
        {
            EscapedTargetPolicy.RecordsTheVictory(QuestType.CapturePrisoner).Should().BeTrue();
            EscapedTargetPolicy.RecordsTheVictory(QuestType.DeliverPrisoner).Should().BeTrue();
            EscapedTargetPolicy.RecordsTheVictory(QuestType.AttackLord).Should().BeFalse();
        }

        // ── The victory, accepted in place of the capture ────────────────────────────────────────────

        // Only a recorded victory opens it: words alone never settle a task ("you reward deeds, not stories").
        [Test]
        public void GIVEN_a_capture_task_WHEN_the_target_was_or_was_not_beaten_THEN_only_a_recorded_victory_may_settle_it()
        {
            EscapedTargetPolicy.MaySettleByVictory(Capture(beatenOn: 40)).Should().BeTrue();
            EscapedTargetPolicy.MaySettleByVictory(Capture()).Should().BeFalse();
        }

        // A victory is not a capture: half the reward, and a warm regard reward is never rounded to nothing.
        [Test]
        public void GIVEN_a_victory_accepted_in_place_of_the_capture_WHEN_paid_THEN_it_is_half_the_reward()
        {
            (int gold, int relationReward) = EscapedTargetPolicy.VictoryReward(1000, 6);
            gold.Should().Be(500);
            relationReward.Should().Be(3);
            EscapedTargetPolicy.VictoryReward(0, 1).RelationReward.Should().Be(1);
        }

        // ── Another lord of his realm, if the giver takes him ────────────────────────────────────────

        // "I've got those two instead": a lord of the same realm may stand in once the target was beaten.
        [Test]
        public void GIVEN_the_target_beaten_WHEN_a_captive_of_his_realm_is_handed_over_THEN_he_may_stand_in()
        {
            EscapedTargetPolicy.SubstituteSettles(Capture(beatenOn: 40), new HeldCaptive {Id = "lord_y", FactionId = "empire_s"}).Should().BeTrue();
        }

        // Not a lord of another realm, and not before the player has even met the target in battle.
        [Test]
        public void GIVEN_another_realm_or_no_victory_WHEN_a_captive_is_handed_over_THEN_he_does_not_stand_in()
        {
            EscapedTargetPolicy.SubstituteSettles(Capture(beatenOn: 40), new HeldCaptive {Id = "lord_y", FactionId = "vlandia"}).Should().BeFalse();
            EscapedTargetPolicy.SubstituteSettles(Capture(), new HeldCaptive {Id = "lord_y", FactionId = "empire_s"}).Should().BeFalse();
        }

        // ── No fault for a fugitive ─────────────────────────────────────────────────────────────────

        // A deadline that runs out while the man is a fugitive, or after the player beat him, is not the player's
        // broken word.
        [Test]
        public void GIVEN_a_deadline_running_out_WHEN_the_target_is_a_fugitive_or_was_beaten_THEN_it_lapses_without_fault()
        {
            EscapedTargetPolicy.LapsesWithoutFault(Capture(), targetIsFugitive: true).Should().BeTrue();
            EscapedTargetPolicy.LapsesWithoutFault(Capture(beatenOn: 40), targetIsFugitive: false).Should().BeTrue();
        }

        // A target simply never pursued is still a broken word, and other kinds of task are untouched.
        [Test]
        public void GIVEN_a_target_free_and_never_beaten_or_another_kind_of_task_WHEN_the_deadline_runs_out_THEN_the_old_rule_stands()
        {
            EscapedTargetPolicy.LapsesWithoutFault(Capture(), targetIsFugitive: false).Should().BeFalse();
            EscapedTargetPolicy.LapsesWithoutFault(new InformalQuest {Type = QuestType.BanditClear}, targetIsFugitive: true).Should().BeFalse();
        }
    }
}
