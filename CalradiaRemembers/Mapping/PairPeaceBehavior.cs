// Code written by Gabriel Mailhot, 07/11/2026.
// The peaces characters have made with one another in a conversation, one per direction. kurushimi00 (Nexus, 06/11/2026):
// Ira and Adram made peace at the player's table and the encyclopedia still listed them as enemies "from the tourney".
// Gabriel (07/11/2026): personal grudges, one per direction; each pardons in their own voice. This keeps only the day each
// holder made peace with the other; PairPeacePolicy says what that peace lays to rest, and PairRegardReader leaves it out.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Foundation;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The day each character made peace with another, by direction.</summary>
   public sealed class PairPeaceBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads no peace at all (save-migration rule).
      private const string SaveKey = "CalradiaRemembers_PairPeace_v1bytes";

      private Dictionary<string, int> _peaceDays = new Dictionary<string, int>();

      public PairPeaceBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static PairPeaceBehavior Instance { get; private set; }

      /// <summary>The day <paramref name="holderId" /> last made peace with <paramref name="otherId" />, or null.</summary>
      public int? PeaceDay(string holderId, string otherId)
      {
         if (string.IsNullOrEmpty(holderId) || string.IsNullOrEmpty(otherId)) return null;

         return _peaceDays.TryGetValue(Key(holderId, otherId), out int day) ? day : (int?) null;
      }

      /// <summary>Records that the holder made peace with the other today.</summary>
      public void Record(string holderId, string otherId, int day)
      {
         if (string.IsNullOrEmpty(holderId) || string.IsNullOrEmpty(otherId) || holderId == otherId) return;

         _peaceDays[Key(holderId, otherId)] = day;
      }

      /// <summary>Forgets a peace (the self-test puts the world back as it found it).</summary>
      public void Forget(string holderId, string otherId) => _peaceDays.Remove(Key(holderId, otherId));

      public override void RegisterEvents()
      {
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Dictionary<string, int>(_peaceDays), Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[PEACE] serialize failed, saving no peace this time: {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("{}");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[PEACE] save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature */
            }

            _peaceDays = new Dictionary<string, int>();
            if (bytes is {Length: > 0})
               try { _peaceDays = JsonConvert.DeserializeObject<Dictionary<string, int>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, int>(); }
               catch (Exception ex)
               {
                  ModLog.Error($"[PEACE] load failed, no peace read this load: {ex.GetType().Name}: {ex.Message}");
               }
         }
      }

      private static string Key(string holderId, string otherId) => holderId + "|" + otherId;
   }
}
