using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

namespace CalradiaRemembers.GameState
{
    /// <summary>
    /// Read-only abstraction over TaleWorlds' campaign state for everything
    /// the mod needs to know about the game world. All game-state lookups
    /// should go through this interface — no direct <c>Hero.MainHero</c>
    /// or <c>Campaign.Current</c> calls anywhere else in the mod.
    ///
    /// In a later phase we will add write operations
    /// (ChangeRelation, AddItem, etc.) here as well.
    /// </summary>
    public interface IGameStateBridge
    {
        /// <summary>Current diplomatic posture between the player and the NPC's faction.</summary>
        DiplomaticStatus GetDiplomaticStatus(Hero npc);

        /// <summary>The player's structural standing vis-à-vis this NPC (free, captive, vassal, etc.).</summary>
        PlayerStatusVsNpc GetPlayerStatus(Hero npc);

        /// <summary>The kind of scene the player is currently in (outdoor, settlement, keep, etc.).</summary>
        SceneType GetCurrentScene();

        /// <summary>The current in-game day (from <c>CampaignTime.Now</c>).</summary>
        int GetCurrentGameDay();
    }
}
