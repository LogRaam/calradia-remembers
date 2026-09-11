// Code written by Gabriel Mailhot, 08/09/2026, reworked 10/09/2026.
// Player report (Nexus, 2026-09-08): a travelling companion never asks for a word unless the player enters a
// settlement. Gabriel's ruling adds a ROAD trigger, but it must respect everything the settlement path already
// respects (SettlementAudienceSafetyPolicy's hostility gate, the timing guard in
// CompanionAudienceTopicBehavior.OnSettlementEntered) plus the road's OWN hazards: a road check runs on a plain
// daily tick, not a deliberate arrival, so it can land in the middle of far more states than "just walked into
// a town" ever could (mid-battle, mid-siege, inside a menu, a prisoner's cell...).
//
// 10/09/2026: ELEVEN BOOLEANS BECAME A FACTS OBJECT, and the reason is correctness before it is testing.
//
//   IsSafeForPrivateWord(inMission, inConversation, playerIsPrisoner, inSettlement, inMapEvent, inSiegeEvent,
//                        inEncounter, inMenu, chatOpen, chatStaging, atSea)
//
// Eleven parameters of the SAME TYPE in a row. Swap any two and the compiler says nothing, and no test catches it
// on a day when both happen to hold the same value. The only reason it had not gone wrong is that every caller
// wrote the argument names out by hand, which is the language admitting the signature is unsafe.
//
// The eleventh, atSea, was bolted on the day before with a DEFAULT VALUE so existing callers would keep
// compiling. That is the smell in its purest form: dodging the cost of the shape instead of fixing it.
//
// Gabriel, 10/09/2026: "Le code doit pouvoir changer, evoluer, s'ameliorer, mais le comportement attendu ne doit
// jamais se degrader." A facts object is how that is bought here. A test now names the ONE hazard it is about and
// says nothing of the other ten, so the twelfth hazard costs no existing test anything, and no caller can pass
// "in a siege" where "in a menu" was meant.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Everything that could make right now a bad moment to interrupt the player, read off the live campaign
    ///   by the caller. Every one of these BLOCKS the offer; none of them permits it.
    /// </summary>
    public sealed class RoadAudienceFacts
    {
        /// <summary>A 3D scene is loaded (battle, siege assault, conversation scene, duel...).</summary>
        public bool InMission { get; set; }

        /// <summary>A vanilla one-to-one conversation is already under way.</summary>
        public bool InConversation { get; set; }

        /// <summary>The player is held captive: no private word to be had with a captor watching.</summary>
        public bool PlayerIsPrisoner { get; set; }

        /// <summary>
        ///   The player's party is sitting inside a settlement already: that is the settlement trigger's own
        ///   moment, not the road's.
        /// </summary>
        public bool InSettlement { get; set; }

        /// <summary>The player's party is engaged in an ongoing map event (a battle, a raid, a siege assault).</summary>
        public bool InMapEvent { get; set; }

        /// <summary>The player's party is besieging, or is itself besieged.</summary>
        public bool InSiegeEvent { get; set; }

        /// <summary>A PlayerEncounter is already resolving (a meeting or fight about to open).</summary>
        public bool InEncounter { get; set; }

        /// <summary>A campaign menu is open (village/town/prisoner/quest menus and the like).</summary>
        public bool InMenu { get; set; }

        /// <summary>The CR chat overlay is already open.</summary>
        public bool ChatOpen { get; set; }

        /// <summary>A CR conversation scene (or the council's own) is in the middle of staging.</summary>
        public bool ChatStaging { get; set; }

        /// <summary>
        ///   The player is sailing (War Sails: MobileParty.IsCurrentlyAtSea). Added 2026-09-10 after a crash
        ///   report: "My companion wanted a word at sea and it kept open the window to choose not now or have a
        ///   word while simultaneously opening the chat window. When we had our conversation, i ended it, and it
        ///   caused a crash." Not one fact above is true on open water, so the offer sailed through every guard
        ///   and staged a conversation on a screen no CR conversation has ever been staged on. This does not
        ///   claim to explain the crash, which still wants a log; it stops the mod walking unbidden into the
        ///   place the crash was found.
        /// </summary>
        public bool AtSea { get; set; }
    }

    /// <summary>
    ///   Pure gate: is RIGHT NOW, on the open road, a moment where a travelling companion may ask the player
    ///   for a private word? The host gathers the engine facts; this decides. Any one of them blocks the offer,
    ///   mirroring InterceptionBehavior's own "PlayerIsFreeOnMap" reasoning (the analogous "is it safe to raise
    ///   something with the player right now" read for a lord's road pursuit).
    /// </summary>
    public static class RoadAudienceSafetyPolicy
    {
        /// <summary>
        ///   True only when nothing at all stands in the way. Facts we could not read block the offer: this runs
        ///   unattended every day, so a broken read costs one quiet day, whereas failing open risks popping an
        ///   inquiry mid-battle on the one day the read breaks.
        /// </summary>
        public static bool IsSafeForPrivateWord(RoadAudienceFacts facts)
            => facts != null
               && !facts.InMission && !facts.InConversation && !facts.PlayerIsPrisoner && !facts.InSettlement
               && !facts.InMapEvent && !facts.InSiegeEvent && !facts.InEncounter && !facts.InMenu
               && !facts.ChatOpen && !facts.ChatStaging && !facts.AtSea;
    }
}
