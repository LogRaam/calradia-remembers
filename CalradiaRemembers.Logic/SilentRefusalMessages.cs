// Code written by Gabriel Mailhot, 03/08/2026.
// The exact wording a bridge executor hands back when it refuses a give_gold, take_gold, give_prisoner,
// give_item, grant_blessing, arrange_marriage, or decline_divorce action instead of silently returning null.
//
// The stake: several executors used to just "return null" on a refusal branch (no parsable amount, an
// insolvent giver, a missing bargain, an unnamed gift or kin, a demand that was never pending). A null
// ActionOutcome reaches neither AppendSystemMessage (the player line) nor ActionRefusalNote (the model's
// transcript note), so the player sees nothing and the model, having already narrated the deed as done,
// keeps believing it happened into every prompt rebuilt afterwards. Player reports, 2026-08-03: a gifted
// item never transferred, an NPC's promised 1000 denars never arrived, an offered prisoner stayed in the
// party. The same class of bug also reaches a promised gift with no item named, a blessing with no one
// named, a betrothal with no kin named, and a refused divorce demand that never registered. These strings
// are what turns that silent no-op into a surfaced ActionOutcome.Refused(...), read by both the player and
// the model in one pass.

namespace CalradiaRemembers.Logic
{
    /// <summary>Wording for the bridge executor refusal branches that used to return null.</summary>
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

        /// <summary>The prompt's give_item named no item at all (neither a picked item id nor a free-text name).</summary>
        public static string GiveItemNoneNamed()
        {
            return "(No item was named, so nothing changed hands.)";
        }

        /// <summary>The prompt's grant_blessing carried no hero parameter, or a blank one, to bless.</summary>
        public static string GrantBlessingNoOneNamed()
        {
            return "(No one was named to bless, so no blessing was given.)";
        }

        /// <summary>The prompt's arrange_marriage was missing the player_kin and/or target_kin parameter.</summary>
        public static string ArrangeMarriageMissingKin()
        {
            return "(The match was not clear enough to arrange; no betrothal was made.)";
        }

        /// <summary>
        ///   The named hero is not, in fact, the player's spouse. Shared by accept_divorce and
        ///   decline_divorce, whose bridge executors mirror this same first check.
        /// </summary>
        public static string DivorceNotFromSpouse()
        {
            return "(There is no marriage between you and this person to end.)";
        }

        /// <summary>
        ///   The named spouse has not actually pressed a divorce demand, so there is nothing to accept or
        ///   decline. Shared by accept_divorce and decline_divorce, whose bridge executors mirror this same
        ///   second check.
        /// </summary>
        public static string DivorceNoDemandPending(string npcName)
        {
            return $"({npcName} has not pressed you to end the marriage.)";
        }
    }
}
