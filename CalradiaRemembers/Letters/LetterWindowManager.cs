// Code written by Gabriel Mailhot, 21/06/2026.
// 03/09/2026: migrated from a TopScreen overlay layer (the map kept RENDERING underneath, a latent render-texture
// VRAM leak) to the shared crash-safe CrPanelHost, so the parchment is now a pushed screen (map render suspended,
// leak-free) with the campaign clock frozen while open, hosted identically to every other CR panel. The window's
// own hardening (portrait release before teardown, defensive teardown, session reset, LoadMovie-failure survival)
// now lives IN the host, so it applies to every panel, not just this one.

#region

using System;
using CalradiaRemembers.UI;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Show/hide lifecycle for the parchment courier window, composed on the shared crash-safe panel host. One window at a time.</summary>
   public static class LetterWindowManager
   {
      // Set by whichever Open* method the caller uses, then applied to the FRESH view model inside the host's
      // factory (below) on the same open. This is how the one window carries its three modes (compose/read/reply)
      // with per-open data through the host's fixed "prefab + factory" shape.
      private static Action<LetterViewModel> _pendingSetup;

      // The panel's identity: its prefab, and a factory that builds a FRESH view model each open and applies the
      // pending mode setup to it before the movie loads (so the parchment binds already populated). The host owns
      // everything else: the pushed screen, the campaign-clock freeze, input restrictions and focus, teardown on any
      // pop (including the view model's portrait release, since LetterViewModel is an IManagedPortrait), and reload.
      // designUnits: the prefab is a fixed 1180x944-unit centred book (BookRoot) over the painted leather art. The
      // fit zoom only engages when the player's real layout space is SMALLER than the book itself (high UI-Scale,
      // tiny window), shrinking art and contents together so the book stays whole instead of overflowing.
      private static readonly CrPanelHost Host = new CrPanelHost(
         "CalradiaRemembersLetter",
         close =>
         {
            var vm = new LetterViewModel {OnCloseRequested = close};
            _pendingSetup?.Invoke(vm);

            return vm;
         },
         designUnitsWidth: 1180f, designUnitsHeight: 944f);

      public static bool IsOpen => Host.IsOpen;

      /// <summary>Closes the courier window (delegates to the host: teardown, pop, clock resumed).</summary>
      public static void Close() => Host.Close();

      /// <summary>
      ///   Dev hot-reload (cr.reload_ui): close+reopen on the refreshed prefab. See CrPanelHost.Reload. The reopen
      ///   rebuilds the same mode because _pendingSetup still holds the last Open*'s setup.
      /// </summary>
      public static void ReloadActiveMovie() => Host.Reload();

      /// <summary>
      ///   C4: at session launch, drop any window reference inherited from a prior session (its engine objects are
      ///   already gone) so IsOpen can never be stuck true and swallow every subsequent letter. Never touches an
      ///   engine object.
      /// </summary>
      public static void ResetForSession()
      {
         Host.ResetForSession();
         _pendingSetup = null;
      }

      /// <summary>Opens a blank parchment to compose a new letter to <paramref name="recipientProfile" />.</summary>
      public static void OpenCompose(NpcProfile recipientProfile, int courierCost = 0)
         => OpenWith(vm => vm.SetupCompose(recipientProfile, courierCost));

      /// <summary>Reads a freshly delivered letter on parchment, with Acknowledge / Reply choices.</summary>
      public static void OpenRead(PendingLetter letter, NpcProfile senderProfile)
         => OpenWith(vm => vm.SetupRead(letter, senderProfile));

      /// <summary>Opens a reply parchment showing the received letter above the player's answer.</summary>
      public static void OpenReply(PendingLetter letter, NpcProfile profile, Hero npcHero)
         => OpenWith(vm => vm.SetupReply(letter, profile, npcHero));

      #region private

      // One window at a time: if the parchment is already open, refuse rather than stacking a second (matches the
      // old overlay behaviour). Otherwise stage this open's setup and let the host build + show the window.
      private static void OpenWith(Action<LetterViewModel> setup)
      {
         if (Host.IsOpen)
         {
            ModLog.Info("LetterWindowManager.Open called but a courier window is already open.");

            return;
         }

         _pendingSetup = setup;
         Host.Open();
      }

      #endregion
   }
}
