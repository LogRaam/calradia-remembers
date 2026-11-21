// Code written by Gabriel Mailhot, 20/11/2026.
// Bhelogan (Discord, 20/11/2026) asked to put his mod's pictures in the Memories panel. Gabriel: "What if I click on a
// memory record and it shows an image left to that panel? The new panel could have a 'generate new image' button that raise
// an event", then "oui on y va. et on protège les IDS associés à des images". Bhelogan's agent: "We generate (20-60 s, so
// async), then call SetImage. A 'busy' state on the button while that runs would be ideal", "We'd rather you copy it, so
// our own cleanup can't leave a memory pointing at nothing. Also what happens in the panel if the file is missing."
//
// Decides whether a picture handed to CrNpcMemory.SetImage is taken, the name CR keeps it under, whether the panel offers
// to paint a memory, and how long a painting may take. If it is wrong: a video or a missing file reaches the panel, a
// repainted memory shows the old picture (CR keeps a loaded image by its path), a secret memory's words are handed to
// another mod, or the button stays "painting" forever after a mod failed silently.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Extension
{
    /// <summary>What CR knows about a picture another mod hands it for a memory.</summary>
    public sealed class MemoryPictureFacts
    {
        /// <summary>The mod named a character and a memory.</summary>
        public bool Named { get; init; }

        /// <summary>The file exists on disk.</summary>
        public bool FileExists { get; init; }

        /// <summary>The file's extension, with its dot (".png").</summary>
        public string Extension { get; init; }

        /// <summary>A campaign is loaded.</summary>
        public bool CampaignLoaded { get; init; }
    }

    /// <summary>What the Memories panel knows about the memory the player selected.</summary>
    public sealed class MemoryPaintFacts
    {
        /// <summary>At least one mod listens for picture requests.</summary>
        public bool AnyPainter { get; init; }

        /// <summary>The memory is a secret (CR keeps it out of every other mod's reach).</summary>
        public bool IsPrivate { get; init; }

        /// <summary>The memory carries a picture already.</summary>
        public bool HasPicture { get; init; }

        /// <summary>A painting of this memory was asked for and has not come back yet.</summary>
        public bool Busy { get; init; }
    }

    /// <summary>Memory pictures: which are taken, their file names, the panel's paint button.</summary>
    public static class MemoryPicturePolicy
    {
        /// <summary>
        ///   How long a painting may take before the button is offered again, in seconds. Bhelogan's agent: 20 to 60 seconds;
        ///   three minutes leaves a slow local generator its time.
        /// </summary>
        public const int PaintingTimeoutSeconds = 180;

        /// <summary>Null when the picture is taken; else why not, for the [MEMORY PICTURE] log line.</summary>
        public static string WhyRefused(MemoryPictureFacts f)
        {
            if (f == null || !f.Named) return "no character or memory was named";
            if (!f.CampaignLoaded) return "no campaign is loaded";
            if (!ExternalPicturePolicy.IsStillImage(f.Extension)) return $"'{f.Extension}' is not a picture CR shows (.png, .jpg, .jpeg)";
            if (!f.FileExists) return "the file does not exist";

            return null;
        }

        /// <summary>
        ///   The name CR keeps the copy under: the memory's id, a stamp unique to this copy (a repainted memory gets a new name,
        ///   or the panel would keep showing the picture it loaded first), and the extension in lower case.
        /// </summary>
        public static string FileName(string memoryId, string extension)
        {
            string stamp = Guid.NewGuid().ToString("N").Substring(0, 8);
            string ext = (extension ?? "").Trim().ToLowerInvariant();
            if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;

            return $"{Safe(memoryId)}_{Safe(stamp)}{ext}";
        }

        /// <summary>
        ///   The panel offers to paint the selected memory: a mod listens, and the memory is not a secret (GetMemories withholds
        ///   secrets from other mods, and a request carries the memory's words).
        /// </summary>
        public static bool OffersPainting(MemoryPaintFacts f) => f != null && f.AnyPainter && !f.IsPrivate;

        /// <summary>The paint button's words: busy while a painting is on its way, "anew" when a picture is there already.</summary>
        public static string PaintLabel(MemoryPaintFacts f)
        {
            if (f?.Busy == true) return "Painting...";

            return f?.HasPicture == true ? "Paint it anew" : "Paint this memory";
        }

        /// <summary>A name part kept to plain letters, digits and dashes (a campaign id or a memory id as a folder or file name).</summary>
        public static string Safe(string part)
        {
            var sb = new StringBuilder();
            foreach (char c in part ?? "")
                if (c < 128 && (char.IsLetterOrDigit(c) || c == '-')) sb.Append(c);

            return sb.Length == 0 ? "x" : sb.ToString();
        }
    }

    /// <summary>The paintings asked for and not yet back, by memory id. The game thread only.</summary>
    public sealed class MemoryPaintingsUnderway
    {
        private readonly Dictionary<string, DateTime> _asked = new(StringComparer.Ordinal);

        /// <summary>A painting of this memory was asked for at <paramref name="now" />.</summary>
        public void Begin(string memoryId, DateTime now)
        {
            if (!string.IsNullOrEmpty(memoryId)) _asked[memoryId] = now;
        }

        /// <summary>The painting came back, or failed.</summary>
        public void End(string memoryId)
        {
            if (!string.IsNullOrEmpty(memoryId)) _asked.Remove(memoryId);
        }

        /// <summary>A painting is on its way and not older than <see cref="MemoryPicturePolicy.PaintingTimeoutSeconds" />.</summary>
        public bool IsBusy(string memoryId, DateTime now)
        {
            if (string.IsNullOrEmpty(memoryId) || !_asked.TryGetValue(memoryId, out DateTime at)) return false;
            if ((now - at).TotalSeconds <= MemoryPicturePolicy.PaintingTimeoutSeconds) return true;

            _asked.Remove(memoryId);

            return false;
        }

        /// <summary>Forgets every painting: another campaign was loaded.</summary>
        public void Clear() => _asked.Clear();
    }
}
