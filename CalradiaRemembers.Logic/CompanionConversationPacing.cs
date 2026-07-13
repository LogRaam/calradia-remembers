// Code written by Gabriel Mailhot, 13/07/2026.
// Sitting down and actually TALKING with a companion should count for something (player request), but it must
// not become a farm: without a cooldown a player could open and close the chat window a dozen times to pin a
// companion at devotion. Being heard is worth something once in a while, not once a minute.

namespace CalradiaRemembers.Logic
{
    /// <summary>Rate-limits the lift a companion takes from being genuinely heard by the player.</summary>
    public static class CompanionConversationPacing
    {
        /// <summary>Days between two conversations that lift a companion's spirits. Tunable.</summary>
        public const int HeardOutCooldownDays = 7;

        /// <summary>A companion never talked to before: any conversation counts.</summary>
        public const int NeverHeard = -1;

        /// <summary>
        ///   True when a conversation today should lift this companion's spirits: they have never been heard, or
        ///   the cooldown since the last time has fully elapsed. A clock that has gone backwards (a save loaded
        ///   into an earlier day) counts as elapsed rather than locking them out forever.
        /// </summary>
        public static bool LiftsSpirits(int lastHeardDay, int today)
        {
            if (lastHeardDay <= NeverHeard) return true;
            if (today < lastHeardDay) return true;

            return today - lastHeardDay >= HeardOutCooldownDays;
        }
    }
}
