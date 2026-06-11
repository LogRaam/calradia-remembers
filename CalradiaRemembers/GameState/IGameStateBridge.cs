using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

namespace CalradiaRemembers.GameState
{
    /// <summary>
    /// Abstraction over TaleWorlds' campaign state for everything
    /// the mod needs to read from or write to the game world. All game-state
    /// access should go through this interface — no direct <c>Hero.MainHero</c>
    /// or <c>Campaign.Current</c> calls anywhere else in the mod.
    ///
    /// Sprint 10: write operations added (<see cref="ExecuteAction"/>).
    /// </summary>
    public interface IGameStateBridge
    {
        /// <summary>Current diplomatic posture between the player and the NPC's faction.</summary>
        DiplomaticStatus GetDiplomaticStatus(Hero npc);

        /// <summary>The player's structural standing vis-à-vis this NPC (free, captive, vassal, etc.).</summary>
        PlayerStatusVsNpc GetPlayerStatus(Hero npc);

        /// <summary>The kind of scene the player is currently in (outdoor, settlement, keep, etc.).</summary>
        SceneType GetCurrentScene();

        /// <summary>
        ///   A short, plain description of what <paramref name="npc"/> is currently doing on
        ///   the campaign map — laying siege, marching with an army toward an objective, or
        ///   traveling to a settlement. Returns null when it cannot be read with confidence:
        ///   feeding a guessed destination would let the prompt endorse the very confabulation
        ///   this is meant to prevent, so uncertainty must produce null, not a best guess.
        /// </summary>
        string GetCurrentActivity(Hero npc);

        /// <summary>The current in-game day (from <c>CampaignTime.Now</c>).</summary>
        int GetCurrentGameDay();

        /// <summary>Current season name: Spring, Summer, Autumn, or Winter.</summary>
        string GetSeasonName();

        /// <summary>
        ///   Short summary of active inter-kingdom wars, suitable for injection
        ///   into the world state section of the system prompt.
        ///   Returns an empty string when there are no active wars.
        /// </summary>
        string GetActiveConflicts();

        /// <summary>
        ///   The live game relation between the player and <paramref name="npc"/>
        ///   ([-100, 100]). This is the single authoritative relationship score;
        ///   it is synced into the prompt's CURRENT STANCE each turn. Returns 0 if
        ///   either hero is missing.
        /// </summary>
        int GetPlayerRelation(Hero npc);

        /// <summary>
        ///   Executes a game action emitted by the LLM in an [ACTION] block and
        ///   returns a short outcome to surface in the chat, or null when the action
        ///   changed nothing (unknown type, gated out, invalid parameters).
        /// </summary>
        ActionOutcome ExecuteAction(GameAction action, Hero npcHero, ActionContext context);

        /// <summary>
        ///   Pays the reward promised by a completed informal quest: <paramref name="gold"/>
        ///   denars from the NPC to the player, and <paramref name="relation"/> added to the
        ///   NPC's personal regard. Unlike routine warmth, a verified quest reward bypasses
        ///   the relation cooldown and cap — the deed was confirmed by real game events.
        ///   Returns a combined outcome for the chat, or null when nothing was granted.
        /// </summary>
        ActionOutcome GrantQuestReward(Hero npcHero, NpcProfile profile, int gold, int relation);

        /// <summary>
        ///   Applies the standing penalty for the player being publicly abased before
        ///   <paramref name="witnessCount"/> onlookers (Sprint 17 captive humiliation).
        ///   The clan loses Renown (prestige) and Influence (political authority),
        ///   scaled by the number of witnesses. The world is only mutated when the player
        ///   is genuinely a prisoner — console test scenes report the figures without
        ///   touching the save. Returns the (renownLoss, influenceLoss) magnitudes for the
        ///   chat message; (0, 0) when there are no witnesses.
        /// </summary>
        (int renownLoss, int influenceLoss) ApplyPlayerPublicHumiliation(int witnessCount);
    }
}
