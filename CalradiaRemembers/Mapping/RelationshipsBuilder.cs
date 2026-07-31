// Code written by Gabriel Mailhot, 17/06/2026.

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

      /// <summary>
      ///   Recently-dead foes the NPC would still remember — a hero is dropped from the live enemy
      ///   scan the moment they die, so without this an NPC denies ever knowing a rival who just
      ///   fell (tester: a lord did not recognize a recently-killed enemy). Limited to deaths within
      ///   the last ~2 Calradian years so the list stays short and relevant.
      /// </summary>
      private static void AppendDeadRivals(StringBuilder sb, Hero hero)
      {
         double now = CampaignTime.Now.ToDays;

         List<Hero> deadRivals = Hero.DeadOrDisabledHeroes
                                     .Where(h => h is {IsHumanPlayerCharacter: false, IsAlive: false, DeathDay.ToDays: > 0} &&
                                                 now - h.DeathDay.ToDays >= 0 &&
                                                 now - h.DeathDay.ToDays <= 168 // ~2 Calradian years
                                                 &&
                                                 CharacterRelationManager.GetHeroRelation(hero, h) <= EnemyThreshold)
                                     .OrderByDescending(h => h.DeathDay.ToDays)
                                     .Take(4)
                                     .ToList();

         if (deadRivals.Count > 0)
            sb.AppendLine($"Recent rivals now dead (you remember them): " +
                          $"{string.Join(", ", deadRivals.Select(h => $"the late {h.Name}{DescribeDeath(h)}"))}.");
      }

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

         AppendDeadRivals(sb, hero);
      }

      private static void AppendFamily(StringBuilder sb, Hero hero)
      {
         var parts = new List<string>();

         // Spouse, with their own parentage so in-laws are known (tester: a wife did not realize
         // her husband was the ruler's eldest son, and invented his family).
         if (hero.Spouse is {IsAlive: true})
            parts.Add($"Spouse: {hero.Spouse.Name}{DescribeParentage(hero.Spouse)}");

         // Parents are listed whether alive or dead, but a deceased one is marked as such —
         // otherwise the NPC reads "Father: X" and speaks of a dead parent as living,
         // even denying a death the player witnessed.
         if (hero.Father != null)
            parts.Add($"Father: {FormatKin(hero.Father)}");

         if (hero.Mother != null)
            parts.Add($"Mother: {FormatKin(hero.Mother)}");

         // Children, EACH with their sex named — otherwise the NPC misgenders them (tester: a
         // mother called her daughter her "son"). Co-parent is named when not the current spouse
         // so the LLM does not treat a step-child as a shared child (tester: a lord's son from
         // a deceased first wife was addressed as "our child" by the player-character spouse).
         List<Hero> aliveChildren = hero.Children?.Where(c => c.IsAlive).ToList();
         if (aliveChildren is {Count: > 0})
            parts.Add($"Children: {string.Join(", ", aliveChildren.Select(c => DescribeChild(c, hero)))}");


         // Siblings, named with sex, so the NPC knows their own brothers and sisters.
         List<Hero> siblings = hero.Siblings?.Where(s => s != null).ToList();
         if (siblings is {Count: > 0})
            parts.Add($"Siblings: {string.Join(", ", siblings.Select(DescribeRelative))}");

         if (parts.Count > 0)
            sb.AppendLine($"Family: {string.Join("; ", parts)}.");
      }

      private static void AppendFriends(StringBuilder sb, Hero hero)
      {
         var friends = new List<Hero>();
         foreach (Hero hero1 in Hero.AllAliveHeroes.Where(h => h != hero && !h.IsHumanPlayerCharacter && CharacterRelationManager.GetHeroRelation(hero, h) >= FriendThreshold)
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

      /// <summary>
      ///   Names a child with their sex. When the child's other parent is not the NPC's current
      ///   spouse (e.g. a child from a deceased first wife), that parent is named inline so the
      ///   LLM understands the child is NOT shared with the current spouse.
      /// </summary>
      private static string DescribeChild(Hero child, Hero npcHero)
      {
         string sex = child.IsFemale
            ? "female"
            : "male";
         Hero other = child.Father == npcHero
            ? child.Mother
            : child.Father;

         string baseName = child.IsAlive
            ? $"{child.Name} ({sex}, age {(int) child.Age}"
            : $"the late {child.Name} ({sex}";

         if (other == null || other == npcHero.Spouse)
            return baseName + ")";

         string coParent = other.IsAlive
            ? other.Name.ToString()
            : $"the late {other.Name}";

         return $"{baseName}, by {coParent})";
      }

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

            string when = daysAgo < 1 ? "this very day"
               : daysAgo < 84 ? $"{(int) daysAgo} days ago"
               : $"about {(int) (daysAgo / 84)} year(s) ago"; // Calradian year = 84 days

            string cause;
            switch (kin.DeathMark.ToString())
            {
               case "DiedInBattle": cause = "fell in battle"; break;
               case "DiedOfOldAge": cause = "died of old age"; break;
               case "Executed": cause = "was executed"; break;
               case "Murdered": cause = "was murdered"; break;
               case "DiedInLabor": cause = "died in childbirth"; break;
               default: cause = "died"; break;
            }

            return $" (who {cause} {when})";
         }
         catch
         {
            return "";
         }
      }

      /// <summary>" (son of X)" / " (daughter of X)" — a relative's parentage, for in-law awareness.</summary>
      private static string DescribeParentage(Hero h)
      {
         Hero parent = h.Father ?? h.Mother;

         if (parent == null) return "";
         string rel = h.IsFemale
            ? "daughter"
            : "son";

         return $" ({rel} of {parent.Name})";
      }

      /// <summary>Names a relative with their sex (so they are never misgendered), marking the deceased.</summary>
      private static string DescribeRelative(Hero h)
      {
         string sex = h.IsFemale
            ? "female"
            : "male";

         return h.IsAlive
            ? $"{h.Name} ({sex}, age {(int) h.Age})"
            : $"the late {h.Name} ({sex})";
      }

      /// <summary>Names a relative, marking the deceased as "the late …" so the NPC speaks of them accurately.</summary>
      private static string FormatKin(Hero kin)
         => kin.IsAlive
            ? $"{kin.Name} (age {(int) kin.Age})"
            : $"the late {kin.Name}{DescribeDeath(kin)}";

      #endregion
   }
}