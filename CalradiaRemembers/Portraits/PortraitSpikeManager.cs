// Code written by Gabriel Mailhot, 09/07/2026.

#region

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;
using EngTex = TaleWorlds.Engine.Texture;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>
    ///   Show/hide lifecycle for the portrait-spike test overlay. Holds the engine texture behind the shown
    ///   sprite and RELEASES it on close, so we can confirm loading a runtime image does not leak VRAM.
    /// </summary>
    public static class PortraitSpikeManager
    {
        private const string PrefabName = "CalradiaRemembersPortraitSpike";

        private static ScreenBase _host;
        private static GauntletLayer _layer;
        private static GauntletMovieIdentifier _movieId;
        private static PortraitSpikeViewModel _vm;
        private static EngTex _texture;

        public static bool IsOpen => _layer != null;

        public static void Open(Sprite sprite, EngTex engineTexture, string status)
        {
            if (IsOpen) Close();

            _host = ScreenManager.TopScreen;
            if (_host == null)
            {
                ModLog.Error("PortraitSpikeManager.Open: no top screen available.");

                return;
            }

            _texture = engineTexture;
            _vm = new PortraitSpikeViewModel {OnCloseRequested = Close, Portrait = sprite, Status = status};

            _layer = new GauntletLayer("CalradiaRemembersPortraitSpike", 11000);
            _layer.InputRestrictions.SetInputRestrictions();
            _movieId = _layer.LoadMovie(PrefabName, _vm);

            // A Sprite object cannot be bound through @-binding, so set it on the widget directly once the
            // prefab's widget tree exists (right after LoadMovie). The calibration square gets a KNOWN vanilla
            // sprite (the backdrop's own, resolved by the prefab system) through the same code path, and the
            // wiring outcome is surfaced in the visible status line, so one screenshot tells the whole story.
            Widget root = _movieId.Movie?.RootWidget;
            Widget image = FindById(root, "PortraitImage");
            Widget calib = FindById(root, "CalibrationSquare");

            var wiring = "";
            if (image != null) { image.Sprite = sprite; wiring += "portrait:set"; }
            else wiring += "portrait:WIDGET NOT FOUND";
            wiring += sprite?.Texture != null ? ", tex:ok" : ", tex:NULL";

            Sprite vanilla = FindAnySprite(root);
            if (calib != null && vanilla != null) { calib.Sprite = vanilla; wiring += ", calib:set"; }
            else wiring += ", calib:none";

            _vm.Status = $"{status} | {wiring}";

            _host.AddLayer(_layer);
            _layer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_layer);

            ModLog.Info($"Portrait spike overlay opened: {status}");
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

            // VRAM half of the spike: give the GPU memory back the moment the overlay closes.
            if (_texture != null && !_texture.IsReleased) _texture.Release();

            _texture = null;
            ModLog.Info("Portrait spike overlay closed (texture released).");
        }

        /// <summary>Depth-first search of the loaded widget tree for a widget by its Id.</summary>
        private static Widget FindById(Widget root, string id)
        {
            if (root == null) return null;

            foreach (Widget w in root.GetAllChildrenAndThisRecursive())
                if (w.Id == id)
                    return w;

            return null;
        }

        /// <summary>The first prefab-resolved sprite in the tree (a known-good vanilla sprite for calibration).</summary>
        private static Sprite FindAnySprite(Widget root)
        {
            if (root == null) return null;

            foreach (Widget w in root.GetAllChildrenAndThisRecursive())
                if (w.Sprite != null && w.Id != "PortraitImage" && w.Id != "CalibrationSquare")
                    return w.Sprite;

            return null;
        }
    }
}
