// Code written by Gabriel Mailhot, 09/11/2026.
// How CR's state is written into a save, and the ceiling it must never reach.
//
// tashmetu (Nexus, 09/11/2026): "long campaigns with CR stop loading". A save over 1 GB failed with "An item with the
// same key has already been added" in TaleWorlds.SaveSystem.ArchiveDeserializer.LoadFrom; cr_relation_baselines held
// 18.2 million bytes and CalradiaRemembers_NpcProfiles_v2bytes 15.9 million. Read in the decompiled 1.4.8 save code:
// a byte[] is saved as a container with one entry per byte (ContainerSaveData, EntryId(i, Value)), and an entry's id is
// read back on 3 bytes (TaleWorlds.Library.BinaryReader.Read3ByteInt, 0xFFFFFF read as -1) into a dictionary
// (SaveEntryFolder.AddEntry). Past 16,777,215 bytes two entries share an id and the save, written without any error,
// never loads again. Each byte also costs about 19 bytes of save.
//
// So CR's JSON is GZip-compressed before it reaches the save (tashmetu's first idea; such JSON shrinks ten times or
// more), and a blob that would still come near the ceiling is not written at all: one store lost is far better than a
// campaign that never loads. Plain JSON from an older save is still read: GZip opens with 1F 8B, JSON never does.

#region

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Saving
{
   /// <summary>Turns a store's JSON into the bytes a save holds, and back, and says whether those bytes are safe to save.</summary>
   public static class SaveBlobCodec
   {
      /// <summary>
      ///   The most bytes one byte[] may hold and still load: entry ids are read on 3 bytes, 0xFFFFFF stands for -1, so ids
      ///   0 to 16,777,214 are distinct (TaleWorlds.Library.BinaryReader.Read3ByteInt, 1.4.8).
      /// </summary>
      public const int EngineCeiling = 16_777_215;

      /// <summary>TUNE: a blob this big or bigger is not written (a margin under <see cref="EngineCeiling" />).</summary>
      public const int RefuseAt = 15_000_000;

      /// <summary>TUNE: a blob this big or bigger is written, and the log warns that the store is growing toward the ceiling.</summary>
      public const int WarnAt = 8_000_000;

      /// <summary>The JSON as GZip-compressed UTF-8; an empty array for null or empty JSON.</summary>
      public static byte[] Encode(string json)
      {
         if (string.IsNullOrEmpty(json)) return new byte[0];

         byte[] raw = Encoding.UTF8.GetBytes(json);
         using (var output = new MemoryStream())
         {
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
               gzip.Write(raw, 0, raw.Length);

            return output.ToArray();
         }
      }

      /// <summary>
      ///   The JSON a saved blob holds: unzipped when it is GZip, read as plain UTF-8 when an older save wrote it so; null for
      ///   no bytes. Throws on a GZip blob that is damaged, like any unreadable store, for the caller's own fallback.
      /// </summary>
      public static string Decode(byte[] stored)
      {
         if (stored == null || stored.Length == 0) return null;
         if (!IsCompressed(stored)) return Encoding.UTF8.GetString(stored);

         using (var input = new MemoryStream(stored))
         using (var gzip = new GZipStream(input, CompressionMode.Decompress))
         using (var output = new MemoryStream())
         {
            gzip.CopyTo(output);

            return Encoding.UTF8.GetString(output.ToArray());
         }
      }

      /// <summary>Whether a saved blob is GZip (opens with 1F 8B), as this codec writes it; plain JSON never opens so.</summary>
      public static bool IsCompressed(byte[] stored) => stored != null && stored.Length >= 2 && stored[0] == 0x1F && stored[1] == 0x8B;

      /// <summary>What to do with a blob of this many bytes at save time.</summary>
      public static SaveBlobVerdict Judge(int storedBytes)
         => storedBytes >= RefuseAt ? SaveBlobVerdict.Refuse : storedBytes >= WarnAt ? SaveBlobVerdict.Warn : SaveBlobVerdict.Fine;
   }

   /// <summary>Whether a blob may go into the save.</summary>
   public enum SaveBlobVerdict
   {
      /// <summary>Well under the ceiling.</summary>
      Fine,

      /// <summary>Written, but growing toward the ceiling: the log says so.</summary>
      Warn,

      /// <summary>Not written: it would come too near the size past which the save never loads.</summary>
      Refuse
   }
}
