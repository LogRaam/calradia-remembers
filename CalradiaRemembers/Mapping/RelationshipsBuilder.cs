// Code written by Gabriel Mailhot, 30/05/2026.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Builds a human-readable RELATIONSHIPS block for an NPC from live game state.
    ///   Uses the same relation data the vanilla encyclopedia displays (Friends / Enemies)
    ///   plus family and liege, so the block matches what a player can verify in-game.
    ///   Refreshed every session launch — not persisted as identity data.
    /// </summary>
    public static class RelationshipsBuilder
    {
        private const int FriendThreshold = 10;
        private const int EnemyThreshold  = -10;
        private const int MaxEntries      = 6;

        public static string Build(Hero hero)
        {
            if (hero == null) return "";

            var sb = new StringBuilder();

            AppendLiege(sb, hero);
            AppendFriends(sb, hero);
            AppendEnemies(sb, hero);
            AppendFamily(sb, hero);

            return sb.ToString().TrimEnd();
        }

        #region private

        private static void AppendLiege(StringBuilder sb, Hero hero)
        {
            Hero kingdomLeader = hero.Clan?.Kingdom?.Leader;
            if (kingdomLeader != null && kingdomLeader != hero)
            {
                sb.AppendLine(
                    $"Liege: {kingdomLeader.Name}, ruler of {hero.Clan.Kingdom.Name}.");
                return;
            }

            Hero clanLeader = hero.Clan?.Leader;
            if (clanLeader != null && clanLeader != hero)
                sb.AppendLine($"Clan chief: {clanLeader.Name} leads your clan.");
        }

        private static void AppendFriends(StringBuilder sb, Hero hero)
        {
            List<Hero> friends = Hero.AllAliveHeroes
                .Where(h => h != hero
                         && !h.IsHumanPlayerCharacter
                         && CharacterRelationManager.GetHeroRelation(hero, h) >= FriendThreshold)
                .OrderByDescending(h => CharacterRelationManager.GetHeroRelation(hero, h))
                .Take(MaxEntries)
                .ToList();

            if (friends.Count > 0)
                sb.AppendLine($"Friends: {string.Join(", ", friends.Select(h => h.Name))}.");
        }

        private static void AppendEnemies(StringBuilder sb, Hero hero)
        {
            List<Hero> enemies = Hero.AllAliveHeroes
                .Where(h => h != hero
                         && !h.IsHumanPlayerCharacter
                         && CharacterRelationManager.GetHeroRelation(hero, h) <= EnemyThreshold)
                .OrderBy(h => CharacterRelationManager.GetHeroRelation(hero, h))
                .Take(MaxEntries)
                .ToList();

            if (enemies.Count > 0)
                sb.AppendLine($"Enemies: {string.Join(", ", enemies.Select(h => h.Name))}.");
        }

        private static void AppendFamily(StringBuilder sb, Hero hero)
        {
            var parts = new List<string>();

            if (hero.Spouse != null && hero.Spouse.IsAlive)
                parts.Add($"Spouse: {hero.Spouse.Name}");

            if (hero.Father != null)
                parts.Add($"Father: {hero.Father.Name}");

            if (hero.Mother != null)
                parts.Add($"Mother: {hero.Mother.Name}");

            var aliveChildren = hero.Children?.Where(c => c.IsAlive).ToList();
            if (aliveChildren != null && aliveChildren.Count > 0)
                parts.Add($"Children: {string.Join(", ", aliveChildren.Select(c => c.Name))}");

            if (parts.Count > 0)
                sb.AppendLine($"Family: {string.Join("; ", parts)}.");
        }

        #endregion
    }
}
