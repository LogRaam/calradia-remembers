// Code written by Gabriel Mailhot, 08/07/2026.

#region

using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.Appearance
{
    /// <summary>
    ///   Show/hide lifecycle for the appearance-note editor popup. A focus Gauntlet layer with input
    ///   restrictions set, so the field captures Backspace instead of the encyclopedia navigating BACK.
    /// </summary>
    public static class AppearanceEditorManager
    {
        private const string PrefabName = "CalradiaRemembersAppearanceEditor";

        private static ScreenBase _host;
        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movieId;
        private static AppearanceEditorViewModel _vm;

        public static bool IsOpen => _layer != null;

        /// <summary>
        ///   Opens the editor pre-filled with <paramref name="currentNote" />. <paramref name="composePreview" />
        ///   turns a candidate note into the full "how others see you" line for the live preview;
        ///   <paramref name="onConfirm" /> receives the final note text when the player confirms.
        /// </summary>
        public static void Open(string currentNote, Func<string, string> composePreview, Action<string> onConfirm)
        {
            if (IsOpen) return;

            _host = ScreenManager.TopScreen;
            if (_host == null)
            {
                ModLog.Error("AppearanceEditorManager.Open: no top screen available.");

                return;
            }

            _vm = new AppearanceEditorViewModel(currentNote, composePreview)
            {
                OnCloseRequested = Close,
                OnConfirm = onConfirm
            };

            _layer = new GauntletLayer("CalradiaRemembersAppearanceEditor", 11000);
            _layer.InputRestrictions.SetInputRestrictions();
            _movieId = _layer.LoadMovie(PrefabName, _vm);

            _host.AddLayer(_layer);
            _layer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_layer);

            ModLog.Info($"Appearance editor opened on {_host.GetType().Name}.");
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

            ModLog.Info("Appearance editor closed.");
        }
    }
}
