// Code written by Gabriel Mailhot, 11/10/2026.
// The pictures a CrRuntimeImageWidget shows that are not files: each frame of a tale's video (Chat/CrVideoPlayer) is a
// texture made at run time, and a widget's ImagePath is a string, so a frame is named "cr-live:<name>" and looked up
// here. This registry was born in the tale-video spike (11/10/2026), whose native player (VideoPlayerView) did not play
// inside a conversation and was removed; CR's own .crvid player kept the registry.

#region

using System.Collections.Generic;
using TaleWorlds.TwoDimension;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>Sprites made at run time, shown by any <see cref="CrRuntimeImageWidget" /> whose path names them.</summary>
    internal static class LiveSprites
    {
        /// <summary>The image-path prefix a widget reads as "a live sprite", not a file.</summary>
        public const string LivePrefix = "cr-live:";

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        /// <summary>Makes <paramref name="sprite" /> the one a widget shows for <paramref name="livePath" />.</summary>
        public static void Register(string livePath, Sprite sprite) => Sprites[livePath] = sprite;

        /// <summary>Forgets a live sprite no widget shows any more.</summary>
        public static void Unregister(string livePath) => Sprites.Remove(livePath);

        /// <summary>The sprite a widget shows for <paramref name="livePath" />, or null when none is registered under it.</summary>
        public static Sprite SpriteFor(string livePath)
            => livePath != null && Sprites.TryGetValue(livePath, out Sprite s) ? s : null;
    }
}
