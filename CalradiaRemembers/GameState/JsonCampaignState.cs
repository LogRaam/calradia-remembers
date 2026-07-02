// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Text;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.GameState
{
    /// <summary>
    ///   Shared byte[] JSON persistence for campaign behaviors, mirroring the pattern already
    ///   proven in <see cref="Storage.BannerlordNpcMemoryStore" />. Bannerlord's native STRING
    ///   serializer corrupts a save once the blob grows past roughly 65,535 characters (a
    ///   confirmed historical bug), so every behavior should persist its JSON state as a UTF-8
    ///   byte[] instead. <see cref="Load{T}" /> also migrates a save still holding an OLD
    ///   string-keyed blob, so saves written before a behavior switched to this helper keep
    ///   loading correctly.
    /// </summary>
    public static class JsonCampaignState
    {
        /// <summary>
        ///   Loads and deserializes the state stored under the byte[] <paramref name="key" />.
        ///   When that key is absent or empty (a save written before the behavior adopted this
        ///   helper), falls back to the legacy STRING key <paramref name="legacyKey" /> so the
        ///   old save still migrates. Returns <paramref name="fallback" />'s result when both are
        ///   missing, empty, or fail to deserialize; never throws.
        /// </summary>
        public static T Load<T>(IDataStore dataStore, string key, string legacyKey, Func<T> fallback)
            where T : class
        {
            byte[] bytes = null;
            try { dataStore.SyncData(key, ref bytes); }
            catch
            {
                // key absent in older saves, fall through to the legacy string key
            }

            if (bytes is {Length: > 0})
            {
                try { return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes)) ?? fallback(); }
                catch (Exception ex)
                {
                    ModLog.Error($"[STATE] '{key}' load failed, reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");

                    return fallback();
                }
            }

            string legacy = null;
            try { dataStore.SyncData(legacyKey, ref legacy); }
            catch
            {
                // no legacy data either, a fresh campaign
            }

            if (string.IsNullOrEmpty(legacy)) return fallback();

            try { return JsonConvert.DeserializeObject<T>(legacy) ?? fallback(); }
            catch (Exception ex)
            {
                ModLog.Error($"[STATE] legacy '{legacyKey}' load failed, reset for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");

                return fallback();
            }
        }

        /// <summary>
        ///   Serializes <paramref name="state" /> and persists it as a UTF-8 byte[] under
        ///   <paramref name="key" />. Never throws: a serialize or save failure is logged and an
        ///   empty blob is persisted instead of bricking the save.
        /// </summary>
        public static void Save<T>(IDataStore dataStore, string key, T state)
        {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state, Formatting.None)); }
            catch (Exception ex)
            {
                ModLog.Error($"[STATE] '{key}' serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                bytes = Encoding.UTF8.GetBytes("null");
            }

            try { dataStore.SyncData(key, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[STATE] '{key}' byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
