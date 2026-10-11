// Code written by Gabriel Mailhot, 11/10/2026.
// SPIKE console door for tale videos (see Portraits/CrVideoSurface.cs): plays a VP8 .ivf into the tale picture frame of
// the open conversation, stops it, and reports whether it finished.

#region

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Portraits;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.video_spike start &lt;file.ivf|test&gt; &lt;width&gt; &lt;height&gt; [fps] [loop] [screen] [green] | stop | status.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("video_spike", "cr")]
      public static string VideoSpike(List<string> args)
      {
         string verb = args is {Count: > 0} ? args[0].ToLowerInvariant() : "";
         switch (verb)
         {
            case "stop":
               ChatManager.ActiveViewModel?.ClearLiveSceneImage();
               CrVideoSurface.Stop();
               return "Video stopped.";
            case "status":
               return CrVideoSurface.CurrentPath == null ? "No video playing." : $"{CrVideoSurface.CurrentPath}: finished={CrVideoSurface.IsFinished()?.ToString() ?? "?"}";
            case "start":
               break;
            default:
               return "Usage: cr.video_spike start <file.ivf|test> <width> <height> [fps] [loop] [screen] [green] | stop | status";
         }

         if (args.Count < 4) return "Usage: cr.video_spike start <file.ivf|test> <width> <height> [fps] [loop] [screen] [green]";
         // "test": the spike's video in CR's own Videos folder, its path built exactly as the game builds its own.
         string file = args[1].Trim('"').ToLowerInvariant() == "test"
            ? TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath("CalradiaRemembers") + "Videos/cr_test.ivf"
            : args[1].Trim('"');
         if (!System.IO.File.Exists(file)) return "No such file: " + file;
         if (!int.TryParse(args[2], out int w) || !int.TryParse(args[3], out int h) || w <= 0 || h <= 0) return "Width and height must be positive whole numbers.";
         float fps = args.Count > 4 && float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && f > 0 ? f : 24f;
         var flags = new HashSet<string>(args.Skip(4).Select(a => a.ToLowerInvariant()));
         bool loop = flags.Contains("loop");
         bool toScreen = flags.Contains("screen");
         uint clear = flags.Contains("green") ? 0xFF00FF00u : 0u;

         // Fourth run (11/10/2026): the video plays on the campaign map but never inside a conversation. "cinema" pushes
         // the game's own video state over whatever is open, as the campaign intro is played, and pops back when it ends:
         // the full-screen fallback, and a test of whether the conversation underneath blocks it too.
         if (flags.Contains("cinema"))
         {
            string sound = System.IO.Path.ChangeExtension(file, ".ogg");
            if (!System.IO.File.Exists(sound)) sound = "";
            var state = TaleWorlds.Core.Game.Current.GameStateManager.CreateState<TaleWorlds.MountAndBlade.VideoPlaybackState>();
            state.SetStartingParameters(file, sound, "", fps, true);
            state.SetOnVideoFinisedDelegate(() =>
            {
               ModLog.Info("[VIDEO] cinema finished; back to what was open.");
               TaleWorlds.Core.Game.Current.GameStateManager.PopState();
            });
            TaleWorlds.Core.Game.Current.GameStateManager.PushState(state);
            ModLog.Info($"[VIDEO] cinema pushed: {file} with sound '{sound}', conversation open={ChatManager.ActiveViewModel != null}.");

            return "Cinema pushed (Escape skips it).";
         }

         ChatViewModel vm = ChatManager.ActiveViewModel;
         // Third run (11/10/2026): not one frame decoded, in a conversation (a Mission). Screen mode runs without one, on
         // the campaign map, to tell whether the player works at all outside the game's own video screens.
         if (vm == null && !toScreen) return "Open a conversation first (the video shows where a tale's picture stands), or add 'screen'.";

         vm?.ClearLiveSceneImage(); // off the frame before Start frees a video already playing
         string live = CrVideoSurface.Start(file, w, h, fps, loop, toScreen, clear, out string report);
         if (live == null) return "Could not start: " + report;
         if (toScreen) return report + " (cr.video_spike stop to end it)";
         if (!vm.ShowLiveSceneImage(live, w, h))
         {
            CrVideoSurface.Stop();
            return "The Classic skin is open: the spike shows the video on the Manuscript skin only (cr.skin). Nothing played.";
         }

         return report;
      }
   }
}
