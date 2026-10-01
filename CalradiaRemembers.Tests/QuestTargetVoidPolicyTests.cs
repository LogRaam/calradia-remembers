// Code written by Gabriel Mailhot, 06/08/2026.
// Guards the v1.34.2 fault/no-fault hardening (audit Pattern 2) for hero-bound quests. The daily quest sweep
// (QuestService.RunDailyMaintenance) only ever handled the GIVER dying and the war premise ending; a
// capture/attack/execute/rescue/deliver quest whose TARGET hero died or vanished went untouched and later
// expired with a relation penalty PLUS a FailedQuest grudge, punishing the player for an act of the world.
// QuestTargetVoidPolicy tells the sweep to cancel such a quest as a clean neutral void instead. If this rule
// drifts, either the unjust penalty returns (a target the world killed still fails the player), or the opposite
// failure, a quest whose target is alive and reachable is wrongly destroyed, throwing away a valid task.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestTargetVoidPolicyTests
    {
        // The core no-fault case, per hero-bound kind: the target hero is DEAD. Capturing, attacking, executing,
        // or delivering to a corpse is impossible, so each must void. Punishing the player here is the exact
        // Pattern-2 wrong the hardening removes.
        [TestCase(QuestType.CapturePrisoner)]
        [TestCase(QuestType.AttackLord)]
        [TestCase(QuestType.ExecuteEnemy)]
        [TestCase(QuestType.DeliverLetter)]
        [TestCase(QuestType.DeliverPrisoner)]
        [TestCase(QuestType.EarnRegard)] // a dead lord's good graces are won by nobody (Lordfadooboo, 01/10/2026)
        [TestCase(QuestType.PleadCase)] // nor is a case pleaded before him
        public void GIVEN_a_hero_bound_quest_WHEN_its_target_is_dead_THEN_it_voids(QuestType type)
        {
            QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = type, HasTargetHero = true, TargetResolved = true, TargetAlive = false, TargetIsPrisoner = false})
                .Should().BeTrue();
        }

        // The target's StringId no longer resolves to any hero (vanished from the world). Same no-fault outcome as
        // death: the task can never be answered, so it must void rather than lapse into an unjust breach.
        [Test]
        public void GIVEN_a_hero_bound_quest_WHEN_its_target_has_vanished_THEN_it_voids()
        {
            QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.CapturePrisoner, HasTargetHero = true, TargetResolved = false, TargetAlive = false, TargetIsPrisoner = false})
                .Should().BeTrue();
        }

        // THE GUARD AGAINST OVER-VOIDING: a target who is alive, present, and (where it matters) still captive means
        // the task genuinely stands. Voiding it would destroy a valid quest the player can still complete.
        [TestCase(QuestType.CapturePrisoner)]
        [TestCase(QuestType.AttackLord)]
        [TestCase(QuestType.ExecuteEnemy)]
        [TestCase(QuestType.DeliverLetter)]
        [TestCase(QuestType.DeliverPrisoner)]
        [TestCase(QuestType.EarnRegard)]
        public void GIVEN_a_hero_bound_quest_WHEN_its_target_is_alive_and_present_THEN_it_does_NOT_void(QuestType type)
        {
            QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = type, HasTargetHero = true, TargetResolved = true, TargetAlive = true, TargetIsPrisoner = true})
                .Should().BeFalse();
        }

        // STAKE: a rescue's whole point is a captive still held. If the target is alive but NO LONGER a prisoner
        // (freed or escaped by another hand), there is no one left to rescue, so the quest must void even though the
        // hero lives. This is the one kind that reads targetIsPrisoner.
        [Test]
        public void GIVEN_a_rescue_WHEN_the_target_is_alive_but_no_longer_a_prisoner_THEN_it_voids()
        {
            QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.RescuePrisoner, HasTargetHero = true, TargetResolved = true, TargetAlive = true, TargetIsPrisoner = false})
                .Should().BeTrue();
        }

        // The mirror of the above: a rescue target who is alive AND still held is exactly the situation the quest
        // exists for, so it must NOT void.
        [Test]
        public void GIVEN_a_rescue_WHEN_the_target_is_alive_and_still_held_THEN_it_does_NOT_void()
        {
            QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.RescuePrisoner, HasTargetHero = true, TargetResolved = true, TargetAlive = true, TargetIsPrisoner = true})
                .Should().BeFalse();
        }

        // STAKE: a DeliverPrisoner (or any hero-bound kind) that names NO specific hero, keyed on a faction instead,
        // must never be voided by this rule: its premise is handled elsewhere (PremiseGone). A hasTargetHero=false
        // quest with a dead lookup (there is no hero) must fall through to "do not void".
        [Test]
        public void GIVEN_a_quest_with_no_named_target_hero_WHEN_judging_THEN_it_does_NOT_void()
        {
            QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.DeliverPrisoner, HasTargetHero = false, TargetResolved = false, TargetAlive = false, TargetIsPrisoner = false})
                .Should().BeFalse();
        }

        // STAKE: faction/settlement-bound kinds (BanditClear, AttackFaction, RaidVillage, Siege, ...) and the
        // separately-resolved NemesisBounty are NOT hero-bound here, so even a dead "target" must not trip this
        // rule, or it would collide with their own dedicated handling (PremiseGone / NemesisResolutionBehavior).
        [TestCase(QuestType.BanditClear)]
        [TestCase(QuestType.AttackFaction)]
        [TestCase(QuestType.RaidVillage)]
        [TestCase(QuestType.Siege)]
        [TestCase(QuestType.NemesisBounty)]
        [TestCase(QuestType.ScoutArmy)]
        public void GIVEN_a_non_hero_bound_kind_WHEN_judging_THEN_it_does_NOT_void(QuestType type)
        {
            QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = type, HasTargetHero = true, TargetResolved = false, TargetAlive = false, TargetIsPrisoner = false})
                .Should().BeFalse();
        }

        // Pins the exact hero-bound set the sweep acts on, so a future kind added to the enum does not silently
        // start (or stop) voiding without a deliberate choice recorded here.
        [TestCase(QuestType.CapturePrisoner, true)]
        [TestCase(QuestType.AttackLord, true)]
        [TestCase(QuestType.ExecuteEnemy, true)]
        [TestCase(QuestType.RescuePrisoner, true)]
        [TestCase(QuestType.DeliverLetter, true)]
        [TestCase(QuestType.DeliverPrisoner, true)]
        [TestCase(QuestType.EarnRegard, true)]
        [TestCase(QuestType.PleadCase, true)]
        [TestCase(QuestType.NemesisBounty, false)]
        [TestCase(QuestType.BanditClear, false)]
        [TestCase(QuestType.DeclareWar, false)]
        public void GIVEN_a_quest_type_WHEN_asked_if_hero_bound_THEN_only_the_target_hero_kinds_qualify(QuestType type, bool expected)
        {
            QuestTargetVoidPolicy.IsTargetHeroBound(type).Should().Be(expected);
        }

        // THE PLAYER REPORT: a "capture an enemy lord" task that outlived the peace treaty. Once the giver's realm
        // is no longer at war with the target's, carrying it out would break the fresh treaty, so a war-hostile
        // hero quest must void the moment peace holds. Capture/attack/execute all share this premise.
        [TestCase(QuestType.CapturePrisoner)]
        [TestCase(QuestType.AttackLord)]
        [TestCase(QuestType.ExecuteEnemy)]
        public void GIVEN_a_war_hostile_hero_quest_WHEN_peace_is_made_THEN_it_voids(QuestType type)
        {
            QuestTargetVoidPolicy.ShouldVoidOnPeace(type, hasTargetHero: true, giverAtWarWithTargetFaction: false)
                .Should().BeTrue();
        }

        // The guard against over-voiding: while the war is still ON, the task genuinely stands. Voiding a live
        // war quest would throw away a valid, completable task.
        [TestCase(QuestType.CapturePrisoner)]
        [TestCase(QuestType.AttackLord)]
        [TestCase(QuestType.ExecuteEnemy)]
        public void GIVEN_a_war_hostile_hero_quest_WHEN_the_war_is_still_on_THEN_it_does_NOT_void(QuestType type)
        {
            QuestTargetVoidPolicy.ShouldVoidOnPeace(type, hasTargetHero: true, giverAtWarWithTargetFaction: true)
                .Should().BeFalse();
        }

        // STAKE: peace must NOT silently drop a rescue or a delivery. Rescuing your own man or delivering a letter
        // is not an act of war, so a treaty leaves those tasks perfectly answerable, they only void via the
        // target-left-the-board rule above, never on peace.
        [TestCase(QuestType.RescuePrisoner)]
        [TestCase(QuestType.DeliverLetter)]
        [TestCase(QuestType.DeliverPrisoner)]
        public void GIVEN_a_non_hostile_hero_quest_WHEN_peace_is_made_THEN_it_does_NOT_void(QuestType type)
        {
            QuestTargetVoidPolicy.ShouldVoidOnPeace(type, hasTargetHero: true, giverAtWarWithTargetFaction: false)
                .Should().BeFalse();
        }

        // STAKE: a war quest keyed on a faction rather than a named lord (hasTargetHero=false) is PremiseGone's
        // job, not this one; passing it here must not double-void, so it returns false regardless of peace.
        [Test]
        public void GIVEN_a_war_quest_with_no_named_target_hero_WHEN_peace_is_made_THEN_this_rule_does_NOT_void()
        {
            QuestTargetVoidPolicy.ShouldVoidOnPeace(QuestType.CapturePrisoner, hasTargetHero: false, giverAtWarWithTargetFaction: false)
                .Should().BeFalse();
        }

        // Pins the exact war-hostile set, so a future quest kind cannot silently start (or stop) voiding on peace.
        [TestCase(QuestType.CapturePrisoner, true)]
        [TestCase(QuestType.AttackLord, true)]
        [TestCase(QuestType.ExecuteEnemy, true)]
        [TestCase(QuestType.RescuePrisoner, false)]
        [TestCase(QuestType.DeliverLetter, false)]
        [TestCase(QuestType.DeliverPrisoner, false)]
        [TestCase(QuestType.BanditClear, false)]
        [TestCase(QuestType.DeclareWar, false)]
        public void GIVEN_a_quest_type_WHEN_asked_if_war_hostile_THEN_only_the_strike_an_enemy_kinds_qualify(QuestType type, bool expected)
        {
            QuestTargetVoidPolicy.IsWarHostileHeroQuest(type).Should().Be(expected);
        }

        // v2.5.3 (player report, "his success cancelled his quest"): ShouldVoid alone cannot tell the two apart,
        // but the wording the player reads must. A vanished target IS gone from the world; both must keep
        // resolving to their own distinct reason so QuestService can compose an honest closure line for each.
        [Test]
        public void GIVEN_a_vanished_target_WHEN_asked_the_void_reason_THEN_it_is_TargetVanished()
        {
            QuestTargetVoidPolicy.ReasonToVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.CapturePrisoner, HasTargetHero = true, TargetResolved = false, TargetAlive = false, TargetIsPrisoner = false})
                .Should().Be(QuestVoidReason.TargetVanished);
        }

        // Same as above, for a resolved-but-dead target: still genuinely gone from the world, distinct from a
        // rescue's target simply having been freed (see the next test).
        [Test]
        public void GIVEN_a_dead_target_WHEN_asked_the_void_reason_THEN_it_is_TargetDead()
        {
            QuestTargetVoidPolicy.ReasonToVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.AttackLord, HasTargetHero = true, TargetResolved = true, TargetAlive = false, TargetIsPrisoner = false})
                .Should().Be(QuestVoidReason.TargetDead);
        }

        // THE FIX ITSELF: a rescue's target being alive, present, and simply no longer a prisoner must NOT
        // resolve to the same reason as a vanished or dead target. The old single "gone from the world" wording
        // is exactly what told a player who had just freed someone that their target had vanished.
        [Test]
        public void GIVEN_a_rescue_target_alive_but_freed_WHEN_asked_the_void_reason_THEN_it_is_RescueTargetFreed()
        {
            QuestTargetVoidPolicy.ReasonToVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.RescuePrisoner, HasTargetHero = true, TargetResolved = true, TargetAlive = true, TargetIsPrisoner = false})
                .Should().Be(QuestVoidReason.RescueTargetFreed);
        }

        // Guard against over-voiding, same case as ShouldVoid's own equivalent test: a live, present, still-held
        // target genuinely stands, so the reason must be None, never any of the three void reasons.
        [Test]
        public void GIVEN_a_live_reachable_target_WHEN_asked_the_void_reason_THEN_it_is_None()
        {
            QuestTargetVoidPolicy.ReasonToVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = QuestType.RescuePrisoner, HasTargetHero = true, TargetResolved = true, TargetAlive = true, TargetIsPrisoner = true})
                .Should().Be(QuestVoidReason.None);
        }

        // ShouldVoid is kept as a thin bool projection of ReasonToVoid for its existing callers/tests; this pins
        // that the two can never silently disagree (ShouldVoid true iff the reason is not None) across every
        // case this fixture already exercises for ShouldVoid directly.
        [TestCase(QuestType.RescuePrisoner, true, true, true, false)]
        [TestCase(QuestType.RescuePrisoner, true, true, true, true)]
        [TestCase(QuestType.CapturePrisoner, true, true, false, false)]
        [TestCase(QuestType.CapturePrisoner, true, false, false, false)]
        [TestCase(QuestType.BanditClear, true, false, false, false)]
        public void GIVEN_any_case_WHEN_comparing_ShouldVoid_and_ReasonToVoid_THEN_they_agree(
            QuestType type, bool hasTargetHero, bool targetResolved, bool targetAlive, bool targetIsPrisoner)
        {
            bool shouldVoid = QuestTargetVoidPolicy.ShouldVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = type, HasTargetHero = hasTargetHero, TargetResolved = targetResolved, TargetAlive = targetAlive, TargetIsPrisoner = targetIsPrisoner});
            QuestVoidReason reason = QuestTargetVoidPolicy.ReasonToVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = type, HasTargetHero = hasTargetHero, TargetResolved = targetResolved, TargetAlive = targetAlive, TargetIsPrisoner = targetIsPrisoner});

            shouldVoid.Should().Be(reason != QuestVoidReason.None);
        }
    }
}
