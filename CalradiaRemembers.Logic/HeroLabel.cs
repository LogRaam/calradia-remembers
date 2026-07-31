// Code written by Gabriel Mailhot, 31/07/2026.
// Bare first names collide across heroes and generations: a rumor about a Battanian lord and the
// player's own six-year-old son shared the one word "Luichan", and an NPC spoke as if the child had
// taken a castle. This appends the sharp disambiguator, clan then culture, so the model tells
// namesakes apart. Pure so it is unit-tested and reused at every site that names a hero.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Builds a disambiguating label for a hero from primitives, since this project cannot reference
    ///   the engine <c>Hero</c> type directly (callers pass the strings they already read off it).
    /// </summary>
    public static class HeroLabel
    {
        /// <summary>
        ///   Builds "Luichan of Fen Company (Battanian)" from what is supplied. Clan is appended as
        ///   " of {clan}" unless it is blank or a clanless sentinel ("No clan", "Unaligned"); culture
        ///   is appended in parentheses. The point is that a namesake in one clan is told apart from a
        ///   namesake in another.
        /// </summary>
        public static string Describe(string name, string clanName, string cultureName = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return name ?? "";

            string label = name.Trim();

            if (!string.IsNullOrWhiteSpace(clanName) &&
                !string.Equals(clanName.Trim(), "No clan", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(clanName.Trim(), "Unaligned", StringComparison.OrdinalIgnoreCase))
                label += " of " + clanName.Trim();

            if (!string.IsNullOrWhiteSpace(cultureName))
                label += " (" + cultureName.Trim() + ")";

            return label;
        }
    }
}
