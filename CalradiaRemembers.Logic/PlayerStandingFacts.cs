// Code written by Gabriel Mailhot, 18/07/2026.
// The engine facts that decide where the player stands relative to one NPC. A plain, engine-agnostic
// carrier in the same spirit as AppearanceFacts: the mod reads TaleWorlds state and fills this in, and the
// pure policy turns it into a verdict. Nothing here knows what a Hero is.
//
// This layer exists because of a shipped bug (JoshGray700, Nexus 2026-07-18, v1.30.16). The mapping from
// engine state to PlayerStatusVsNpc was written straight into BannerlordGameStateBridge, in net472 where no
// test could reach it, and the vassal/liege pair was INVERTED there. The Aserai Sultan was therefore told
// "The player is your liege lord.", denied being Sultan, invented a third-party Sultan to reconcile it, and
// addressed his own new vassal as "my liege".

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Plain, engine-agnostic carrier of everything needed to place the player beside one NPC.
    ///   Each flag is a fact the engine can answer directly, never a conclusion.
    /// </summary>
    public sealed class PlayerStandingFacts
    {
        /// <summary>The player is held prisoner by this NPC's party.</summary>
        public bool PlayerIsCaptiveOfNpc { get; init; } = false;

        /// <summary>This NPC is held prisoner by the player's party.</summary>
        public bool NpcIsCaptiveOfPlayer { get; init; } = false;

        /// <summary>This NPC belongs to the player's own clan.</summary>
        public bool SharesPlayerClan { get; init; } = false;

        /// <summary>
        ///   This NPC is the RULER of the kingdom the player's clan serves. Which makes the NPC the liege and
        ///   the PLAYER the vassal: the direction that shipped inverted.
        /// </summary>
        public bool NpcRulesPlayerRealm { get; init; } = false;

        /// <summary>The player rules the kingdom this NPC's clan serves, making the player the liege.</summary>
        public bool PlayerRulesNpcRealm { get; init; } = false;

        /// <summary>
        ///   The player's clan is under MERCENARY CONTRACT to the kingdom this NPC belongs to. This must be
        ///   asked before either feudal fact, because the engine models a contract by setting the clan's
        ///   Kingdom exactly as it does for a sworn vassal: read without this flag, a hired sword is
        ///   indistinguishable from an oath-bound one, and CR's own join_as_mercenary action creates that
        ///   state. Paid service is not fealty, and the distinction is the whole of the relationship.
        /// </summary>
        public bool PlayerIsMercenaryOfNpcRealm { get; init; } = false;

        /// <summary>
        ///   Both clans serve the same kingdom. On its own (neither ruling, no contract) this makes them
        ///   fellow vassals of one crown, which used to render as nothing at all.
        /// </summary>
        public bool SharesRealm { get; init; } = false;

        /// <summary>
        ///   The mirror of <see cref="PlayerIsMercenaryOfNpcRealm" />: it is the NPC's OWN clan that is hired
        ///   to the realm binding them, rather than sworn to it. Same engine ambiguity, same consequence:
        ///   without it, a lord whose sword the player's crown merely rents is called a sworn subject, and a
        ///   hired band in a realm the player only serves is called a comrade of the same oath.
        /// </summary>
        public bool NpcIsMercenaryOfSharedRealm { get; init; } = false;
    }
}
