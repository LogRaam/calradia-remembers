// Code written by Gabriel Mailhot, 20/08/2026.
// The SCHEME WARNING found-topic: a loyal companion seeks the player out, of their own accord, to WARN them
// that someone is secretly plotting against them (SchemeStore's own ledger is the single source of truth for
// "is anyone actually plotting against the player right now"). This decides whether the companion is a
// plausible, loyal messenger for that warning: a disaffected companion would not go out of their way to
// protect the player, so the SAME Content/Devoted happiness gate CompanionGratitudePolicy uses is reused here
// (CompanionHappiness.Band's own threshold), so this can never disagree with what the encyclopedia/prompt
// already tell the player about this companion's mood. Unlike Gratitude, there is no magnitude to scale a
// strength from: a live plot against the player is inherently high-value and urgent (design brief), so the
// strength is a flat constant, pinned well above Gratitude's typical strength (CompanionGratitudePolicyTests'
// own "content, fresh, significant" example) so a real threat to the player always outranks a mere thank-you
// or rumour when several found-topics compete for the same audience.

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>Pure rules for whether a companion qualifies to voice an unprompted scheme-warning audience.</summary>
    public static class CompanionSchemeWarningPolicy
    {
        // High-value and urgent by design (a live plot against the player), so this sits comfortably above
        // MissionFollowUp's flat strength (55, CompanionAudienceTopicResolver) and above Gratitude's own typical
        // strength, without pinning itself to the absolute 0..100 ceiling (room is left for a still-graver future
        // found-topic to outrank it).
        public const int Strength = 90;

        /// <summary>
        ///   True when the companion is CONTENT enough in the player's service to risk carrying this warning.
        ///   Eligibility of the scheme itself (does one exist, is it still warnable, has it already been spent)
        ///   is the caller's concern, exactly like Gratitude's own live-appreciation checks.
        /// </summary>
        public static bool Holds(int happinessScore) => IsContentEnough(happinessScore);

        /// <summary>
        ///   Reuses CompanionHappiness's own Content/Devoted bands (the same wording the encyclopedia and the
        ///   prompt already show the player), so a disaffected companion can never be voiced as going out of
        ///   their way to protect a player they are not, right now, content in the service of.
        /// </summary>
        public static bool IsContentEnough(int happinessScore)
            => CompanionHappiness.Band(happinessScore) is HappinessBand.Content or HappinessBand.Devoted;
    }
}
