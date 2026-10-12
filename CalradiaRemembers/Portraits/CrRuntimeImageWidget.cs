// Code written by Gabriel Mailhot, 09/07/2026.
// A GauntletUI widget that displays a runtime-loaded image from a file PATH. A Sprite object cannot be @-bound in
// prefab XML, but a string can, so this widget takes a bindable ImagePath, loads the sprite itself (cached by
// path via RuntimePortraitFactory), and shows it. This is the reusable mechanism the custom-portrait feature
// needs; the spike uses it to put the player's own portrait on their chat lines beside the vanilla NPC renders.

#region

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>A widget that renders a runtime image loaded from <see cref="ImagePath" /> (bindable string).</summary>
    public class CrRuntimeImageWidget : Widget
    {
        private string _imagePath = "";
        private bool _correctRedBlue = true;
        private string _ninePatch = "";

        public CrRuntimeImageWidget(UIContext context) : base(context) { }

        [Editor(false)]
        public string ImagePath
        {
            get => _imagePath;
            set
            {
                if (_imagePath == value) return;
                _imagePath = value;
                Rebuild();
            }
        }

        /// <summary>
        ///   Whether to apply the engine's BGR-read colour correction (see <see cref="RuntimePortraitFactory" />).
        ///   True (default) for the JPG player portraits; a UI sprite from a normal PNG sets it false so its gold
        ///   is not inverted to blue.
        /// </summary>
        [Editor(false)]
        public bool CorrectRedBlue
        {
            get => _correctRedBlue;
            set
            {
                if (_correctRedBlue == value) return;
                _correctRedBlue = value;
                Rebuild();
            }
        }

        /// <summary>Fixed nine-patch corner widths as "left,right,top,bottom"; empty is a flat, full-stretch sprite.</summary>
        [Editor(false)]
        public string NinePatch
        {
            get => _ninePatch;
            set
            {
                if (_ninePatch == value) return;
                _ninePatch = value;
                Rebuild();
            }
        }

        private void Rebuild()
            => Sprite = string.IsNullOrEmpty(_imagePath)
                ? null
                : _imagePath.StartsWith(LiveSprites.LivePrefix, System.StringComparison.Ordinal)
                    ? LiveSprites.SpriteFor(_imagePath)
                    : RuntimePortraitFactory.GetOrCreateCached(_imagePath, _correctRedBlue, ParseNinePatch(_ninePatch));

        private static RuntimePortraitFactory.NinePatch ParseNinePatch(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return default;

            string[] p = s.Split(',');
            if (p.Length == 4
                && int.TryParse(p[0].Trim(), out int l) && int.TryParse(p[1].Trim(), out int r)
                && int.TryParse(p[2].Trim(), out int t) && int.TryParse(p[3].Trim(), out int b))
                return new RuntimePortraitFactory.NinePatch(l, r, t, b);

            return default;
        }
    }
}
