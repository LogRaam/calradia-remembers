// Code written by Gabriel Mailhot, 23/11/2026.
// The love letter: a character who has come to love the player, and never said so, writes to confess it. Gabriel asked
// (23/11/2026) whether a romance could ever begin on the NPC's side, "parfois une confidence, parfois un aveu, parfois un
// flirt"; outside companions (who ask for a word on the road, CompanionSecretAffectionPolicy) no one ever took the first
// step. His answer to the letter proposed: "Une lettre d'aveux ou d'amour serait tres bien", then: "NPC marie devrait
// avoir moins de chances que ca arrive. Pas de relance avec un silence. Il faudrait qu'il y ait une forme d'affinite;
// culture, traits, etc."
// The door is the conversation's own: the register RomanticRegisterPolicy resolves must be Attachment, the very verdict
// that tells the chat "you may say it first", so a letter never confesses what the next conversation forbids
// (yozakura12, 09/09/2026). The weekly chance is the companion confession's curve, weighed by affinity: the game's own
// courtship attraction (its bands, RomanceCampaignBehavior: above 70 "drawn to you", 40 or less "not my type"), a shared
// culture, personality traits held alike or opposed (the courtship's "something in common"), and the desire the mod
// itself tracks. A married character confesses far more rarely.

#region

using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>What decides whether a character may write their love letter at all.</summary>
    public sealed class LoveConfessionFacts
    {
        /// <summary>The register the conversation prompt would give them, resolved by RomanticRegisterPolicy from the same facts.</summary>
        public RomanticRegister Register { get; init; }

        /// <summary>Where the romantic arc stands: a courtship already has its own letters.</summary>
        public RomanticStatus Status { get; init; }

        /// <summary>They are one of the player's companions, who confide on the road instead (even away on a mission).</summary>
        public bool IsPlayerCompanion { get; init; }

        /// <summary>The player has met them: no one confesses love to a stranger.</summary>
        public bool HasMet { get; init; }

        /// <summary>They have written their confession before: it is said once.</summary>
        public bool AlreadyConfessed { get; init; }
    }

    /// <summary>What draws a character to the player, beyond how well they know them.</summary>
    public sealed class LoveAffinityFacts
    {
        /// <summary>The game's own courtship attraction of this character to the player, 0 to 99 (RomanceModel).</summary>
        public int GameAttractionPercent { get; init; }

        /// <summary>They share the player's culture.</summary>
        public bool SharesCulture { get; init; }

        /// <summary>Personality traits (honor, mercy, valor, generosity, calculating) both hold the same way, minus those held opposite: -5 to 5.</summary>
        public int TraitAgreement { get; init; }

        /// <summary>The desire the mod itself tracks (flirts, intimacy, court duels).</summary>
        public int AttractionToPlayer { get; init; }

        /// <summary>They are married to someone else.</summary>
        public bool IsMarried { get; init; }
    }

    /// <summary>Who may write a love letter, and how likely they are to this week. Pure.</summary>
    public static class LoveConfessionLetterPolicy
    {
        /// <summary>The game's courtship band above which a character is "drawn to you" (RomanceCampaignBehavior).</summary>
        public const int DrawnToYouAbove = 70;

        /// <summary>The game's courtship band at or below which "you're not my type" (RomanceCampaignBehavior).</summary>
        public const int NotMyTypeAtOrBelow = 40;

        /// <summary>Affinity, in percent of the base chance, for one drawn to the player. Tuning.</summary>
        public const int DrawnToYouPercent = 150;

        /// <summary>Affinity, in percent of the base chance, for one the player is not the type of. Tuning.</summary>
        public const int NotMyTypePercent = 50;

        /// <summary>What a shared culture adds, in percent. Tuning.</summary>
        public const int SharedCulturePercent = 25;

        /// <summary>What each trait held alike adds (and each held opposite takes), in percent. Tuning.</summary>
        public const int PerTraitPercent = 10;

        /// <summary>What a desire already grown adds, in percent. Tuning.</summary>
        public const int GrownDesirePercent = 25;

        /// <summary>Affinity never falls below this, in percent: a deep regard can still overcome much. Tuning.</summary>
        public const int AffinityFloorPercent = 25;

        /// <summary>A married character confesses at this share of the chance, in percent (Gabriel: "moins de chances"). Tuning.</summary>
        public const int MarriedPercent = 33;

        /// <summary>Whether this character may write their confession at all. Unknown facts refuse.</summary>
        public static bool MayConfess(LoveConfessionFacts facts)
        {
            if (facts == null) return false;
            if (facts.IsPlayerCompanion || !facts.HasMet || facts.AlreadyConfessed) return false;

            return IsStillHeartfelt(facts.Register, facts.Status);
        }

        /// <summary>
        ///   Whether the feeling that wrote the letter still stands: the conversation would still let them say it first,
        ///   and nothing has yet been begun or ended between them. Read at writing and again at delivery.
        /// </summary>
        public static bool IsStillHeartfelt(RomanticRegister register, RomanticStatus status)
            => register == RomanticRegister.Attachment && status is RomanticStatus.None or RomanticStatus.Curious;

        /// <summary>The affinity, in percent of the base chance: attraction band, then culture, traits and desire, then marriage.</summary>
        public static int AffinityPercent(LoveAffinityFacts facts)
        {
            if (facts == null) return 0;

            int band = facts.GameAttractionPercent > DrawnToYouAbove ? DrawnToYouPercent
               : facts.GameAttractionPercent > NotMyTypeAtOrBelow ? 100
               : NotMyTypePercent;

            int kinship = 100 + (facts.SharesCulture ? SharedCulturePercent : 0) + facts.TraitAgreement * PerTraitPercent
                          + (facts.AttractionToPlayer >= RomanticRegisterPolicy.InterestAttractionFloor ? GrownDesirePercent : 0);
            if (kinship < AffinityFloorPercent) kinship = AffinityFloorPercent;

            int affinity = band * kinship / 100;

            return facts.IsMarried ? affinity * MarriedPercent / 100 : affinity;
        }

        /// <summary>
        ///   This week's chance, in tenths of a percent (0 to 1000): the companion confession's own weekly curve from the
        ///   active bar, weighed by affinity, never above that curve's cap.
        /// </summary>
        public static int WeeklyChancePermille(Regard regard, RelationWarmth warmth, LoveAffinityFacts affinity)
        {
            int permille = CompanionSecretAffectionPolicy.WeeklyChancePercent(regard, warmth) * 10 * AffinityPercent(affinity) / 100;
            int cap = CompanionSecretAffectionPolicy.MaxWeeklyChancePercent * 10;

            return permille > cap ? cap : permille;
        }

        /// <summary>
        ///   The one day of the week this character weighs writing: the roll is weekly while the mailbox ticks daily, and a
        ///   daily roll would send the letter seven times as often. Drawn from their id, so it holds across a reload.
        /// </summary>
        public static bool IsTheirDayToWeigh(int today, uint idHash) => (today + idHash % 7) % 7 == 0;
    }
}
