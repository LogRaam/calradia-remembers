// Code written by Gabriel Mailhot, 31/10/2026.
// A painted button: the killing window's Strike and Stay your hand are Gabriel's own art, in four pictures each, and a
// picture cannot be chosen by state in prefab XML because a file path has no brush layers. Placed inside a ButtonWidget,
// this shows the picture for the button's state. All four load as soon as the art is named, so the first hover does not
// stall on a file read.

#region

using TaleWorlds.GauntletUI;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>Shows {ArtStem}_normal/_hover/_pressed/_disabled.png by the state of the parent button. Art is a straight RGBA PNG.</summary>
    public class CrButtonArtWidget : CrRuntimeImageWidget
    {
        private static readonly string[] States = {"normal", "hover", "pressed", "disabled"};

        private string _artStem = "";

        public CrButtonArtWidget(UIContext context) : base(context) => CorrectRedBlue = false;

        /// <summary>The full path of the pictures up to the state suffix; empty shows nothing and loads nothing.</summary>
        [Editor(false)]
        public string ArtStem
        {
            get => _artStem;
            set
            {
                if (_artStem == value) return;
                _artStem = value ?? "";
                if (_artStem.Length == 0)
                {
                    ImagePath = "";
                    return;
                }

                foreach (string state in States) RuntimePortraitFactory.GetOrCreateCached(PathFor(state), false);
                ImagePath = PathFor("normal");
            }
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            if (_artStem.Length == 0 || ParentWidget == null) return;

            string state = ParentWidget.IsDisabled ? "disabled"
                : ParentWidget.IsPressed ? "pressed"
                : ParentWidget.IsHovered ? "hover"
                : "normal";
            ImagePath = PathFor(state);
        }

        private string PathFor(string state) => $"{_artStem}_{state}.png";
    }
}
