// Code written by Gabriel Mailhot, 13/07/2026.
// How quickly companions get over things: the player's own taste, not a difficulty. The rebalance of 13/07/2026
// deliberately made the contentment loop gentle (a player has a realm to run), but a tester who had suffered the
// OLD punishing version still wrote "I do like the idea of it quite a lot" (SuperGraham339). This lets the ones
// who want the tension keep it, without imposing it on the ones who do not.
//
// The lever is the RECOVERY half of the loop, never the wounds themselves: dialing your patience must not
// rewrite what an insult costs. And the core guarantee survives every setting: a companion left merely IDLE can
// never be carried to the door, because the drift home is never weaker than the ennui of idleness.

namespace CalradiaRemembers.Logic
{
    /// <summary>How readily a companion's spirits recover from a grievance. A philosophy, not a difficulty.</summary>
    public enum ContentmentPacing
    {
        /// <summary>They take much in stride: a grievance fades almost as fast as it forms.</summary>
        Forgiving,

        /// <summary>A real, ignored grievance sours them over seasons. The default.</summary>
        Balanced,

        /// <summary>Grudges linger. A lord must truly tend his people.</summary>
        Exacting
    }

    /// <summary>Turns the player's chosen pacing into the weekly recovery it grants.</summary>
    public static class ContentmentPacingRules
    {
        /// <summary>
        ///   How far a companion's happiness eases back toward their clan's resting point each week. It is never
        ///   below the ennui of idleness (<see cref="HappinessDriver.Idle" />), in ANY setting, so being left
        ///   unoccupied can never on its own carry a companion out of your service.
        /// </summary>
        public static int DriftStepFor(ContentmentPacing pacing)
        {
            switch (pacing)
            {
                case ContentmentPacing.Forgiving: return 4;
                case ContentmentPacing.Exacting:  return 1;
                default:                          return CompanionHappiness.WeeklyDriftStep; // Balanced
            }
        }
    }
}
