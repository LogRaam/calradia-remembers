// Code written by Gabriel Mailhot, 26/07/2026.
// Injectable narrative style pillar: whether the cached style TEXT must be re-read from disk before the
// next prompt. Reading a style file is expensive, so the engine's StyleService caches the text and only
// pays the disk cost when it truly must, per Gabriel's ruling: (a) the player picked a different style, or
// (b) the .txt file itself changed on disk, so a modder can fine-tune a style DURING play and see it apply
// on the very next message. Get this wrong and either a stale voice keeps applying after a pick or an edit
// (silently blanking the narrative voice the player expects), or the engine stats/rereads the file on every
// single message for no reason.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Styles
{
    /// <summary>
    ///   Decides whether the engine must re-read a style file from disk before building the next prompt,
    ///   given what is currently cached and the file's last-write stamp. Pure comparison only: the engine
    ///   is responsible for actually stat-ing the file and reading its text.
    /// </summary>
    public static class StyleCachePolicy
    {
        /// <summary>
        ///   True when the engine must re-read the style file from disk. Order: (1) nothing cached yet
        ///   (<paramref name="cachedName" /> null/blank) reloads, the first message of a session must
        ///   load the style; (2) the cached name differs from the requested one (case-insensitive)
        ///   reloads, the player picked another style in the popup; (3) the cached stamp differs from the
        ///   current one reloads, the file changed on disk. Compared with <c>!=</c> rather than "newer
        ///   than": a modder restoring an OLDER copy of a style file (e.g. from version control, or an
        ///   undo) must still be picked up, and a strictly-newer test would silently keep serving the
        ///   stale cache in that case; (4) otherwise the cache is still valid and no disk read is needed.
        /// </summary>
        /// <param name="cachedName">The style name currently cached, or null/blank if nothing is cached.</param>
        /// <param name="requestedName">The style name currently selected by the player.</param>
        /// <param name="cachedStampTicks">The file's last-write stamp (ticks) at the time it was cached.</param>
        /// <param name="currentStampTicks">The file's last-write stamp (ticks) as of the current send.</param>
        /// <returns>True if the engine must re-read the style file from disk.</returns>
        public static bool ShouldReload(string cachedName, string requestedName, long cachedStampTicks, long currentStampTicks)
        {
            if (string.IsNullOrWhiteSpace(cachedName))
            {
                return true;
            }

            if (!string.Equals(cachedName, requestedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return cachedStampTicks != currentStampTicks;
        }
    }
}
