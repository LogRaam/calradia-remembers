// Code written by Gabriel Mailhot, 11/10/2026.
// SPIKE console door for tale videos (see Portraits/CrVideoSurface.cs): plays a VP8 .ivf into the tale picture frame of
// the open conversation, stops it, and reports whether it finished.

#region

using System.Collections.Generic;
using System.Globalization;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Portraits;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.video_spike start &lt;file.ivf&gt; &lt;width&gt; &lt;height&gt; [fps] [loop] | stop | status.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("video_spike", "cr")]
      public static string VideoSpike(List<string> args)
      {
         string verb = args is {Count: > 0} ? args[0].ToLowerInvariant() : "";
         switch (verb)
         {
            case "stop":
               CrVideoSurface.Stop();
               return "Video stopped.";
            case "status":
               return CrVideoSurface.CurrentPath == null ? "No video playing." : $"{CrVideoSurface.CurrentPath}: finished={CrVideoSurface.IsFinished()?.ToString() ?? "?"}";
            case "start":
               break;
            default:
               return "Usage: cr.video_spike start <file.ivf> <width> <height> [fps] [loop] | stop | status";
         }

         if (args.Count < 4) return "Usage: cr.video_spike start <file.ivf> <width> <height> [fps] [loop]";
         string file = args[1].Trim('"');
         if (!System.IO.File.Exists(file)) return "No such file: " + file;
         if (!int.TryParse(args[2], out int w) || !int.TryParse(args[3], out int h) || w <= 0 || h <= 0) return "Width and height must be positive whole numbers.";
         float fps = args.Count > 4 && float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && f > 0 ? f : 24f;
         bool loop = args.Count > 5 && args[5].ToLowerInvariant() == "loop";

         ChatViewModel vm = ChatManager.ActiveViewModel;
         if (vm == null) return "Open a conversation first (the video shows where a tale's picture stands).";

         string live = CrVideoSurface.Start(file, w, h, fps, loop, out string report);
         if (live == null) return "Could not start: " + report;
         if (!vm.ShowLiveSceneImage(live, w, h)) return report + " But the Classic skin is open: the spike shows the video on the Manuscript skin only.";

         return report;
      }
   }
}
