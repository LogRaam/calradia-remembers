// Code written by Gabriel Mailhot, 11/10/2026.
// Console door to CR's own video player (Chat/CrVideoPlayer.cs): play a .crvid in the open conversation's picture frame,
// replay it, loop it, stop it, and the two pixel switches the first look in game settles (colour order, orientation).

#region

using System.Collections.Generic;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Video;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.video play &lt;file.crvid&gt; [loop] | replay | loop on|off | stop | status | swaprb on|off | flip on|off.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("video", "cr")]
      public static string Video(List<string> args)
      {
         string verb = args is {Count: > 0} ? args[0].ToLowerInvariant() : "status";
         string on = args is {Count: > 1} ? args[1].ToLowerInvariant() : "";
         switch (verb)
         {
            case "play":
               if (args.Count < 2) return "Usage: cr.video play <file.crvid> [loop]";
               string file = args[1].Trim('"');
               if (!System.IO.File.Exists(file)) return "No such file: " + file;
               bool? loop = args.Count > 2 && args[2].ToLowerInvariant() == "loop" ? true : null;
               return CrVideoPlayer.Play(file, loop) ?? CrVideoPlayer.Describe();
            case "replay":
               return CrVideoPlayer.Replay() ?? CrVideoPlayer.Describe();
            case "loop":
               CrVideoPlayer.SetLoop(on != "off");
               return CrVideoPlayer.Describe();
            case "stop":
               CrVideoPlayer.Stop();
               return "Video stopped.";
            case "swaprb":
               CrvidDecoder.SwapRedBlue = on != "off";
               return $"Red and blue swapped: {CrvidDecoder.SwapRedBlue}. Takes effect on the next frames decoded (cr.video replay).";
            case "flip":
               CrvidDecoder.FlipVertical = on != "off";
               return $"Rows flipped: {CrvidDecoder.FlipVertical}. Takes effect on the next frames decoded (cr.video replay).";
            default:
               return CrVideoPlayer.Describe();
         }
      }
   }
}
