// Code written by Gabriel Mailhot, 11/07/2026.

#region

#endregion

namespace CalradiaRemembers.Logic.Rescue
{
    /// <summary>
    ///   Pure gate deciding whether a prisoner release should be credited to the player as a RESCUE worth
    ///   consequences (gratitude from the freed lord and their circle, and a possible grudge from the
    ///   captor who lost them). Only a genuine, player-caused freeing of a non-bandit lord who is not in
    ///   the player's own clan counts; releasing a bandit, a clanmate, or the player themselves is not a
    ///   rescue in this sense.
    /// </summary>
    public static class RescueEligibilityPolicy
    {
        /// <summary>
        ///   True only when the player won the battle (<paramref name="playerWonBattle"/>), the prisoner was
        ///   released as a direct result of it (<paramref name="releasedAfterBattle"/>), the freed prisoner is
        ///   a lord (<paramref name="prisonerIsLord"/>), and the prisoner is neither a bandit
        ///   (<paramref name="prisonerIsBandit"/>), a member of the player's own clan
        ///   (<paramref name="prisonerInPlayerClan"/>), nor the player (<paramref name="prisonerIsPlayer"/>).
        /// </summary>
        public static bool IsPlayerRescue(bool playerWonBattle, bool releasedAfterBattle, bool prisonerIsLord, bool prisonerIsBandit, bool prisonerInPlayerClan, bool prisonerIsPlayer)
        {
            return playerWonBattle
                && releasedAfterBattle
                && prisonerIsLord
                && !prisonerIsBandit
                && !prisonerInPlayerClan
                && !prisonerIsPlayer;
        }
    }
}
