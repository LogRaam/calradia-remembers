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
               entries.Add(new QuestMenuEntry {TypeToken = "bandit_hideout", TargetField = "target_settlement", TargetName = anchor.Name?.ToString(), Cue = $"a bandit lair infests the country {bearing} of {anchor.Name}"});
            }

            // 2. Open-field bandit clearing near the same anchor: offered ONLY when a bandit party actually roams
            // within the SAME radius QuestDeedTracker credits a clear in, so an offered bandit_clear is always
            // completable by construction (player report: a village sent me after bandits that were not there,
            // then turned hostile when I reported none). No roaming bandits nearby means no such task to give.
            if (BanditClearOfferPolicy.ShouldOffer(CountBanditPartiesNear(anchor)))
               entries.Add(new QuestMenuEntry {TypeToken = "bandit_clear", TargetField = "target_settlement", TargetName = anchor.Name?.ToString(), Cue = $"bandits prey on travellers around {anchor.Name}"});
         }

         // 3. Wartime deeds: only when the giver's realm truly wars, targets pulled from the live enemy.
         AddWarEntries(entries, giver);

         // 4. An ally of the giver's realm truly held captive somewhere.
         AddRescueEntry(entries, giver);

         // 5. A letter always travels: a real recipient of the giver's own clan or circle, elsewhere.
         AddLetterEntry(entries, giver);

         // 5b. Someone whose favour serves the giver, to be won over (Lordfadooboo, 01/10/2026).
         AddEarnRegardEntry(entries, giver);

         // 5c. A case to plead before his liege (Lordfadooboo, fkasad, 01/10/2026).
         AddPleadCaseEntry(entries, giver);

         // 6. A lord always needs supplies: horses for the cavalry, livestock or grain for the host. Always
         // groundable (the player can bring goods), so this personal task is always on the menu.
         // 6. Men for a thin garrison he governs or holds (fkasad, 01/10/2026).
         Settlement thin = QuestFactory.ThinGarrisonsOf(giver).FirstOrDefault();
         if (thin != null)
            entries.Add(new QuestMenuEntry {TypeToken = "reinforce_garrison", TargetField = "target_settlement", TargetName = thin.Name?.ToString(), Cue = $"the walls of {thin.Name} are thinly manned: have the player bring soldiers (set required_count)"});

         // 6b. Fresh recruits of his own people for the party he leads (Lordfadooboo, 01/10/2026).
         MobileParty ranks = giver.PartyBelongedTo;
         if (ranks != null && ranks.LeaderHero == giver && giver.Culture != null
             && RecruitTroopsPolicy.TroopsToAsk(ReinforceGarrisonPolicy.MinTroops, ranks.Party.PartySizeLimit - ranks.MemberRoster.TotalManCount) != null)
            entries.Add(new QuestMenuEntry {TypeToken = "recruit_troops", TargetField = null, TargetName = null, Cue = $"your ranks have room: have the player bring fresh recruits of {giver.Culture.Name} (set required_count)"});

         // 6a. What one of his towns lacks, read off its own market (fkasad, 01/10/2026). Before the personal supply
         // so the menu's cap keeps it.
         (Settlement Place, MarketReading Good)? need = Mapping.TownNeeds.MostLackingFor(giver);
         if (need != null)
            entries.Add(new QuestMenuEntry {TypeToken = "provide_goods", TargetField = null, TargetName = null, Cue = $"{need.Value.Place.Name} lacks {need.Value.Good.Name}, dear at its market: have the player bring it for the town (category: {need.Value.Good.Name}, set required_count)"});

         entries.Add(new QuestMenuEntry {TypeToken = "provide_goods", TargetField = null, TargetName = null, Cue = "have the player bring you a supply of horses, livestock, or grain (set category + required_count)"});

         // 7a. Someone an unwed lord would marry, of another house and at peace (fkasad's matchmaker, 01/10/2026).
         if (giver.IsLord && giver.Spouse == null && !giver.IsChild)
         {
            Hero bride = Hero.AllAliveHeroes.FirstOrDefault(h => h.Clan != null && h.Clan != giver.Clan && h.Clan != Clan.PlayerClan && !h.IsPrisoner
                                                                && h.Spouse == null && Mapping.HeroPresence.IsInTheWorld(h)
                                                                && Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(giver, h)
                                                                && (h.MapFaction == null || giver.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(h.MapFaction, giver.MapFaction)));
            if (bride != null)
               entries.Add(new QuestMenuEntry {TypeToken = "arrange_match", TargetField = "target_hero", TargetName = bride.Name?.ToString(), Cue = $"you would wed {bride.Name} of {bride.Clan.Name}: have the player win them over for you, then their family's consent"});
         }

         // 7. A weapon forged by the player's own hand, of the kind you name (Lordfadooboo, 01/10/2026).
         if (giver.IsLord)
            entries.Add(new QuestMenuEntry {TypeToken = "craft_weapon", TargetField = null, TargetName = null, Cue = "have the player forge you a weapon at a smithy (set category to the weapon you want)"});

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
            entries.Add(new QuestMenuEntry {TypeToken = "attack_lord", TargetField = "target_hero", TargetName = lords[0].Name?.ToString(), Cue = $"an enemy of your realm rides for {enemyName}"});
         if (lords.Count > 1)
            entries.Add(new QuestMenuEntry {TypeToken = "capture_prisoner", TargetField = "target_hero", TargetName = lords[1].Name?.ToString(), Cue = "bring this enemy back in chains"});

         entries.Add(new QuestMenuEntry {TypeToken = "attack_faction", TargetField = "target_faction", TargetName = enemyName, Cue = "strike their parties in the field"});

         Settlement village = Settlement.All?.FirstOrDefault(s => s?.IsVillage == true && s.MapFaction == enemy);
         if (village != null)
            entries.Add(new QuestMenuEntry {TypeToken = "raid_village", TargetField = "target_settlement", TargetName = village.Name?.ToString(), Cue = $"put one of {enemyName}'s villages to the torch"});

         // The "come take a castle with us" errand, absent from the grounded menu before (player report
         // 2026-07-17: a lord substituted a generic attack-faction quest for a siege the player asked for).
         // Prefer a stronghold already under siege so the deed is achievable now, else any enemy fortification.
         // Same predicate BindSettlement(requireFortification) grounds, so a named target always registers.
         Settlement fortress =
            Settlement.All?.FirstOrDefault(s => (s?.IsTown == true || s?.IsCastle == true) && s.MapFaction == enemy && s.SiegeEvent != null)
            ?? Settlement.All?.FirstOrDefault(s => (s?.IsTown == true || s?.IsCastle == true) && s.MapFaction == enemy);
         if (fortress != null)
            entries.Add(new QuestMenuEntry {TypeToken = "siege", TargetField = "target_settlement", TargetName = fortress.Name?.ToString(), Cue = $"help take one of {enemyName}'s strongholds"});

         // A town of theirs to scout from inside, through its gang leader (fkasad, 01/10/2026).
         Settlement scoutable = Settlement.All?.FirstOrDefault(s => s?.MapFaction == enemy && ScoutTownPolicy.MayAsk(QuestFactory.ScoutFacts(s, giver)));
         if (scoutable != null)
            entries.Add(new QuestMenuEntry {TypeToken = "scout_town", TargetField = "target_settlement", TargetName = scoutable.Name?.ToString(), Cue = $"slip into {scoutable.Name} and buy what its gang leader knows of its walls and stores"});

         // The bounty-hunting loop: any enemy lord brought to the giver in chains, for a promised price.
         entries.Add(new QuestMenuEntry {TypeToken = "deliver_prisoner", TargetField = "target_faction", TargetName = enemyName, Cue = "bring me any of their lords in chains and be paid for the man"});

         // An army to scout: only when the enemy truly fields one, so the deed is achievable today.
         if (enemy.Armies is {Count: > 0})
            entries.Add(new QuestMenuEntry {TypeToken = "scout_army", TargetField = "target_faction", TargetName = enemyName, Cue = "find their army, gauge its strength, and report back"});
      }

      private static void AddRescueEntry(List<QuestMenuEntry> entries, Hero giver)
      {
         if (giver?.MapFaction == null) return;

         Hero captive = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h.MapFaction == giver.MapFaction && h.IsLord && h.IsPrisoner && h != giver && h != Hero.MainHero);
         if (captive == null) return;

         // Held in a town or castle's dungeon, the game's own prison break is a way in (01/10/2026): say so, so the
         // giver can ask for exactly that.
         // Held by a realm the player is at peace with: an envoy's errand, not a rescue by force (fkasad, 01/10/2026).
         int advance = RansomEnvoyPolicy.Advance(GameState.CourtActionResolver.RansomAnchor(captive));
         if (RansomEnvoyPolicy.MaySend(QuestFactory.EnvoyFacts(captive, giver, advance)))
         {
            entries.Add(new QuestMenuEntry {TypeToken = "ransom_envoy", TargetField = "target_hero", TargetName = captive.Name?.ToString(), Cue = $"{captive.Name} of your realm is held by people the player is at peace with: send the player to buy them back with {advance} denars of yours"});

            return;
         }

         string prison = captive.PartyBelongedToAsPrisoner?.Settlement?.Name?.ToString();
         string cue = prison != null
            ? $"{captive.Name} of your realm languishes in the prison of {prison}: the player may break them out, or take the place"
            : $"{captive.Name} of your realm languishes in enemy hands";
         entries.Add(new QuestMenuEntry {TypeToken = "rescue_prisoner", TargetField = "target_hero", TargetName = captive.Name?.ToString(), Cue = cue});
      }

      /// <summary>
      ///   A person whose good graces serve the giver: his realm's ruler, else the head of his house. Offered only
      ///   when the factory would bind them (EarnRegardPolicy: a living adult third person, something left to win).
      /// </summary>
      private static void AddEarnRegardEntry(List<QuestMenuEntry> entries, Hero giver)
      {
         Hero ruler = (giver?.MapFaction as Kingdom)?.Leader;
         Hero head = giver?.Clan?.Leader;

         foreach ((Hero who, string why) in new[] {(ruler, "your liege"), (head, "the head of your house")})
         {
            if (who == null || !EarnRegardPolicy.MayBeNamed(QuestFactory.RegardTargetFacts(who, giver))) continue;
            if (EarnRegardPolicy.GoalFrom(QuestFactory.RegardFor(who)) == null) continue;

            entries.Add(new QuestMenuEntry {TypeToken = "earn_regard", TargetField = "target_hero", TargetName = who.Name?.ToString(), Cue = $"the player should win the good graces of {who.Name}, {why}, whose favour serves you"});

            return;
         }
      }

      /// <summary>The giver's liege, when the giver is a vassal: the court a lord most needs his case pleaded at.</summary>
      private static void AddPleadCaseEntry(List<QuestMenuEntry> entries, Hero giver)
      {
         Hero ruler = (giver?.MapFaction as Kingdom)?.Leader;
         if (ruler == null || !PleadCasePolicy.MayBePleadedTo(QuestFactory.RegardTargetFacts(ruler, giver), GameState.CourtActionResolver.HoldsCourt(ruler))) return;

         entries.Add(new QuestMenuEntry {TypeToken = "plead_case", TargetField = "target_hero", TargetName = ruler.Name?.ToString(), Cue = $"the player should plead your case before {ruler.Name}, your liege, and win them over to you"});
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

         entries.Add(new QuestMenuEntry {TypeToken = "deliver_letter", TargetField = "target_hero", TargetName = recipient.Name?.ToString(), Cue = $"a letter must reach {recipient.Name}"});
      }

      #endregion
   }
}
