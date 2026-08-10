// Code written by Gabriel Mailhot, 10/08/2026.
// Open relationship (Inc 3b-a): once a committed open / harem partner has taken a lover (Inc 3a), she MEETS him
// when the player is away. This pure policy owns the one decision the mod-side weekly pass must not fork:
// WHETHER a tryst happens this week, which is true only when she has a lover, the player is not beside her (so
// the meeting is unobserved), and a modest weekly roll lands. The engine facts (has a lover, is the player
// beside her, the random roll) are supplied by the host; the RULE and its one tuning knob live here, named and
// tested. A tryst nudges the wife-to-lover bond, increments a persisted counter, and feeds both the secret-
// affair discovery curve (AffairDiscoveryPolicy) and the later pregnancy-by-the-lover hook (Inc 3c).

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure rule for a taken lover being met when the player is away: the modest weekly chance a tryst
    ///   happens. Consumed by the mod's weekly open-partner pass; the host supplies the engine facts, this
    ///   holds the decision.
    /// </summary>
    public static class TrystPolicy
    {
        /// <summary>The modest weekly chance that an away-from-player partner meets her lover.</summary>
        public const int ChancePercent = 50;

        /// <summary>
        ///   Whether a partner meets her lover this week. True only when she HAS a lover
        ///   (<paramref name="hasLover" />), the player is AWAY (<paramref name="playerIsAway" />, so the
        ///   meeting is unobserved), and <paramref name="rollPercent" /> (0..99) falls under
        ///   <see cref="ChancePercent" />. The host rolls the percent (via MBRandom) so the decision stays pure.
        /// </summary>
        public static bool ShouldMeet(bool hasLover, bool playerIsAway, int rollPercent)
        {
            if (!hasLover) return false;
            if (!playerIsAway) return false;
            return rollPercent < ChancePercent;
        }
    }
}
