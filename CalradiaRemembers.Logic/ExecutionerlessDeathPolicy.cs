// Code written by Gabriel Mailhot, 15/11/2026.
// Gabriel's play test of 15/11/2026 on 1.5.7, after a cr.test all: a crash in ExecutionCampaignBehavior.OnHeroKilled,
// reached from AgingCampaignBehavior.DailyTickHero -> KillCharacterAction.ApplyByDeathMark. The victim carried an
// Executed death mark with no executioner: Hero.OnDeath clears DeathMarkKillerHero but leaves DeathMark, and a hero
// brought back from a prison roster afterwards (EndCaptivityAction's release sets him Released without asking whether he
// is dead) keeps that stale mark. The next daily tick re-applies it, and the engine's blood-feud bookkeeping reads
// killer.Clan on the missing killer. Any road to such a hero (a test fixture, another mod) ended the game.

namespace CalradiaRemembers.Logic
{
    /// <summary>Whether the engine's execution bookkeeping (blood feuds) may treat a death as an execution.</summary>
    public static class ExecutionerlessDeathPolicy
    {
        /// <summary>
        ///   Only when there is someone who did the executing: the bookkeeping counts what the executioner's house did and
        ///   starts a feud with it, and with nobody there it has nothing to count and throws. Without an executioner the
        ///   death is still a death; only the feud ledger is skipped.
        /// </summary>
        public static bool KeptAsExecution(bool isExecution, bool hasExecutioner) => isExecution && hasExecutioner;
    }
}
