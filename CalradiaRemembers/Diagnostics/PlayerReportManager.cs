// Code written by Gabriel Mailhot, 14/11/2026.
// Opens and closes the "report a mistake" window (PlayerReportViewModel), the same way the appearance editor does: a focus
// Gauntlet layer with input restrictions, so the text field keeps its keys. On "Create the report" it writes the file and
// the log (PlayerReportService), shows the file and puts the Discord invite in the clipboard.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Show/hide lifecycle of the player report window.</summary>
   internal static class PlayerReportManager
   {
      private const string PrefabName = "CalradiaRemembersReport";

      private static ScreenBase _host;
      private static GauntletLayer _layer;
      private static GauntletMovieIdentifier _movieId;
      private static PlayerReportViewModel _vm;

      internal static bool IsOpen => _layer != null;

      /// <summary>Opens the window for the conversation <paramref name="transcript" /> with <paramref name="characterName" />.</summary>
      internal static void Open(IReadOnlyList<string> transcript, string characterName)
      {
         if (IsOpen) return;

         _host = ScreenManager.TopScreen;
         if (_host == null) return;

         bool hardcore = ModSettings.AdultContentLevel == NpcMemoryService.Core.Models.AdultContentLevel.Hardcore;
         _vm = new PlayerReportViewModel((text, removed) => PlayerReportService.Gather(text, removed, transcript, characterName), hardcore) {
            OnCloseRequested = Close, OnCreated = Created
         };

         _layer = new GauntletLayer("CalradiaRemembersReport", 11000);
         _layer.InputRestrictions.SetInputRestrictions();
         _movieId = _layer.LoadMovie(PrefabName, _vm);
         _host.AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);
         ModLog.Info($"[REPORT] report window opened for {characterName}.");
      }

      internal static void Close()
      {
         if (!IsOpen) return;

         _layer.IsFocusLayer = false;
         _layer.InputRestrictions.ResetInputRestrictions();
         _layer.ReleaseMovie(_movieId);
         _host?.RemoveLayer(_layer);
         _layer = null;
         _movieId = null;
         _host = null;
         _vm = null;
      }

      private static void Created(string report)
      {
         string path = PlayerReportService.Write(report);
         PlayerReportService.ShowAndShare(path);

         string where = path ?? "CalradiaRemembers.log (the file could not be written)";
         InformationManager.ShowInquiry(new InquiryData(
            "Report saved",
            $"Your report is saved here:\n{where}\n\nIt is also written into CalradiaRemembers.log.\n\n"
            + $"Join the file in the bug reports channel of the Calradia Remembers Discord. The invite is in your clipboard:\n{PlayerReportService.DiscordInvite}",
            true, false, "Done", "", null, null));
      }
   }
}
