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
        ///   What the 1:1 path weighs before showing the LLM swear_oath. FIVE booleans in a row, two of
        ///   them requirements and three of them exclusions, guarding the empty-promise class the whole
        ///   conditional-teaching contract exists to prevent.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class OathOfferFacts
        {
           /// <summary>They are alive.</summary>
           public bool NpcAlive { get; set; }

           /// <summary>They are held prisoner, and a captive swears nothing.</summary>
           public bool NpcIsPrisoner { get; set; }

           /// <summary>They ARE the player, who does not swear to themselves.</summary>
           public bool NpcIsPlayer { get; set; }

           /// <summary>A vow is already outstanding. One at a time, the same gate pledge_against shares.</summary>
           public bool NpcHasActiveCommitment { get; set; }

           /// <summary>
           ///   At least one whitelisted oath kind plausibly fits them: gold to pay, a party to protect with,
           ///   or a faction of their own to keep peace for. WHICH kind is re-decided at the lift.
           /// </summary>
           public bool NpcQualifiesForSomeKind { get; set; }
        }

        /// <summary>
        ///   Whether the 1:1 path should teach the conversation NPC the swear_oath action this turn. True only when
        ///   they are alive, not held prisoner, not the player themselves, carry NO commitment already outstanding
        ///   (<c>NpcHasActiveCommitment</c>, one vow at a time), and plausibly qualify for at least one
        ///   whitelisted oath kind (<c>NpcQualifiesForSomeKind</c>: holds gold for pay_gold, leads a
        ///   party of their own for protect, or leads their own faction for keep_peace). WHICH kind actually fits,
        ///   and every other invariant, is re-validated at the lift/bridge regardless
        ///   (CourtActionResolver.TrySwearOath, CouncilLift.SettleSwearOath): this only decides whether to OFFER.
        /// </summary>
        public static bool CanOfferOath(OathOfferFacts facts)
        {
           if (facts == null) return false;

           bool npcAlive = facts.NpcAlive;
           bool npcIsPrisoner = facts.NpcIsPrisoner;
           bool npcIsPlayer = facts.NpcIsPlayer;
           bool npcHasActiveCommitment = facts.NpcHasActiveCommitment;
           bool npcQualifiesForSomeKind = facts.NpcQualifiesForSomeKind;

           return npcAlive && !npcIsPrisoner && !npcIsPlayer && !npcHasActiveCommitment && npcQualifiesForSomeKind;
        }
    }
}
