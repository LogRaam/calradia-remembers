// Code written by Gabriel Mailhot, 13/11/2026.
// Aporo (Discord, 13/11/2026): "I don't need that 100 denars for bringing 20 grain when it's my money either way ...
// i told her before that i need no payment for that and still got it". Gabriel: "tout le clan". A task's coin leaves the
// giver's purse for the player's; within the player's own clan that is the player's money going round in a circle. This
// decides the coin a task carries once its giver is known.

namespace CalradiaRemembers.Logic.Quests
{
    /// <summary>The coin a task pays, given who gives it.</summary>
    public static class ClanTaskRewardPolicy
    {
        /// <summary>
        ///   None when the giver belongs to the player's own clan (spouse, kin, companion, any member): they share one purse
        ///   with the player, and their thanks is regard. Anyone else pays what they promised.
        /// </summary>
        public static int GoldFor(bool giverInPlayerClan, int promisedGold) => giverInPlayerClan ? 0 : promisedGold;
    }
}
