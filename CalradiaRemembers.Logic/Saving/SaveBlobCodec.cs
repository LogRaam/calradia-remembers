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
//
// Gabriel (09/11/2026), on how to weigh less in a save several mods share: "Ce que CR peut faire c'est de reduire le
// poids de ses donnees." Since 2.8.8 the compressed bytes are saved as Base64 text in pieces (ToPieces), not as a byte[].
// A string is written whole in the save's string table, about one byte per character, where a byte[] costs about 19
// bytes per byte; but that table's entry length is read on a short (ArchiveDeserializer.LoadFrom, also for the strings,
// LoadContext.Load), so one string can never pass EngineStringLimit, hence the pieces. Each piece is one entry of a
// List<string>, so the 3-byte ceiling would take some 500 GB to reach.
//
// Gabriel (10/11/2026), after the codec bench on a campaign inflated to 41.5 MB of store JSON (cr.inflate_history): "je
// crois que ppmd est le bon choix", then "go". Since 2.8.8 a store is PPMd var.H (Ppmd7), order PpmdOrder with PpmdMemoryMb
// of model, behind a 12-byte header that names both, so a later version can change them without breaking a save. The
// bench: relations at 53% of the GZip CR shipped, memories far below; reading 41 MB back in about a second. GZip (1F 8B)
// and plain JSON are still read, for every save written before.

#region

