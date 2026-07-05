// Code written by Gabriel Mailhot, 05/07/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The VRAM policy for character portraits, pure and engine-free. A character tableau is an expensive GPU
    ///   render-target; left one-per-transcript-line it exhausts VRAM on a long scene (the observed
    ///   create_render_texture crash). These rules bound the live render-targets to the number of distinct
    ///   participants present, never the transcript length. They operate only on <see cref="IManagedPortrait" />,
    ///   so they are unit-tested with fakes and the engine glue (<c>PortraitTextureGuard</c>) stays thin.
    /// </summary>
    public static class PortraitCapPolicy
    {
        /// <summary>
        ///   Keeps at most one live portrait per distinct <see cref="IManagedPortrait.PortraitKey" /> — the most
        ///   recent — and releases every older duplicate. <paramref name="linesOldestFirst" /> is the transcript
        ///   in chronological order (as it is appended). Returns how many render-targets were freed. Idempotent:
        ///   running it again on the same list frees nothing more.
        /// </summary>
        public static int KeepOnePerParticipant(IReadOnlyList<IManagedPortrait> linesOldestFirst)
        {
            if (linesOldestFirst == null) return 0;

            HashSet<string> keysWithLivePortrait = new HashSet<string>(StringComparer.Ordinal);
            int released = 0;

            // Newest-first: the first time a key is seen is that participant's most recent line — keep it; any
            // older line of the same participant is a duplicate render-target and is freed.
            for (int i = linesOldestFirst.Count - 1; i >= 0; i--)
            {
                IManagedPortrait line = linesOldestFirst[i];
                if (line == null || !line.HasLivePortrait) continue;

                if (!keysWithLivePortrait.Add(line.PortraitKey ?? ""))
                {
                    line.ReleasePortrait();
                    released++;
                }
            }

            return released;
        }

        /// <summary>
        ///   Frees every live portrait in the set (window teardown), so no render-target survives a close.
        ///   Returns how many were freed.
        /// </summary>
        public static int ReleaseAll(IReadOnlyList<IManagedPortrait> lines)
        {
            if (lines == null) return 0;

            int released = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                IManagedPortrait line = lines[i];
                if (line == null || !line.HasLivePortrait) continue;

                line.ReleasePortrait();
                released++;
            }

            return released;
        }
    }
}
