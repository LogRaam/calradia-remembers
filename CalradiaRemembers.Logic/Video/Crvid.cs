// Code written by Gabriel Mailhot, 11/10/2026.
// CR's own video format and clock. The game's video player stops whenever a conversation is open (vanilla or CR: Gabriel's
// runs, 11/10/2026), and a tale is always lived in conversation. Gabriel: "on pourrait developper notre propre runtime ...
// qui appartiendrait au chat et qui permettrait de jouer un video", from "une sequence d'images dans une archive". A .crvid
// is a zip of JPEG frames and a manifest; a background thread decodes them ahead, and the chat shows one new texture per
// frame (measured: 768x1152 at 30 Hz, no frame skipped, the game unslowed, graphics memory flat). This file is the pure
// part: what a manifest must say, and which frame shows at a moment. Played once, a video comes back to its first frame
// (Gabriel: "je jouerais le video 1 seule fois et reviendrais au premier frame"); looped, it starts over.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Video
{
   /// <summary>A .crvid's manifest.json: what the frames are and how fast they play.</summary>
   public sealed class CrvidManifest
   {
      /// <summary>The format's version; 1 today.</summary>
      public int Version { get; init; }

      /// <summary>Frames per second.</summary>
      public float Fps { get; init; }

      /// <summary>Every frame's width in pixels.</summary>
      public int Width { get; init; }

      /// <summary>Every frame's height in pixels.</summary>
      public int Height { get; init; }

      /// <summary>How many frames the archive holds (frames/000001.jpg onward).</summary>
      public int Frames { get; init; }

      /// <summary>Whether it loops by default (the player can change it).</summary>
      public bool Loop { get; init; }
   }

   /// <summary>What a manifest must say for the game to play it. Pure.</summary>
   public static class CrvidRules
   {
      /// <summary>The format version this game reads.</summary>
      public const int Version = 1;

      /// <summary>TUNE: the fastest rate played; above it frames would be skipped on a 60 Hz game.</summary>
      public const float MaxFps = 60f;

      /// <summary>
      ///   TUNE: the largest frame in pixels (768 x 1152, measured in game on 11/10/2026: a new texture per frame at 30 Hz,
      ///   none skipped, graphics memory +15 MB and flat). Any shape fits as long as it holds no more pixels than that.
      /// </summary>
      public const int MaxPixels = 768 * 1152;

      /// <summary>Every reason the manifest cannot be played; empty when it can.</summary>
      public static IReadOnlyList<string> Problems(CrvidManifest m)
      {
         var problems = new List<string>();
         if (m == null) return new[] {"no manifest.json"};
         if (m.Version != Version) problems.Add($"version {m.Version} is not one this game reads ({Version})");
         if (!(m.Fps > 0 && m.Fps <= MaxFps)) problems.Add($"fps {m.Fps} must be above 0 and at most {MaxFps}");
         if (m.Width <= 0 || m.Height <= 0) problems.Add($"size {m.Width}x{m.Height} must be positive");
         else if ((long) m.Width * m.Height > MaxPixels) problems.Add($"size {m.Width}x{m.Height} is larger than {MaxPixels} pixels (768x1152)");
         if (m.Frames <= 0) problems.Add("it holds no frame");

         return problems;
      }

      /// <summary>The archive entry of frame <paramref name="index" /> (0-based): frames/000001.jpg for the first.</summary>
      public static string FrameEntry(int index) => $"frames/{index + 1:D6}.jpg";
   }

   /// <summary>Which frame shows <paramref name="Seconds" /> into a video, and whether a played-once video is over.</summary>
   public readonly struct VideoMoment
   {
      public VideoMoment(int frame, bool finished)
      {
         Frame = frame;
         Finished = finished;
      }

      /// <summary>The frame to show (0-based).</summary>
      public int Frame { get; }

      /// <summary>A video played once that has reached its end: it shows its first frame again and waits.</summary>
      public bool Finished { get; }
   }

   /// <summary>The frame a video shows at a moment. Pure.</summary>
   public static class VideoClock
   {
      /// <summary>
      ///   The frame at <paramref name="seconds" /> since the video started: looped, it starts over at the end; played
      ///   once, it is finished at the end and shows its first frame again.
      /// </summary>
      public static VideoMoment At(double seconds, float fps, int frames, bool loop)
      {
         if (frames <= 0 || fps <= 0) return new VideoMoment(0, true);
         var index = (long) Math.Floor(Math.Max(0, seconds) * fps);
         if (loop) return new VideoMoment((int) (index % frames), false);

         return index >= frames ? new VideoMoment(0, true) : new VideoMoment((int) index, false);
      }
   }
}
