// Code written by Gabriel Mailhot, 07/07/2026.
// Player appearance pillar, persistence: the player-authored per-hero appearance prose (the editable descriptor
// that replaces player_description.txt). Stored as a byte[] JSON blob (the corrupting-large-string pattern, the
// same save-safety net as GrudgeBehavior / BastardBehavior): a corrupt blob degrades to an empty map rather
// than throwing from SyncData. Feeds the AppearanceComposer's authored-prose argument for player AND NPC.

#region

using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Appearance
{
    /// <summary>Persists authored appearance prose keyed by <see cref="Hero.StringId" />. Save-safe, never throws from SyncData.</summary>
    public sealed class AppearanceProseBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_AppearanceProse_v1bytes";

        public static AppearanceProseBehavior Instance;

        private Dictionary<string, string> _prose = new();

        public AppearanceProseBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            // No events: this is a passive store, read at prompt-build time and written by the edit surface.
        }

        /// <summary>The authored prose for a hero, or "" when none is set.</summary>
        public string Get(string heroStringId)
            => heroStringId != null && _prose.TryGetValue(heroStringId, out string v) ? v ?? "" : "";

        /// <summary>Sets the authored prose for a hero, or clears it when the text is null/blank. Returns the stored (trimmed) value.</summary>
        public string Set(string heroStringId, string prose)
        {
            if (string.IsNullOrEmpty(heroStringId)) return "";

            if (string.IsNullOrWhiteSpace(prose))
            {
                _prose.Remove(heroStringId);

                return "";
            }

            string trimmed = prose.Trim();
            _prose[heroStringId] = trimmed;

            return trimmed;
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try
                {
                    // Serialize a frozen copy so an autosave racing a main-thread edit cannot throw mid-serialize.
                    bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Dictionary<string, string>(_prose), Formatting.None));
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[APPEARANCE] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Array.Empty<byte>();
                }

                dataStore.SyncData(SaveKey, ref bytes);
            }
            else
            {
                byte[] bytes = Array.Empty<byte>();
                try
                {
                    dataStore.SyncData(SaveKey, ref bytes);
                    _prose = bytes is {Length: > 0}
                        ? JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, string>()
                        : new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[APPEARANCE] load failed, starting empty (graceful degradation). {ex.GetType().Name}: {ex.Message}");
                    _prose = new Dictionary<string, string>();
                }
            }
        }
    }
}
