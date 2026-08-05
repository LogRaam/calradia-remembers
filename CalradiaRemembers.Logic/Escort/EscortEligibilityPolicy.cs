// Code written by Gabriel Mailhot, 04/08/2026.
// follow_me (1:1 bridge action, player wish: a companion promising to "stay by my side" - the battlefield
// version of that is roadmapped separately, this is the map-escort that is doable now): a lord's OWN party
// escorts the player's MobileParty.MainParty for a bounded term, distinct from join_party (the lord keeps
// their own troops and command, never joining the player's party or clan, never swearing to their clan).
// Four independent facts gate BOTH the conditional teaching (EncounterContextBuilder.Build ->
// EncounterContext.NpcCanEscortPlayer -> SDK PromptBuilder.AppendFollowMe) and the mod's own bridge re-check at
// execution (BannerlordGameStateBridge.ExecuteFollowMe / FollowMeBehavior.TryStart), so the two can never
// quietly diverge ("the prompt is advice, the bridge is law").
namespace CalradiaRemembers.Logic.Escort
{
    /// <summary>Pure eligibility gate for follow_me: every guard the teaching and the bridge share.</summary>
    public static class EscortEligibilityPolicy
    {
        /// <summary>
        ///   A follow_me escort always runs a FIXED term of 15 days (author's design call - no player-facing
        ///   request set a duration, and a fixed term keeps the promise unambiguous: the lord rides along for a
        ///   while, then plainly resumes their own business, with no second figure for the LLM to invent or
        ///   ground).
        /// </summary>
        public const int TermDays = 15;

        /// <summary>
        ///   True only when the NPC truly leads their own mobile party (a companion already riding IN the
        ///   player's own party never "leads" one, so this alone already excludes that case - there is nothing
        ///   further for follow_me to do for them), is not at war with the player (an enemy lord does not
        ///   pledge to ride alongside the very army it may soon fight), is not currently bound to an army or a
        ///   siege (their own AI would immediately reclaim the party for that duty, making the escort an
        ///   unkeepable promise the moment it started), and is not already escorting (one escort at a time per
        ///   lord).
        /// </summary>
        public static bool CanEscort(bool leadsParty, bool atWar, bool inArmy, bool alreadyEscorting)
        {
            if (!leadsParty) return false;
            if (atWar) return false;
            if (inArmy) return false;
            if (alreadyEscorting) return false;

            return true;
        }
    }
}
