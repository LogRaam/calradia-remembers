// Code written by Gabriel Mailhot, 20/11/2026.
// The engine half of memory pictures (Bhelogan, Discord, 20/11/2026; Gabriel: "oui on y va. et on protège les IDS associés
// à des images"). Another mod paints a picture of a memory; CR keeps its own copy, one folder per campaign outside the save,
// and the memory names the file (NotableEvent.ImageFile, which memory compression never drops). This class also runs the
// MemoryWriteWatch that announces each memory written (CrNpcMemory.MemoryWritten) and stamps the conversation it was born
// in, and remembers which paintings are on their way for the panel's busy button. The rules are in
// CalradiaRemembers.Logic.Extension (MemoryWriteWatch, MemoryPicturePolicy).

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Extension;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Memory pictures and the memory-written watch. The game thread, except where a member says otherwise.</summary>
   internal static class MemoryPictures
   {
      private static readonly MemoryWriteWatch Watch = new();
      private static readonly MemoryPaintingsUnderway Underway = new();
      private static Campaign _campaign;

      /// <summary>The folder every campaign's memory pictures live in, one sub-folder per campaign.</summary>
      internal static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                  "Mount and Blade II Bannerlord", "CalradiaRemembers", "MemoryPictures");

      /// <summary>This campaign's folder, or null when no campaign is loaded. Any thread.</summary>
      internal static string FolderOf(string saveId)
         => string.IsNullOrWhiteSpace(saveId) ? null : Path.Combine(Root, MemoryPicturePolicy.Safe(saveId));

      /// <summary>The memory's id, given now when it has none (see <see cref="MemoryWriteWatch.EnsureId" />).</summary>
      internal static string EnsureId(NotableEvent memory)
      {
         Sync();

         return Watch.EnsureId(memory);
      }

      /// <summary>The full path of the memory's picture in the loaded campaign, or null when it has none or the file is gone.</summary>
      internal static string PathOf(NotableEvent memory)
      {
         if (string.IsNullOrEmpty(memory?.ImageFile)) return null;

         string folder = FolderOf(Campaign.Current?.UniqueGameId);
         if (folder == null) return null;

         string path = Path.Combine(folder, Path.GetFileName(memory.ImageFile));

         return File.Exists(path) ? path : null;
      }

      /// <summary>A painting of this memory is on its way (the panel's busy button).</summary>
      internal static bool IsBusy(string memoryId) => Underway.IsBusy(memoryId, DateTime.UtcNow);

      /// <summary>Looks at every character's memories and announces what was written; <paramref name="sessionId" /> when a conversation closes.</summary>
      internal static void LookAtEveryone(string sessionId)
      {
         try
         {
            if (Campaign.Current == null) return;
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return;
            Sync();

            var profiles = new List<WatchedProfile>();
            foreach (string id in store.ListIds())
            {
               NpcProfile profile = store.Get(id);
               if (profile?.Events != null) profiles.Add(new WatchedProfile {NpcId = id, Memories = profile.Events.ToArray()});
            }

            Announce(Watch.Look(profiles, sessionId, true));
         }
         catch (Exception ex) { ModLog.Error($"[MEMORY PICTURE] the memory watch failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Looks at one character after a mod's AddMemory. Not while a conversation is open: its memories are stamped with
      ///   the conversation when it closes, and a look now would take them first.
      /// </summary>
      internal static void LookAt(string npcId)
      {
         try
         {
            if (Campaign.Current == null || Chat.ChatManager.IsOpen) return;
            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npcId);
            if (profile?.Events == null) return;
            Sync();
            if (!Watch.Seeded)
            {
               LookAtEveryone(null);

               return;
            }

            Announce(Watch.Look(new[] {new WatchedProfile {NpcId = npcId, Memories = profile.Events.ToArray()}}, null, false));
         }
         catch (Exception ex) { ModLog.Error($"[MEMORY PICTURE] the memory watch failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The hourly look, skipped while a conversation is open (its close looks with the conversation's id).</summary>
      internal static void OnHourlyTick()
      {
         if (!Chat.ChatManager.IsOpen) LookAtEveryone(null);
      }

      /// <summary>The player asked the panel to paint this memory: busy, then the request to every painter.</summary>
      internal static void Request(string npcId, NotableEvent memory)
      {
         if (memory == null || string.IsNullOrEmpty(npcId) || memory.IsPrivate) return;

         string id = EnsureId(memory);
         Underway.Begin(id, DateTime.UtcNow);
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(npcId);
         if (profile != null) store.Set(profile);
         ModLog.Info($"[MEMORY PICTURE] painting asked for {npcId}, memory {id}: {memory.summary}");
         CrNpcMemory.RaiseImageRequested(new CrMemoryImageRequest {
            NpcId = npcId, MemoryId = id, Day = memory.gameDay, Kind = memory.type.ToString(), Summary = memory.summary,
            SessionId = memory.SessionId, HasImage = !string.IsNullOrEmpty(memory.ImageFile), SaveId = Campaign.Current?.UniqueGameId
         });
      }

      /// <summary>The painting will not come: the button is offered again.</summary>
      internal static void Failed(string memoryId)
      {
         Underway.End(memoryId);
         Chat.ChatManager.ActiveViewModel?.OnMemoryPictureChanged(memoryId);
      }

      /// <summary>
      ///   Copies the picture into this campaign's folder under a new name and returns that name, or null (logged). Any thread:
      ///   the slow part of SetImage runs on the painter's own thread.
      /// </summary>
      internal static string CopyIn(string saveId, string memoryId, string sourcePath)
      {
         try
         {
            string folder = FolderOf(saveId);
            if (folder == null) return null;
            Directory.CreateDirectory(folder);

            string name = MemoryPicturePolicy.FileName(memoryId, Path.GetExtension(sourcePath));
            File.Copy(sourcePath, Path.Combine(folder, name), true);

            return name;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MEMORY PICTURE] the picture could not be copied: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Puts the copied picture (<paramref name="fileName" />, or null to take the picture away) on the memory and deletes
      ///   the copy it replaces. The memory is replaced by a copy carrying the picture, so a compression pass already under
      ///   way sees it changed and tries again later, protecting it. Returns false when the memory is not found.
      /// </summary>
      internal static bool Attach(string npcId, string memoryId, string fileName)
      {
         string folder = FolderOf(Campaign.Current?.UniqueGameId);
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = store?.Get(npcId);
         int index = profile?.Events?.FindIndex(e => e != null && e.Id == memoryId) ?? -1;
         Underway.End(memoryId);

         if (index < 0)
         {
            Delete(folder, fileName);
            ModLog.Info($"[MEMORY PICTURE] refused: {npcId} holds no memory {memoryId} (taken back, or another save).");
            Chat.ChatManager.ActiveViewModel?.OnMemoryPictureChanged(memoryId);

            return false;
         }

         NotableEvent memory = profile.Events[index];
         string old = memory.ImageFile;
         profile.Events[index] = memory with {ImageFile = fileName};
         store.Set(profile);
         if (!string.Equals(old, fileName, StringComparison.Ordinal)) Delete(folder, old);
         ModLog.Info(fileName == null
            ? $"[MEMORY PICTURE] picture taken away from {profile.Name}'s memory {memoryId}."
            : $"[MEMORY PICTURE] picture set on {profile.Name}'s memory {memoryId}: {fileName}");
         Chat.ChatManager.ActiveViewModel?.OnMemoryPictureChanged(memoryId);

         return true;
      }

      #region private

      private static void Announce(IReadOnlyList<WatchedMemory> written)
      {
         if (written == null || written.Count == 0) return;

         string saveId = Campaign.Current?.UniqueGameId;
         foreach (WatchedMemory w in written)
         {
            // Secrets stay out of every other mod's reach, as in GetMemories.
            if (w.Memory == null || w.Memory.IsPrivate) continue;

            CrNpcMemory.RaiseMemoryWritten(new CrMemoryWritten {
               NpcId = w.NpcId, MemoryId = w.Memory.Id, Day = w.Memory.gameDay, Kind = w.Memory.type.ToString(), Summary = w.Memory.summary,
               SessionId = w.Memory.SessionId, IsNew = w.IsNew, SaveId = saveId
            });
         }
      }

      // Another campaign loaded: the watch and the paintings under way belong to the one before.
      private static void Sync()
      {
         if (ReferenceEquals(_campaign, Campaign.Current)) return;

         _campaign = Campaign.Current;
         Watch.Reset();
         Underway.Clear();
      }

      private static void Delete(string folder, string fileName)
      {
         if (folder == null || string.IsNullOrEmpty(fileName)) return;

         try
         {
            string path = Path.Combine(folder, Path.GetFileName(fileName));
            if (File.Exists(path)) File.Delete(path);
         }
         catch (Exception ex) { ModLog.Info($"[MEMORY PICTURE] an old picture could not be deleted: {ex.Message}"); }
      }

      #endregion
   }
}
