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
// 10/09/2026, THE BLANKET WAR GATE IS GONE, and a player report is why. "It feels like the AI using this like
// never follows me despite it agreeing to." A gate added 29/08/2026 refused any lord whose realm was at war with
// ANYONE, so that he stayed available for his liege's muster call. The intent was sound and the effect was not:
// in an ordinary campaign nearly every kingdom is at war with somebody nearly all the time, so follow_me was
// almost never taught, and a verb that is not taught is ABSENT rather than refused. The model, told nothing,
// agreed warmly and emitted nothing. The player watched a promise evaporate, over and over, with no explanation.
//
// Gabriel's ruling, 10/09/2026: keep the intent, drop the blanket. A lord is stopped by being COMMITTED to that
// war right now (an army, a siege, a battle), not by its existence somewhere on the map. And, his condition on
// relaxing it: "si il accepte de suivre le joueur et apres il y a un appel pour joindre une armee, ce Lord
// devrait expliquer au joueur qu'il doit le quitter pour suivre l'appel de son seigneur." That half lives in
// EscortContinuationPolicy. RealmAtWar did not disappear here; it changed jobs, from refusing to WARNING.
//
// ---------------------------------------------------------------------------------------------------------
// WHY THIS TAKES A FACTS OBJECT AND NOT FOUR BOOLEANS (Gabriel's ruling on testing, 10/09/2026):
//
//   "Le code doit pouvoir changer, evoluer, s'ameliorer, mais le comportement attendu ne doit jamais se
//    degrader."
//
// This very file is the evidence for that rule and the debt it left. Widening the gate above changed the
// positional signature, and SEVEN existing assertions had to be rewritten, including ones whose behaviour had
// not moved at all: "a lord who leads no party of his own is refused" was as true after as before. They broke on
// ARITY AND PARAMETER NAMES. They were bound to the shape of the call rather than to anything a player would
// notice, which is the exact failure the rule is about, in its second and less obvious form.
//
// A named facts object fixes it structurally: a test names the facts it cares about and says nothing about the
// others, so adding, removing or reordering a fact costs nothing to any test that never depended on it. This is
// already the house style (8 dedicated *Facts types, 39 files in Logic taking one); this policy was one of the
// older positional stragglers, which is precisely why it was the one that cost seven rewrites.

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

    /// <summary>
    ///   What the escort decision rests on, read off the live campaign by whoever is asking. Named rather than
    ///   positional so a caller cannot pass them in the wrong order and a test cannot bind to their arity.
    /// </summary>
    public sealed class EscortFacts
    {
        /// <summary>
        ///   He truly leads his own mobile party. A companion already riding IN the player's party never does,
        ///   so this alone excludes that case: there is nothing further for follow_me to do for them.
        /// </summary>
        public bool LeadsOwnParty { get; set; }

        /// <summary>His realm and the player's are at war with EACH OTHER, which is not the same as his realm being at war.</summary>
        public bool AtWarWithPlayer { get; set; }

        /// <summary>
        ///   His party is spoken for by a war TODAY: an army, a siege, or a live battle. The successor to the
        ///   old inArmy, widened to include a battle, because all three mean the same thing to a promise.
        /// </summary>
        public bool CommittedToWar { get; set; }

        /// <summary>He is already riding escort for somebody. One at a time.</summary>
        public bool AlreadyEscorting { get; set; }

        /// <summary>
        ///   His realm is at war with somebody, anybody. This REFUSES NOTHING: it is the fact behind
        ///   <see cref="ShouldWarnOfMuster" />, so a lord who may be called away says so as he agrees rather
        ///   than leaving the player to discover it when he rides off. It used to be the fifth refusal, and
        ///   being at war is so nearly always true that it made the whole feature unreachable.
        /// </summary>
        public bool RealmAtWar { get; set; }
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
        ///   Which door is shut, tried in the order a lord would think of them: what he has, who he is at war
        ///   with, what he is doing today, and what he has already promised. Missing facts refuse rather than
        ///   permit, since the safe answer to "I do not know" is that no promise is made.
        /// </summary>
        public static EscortRefusal Refusal(EscortFacts facts)
        {
            if (facts == null) return EscortRefusal.NoPartyOfHisOwn;

            if (!facts.LeadsOwnParty) return EscortRefusal.NoPartyOfHisOwn;
            if (facts.AtWarWithPlayer) return EscortRefusal.AtWarWithPlayer;
            if (facts.CommittedToWar) return EscortRefusal.CommittedToWar;
            if (facts.AlreadyEscorting) return EscortRefusal.AlreadyEscorting;

            return EscortRefusal.None;
        }

        /// <summary>Whether the escort can be promised at all. The bridge asks this; so does the teaching gate.</summary>
        public static bool CanEscort(EscortFacts facts) => Refusal(facts) == EscortRefusal.None;

        /// <summary>
        ///   True when this lord should WARN the player, as he agrees, that he may be called away: his realm is
        ///   at war with someone, so the muster may reach him at any time and the escort will end there
        ///   (EscortEndReason.SummonedByLiege). Said in advance that is a lord being honest; said only
        ///   afterwards it is the broken promise this whole change exists to end.
        /// </summary>
        public static bool ShouldWarnOfMuster(EscortFacts facts) => facts?.RealmAtWar == true;

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
