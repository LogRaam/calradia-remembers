// Code written by Gabriel Mailhot, 06/09/2026.
// Player violence against an NPC (player request 2026-09-06, SuperGraham339: "I would love the ability, as a player,
// to inflict harm/death to NPC's ... in instances like this one with the village notable who is without guards and
// someone I'd gotten the drop on"). The reporter and Gabriel both named the real difficulty: telling a legitimate
// act of violence from an abuse of the sandbox. This file is the vocabulary; the policy beside it is the ruling.
//
// GABRIEL RATIFIED (2026-09-06): the MCM toggle governs KILLING ONLY (default off, so robbery and assault are always
// available and no one can be killed until the player opts in); the player CAN FAIL; and violence is possible
// EVERYWHERE, with a victim who is not isolated merely being HARDER to reach, never impossible.

namespace CalradiaRemembers.Logic.Violence
{
    /// <summary>What the player is attempting against an NPC, from least to most severe.</summary>
    public enum ViolentAct
    {
        /// <summary>Take their coin by force or threat. The easiest: a knife shown is often enough.</summary>
        Rob,

        /// <summary>Beat them down. They live, and they remember.</summary>
        Assault,

        /// <summary>Kill them. Only ever possible when the player has opted in through the MCM toggle.</summary>
        Kill
    }

    /// <summary>How exposed the victim is, which is what makes the same act easy in a hut and desperate in a hall.</summary>
    public enum ViolenceExposure
    {
        /// <summary>Alone with the player, no one to intervene: the reporter's village notable without guards.</summary>
        Alone,

        /// <summary>Others are about who could raise a cry or lend a hand, though none are soldiers.</summary>
        Onlookers,

        /// <summary>Guards, a retinue, or armed companions stand with them.</summary>
        Guarded
    }

    /// <summary>How an attempt ended.</summary>
    public enum ViolenceVerdict
    {
        /// <summary>The player got what they went for.</summary>
        Succeeded,

        /// <summary>The attempt failed: the victim resisted, or those around them did.</summary>
        Failed,

        /// <summary>The act is not permitted at all in this campaign (killing with the toggle off), so nothing is even attempted.</summary>
        Forbidden
    }

    /// <summary>Everything the ruling needs: how the two compare, who else is present, and the player's craft at this sort of thing.</summary>
    public sealed class ViolenceFacts
    {
        public ViolenceFacts(int playerProwess, int targetProwess, ViolenceExposure exposure, int playerRoguery)
        {
            PlayerProwess = playerProwess;
            TargetProwess = targetProwess;
            Exposure = exposure;
            PlayerRoguery = playerRoguery;
        }

        /// <summary>The player's fighting worth, from the same measure the battle-memory pillar uses (CombatProwessPolicy).</summary>
        public int PlayerProwess { get; }

        /// <summary>The victim's fighting worth, measured the same way, so a champion is a champion in both systems.</summary>
        public int TargetProwess { get; }

        public ViolenceExposure Exposure { get; }

        /// <summary>The player's Roguery. It helps at robbery (a practised shakedown), not at open murder.</summary>
        public int PlayerRoguery { get; }
    }
}
