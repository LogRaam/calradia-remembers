// Code written by Gabriel Mailhot, 12/11/2026.
// Decides when a task means attacking a realm the player is at peace with. EvilPoptart1987 (Nexus, 11/11/2026): "capturing
// a noble you are not at war with. You cannot complete that quest without starting a war ... It forces you to abandon and
// incur a massive grudge for no reason." Gabriel (12/11/2026, "A + C"): the offer warns before the player accepts, and
// giving up such a task costs no grudge. If this rule is too narrow, the player accepts a war blind and is punished for
// dropping it; if too wide, a task the player can do in peace is forgiven when dropped, and the offer cries wolf.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PeaceAttackPolicyTests
    {
        private static PeaceAttackFacts AtPeace(QuestType type, bool held = false)
            => new PeaceAttackFacts {Type = type, TargetRealmKnown = true, PlayerAtWarWithTarget = false, TargetHeroHeldByPlayer = held};

        // THE REPORT: a noble of a realm the player is at peace with can only be taken by attacking that realm.
        [TestCase(QuestType.CapturePrisoner)]
        [TestCase(QuestType.DeliverPrisoner)]
        [TestCase(QuestType.ExecuteEnemy)]
        [TestCase(QuestType.AttackLord)]
        public void GIVEN_a_lord_of_a_realm_at_peace_not_yet_held_WHEN_judged_THEN_the_task_means_attacking_them(QuestType type)
        {
            PeaceAttackPolicy.MeansAttackingARealmAtPeace(AtPeace(type)).Should().BeTrue();
        }

        // STAKE: a captive the player already holds is handed over without a blow; warning of a war there would be false.
        [TestCase(QuestType.CapturePrisoner)]
        [TestCase(QuestType.DeliverPrisoner)]
        [TestCase(QuestType.ExecuteEnemy)]
        public void GIVEN_the_lord_is_already_the_players_captive_WHEN_judged_THEN_no_attack_is_needed(QuestType type)
        {
            PeaceAttackPolicy.MeansAttackingARealmAtPeace(AtPeace(type, held: true)).Should().BeFalse();
        }

        // STAKE: a raid, a caravan, a siege or a battle against a realm at peace is the same war started, by other means.
        [TestCase(QuestType.AttackFaction)]
        [TestCase(QuestType.AttackCaravan)]
        [TestCase(QuestType.RaidVillage)]
        [TestCase(QuestType.Siege)]
        public void GIVEN_a_strike_at_a_realm_at_peace_WHEN_judged_THEN_the_task_means_attacking_them(QuestType type)
        {
            PeaceAttackPolicy.MeansAttackingARealmAtPeace(AtPeace(type)).Should().BeTrue();
        }

        // STAKE: in a war the player is already fighting, the task is an ordinary war errand: no warning, full word given.
        [Test]
        public void GIVEN_the_player_is_at_war_with_the_target_WHEN_judged_THEN_the_task_attacks_no_one_at_peace()
        {
            var facts = new PeaceAttackFacts {Type = QuestType.CapturePrisoner, TargetRealmKnown = true, PlayerAtWarWithTarget = true};

            PeaceAttackPolicy.MeansAttackingARealmAtPeace(facts).Should().BeFalse();
        }

        // STAKE: declaring a war is the deed asked, openly; it is no hidden cost to warn of, nor an excuse to drop it freely.
        [Test]
        public void GIVEN_a_task_to_declare_war_WHEN_judged_THEN_it_is_not_counted_as_a_hidden_attack()
        {
            PeaceAttackPolicy.MeansAttackingARealmAtPeace(AtPeace(QuestType.DeclareWar)).Should().BeFalse();
        }

        // STAKE: bandits belong to no realm; a hideout task must not be told as an attack on anyone at peace.
        [Test]
        public void GIVEN_no_realm_can_be_told_WHEN_judged_THEN_the_task_attacks_no_one_at_peace()
        {
            var facts = new PeaceAttackFacts {Type = QuestType.AttackFaction, TargetRealmKnown = false};

            PeaceAttackPolicy.MeansAttackingARealmAtPeace(facts).Should().BeFalse();
        }
    }
}
