// Code written by Gabriel Mailhot, 17/11/2026.
// The story archive's file (Gabriel, 17/11/2026: the Story Log replaced by a long-term record of every episode, written
// "toujours, car le joueur peut activer, desactiver, reactiver"; the switch will govern only what reaches a prompt). One file
// per campaign, OUTSIDE the save (a save never grows with the story): Documents\Mount and Blade II Bannerlord\
// CalradiaRemembers\StoryArchive\<archive id>.jsonl, one record per line, append only. The save holds only a pointer: which
// file, and the last record it knew (the head). Reads walk back from that head (StoryArchiveBranch), so a reloaded or
// out-of-order save reads exactly its own past and an abandoned branch stays in the file, unreachable.
//
// Owned by NpcMemoryCampaignBehavior, so a new campaign starts with a new store. An older save has no pointer: a new
// archive begins at its first record. Writes are locked: a letter or a background path may record off the main thread.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic.StoryArchive;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Foundation
{
   /// <summary>Appends the player's story to its campaign's archive file and reads back the branch the loaded save belongs to.</summary>
   public sealed class StoryArchiveStore
   {
      /// <summary>The save key of the pointer (archive id and head).</summary>
      public const string SaveKey = "CR_StoryArchive_v1";

      private readonly object _gate = new object();
      private string _archiveId;
      private string _headId;
      private bool _complained;
      private List<StoryArchiveRecord> _branch; // the loaded branch, read once, then extended on every append

      /// <summary>While true nothing is written: a self-test's lines are not the player's story.</summary>
      public bool Paused { get; set; }

      /// <summary>The folder every campaign's archive file lives in.</summary>
      public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                  "Mount and Blade II Bannerlord", "CalradiaRemembers", "StoryArchive");

      /// <summary>This campaign's archive id; null until the first record.</summary>
      public string ArchiveId
      {
         get { lock (_gate) return _archiveId; }
      }

      /// <summary>The id of the last record on the loaded branch; null before the first.</summary>
      public string HeadId
      {
         get { lock (_gate) return _headId; }
      }

      /// <summary>This campaign's archive file; null until the first record.</summary>
      public string FilePath
      {
         get
         {
            string id = ArchiveId;

            return id == null ? null : Path.Combine(Folder, id + ".jsonl");
         }
      }

      /// <summary>
      ///   Places <paramref name="draft" /> after the head, appends it to the file, and returns the record as written (null
      ///   when it could not be written). Never throws. The Story Log, when on, gets its novel paragraph.
      /// </summary>
      public StoryArchiveRecord Append(StoryArchiveRecord draft)
      {
         if (draft == null || Paused) return null;

         StoryArchiveRecord written;
         lock (_gate)
         {
            _archiveId ??= Guid.NewGuid().ToString("N");
            written = draft.Chained(Guid.NewGuid().ToString("N").Substring(0, 16), _headId);
            try
            {
               string path = Path.Combine(Folder, _archiveId + ".jsonl");
               Directory.CreateDirectory(Folder);
               File.AppendAllText(path, written.Serialize() + "\n", Encoding.UTF8);
               _headId = written.Id;
               _branch?.Add(written);
            }
            catch (Exception ex)
            {
               if (!_complained)
               {
                  _complained = true;
                  ModLog.Error($"[STORY ARCHIVE] writing failed, the story is not being kept (said once): {ex.GetType().Name}: {ex.Message}");
               }

               return null;
            }
         }

         StoryLog.WriteRendered(StoryArchiveNovel.Render(written));

         return written;
      }

      /// <summary>
      ///   The loaded save's branch, oldest first: read from the file once, then kept and extended as records are written
      ///   (the long memory asks every turn). Empty when there is none yet.
      /// </summary>
      public IReadOnlyList<StoryArchiveRecord> ReadBranch()
      {
         lock (_gate)
            if (_branch != null) return _branch.ToList();

         IReadOnlyList<StoryArchiveRecord> read = ReadBranchFromFile();
         lock (_gate)
         {
            // A record appended while the file was read is already in it, or arrives with the next append.
            if (_branch == null && read.Count > 0 && read[read.Count - 1].Id == _headId) _branch = read.ToList();
         }

         return read;
      }

      private IReadOnlyList<StoryArchiveRecord> ReadBranchFromFile()
      {
         string path, head;
         lock (_gate)
         {
            path = _archiveId == null ? null : Path.Combine(Folder, _archiveId + ".jsonl");
            head = _headId;
         }

         if (path == null || head == null || !File.Exists(path)) return new List<StoryArchiveRecord>();

         try
         {
            IEnumerable<StoryArchiveRecord> all = File.ReadAllLines(path, Encoding.UTF8).Select(StoryArchiveRecord.Deserialize);

            return StoryArchiveBranch.Walk(StoryArchiveBranch.Index(all), head);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[STORY ARCHIVE] reading failed: {ex.GetType().Name}: {ex.Message}");

            return new List<StoryArchiveRecord>();
         }
      }

      /// <summary>Saves or loads the pointer. A save written before the archive reads none: a new archive begins.</summary>
      public void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            lock (_gate)
               bytes = _archiveId == null ? new byte[0] : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Pointer {ArchiveId = _archiveId, HeadId = _headId}));
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[STORY ARCHIVE] pointer save failed: {ex.GetType().Name}: {ex.Message}"); }

            return;
         }

         byte[] read = null;
         try { dataStore.SyncData(SaveKey, ref read); }
         catch
         {
            /* a save written before the archive: no pointer */
         }

         Pointer p = null;
         try { p = read is {Length: > 0} ? JsonConvert.DeserializeObject<Pointer>(Encoding.UTF8.GetString(read)) : null; }
         catch
         {
            /* unreadable: start a new archive rather than attach to a wrong one */
         }

         lock (_gate)
         {
            _archiveId = string.IsNullOrEmpty(p?.ArchiveId) ? null : p.ArchiveId;
            _headId = _archiveId == null ? null : p.HeadId;
            _branch = null;
         }

         if (_archiveId != null) ModLog.Info($"[STORY ARCHIVE] loaded pointer: archive {_archiveId}, head {_headId ?? "-"}.");
      }

      /// <summary>cr.test seam: attaches to an archive and head exactly as loading a save does.</summary>
      internal void AttachForDiagnostics(string archiveId, string headId)
      {
         lock (_gate)
         {
            _archiveId = archiveId;
            _headId = headId;
            _branch = null;
         }
      }

      private sealed class Pointer
      {
         public string ArchiveId { get; set; }
         public string HeadId { get; set; }
      }
   }
}
