// Code written by Gabriel Mailhot, 13/11/2026.
// Bannerlord 1.5.7 (live 11/11/2026, "Executing a member of another clan can start a blood feud") starts a feud with the
// executed hero's clan and sets the player's relation with that clan's LEADER. A Calradia Remembers nemesis belongs to a
// bandit clan (looters, forest bandits...), which has no leader, so the engine hands a null hero to ChangeRelationAction
// and throws (cr.test nemesis_execute_prisoner, Gabriel's run of 13/11/2026: NullReferenceException in
// DefaultDiplomacyModel.GetHeroesForEffectiveRelation). The kill had already happened; everything after it was lost.

namespace CalradiaRemembers.Logic
{
    /// <summary>Whether the engine may start its blood feud for a hero the player executed.</summary>
    public static class LeaderlessFeudPolicy
    {
        /// <summary>
        ///   Only with a clan that has a leader: the feud is a quarrel between two houses' heads, and a band of looters
        ///   has none (the engine would throw on the missing head).
        /// </summary>
        public static bool MayStartFeud(bool victimClanHasLeader) => victimClanHasLeader;
    }
}
