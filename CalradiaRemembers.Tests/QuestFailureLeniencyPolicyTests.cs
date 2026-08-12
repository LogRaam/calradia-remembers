// Code written by Gabriel Mailhot, 12/08/2026.
// Guards the no-fault forgiveness for a failed or abandoned BANDIT quest (player report: after searching for a
// hideout the marker never pointed to, or open-field bandits that were not there, the player returned to say they
// could not find the base and the giver turned disgusted and hostile). That hostility was a magnitude-25
// FailedQuest grudge + a "broken word" memory + a stance/trust hit + full regard loss, all fired for honestly
// reporting an unfindable target. QuestFailureLeniencyPolicy softens ONLY the hard-to-find bandit class (no
// grudge, no broken-word memory, no trust spent, a token regard nick), and keeps the full consequence for every
// other quest type WITHOUT the policy owning those penalty constants (the caller passes its own value in). If
// this rule drifts, either the hostility returns for bandit failures, or a genuine broken promise (a war deed, a
// letter, a capture) stops costing the player anything.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestFailureLeniencyPolicyTests
    {
        // The core report: a failed bandit deed must NOT plant a grudge, must NOT record a broken-word memory,
        // and must cost only the gentle flat nick, not the caller's full penalty. This is exactly the disgust +
        // hostility the player felt for reporting an unfindable base, removed.
        [TestCase(QuestType.BanditClear)]
        [TestCase(QuestType.BanditHideout)]
        public void GIVEN_a_bandit_quest_WHEN_it_fails_THEN_it_is_forgiven(QuestType type)
        {
            // A deliberately large caller default, so a leaked full penalty would be obvious.
            QuestFailureLeniency result = QuestFailureLeniencyPolicy.Resolve(type, defaultRegardPenalty: 5);

            result.PlantGrudge.Should().BeFalse();
            result.RecordBrokenWordMemory.Should().BeFalse();
            result.RegardPenalty.Should().Be(QuestFailureLeniencyPolicy.BanditFailureRegardPenalty);
        }

        // STAKE: failing to find an UNFINDABLE target is not a broken promise, so the stance/trust spend
        // (OnPromiseBroken) must be skipped for the bandit class too, not just the grudge and the memory. This
        // pins the deliberate design call that the whole no-fault void extends to trust, not only to regard.
        [TestCase(QuestType.BanditClear)]
        [TestCase(QuestType.BanditHideout)]
        public void GIVEN_a_bandit_quest_WHEN_it_fails_THEN_no_promise_trust_is_spent(QuestType type)
        {
            QuestFailureLeniencyPolicy.Resolve(type, defaultRegardPenalty: 3)
                                      .SpendPromiseTrust.Should().BeFalse();
        }

        // A representative non-bandit deed (a war errand) keeps the FULL broken-word consequence: grudge, memory,
        // trust spent, and the caller's own penalty echoed back verbatim (never overridden by the policy). Without
        // this half, forgiving bandits would risk forgiving every quest, gutting the cost of a genuine broken word.
        [Test]
        public void GIVEN_a_non_bandit_quest_WHEN_it_fails_THEN_the_full_consequence_applies()
        {
            const int callerPenalty = 5;
            QuestFailureLeniency result = QuestFailureLeniencyPolicy.Resolve(QuestType.AttackLord, callerPenalty);

            result.PlantGrudge.Should().BeTrue();
            result.RecordBrokenWordMemory.Should().BeTrue();
            result.SpendPromiseTrust.Should().BeTrue();
            result.RegardPenalty.Should().Be(callerPenalty);
        }

        // STAKE: the policy must NEVER own the caller's penalty constants (AbandonPenalty=3 / DeadlineFailurePenalty=5
        // live in QuestService). For a non-bandit type it must echo back whatever the caller passed, so the abandon
        // path stays 3 and the deadline path stays 5 without the policy duplicating either number.
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(7)]
        public void GIVEN_a_non_bandit_quest_WHEN_resolving_THEN_the_callers_own_penalty_is_echoed(int callerPenalty)
        {
            QuestFailureLeniencyPolicy.Resolve(QuestType.DeliverLetter, callerPenalty)
                                      .RegardPenalty.Should().Be(callerPenalty);
        }

        // Pins the exact set of forgiven kinds, so a future quest type added to the enum does not silently start
        // (or stop) being forgiven without a deliberate choice recorded here. Only the two bandit deeds qualify.
        [TestCase(QuestType.BanditClear, true)]
        [TestCase(QuestType.BanditHideout, true)]
        [TestCase(QuestType.AttackLord, false)]
        [TestCase(QuestType.DeliverLetter, false)]
        [TestCase(QuestType.RaidVillage, false)]
        [TestCase(QuestType.Siege, false)]
        [TestCase(QuestType.NemesisBounty, false)]
        [TestCase(QuestType.ProvideGold, false)]
        public void GIVEN_a_quest_type_WHEN_asked_if_hard_to_find_THEN_only_the_bandit_deeds_qualify(QuestType type, bool expected)
        {
            QuestFailureLeniencyPolicy.IsHardToFindTarget(type).Should().Be(expected);
        }
    }
}
