// Code written by Gabriel Mailhot, 23/06/2026.
// Negotiation Phase 3 (ROADMAP) — "intimacy as currency", PURE gating only (no content here). A narrow,
// adult-gated, opt-in dynamic: a CYNICAL man may, from a position of leverage, offer a favour to a female
// player in exchange for intimacy. This decides ONLY who may raise it: female player, an unrelated man of
// low scruple, at a high-enough adult tier. The framing, the consequence, and the persistent leverage live
// in the prompt + the event pipeline; everything sensitive is gated and off by default.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure eligibility for the intimacy-as-currency dynamic. Kept deliberately strict and one-directional
    ///   (a man toward a female player only), filtered to a cynical/exploitative archetype — never an
    ///   honourable man, never the player's kin — and only when the adult tier is high enough.
    /// </summary>
    public static class IntimacyCurrencyPolicy
    {
        /// <summary>
        ///   Who the two people are, and what kind of man he is. Three booleans, then THREE TRAIT INTS in a
        ///   row, then the content gate: the traits are the archetype itself, and swapping mercy with
        ///   calculating turns a callous man into a scheming one, which reads the same to the compiler and
        ///   differently to every rule built on it.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class LeverageArchetype
        {
           /// <summary>The dynamic is one-directional: only toward a female player.</summary>
           public bool PlayerIsFemale { get; set; }

           /// <summary>Only a man propositions here.</summary>
           public bool NpcIsMale { get; set; }

           /// <summary>A relative would never. Excluded outright.</summary>
           public bool NpcIsPlayerKin { get; set; }

           /// <summary>Honour trait, -2..+2. An honourable man does not exploit this way.</summary>
           public int NpcHonor { get; set; }

           /// <summary>Calculating trait, -2..+2. A schemer leverages.</summary>
           public int NpcCalculating { get; set; }

           /// <summary>Mercy trait, -2..+2. A callous man leverages.</summary>
           public int NpcMercy { get; set; }

           /// <summary>The adult tier is at least Explicit. The gate.</summary>
           public bool AdultExplicitOrHigher { get; set; }
        }

        /// <param name="playerIsFemale">The dynamic is one-directional — only toward a female player.</param>
        /// <param name="npcIsMale">Only a man propositions here.</param>
        /// <param name="npcIsPlayerKin">A relative would never — excluded outright.</param>
        /// <param name="npcHonor">Honour trait (−2..+2); an honourable man does not exploit this way.</param>
        /// <param name="npcCalculating">Calculating trait (−2..+2); a schemer leverages.</param>
        /// <param name="npcMercy">Mercy trait (−2..+2); a callous man leverages.</param>
        /// <param name="adultExplicitOrHigher">The adult tier is at least Explicit (the gate).</param>
        public static bool IsEligible(LeverageArchetype facts)
        {
           if (facts == null) return false;

           bool playerIsFemale = facts.PlayerIsFemale;
           bool npcIsMale = facts.NpcIsMale;
           bool npcIsPlayerKin = facts.NpcIsPlayerKin;
           int npcHonor = facts.NpcHonor;
           int npcCalculating = facts.NpcCalculating;
           int npcMercy = facts.NpcMercy;
           bool adultExplicitOrHigher = facts.AdultExplicitOrHigher;

            if (!playerIsFemale || !npcIsMale || npcIsPlayerKin || !adultExplicitOrHigher) return false;

            // The archetype: a man of low scruple — dishonourable, and either scheming or callous.
            bool dishonourable = npcHonor <= 0;
            bool exploitative  = npcCalculating >= 1 || npcMercy <= 0;
            return dishonourable && exploitative;
        }
    }
}
