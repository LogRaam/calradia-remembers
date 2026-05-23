using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Storage;
using TaleWorlds.CampaignSystem;

namespace CalradiaRemembers.Storage
{
    /// <summary>
    /// Implements <see cref="INpcMemoryStore"/> using TaleWorlds' native
    /// <see cref="IDataStore"/> for persistence inside the campaign save file.
    /// </summary>
    public sealed class BannerlordNpcMemoryStore : INpcMemoryStore
    {
        private const string SaveKey = "CalradiaRemembers_NpcProfiles_v1";

        private readonly Dictionary<string, NpcProfile> _profiles
            = new Dictionary<string, NpcProfile>();

        // ── TaleWorlds save/load bridge ───────────────────────────────────────

        public void SyncData(IDataStore dataStore)
        {
            string json = dataStore.IsSaving ? SerializeProfiles() : null;
            dataStore.SyncData<string>(SaveKey, ref json);

            if (!dataStore.IsSaving && json != null)
                DeserializeProfiles(json);

            ModLog.Info(dataStore.IsSaving
                ? $"Saved {_profiles.Count} NPC profile(s)."
                : $"Loaded {_profiles.Count} NPC profile(s) from save.");
        }

        // ── INpcMemoryStore ───────────────────────────────────────────────────

        public Task InitializeAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        public NpcProfile Get(string npcId)
            => _profiles.TryGetValue(npcId, out NpcProfile profile) ? profile : null;

        public void Set(NpcProfile profile)
            => _profiles[profile.Id] = profile;

        public void Remove(string npcId)
            => _profiles.Remove(npcId);

        public IReadOnlyList<string> ListIds()
            => new List<string>(_profiles.Keys);

        public Task CommitAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        // ── Serialization ─────────────────────────────────────────────────────

        private string SerializeProfiles()
            => JsonConvert.SerializeObject(_profiles, Formatting.None);

        private void DeserializeProfiles(string json)
        {
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, NpcProfile>>(json);
            if (loaded == null) return;
            _profiles.Clear();
            foreach (var kvp in loaded)
                _profiles[kvp.Key] = kvp.Value;
        }
    }
}
