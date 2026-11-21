// Code written by Gabriel Mailhot, 20/11/2026.
// Extension Surface: pictures of memories, and the memory-written announcement. Bhelogan (Discord, 20/11/2026): "Do we have,
// or can we get an API hook to put images into the Memories GUI". Gabriel: "What if I click on a memory record and it shows an
// image left to that panel? The new panel could have a 'generate new image' button that raise an event", then "oui on y va.
// et on protège les IDS associés à des images". Every subscriber is invoked under its own guard; a bad call never throws.

#region

using System;
using System.IO;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Extension;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   public static partial class CrNpcMemory
   {
      /// <summary>
      ///   Fired when a memory has been written since CR last looked: a new memory, or one whose words were rewritten (the
      ///   summary that turns a conversation's "Spoke with X." into what was said arrives this way, with
      ///   <see cref="CrMemoryWritten.IsNew" /> false). CR looks each in-game hour, as a conversation opens and as it closes
      ///   (the memories born in it then carry its <see cref="CrMemoryWritten.SessionId" />), and after
      ///   <see cref="AddMemory" />. Secrets are never announced. On the game thread. Since 2.8.18 (CrApi.Version 6).
      /// </summary>
      public static event Action<CrMemoryWritten> MemoryWritten;

      /// <summary>
      ///   Fired when the player presses "Paint this memory" in the Memories panel. The button shows only while at least one
      ///   mod listens, and never on a secret. The button reads "Painting..." until you call <see cref="SetImage" /> or
      ///   <see cref="ImageFailed" /> with the same ids, or for three minutes. On the game thread; paint on your own.
      ///   Since 2.8.18 (CrApi.Version 6).
      /// </summary>
      public static event Action<CrMemoryImageRequest> ImageRequested;

      /// <summary>
      ///   Puts a picture on one memory (<paramref name="memoryId" /> from <see cref="GetMemories" />,
      ///   <see cref="MemoryWritten" /> or <see cref="ImageRequested" />). CR COPIES the file (a .png, .jpg or .jpeg that
      ///   exists) into its own folder for this campaign, so you may delete yours at once; a memory painted anew drops its
      ///   old copy. A memory with a picture is never dropped by memory compression. The player sees it by clicking the memory
      ///   in the Memories panel. Pass a null <paramref name="imagePath" /> to take the picture away.
      ///   <para>
      ///     Any thread (the copy is made on yours). Returns false, doing nothing, when the call is refused (the reason is
      ///     logged as [MEMORY PICTURE]); true when it is accepted, and the memory then takes it on the game thread at the next
      ///   frame (if the memory is gone by then, the copy is deleted and the refusal logged). Never throws.
      ///   </para>
      /// </summary>
      public static bool SetImage(string npcId, string memoryId, string imagePath)
      {
         try
         {
            string saveId = Campaign.Current?.UniqueGameId;
            bool named = !string.IsNullOrWhiteSpace(npcId) && !string.IsNullOrWhiteSpace(memoryId);
            string fileName = null;

            if (imagePath != null)
            {
               string why = MemoryPicturePolicy.WhyRefused(new MemoryPictureFacts {
                  Named = named, CampaignLoaded = saveId != null, FileExists = File.Exists(imagePath), Extension = Path.GetExtension(imagePath)
               });
               if (why != null)
               {
                  ModLog.Info($"[MEMORY PICTURE] refused ({why}): {Path.GetFileName(imagePath)}");

                  return false;
               }

               fileName = MemoryPictures.CopyIn(saveId, memoryId, imagePath);
               if (fileName == null) return false;
            }
            else if (!named || saveId == null) return false;

            MainThreadDispatcher.EnqueueLasting(() =>
            {
               // Another save loaded meanwhile: the picture belongs to the campaign it was painted for.
               if (!string.Equals(Campaign.Current?.UniqueGameId, saveId, StringComparison.Ordinal)) return;
               MemoryPictures.Attach(npcId, memoryId, fileName);
            });

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MEMORY PICTURE] SetImage threw and was isolated: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Says a painting asked for through <see cref="ImageRequested" /> will not come, so the panel offers the button again
      ///   at once. Any thread. Never throws. Since 2.8.18.
      /// </summary>
      public static void ImageFailed(string npcId, string memoryId)
      {
         try
         {
            if (string.IsNullOrWhiteSpace(memoryId)) return;
            MainThreadDispatcher.EnqueueLasting(() => MemoryPictures.Failed(memoryId));
         }
         catch (Exception ex) { ModLog.Error($"[MEMORY PICTURE] ImageFailed threw and was isolated: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>True while at least one mod listens for picture requests (the panel shows its button only then).</summary>
      internal static bool AnyPainter => ImageRequested != null;

      internal static void RaiseMemoryWritten(CrMemoryWritten written) => InvokeGuarded(MemoryWritten, written);

      internal static void RaiseImageRequested(CrMemoryImageRequest request) => InvokeGuarded(ImageRequested, request);

      private static void InvokeGuarded<T>(Action<T> evt, T arg)
      {
         if (evt == null || arg == null) return;

         foreach (Delegate handler in evt.GetInvocationList())
            try { ((Action<T>) handler)(arg); }
            catch (Exception ex) { ModLog.Error($"[MEMORY PICTURE] a subscriber threw and was isolated: {ex.GetType().Name}: {ex.Message}"); }
      }
   }

   /// <summary>
   ///   What the player chose in CR's settings, read-only, for a mod that should follow the same line. Since 2.8.18
   ///   (CrApi.Version 6).
   /// </summary>
   public static class CrContent
   {
      /// <summary>
      ///   The MCM "Adult content" level: "Off", "Mature", "Explicit" or "Hardcore" ("Mature" when MCM has not loaded, as CR
      ///   itself reads it). A string, so a mod reading it by reflection needs no reference to CR's SDK. Asked for by
      ///   Bhelogan (20/11/2026), who read CR's settings class by reflection.
      /// </summary>
      public static string AdultContentLevel => ModSettings.AdultContentLevel.ToString();
   }
}
