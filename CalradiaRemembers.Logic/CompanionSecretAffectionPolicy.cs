// Code written by Gabriel Mailhot, 10/07/2026.
// A faithful, content companion who has served the player through war may, once the comrade bond runs deep enough,
// ask for a private audience to confess genuine affection and become the player's SECRET LOVER (kept discreet to
// avoid jealousy). This is the pure decision only: whether a companion could ever confide, and whether the weekly
// roll fires this week. Spawning the audience, the popup, and any refusal cooldown bookkeeping are engine glue.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for a companion's secret affection confession (eligibility plus the weekly roll).</summary>
    public static class CompanionSecretAffectionPolicy
    {
        /// <summary>
        ///   Minimum regard at the GROUNDED pacing: the level the mod is balanced around, unchanged since this
        ///   shipped. See <see cref="MinRegardFor" /> for why the other two settings sit lower.
        /// </summary>
        public const int MinRegard = 50;

        /// <summary>
        ///   The bond a companion needs before they would ever confide, under the player's chosen Relationship
        ///   Pacing. Grounded keeps 50 exactly, so nobody's campaign shifts under them.
        ///   <para>
        ///     Gabriel's ruling, 2026-09-08. Two players reported their travelling companions felt inert, and the
        ///     romance they might eventually have offered sat behind a regard of 50, which is very far when
        ///     ordinary gains are capped at +1 per eight game-hours. Rather than add a dial nobody would know how
        ///     to set, the threshold now follows the one the player has already answered: Relationship Pacing
        ///     describes itself as a philosophy of how readily bonds deepen, and when a companion dares to speak
        ///     is exactly that. A Storyteller player meets the confession at a human distance; a Grounded one
        ///     keeps it rare and earned.
        ///   </para>
        /// </summary>
        public static int MinRegardFor(RelationWarmth warmth)
        {
            switch (warmth)
            {
                case RelationWarmth.VeryEasy: return 28; // Storyteller: still a real bond, not a passing acquaintance
                case RelationWarmth.Easier:   return 38; // Warm
                default:                      return MinRegard; // Grounded, the shipped balance
            }
        }

        /// <summary>Cap on the weekly chance a fully-bonded companion finally confides.</summary>
        public const int MaxWeeklyChancePercent = 40;

        /// <summary>After the player turns the audience popup away, do not ask again for this many days.</summary>
        public const int RefusalCooldownDays = 60;

        /// <summary>
        ///   Whether a companion could EVER confide (all must hold): they are a companion, orientation-compatible
        ///   with the player, adult content is enabled, they are content (not unhappy), they are not already
        ///   attached to the player (spouse/consort/secret-lover/committed), and the bond is deep enough.
        /// </summary>
        public static bool IsEligible(bool isCompanion, bool orientationCompatible, bool adultEnabled,
                                      bool isContent, bool alreadyAttached, int regard,
                                      RelationWarmth warmth = RelationWarmth.Normal)
        {
            // Everything this shares with the bond itself is asked of the ONE rule, so a companion can never
            // confide a bond the game would then refuse to form. Only contentment is judged here, because it
            // belongs to the confession alone: an unhappy companion does not open their heart, but unhappiness
            // is no reason the bond could not exist if they did.
            if (!isContent) return false;

            return SecretBondEligibility.IsEligible(new SecretBondFacts {
                AdultContentEnabled = adultEnabled,
                IsPlayerCompanion = isCompanion,
                HasRomanticProfile = true, // the caller has already read a romantic profile to get here
                OrientationCompatible = orientationCompatible,
                AlreadyBoundToPlayer = alreadyAttached,
                Status = NpcMemoryService.Core.Models.RomanticStatus.None, // "attached" above already carries it
                Regard = regard,
                Warmth = warmth
            });
        }

        /// <summary>
        ///   Given eligibility, the weekly chance (percent) this companion finally confides, scaling with how far
        ///   the bond runs beyond MinRegard: clamp((regard - MinRegard) * 2 + 5, 5, MaxWeeklyChancePercent).
        /// </summary>
        public static int WeeklyChancePercent(int regard, RelationWarmth warmth = RelationWarmth.Normal)
        {
            // Measured from the ACTIVE threshold, not the Grounded one, so the curve keeps its shape at every
            // pacing: a companion who has just cleared the bar confides rarely, and grows bolder from there.
            int raw = (regard - MinRegardFor(warmth)) * 2 + 5;
            if (raw < 5) return 5;
            if (raw > MaxWeeklyChancePercent) return MaxWeeklyChancePercent;
            return raw;
        }

        /// <summary>True when a 0..99 roll lands within this week's chance.</summary>
        public static bool ShouldConfideThisWeek(int regard, int roll, RelationWarmth warmth = RelationWarmth.Normal)
            => roll >= 0 && roll < WeeklyChancePercent(regard, warmth);
    }
}
