// Code written by Gabriel Mailhot, 07/08/2026.
// Show/hide lifecycle for the per-contact letter archive (thread) window. Mirrors ChronicleManager:
// one window at a time, defensive teardown, opened over whatever screen is on top (the encyclopedia).

#region

using NpcMemoryService.Core.Models;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Show/hide lifecycle for the letter-archive thread window.</summary>
   public static class LetterArchiveManager
   {
      private const string PrefabName = "CalradiaRemembersLetterArchive";
      private static ScreenBase _host;

      private static GauntletLayer _layer;
      private static GauntletMovieIdentifier _movieId;
      private static LetterArchiveViewModel _vm;

      public static bool IsOpen => _layer != null;

      public static void Close()
      {
         if (!IsOpen) return;

         try
         {
            _layer.IsFocusLayer = false;
            _layer.InputRestrictions.ResetInputRestrictions();
            _layer.ReleaseMovie(_movieId);
            _host?.RemoveLayer(_layer);
         }
         catch (System.Exception ex)
         {
            ModLog.Error($"[LETTER ARCHIVE] window teardown failed (non-fatal, fields cleared anyway): {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            _layer = null;
            _movieId = null;
            _host = null;
            _vm = null;
         }

         ModLog.Info("Letter archive window closed.");
      }

      /// <summary>Opens the archive showing the whole correspondence thread with <paramref name="profile" />.</summary>
      public static void Open(NpcProfile profile)
      {
         if (IsOpen) return;
         if (profile == null)
         {
            ModLog.Error("LetterArchiveManager.Open: no contact profile supplied.");

            return;
         }

         _host = ScreenManager.TopScreen;
         if (_host == null)
         {
            ModLog.Error("LetterArchiveManager.Open: no top screen available.");

            return;
         }

         _vm = new LetterArchiveViewModel(profile) {OnCloseRequested = Close};

         _layer = new GauntletLayer("CalradiaRemembersLetterArchive", 11000);
         _layer.InputRestrictions.SetInputRestrictions();

         try
         {
            _movieId = _layer.LoadMovie(PrefabName, _vm);
         }
         catch (System.Exception ex)
         {
            ModLog.Error($"[LETTER ARCHIVE] LoadMovie threw; rolling back (no window opened): {ex.GetType().Name}: {ex.Message}");
            _movieId = null;
         }

         if (_movieId == null)
         {
            try { _layer.InputRestrictions.ResetInputRestrictions(); }
            catch (System.Exception ex) { ModLog.Error($"[LETTER ARCHIVE] rollback reset failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
            _layer = null;
            _host = null;
            _vm = null;

            return;
         }

         _host.AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);

         ModLog.Info($"Letter archive window opened on {_host.GetType().Name}.");
      }
   }
}
