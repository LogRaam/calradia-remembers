// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, Tier 2 source catalog (ROADMAP ~line 351): the four NEW detections, as opposed to
// Tier 1's migration-backfill list. Each rule is a pure fact-in/proposal-out function; it does not
// compute rigidity (GrudgePolicy.RigidityAt does that downstream from the holder's regard at plant
// time) and does not itself plant anything (a future GrudgeBehavior.TryPlant does, gated by
// GrudgePolicy.ShouldForm/TryAdmit exactly like every other source). All facts are primitives so this
// namespace stays engine/SDK-free; the mod-side bridge maps its own enums (CaptiveSceneIntent, sway
// deltas, marriage-blessed flags) onto these DTOs. See the morning report for exactly where each rule
// would be called from.

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>
    ///   The uniform verdict every Tier 2 rule returns. <see cref="Forms" /> false (with
    ///   <see cref="Magnitude" /> 0) means the event does not warrant a grudge at all; the caller still
    ///   owes the result to <see cref="GrudgePolicy.ShouldForm" /> for the anti-noise guardrails
    ///   (attributability, the trait-modulated floor) before actually planting one.
    /// </summary>
    public struct GrudgeProposal
    {
        /// <summary>True when this event warrants a candidate grudge at all.</summary>
        public bool Forms { get; set; }

        /// <summary>The proposed intensity, 0..100, before <see cref="GrudgePolicy" /> formation/trait gating. Zero when <see cref="Forms" /> is false.</summary>
        public int Magnitude { get; set; }

        /// <summary>True when the holder already knows of the grudge at formation (drives prompt open-vs-conceal, same field as <see cref="Grudge.KnownToPlayer" />).</summary>
        public bool KnownToPlayer { get; set; }

        private static GrudgeProposal DoesNotForm() => new GrudgeProposal {Forms = false, Magnitude = 0, KnownToPlayer = false};

        private static GrudgeProposal Formed(int magnitude, bool knownToPlayer) => new GrudgeProposal {Forms = true, Magnitude = magnitude, KnownToPlayer = knownToPlayer};

        /// <summary>Factory for the "nothing warrants a grudge" verdict, reused by every rule below.</summary>
        public static GrudgeProposal None => DoesNotForm();

        /// <summary>Factory for a formed proposal, reused by every rule below.</summary>
        public static GrudgeProposal Of(int magnitude, bool knownToPlayer) => Formed(magnitude, knownToPlayer);
    }

    /// <summary>Facts for <see cref="Tier2GrudgeRules.DeliveredPrisonerGrudge" />: the player handed this captive lord to someone.</summary>
    public sealed class DeliveredPrisonerFacts
    {
        /// <summary>True when the captive was delivered to a faction/lord who is THEIR foe (give_prisoner's bargain target), vs a neutral or allied party.</summary>
        public bool DeliveredToEnemyOfCaptive { get; set; }

        /// <summary>The captive's regard for the player BEFORE the delivery (mirrors <see cref="DivorceFacts.SpouseRegardForPlayer" />'s low-regard-nurses-harder framing).</summary>
        public int CaptiveRegardForPlayer { get; set; }
    }

    /// <summary>Facts for <see cref="Tier2GrudgeRules.CaptiveAbuseGrudge" />: an NPC or held companion was tormented/assaulted in a captive scene.</summary>
    public sealed class CaptiveAbuseFacts
    {
        /// <summary>
        ///   0 = none/benign (Reward, Interrogation: no grudge), 1 = mild (a rough Interrogation),
        ///   2 = moderate (Domination, Training), 3 = grievous (Torture, or a coerced PersonalDesire scene).
        ///   The engine maps its own <c>CaptiveSceneIntent</c> onto this scale (see morning report).
        /// </summary>
        public int Severity { get; set; }

        /// <summary>True when the player was the one who directly did it, vs a captor acting in front of/around the player (e.g. the player's own captivity, or a companion-victim scene the player merely witnessed).</summary>
        public bool PlayerWasThePerpetrator { get; set; }
    }

    /// <summary>Facts for <see cref="Tier2GrudgeRules.SwayExposedGrudge" />: the player turned X's opinion against this target, and the target heard of it.</summary>
    public sealed class SwayExposedFacts
    {
        /// <summary>True only once the existing Rivalry-rumour spread actually reached the target — the sole formation gate (an unheard sway leaves no trace to grudge over).</summary>
        public bool RumourReachedTarget { get; set; }

        /// <summary>How hard the sway hit (the relation delta magnitude sway_opinion applied against the target, see <c>CourtActionPolicy.SwayDelta</c>), 0..100 scale.</summary>
        public int SwayMagnitude { get; set; }
    }

    /// <summary>Facts for <see cref="Tier2GrudgeRules.UnblessedMarriageGrudge" />: a family member who opposed an unblessed marriage the player made.</summary>
    public sealed class UnblessedMarriageFacts
    {
        /// <summary>True when the family never blessed this union (mirrors <see cref="DivorceFacts.MarriageWasBlessed" />, negated).</summary>
        public bool MarriageIsUnblessed { get; set; }

        /// <summary>True while the marriage still stands. Once it ends this grudge is meant to be CLEARED, not merely decayed (see the rule's own doc comment).</summary>
        public bool StillMarried { get; set; }

        /// <summary>Reserved for a future magnitude-scaling pass; not consumed by the current rule (a flat standing magnitude is used instead, matching <see cref="DivorceConsequencePolicy.UnblessedFamilyGratitudeRelationNudge" />'s own flat-nudge precedent).</summary>
        public int FamilyMemberRegardForPlayer { get; set; }
    }

    /// <summary>
    ///   Pure Tier 2 grudge SOURCE rules (ROADMAP "new detections, high value"): delivered prisoners,
    ///   captive-scene abuse (the unified companion-revenge driver), exposed sway, and the unblessed-
    ///   marriage family. Each rule only decides Forms/Magnitude/KnownToPlayer; rigidity, decay, the
    ///   per-NPC cap, and resolution are all <see cref="GrudgePolicy" />'s job downstream, unchanged.
    /// </summary>
    public static class Tier2GrudgeRules
    {
        // ── DeliveredPrisoner ────────────────────────────────────────────────

        /// <summary>Tuning: base magnitude when the captive was delivered to a neutral/allied party (a lesser betrayal, still a betrayal of trust).</summary>
        public const int DeliveredPrisonerBaseMagnitude = 35;

        /// <summary>Tuning: magnitude when delivered to the captive's own foe ("you sold me to my enemy") — the heavier, headline case.</summary>
        public const int DeliveredPrisonerToEnemyMagnitude = 65;

        /// <summary>Tuning: at/below this prior regard for the player, the captive already disliked them, so the betrayal is nursed HARDER (mirrors <see cref="DivorceConsequencePolicy.LowRegardThreshold" />).</summary>
        public const int DeliveredPrisonerLowRegardThreshold = -10;

        /// <summary>Tuning: the magnitude bump applied when <see cref="DeliveredPrisonerFacts.CaptiveRegardForPlayer" /> is at/below <see cref="DeliveredPrisonerLowRegardThreshold" /> (mirrors <see cref="DivorceConsequencePolicy.SpouseGrudgeLowRegardBonus" />).</summary>
        public const int DeliveredPrisonerLowRegardBonus = 10;

        /// <summary>
        ///   ALWAYS forms (a captive always learns, and always resents, who sold them onward — never a
        ///   silent event), heavier when delivered to a bitter enemy of the captive's than to a neutral
        ///   or allied party. Design judgment call: unlike CaptiveAbuse (below), even a "gentle" delivery
        ///   to an ally is still a betrayal of the captor-captive trust the game itself modelled (they were
        ///   the player's to protect or free), so this rule never returns <see cref="GrudgeProposal.None" />.
        ///   KnownToPlayer is always true: the captive was physically handed over, so there is no
        ///   concealment to model. A captive who already regarded the player poorly nurses the betrayal a
        ///   touch harder, exactly like the divorce's repudiated spouse.
        /// </summary>
        public static GrudgeProposal DeliveredPrisonerGrudge(DeliveredPrisonerFacts facts)
        {
            if (facts == null) return GrudgeProposal.None;

            int magnitude = facts.DeliveredToEnemyOfCaptive ? DeliveredPrisonerToEnemyMagnitude : DeliveredPrisonerBaseMagnitude;

            if (facts.CaptiveRegardForPlayer <= DeliveredPrisonerLowRegardThreshold)
                magnitude += DeliveredPrisonerLowRegardBonus;

            return GrudgeProposal.Of(magnitude, knownToPlayer: true);
        }

        // ── CaptiveAbuse ─────────────────────────────────────────────────────

        /// <summary>Tuning: the severity floor below which nothing forms (Reward/Interrogation register as benign, Severity 0).</summary>
        public const int CaptiveAbuseFormationSeverity = 1;

        /// <summary>Tuning: base magnitude per severity step (1..3), before the perpetrator bonus. Deliberately steep: this is the deepest grudge class, a VICTIM's grudge.</summary>
        public const int CaptiveAbuseMagnitudePerSeverity = 25;

        /// <summary>Tuning: flat bonus added when the player was the direct perpetrator, not merely present for a captor's act.</summary>
        public const int CaptiveAbusePlayerPerpetratorBonus = 15;

        /// <summary>
        ///   The ceiling this rule can ever propose: Severity 3 (grievous) with the player as direct
        ///   perpetrator. Deliberately kept BELOW <see cref="GrudgePolicy.VendettaRigidity" />-adjacent
        ///   territory in absolute terms (magnitude is not rigidity) — a high-magnitude abuse grudge can
        ///   still, at high holder regard for the player, resolve softer than a true
        ///   <see cref="GrudgeSource.ExecutedKin" /> vendetta, which is ALWAYS maximum rigidity regardless
        ///   of magnitude or regard. Magnitude sets how deep the wound starts; only ExecutedKin (or a
        ///   grudge that happens to compute to 100 rigidity) is unforgivable by construction.
        /// </summary>
        public const int CaptiveAbuseMaxMagnitude = CaptiveAbuseMagnitudePerSeverity * 3 + CaptiveAbusePlayerPerpetratorBonus;

        /// <summary>
        ///   Forms at Severity >= <see cref="CaptiveAbuseFormationSeverity" /> (Reward/Interrogation stay
        ///   Severity 0 and never form). Magnitude scales steeply with severity and is heaviest when the
        ///   player was the direct perpetrator rather than a bystander to a captor's act. Always
        ///   KnownToPlayer: the victim lived it firsthand, there is nothing to later discover.
        /// </summary>
        public static GrudgeProposal CaptiveAbuseGrudge(CaptiveAbuseFacts facts)
        {
            if (facts == null || facts.Severity < CaptiveAbuseFormationSeverity) return GrudgeProposal.None;

            int magnitude = facts.Severity * CaptiveAbuseMagnitudePerSeverity;

            if (facts.PlayerWasThePerpetrator)
                magnitude += CaptiveAbusePlayerPerpetratorBonus;

            return GrudgeProposal.Of(magnitude, knownToPlayer: true);
        }

        // ── SwayExposed ──────────────────────────────────────────────────────

        /// <summary>Tuning: base magnitude once the rumour has reached the target, before scaling with sway strength. Set comfortably above <see cref="GrudgePolicy.MagnitudeFloor" /> on its own, so even the lightest possible sway still clears formation once the rumour has spread (the rumour reaching the target, not the sway's own strength, is this rule's real gate).</summary>
        public const int SwayExposedBaseMagnitude = 20;

        /// <summary>Tuning: how much of the sway's own magnitude carries over into the grudge (a fraction: a sway is a lesser wound than being sold or abused).</summary>
        public const double SwayExposedMagnitudeFraction = 0.5;

        /// <summary>
        ///   Forms ONLY when <see cref="SwayExposedFacts.RumourReachedTarget" /> is true — an unheard sway
        ///   leaves the target with nothing to resent. Magnitude scales modestly (half) with the sway's own
        ///   strength, on top of a small base. KnownToPlayer is false BY DESIGN: the target resents quietly
        ///   until the grudge surfaces some other way, mirroring the concealment already modelled for
        ///   scheme-warned/scheme-abetted grudges (the player is not automatically told they were caught).
        /// </summary>
        public static GrudgeProposal SwayExposedGrudge(SwayExposedFacts facts)
        {
            if (facts == null || !facts.RumourReachedTarget) return GrudgeProposal.None;

            int magnitude = SwayExposedBaseMagnitude + (int) (facts.SwayMagnitude * SwayExposedMagnitudeFraction);

            return GrudgeProposal.Of(magnitude, knownToPlayer: false);
        }

        // ── UnblessedMarriage ────────────────────────────────────────────────

        /// <summary>Tuning: the standing magnitude while an unblessed marriage lasts. Flat, not regard-scaled, matching <see cref="DivorceConsequencePolicy" />'s own flat-nudge precedent for this same family.</summary>
        public const int UnblessedMarriageMagnitude = 30;

        /// <summary>
        ///   Forms only while BOTH the marriage is unblessed AND it still stands — the moment the marriage
        ///   ends, this grudge is meant to CLEAR entirely (not merely decay), since
        ///   <see cref="DivorceConsequencePolicy.ResolveUnblessedFamilyReaction" /> already turns the very
        ///   same family's reaction into <see cref="ReactionKind.Gratitude" /> at that point — a resolved
        ///   grievance, not a resentful one. The engine's divorce-consequence wiring is therefore expected
        ///   to explicitly resolve/remove any live <see cref="GrudgeSource.UnblessedMarriage" /> grudge for
        ///   this holder the same tick it applies the Gratitude nudge, so the two paths never disagree.
        ///   KnownToPlayer is always true: the family has "made their disapproval plain" (ROADMAP wording),
        ///   there is nothing hidden about it.
        /// </summary>
        public static GrudgeProposal UnblessedMarriageGrudge(UnblessedMarriageFacts facts)
        {
            if (facts == null || !facts.MarriageIsUnblessed || !facts.StillMarried) return GrudgeProposal.None;

            return GrudgeProposal.Of(UnblessedMarriageMagnitude, knownToPlayer: true);
        }
    }
}
