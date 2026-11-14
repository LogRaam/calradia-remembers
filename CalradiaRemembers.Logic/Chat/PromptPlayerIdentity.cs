// Code written by Gabriel Mailhot, 14/11/2026.
// Icezera (Discord, 13/11/2026): "Used the player switcher to switch character and NPC's are still thinking that I'm the
// original character. Even replaced the player_description with the new character." The character prompt carries the
// player's name and gender, built once and kept until the player's clan, realm or a setting changed: a switch to someone of
// the same clan kept the old name until the save was reloaded. This is the identity a built prompt belongs to; when it
// differs, the prompt is built again.

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>The player identity a built character prompt is tied to.</summary>
    public static class PromptPlayerIdentity
    {
        private const char Separator = '\u001f';

        /// <summary>
        ///   One string naming who the player is for the prompt: their hero, name, gender, clan and realm. Two stamps differ
        ///   whenever any of these differ, so a switch to another hero of the same clan, or a renamed hero, rebuilds the prompt.
        /// </summary>
        public static string Stamp(string heroId, string name, bool isFemale, string clan, string realm)
            => string.Join(Separator.ToString(), heroId ?? "", name ?? "", isFemale ? "f" : "m", clan ?? "", realm ?? "");
    }
}
