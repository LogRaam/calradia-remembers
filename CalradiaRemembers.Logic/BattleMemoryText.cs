// Code written by Gabriel Mailhot, 08/08/2026.
// The words a battle leaves in the profile of someone who stood on the player's side. Shared by the
// two places that write it: the lords who led their own party in the fight, and the companions who
// rode inside the player's own party (a player report, 08/08/2026: allied lords remembered fighting
// together but party companions did not, because only party LEADERS were ever visited). Pure so the
// one phrasing stays identical for both and the wording is pinned by a test.

namespace CalradiaRemembers.Logic
{
    /// <summary>Builds the "fought beside you" memory line. The host decides who is on the player's side.</summary>
    public static class BattleMemoryText
    {
        /// <summary>
        ///   The line left in an ally's profile after a notable battle the player fought in.
        ///   <paramref name="near" /> is an optional place clause already shaped as " near X" (or empty).
        /// </summary>
        public static string AllySummary(bool playerWon, string near)
            => $"Fought at your side when you {(playerWon ? "broke the enemy" : "stood against the enemy")}{near}.";
    }
}
