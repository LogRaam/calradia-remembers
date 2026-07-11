// Code written by Gabriel Mailhot, 10/07/2026.
// Received letters now say how old they are ("this letter was sent 3 days ago"), so a note written before
// current events reads as the dated dispatch it is instead of a fresh, incoherent message (player report).

namespace CalradiaRemembers.Logic
{
    /// <summary>Renders how long ago a received letter was written, for the read window's heading.</summary>
    public static class LetterAgeDescriber
    {
        /// <summary>
        ///   "sent today" / "sent yesterday" / "sent N days ago", or null when the writing day is unknown
        ///   (older saves) or nonsensical (in the future).
        /// </summary>
        public static string Describe(int sentOnDay, int today)
        {
            if (sentOnDay <= 0) return null;

            int age = today - sentOnDay;

            if (age < 0) return null;
            if (age == 0) return "sent today";
            if (age == 1) return "sent yesterday";

            return $"sent {age} days ago";
        }
    }
}
