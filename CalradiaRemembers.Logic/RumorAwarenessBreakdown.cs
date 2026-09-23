// Code written by Gabriel Mailhot, 23/09/2026.
// tashmetu (bridge author, 23/09/2026): "Is there any console command or save-file way to check whether a
// specific event I fed has actually propagated to a given NPC ... short of just going and talking to them?"
// Nothing propagates: awareness is recomputed at every prompt. So the honest answer to "has he heard it" is
// this verdict, with its terms, and the day it fades.

#region

using System.Collections.Generic;
using System.Globalization;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   One observer's awareness of one event, with every term that produced it. Built only by
    ///   <see cref="RumorAwareness.Explain" />, so it cannot disagree with the gate the prompt uses.
    /// </summary>
    public sealed class RumorAwarenessBreakdown
    {
        /// <summary>Days since the event.</summary>
        public int AgeDays { get; set; }

        /// <summary>Whether the observer is named among the event's actors.</summary>
        public bool ConcernedParty { get; set; }

        /// <summary>How surely the news is held, from the score.</summary>
        public RumorConfidence Confidence { get; set; }

        /// <summary>Whether the event touches the observer's own faction.</summary>
        public bool FactionMatches { get; set; }

        /// <summary>The proximity bonus from where the event happened (0 when it has no place).</summary>
        public int GeoBonus { get; set; }

        /// <summary>True when the observer has heard of it today.</summary>
        public bool Knows { get; set; }

        /// <summary>The oldest age at which this observer knows it, or -1 when never.</summary>
        public int LastAgeKnown { get; set; }

        /// <summary>The event's magnitude.</summary>
        public int Magnitude { get; set; }

        /// <summary>The awareness score, compared against <see cref="RumorAwareness.Threshold" />.</summary>
        public int Score { get; set; }

        /// <summary>
        ///   A one-paragraph account for a console: the verdict, the arithmetic, and what to change when the
        ///   answer is "never". Written for a modder testing a bridge, so it names the levers they control.
        /// </summary>
        public string Describe()
        {
            float recency = System.Math.Max(0f, 1f - AgeDays / (float) RumorAwareness.RecencyWindowDays);
            string terms = $"magnitude {Magnitude}"
                           + $" + realm {(FactionMatches ? RumorAwareness.FactionBonus : 0)}"
                           + $" + place {GeoBonus}"
                           + $", x recency {recency.ToString("0.00", CultureInfo.InvariantCulture)} (day {AgeDays} of {RumorAwareness.RecencyWindowDays})"
                           + $" + named {(ConcernedParty ? RumorAwareness.ConcernedPartyBonus : 0)}";

            if (Knows)
            {
                int left = LastAgeKnown - AgeDays;

                return $"HEARD, score {Score} ({Confidence}): {terms}. "
                       + (left > 0
                          ? $"Still heard for {left} more day(s)."
                          : "Today is the last day they have it.");
            }

            string verdict = $"NOT HEARD, score {Score} (needs {RumorAwareness.Threshold}): {terms}. ";

            if (LastAgeKnown >= 0)
                return verdict + $"Faded: they had it until it was {LastAgeKnown} day(s) old.";

            var levers = new List<string> {"raise the magnitude"};
            if (!FactionMatches) levers.Add("tag their realm (the kingdom id)");
            if (GeoBonus < RumorAwareness.GeoBonusMax) levers.Add("place it near them");
            if (!ConcernedParty) levers.Add("name them in HeroIds");

            return verdict + $"This character could not hear it even on the day it happened. To reach them: {string.Join(", ", levers)}.";
        }
    }
}
