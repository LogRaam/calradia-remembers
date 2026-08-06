// Code written by Gabriel Mailhot, 05/08/2026.
// swear_oath's 1:1 conversation TEACHING gate, added when swear_oath (R7-light, COUNCIL_ACTIONS.md's Partie 8) was
// brought to the 1:1 pipeline alongside the council on 2026-08-05. What it decides: whether the LLM is even SHOWN
// the swear_oath action this turn, from the plain engine facts the mod's EncounterContextBuilder reads about the ONE
// conversation NPC. What it feeds: PromptBuilder.AppendSwearOathOffer's gate. What breaks if it is wrong: it guards
// the empty-promise class the whole conditional-teaching contract exists to prevent. It mirrors the council's own
// offering eligibility for swear_oath (ChatViewModel.AnySeatedMemberCanSwearOath): alive, free, not the player, no
// commitment already outstanding (one vow at a time, the SAME CommitmentBehavior.HasActive gate pledge_against
// shares), and plausibly qualifying for at least one of the three whitelisted oath kinds. Every one of these is
// re-checked at commit by CourtActionResolver.TrySwearOath (the prompt is advice, the bridge is law), so a true
// answer here only ever OFFERS a teaching, it never records an oath.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure 1:1 teaching gate for swear_oath: mirrors the council's own swearer eligibility.</summary>
    public static class OathOfferPolicy
    {
        /// <summary>
        ///   Whether the 1:1 path should teach the conversation NPC the swear_oath action this turn. True only when
        ///   they are alive, not held prisoner, not the player themselves, carry NO commitment already outstanding
        ///   (<paramref name="npcHasActiveCommitment" />, one vow at a time), and plausibly qualify for at least one
        ///   whitelisted oath kind (<paramref name="npcQualifiesForSomeKind" />: holds gold for pay_gold, leads a
        ///   party of their own for protect, or leads their own faction for keep_peace). WHICH kind actually fits,
        ///   and every other invariant, is re-validated at the lift/bridge regardless
        ///   (CourtActionResolver.TrySwearOath, CouncilLift.SettleSwearOath): this only decides whether to OFFER.
        /// </summary>
        public static bool CanOfferOath(bool npcAlive, bool npcIsPrisoner, bool npcIsPlayer, bool npcHasActiveCommitment, bool npcQualifiesForSomeKind)
            => npcAlive && !npcIsPrisoner && !npcIsPlayer && !npcHasActiveCommitment && npcQualifiesForSomeKind;
    }
}
