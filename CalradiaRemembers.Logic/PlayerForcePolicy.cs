// Code written by Gabriel Mailhot, 18/07/2026.
// What force an interlocutor SEES at the player's back. A player report drove this: an NPC called the
// player "brave or foolish to approach me alone" while two thousand Nords stood behind them. The count
// itself never reaches the prompt (the SDK renders a coarse qualitative band), but WHICH count is the
// right one is a judgement, not plumbing, so it lives here: an army the player leads is what is visible,
// not merely their own party, and a captive player has no force at all.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Resolves the troop count an interlocutor can plainly see standing with the player. The host
    ///   gathers the engine facts; this decides which of them counts.
    /// </summary>
    public static class PlayerForcePolicy
    {
        /// <summary>
        ///   The force at the player's back, as seen from across a conversation.
        ///   <para>
        ///     A CAPTIVE player has none: their party does not follow them into a cell, so reporting it
        ///     would be a lie the captor can see through. A player who LEADS an army is backed by the
        ///     whole army, not just their own party, which is the case the original bug got wrong. A
        ///     player merely attached to someone else's army speaks only for their own party: the host's
        ///     men are not theirs to show.
        ///   </para>
        /// </summary>
        /// <param name="playerIsCaptive">The player is held prisoner.</param>
        /// <param name="ownPartyTroops">Men in the player's own party.</param>
        /// <param name="playerLeadsArmy">The player commands an army (not merely attached to one).</param>
        /// <param name="ledArmyTroops">Men across every party of the army the player leads.</param>
        public static int Resolve(bool playerIsCaptive, int ownPartyTroops, bool playerLeadsArmy, int ledArmyTroops)
        {
            if (playerIsCaptive) return 0;

            int visible = playerLeadsArmy ? ledArmyTroops : ownPartyTroops;

            return visible > 0 ? visible : 0;
        }
    }
}
