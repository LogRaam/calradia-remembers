// Code written by Gabriel Mailhot, 16/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Storage;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Storage
{
   /// <summary>
   ///   Implements <see cref="INpcMemoryStore" /> using TaleWorlds' native
   ///   <see cref="IDataStore" /> for persistence inside the campaign save file.
   /// </summary>
   public sealed class BannerlordNpcMemoryStore : INpcMemoryStore
   {
      // Legacy key: the profile JSON stored as a STRING. Bannerlord's native string
      // serialization corrupts the save once this blob grows large (saves with many
      // remembered NPCs became unreadable). Kept only to migrate old saves on load.
      private const string SaveKey = "CalradiaRemembers_NpcProfiles_v1";

      // Current key: the same JSON stored as a UTF-8 byte[], which has no such size limit.
      private const string SaveKeyBytes = "CalradiaRemembers_NpcProfiles_v2bytes";

      private readonly Dictionary<string, NpcProfile> _profiles = new();

      // Courier audit 3.2 (thread race): the LLM letter-generation continuations resume on the thread pool and
      // call Set here, while the main thread enumerates via ListIds/Get and snapshots for save. A plain Dictionary
      // under concurrent access throws "Collection was modified" (swallowed by SafeRun, aborting the daily pass)
      // or corrupts. Every access to _profiles goes through this gate.
      private readonly object _gate = new();

      public Task CommitAsync(CancellationToken ct = default)
         => Task.CompletedTask;

      public NpcProfile Get(string npcId)
      {
         lock (_gate) return _profiles.TryGetValue(npcId, out NpcProfile profile) ? profile : null;
      }

      // ── INpcMemoryStore ───────────────────────────────────────────────────

      public Task InitializeAsync(CancellationToken ct = default)
         => Task.CompletedTask;

      public IReadOnlyList<string> ListIds()
      {
         lock (_gate) return new List<string>(_profiles.Keys);
      }

      public void Remove(string npcId)
      {
         lock (_gate) _profiles.Remove(npcId);
      }

      public void Set(NpcProfile profile)
      {
         if (profile?.Id == null) return;
         lock (_gate) _profiles[profile.Id] = profile;
      }

      // ── TaleWorlds save/load bridge ───────────────────────────────────────

      public void SyncData(IDataStore dataStore)
      {
         // A roleplay-memory mod must NEVER prevent a campaign from saving or loading.
         // We store the profile JSON as a byte[] (no native string-size limit) and isolate
         // our own (de)serialization so a bad blob degrades gracefully instead of bricking
         // the save (the Nexus guarantee).
         if (dataStore.IsSaving)
         {
            string json;
            try { json = SerializeProfiles(); }
            catch (Exception ex)
            {
               ModLog.Error($"[STORE] profile serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               json = "{}";
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            try { dataStore.SyncData(SaveKeyBytes, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[STORE] byte[] save failed: {ex.GetType().Name}: {ex.Message}");
            }

            // Blob size is logged so growth can be tracked: the legacy string path corrupted
            // saves past ~65535 bytes; the byte[] path has no such ceiling.
            ModLog.Info($"Saved {_profiles.Count} NPC profile(s) ({bytes.Length:N0} bytes in the memory blob).");
         }
         else
         {
            string json = LoadJson(dataStore);
            if (!string.IsNullOrEmpty(json))
               try { DeserializeProfiles(json); }
               catch (Exception ex)
               {
                  lock (_gate) _profiles.Clear();
                  ModLog.Error($"[STORE] profile load failed; CR memory dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
               }

            ModLog.Info($"Loaded {_profiles.Count} NPC profile(s) from save.");
         }
      }

      #region private

      /// <summary>
      ///   Reads the profile JSON from the save, preferring the current byte[] blob and
      ///   falling back to the legacy string key so saves written by older versions still
      ///   load (they migrate to byte[] on the next save). Each read is isolated so a key
      ///   absent from this particular save never throws.
      /// </summary>
      private static string LoadJson(IDataStore dataStore)
      {
         byte[] bytes = null;
         try { dataStore.SyncData(SaveKeyBytes, ref bytes); }
         catch
         {
            /* key absent in older saves — fall through to legacy */
         }

         if (bytes is {Length: > 0})
            return Encoding.UTF8.GetString(bytes);

         string legacy = null;
         try { dataStore.SyncData(SaveKey, ref legacy); }
         catch
         {
            /* no legacy data either — a fresh campaign */
         }

         return legacy;
      }

      // The resilient bundle (round-trip guard on write, per-entry isolation on read) lives
      // in the SDK so any single-blob store benefits; this class only adds the byte[]/IDataStore
      // bridge and routes the SDK's diagnostics to ModLog.

      private void DeserializeProfiles(string json)
      {
         var dropped = 0;
         Dictionary<string, NpcProfile> loaded = ResilientProfileBundle.Deserialize(json, (id, ex) =>
         {
            dropped++;
            ModLog.Error($"[STORE] profile '{id}' failed to load and was skipped (others kept). " +
                         $"{ex.GetType().Name}: {ex.Message}");
         });

         lock (_gate)
         {
            _profiles.Clear();
            foreach (KeyValuePair<string, NpcProfile> kvp in loaded)
               _profiles[kvp.Key] = kvp.Value;
         }

         if (dropped > 0)
            ModLog.Info($"[STORE] {dropped} profile(s) skipped on load; {_profiles.Count} loaded.");
      }

      // ── Serialization ─────────────────────────────────────────────────────

      /// <summary>
      ///   Serializes the profiles via the SDK's resilient bundle: every profile is proven to
      ///   survive a serialize→deserialize round-trip first, and any that cannot is dropped
      ///   (with a loud log naming it) so a single un-round-trippable profile can never make the
      ///   whole save unreadable — the mod's "your campaign always loads" guarantee.
      /// </summary>
      private string SerializeProfiles()
      {
         // Freeze the dictionary structure before serializing: an autosave can run while an async LLM continuation
         // is landing a Set on the thread pool, and a dictionary copied mid-mutation would throw and drop the
         // WHOLE profile blob this save. 3.2: take the same gate every mutation uses, so the copy is atomic. (Per-
         // profile failures are still isolated by the resilient bundle; this guards only the outer enumeration.)
         Dictionary<string, NpcProfile> snapshot;
         lock (_gate) snapshot = new Dictionary<string, NpcProfile>(_profiles);

         return ResilientProfileBundle.Serialize(snapshot, (id, name, ex) =>
            ModLog.Error($"[STORE] profile '{id}' ({name}) cannot be persisted — " +
                         $"it does not survive a round-trip, so it is left out to keep the save loadable. " +
                         $"{ex.GetType().Name}: {ex.Message}"));
      }

      #endregion
   }
}