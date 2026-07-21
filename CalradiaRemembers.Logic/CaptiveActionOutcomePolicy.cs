// Code written by Gabriel Mailhot, 20/07/2026.
// Player agency in a captive scene. Ratified with Gabriel 2026-07-20 after a play-test where every "I resist",
// "I don't lick his boot", "I try to fight back" was absorbed with no effect: the LLM was ADJUDICATING the
// player's physical attempts itself, and an LLM asked to judge a player-vs-NPC struggle always favours the
// story it already meant to tell, so the scene railroads. The fix is the same everywhere else in CR: the mod
// DECIDES, the LLM NARRATES. One fresh verdict is drawn per turn (no list, no index, nothing for the model to
// count) and handed to the prompt as a conditional note.
//
// The chance is Gabriel's formula: a 10% floor plus the player's HEALTH. A hale prisoner nearly always lands
// what they try, which is correct, because SUCCEEDING here only means the attempt WORKS and the scene
// escalates (the men close in, the captor answers it), never that they are freed. Being worn down is what
// takes agency away: the captor's own harm_prisoner action lowers REAL hit points, so every wound quietly
// lowers the odds of the next attempt. That attrition loop needs no extra code, only this reading of health.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether the player's physical attempt in a captive scene lands, this turn.</summary>
    public static class CaptiveActionOutcomePolicy
    {
        /// <summary>
        ///   TUNE: the floor. Even at death's door a prisoner has this much chance of landing what they try,
        ///   so a beaten player is never left with literally no agency at all.
        /// </summary>
        public const int BaseChancePercent = 10;

        /// <summary>
        ///   The chance, in percent, that a physical attempt lands: the floor plus the player's health percent,
        ///   capped at certainty. Health is the whole modifier by design (see this file's header).
        /// </summary>
        public static int SuccessChance(int healthPercent)
        {
            int health = healthPercent < 0
                ? 0
                : healthPercent > 100
                    ? 100
                    : healthPercent;
            int chance = BaseChancePercent + health;

            return chance > 100
                ? 100
                : chance;
        }

        /// <summary>
        ///   True when this turn's attempt lands. <paramref name="rollPercent" /> is a real 0..99 draw made by
        ///   the mod (never by the model, whose "dice" bend toward the story it wants to tell).
        /// </summary>
        public static bool Succeeds(int rollPercent, int healthPercent) => rollPercent < SuccessChance(healthPercent);
    }
}
