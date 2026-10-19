// Code written by Gabriel Mailhot, 08/09/2026.
// Player report (Nexus, 2026-09-08): "unless I remember to routinely engage with my travel buddies, they never
// say anything on their own." The found-topic companion audience (CompanionAudienceTopicBehavior) only ever
// offered on SettlementEntered, so a player who travels for long stretches without visiting a town never sees
// it at all. Gabriel's ruling: add a road trigger alongside the settlement one, and make the whole thing
// player-tunable, mirroring InterceptionPacingRules (the identical idea for lords approaching on the road).
//
// This is NOT a re-use of InterceptionPacingRules itself: that dial's "Occasionally" resolves to a 30-day
// per-lord cooldown, but the shipped companion audience has always run on a 14-day GLOBAL cooldown
// (CompanionAudienceTopicPolicy.GlobalCooldownDays). Reusing the lord numbers outright would quietly double
// the wait an existing player already knows, which fails the "default reproduces today's feel" requirement.
// So Occasionally keeps the shipped 14 days, and the other settings scale from there. The daily ROAD chance is
// a genuinely NEW number (there was no road trigger before), chosen to feel like a plausible occasional aside
// while travelling rather than a per-lord "does he ride out today" roll: see DailyChancePercent's own remarks.
namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>How readily a travelling companion asks for a private word, on the road as well as in a settlement. The player's taste, not a difficulty.</summary>
    public enum CompanionAudiencePacing
    {
        /// <summary>Never. No companion ever asks, on the road or entering a settlement; the found-topic audience is fully off.</summary>
        Never,

        /// <summary>A rare thing, remembered when it happens.</summary>
        Rarely,

        /// <summary>The shipped feel (settlement-only, 14-day global cooldown) plus a modest road chance. The default.</summary>
        Occasionally,

        /// <summary>Companions speak up readily, on the road and in town alike.</summary>
        Often
    }

    /// <summary>Turns the player's chosen pacing into the shared cooldown and the road trigger's daily chance.</summary>
    public static class CompanionAudiencePacingRules
    {
        /// <summary>
        ///   The shipped global cooldown (CompanionAudienceTopicPolicy.GlobalCooldownDays), and the value
        ///   Occasionally must keep returning so an existing player's pacing never quietly changes.
        /// </summary>
        public const int DefaultCooldownDays = CompanionAudienceTopicPolicy.GlobalCooldownDays;

        /// <summary>
        ///   Days before the shared roster-wide clock resets, scaled by the SAME dial as the road chance (one
        ///   feeling, not two independent levers): asking for more company should not then starve the player
        ///   with a long silence once the popup finally lands, and asking for less should not still nag with a
        ///   quick reset.
        /// </summary>
        public static int CooldownDays(CompanionAudiencePacing pacing)
        {
            switch (pacing)
            {
                case CompanionAudiencePacing.Never:  return DefaultCooldownDays; // moot: nobody ever asks
                case CompanionAudiencePacing.Rarely: return 21;
                case CompanionAudiencePacing.Often:  return 7;
                default:                             return DefaultCooldownDays; // Occasionally
            }
        }

        /// <summary>
        ///   The chance, per day of travel, that a companion asks for a word on the ROAD (never on top of an
        ///   entry into a settlement, which is checked separately). Zero at
        ///   <see cref="CompanionAudiencePacing.Never" />, which switches the whole feature off, road and
        ///   settlement alike, rather than merely making it rare. Occasionally's 20% is deliberately gentler
        ///   than InterceptionPacingRules' 25% for the identical setting name: a lord riding out is a rare,
        ///   memorable event on its own, but a companion is asked EVERY day regardless (this check runs
        ///   whether or not a settlement was also entered that day), so a matching percentage would in
        ///   practice ask far more often across a long journey.
        /// </summary>
        public static int DailyChancePercent(CompanionAudiencePacing pacing)
        {
            switch (pacing)
            {
                case CompanionAudiencePacing.Never:  return 0;
                case CompanionAudiencePacing.Rarely: return 8;
                case CompanionAudiencePacing.Often:  return 35;
                default:                             return 20; // Occasionally
            }
        }

        /// <summary>True when the player has switched the whole thing off, so the behaviour can skip its work entirely (road AND settlement).</summary>
        public static bool IsOff(CompanionAudiencePacing pacing) => pacing == CompanionAudiencePacing.Never;
    }
}
