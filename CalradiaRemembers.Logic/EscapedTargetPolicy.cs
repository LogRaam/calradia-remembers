// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): since Bannerlord 1.3 a beaten lord often escapes and is out of reach for weeks, so a
// "capture that lord" task could not be finished in time, and the player paid the full broken-word price for the
// game's own escape mechanic. "I didn't catch the one you wanted but I've got those two instead." Ruled by Gabriel the
// same day, three parts:
//
//   - the victory counts: a capture or delivery task whose man the player beat records it, and the giver may accept
//     it in place of the capture, for half the reward (a victory is not a capture);
//   - a substitute: once the target was beaten, a captive lord of the same realm may stand in, if the giver takes
//     him (the hand-over verb; the automatic sale-to-delivery reroute stays strict, a giver may want the man himself);
//   - no fault: a deadline that runs out while the target is a fugitive, or after the player beat him, lapses as a
//     neutral cancel, not a broken word.
//
// Words alone still settle nothing: every door here opens on a victory the game recorded.

#region

using System;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The rules for a capture or delivery task whose target escaped. Pure.</summary>
    public static class EscapedTargetPolicy
    {
        /// <summary>Whether beating this task's target in battle is recorded on it (attack tasks are already credited).</summary>
        public static bool RecordsTheVictory(QuestType type) => type == QuestType.CapturePrisoner || type == QuestType.DeliverPrisoner;

        /// <summary>Whether the giver may accept the recorded victory in place of the capture or delivery.</summary>
        public static bool MaySettleByVictory(InformalQuest q)
            => q != null && q.IsOutstanding && RecordsTheVictory(q.Type) && q.TargetBeatenOnDay != null;

        /// <summary>
        ///   Half the reward for a victory accepted in place of the capture: the gold, and the relation points the task
        ///   promised (InformalQuest.RewardRelation, an amount, not a regard level); a promised point is never rounded away.
        /// </summary>
        public static (int Gold, int RelationReward) VictoryReward(int gold, int relationReward)
            => (Math.Max(0, gold) / 2, relationReward >= 1 ? Math.Max(1, relationReward / 2) : 0);

        /// <summary>
        ///   Whether handing over <paramref name="captive" /> may stand in for this task's escaped target: the target was
        ///   beaten, and the captive serves the same realm.
        /// </summary>
        public static bool SubstituteSettles(InformalQuest q, HeldCaptive captive)
            => MaySettleByVictory(q) && captive != null && !string.IsNullOrEmpty(captive.Id)
               && captive.Id != q.TargetHeroId
               && !string.IsNullOrEmpty(q.TargetFactionId) && captive.FactionId == q.TargetFactionId;

        /// <summary>Whether a deadline running out on this task is no fault of the player's (the target fled, or was beaten).</summary>
        public static bool LapsesWithoutFault(InformalQuest q, bool targetIsFugitive)
            => q != null && RecordsTheVictory(q.Type) && (targetIsFugitive || q.TargetBeatenOnDay != null);
    }
}
