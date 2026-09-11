// Code written by Gabriel Mailhot, 08/07/2026.
// Viable-quest referential (ratified 2026-07-08): scans the LIVE world for the tasks this giver can truly hand
// out right now, so the prompt teaches only a GROUNDED menu (targets named verbatim) and every offer the model
// makes registers by construction. Agreement with QuestFactory is by CONSTRUCTION, not by coincidence: the scan
// calls the factory's own primitives (NearestTownTo / NearestActiveHideout / HideoutSearchRadius), so a target
// the menu names is a target the factory will ground. The formatting core is the pure, unit-tested
// CalradiaRemembers.Logic.QuestMenuComposer; this type is the engine-facing world scan.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>
   ///   Builds the giver's grounded quest menu for the prompt. Cached per giver per game day (the world
   ///   does not shift enough within a day to stale the menu, and a stable menu keeps the prompt prefix
   ///   cacheable across the conversation). Never throws: any failure yields an empty menu, which falls
   ///   back to the SDK's generic catalogue.
   /// </summary>
   internal static class ViableQuestScanner
   {
      /// <summary>TUNE: how many enemy-lord entries a wartime menu offers at most.</summary>
      private const int MaxEnemyLordEntries = 2;

      private static readonly Dictionary<string, (int Day, string Menu)> Cache = new();

      /// <summary>The grounded menu for this giver, or "" when nothing is viable (or the giver is null).</summary>
      public static string BuildMenu(Hero giver, int gameDay)
      {
         if (giver?.StringId == null) return "";
         if (Cache.TryGetValue(giver.StringId, out (int Day, string Menu) hit) && hit.Day == gameDay) return hit.Menu;

         string menu;
         try { menu = QuestMenuComposer.Compose(ScanEntries(giver)); }
         catch (Exception ex)
         {
            ModLog.Error($"[QUEST-MENU] scan failed for {giver.Name}: {ex.GetType().Name}: {ex.Message}");
            menu = "";
         }

         Cache[giver.StringId] = (gameDay, menu);

         return menu;
      }

      #region private

      private static List<QuestMenuEntry> ScanEntries(Hero giver)
      {
         var entries = new List<QuestMenuEntry>();
         Settlement anchor = QuestFactory.NearestTownTo(giver);

         // 1. A real, still-infested lair near the giver's ground: the flagship verifiable deed.
         if (anchor != null)
         {
            Settlement lair = QuestFactory.NearestActiveHideout(anchor.GetPosition2D, QuestFactory.HideoutSearchRadius);
            if (lair != null)
            {
               Vec2 a = anchor.GetPosition2D, h = lair.GetPosition2D;
               string bearing = CardinalDirectionPolicy.Describe(new MapPoint(a.x, a.y).StepTo(new MapPoint(h.x, h.y)));
               entries.Add(new QuestMenuEntry("bandit_hideout", "target_settlement", anchor.Name?.ToString(),
                  $"a bandit lair infests the country {bearing} of {anchor.Name}"));
            }

            // 2. Open-field bandit clearing near the same anchor: offered ONLY when a bandit party actually roams
            // within the SAME radius QuestDeedTracker credits a clear in, so an offered bandit_clear is always
            // completable by construction (player report: a village sent me after bandits that were not there,
            // then turned hostile when I reported none). No roaming bandits nearby means no such task to give.
            if (BanditClearOfferPolicy.ShouldOffer(CountBanditPartiesNear(anchor)))
               entries.Add(new QuestMenuEntry("bandit_clear", "target_settlement", anchor.Name?.ToString(),
                  $"bandits prey on travellers around {anchor.Name}"));
         }

         // 3. Wartime deeds: only when the giver's realm truly wars, targets pulled from the live enemy.
         AddWarEntries(entries, giver);

         // 4. An ally of the giver's realm truly held captive somewhere.
         AddRescueEntry(entries, giver);

         // 5. A letter always travels: a real recipient of the giver's own clan or circle, elsewhere.
         AddLetterEntry(entries, giver);

         // 6. A lord always needs supplies: horses for the cavalry, livestock or grain for the host. Always
         // groundable (the player can bring goods), so this personal task is always on the menu.
         entries.Add(new QuestMenuEntry("provide_goods", null, null,
            "have the player bring you a supply of horses, livestock, or grain (set category + required_count)"));

         return entries;
      }

      /// <summary>
      ///   Counts the bandit parties currently roaming within QuestDeedTracker's own bandit-clear completion
      ///   radius (<see cref="QuestDeedTracker.BanditClearZoneRadius" />) of <paramref name="anchor" />, using the
      ///   SAME bandit predicate the completion path uses (<see cref="IFaction.IsBanditFaction" />), so an offered
      ///   bandit_clear deed is one a real fight in this zone can satisfy. Internal so the self-test drives the
      ///   exact count the scanner gates on. Never throws: an unreadable party is simply skipped.
      /// </summary>
      internal static int CountBanditPartiesNear(Settlement anchor)
      {
         if (anchor == null) return 0;

         Vec2 at = anchor.GetPosition2D;
         var count = 0;
         foreach (MobileParty party in MobileParty.All)
         {
            if (party is not {IsActive: true}) continue;
            if (party.MapFaction?.IsBanditFaction != true) continue;
            if (party.GetPosition2D.Distance(at) > QuestDeedTracker.BanditClearZoneRadius) continue;
            count++;
         }

         return count;
      }

      private static void AddWarEntries(List<QuestMenuEntry> entries, Hero giver)
      {
         if (giver?.MapFaction is not Kingdom mine) return;

         Kingdom enemy = Kingdom.All?.FirstOrDefault(k =>
            k != mine && !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(mine, k));
         if (enemy == null) return;

         string enemyName = enemy.Name?.ToString();

         // Enemy lords in the field: defeat or capture, the two classic war errands.
         List<Hero> lords = Hero.AllAliveHeroes
                                .Where(h => h.MapFaction == enemy && h.IsLord && h != enemy.Leader
                                            && !h.IsPrisoner && h.PartyBelongedTo != null)
                                .Take(MaxEnemyLordEntries)
                                .ToList();
         if (lords.Count > 0)
            entries.Add(new QuestMenuEntry("attack_lord", "target_hero", lords[0].Name?.ToString(),
               $"an enemy of your realm rides for {enemyName}"));
         if (lords.Count > 1)
            entries.Add(new QuestMenuEntry("capture_prisoner", "target_hero", lords[1].Name?.ToString(),
               "bring this enemy back in chains"));

         entries.Add(new QuestMenuEntry("attack_faction", "target_faction", enemyName,
            "strike their parties in the field"));

         Settlement village = Settlement.All?.FirstOrDefault(s => s?.IsVillage == true && s.MapFaction == enemy);
         if (village != null)
            entries.Add(new QuestMenuEntry("raid_village", "target_settlement", village.Name?.ToString(),
               $"put one of {enemyName}'s villages to the torch"));

         // The "come take a castle with us" errand, absent from the grounded menu before (player report
         // 2026-07-17: a lord substituted a generic attack-faction quest for a siege the player asked for).
         // Prefer a stronghold already under siege so the deed is achievable now, else any enemy fortification.
         // Same predicate BindSettlement(requireFortification) grounds, so a named target always registers.
         Settlement fortress =
            Settlement.All?.FirstOrDefault(s => (s?.IsTown == true || s?.IsCastle == true) && s.MapFaction == enemy && s.SiegeEvent != null)
            ?? Settlement.All?.FirstOrDefault(s => (s?.IsTown == true || s?.IsCastle == true) && s.MapFaction == enemy);
         if (fortress != null)
            entries.Add(new QuestMenuEntry("siege", "target_settlement", fortress.Name?.ToString(),
               $"help take one of {enemyName}'s strongholds"));

         // The bounty-hunting loop: any enemy lord brought to the giver in chains, for a promised price.
         entries.Add(new QuestMenuEntry("deliver_prisoner", "target_faction", enemyName,
            "bring me any of their lords in chains and be paid for the man"));

         // An army to scout: only when the enemy truly fields one, so the deed is achievable today.
         if (enemy.Armies is {Count: > 0})
            entries.Add(new QuestMenuEntry("scout_army", "target_faction", enemyName,
               "find their army, gauge its strength, and report back"));
      }

      private static void AddRescueEntry(List<QuestMenuEntry> entries, Hero giver)
      {
         if (giver?.MapFaction == null) return;

         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h.MapFaction == giver.MapFaction && h.IsLord && h.IsPrisoner && h != giver && h != Hero.MainHero);
         if (captive == null) return;

         entries.Add(new QuestMenuEntry("rescue_prisoner", "target_hero", captive.Name?.ToString(),
            $"{captive.Name} of your realm languishes in enemy hands"));
      }

      private static void AddLetterEntry(List<QuestMenuEntry> entries, Hero giver)
      {
         // A kinsman or fellow lord elsewhere in the world: the always-available errand, so a giver
         // with no war and quiet roads still has SOMETHING real to ask.
         Hero recipient = giver?.Clan?.Heroes?.FirstOrDefault(h =>
                             h != giver && h != Hero.MainHero && h.IsAlive && !h.IsPrisoner
                             && h.CurrentSettlement != giver.CurrentSettlement)
                          ?? Hero.AllAliveHeroes.FirstOrDefault(h =>
                             h != giver && h != Hero.MainHero && h.IsLord && !h.IsPrisoner
                             && h.MapFaction == giver?.MapFaction && h.CurrentSettlement != giver?.CurrentSettlement);
         if (recipient == null) return;

         entries.Add(new QuestMenuEntry("deliver_letter", "target_hero", recipient.Name?.ToString(),
            $"a letter must reach {recipient.Name}"));
      }

      #endregion
   }
}
