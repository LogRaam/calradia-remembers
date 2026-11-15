// Code written by Gabriel Mailhot, 15/11/2026.
// LapitaPolata (Discord, 15/11/2026, "Add an option to expand/collapse the Friends and enemies, and Houses section in the
// cyclopedia"): "The longer I play the game, the longer the lists have become." Gabriel (15/11/2026): "on va l'ajouter avant
// de livrer sur Nexus". The header of each section becomes the switch: it says how many rows it holds and whether a click
// shows or hides them, so a folded section still tells the player there is something under it.
//
// If this is wrong: a folded section reads as an empty one (no count), the header loses its own words, or the switch shows
// the opposite of what a click does.

namespace CalradiaRemembers.Logic
{
    /// <summary>The header of an encyclopedia section the player can fold. Pure.</summary>
    public static class FoldableSectionHeader
    {
        /// <summary>
        ///   "Friends and enemies, and why (3) [-]" while shown, "(3) [+]" while folded: the section's own label without its
        ///   closing colon, the number of rows, and what a click does. Empty when there is no label or no row.
        /// </summary>
        public static string Text(string label, int rows, bool shown)
        {
            if (string.IsNullOrWhiteSpace(label) || rows <= 0) return "";

            string words = label.Trim();
            if (words.EndsWith(":")) words = words.Substring(0, words.Length - 1).TrimEnd();

            return $"{words} ({rows}) {(shown ? "[-]" : "[+]")}";
        }
    }
}
