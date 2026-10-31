// Code written by Gabriel Mailhot, 31/10/2026.
// The glow a chat button shows under the cursor. Gabriel (31/10/2026): "ce serait bien d'ajouter le hover effect sur les
// boutons". Dark Leather's tool buttons are transparent hotspots over painted tiles, so nothing told the player they
// could be clicked. A disabled button stays dark, or the glow would promise a click that does nothing.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>What the button under the glow is doing.</summary>
    public sealed class ButtonGlowFacts
    {
        /// <summary>The cursor is on the button.</summary>
        public bool IsHovered { get; init; }

        /// <summary>The button is held down.</summary>
        public bool IsPressed { get; init; }

        /// <summary>The button refuses clicks now.</summary>
        public bool IsDisabled { get; init; }
    }

    /// <summary>How strongly a chat button glows, and how the glow fades there.</summary>
    public static class ButtonGlowPolicy
    {
        /// <summary>The glow under the cursor: faint, so the painted tile still reads through it. Tuning.</summary>
        public const float HoveredStrength = 0.22f;

        /// <summary>The glow while held down, a clear answer to the press. Tuning.</summary>
        public const float PressedStrength = 0.34f;

        /// <summary>How long a full fade takes, in seconds: quick enough to feel like the cursor's own. Tuning.</summary>
        public const float FadeSeconds = 0.12f;

        /// <summary>The strength this button should reach.</summary>
        public static float TargetStrength(ButtonGlowFacts facts)
        {
            if (facts == null || facts.IsDisabled) return 0f;
            if (facts.IsPressed) return PressedStrength;

            return facts.IsHovered ? HoveredStrength : 0f;
        }

        /// <summary>The strength after <paramref name="seconds" /> of fading from <paramref name="current" /> toward the button's target.</summary>
        public static float Next(float current, ButtonGlowFacts facts, float seconds)
        {
            float target = TargetStrength(facts);
            float step = Math.Max(seconds, 0f) * PressedStrength / FadeSeconds;

            return current < target ? Math.Min(current + step, target) : Math.Max(current - step, target);
        }
    }
}
