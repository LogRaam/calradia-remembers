// Code written by Gabriel Mailhot, 19/06/2026.

#region

using System;
using System.Text;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chronicle
{
   /// <summary>
   ///   Holds and persists the illuminated chronicle: <see cref="IlluminatedText" /> is the prose
   ///   written so far, <see cref="IlluminatedThroughCount" /> is how many of the player's deeds it
   ///   covers (so a later pass illuminates only what was added since). Stored as a UTF-8 byte[]
   ///   (no native string-size ceiling) with the same "never brick a save" degradation.
   /// </summary>
   public sealed class ChronicleBehavior : CampaignBehaviorBase
   {
      private const string CountKey = "CalradiaRemembers_ChronicleThrough_v1";

      private const string TextKey = "CalradiaRemembers_ChronicleText_v1bytes";
      public static ChronicleBehavior Instance;

      public ChronicleBehavior()
      {
         Instance = this;
      }

      public string IlluminatedText { get; set; } = "";
      public int IlluminatedThroughCount { get; set; }

      public override void RegisterEvents() { }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes = Encoding.UTF8.GetBytes(IlluminatedText ?? "");
            try { dataStore.SyncData(TextKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[CHRONICLE] text save failed: {ex.GetType().Name}: {ex.Message}"); }

            int count = IlluminatedThroughCount;
            try { dataStore.SyncData(CountKey, ref count); }
            catch
            {
               /* non-fatal */
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(TextKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            IlluminatedText = bytes is {Length: > 0}
               ? Encoding.UTF8.GetString(bytes)
               : "";

            var count = 0;
            try { dataStore.SyncData(CountKey, ref count); }
            catch
            {
               /* absent */
            }

            IlluminatedThroughCount = count;
         }
      }
   }
}