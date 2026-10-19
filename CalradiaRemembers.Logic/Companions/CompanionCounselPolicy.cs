// Code written by Gabriel Mailhot, 22/07/2026.
// The PRACTICAL COUNSEL found-topic: a measurable, currently-true condition of the party worth acting on. The
// owner's own example is food monotony (Bannerlord itself grants a real morale bonus for food variety) and the
// hard rule attached to it is absolute: "a companion advising the player to vary their food when they already
// carry eight kinds is worse than silence", so every condition here reads LIVE state, never a cached guess.
// Four conditions compete the same way the five top-level topics do (strongest true one wins, deterministic
// tie-break) because a party could plausibly be low on morale AND overloaded AND food-monotonous at once, and
// only one can headline a single audience.

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>Which practical condition of the party the counsel is about, or none.</summary>
    public enum PartyCounselKind
    {
        None,
        FoodMonotony,
        LowMorale,
        WoundedNoSurgeon,
        Overloaded
    }

    /// <summary>
    ///   Live facts about the party the caller has already gathered (item roster, morale model, member roster,
    ///   carry weight), everything <see cref="CompanionCounselPolicy" /> needs, and nothing it has to guess.
    /// </summary>
    public sealed class PartyCounselFacts
    {
        /// <summary>Distinct kinds of food currently carried (0 if the party carries no food at all, a starvation problem, not a monotony one, and out of scope here).</summary>
        public int DistinctFoodKinds { get; set; }

        /// <summary>The party's current morale (0..100), the same value <see cref="PartyMoraleMood" /> already reads.</summary>
        public float Morale { get; set; }

        /// <summary>Total wounded troop count across the party's member roster.</summary>
        public int WoundedTroops { get; set; }

        /// <summary>True when some hero in the party (the player or a companion) is skilled enough in Medicine to count as a surgeon.</summary>
        public bool HasSurgeon { get; set; }

        /// <summary>The party's current carried weight.</summary>
        public float CarriedWeight { get; set; }

        /// <summary>The party's carry capacity. Non-positive means the fact is unavailable (never flags overloaded).</summary>
        public float CarryCapacity { get; set; }
    }

    /// <summary>Pure rules for which practical counsel (if any) the party's live state actually supports today.</summary>
    public static class CompanionCounselPolicy
    {
        // Fewer distinct kinds than this reads as monotonous. Deliberately modest: Bannerlord's own variety
        // bonus keeps climbing well past this, but the counsel only needs to be TRUE, not maximal, and the
        // hard rule (never advise a well-stocked train) means erring toward a low bar is the safe direction.
        public const int FoodVarietyMonotonyThreshold = 3;

        // Medicine skill at/above which a party member counts as a "surgeon" for this counsel. Reuses the same
        // 0..330 vanilla skill scale the rest of the mod already reads (e.g. CompanionMissions' governing-skill
        // checks); 100 sits at a genuinely trained level, not a dabbler's handful of points.
        public const int SurgeonMedicineThreshold = 100;

        // A wounded party carries weight at/above capacity, vanilla's own overloaded state (the speed
        // penalty the player already feels), read here rather than re-derived.
        private const float OverloadedRatio = 1f;

        private const int FoodMonotonyBaseStrength = 25;
        private const int FoodMonotonyStepPerMissingKind = 15;
        private const int LowMoraleBaseStrength = 30;
        private const int WoundedNoSurgeonBaseStrength = 30;
        private const int WoundedNoSurgeonStrengthCap = 40;
        private const int WoundedNoSurgeonStrengthPerTrooper = 5;
        private const int OverloadedStrength = 45;

        /// <summary>
        ///   The strongest practical counsel the live facts actually support, with its strength (0..100), or
        ///   <see cref="PartyCounselKind.None" /> (strength 0) when nothing qualifies. Reuses
        ///   <see cref="PartyMoraleMood.LowAt" /> as the morale bar so this never disagrees with the mod's own
        ///   existing "sullen camp" threshold.
        /// </summary>
        public static (PartyCounselKind Kind, int Strength) Select(PartyCounselFacts facts)
        {
            if (facts == null) return (PartyCounselKind.None, 0);

            PartyCounselKind bestKind = PartyCounselKind.None;
            var bestStrength = 0;

            // Food monotony: only ever fires while food is actually carried (DistinctFoodKinds > 0), a party
            // with none is a starvation problem the mod must not misdescribe as "vary your diet".
            if (facts.DistinctFoodKinds > 0 && facts.DistinctFoodKinds < FoodVarietyMonotonyThreshold)
            {
                int strength = FoodMonotonyBaseStrength
                               + (FoodVarietyMonotonyThreshold - facts.DistinctFoodKinds) * FoodMonotonyStepPerMissingKind;
                (bestKind, bestStrength) = Consider(PartyCounselKind.FoodMonotony, strength, bestKind, bestStrength);
            }

            if (facts.Morale <= PartyMoraleMood.LowAt)
            {
                int strength = LowMoraleBaseStrength + (int) (PartyMoraleMood.LowAt - facts.Morale);
                (bestKind, bestStrength) = Consider(PartyCounselKind.LowMorale, strength, bestKind, bestStrength);
            }

            if (facts.WoundedTroops > 0 && !facts.HasSurgeon)
            {
                int strength = WoundedNoSurgeonBaseStrength
                               + Min(facts.WoundedTroops * WoundedNoSurgeonStrengthPerTrooper, WoundedNoSurgeonStrengthCap);
                (bestKind, bestStrength) = Consider(PartyCounselKind.WoundedNoSurgeon, strength, bestKind, bestStrength);
            }

            if (facts.CarryCapacity > 0 && facts.CarriedWeight / facts.CarryCapacity >= OverloadedRatio)
                (bestKind, bestStrength) = Consider(PartyCounselKind.Overloaded, OverloadedStrength, bestKind, bestStrength);

            return (bestKind, bestStrength);
        }

        /// <summary>
        ///   Keeps the strongest of (current best, candidate); ties resolve by the LOWER enum ordinal, so the
        ///   same facts always yield the same counsel regardless of the order conditions are checked in.
        /// </summary>
        private static (PartyCounselKind Kind, int Strength) Consider(
            PartyCounselKind candidateKind, int candidateStrength, PartyCounselKind bestKind, int bestStrength)
        {
            if (bestKind == PartyCounselKind.None) return (candidateKind, candidateStrength);
            if (candidateStrength > bestStrength) return (candidateKind, candidateStrength);
            if (candidateStrength == bestStrength && candidateKind < bestKind) return (candidateKind, candidateStrength);

            return (bestKind, bestStrength);
        }

        private static int Min(int a, int b) => a < b ? a : b;
    }
}
