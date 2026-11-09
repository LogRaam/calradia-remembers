// Code written by Gabriel Mailhot, 09/11/2026.
// The one way a CR store's JSON goes into a save and comes back (tashmetu, Nexus, 09/11/2026: "long campaigns with CR stop
// loading"; a byte[] past 16,777,215 bytes is saved without error and never loads, see SaveBlobCodec).
//
// Written GZip-compressed under a NEW key (each store's own "_gz" constant), so a CR older than 2.8.6 that opens this save finds no key
// of its own and starts that store empty, instead of reading compressed bytes as text (save-migration rule: additive keys).
// Read from the new key first, then from the old byte[] key an older save holds, plain or not. A blob that would still
// come near the ceiling is not written: that one store comes back empty, the campaign still loads, and the player is told.

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
      private static readonly ConcurrentDictionary<string, SavedSize> Sizes = new ConcurrentDictionary<string, SavedSize>();

      /// <summary>What each store weighed at the last save of this session, largest stored first (cr.save_sizes).</summary>
      internal static IReadOnlyList<KeyValuePair<string, SavedSize>> LastSaved
         => Sizes.OrderByDescending(kv => kv.Value.Stored).ToList();

      /// <summary>
      ///   Writes <paramref name="json" /> compressed under <paramref name="key" /> (a store's "_gz" key). Never throws. A blob too near the
      ///   engine's ceiling is not written: logged, told to the player, and that store loads empty next time.
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
               ModLog.Error($"[SAVE] '{key}' is {stored.Length:N0} bytes compressed ({raw:N0} raw), too near the {SaveBlobCodec.EngineCeiling:N0} "
                            + "past which the game cannot load a save. It is NOT saved this time, so the campaign still loads; this store starts empty.");
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
               ModLog.Notice($"[SAVE] '{key}' is {stored.Length:N0} bytes compressed ({raw:N0} raw), growing toward the {SaveBlobCodec.EngineCeiling:N0} ceiling.");
               break;
         }

         Sizes[key] = new SavedSize {Raw = raw, Stored = stored.Length};
         try { dataStore.SyncData(key, ref stored); }
         catch (Exception ex) { ModLog.Error($"[SAVE] '{key}' save failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   The JSON a save holds for a store: under its compressed <paramref name="key" /> first, then under the
      ///   <paramref name="olderKey" /> an older save wrote it to as plain byte[]. Null when neither holds anything, for the
      ///   caller's own legacy key or fallback. Never throws.
      /// </summary>
      internal static string Read(IDataStore dataStore, string key, string olderKey)
      {
         string json = Decode(key, Fetch(dataStore, key));

         return json ?? Decode(olderKey, Fetch(dataStore, olderKey));
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

      /// <summary>A store's weight at its last save.</summary>
      internal struct SavedSize
      {
         /// <summary>The JSON's UTF-8 bytes.</summary>
         public int Raw;

         /// <summary>The bytes the save holds (compressed; 0 when refused).</summary>
         public int Stored;
      }
   }
}
