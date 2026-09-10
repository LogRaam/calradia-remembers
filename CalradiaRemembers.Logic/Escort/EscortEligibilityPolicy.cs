// Code written by Gabriel Mailhot, 04/08/2026, reworked 10/09/2026.
// follow_me (1:1 bridge action, player wish: a companion promising to "stay by my side" - the battlefield
// version of that is roadmapped separately, this is the map-escort that is doable now): a lord's OWN party
// escorts the player's MobileParty.MainParty for a bounded term, distinct from join_party (the lord keeps
// their own troops and command, never joining the player's party or clan, never swearing to their clan).
// These facts gate BOTH the conditional teaching (EncounterContextBuilder.Build ->
// EncounterContext.NpcCanEscortPlayer -> SDK PromptBuilder.AppendFollowMe) and the mod's own bridge re-check at
// execution (BannerlordGameStateBridge.ExecuteFollowMe / FollowMeBehavior.TryStart), so the two can never
// quietly diverge ("the prompt is advice, the bridge is law").
//
// 10/09/2026, THE FIFTH GATE IS GONE, and a player report is why. "It feels like the AI using this like never
// follows me despite it agreeing to." A gate added 29/08/2026 refused any lord whose realm was at war with
// ANYONE, so that he stayed available for his liege's muster call. The intent was sound and the effect was not:
// in an ordinary campaign nearly every kingdom is at war with somebody nearly all the time, so follow_me was
// almost never taught, and a verb that is not taught is ABSENT rather than refused. The model, told nothing,
// agreed warmly and emitted nothing. The player watched a promise evaporate, over and over, with no explanation
// ever offered.
//
// Gabriel's ruling, 10/09/2026: keep the intent, drop the blanket. A lord is stopped by being COMMITTED to that
// war right now (an army, a siege, a battle), not by its existence somewhere on the map. And, his own condition
// on relaxing it: "si il accepte de suivre le joueur et apres il y a un appel pour joindre une armee, ce Lord
// devrait expliquer au joueur qu'il doit le quitter pour suivre l'appel de son seigneur." That is what makes the
// relaxation honest rather than merely permissive, and it lives in EscortContinuationPolicy.
//
// The factionAtWar fact did not disappear; it changed jobs. It no longer refuses anything. It is now a
// DISCLOSURE: a lord whose realm is at war says up front that he may be called away, so the promise the player
// hears is the promise the world can keep.

namespace CalradiaRemembers.Logic.Escort
{
    /// <summary>Why a lord will not escort the player, or <see cref="None" /> when he will.</summary>
    public enum EscortRefusal
    {
        /// <summary>Nothing stands in the way.</summary>
        None,

        /// <summary>He leads no field party of his own, so there is nothing to escort with (that is ride_with_me).</summary>
        NoPartyOfHisOwn,

        /// <summary>His realm and the player's are at war. No lord pledges to ride beside an army he may soon fight.</summary>
        AtWarWithPlayer,

        /// <summary>
        ///   He is committed to a war right now: bound to an army, laying a siege, or in a battle. His own AI
        ///   would reclaim the party within the day, which makes the escort an unkeepable promise at the moment
        ///   it is made.
        /// </summary>
        CommittedToWar,

        /// <summary>He is already escorting somebody. One at a time.</summary>
        AlreadyEscorting
    }

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
        ///   Which door is shut, tried in the order a lord would think of them. <paramref name="committedToWar" />
        ///   is the widened successor to the old inArmy: an army, a siege, or a live battle, because all three
        ///   mean the same thing, that his party is spoken for today.
        /// </summary>
        public static EscortRefusal Refusal(bool leadsParty, bool atWar, bool committedToWar, bool alreadyEscorting)
        {
            if (!leadsParty) return EscortRefusal.NoPartyOfHisOwn;
            if (atWar) return EscortRefusal.AtWarWithPlayer;
            if (committedToWar) return EscortRefusal.CommittedToWar;
            if (alreadyEscorting) return EscortRefusal.AlreadyEscorting;

            return EscortRefusal.None;
        }

        /// <summary>Whether the escort can be promised at all. The bridge asks this; so does the teaching gate.</summary>
        public static bool CanEscort(bool leadsParty, bool atWar, bool committedToWar, bool alreadyEscorting)
            => Refusal(leadsParty, atWar, committedToWar, alreadyEscorting) == EscortRefusal.None;

        /// <summary>
        ///   True when this lord should WARN the player, as he agrees, that he may be called away. His realm is
        ///   at war with someone, so the muster may reach him at any time and the escort will end there
        ///   (EscortContinuationPolicy.EscortEndReason.SummonedByLiege). Said in advance, that is a lord being
        ///   honest; said only afterwards, it is the broken promise this whole change exists to end.
        /// </summary>
        public static bool ShouldWarnOfMuster(bool factionAtWar) => factionAtWar;

        /// <summary>
        ///   What to tell the player when the escort is refused, in the lord's own terms rather than the rule's.
        ///   Empty when nothing is refused.
        /// </summary>
        public static string Describe(EscortRefusal refusal, string lordName)
        {
            string who = string.IsNullOrWhiteSpace(lordName) ? "He" : lordName.Trim();

            switch (refusal)
            {
                case EscortRefusal.NoPartyOfHisOwn:
                    return $"({who} leads no company of his own to ride with yours.)";
                case EscortRefusal.AtWarWithPlayer:
                    return $"({who} will not ride beside an army his own realm is at war with.)";
                case EscortRefusal.CommittedToWar:
                    return $"({who} is already committed elsewhere, and his men are not his to spare today.)";
                case EscortRefusal.AlreadyEscorting:
                    return $"({who} is already riding escort, and will not promise it twice.)";
                default:
                    return "";
            }
        }
    }
}
