// Code written by Gabriel Mailhot, 19/06/2026.

#region

using System;
using CalradiaRemembers.Logic.Chronicle;
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

      // ADDITIVE, and the reason it exists is worth carrying: the count above indexes into a list that could
      // only grow until tashmetu's bridge became the first caller anywhere to use Feed.Retract (22/09/2026).
      // Remove a deed the chronicle has already written up and the count points one entry too far in, so the
      // next illumination skips a deed that was never written. This names the last deed actually written, so
      // it can be found again wherever it has moved to. Absent on older saves, where the count is used exactly
      // as before.
      private const string MarkKey = "CalradiaRemembers_ChronicleThroughMark_v1";
      public static ChronicleBehavior Instance;

      public ChronicleBehavior()
      {
         Instance = this;
      }

      public string IlluminatedText { get; set; } = "";
      public int IlluminatedThroughCount { get; set; }

      /// <summary>The fingerprint of the last deed written up (<see cref="ChronicleResumePolicy.Mark" />), or empty.</summary>
      public string IlluminatedThroughMark { get; set; } = "";

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

            string mark = IlluminatedThroughMark ?? "";
            try { dataStore.SyncData(MarkKey, ref mark); }
            catch
            {
               /* non-fatal: the count still answers, as it did before this key existed */
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

            var mark = "";
            try { dataStore.SyncData(MarkKey, ref mark); }
            catch
            {
               /* absent in a save written before the mark: the count is used, exactly as it was */
            }

            IlluminatedThroughMark = mark ?? "";

            IlluminatedThroughCount = count;
         }
      }
   }
}