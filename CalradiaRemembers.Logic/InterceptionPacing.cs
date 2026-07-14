// Code written by Gabriel Mailhot, 13/07/2026.
// How often lords ride out to have a word with the player. A player asked for this outright ("is it possible to
// let the player set the chances the AI will approach the player?") and, in the same breath, named the reason it
// matters in BOTH directions: "I like NPCs approaching me to kick off conversations" and "I don't want to be
// forced into a bunch of conversations" (Fakade, 2026-07-13). One player, two opposite needs, depending on the
// evening. That is a setting, not a number for us to guess at.
//
// Two levers move together, because they are the same feeling: how likely an approach is on any given day, and
// how long a lord who has already had his word waits before wanting another. Turning one without the other gives
// a false dial (a high chance with a long cooldown still starves; a low chance with none still nags).

namespace CalradiaRemembers.Logic
{
    /// <summary>How readily lords ride out to meet the player on the road. The player's taste, not a difficulty.</summary>
    public enum InterceptionPacing
    {
        /// <summary>Never. No lord ever rides out; the road is the player's own.</summary>
        Never,

        /// <summary>A rare thing, remembered when it happens.</summary>
        Rarely,

        /// <summary>The shipped feel: often enough to matter, seldom enough to welcome. The default.</summary>
        Occasionally,

        /// <summary>The realm comes to you. Expect company.</summary>
        Often
    }

    /// <summary>Turns the player's chosen pacing into the two numbers that actually govern how often a lord appears.</summary>
    public static class InterceptionPacingRules
    {
        /// <summary>The shipped daily chance, and the value Occasionally must keep returning so the default feel never drifts.</summary>
        public const int DefaultDailyChancePercent = 25;

        /// <summary>The shipped per-lord cooldown, for the same reason.</summary>
        public const int DefaultCooldownDays = 30;

        /// <summary>
        ///   The chance, per day, that ANY lord rides out to intercept the player. Zero at
        ///   <see cref="InterceptionPacing.Never" />, which switches the whole feature off rather than merely
        ///   making it rare: a player who does not want to be stopped on the road should not be stopped at all.
        /// </summary>
        public static int DailyChancePercent(InterceptionPacing pacing)
        {
            switch (pacing)
            {
                case InterceptionPacing.Never:  return 0;
                case InterceptionPacing.Rarely: return 10;
                case InterceptionPacing.Often:  return 50;
                default:                        return DefaultDailyChancePercent; // Occasionally
            }
        }

        /// <summary>
        ///   Days before the SAME lord may want another word. It moves with the chance rather than against it: a
        ///   player who asked for more company should not then be starved by a long silence per lord, and one who
        ///   asked for less should not be nagged by the same face returning quickly.
        /// </summary>
        public static int CooldownDays(InterceptionPacing pacing)
        {
            switch (pacing)
            {
                case InterceptionPacing.Never:  return DefaultCooldownDays; // moot: nobody rides out at all
                case InterceptionPacing.Rarely: return 60;
                case InterceptionPacing.Often:  return 15;
                default:                        return DefaultCooldownDays; // Occasionally
            }
        }

        /// <summary>True when the player has switched the whole thing off, so the behaviour can skip its work entirely.</summary>
        public static bool IsOff(InterceptionPacing pacing) => pacing == InterceptionPacing.Never;
    }
}
