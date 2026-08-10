// Code written by Gabriel Mailhot, 10/08/2026.
// War-council OFFER trigger (player suggestion): after an army forms or takes a considerable action (capturing a
// settlement, winning a large pitched battle), the mod OFFERS to convene the already-built WarCouncil instead of
// the player typing cr.council war. This pure policy decides WHETHER that offer should surface; the mod's
// WarCouncilOfferBehavior maps live campaign state onto these flags and honours the verdict. Kept engine-free so
// the "in an army", "notable action", and anti-spam cooldown rules are documented by unit tests.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Decides whether the war-council offer should be surfaced after an army action.</summary>
    public static class WarCouncilTriggerPolicy
    {
        /// <summary>
        ///   Days that must elapse after one offer before another may be made. A few days: long enough that a run
        ///   of back-to-back battles or captures does not spam the same offer, short enough that a genuinely fresh
        ///   campaign beat days later still earns one.
        /// </summary>
        public const int DefaultCooldownDays = 3;

        /// <summary>
        ///   True only when the player is in or leads an army, the action was notable, and either no offer has ever
        ///   been made (<paramref name="daysSinceLastOffer" /> null) or at least <paramref name="cooldownDays" />
        ///   days have passed since the last one. Every other case returns false, so the offer is refused when the
        ///   player commands no army, when the action was routine, or while the cooldown is still running.
        /// </summary>
        public static bool ShouldOffer(bool playerInOrLeadsArmy, bool actionIsNotable, int? daysSinceLastOffer, int cooldownDays)
        {
            if (!playerInOrLeadsArmy) return false;
            if (!actionIsNotable) return false;
            if (daysSinceLastOffer == null) return true;

            return daysSinceLastOffer.Value >= cooldownDays;
        }
    }
}
