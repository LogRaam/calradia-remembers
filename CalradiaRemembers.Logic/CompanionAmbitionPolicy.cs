// Code written by Gabriel Mailhot, 20/08/2026.
// The AMBITION found-topic (design brief, mirrors the Gratitude/SchemeWarning pattern): a content, ambitious
// companion seeks a private word to voice ONE concrete ambition of their own. Bound to EXACTLY FOUR ambitions,
// each already resolved by an existing action verb (dispatch_mission, assign_party_role, appoint_governor,
// arrange_marriage) once the player grants it in conversation, so there is no new executor here, only the
// SELECTION of which ambition (if any) fits today. The happiness gate exists because an ambitious demand from a
// miserable companion reads as a grievance, not an ambition (Grievance already covers that path); the
// per-companion cooldown exists so a refused ambition does not nag the player every time they pass through a
// settlement, distinct from the shared GLOBAL cooldown (CompanionAudienceTopicPolicy.GlobalCooldownDays) that
// already throttles every found-topic uniformly across the whole roster.

namespace CalradiaRemembers.Logic
{
    /// <summary>Which of the four ambitions the companion would voice, or none.</summary>
    public enum CompanionAmbition
    {
        None,
        Command,
        Post,
        Fief,
        Marriage
    }

    /// <summary>
    ///   Live facts about the companion the caller (CompanionAudienceTopicResolver) has already gathered from
    ///   engine state, everything <see cref="CompanionAmbitionPolicy" /> needs, and nothing it has to guess.
    ///   <see cref="IsLandless" /> and <see cref="HoldsPartyRole" /> each fold in one extra piece of engine
    ///   availability the pure policy has no other slot for (see the resolver's own remarks on each): a
    ///   companion only reads as truly "landless" for THIS ambition when a fief could actually be granted to
    ///   them (the player still leads their clan and holds a vacant governable fief), and a companion only
    ///   reads as free for the party-role ambition when some role actually fits their skills, not merely when
    ///   the four seats happen to be unheld.
    /// </summary>
    public sealed class CompanionAmbitionFacts
    {
        /// <summary>True when the companion already leads a field party of their own, or is currently away on a dispatched errand.</summary>
        public bool HasFieldPartyOfOwn { get; set; }

        /// <summary>True when the companion already holds one of the four party roles, OR no role currently fits their skills (see the class remarks).</summary>
        public bool HoldsPartyRole { get; set; }

        /// <summary>True when the companion holds no post or party leadership of their own AND a fief could actually be granted to them right now (see the class remarks).</summary>
        public bool IsLandless { get; set; }

        /// <summary>True when the companion's own standing toward the player clears the bar a fief-worthy companion needs.</summary>
        public bool IsHighStanding { get; set; }

        /// <summary>True when the companion is unwed, of age, and at least one plausible match was found for them.</summary>
        public bool IsUnmarriedWithMatch { get; set; }

        /// <summary>The companion's current contentment score (CompanionHappiness scale).</summary>
        public int Happiness { get; set; }

        /// <summary>Days since this companion's Ambition topic last asked (a large value when never asked).</summary>
        public int DaysSinceLastAsked { get; set; }
    }

    /// <summary>Pure rules for which ambition (if any) a companion's live facts support voicing today.</summary>
    public static class CompanionAmbitionPolicy
    {
        // A refused ambition should not resurface every time the shared global cooldown clears (14 days):
        // the same underlying condition (landless+high-standing, no party role, etc.) can easily still be true
        // weeks later, and re-asking that soon would read as nagging rather than a fresh beat in the story. A
        // full season gives the story real room before the SAME companion raises an ambition again.
        public const int CooldownDays = 30;

        // Flat per-ambition strengths (no magnitude to scale from, exactly like CompanionSchemeWarningPolicy's
        // own flat constant): ranked by how consequential the ask is, so a rarer, weightier opportunity (a fief)
        // outranks a lighter one (an errand command) when several ambitions qualify on the same companion. All
        // sit below CompanionSchemeWarningPolicy.Strength (90): a companion's own hope to rise is real, but it
        // is never as urgent as a live plot against the player.
        public const int FiefStrength = 60;
        public const int MarriageStrength = 55;
        public const int PostStrength = 45;
        public const int CommandStrength = 35;

        /// <summary>
        ///   The strongest ambition the live facts actually support, with its strength (0..100), or
        ///   <see cref="CompanionAmbition.None" /> (strength 0) when the happiness gate, the cooldown, or every
        ///   individual ambition's own condition fails. Reuses <see cref="CompanionHappiness.Band" />'s own
        ///   Content/Devoted bands, the same gate CompanionGratitudePolicy/CompanionSchemeWarningPolicy already
        ///   use, so this can never disagree with the encyclopedia/prompt wording the player already sees.
        /// </summary>
        public static (CompanionAmbition Ambition, int Strength) Select(CompanionAmbitionFacts facts)
        {
            if (facts == null) return (CompanionAmbition.None, 0);
            if (!IsContentEnough(facts.Happiness)) return (CompanionAmbition.None, 0);
            if (facts.DaysSinceLastAsked < CooldownDays) return (CompanionAmbition.None, 0);

            CompanionAmbition bestKind = CompanionAmbition.None;
            var bestStrength = 0;

            if (facts.IsLandless && facts.IsHighStanding)
                (bestKind, bestStrength) = Consider(CompanionAmbition.Fief, FiefStrength, bestKind, bestStrength);

            if (facts.IsUnmarriedWithMatch)
                (bestKind, bestStrength) = Consider(CompanionAmbition.Marriage, MarriageStrength, bestKind, bestStrength);

            if (!facts.HoldsPartyRole)
                (bestKind, bestStrength) = Consider(CompanionAmbition.Post, PostStrength, bestKind, bestStrength);

            if (!facts.HasFieldPartyOfOwn)
                (bestKind, bestStrength) = Consider(CompanionAmbition.Command, CommandStrength, bestKind, bestStrength);

            return (bestKind, bestStrength);
        }

        /// <summary>
        ///   Reuses CompanionHappiness's own Content/Devoted bands (the same rule CompanionGratitudePolicy and
        ///   CompanionSchemeWarningPolicy already follow): an ambitious demand from a companion who is not at
        ///   least Content reads as a grievance, not an ambition (design brief).
        /// </summary>
        public static bool IsContentEnough(int happinessScore)
            => CompanionHappiness.Band(happinessScore) is HappinessBand.Content or HappinessBand.Devoted;

        /// <summary>
        ///   Keeps the strongest of (current best, candidate); ties resolve by the LOWER enum ordinal, so the
        ///   same facts always yield the same ambition regardless of the order conditions are checked in
        ///   (mirrors CompanionCounselPolicy's own Consider tie-break).
        /// </summary>
        private static (CompanionAmbition Kind, int Strength) Consider(
            CompanionAmbition candidateKind, int candidateStrength, CompanionAmbition bestKind, int bestStrength)
        {
            if (bestKind == CompanionAmbition.None) return (candidateKind, candidateStrength);
            if (candidateStrength > bestStrength) return (candidateKind, candidateStrength);
            if (candidateStrength == bestStrength && candidateKind < bestKind) return (candidateKind, candidateStrength);

            return (bestKind, bestStrength);
        }
    }
}
