// Code written by Gabriel Mailhot, 11/10/2026.
// A .crvid's first frame as an image file. Gabriel (11/10/2026): "le premier frame du video EST l'image." Where the video
// cannot play (the Classic skin shows a tale's picture in its pop-out panel, which takes an image file), its first frame is
// shown still. Extracted once per video file into a cache folder under the temp directory, named by the file's path and
// time, so a changed video is extracted again.

#region

using System;
using System.IO;
using System.IO.Compression;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Video;

#endregion

namespace CalradiaRemembers.Video
{
   /// <summary>The first frame of a .crvid, as a JPEG file the image loaders can read.</summary>
   internal static class CrvidPoster
   {
      private static readonly string Folder = Path.Combine(Path.GetTempPath(), "CalradiaRemembers_crvid_posters");

      /// <summary>The path of the first frame's JPEG, extracted if needed; null when the video cannot be read.</summary>
      public static string FirstFrameOf(string crvidPath)
      {
         try
         {
            var info = new FileInfo(crvidPath);
            if (!info.Exists) return null;

            string name = $"{(uint) crvidPath.ToLowerInvariant().GetHashCode():x8}_{info.LastWriteTimeUtc.Ticks:x}.jpg";
            string poster = Path.Combine(Folder, name);
            if (File.Exists(poster)) return poster;

            Directory.CreateDirectory(Folder);
            using (ZipArchive zip = ZipFile.OpenRead(crvidPath))
            {
               ZipArchiveEntry first = zip.GetEntry(CrvidRules.FrameEntry(0));
               if (first == null) return null;
               first.ExtractToFile(poster, true);
            }

            return poster;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[VIDEO] first frame of {Path.GetFileName(crvidPath)} could not be extracted: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }
   }
}