using System;
using System.Collections.Generic;
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

      /// <summary>
      ///   The longest UTF-8 text one saved string may hold: the entry's length is read on a short, and 4 of those bytes hold
      ///   the text's own length (SaveContext.SaveStringTo, ArchiveDeserializer.LoadFrom, 1.4.8).
      /// </summary>
      public const int EngineStringLimit = short.MaxValue - 4;

      /// <summary>Base64 characters per saved piece: under <see cref="EngineStringLimit" /> with room, and a multiple of 4.</summary>
      public const int PieceLength = 30_000;

      /// <summary>
      ///   TUNE: compressed bytes at which a store is not written. Saved in pieces it no longer nears <see cref="EngineCeiling" />
      ///   (this is under 9,000 pieces); the bound is the save's string table, one buffer shared with every mod.
      /// </summary>
      public const int RefuseAt = 200_000_000;

      /// <summary>TUNE: a blob this big or bigger is written, and the log notes a store grown large enough to be worth reporting.</summary>
      public const int WarnAt = 8_000_000;

      /// <summary>
      ///   TUNE: the PPMd model order a store is written with. On the bench, order 6 to 8 gave the relation ledger its best and
      ///   order 8 halved the memories again against order 6; higher orders gained mostly on invented text. Saved in the header.
      /// </summary>
      public const int PpmdOrder = 8;

      /// <summary>TUNE: the PPMd model memory, in MB, a store is written with: 16 compressed as well as 64 on the bench. Saved in the header.</summary>
      public const int PpmdMemoryMb = 16;

      /// <summary>The bytes a PPMd blob opens with: "CRP7", then the format version.</summary>
      private static readonly byte[] PpmdMagic = {(byte) 'C', (byte) 'R', (byte) 'P', (byte) '7'};

      private const byte PpmdVersion = 1;

      /// <summary>
      ///   Header: magic (4), version (1), order (1), memory in MB (2), JSON length in bytes (4), all little-endian; then the
      ///   PPMd stream.
      /// </summary>
      public const int PpmdHeaderLength = 12;

      /// <summary>The JSON as PPMd-compressed UTF-8 behind its header; an empty array for null or empty JSON.</summary>
      public static byte[] Encode(string json)
      {
         if (string.IsNullOrEmpty(json)) return new byte[0];

         byte[] raw = Encoding.UTF8.GetBytes(json);
         byte[] packed = Ppmd7.Encode(raw, PpmdOrder, PpmdMemoryMb << 20);
         var stored = new byte[PpmdHeaderLength + packed.Length];
         Buffer.BlockCopy(PpmdMagic, 0, stored, 0, 4);
         stored[4] = PpmdVersion;
         stored[5] = PpmdOrder;
         stored[6] = (byte) PpmdMemoryMb;
         stored[7] = (byte) (PpmdMemoryMb >> 8);
         for (var i = 0; i < 4; i++) stored[8 + i] = (byte) (raw.Length >> (8 * i));
         Buffer.BlockCopy(packed, 0, stored, PpmdHeaderLength, packed.Length);

         return stored;
      }

      /// <summary>
      ///   The JSON as GZip-compressed UTF-8, as CR 2.8.6 to 2.8.7 wrote it (and the first 2.8.8 builds); an empty array for
      ///   null or empty JSON. Kept so the self-tests can stand for an older save; CR no longer writes it.
      /// </summary>
      public static byte[] EncodeGzip(string json)
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
      ///   The JSON a saved blob holds: PPMd as CR writes it since 2.8.8, GZip as 2.8.6 to 2.8.7 wrote it, plain UTF-8 as older
      ///   saves did; null for no bytes. Throws on a damaged blob, like any unreadable store, for the caller's own fallback.
      /// </summary>
      public static string Decode(byte[] stored)
      {
         if (stored == null || stored.Length == 0) return null;
         if (IsPpmd(stored)) return DecodePpmd(stored);
         if (!IsGzip(stored)) return Encoding.UTF8.GetString(stored);

         using (var input = new MemoryStream(stored))
         using (var gzip = new GZipStream(input, CompressionMode.Decompress))
         using (var output = new MemoryStream())
         {
            gzip.CopyTo(output);

            return Encoding.UTF8.GetString(output.ToArray());
         }
      }

      /// <summary>
      ///   The saved bytes as Base64 text cut into pieces of <see cref="PieceLength" /> characters, each one savable as a string;
      ///   none for no bytes.
      /// </summary>
      public static List<string> ToPieces(byte[] stored)
      {
         var pieces = new List<string>();
         if (stored == null || stored.Length == 0) return pieces;

         string text = Convert.ToBase64String(stored);
         for (int at = 0; at < text.Length; at += PieceLength)
            pieces.Add(text.Substring(at, Math.Min(PieceLength, text.Length - at)));

         return pieces;
      }

      /// <summary>
      ///   The bytes the pieces were cut from; an empty array for none. Throws on a damaged piece, like any unreadable store,
      ///   for the caller's own fallback.
      /// </summary>
      public static byte[] FromPieces(IList<string> pieces)
      {
         if (pieces == null || pieces.Count == 0) return new byte[0];

         return Convert.FromBase64String(string.Concat(pieces));
      }

      private static string DecodePpmd(byte[] stored)
      {
         if (stored[4] != PpmdVersion) throw new InvalidDataException($"PPMd store of format {stored[4]}, newer than this CR reads ({PpmdVersion}).");

         int order = stored[5];
         int memoryMb = stored[6] | (stored[7] << 8);
         int length = stored[8] | (stored[9] << 8) | (stored[10] << 16) | (stored[11] << 24);
         if (length < 0) throw new InvalidDataException("PPMd store with a length past what CR writes.");

         var packed = new byte[stored.Length - PpmdHeaderLength];
         Buffer.BlockCopy(stored, PpmdHeaderLength, packed, 0, packed.Length);

         return Encoding.UTF8.GetString(Ppmd7.Decode(packed, length, order, memoryMb << 20));
      }

      /// <summary>Whether a saved blob is compressed by CR, PPMd or GZip; plain JSON never opens like either.</summary>
      public static bool IsCompressed(byte[] stored) => IsPpmd(stored) || IsGzip(stored);

      /// <summary>Whether a saved blob is PPMd as CR writes it since 2.8.8 (opens with "CRP7" and holds a whole header).</summary>
      public static bool IsPpmd(byte[] stored)
         => stored != null && stored.Length >= PpmdHeaderLength && stored[0] == PpmdMagic[0] && stored[1] == PpmdMagic[1] && stored[2] == PpmdMagic[2] && stored[3] == PpmdMagic[3];

      /// <summary>Whether a saved blob is GZip (opens with 1F 8B), as CR 2.8.6 to 2.8.7 wrote it; plain JSON never opens so.</summary>
      public static bool IsGzip(byte[] stored) => stored != null && stored.Length >= 2 && stored[0] == 0x1F && stored[1] == 0x8B;

      /// <summary>
      ///   Whether a saved container of this many elements is one the game cannot load: its ids wrapped, so its entries are
      ///   mixed and cannot be put back. The load rescue (SaveRescuePatch) loads such a container EMPTY so the rest of the
      ///   save opens (Gabriel, 09/11/2026: "est-ce qu'on peut implementer notre propre repair kit dans CR").
      /// </summary>
      public static bool IsBeyondCeiling(int elementCount) => elementCount > EngineCeiling;

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
