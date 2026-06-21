// Code written by Gabriel Mailhot, 21/06/2026.
// v1.3 schemes — the price of taking a hand. Aiding a plot or warning a target is no longer free:
// if an abetted plot's outcome surfaces the player's complicity, the wronged party resents them; a
// warned plotter, exposed, suspects the meddler. Pure so these stakes stay protected by tests.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The relation penalties the player risks for meddling in a scheme. The host tracks who was
    ///   involved and applies the hit; this holds the rule for when — and how much — it costs.
    /// </summary>
    public static class SchemeBacklash
    {
        /// <summary>What the wronged target's regard for the player falls by when an abetted plot's hand is shown.</summary>
        public const int AbettorExposurePenalty = 12;

        /// <summary>What an exposed plotter's regard for the player falls by, suspecting them of the warning.</summary>
        public const int WarnedPlotterResentment = 8;

        /// <summary>
        ///   The hit the target's regard for the player takes when an abetted plot reaches an outcome
        ///   that shows the player's hand — it SUCCEEDED (and the deed, with the player in it, is
        ///   known) or was DISCOVERED mid-flight. A plot that simply fizzled (Failed without exposure)
        ///   never surfaces the player's role, so it costs nothing. Magnitude only; the host negates it.
        /// </summary>
        public static int AbettorPenalty(SchemeState outcome)
        {
            switch (outcome)
            {
                case SchemeState.Succeeded:
                case SchemeState.Discovered:
                    return AbettorExposurePenalty;
                default:
                    return 0;
            }
        }
    }
}
