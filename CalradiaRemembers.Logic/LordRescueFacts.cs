// Code written by Gabriel Mailhot, 19/07/2026.
// What the engine can say about one lord, at one moment, while the player sits in a captor's hands. A
// plain carrier in the same spirit as AppearanceFacts and PlayerStandingFacts: the mod reads TaleWorlds
// state and fills this in, and the pure policy turns it into a verdict. Nothing here knows what a Hero is.
//
// Ratified with Gabriel 2026-07-18/19. The design came out of a real campaign moment: a lord fell upon the
// bandits holding the player, she slipped away in the confusion, and the whole thing was an accident of
// vanilla AI. Gabriel's call was to make it deliberate instead: a liberation is triggered by CR, rationed
// so that being a prisoner keeps its weight, and a lord who has NO interest in freeing the player simply
// does not come.
//
// The eligibility rule is not "who likes the player enough". That reading was corrected by the transcript
// itself: the lord in question framed it as DUTY, not affection ("a lord of the Empire does not hear that
// a woman on the road has been dragged off by bandits and simply ride on"). So an honourable stranger is a
// legitimate rescuer, and the policy carries a duty path beside the bonds of blood, love and hatred.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Everything needed to decide whether one lord rides to break a captivity. Each field is a fact the
    ///   engine can answer directly, never a conclusion.
    /// </summary>
    public sealed class LordRescueFacts
    {
        /// <summary>
        ///   The player has actually LIVED the captivity (its scene has played, or its equivalent has been
        ///   recorded). Gabriel's gate, and the reason it exists: a rescue that can fire immediately would
        ///   cancel the captivity instead of ending it, and being a prisoner would stop costing anything.
        /// </summary>
        public bool CaptivityWasExperienced { get; init; } = false;

        /// <summary>Alive, at liberty, and leading a party that can actually ride.</summary>
        public bool LordIsFreeToRide { get; init; } = false;

        /// <summary>Close enough to plausibly reach the captors.</summary>
        public bool WithinRange { get; init; } = false;

        /// <summary>One attempt per capture, as the companion rescue already works. Rationing, not realism.</summary>
        public bool AlreadyAttemptedThisCaptivity { get; init; } = false;

        /// <summary>
        ///   The captors are a bandit band. Scope decision for the first increment: the engine forbids a
        ///   party attacking one it is not hostile to, so a LORD captor could only ever be assaulted where a
        ///   war already exists, which is a separate rule with separate consequences.
        /// </summary>
        public bool CaptorIsBandit { get; init; } = false;

        /// <summary>This lord belongs to the player's own clan: their own kin is in a cage.</summary>
        public bool SharesPlayerClan { get; init; } = false;

        /// <summary>Spouse, lover, or an acknowledged courtship.</summary>
        public bool HasRomanticBond { get; init; } = false;

        /// <summary>One of them is sworn to the other's realm, in either direction.</summary>
        public bool BoundByOath { get; init; } = false;

        /// <summary>
        ///   This lord already has a grievance against the party holding the player. He was arguably going to
        ///   ride at them anyway, and the rescue is the happy consequence rather than the motive.
        /// </summary>
        public bool HoldsGrudgeAgainstCaptor { get; init; } = false;

        /// <summary>The mod's OWN personal regard ledger, not the vanilla clan relation.</summary>
        public int PersonalRegard { get; init; } = 0;

        /// <summary>Vanilla trait value, conventionally -2..2.</summary>
        public int Honor { get; init; } = 0;

        /// <summary>Vanilla trait value, conventionally -2..2.</summary>
        public int Mercy { get; init; } = 0;
    }
}
