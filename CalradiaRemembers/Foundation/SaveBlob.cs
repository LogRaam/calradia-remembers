// Code written by Gabriel Mailhot, 09/11/2026.
// The one way a CR store's JSON goes into a save and comes back (tashmetu, Nexus, 09/11/2026: "long campaigns with CR stop
// loading"; a byte[] past 16,777,215 bytes is saved without error and never loads, see SaveBlobCodec).
//
// Written GZip-compressed under a NEW key (each store's own "_gz" constant), so a CR older than 2.8.6 that opens this save finds no key
// of its own and starts that store empty, instead of reading compressed bytes as text (save-migration rule: additive keys).
// Read from the new key first, then from the old byte[] key an older save holds, plain or not. A blob that would still
// come near the ceiling is not written: that one store comes back empty, the campaign still loads, and the player is told.
//
// Since 2.8.8 (Gabriel, 09/11/2026: "il faut penser a continuer a supporter les anciennes sauvegardes aussi") the
// compressed bytes are saved as Base64 pieces (SaveBlobCodec.ToPieces) under the store's key + PiecesSuffix, about 1.34
// bytes of save per byte instead of 19, and no longer near the 3-byte ceiling. Read in order: the pieces, then the "_gz"
// byte[] a 2.8.6 or 2.8.7 save holds, then the older plain byte[]; the next save writes pieces. A CR older than 2.8.8 that
// opens a 2.8.8 save finds none of its keys and starts the store empty, as 2.8.6 did with 2.8.5's.

#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Saving;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Foundation
{
   /// <summary>Saves and loads a CR store's JSON, compressed, kept under the size past which a save never loads.</summary>
   internal static class SaveBlob
   {
      /// <summary>Appended to a store's key for the pieces its compressed bytes are saved in (2.8.8).</summary>
      internal const string PiecesSuffix = "_pieces";

      private static readonly ConcurrentDictionary<string, SavedSize> Sizes = new ConcurrentDictionary<string, SavedSize>();

      private static readonly ConcurrentDictionary<string, string> Loaded = new ConcurrentDictionary<string, string>();

      /// <summary>Where each store was read from at the last load of this session (cr.save_sizes): pieces, or an older form.</summary>
      internal static IReadOnlyList<KeyValuePair<string, string>> LastLoaded => Loaded.OrderBy(kv => kv.Key).ToList();

      /// <summary>What each store weighed at the last save of this session, largest stored first (cr.save_sizes).</summary>
      internal static IReadOnlyList<KeyValuePair<string, SavedSize>> LastSaved
         => Sizes.OrderByDescending(kv => kv.Value.Stored).ToList();

      /// <summary>
      ///   Writes <paramref name="json" /> compressed, in pieces, under <paramref name="key" /> + <see cref="PiecesSuffix" /> (key is a
      ///   store's "_gz" key). Never throws. A runaway blob is not written: logged, told to the player, and that store loads empty next time.
      /// </summary>
      internal static void Write(IDataStore dataStore, string key, string json)
      {
         byte[] stored;
         int raw = json == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(json);
         try { stored = SaveBlobCodec.Encode(json); }
         catch (Exception ex)
         {
            ModLog.Error($"[SAVE] '{key}' could not be compressed, saving it empty: {ex.GetType().Name}: {ex.Message}");
            stored = new byte[0];
         }

         switch (SaveBlobCodec.Judge(stored.Length))
         {
            case SaveBlobVerdict.Refuse:
               ModLog.Error($"[SAVE] '{key}' is {stored.Length:N0} bytes compressed ({raw:N0} raw), past the {SaveBlobCodec.RefuseAt:N0} CR writes at most. "
                            + "It is NOT saved this time, so the campaign still loads; this store starts empty.");
               try
               {
                  InformationManager.DisplayMessage(new InformationMessage(
                     $"Calradia Remembers: the store '{key}' grew too large to save safely and was left out of this save. Please report it with your log.",
                     Colors.Red));
               }
               catch
               {
                  /* no UI at this moment: the log holds it */
               }

               stored = new byte[0];
               break;
            case SaveBlobVerdict.Warn:
               ModLog.Notice($"[SAVE] '{key}' is {stored.Length:N0} bytes compressed ({raw:N0} raw): a store growing this large is worth reporting.");
               break;
         }

         List<string> pieces = SaveBlobCodec.ToPieces(stored);
         Sizes[key] = new SavedSize {Raw = raw, Stored = stored.Length, Pieces = pieces.Count};
         try { dataStore.SyncData(key + PiecesSuffix, ref pieces); }
         catch (Exception ex) { ModLog.Error($"[SAVE] '{key}' save failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   The JSON a save holds for a store: in pieces under <paramref name="key" /> + <see cref="PiecesSuffix" /> first (2.8.8),
      ///   then as the compressed byte[] under <paramref name="key" /> (2.8.6, 2.8.7), then under the <paramref name="olderKey" />
      ///   an older save wrote it to as plain byte[]. Null when none holds anything, for the caller's own legacy key or
      ///   fallback. Never throws.
      /// </summary>
      internal static string Read(IDataStore dataStore, string key, string olderKey)
      {
         string json = Decode(key + PiecesSuffix, Join(key, FetchPieces(dataStore, key + PiecesSuffix)));
         if (json != null)
         {
            Loaded[key] = "pieces";

            return json;
         }

         json = Decode(key, Fetch(dataStore, key));
         if (json != null)
         {
            Loaded[key] = "compressed byte[] of 2.8.6-2.8.7, pieces at the next save";

            return json;
         }

         json = Decode(olderKey, Fetch(dataStore, olderKey));
         Loaded[key] = json != null ? "plain byte[] of an older save, pieces at the next save" : "nothing (empty store)";

         return json;
      }

      private static List<string> FetchPieces(IDataStore dataStore, string key)
      {
         List<string> pieces = null;
         try { dataStore.SyncData(key, ref pieces); }
         catch
         {
            /* key absent in this save */
         }

         return pieces;
      }

      private static byte[] Join(string key, List<string> pieces)
      {
         try { return SaveBlobCodec.FromPieces(pieces); }
         catch (Exception ex)
         {
            ModLog.Error($"[SAVE] '{key}' pieces could not be joined, trying an older form: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static byte[] Fetch(IDataStore dataStore, string key)
      {
         byte[] bytes = null;
         try { dataStore.SyncData(key, ref bytes); }
         catch
         {
            /* key absent in this save */
         }

         return bytes;
      }

      private static string Decode(string key, byte[] bytes)
      {
         if (bytes == null || bytes.Length == 0) return null;

         try { return SaveBlobCodec.Decode(bytes); }
         catch (Exception ex)
         {
            ModLog.Error($"[SAVE] '{key}' could not be read, that store starts empty: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Drops what a self-test wrote under <paramref name="key" /> from the session's sizes and loads.</summary>
      internal static void Forget(string key)
      {
         Sizes.TryRemove(key, out _);
         Loaded.TryRemove(key, out _);
      }

      /// <summary>A store's weight at its last save.</summary>
      internal struct SavedSize
      {
         /// <summary>The JSON's UTF-8 bytes.</summary>
         public int Raw;

         /// <summary>The compressed bytes (0 when refused).</summary>
         public int Stored;

         /// <summary>The Base64 pieces they were saved in.</summary>
         public int Pieces;
      }
   }
}
