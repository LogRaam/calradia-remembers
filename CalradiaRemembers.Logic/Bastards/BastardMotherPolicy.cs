// Code written by Gabriel Mailhot, 26/06/2026.
// Bastards (design layer) — not every mother of a bastard exploits the player. Her DISPOSITION toward him is
// drawn from her traits and her regard: a scheming, dishonourable woman blackmails for silence; a fond one
// longs for him (come see the child, marry me); a wronged one writes coldly; most simply ask for support.
// This variety is what gives the world personality. Pure so the gate — and its tuning — stays tested.

namespace CalradiaRemembers.Logic.Bastards
{
    /// <summary>How the mother of a hidden bastard relates to the player — drives the letter she writes.</summary>
    public enum BastardMotherDisposition
    {
        /// <summary>Scheming and dishonourable — she extorts the player for her silence (the blackmail thread).</summary>
        Mercenary,

        /// <summary>Fond of the player — she misses him, wants him to meet the child, wishes he would wed her.</summary>
        Longing,

        /// <summary>Cold toward the player — she feels wronged or ignored, and writes with reproach.</summary>
        Resentful,

        /// <summary>Neither scheming nor attached — she simply informs him and asks for the child's keep.</summary>
        Pragmatic
    }

    /// <summary>
    ///   Resolves a bastard mother's disposition from her honour/calculating traits and her regard for the
    ///   player. Only a <see cref="BastardMotherDisposition.Mercenary" /> mother blackmails; the rest write
    ///   warmer letters and never leak the secret themselves.
    /// </summary>
    public static class BastardMotherPolicy
    {
        /// <summary>How far calculating must outweigh honour before she would exploit the secret. Tuning.</summary>
        public const int MercenaryThreshold = 2;

        /// <summary>Regard at/above which she is fond of the player (a friend). Tuning.</summary>
        public const int FondRelation = 10;

        /// <summary>Regard at/below which she feels coldly wronged. Tuning.</summary>
        public const int ColdRelation = -10;

        /// <summary>
        ///   Her two traits and how she feels about the player. THREE INTS on two entirely different scales:
        ///   two traits at roughly -2..+2 and a regard at -100..+100. Swap a trait with the regard and a fond
        ///   mother reads as a schemer, which is exactly the reported bug this rule was written to end.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class MotherDispositionFacts
        {
           /// <summary>Her Honor trait. Weighed only against Calculating, as their difference.</summary>
           public int Honor { get; set; }

           /// <summary>Her Calculating trait. Cunning well above honour inclines her to extort.</summary>
           public int Calculating { get; set; }

           /// <summary>
           ///   Her regard for the player, checked FIRST: a mother who is fond of him does not extort him whatever
           ///   her cunning. Warmth then sets the register, fond to longing and cold to resentful.
           /// </summary>
           public int RelationWithPlayer { get; set; }
        }

        /// <summary>
        ///   Her disposition. A scheming, dishonourable woman inclines toward blackmail, BUT ONLY when her
        ///   regard is not already warm: a mother who is fond of the player (and, in the reported case, has
        ///   his +50 regard and an agreement to wed) does not extort him whatever her cunning. So fondness is
        ///   checked first and rules out the mercenary path; then warmth sets the register: fond → longing,
        ///   cold → resentful, otherwise pragmatic.
        /// </summary>
        public static BastardMotherDisposition Resolve(MotherDispositionFacts facts)
        {
           facts ??= new MotherDispositionFacts();

           int honor = facts.Honor;
           int calculating = facts.Calculating;
           int relationWithPlayer = facts.RelationWithPlayer;

            if (relationWithPlayer >= FondRelation) return BastardMotherDisposition.Longing;
            if (calculating - honor >= MercenaryThreshold) return BastardMotherDisposition.Mercenary;
            if (relationWithPlayer <= ColdRelation) return BastardMotherDisposition.Resentful;

            return BastardMotherDisposition.Pragmatic;
        }
    }
}
