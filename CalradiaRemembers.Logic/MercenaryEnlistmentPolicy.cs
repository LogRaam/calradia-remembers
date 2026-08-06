// Code written by Gabriel Mailhot, 06/08/2026.
// join_as_mercenary war-check (v1.34.2 governance hardening, action-system audit): join_as_mercenary enrolled the
// player's clan under a kingdom's banner with no check on whether the player was AT WAR with that very kingdom, so
// a player actively fighting a realm could sign on as its hired swords, a state the fiction can never justify. This
// pure gate is the one fact both the prompt teaching (ResolveMercenaryOfferKingdom) and the bridge's execution
// guard (ExecuteJoinAsMercenary) must agree on before a "took service" outcome is real: never a silent no-op, so a
// refused enrolment is surfaced to the model as a refusal, not swallowed.

#region

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate for the <c>join_as_mercenary</c> action: whether the player's clan may take mercenary service
    ///   under the kingdom in question right now, the two facts both the prompt teaching and the bridge's
    ///   execution guard must agree on before the contract is real.
    /// </summary>
    public static class MercenaryEnlistmentPolicy
    {
        /// <summary>
        ///   True only when the player's clan is free of any kingdom obligation (a clan already sworn or already
        ///   under contract settles that first) AND the player is not at war with the hiring kingdom. A realm you
        ///   are fighting does not take you on as its paid swords; the bridge must refuse (never silently ignore)
        ///   when this is false, so a player at war with the kingdom gets an honest in-character refusal instead of
        ///   a game state that quietly does not match the fiction.
        /// </summary>
        public static bool CanJoin(bool playerFreeOfKingdom, bool playerAtWarWithKingdom) =>
            playerFreeOfKingdom && !playerAtWarWithKingdom;
    }
}
