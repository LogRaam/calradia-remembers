// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.2 fault/no-fault hardening (audit Pattern 2): decides which kill-target quest a hero's DEATH satisfies,
// as a function of who dealt the blow. The bug this guards: QuestDeedTracker.OnHeroKilled returned early on ANY
// non-player kill, so an ExecuteEnemy quest (a named target the giver simply wanted dead) was NEVER satisfied
// when a third party killed the target: the quest went zombie and later expired with an unjust penalty. The
// established design (QuestDeedTracker's own comment) is that an ExecuteEnemy is satisfied by the target's death
// WHOEVER dealt it (the giver's end is met either way), while a paid NemesisBounty pays ONLY for the player's
// own hand (so a nemesis cut down by another lord never hands the player a reward he did not earn). This pure
// rule encodes exactly that split so the control-flow gate can never regress silently.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a hero's death satisfies a kill-target quest, given who struck the blow.</summary>
    public static class QuestKillCreditPolicy
    {
        /// <summary>
        ///   True when the death of the quest's target satisfies it. A <see cref="QuestType.NemesisBounty" /> is a
        ///   PAID bounty: it is satisfied by a kill only when the player themselves dealt it
        ///   (<paramref name="killerIsPlayer" />), so a nemesis felled by a third hand is not rewarded (that
        ///   now-uncompletable bounty is cancelled elsewhere, by NemesisResolutionBehavior). A
        ///   <see cref="QuestType.ExecuteEnemy" /> quest just wants the named target dead, so ANY hand satisfies
        ///   it: the giver's end is met either way. Every other quest kind is not a kill-target quest and is never
        ///   satisfied here.
        /// </summary>
        public static bool IsSatisfiedByKill(QuestType type, bool killerIsPlayer)
        {
            switch (type)
            {
                case QuestType.NemesisBounty:
                    return killerIsPlayer;
                case QuestType.ExecuteEnemy:
                    return true;
                default:
                    return false;
            }
        }
    }
}
