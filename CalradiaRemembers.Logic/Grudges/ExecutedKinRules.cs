// Code written by Gabriel Mailhot, 06/07/2026.
// Grudges pillar, Tier 3 source rule (ROADMAP "the vendetta end of the spectrum"): the kin of a lord
// the PLAYER executed. GrudgePolicy.RigidityAt already fixes GrudgeSource.ExecutedKin to
// VendettaRigidity regardless of the target's regard or the holder's traits, so this rule only ever
// decides Forms/Magnitude/KnownToPlayer, exactly like every Tier2GrudgeRules sibling. No formation
// gate beyond recognizing the kinship: an execution is always public and always wounds a close kin,
// there is no benign case the way CaptiveAbuse's Severity 0 is benign.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   How a kin is bound to the executed lord. Only close, living family ever reaches
    ///   <see cref="ExecutedKinRules.ExecutedKinGrudge" /> in the first place (the engine-side plant
    ///   site enumerates spouse/parents/children/siblings only, never the wider clan or faction).
    /// </summary>
    public enum ExecutedKinKind
    {
        /// <summary>A living child of the executed lord.</summary>
        Child = 0,

        /// <summary>A living sibling of the executed lord.</summary>
        Sibling = 1,

        /// <summary>A living parent (father or mother) of the executed lord.</summary>
        Parent = 2,

        /// <summary>The executed lord's living spouse.</summary>
        Spouse = 3
    }

    /// <summary>Facts for <see cref="ExecutedKinRules.ExecutedKinGrudge" />: one kin's tie to a lord the player just executed.</summary>
    public sealed class ExecutedKinFacts
    {
        /// <summary>How this kin is bound to the executed lord.</summary>
        public ExecutedKinKind Kinship { get; set; }

        /// <summary>
        ///   The kin's regard for the player at the moment of the execution. Mirrors
        ///   <see cref="DeliveredPrisonerFacts.CaptiveRegardForPlayer" />'s low-regard-nurses-harder
        ///   framing: a kin who already resented the player takes the killing as confirmation, not a
        ///   surprise, and nurses the vendetta a touch harder from day one.
        /// </summary>
        public int KinRegardForPlayer { get; set; }
    }

    /// <summary>
    ///   Pure Tier 3 grudge SOURCE rule: the close living kin of a lord the player executed. Always
    ///   forms, always known to the player (an execution is a public act). Magnitude sets how deep the
    ///   wound starts, heaviest for the tightest bonds (spouse, child); rigidity itself is not this
    ///   rule's job (<see cref="GrudgePolicy.RigidityAt" /> fixes <see cref="GrudgeSource.ExecutedKin" />
    ///   to <see cref="GrudgePolicy.VendettaRigidity" /> unconditionally, so even the lightest magnitude
    ///   this rule can propose is still an unforgivable vendetta).
    /// </summary>
    public static class ExecutedKinRules
    {
        /// <summary>Tuning: magnitude for the tightest bonds, a spouse or a child of the executed lord.</summary>
        public const int ChildOrSpouseMagnitude = 90;

        /// <summary>Tuning: magnitude for a parent or a sibling, still severe but a step lighter than the tightest bonds.</summary>
        public const int SiblingOrParentMagnitude = 75;

        /// <summary>
        ///   Tuning: the magnitude bump applied when <see cref="ExecutedKinFacts.KinRegardForPlayer" /> is
        ///   at/below <see cref="LowRegardThreshold" /> (mirrors
        ///   <see cref="Tier2GrudgeRules.DeliveredPrisonerLowRegardBonus" />).
        /// </summary>
        public const int LowRegardBonus = 10;

        /// <summary>
        ///   Tuning: at/below this prior regard for the player, the kin already disliked them, so the
        ///   killing is nursed HARDER (mirrors <see cref="Tier2GrudgeRules.DeliveredPrisonerLowRegardThreshold" />).
        /// </summary>
        public const int LowRegardThreshold = -10;

        /// <summary>The ceiling any proposal is clamped to, matching every other magnitude's 0..100 scale.</summary>
        public const int MaxMagnitude = 100;

        /// <summary>
        ///   ALWAYS forms for a recognized close kin: there is no benign execution, and no rumour to wait
        ///   on, the killing is witnessed or plainly learned of at once. Magnitude is heaviest for a
        ///   spouse or child, a step lighter for a parent or sibling, bumped further when the kin already
        ///   resented the player before the killing. KnownToPlayer is always true.
        /// </summary>
        public static GrudgeProposal ExecutedKinGrudge(ExecutedKinFacts facts)
        {
            if (facts == null) return GrudgeProposal.None;

            int magnitude = facts.Kinship == ExecutedKinKind.Child || facts.Kinship == ExecutedKinKind.Spouse
                ? ChildOrSpouseMagnitude
                : SiblingOrParentMagnitude;

            if (facts.KinRegardForPlayer <= LowRegardThreshold)
                magnitude += LowRegardBonus;

            if (magnitude > MaxMagnitude) magnitude = MaxMagnitude;

            return GrudgeProposal.Of(magnitude, true);
        }
    }
}
