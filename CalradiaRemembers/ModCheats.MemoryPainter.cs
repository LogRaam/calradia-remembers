// Code written by Gabriel Mailhot, 20/11/2026.
// cr.memory_painter: a stand-in for a mod that paints memories, so the Memories panel's pictures can be seen without one.
// Gabriel (20/11/2026), after cr.test memory_pictures: "je ne comprend pas comment faire le test de l'image". No mod paints
// yet (Bhelogan's is being written), so the "Paint this memory" button never showed. On, this listens to
// CrNpcMemory.ImageRequested exactly as a real painter would, waits two seconds (the button reads "Painting..."), and hands
// back one of CR's own council pictures through CrNpcMemory.SetImage. Off by default, never saved: a test tool.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CalradiaRemembers.Foundation;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      private static readonly string[] StandInPictures = {
         "TavernCounsel.jpg", "OutsideCounselDesert.jpg", "OutsideCounselForestMountains.jpg", "OutsideCounselSteppes.jpg",
         "FamilyCounselFemale.jpg", "WarCounselMale.jpg"
      };

      private static bool _standInPainterOn;

      /// <summary>
      ///   cr.memory_painter on|off: a stand-in painter for the Memories panel (see the file header). With it on, open a
      ///   conversation, press Memories, click a memory: "Paint this memory" appears, and the picture comes two seconds later.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory_painter", "cr")]
      public static string MemoryPainter(List<string> args)
      {
         string arg = args != null && args.Count > 0 ? args[0].Trim().ToLowerInvariant() : "";
         if (arg == "on")
         {
            if (!_standInPainterOn) CrNpcMemory.ImageRequested += PaintWithAStandIn;
            _standInPainterOn = true;

            return "Stand-in painter ON. Open a conversation, press Memories, click a memory, then 'Paint this memory'. "
                 + "A picture of CR's own comes two seconds later; click it to enlarge. cr.memory_painter off to stop.";
         }

         if (arg == "off")
         {
            CrNpcMemory.ImageRequested -= PaintWithAStandIn;
            _standInPainterOn = false;

            return "Stand-in painter OFF. Pictures already set stay on their memories.";
         }

         return $"Usage: cr.memory_painter on|off (now {(_standInPainterOn ? "on" : "off")}).";
      }

      private static void PaintWithAStandIn(CrMemoryImageRequest request)
      {
         string name = StandInPictures[(int) ((uint) (request.MemoryId ?? "").GetHashCode() % StandInPictures.Length)];
         string path = Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", name);
         ModLog.Info($"[MEMORY PICTURE] stand-in painter: painting {request.NpcId}'s memory {request.MemoryId} with {name}.");

         Task.Run(async () =>
         {
            await Task.Delay(2000).ConfigureAwait(false);
            if (!File.Exists(path) || !CrNpcMemory.SetImage(request.NpcId, request.MemoryId, path))
               CrNpcMemory.ImageFailed(request.NpcId, request.MemoryId);
         });
      }
   }
}
