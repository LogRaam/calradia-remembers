// Code written by Gabriel Mailhot, 04/07/2026.
// Stance mechanical behaviour-gating, increment 1 (Fear only, ratified 2026-07-04): the coercion gate
// wired into the freedom-bargain / ransom bridge. Per the ratified structural ruling, the gate reads
// stance.Fear DIRECTLY — the word/deed asymmetry in StanceGate already makes flattery-only coercion
// impractical (~20 accepted word rounds over ~160 in-game hours, with Fear decaying underneath), so
// there is no separate "proven floor" store and no new SyncData key. A terrified captive yields softer
// terms but resents the coercion in secret (a HIDDEN grudge, not open regard); an unafraid, contemptuous
// one refuses outright. Honor/Valor stiffen resolve; Calculating breaks sooner and holds the coercion
// against the player all the harder once it does.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How a coerced captive answers a freedom bargain, decided by their Fear/Respect and traits.</summary>
    public enum CoercionOutcome
    {
        /// <summary>Neither cowed enough to yield nor unafraid enough to refuse outright — the bargain proceeds on its own terms.</summary>
        Neutral,

        /// <summary>Fear breaks their resolve: they yield softer terms, but resent being coerced rather than persuaded.</summary>
        YieldsSofterTerms,

        /// <summary>Too unafraid and too contemptuous of the player to be moved at all — the bargain is refused flat.</summary>
        RefusesFlat
    }

    /// <summary>The resolved answer: what happens to the bargain's terms, and what it costs in secret.</summary>
    public sealed class CoercionResult
    {
        public CoercionResult(CoercionOutcome outcome, double ransomMultiplier, int hiddenGrudgeMagnitude)
        {
            Outcome = outcome;
            RansomMultiplier = ransomMultiplier;
            HiddenGrudgeMagnitude = hiddenGrudgeMagnitude;
        }

        /// <summary>The decision.</summary>
        public CoercionOutcome Outcome { get; }

        /// <summary>Multiplies whatever ransom/term value the bargain uses. 1.0 except on <see cref="CoercionOutcome.YieldsSofterTerms"/>.</summary>
        public double RansomMultiplier { get; }

        /// <summary>Magnitude of the HIDDEN grudge to plant against the coercer. 0 except on <see cref="CoercionOutcome.YieldsSofterTerms"/>.</summary>
        public int HiddenGrudgeMagnitude { get; }
    }

    /// <summary>
    ///   Pure threshold policy for Fear-driven coercion (Stance mechanical behaviour-gating, increment
    ///   1). Consulted by the bridge BEFORE it commits a freedom bargain's terms — "words nudge, deeds
    ///   prove", and now the posture bites.
    /// </summary>
    public static class StanceGatePolicy
    {
        /// <summary>Tuning: Fear at/above this breaks a neutral-traited captive's resolve — they yield.</summary>
        public const int YieldFearFloor = 40;

        /// <summary>Tuning: how much the softened terms are worth to the player once fear breaks the captive.</summary>
        public const double RansomSoftenMultiplier = 0.75;

        /// <summary>Tuning: the base HIDDEN grudge planted for a coerced yield — resentment instead of regard.</summary>
        public const int CoercedGrudgeMagnitude = 30;

        /// <summary>Tuning: Fear strictly below this, combined with low Respect, is what lets a captive refuse outright.</summary>
        public const int RefuseFearCeiling = 15;

        /// <summary>Tuning: Respect strictly below this, combined with low Fear, is what lets a captive refuse outright.</summary>
        public const int RefuseRespectCeiling = 10;

        /// <summary>Tuning: per positive point of Honor or Valor, how much harder the captive is to cow (raises the yield floor).</summary>
        public const int HonorValorResistPerLevel = 5;

        /// <summary>Tuning: per positive point of Calculating, how much sooner the captive breaks (lowers the yield floor).</summary>
        public const int CalculatingYieldPerLevel = 5;

        /// <summary>Tuning: per positive point of Calculating, how much extra the coerced yield resents it (a schemer holds a grudge tighter).</summary>
        public const int CalculatingGrudgeBonusPerLevel = 5;

        /// <summary>
        ///   What a captive's answer to a coercive bargain is weighed from. FIVE INTS in a row, and nothing
        ///   but their order told fear from respect from three traits: swap honor and calculating and the
        ///   captive breaks where he should have stiffened, with no test of the arithmetic any the wiser.
        ///   Trait values are Bannerlord's own scale, roughly -2..+2; only their POSITIVE side is read here.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class CoercionFacts
        {
           /// <summary>How much the captive fears the player, 0..100. The bar the rest of these move.</summary>
           public int Fear { get; set; }

           /// <summary>How much the captive respects the player, 0..100.</summary>
           public int Respect { get; set; }

           /// <summary>Honor trait. Positive honor stiffens resolve; its negative side does not soften it.</summary>
           public int Honor { get; set; }

           /// <summary>Valor trait. Positive valor stiffens resolve, the same way honor does.</summary>
           public int Valor { get; set; }

           /// <summary>
           ///   Calculating trait. Positive calculating makes a captive break sooner AND resent the yielding
           ///   more; a schemer holds the grudge tighter.
           /// </summary>
           public int Calculating { get; set; }
        }

        /// <summary>
        ///   Resolves how a captive NPC answers a coercive freedom bargain. Trait params are standard
        ///   Bannerlord trait levels (roughly −2..+2): only POSITIVE Honor/Valor stiffen resolve, and
        ///   only POSITIVE Calculating softens it — a trait's negative side never works the opposite way
        ///   here, it simply does not apply. The effective yield floor is clamped so it can never fall
        ///   beneath <see cref="RefuseFearCeiling"/> (a captive would otherwise "yield" at a fear level
        ///   the refusal rule already treats as unafraid).
        /// </summary>
        public static CoercionResult ResolveCoercion(CoercionFacts facts)
        {
           facts ??= new CoercionFacts();

           int fear = facts.Fear;
           int respect = facts.Respect;
           int honor = facts.Honor;
           int valor = facts.Valor;
           int calculating = facts.Calculating;

            int honorResist = honor > 0 ? honor * HonorValorResistPerLevel : 0;
            int valorResist = valor > 0 ? valor * HonorValorResistPerLevel : 0;
            int calculatingEase = calculating > 0 ? calculating * CalculatingYieldPerLevel : 0;

            int effectiveFloor = YieldFearFloor + honorResist + valorResist - calculatingEase;
            effectiveFloor = Clamp(effectiveFloor, RefuseFearCeiling, 100);

            if (fear >= effectiveFloor)
            {
                int calculatingGrudgeBonus = calculating > 0 ? calculating * CalculatingGrudgeBonusPerLevel : 0;

                return new CoercionResult(CoercionOutcome.YieldsSofterTerms, RansomSoftenMultiplier,
                    CoercedGrudgeMagnitude + calculatingGrudgeBonus);
            }

            if (fear < RefuseFearCeiling && respect < RefuseRespectCeiling)
                return new CoercionResult(CoercionOutcome.RefusesFlat, 1.0, 0);

            return new CoercionResult(CoercionOutcome.Neutral, 1.0, 0);
        }

        // ── Trust (DELEGATED RISK), increment 2 ratified 2026-07-04 ─────────────
        // Trust gates verbs where the NPC exposes THEMSELVES to the player: lending their own troops or
        // influence, never the reciprocal direction (the player exposing their own to the NPC). Trust-only
        // for this increment — no trait modulation yet; a later increment can layer Calculating wariness
        // or Generosity ease on top of this floor, the same way Honor/Valor/Calculating already shape the
        // Fear gate above.

        /// <summary>
        ///   Tuning: Trust at/above this counts as "genuine regard / earned trust" — the floor an NPC
        ///   needs before they will risk exposing themselves on the player's behalf.
        /// </summary>
        public const int DelegatedRiskTrustFloor = 20;

        /// <summary>
        ///   Tuning: an established relationship (vanilla clan relation at/above this) is its OWN proof of
        ///   trust, so a long-standing ally still lends even before any CR-tracked trust deed. This keeps
        ///   the gate from silently narrowing who could already lend; an unproven stranger must still earn
        ///   Trust the hard way.
        /// </summary>
        public const int DelegatedRiskRelationBypass = 30;

        /// <summary>
        ///   Tuning: personal regard (Affection) at/above this is PERSONAL LOYALTY enough to risk a favour
        ///   for the player even when clan standing is cold — warmth cutting through political distance.
        ///   This is the Affection axis's "loyalty despite poor clan standing" path (increment 4, ratified
        ///   2026-07-05), a THIRD door onto the delegated-risk gate distinct from proven Trust and from the
        ///   formal clan-relation bypass: a lord who personally adores the player takes the risk on THEM.
        /// </summary>
        public const int AffectionLoyaltyFloor = 40;

        /// <summary>
        ///   The three independent standings any one of which is enough for an NPC to risk something of
        ///   their own on the player. Three ints in a row on three DIFFERENT ledgers, each with its own
        ///   floor, so a swap silently measures loyalty against the wrong bar.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class DelegatedRiskFacts
        {
           /// <summary>Proven Trust: the "low trust, prove it first" path.</summary>
           public int Trust { get; set; }

           /// <summary>Vanilla CLAN relation: a long-cultivated ally has proof enough without personal warmth.</summary>
           public int Relation { get; set; }

           /// <summary>
           ///   The mod's own personal regard: loyalty that cuts through a cold clan standing, because a lord
           ///   who adores the player takes the risk on THEM.
           /// </summary>
           public int Affection { get; set; }
        }

        /// <summary>
        ///   True when this NPC would risk delegating something of their own to the player (lending their
        ///   own troops/influence, or a companion accepting a risky errand). Three independent paths, any
        ///   one suffices: proven <c>Trust</c> ("low Trust, prove it first"); an established
        ///   clan <c>Relation</c> (a long-cultivated ally has proof enough); or high personal
        ///   <c>Affection</c> (personal loyalty that cuts through a cold clan standing).
        /// </summary>
        public static bool WillDelegateRisk(DelegatedRiskFacts facts)
        {
           if (facts == null) return false;

           int trust = facts.Trust;
           int relation = facts.Relation;
           int affection = facts.Affection;

           return trust >= DelegatedRiskTrustFloor
              || relation >= DelegatedRiskRelationBypass
              || affection >= AffectionLoyaltyFloor;
        }

        // ── Respect (POLITICAL WEIGHT), increment 3 ratified 2026-07-04 ─────────
        // Respect gates how much political weight the player carries with the NPC. Increment 1's concrete
        // site is `join_clan` (recruiting a lord into the player's clan): a lord who holds the player in
        // contempt will not throw in their lot with them, however cordial the relation. Respect-only for
        // this increment — no trait modulation yet; a later increment can layer trait-driven susceptibility
        // on top of this floor, the same way Honor/Valor/Calculating already shape the Fear gate above.

        /// <summary>
        ///   Tuning: Respect at/above this counts as enough esteem for the player's political weight to
        ///   carry with this NPC (e.g. throwing in their lot via <c>join_clan</c>).
        /// </summary>
        public const int RespectFloor = 20;

        /// <summary>
        ///   Tuning: the GENERAL Respect soupape — an established relationship (vanilla clan relation
        ///   at/above this) is its own proof of standing, so a long-cultivated ally carries the player's
        ///   weight even before any CR-tracked Respect deed. Kept symmetric with
        ///   <see cref="DelegatedRiskRelationBypass"/>. Used by <see cref="CommandsRespect"/> for future
        ///   Respect sites whose own relation gate sits below this (sway_opinion weight, threat
        ///   credibility). NOT used by join_clan, which sets its own higher bypass below.
        /// </summary>
        public const int RespectRelationBypass = 30;

        /// <summary>
        ///   Tuning: the join_clan-SPECIFIC Respect soupape (Gabriel ratified 2026-07-05). Recruiting a
        ///   lord already requires <c>LordRecruitment.MinRelation</c> (60), so the general bypass (30)
        ///   would always fire and neuter the Respect floor. This higher bar makes Respect actually bite:
        ///   an adored lord (relation at/above this) still joins freely, but a merely-eligible lord (in the
        ///   60..this band) must ALSO respect the player to throw in their lot.
        /// </summary>
        public const int RecruitRespectRelationBypass = 80;

        /// <summary>
        ///   True when this NPC's Respect for the player is high enough that the player's political
        ///   weight commands something with them — "low Respect, prove your worth first". An
        ///   already-high vanilla relation bypasses the Respect floor, the same soupape as
        ///   <see cref="WillDelegateRisk"/>. For general Respect sites; join_clan uses
        ///   <see cref="CommandsRespectToJoinClan"/> with a higher bypass.
        /// </summary>
        public static bool CommandsRespect(int respect, int relation)
            => respect >= RespectFloor || relation >= RespectRelationBypass;

        /// <summary>
        ///   The join_clan variant of <see cref="CommandsRespect"/>: same Respect floor, but a higher
        ///   relation bypass (<see cref="RecruitRespectRelationBypass"/>) so the floor actually binds above
        ///   the recruitment's own relation gate — a merely-eligible lord must respect the player to follow
        ///   them, while a truly beloved one still joins on relation alone.
        /// </summary>
        public static bool CommandsRespectToJoinClan(int respect, int relation)
            => respect >= RespectFloor || relation >= RecruitRespectRelationBypass;

        #region private

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        #endregion
    }
}
