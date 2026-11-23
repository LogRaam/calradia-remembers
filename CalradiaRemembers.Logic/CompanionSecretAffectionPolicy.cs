// Code written by Gabriel Mailhot, 10/07/2026.
// A faithful, content companion who has served the player through war may, once the comrade bond runs deep enough,
// ask for a private audience to confess genuine affection and become the player's SECRET LOVER (kept discreet to
// avoid jealousy). This is the pure decision only: whether a companion could ever confide, and whether the weekly
// roll fires this week. Spawning the audience, the popup, and any refusal cooldown bookkeeping are engine glue.

using NpcMemoryService.Core.Standing;

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for a companion's secret affection confession (eligibility plus the weekly roll).</summary>
    public static class CompanionSecretAffectionPolicy
    {
        /// <summary>
        ///   Minimum regard at the GROUNDED pacing: the level the mod is balanced around, unchanged since this
        ///   shipped. See <see cref="MinRegardFor" /> for why the other two settings sit lower.
        /// </summary>
        public const int MinRegard = Regard.DevotedTier;

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
        /// <summary>
        ///   What a companion's confession rests on, read off the campaign by the caller. Named rather than
        ///   positional (2026-09-10): this took FIVE booleans in a row, and cr.test all called it as
        ///   IsEligible(true, true, true, true, false, bar, warmth), six bare values with nothing to say which
        ///   was which, in the very test written to prove this bar had stopped drifting.
        /// </summary>
        public sealed class SecretAffectionFacts
        {
            /// <summary>They are one of the player's own companions.</summary>
            public bool IsCompanion { get; set; }

            /// <summary>Their orientation admits the player.</summary>
            public bool OrientationCompatible { get; set; }

            /// <summary>The campaign's Adult Content level admits the romantic arc at all.</summary>
            public bool AdultEnabled { get; set; }

            /// <summary>
            ///   They are content. This one belongs to the CONFESSION alone rather than to the bond: an unhappy
            ///   companion does not open their heart, but unhappiness is no reason the bond could not exist.
            /// </summary>
            public bool IsContent { get; set; }

            /// <summary>They are already bound to the player, so there is nothing left to confess.</summary>
            public bool AlreadyAttached { get; set; }

            /// <summary>The mod's own personal regard ledger, never the vanilla clan relation.</summary>
            public int Regard { get; set; }

            /// <summary>The player's Relationship Pacing, which sets how deep the bond must run first.</summary>
            public RelationWarmth Warmth { get; set; } = RelationWarmth.Normal;
        }

        /// <summary>Whether this companion may confide their feelings. Unknown facts refuse.</summary>
        public static bool IsEligible(SecretAffectionFacts facts)
        {
            if (facts == null) return false;

            bool isCompanion = facts.IsCompanion;
            bool orientationCompatible = facts.OrientationCompatible;
            bool adultEnabled = facts.AdultEnabled;
            bool isContent = facts.IsContent;
            bool alreadyAttached = facts.AlreadyAttached;
            int regard = facts.Regard;
            RelationWarmth warmth = facts.Warmth;

            // Everything this shares with the bond itself is asked of the ONE rule, so a companion can never
            // confide a bond the game would then refuse to form. Only contentment is judged here, because it
            // belongs to the confession alone: an unhappy companion does not open their heart, but unhappiness
            // is no reason the bond could not exist if they did.
            if (!isContent) return false;

            // The confession is a companion's audience (they ask for a word on the road). The bond itself is open to
            // anyone since 22/11/2026 (Gabriel: "il ne devrait pas y avoir de restrictions"), so this door is the
            // confession's own now.
            if (!isCompanion) return false;

            return SecretBondEligibility.IsEligible(new SecretBondFacts {
                AdultContentEnabled = adultEnabled,
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
        public static int WeeklyChancePercent(Regard regard, RelationWarmth warmth = RelationWarmth.Normal)
        {
            // Measured from the ACTIVE threshold, not the Grounded one, so the curve keeps its shape at every
            // pacing: a companion who has just cleared the bar confides rarely, and grows bolder from there.
            int raw = (regard.Points - MinRegardFor(warmth)) * 2 + 5;
            if (raw < 5) return 5;
            if (raw > MaxWeeklyChancePercent) return MaxWeeklyChancePercent;
            return raw;
        }

        /// <summary>True when a 0..99 roll lands within this week's chance.</summary>
        public static bool ShouldConfideThisWeek(Regard regard, int roll, RelationWarmth warmth = RelationWarmth.Normal)
            => roll >= 0 && roll < WeeklyChancePercent(regard, warmth);
    }
}
