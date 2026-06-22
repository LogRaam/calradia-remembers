// Code written by Gabriel Mailhot, 21/06/2026.
// The dedicated courier window — letters split out of the chat overlay into their own parchment sheet.
// Mirrors ChronicleManager's layer lifecycle (no LLM plumbing); the window seals + closes SYNCHRONOUSLY
// from the button phase, the pattern that ended the letter-reply hang and the GauntletUI close crash.

#region

using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Letters
{
    /// <summary>Show/hide lifecycle for the parchment courier window. One window at a time.</summary>
    public static class LetterWindowManager
    {
        private const string PrefabName = "CalradiaRemembersLetter";

        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movieId;
        private static ScreenBase _host;
        private static LetterViewModel _vm;

        public static bool IsOpen => _layer != null;

        /// <summary>Reads a freshly delivered letter on parchment, with Acknowledge / Reply choices.</summary>
        public static void OpenRead(PendingLetter letter, NpcProfile senderProfile)
        {
            if (!OpenWindow()) return;
            _vm.SetupRead(letter, senderProfile);
        }

        /// <summary>Opens a blank parchment to compose a new letter to <paramref name="recipientProfile"/>.</summary>
        public static void OpenCompose(NpcProfile recipientProfile, int courierCost = 0)
        {
            if (!OpenWindow()) return;
            _vm.SetupCompose(recipientProfile, courierCost);
        }

        /// <summary>Opens a reply parchment showing the received letter above the player's answer.</summary>
        public static void OpenReply(PendingLetter letter, NpcProfile profile, Hero npcHero)
        {
            if (!OpenWindow()) return;
            _vm.SetupReply(letter, profile, npcHero);
        }

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

            _vm = new LetterViewModel { OnCloseRequested = Close };

            _layer = new GauntletLayer("CalradiaRemembersLetter", 11000);
            _layer.InputRestrictions.SetInputRestrictions();
            _movieId = _layer.LoadMovie(PrefabName, _vm);

            _host.AddLayer(_layer);
            _layer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_layer);

            ModLog.Info($"Courier window opened on {_host.GetType().Name}.");
            return true;
        }

        public static void Close()
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

            ModLog.Info("Courier window closed.");
        }
    }
}
