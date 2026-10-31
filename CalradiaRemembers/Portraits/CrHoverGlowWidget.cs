// Code written by Gabriel Mailhot, 31/10/2026.
// A faint glow inside a button, lit by the button's state (ButtonGlowPolicy). Dark Leather's tool buttons are transparent
// hotspots over painted tiles and many of the chat's small buttons carry no brush, so they have no hover layer to style.
// The sprite and colour are set where it is placed; this only moves the strength.

#region

using CalradiaRemembers.Logic.Chat;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>Place as a child of a ButtonWidget: fades in over the button when it is hovered or held.</summary>
    public class CrHoverGlowWidget : Widget
    {
        private float _strength;

        public CrHoverGlowWidget(UIContext context) : base(context)
        {
            AlphaFactor = 0f;
            DoNotAcceptEvents = true;
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
