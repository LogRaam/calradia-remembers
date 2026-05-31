// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.GameState
{
    /// <summary>
    ///   Concrete implementation of <see cref="IGameStateBridge" /> using the
    ///   TaleWorlds campaign API. This is the ONLY class in the mod that should
    ///   reach directly into <c>Hero.MainHero</c>, <c>Campaign.Current</c>,
    ///   <c>FactionManager</c>, etc. Everything else uses the abstraction.
    /// </summary>
    public sealed class BannerlordGameStateBridge : IGameStateBridge
    {
        public int GetCurrentGameDay()
            => (int) CampaignTime.Now.ToDays;

        public SceneType GetCurrentScene()
        {
            Settlement settlement = Hero.MainHero?.CurrentSettlement ?? PlayerEncounter.LocationEncounter?.Settlement;

            if (settlement == null)
                return SceneType.Outdoor;

            if (settlement.IsCastle || settlement.IsFortification)
                return SceneType.Keep;

            if (settlement.IsTown || settlement.IsVillage)
                return SceneType.Settlement;

            return SceneType.Outdoor;
        }

        public DiplomaticStatus GetDiplomaticStatus(Hero npc)
        {
            if (npc == null || npc.MapFaction == null || Hero.MainHero?.MapFaction == null)
                return DiplomaticStatus.Unknown;

            // Same faction (or same clan, both treated as "allied" here)
            if (npc.MapFaction == Hero.MainHero.MapFaction)
                return DiplomaticStatus.Allied;

            if (FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction))
                return DiplomaticStatus.AtWar;

            return DiplomaticStatus.AtPeace;
        }

        public string GetSeasonName()
        {
            // Bannerlord year = 84 campaign days (21 per season).
            int dayOfYear = (int)(CampaignTime.Now.ToDays % 84);
            if (dayOfYear < 21) return "Spring";
            if (dayOfYear < 42) return "Summer";
            if (dayOfYear < 63) return "Autumn";
            return "Winter";
        }

        public string GetActiveConflicts()
        {
            if (Kingdom.All == null) return "";

            var seen = new HashSet<string>();
            var wars = new List<string>();
            var kingdoms = Kingdom.All.Where(k => !k.IsEliminated).ToList();

            for (int i = 0; i < kingdoms.Count; i++)
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
                Kingdom a = kingdoms[i];
                Kingdom b = kingdoms[j];
                if (FactionManager.IsAtWarAgainstFaction(a, b))
                    wars.Add($"{a.Name} vs {b.Name}");
            }

            return wars.Count > 0 ? string.Join("; ", wars) : "";
        }

        public PlayerStatusVsNpc GetPlayerStatus(Hero npc)
        {
            if (npc == null || Hero.MainHero == null)
                return PlayerStatusVsNpc.Unknown;

            // Player is the NPC's captive
            if (Hero.MainHero.IsPrisoner && Hero.MainHero.PartyBelongedToAsPrisoner?.LeaderHero == npc)
                return PlayerStatusVsNpc.Captive;

            // NPC is the player's captive
            if (npc.IsPrisoner && npc.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero)
                return PlayerStatusVsNpc.NpcIsCaptive;

            // Same clan
            if (npc.Clan != null && npc.Clan == Clan.PlayerClan)
                return PlayerStatusVsNpc.ClanMember;

            // Player is a vassal of the NPC's kingdom (and NPC leads it)
            if (Hero.MainHero.Clan?.Kingdom?.Leader == npc && npc != Hero.MainHero)
                return PlayerStatusVsNpc.Liege;

            // NPC is a vassal of the player's kingdom (player leads it)
            if (npc.Clan?.Kingdom?.Leader == Hero.MainHero && Hero.MainHero.Clan?.Kingdom != null)
                return PlayerStatusVsNpc.Vassal;

            return PlayerStatusVsNpc.Free;
        }
    }
}