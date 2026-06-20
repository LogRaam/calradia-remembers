// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 Phase 2: opens the illuminated chronicle as a non-blocking Gauntlet overlay on the current
// top screen (map, encyclopedia, …). Mirrors ChatManager's layer lifecycle, without the LLM plumbing.

#region

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Chronicle
{
    /// <summary>Show/hide lifecycle for the chronicle window.</summary>
    public static class ChronicleManager
    {
        private const string PrefabName = "CalradiaRemembersChronicle";

        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movieId;
        private static ScreenBase _host;
        private static ChronicleViewModel _vm;

        public static bool IsOpen => _layer != null;

        /// <summary>Rebuilds the open chronicle window from current state (e.g. after an async illumination pass).</summary>
        public static void RefreshActive() => _vm?.RefreshFromState();

        public static void Open()
        {
            if (IsOpen) return;

            _host = ScreenManager.TopScreen;
            if (_host == null)
            {
                ModLog.Error("ChronicleManager.Open: no top screen available.");
                return;
            }

            _vm = new ChronicleViewModel { OnCloseRequested = Close };

            _layer = new GauntletLayer("CalradiaRemembersChronicle", 11000);
            _layer.InputRestrictions.SetInputRestrictions();
            _movieId = _layer.LoadMovie(PrefabName, _vm);

            _host.AddLayer(_layer);
            _layer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_layer);

            ModLog.Info($"Chronicle window opened on {_host.GetType().Name}.");
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

            ModLog.Info("Chronicle window closed.");
        }
    }
}
