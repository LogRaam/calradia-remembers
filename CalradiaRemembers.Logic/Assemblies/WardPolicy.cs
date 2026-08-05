// Code written by Gabriel Mailhot, 05/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "sponsor_ward", Kimi's own
// MINIMAL v1 recipe, adopted per this task's own scope note): the pure tuning behind the mentor-protege bond.
// COUNCIL_ACTIONS.md itself names no exact age band or XP figure (unlike, say, HostagePolicy.TermDays, which
// inherits Kimi's own 20-90 day range) - both constants below are this build's own author's design call,
// documented here rather than left as a silent magic number.
//
// Age band (MinAge/MaxAge): a "young companion" (this task's own wording) needs SOME adult floor - vanilla has
// no youth/companion age flag, so this mirrors MarriageEligibility.MinMarriageAge's own floor (18, the vanilla
// adult threshold) rather than inventing a second one. The ceiling (26) is this build's own choice to keep
// "young" meaningfully narrower than the general adult companion population (a grizzled 50-year-old sellsword
// is not a "young protege" by any reading of Kimi's own framing), without being so narrow the pool is almost
// always empty.
//
// Daily XP (DailyXpPerSkill): deliberately far below CaptiveEscapeCoordinator's own ONE-TIME GrantEscapeXp
// amounts (15 XP for a real feat, RogueryXpOnFreedom/AthleticsXpOnFled) - a mentorship bond has NO term (see
// CouncilWard's own header) and could in principle run for the rest of a campaign, so a flat 2 XP/day/skill is a
// slow ambient trickle, never a farmable burst; over even a full season (~20 game-days) that is 40 XP per skill,
// a fraction of a single vanilla training exercise.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for sponsor_ward: the "young companion" age band and the daily skill-XP trickle.</summary>
    public static class WardPolicy
    {
        /// <summary>The vanilla adult floor (mirrors MarriageEligibility.MinMarriageAge): a ward is never a minor.</summary>
        public const float MinAge = 18f;

        /// <summary>This build's own ceiling for "young": a design call, not a vanilla rule, see this file's own header.</summary>
        public const float MaxAge = 26f;

        /// <summary>The flat daily skill-XP trickle granted to the protege in EACH of the two taught skills while co-located with the mentor (see CouncilWardBehavior.GrantMentorshipXp).</summary>
        public const float DailyXpPerSkill = 2f;

        /// <summary>True when <paramref name="age" /> falls within ["MinAge", "MaxAge"]: sponsor_ward's own "young companion" gate.</summary>
        public static bool IsWithinAgeBand(float age) => age >= MinAge && age <= MaxAge;
    }
}
