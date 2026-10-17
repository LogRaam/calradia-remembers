// Code written by Gabriel Mailhot, 17/10/2026.
// Gabriel (17/10/2026), on a grievance reading "Holds an old bitterness against you, over The marriage was ended by
// the player's own choice": never "the player" in the game, the player's character by name; true of every text the
// game shows, logs aside. Stored memories and causes, and the summaries a model writes (its prompts call him "the
// player"), carry the role word into old saves and new ones alike, so the screen swaps it for the name as it shows them.

#region

using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure: names the player's character where a text shown in game says "the player".</summary>
    public static class PlayerNameText
    {
        // "the player character" first, so it does not become "Name character"; then "the player" alone. The
        // trailing \b leaves "players" and "players'" untouched; "the player's" keeps its "'s" after the name.
        private static readonly Regex RoleWord = new Regex(@"\b[Tt]he player(?: character)?\b", RegexOptions.CultureInvariant);

        /// <summary>
        ///   The text with every "the player" / "The player" (and "the player character") replaced by
        ///   <paramref name="playerName" />, possessives kept; the text unchanged when the name is unknown.
        /// </summary>
        public static string ForDisplay(string text, string playerName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(playerName)) return text;
            if (text.IndexOf("he player", System.StringComparison.Ordinal) < 0) return text;

            string name = playerName.Trim();

            return RoleWord.Replace(text, _ => name);
        }
    }
}
