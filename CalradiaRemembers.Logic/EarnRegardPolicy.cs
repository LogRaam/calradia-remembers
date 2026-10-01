// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "a quest to earn the good graces of another lord (earn 10 regard with Mesui of
// the Khuzites) but issued by a different lord". fkasad answered the same day: "Pleading their case? That's a good
// idea." The giver names a third person; the player must raise that person's own regard for him by a set amount,
// measured on the mod's personal ledger (NpcProfile.ReputationWithPlayer) from where it stood when the task was
// given. Regard is slow by design (a +1 cap and a cooldown per conversation), so the task is a campaign, not an
// afternoon, which is the point of asking it.

#region

using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Who a giver may send the player to win over.</summary>
    public sealed class EarnRegardTargetFacts
    {
        /// <summary>The target is the player: nobody is asked to win their own good graces.</summary>
        public bool IsThePlayer { get; set; }

        /// <summary>The target is the giver: "win my favour" is any task at all, not this one.</summary>
        public bool IsTheGiver { get; set; }

        /// <summary>In the world (HeroPresence): the dead and the set-aside can be won over by nobody.</summary>
        public bool IsInTheWorld { get; set; }

        /// <summary>Of age: a child's regard is no favour a lord needs won.</summary>
        public bool IsAdult { get; set; }
    }

    /// <summary>The rules of an earn_regard task. Pure.</summary>
    public static class EarnRegardPolicy
    {
        /// <summary>How much regard the player must win, Lordfadooboo's own figure ("earn 10 regard").</summary>
        public const int RequiredGain = 10;

        /// <summary>At or past this the target already holds the player dear: there is nothing left to win.</summary>
        public const int NothingLeftToWin = Regard.DevotedTier;

        /// <summary>
        ///   The fewest days such a task may be given: regard moves a point at a time, a few conversations apart, so
        ///   a week's deadline would fail a player who did everything right.
        /// </summary>
        public const int MinimumDays = 30;

        /// <summary>The task's deadline in days: none stays none (open-ended), a short one is lengthened to the minimum.</summary>
        public static int? DeadlineDays(int? proposedDays)
            => proposedDays is int d && d > 0 ? (d < MinimumDays ? MinimumDays : d) : (int?) null;

        /// <summary>Whether this person may be named as the one to win over.</summary>
        public static bool MayBeNamed(EarnRegardTargetFacts f)
            => f != null && !f.IsThePlayer && !f.IsTheGiver && f.IsInTheWorld && f.IsAdult;

        /// <summary>The regard the target must reach, from where it stands now; null when there is nothing left to win.</summary>
        public static int? GoalFrom(int baseline)
            => baseline >= NothingLeftToWin ? (int?) null : baseline + RequiredGain;

        /// <summary>Whether the target's regard has reached the goal.</summary>
        public static bool IsMet(int currentRegard, int? goal)
            => goal is int g && currentRegard >= g;
    }
}
