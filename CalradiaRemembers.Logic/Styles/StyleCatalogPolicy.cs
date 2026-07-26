// Code written by Gabriel Mailhot, 26/07/2026.
// Injectable narrative style pillar: which style NAME actually applies. A style is a prompt-rules file
// under ModuleData/Styles/*.txt (the file name is the display name), complementary to the existing
// behaviour rules (WHO the character is) but describing HOW the LLM writes (the voice). This policy owns
// only the name resolution, never the file contents: given what the player asked for and what style files
// exist on disk, it decides the CANONICAL name the engine should open and the popup should show. Consumed
// by the mod's StyleService before every prompt build. Get this wrong and either the popup shows the wrong
// casing (so the engine opens the wrong file, or none), or a deleted/renamed style silently blanks the
// voice instead of degrading to a known default.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Styles
{
    /// <summary>
    ///   Resolves which narrative style NAME applies, given the player's request and the set of style
    ///   files that currently exist on disk (one .txt per style under ModuleData/Styles). Pure name
    ///   arithmetic only: reading the file's contents is the engine's job.
    /// </summary>
    public static class StyleCatalogPolicy
    {
        /// <summary>The last-resort style name the engine treats as "no style file present".</summary>
        public const string DefaultStyleName = "Default";

        /// <summary>
        ///   Picks the style name that actually applies. Order: (1) no style files at all falls back to
        ///   <see cref="DefaultStyleName" />; (2) a requested name matching a file case-insensitively
        ///   returns that file's CANONICAL spelling (so the popup and the log show the real file name,
        ///   and the engine opens the right path); (3) otherwise (nothing requested, or an unknown name,
        ///   e.g. a style file deleted or renamed while selected) falls back to a "Default" entry's
        ///   canonical spelling if one exists, else <see cref="DefaultStyleName" />.
        /// </summary>
        /// <param name="requested">The style name the player picked, or null/blank if none yet.</param>
        /// <param name="available">The display names of the style files currently on disk.</param>
        /// <returns>The canonical style name to use.</returns>
        public static string ResolveName(string requested, IReadOnlyCollection<string> available)
        {
            if (available == null || available.Count == 0)
            {
                return DefaultStyleName;
            }

            if (!string.IsNullOrWhiteSpace(requested))
            {
                string requestedMatch = available.FirstOrDefault(name => string.Equals(name, requested, StringComparison.OrdinalIgnoreCase));
                if (requestedMatch != null)
                {
                    return requestedMatch;
                }
            }

            string defaultMatch = available.FirstOrDefault(name => string.Equals(name, DefaultStyleName, StringComparison.OrdinalIgnoreCase));

            return defaultMatch ?? DefaultStyleName;
        }
    }
}
