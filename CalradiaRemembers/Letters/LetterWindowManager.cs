// Code written by Gabriel Mailhot, 21/06/2026.

#region

using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Show/hide lifecycle for the parchment courier window. One window at a time.</summary>
   public static class LetterWindowManager
   {
      private const string PrefabName = "CalradiaRemembersLetter";
      private static ScreenBase _host;

      private static GauntletLayer _layer;
      private static GauntletMovieIdentifier _movieId;
      private static LetterViewModel _vm;

      // Campaign time is paused while the parchment is open (a letter pops up on arrival, and the player must be
      // able to read/answer it without the world running on under a focused window they cannot pause). Restored
      // on close to whatever speed they were at.
      private static CampaignTimeControlMode _previousTimeControlMode;

      public static bool IsOpen => _layer != null;

      public static void Close()
      {
         if (!IsOpen) return;

         // C4: after a save-load or campaign-end the engine objects behind these fields may already be dead. Tear
         // down defensively so a late Close never crashes on a destroyed layer/movie, and ALWAYS null the fields
         // (finally) so a failed teardown can never leave a zombie window that blocks every future letter.
         try
         {
            _layer.IsFocusLayer = false;
            _layer.InputRestrictions.ResetInputRestrictions();
            _vm?.ReleasePortrait(); // free the correspondent tableau before the movie teardown
            _layer.ReleaseMovie(_movieId);
            _host?.RemoveLayer(_layer);

            // Restore time only if WE still hold it at Stop (4.6): never yank the player out of a speed they set
            // themselves after the window opened.
            if (Campaign.Current != null && Campaign.Current.TimeControlMode == CampaignTimeControlMode.Stop)
               Campaign.Current.TimeControlMode = _previousTimeControlMode;
         }
         catch (System.Exception ex)
         {
            ModLog.Error($"[LETTER] window teardown failed (non-fatal, fields cleared anyway): {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            _layer = null;
            _movieId = null;
            _host = null;
            _vm = null;
         }

         ModLog.Info("Courier window closed.");
      }

      /// <summary>
      ///   Dev hot-reload (cr.reload_ui): if the letter book is open, rebuild its widget tree in place by
      ///   releasing and reloading the movie against the SAME view-model, so the current letter, its mode, and
      ///   the correspondence thread all survive. Lets a UI author tune the letter prefab's margins without
      ///   reopening. The prefab XML itself is re-read by the caller (WidgetFactory.CheckForUpdates) before this.
      ///   The portrait id is left intact so the reloaded portrait widget rebinds to the same cached tableau.
      /// </summary>
      public static void ReloadActiveMovie()
      {
         if (!IsOpen) return;

         try
         {
            _layer.ReleaseMovie(_movieId);
            _movieId = _layer.LoadMovie(PrefabName, _vm);
            ModLog.Info("[RELOAD_UI] letter book rebuilt in place.");
         }
         catch (System.Exception ex)
         {
            ModLog.Error($"[RELOAD_UI] letter book reload failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   C4: nulls the statics WITHOUT touching any engine object (they belong to a screen torn down by a
      ///   previous session or load). Called at session launch so a dead layer reference from before can never
      ///   leave <see cref="IsOpen" /> stuck true, which would send every subsequent letter into the
      ///   never-shown-and-lost path.
      /// </summary>
      public static void ResetForSession()
      {
         _layer = null;
         _movieId = null;
         _host = null;
         _vm = null;
      }

      /// <summary>Opens a blank parchment to compose a new letter to <paramref name="recipientProfile" />.</summary>
      public static void OpenCompose(NpcProfile recipientProfile, int courierCost = 0)
      {
         if (!OpenWindow()) return;
         _vm.SetupCompose(recipientProfile, courierCost);
      }

      /// <summary>Reads a freshly delivered letter on parchment, with Acknowledge / Reply choices.</summary>
      public static void OpenRead(PendingLetter letter, NpcProfile senderProfile)
      {
         if (!OpenWindow()) return;
         _vm.SetupRead(letter, senderProfile);
      }

      /// <summary>Opens a reply parchment showing the received letter above the player's answer.</summary>
      public static void OpenReply(PendingLetter letter, NpcProfile profile, Hero npcHero)
      {
         if (!OpenWindow()) return;
         _vm.SetupReply(letter, profile, npcHero);
      }

      #region private

      private static bool OpenWindow()
      {
         if (IsOpen)
         {
            ModLog.Info("LetterWindowManager.Open called but a courier window is already open.");

            return false;
         }

         _host = ScreenManager.TopScreen;
         if (_host == null)
         {
            ModLog.Error("LetterWindowManager.Open: no top screen available.");

            return false;
         }

         _vm = new LetterViewModel {OnCloseRequested = Close};

         _layer = new GauntletLayer("CalradiaRemembersLetter", 11000);
         _layer.InputRestrictions.SetInputRestrictions();

         // C4: the movie can fail to load (a missing/broken prefab). If it does, a blank focused layer with
         // restricted input and paused time is a SOFT-LOCK (only ALT+F4 escapes). Validate the load and, on any
         // failure, roll the layer back completely BEFORE we grab focus or pause time, then refuse to open.
         try
         {
            _movieId = _layer.LoadMovie(PrefabName, _vm);
         }
         catch (System.Exception ex)
         {
            ModLog.Error($"[LETTER] LoadMovie threw; rolling back (no window opened): {ex.GetType().Name}: {ex.Message}");
            _movieId = null;
         }

         if (_movieId == null)
         {
            ModLog.Error("LetterWindowManager.Open: the parchment movie failed to load; rolling back rather than trapping the player.");
            try { _layer.InputRestrictions.ResetInputRestrictions(); }
            catch (System.Exception ex) { ModLog.Error($"[LETTER] rollback reset failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
            _layer = null;
            _movieId = null;
            _vm = null;
            _host = null;

            return false;
         }

         _host.AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);

         // Pause the campaign while the parchment holds focus, so the player can read/reply without time
         // running on (and without being unable to pause it under the focused window).
         if (Campaign.Current != null)
         {
            _previousTimeControlMode = Campaign.Current.TimeControlMode;
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
         }

         ModLog.Info($"Courier window opened on {_host.GetType().Name}.");

         return true;
      }

      #endregion
   }
}