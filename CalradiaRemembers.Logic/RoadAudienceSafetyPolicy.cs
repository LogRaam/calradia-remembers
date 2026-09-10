// Code written by Gabriel Mailhot, 08/09/2026.
// Player report (Nexus, 2026-09-08): a travelling companion never asks for a word unless the player enters a
// settlement. Gabriel's ruling adds a ROAD trigger, but it must respect everything the settlement path already
// respects (SettlementAudienceSafetyPolicy's hostility gate, the timing guard in
// CompanionAudienceTopicBehavior.OnSettlementEntered) plus the road's OWN hazards: a road check runs on a plain
// daily tick, not a deliberate arrival, so it can land in the middle of far more states than "just walked into
// a town" ever could (mid-battle, mid-siege, inside a menu, a prisoner's cell...).
namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure gate: is RIGHT NOW, on the open road, a moment where a travelling companion may ask the player
    ///   for a private word? The host gathers the engine facts; this decides. Any one of the listed conditions
    ///   blocks the offer, mirroring InterceptionBehavior's own "PlayerIsFreeOnMap" reasoning (the analogous
    ///   "is it safe to raise something with the player right now" read for a lord's road pursuit).
    /// </summary>
    public static class RoadAudienceSafetyPolicy
    {
        /// <param name="inMission">A 3D scene is loaded (battle, siege assault, conversation scene, duel...).</param>
        /// <param name="inConversation">A vanilla one-to-one conversation is already under way.</param>
        /// <param name="playerIsPrisoner">The player is held captive: no private word to be had with a captor watching.</param>
        /// <param name="inSettlement">The player's party is sitting inside a settlement already: that is the settlement trigger's own moment, not the road's.</param>
        /// <param name="inMapEvent">The player's party is engaged in an ongoing map event (a battle, a raid, a siege assault).</param>
        /// <param name="inSiegeEvent">The player's party is besieging, or is itself besieged.</param>
        /// <param name="inEncounter">A PlayerEncounter is already resolving (a meeting or fight about to open).</param>
        /// <param name="inMenu">A campaign menu is open (village/town/prisoner/quest menus and the like).</param>
        /// <param name="chatOpen">The CR chat overlay is already open.</param>
        /// <param name="chatStaging">A CR conversation scene (or the council's own) is in the middle of staging.</param>
        /// <param name="atSea">
        ///   The player is sailing (War Sails: MobileParty.IsCurrentlyAtSea). Added 2026-09-10 after a crash
        ///   report: "My companion wanted a word at sea and it kept open the window to choose not now or have a
        ///   word while simultaneously opening the chat window. When we had our conversation, i ended it, and it
        ///   caused a crash." Nothing in the list above is true on open water, so the offer sailed straight
        ///   through every guard and staged a conversation on a screen no CR conversation has ever been staged
        ///   on. This does not claim to explain the crash, which still wants a log; it stops the mod walking
        ///   unbidden into the place the crash was found, which is a different and smaller claim.
        /// </param>
        public static bool IsSafeForPrivateWord(
            bool inMission, bool inConversation, bool playerIsPrisoner, bool inSettlement,
            bool inMapEvent, bool inSiegeEvent, bool inEncounter, bool inMenu, bool chatOpen, bool chatStaging,
            bool atSea = false)
            => !inMission && !inConversation && !playerIsPrisoner && !inSettlement
               && !inMapEvent && !inSiegeEvent && !inEncounter && !inMenu && !chatOpen && !chatStaging
               && !atSea;
    }
}
