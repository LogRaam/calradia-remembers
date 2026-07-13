// Code written by Gabriel Mailhot, 13/07/2026.
// Party morale is something the player ALREADY manages (food, wages, victories), so letting it colour companion
// contentment costs them no new chore and makes good camp-keeping feel rewarded (player request, SuperGraham339).
// Deliberately a light touch: a nudge each week, never a driver of departures on its own.

namespace CalradiaRemembers.Logic
{
    /// <summary>Turns the player party's morale into the weekly mood nudge it gives their companions, if any.</summary>
    public static class PartyMoraleMood
    {
        /// <summary>At or above this the camp is in good spirits. Tunable.</summary>
        public const float HighAt = 70f;

        /// <summary>At or below this the camp is sullen. Tunable.</summary>
        public const float LowAt = 30f;

        /// <summary>
        ///   The driver the camp's mood contributes this week, or null in the broad middle band where morale is
        ///   unremarkable and should say nothing at all.
        /// </summary>
        public static HappinessDriver? DriverFor(float morale)
        {
            if (morale >= HighAt) return HappinessDriver.HighMorale;
            if (morale <= LowAt) return HappinessDriver.LowMorale;

            return null;
        }
    }
}
