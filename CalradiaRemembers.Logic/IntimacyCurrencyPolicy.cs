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
        /// <param name="playerIsFemale">The dynamic is one-directional — only toward a female player.</param>
        /// <param name="npcIsMale">Only a man propositions here.</param>
        /// <param name="npcIsPlayerKin">A relative would never — excluded outright.</param>
        /// <param name="npcHonor">Honour trait (−2..+2); an honourable man does not exploit this way.</param>
        /// <param name="npcCalculating">Calculating trait (−2..+2); a schemer leverages.</param>
        /// <param name="npcMercy">Mercy trait (−2..+2); a callous man leverages.</param>
        /// <param name="adultExplicitOrHigher">The adult tier is at least Explicit (the gate).</param>
        public static bool IsEligible(
            bool playerIsFemale, bool npcIsMale, bool npcIsPlayerKin,
            int npcHonor, int npcCalculating, int npcMercy,
            bool adultExplicitOrHigher)
        {
            if (!playerIsFemale || !npcIsMale || npcIsPlayerKin || !adultExplicitOrHigher) return false;

            // The archetype: a man of low scruple — dishonourable, and either scheming or callous.
            bool dishonourable = npcHonor <= 0;
            bool exploitative  = npcCalculating >= 1 || npcMercy <= 0;
            return dishonourable && exploitative;
        }
    }
}
