// Code written by Gabriel Mailhot, 09/11/2026.
// Makes, on purpose, the broken save tashmetu reported (Nexus, 09/11/2026), so the load rescue (SaveRescuePatch) can be
// proven in game rather than assumed: armed with cr.save_rescue_probe arm, the next save writes one plain byte[] of
// EngineCeiling + 100 bytes under its own key, the way CR's stores were written before 2.8.6. Without the rescue that save
// never loads; with it, it loads and the probe comes back empty. For a COPY of a campaign only: the save grows by about
// 320 MB (each byte of a byte[] costs about 19 bytes of save).

#region

using CalradiaRemembers.Logic.Saving;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Saving
{
   /// <summary>Writes an oversize byte[] once, on request, and reports what came back from it at the last load.</summary>
   internal static class SaveRescueProbe
   {
      private const string ProbeKey = "cr_rescue_probe";

      /// <summary>Whether the next save writes the oversize probe.</summary>
      internal static bool Armed { get; set; }

      /// <summary>The probe's length at the last load: null when the save held none, 0 when the rescue emptied it.</summary>
      internal static int? LastLoadedLength { get; private set; }

      /// <summary>Writes the probe when armed (then disarms), or reads it back on load. Never throws.</summary>
      internal static void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            if (!Armed) return;

            Armed = false;
            byte[] probe = new byte[SaveBlobCodec.EngineCeiling + 100];
            try { dataStore.SyncData(ProbeKey, ref probe); }
            catch
            {
               /* the probe is a test: it never stands in the way of a save */
            }

            return;
         }

         byte[] read = null;
         try { dataStore.SyncData(ProbeKey, ref read); }
         catch
         {
            /* no probe in this save */
         }

         LastLoadedLength = read?.Length;
      }
   }
}
