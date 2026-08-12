// Code written by Gabriel Mailhot, 29/06/2026.
// Pure: decides whether — and how — the prompt should tell an NPC how long it has been since they last saw
// the player. On the campaign map time passes fast, so injecting the gap on every conversation made NPCs
// harp on it ("Four days since we last spoke...", player report). We surface it only once the gap is long
// enough that a person would genuinely feel the time apart, and as a plain fact the host frames with its own
// restraint guidance. Kept here, behind tests, so the threshold and wording are documented and easy to tune.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Turns a "days since the NPC last saw the player" count into a prompt-ready note, or null when the gap
    ///   is too short (or unknown) to be worth remarking on. Uses the Calradian calendar (84-day year, 21-day
    ///   season) to phrase longer gaps naturally.
    /// </summary>
    public static class MeetingGapDescriber
    {
        private const int SeasonDays = 21;
        private const int YearDays = 84;

        /// <summary>
        ///   Days up to and including this are treated as too recent to remark on. A week or less between
        ///   meetings is unremarkable when the player is criss-crossing the map; only a longer absence is felt.
        /// </summary>
        public const int RemarkThresholdDays = 7;

        /// <summary>
        ///   A plain note of how long it has been since the NPC last saw the player, or null when the gap is
        ///   unknown (negative), this week or shorter (≤ <see cref="RemarkThresholdDays" />). The host pairs a
        ///   non-null note with its own "do not dwell on it" guidance, so the model is aware of the time apart
        ///   without leading every conversation with it.
        /// </summary>
        public static string Describe(int daysSinceLastMeeting) => Describe(daysSinceLastMeeting, isHousehold: false);

        /// <summary>
        ///   As <see cref="Describe(int)" />, but relationship-aware. When <paramref name="isHousehold" /> is
        ///   true (the player's spouse, consort, or close kin), an absence is NOT framed as a fading acquaintance
        ///   they have not seen in a while (a player report: wives felt "left behind" the moment the player rode
        ///   out of town). Instead it is the plain truth of a married life split between the road and the hearth:
        ///   they are of the player's own house, apart while the player campaigns, keeping matters here and
        ///   awaiting a return, never abandoned. Rendered whatever the day count, since a household bond does not
        ///   turn on how many days it has been. The ordinary day-gated note still serves everyone else.
        /// </summary>
        public static string Describe(int daysSinceLastMeeting, bool isHousehold)
        {
            if (isHousehold)
                return "You belong to the player's own house (their spouse, consort, or close kin). You are apart " +
                       "only because the player is away on campaign while you keep matters here; you await their " +
                       "return and are never abandoned, forgotten, or left behind.";

            if (daysSinceLastMeeting <= RemarkThresholdDays) return null;

            return $"It has been roughly {HumanizeSpan(daysSinceLastMeeting)} since you last saw the player.";
        }

        #region private

        private static string HumanizeSpan(int days)
        {
            if (days < SeasonDays) return $"{days} days";

            if (days < YearDays)
            {
                int seasons = days / SeasonDays;

                return seasons == 1 ? "a season" : $"{seasons} seasons";
            }

            int years = days / YearDays;

            return years == 1 ? "a year" : $"{years} years";
        }

        #endregion
    }
}
