// Code written by Gabriel Mailhot, 30/05/2026.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;

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
      private const int EnemyThreshold = -10;
      private const int FriendThreshold = 10;
      private const int MaxEntries = 6;

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

      private static void AppendEnemies(StringBuilder sb, Hero hero)
      {
         var enemies = new List<Hero>();
         foreach (Hero hero1 in Hero.AllAliveHeroes
                                    .Where(h => h != hero && !h.IsHumanPlayerCharacter)
                                    .Select(h => (h, rel: CharacterRelationManager.GetHeroRelation(hero, h)))
                                    .Where(x => x.rel <= EnemyThreshold)
                                    .OrderBy(x => x.rel)
                                    .Take(MaxEntries)
                                    .Select(x => x.h))
            enemies.Add(hero1);

         if (enemies.Count > 0)
            sb.AppendLine($"Enemies: {string.Join(", ", enemies.Select(h => h.Name))}.");
      }

      private static void AppendFamily(StringBuilder sb, Hero hero)
      {
         var parts = new List<string>();

         if (hero.Spouse is {IsAlive: true})
            parts.Add($"Spouse: {hero.Spouse.Name}");

         // Parents are listed whether alive or dead, but a deceased one is marked as such —
         // otherwise the NPC reads "Father: X" and speaks of a dead parent as living,
         // even denying a death the player witnessed.
         if (hero.Father != null)
            parts.Add($"Father: {FormatKin(hero.Father)}");

         if (hero.Mother != null)
            parts.Add($"Mother: {FormatKin(hero.Mother)}");

         List<Hero> aliveChildren = hero.Children?.Where(c => c.IsAlive).ToList();
         if (aliveChildren is {Count: > 0})
            parts.Add($"Children: {string.Join(", ", aliveChildren.Select(c => c.Name))}");

         if (parts.Count > 0)
            sb.AppendLine($"Family: {string.Join("; ", parts)}.");
      }

      /// <summary>Names a relative, marking the deceased as "the late …" so the NPC speaks of them accurately.</summary>
      private static string FormatKin(Hero kin)
         => kin.IsAlive ? kin.Name.ToString() : $"the late {kin.Name}{DescribeDeath(kin)}";

      /// <summary>
      ///   How and how long ago a dead relative died — " (who fell in battle 6 days ago)".
      ///   Without this the LLM invents both ("three winters ago" for a father who died
      ///   last week in a siege — tester report). Empty when the game has no usable
      ///   death record (pre-campaign deaths often carry no DeathDay).
      /// </summary>
      private static string DescribeDeath(Hero kin)
      {
         try
         {
            if (kin.DeathDay.ToDays <= 0) return "";
            double daysAgo = CampaignTime.Now.ToDays - kin.DeathDay.ToDays;
            // Negative or absurdly old (> ~100 Calradian years) means the record is
            // not real campaign history — say nothing rather than something false.
            if (daysAgo < 0 || daysAgo > 8400) return "";

            string when = daysAgo < 1  ? "this very day"
                        : daysAgo < 84 ? $"{(int) daysAgo} days ago"
                                       : $"about {(int) (daysAgo / 84)} year(s) ago"; // Calradian year = 84 days

            string cause;
            switch (kin.DeathMark.ToString())
            {
               case "DiedInBattle": cause = "fell in battle"; break;
               case "DiedOfOldAge": cause = "died of old age"; break;
               case "Executed":     cause = "was executed"; break;
               case "Murdered":     cause = "was murdered"; break;
               case "DiedInLabor":  cause = "died in childbirth"; break;
               default:             cause = "died"; break;
            }

            return $" (who {cause} {when})";
         }
         catch
         {
            return "";
         }
      }

      private static void AppendFriends(StringBuilder sb, Hero hero)
      {
         var friends = new List<Hero>();
         foreach (Hero hero1 in Hero.AllAliveHeroes.Where(h => h != hero
                                                               && !h.IsHumanPlayerCharacter
                                                               && CharacterRelationManager.GetHeroRelation(hero, h) >= FriendThreshold)
                                    .OrderByDescending(h => CharacterRelationManager.GetHeroRelation(hero, h))
                                    .Take(MaxEntries)) friends.Add(hero1);

         if (friends.Count > 0)
            sb.AppendLine($"Friends: {string.Join(", ", friends.Select(h => h.Name))}.");
      }

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

      #endregion
   }
}