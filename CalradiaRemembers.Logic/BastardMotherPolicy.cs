// Code written by Gabriel Mailhot, 26/06/2026.
// Bastards (design layer) — not every mother of a bastard exploits the player. Her DISPOSITION toward him is
// drawn from her traits and her regard: a scheming, dishonourable woman blackmails for silence; a fond one
// longs for him (come see the child, marry me); a wronged one writes coldly; most simply ask for support.
// This variety is what gives the world personality. Pure so the gate — and its tuning — stays tested.

namespace CalradiaRemembers.Logic
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
        ///   Her disposition. Traits decide first — a scheming, dishonourable woman exploits the secret
        ///   whatever her regard — then warmth: fond → longing, cold → resentful, otherwise pragmatic.
        /// </summary>
        public static BastardMotherDisposition Resolve(int honor, int calculating, int relationWithPlayer)
        {
            if (calculating - honor >= MercenaryThreshold) return BastardMotherDisposition.Mercenary;
            if (relationWithPlayer >= FondRelation) return BastardMotherDisposition.Longing;
            if (relationWithPlayer <= ColdRelation) return BastardMotherDisposition.Resentful;

            return BastardMotherDisposition.Pragmatic;
        }
    }
}
