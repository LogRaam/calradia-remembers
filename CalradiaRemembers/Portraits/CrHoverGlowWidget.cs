// Code written by Gabriel Mailhot, 31/10/2026.
// A gold halo around a button, lit by the button's state (ButtonGlowPolicy). Dark Leather's tool buttons are transparent
// hotspots over painted tiles and many of the chat's small buttons carry no brush, so they have no hover layer to style.
// The picture is a nine-patch ring with an empty centre, so it fits any button shape and leaves the art inside untouched.
// Place it with negative margins so the ring stands around the button; the button must have a size of its own, since one
// sized by its children gives this nothing to stretch to and the halo floods the screen (Gabriel's screenshot, same day).

#region

using System.IO;
using CalradiaRemembers.Logic.Chat;
using TaleWorlds.GauntletUI;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>Place as a child of a fixed-size ButtonWidget: fades in around the button when it is hovered or held.</summary>
    public class CrHoverGlowWidget : CrRuntimeImageWidget
    {
        private float _strength;

        public CrHoverGlowWidget(UIContext context) : base(context)
        {
            AlphaFactor = 0f;
            DoNotAcceptEvents = true;
            CorrectRedBlue = false;
            NinePatch = "24,24,24,24";
            ImagePath = Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_hover_glow.png");
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            if (ParentWidget == null) return;

            _strength = ButtonGlowPolicy.Next(_strength, new ButtonGlowFacts {
                IsHovered = ParentWidget.IsHovered, IsPressed = ParentWidget.IsPressed, IsDisabled = ParentWidget.IsDisabled
            }, dt);
            AlphaFactor = _strength;
        }
    }
}
