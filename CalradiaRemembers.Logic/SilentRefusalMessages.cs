// Code written by Gabriel Mailhot, 03/08/2026.
// The exact wording a bridge executor hands back when it refuses a give_gold, take_gold, or give_prisoner
// action instead of silently returning null.
//
// The stake: several executors used to just "return null" on a refusal branch (no parsable amount, an
// insolvent giver, a missing bargain). A null ActionOutcome reaches neither AppendSystemMessage (the player
// line) nor ActionRefusalNote (the model's transcript note), so the player sees nothing and the model, having
// already narrated the gift or payment as done, keeps believing it happened into every prompt rebuilt
// afterwards. Player reports, 2026-08-03: a gifted item never transferred, an NPC's promised 1000 denars
// never arrived, an offered prisoner stayed in the party. These strings are what turns that silent no-op
// into a surfaced ActionOutcome.Refused(...), read by both the player and the model in one pass.

namespace CalradiaRemembers.Logic
{
    /// <summary>Wording for the give_gold / take_gold / give_prisoner refusal branches that used to return null.</summary>
    public static class SilentRefusalMessages
    {
        /// <summary>The prompt's give_gold carried no amount that parsed, or a non-positive one.</summary>
        public static string GiveGoldNoValidSum()
        {
            return "(No sum was named, so no coin changed hands.)";
        }

        /// <summary>The NPC offering gold does not hold any to give.</summary>
        public static string GiveGoldGiverInsolvent(string giverName)
        {
            return $"({giverName} reaches for a purse that holds nothing; no coin changes hands.)";
        }

        /// <summary>The prompt's take_gold carried no amount that parsed, or a non-positive one.</summary>
        public static string TakeGoldNoValidSum()
        {
            return "(No sum was named, so no coin changed hands.)";
        }

        /// <summary>The player has no coin left once the demand is clamped to their real purse.</summary>
        public static string TakeGoldPlayerInsolvent()
        {
            return "(You have no coin to give.)";
        }

        /// <summary>No outstanding DeliverPrisoner bargain matches the give_prisoner the model just emitted.</summary>
        public static string GivePrisonerNoBargain()
        {
            return "(There is no such bargain between us; the captive stays in your hold.)";
        }
    }
}
