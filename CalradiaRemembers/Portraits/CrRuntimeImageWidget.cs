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

        public CrRuntimeImageWidget(UIContext context) : base(context) { }

        [Editor(false)]
        public string ImagePath
        {
            get => _imagePath;
            set
            {
                if (_imagePath == value) return;
                _imagePath = value;
                Sprite = string.IsNullOrEmpty(value) ? null : RuntimePortraitFactory.GetOrCreateCached(value);
            }
        }
    }
}
