// Code written by Gabriel Mailhot, 22/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>
   ///   Watches the campaign for the deeds that satisfy outstanding quests and stamps
   ///   the matching quest with proof. This is what makes completion event-driven rather
   ///   than roleplay-driven: a quest is only ever marked done here, from a real battle,
   ///   capture, kill, or rescue in which the PLAYER took part. Stamping writes directly
   ///   onto the quest record (which persists with the profile), so it survives save/load
   ///   between doing the deed and reporting it — no separate, volatile journal.
   ///   Owned by <see cref="NpcMemoryCampaignBehavior" /> and subscribed at session launch,
   ///   so its listeners die with the campaign (handoff rule: no statics holding world refs).
   /// </summary>
   public sealed class QuestDeedTracker
   {
      // Visual detection radius in campaign-map units (matches typical party spotting range).
      private const float ScoutRangeUnits = 3.0f;

      public void Subscribe()
      {
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         // Rescue is detected from the player's own victory (OnMapEventEnded), NOT from
         // HeroPrisonerReleased: that event carries no rescuer, so it cannot tell a
         // player rescue from an AI one — crediting it would break the player-link rule.
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
         CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
         ModLog.Info("QuestDeedTracker subscribed to campaign events.");
      }

      /// <summary>Flattened facts about a player-won battle, computed once per event.</summary>
      private sealed class BattleFacts
      {
         public HashSet<string> CaravanFactionIds = new();
         public HashSet<string> DefeatedHeroIds = new();
         public HashSet<string> FreedPrisonerHeroIds = new();
         public bool HasCaravanLoser;
         public bool IsFieldBattle;
         public bool IsHideout;
         public bool IsRaid;
         public bool IsSiege;
         public HashSet<string> LoserFactionIds = new();
         public bool LosersAreBandits;
         public string SettlementName;
      }

      #region private

      private static int CurrentDay() => (int) CampaignTime.Now.ToDays;

      /// <summary>
      ///   Builds an evidence string that describes the army's composition: total
      ///   troop count, rough cavalry ratio, and up to three notable lords (excluding
      ///   the army commander, who is named at the start). Used as quest evidence so the
      ///   LLM sees concrete scouting intel when the player reports back.
      /// </summary>
      private static string DescribeArmy(MobileParty armyLeader, int day)
      {
         int total = 0,
             cavalry = 0;
         foreach (MobileParty p in armyLeader.Army.Parties)
            for (var i = 0; i < p.MemberRoster.Count; i++)
            {
               TroopRosterElement elem = p.MemberRoster.GetElementCopyAtIndex(i);
               total += elem.Number;
               FormationClass fc = elem.Character?.DefaultFormationClass ?? FormationClass.Infantry;
               if (fc == FormationClass.Cavalry || fc == FormationClass.HorseArcher || fc == FormationClass.LightCavalry || fc == FormationClass.HeavyCavalry)
                  cavalry += elem.Number;
            }

         string composition = total > 0 && cavalry > total * 0.5f ? "cavalry-heavy"
            : total > 0 && cavalry > total * 0.3f ? "mixed forces"
            : "infantry-heavy";

         var nobles = new List<string>();
         foreach (MobileParty p1 in armyLeader.Army.Parties.Where(p => p != armyLeader && p.LeaderHero is {IsLord: true}).Take(3))
            nobles.Add(p1.LeaderHero.Name.ToString());

         string nobleStr = nobles.Count > 0
            ? $", with {string.Join(", ", nobles)}"
            : "";

         Settlement nearest = FindNearestTownOrCastle(armyLeader.GetPosition2D);
         string locationStr = nearest != null
            ? $" near {nearest.Name}"
            : "";

         string leaderName = armyLeader.LeaderHero?.Name?.ToString() ?? armyLeader.Name.ToString();

         return $"Scouted the army of {leaderName}{locationStr} on day {day}: ~{total} troops, {composition}{nobleStr}";
      }

      /// <summary>
      ///   Returns the first active enemy army visible from <paramref name="playerPos" />
      ///   that satisfies the scout quest's target constraints. Null if none in range.
      ///   "Army" means the <see cref="MobileParty.Army" /> is non-null and this party is its
      ///   leader — lone wandering lords are excluded.
      /// </summary>
      private static MobileParty FindEnemyArmyInRange(InformalQuest quest, Hero giverHero, Vec2 playerPos)
      {
         foreach (MobileParty party in MobileParty.All)
         {
            // Must be the leader of a real army (not a lone wandering lord).
            if (party.Army == null || party.Army.LeaderParty != party) continue;

            // Must be within visual range.
            if (party.GetPosition2D.Distance(playerPos) > ScoutRangeUnits) continue;

            // Must be at war with the quest giver's faction.
            if (!FactionManager.IsAtWarAgainstFaction(giverHero.MapFaction, party.MapFaction)) continue;

            // If a specific army leader was named, it must match.
            if (!string.IsNullOrEmpty(quest.TargetHeroId))
            {
               if (party.LeaderHero?.StringId != quest.TargetHeroId) continue;
            }
            // If a faction was named, the army must belong to it.
            else if (!string.IsNullOrEmpty(quest.TargetFactionId))
            {
               if (party.MapFaction?.StringId != quest.TargetFactionId) continue;
            }

            return party;
         }

         return null;
      }

      private static Settlement FindNearestTownOrCastle(Vec2 pos)
      {
         Settlement best = null;
         var bestDist = float.MaxValue;
         foreach (Settlement s in Settlement.All)
         {
            if (!(s.IsTown || s.IsCastle)) continue;
            float d = s.GetPosition2D.Distance(pos);

            if (!(d < bestDist)) continue;

            bestDist = d;
            best = s;
         }

         return best;
      }

      private static void ForEachOutstandingQuest(Action<NpcProfile, InformalQuest> visit)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         if (store == null) return;

         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);

            if (profile == null || profile.ActiveQuests.Count == 0) continue;

            // Snapshot so a stamp doesn't disturb the enumeration.
            foreach (InformalQuest quest in profile.ActiveQuests.Where(q => q.IsOutstanding).ToList())
               visit(profile, quest);
         }
      }

      private static void InformPlayer(string text)
      {
         try { MBInformationManager.AddQuickInformation(new TextObject(text)); }
         catch (Exception ex) { ModLog.Error($"[QUEST] InformPlayer failed: {ex.Message}"); }
      }

      /// <summary>
      ///   Returns an evidence string when this battle satisfies the quest, else null.
      ///   Place-bound types require the named settlement to match where the fight was.
      /// </summary>
      private static string MatchBattle(InformalQuest quest, BattleFacts f, int day)
      {
         switch (quest.Type)
         {
            case QuestType.BanditClear:
               return f.LosersAreBandits && !f.IsHideout && SettlementMatches(quest, f)
                  ? $"Defeated the bandits near {quest.TargetSettlement} on day {day}"
                  : null;

            case QuestType.BanditHideout:
               // A hideout battle reports the hidden lair itself as its settlement, so the
               // nearest NAMED settlement at battle's end rarely equals the one named when
               // the quest was issued — requiring an exact match left these quests forever
               // unverifiable (the player clears the lair but the deed never registers, and
               // the NPC ends up "rewarding" it ad hoc, repeatedly). Since only one quest is
               // ever outstanding per giver, clearing any bandit hideout is accepted as the deed.
               return f.LosersAreBandits && f.IsHideout
                  ? $"Cleared the bandit hideout near {quest.TargetSettlement} on day {day}"
                  : null;

            case QuestType.AttackFaction:
               return quest.TargetFactionId != null && f.LoserFactionIds.Contains(quest.TargetFactionId)
                  ? $"Beat the forces of {quest.TargetFactionName} in battle on day {day}"
                  : null;

            case QuestType.AttackLord:
               return quest.TargetHeroId != null && f.DefeatedHeroIds.Contains(quest.TargetHeroId)
                  ? $"Defeated {quest.TargetHeroName} in battle on day {day}"
                  : null;

            case QuestType.AttackCaravan:
               return quest.TargetFactionId != null && f.CaravanFactionIds.Contains(quest.TargetFactionId)
                  ? $"Destroyed a caravan of {quest.TargetFactionName} on day {day}"
                  : null;

            case QuestType.RaidVillage:
               return f.IsRaid && SettlementMatches(quest, f)
                  ? $"Raided {quest.TargetSettlement} on day {day}"
                  : null;

            case QuestType.Siege:
               return f.IsSiege && SettlementMatches(quest, f)
                  ? $"Stormed {quest.TargetSettlement} on day {day}"
                  : null;

            case QuestType.RescuePrisoner:
               return quest.TargetHeroId != null && f.FreedPrisonerHeroIds.Contains(quest.TargetHeroId)
                  ? $"Freed {quest.TargetHeroName} from captivity on day {day}"
                  : null;

            default:
               return null; // capture/execute are handled by their own hero events
         }
      }

      private static Settlement NearestSettlement()
      {
         MobileParty main = MobileParty.MainParty;

         if (main == null) return null;

         Vec2 pos = main.GetPosition2D;
         Settlement best = null;
         var bestDist = float.MaxValue;
         foreach (Settlement s in Settlement.All)
         {
            // Only real, nameable settlements — never a hideout or hidden point.
            if (!(s.IsTown || s.IsVillage || s.IsCastle)) continue;

            float d = s.GetPosition2D.Distance(pos);

            if (!(d < bestDist)) continue;
            bestDist = d;
            best = s;
         }

         return best;
      }

      /// <summary>Stable ids of every hero a party is holding prisoner.</summary>
      private static IEnumerable<string> PrisonerHeroIds(PartyBase party)
      {
         TroopRoster roster = party?.PrisonRoster;

         if (roster == null) yield break;

         for (var i = 0; i < roster.Count; i++)
         {
            Hero hero = roster.GetCharacterAtIndex(i)?.HeroObject;

            if (hero != null) yield return hero.StringId;
         }
      }

      /// <summary>
      ///   The settlement a battle should be matched against. A hideout battle reports
      ///   the hideout itself as <see cref="MapEvent.MapEventSettlement" /> — useless for
      ///   matching a named town — so the hideout path falls back to the nearest real
      ///   settlement. Sieges and raids carry the true target settlement and use it.
      /// </summary>
      private static string ResolveBattleSettlementName(MapEvent mapEvent)
      {
         if (mapEvent.IsHideoutBattle) return NearestSettlement()?.Name?.ToString();

         return (mapEvent.MapEventSettlement ?? NearestSettlement())?.Name?.ToString();
      }

      private static bool SettlementMatches(InformalQuest quest, BattleFacts f)
         => !string.IsNullOrWhiteSpace(quest.TargetSettlement) && string.Equals(quest.TargetSettlement, f.SettlementName, StringComparison.OrdinalIgnoreCase);

      private static void Stamp(NpcProfile profile, InformalQuest quest, string evidence, int day)
      {
         quest.SatisfiedOnDay = day;
         quest.Evidence = evidence;
         NpcMemoryCampaignBehavior.Instance?.Store.Set(profile);
         ModLog.Info($"[QUEST] satisfied for {profile.Name}: {evidence}");
         InformPlayer($"Task progress — {evidence}. Report back to {profile.Name}.");

         // Update the journal mirror so the player sees "Done" without waiting to open chat.
         Hero giver = Hero.FindFirst(h => h.StringId == profile.Id);
         QuestJournalBridge.NotifyDeedDone(quest, giver);
      }

      private void OnHeroKilled(Hero victim,
                                Hero killer,
                                KillCharacterAction.KillCharacterActionDetail detail,
                                bool showNotification)
      {
         if (victim == null || killer != Hero.MainHero) return; // the player struck the blow

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            // Nemesis pillar increment 3 (bounty missions): a NemesisBounty quest is satisfied by EITHER
            // this hook or OnHeroPrisonerTaken below, whichever fires first (a bounty is "capture OR
            // execute", never both required). Any player-dealt death counts (battle or execution alike);
            // NemesisResolutionBehavior's own HeroKilledEvent listener separately resolves the identity
            // itself (IsAlive/DeathDay), unaffected by whether a bounty happened to be outstanding on him.
            if ((quest.Type == QuestType.ExecuteEnemy || quest.Type == QuestType.NemesisBounty)
                && string.Equals(quest.TargetHeroId, victim.StringId, StringComparison.Ordinal))
               Stamp(profile, quest, $"Killed {victim.Name} on day {day}", day);
         });
      }

      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         if (prisoner == null) return;
         bool byPlayer = capturer == PartyBase.MainParty || prisoner.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (!byPlayer) return;

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            // Nemesis pillar increment 3 (bounty missions): see the matching comment in OnHeroKilled, a
            // capture satisfies a NemesisBounty quest exactly like an ordinary CapturePrisoner one.
            if ((quest.Type == QuestType.CapturePrisoner || quest.Type == QuestType.NemesisBounty)
                && string.Equals(quest.TargetHeroId, prisoner.StringId, StringComparison.Ordinal))
               Stamp(profile, quest, $"Captured {prisoner.Name} on day {day}", day);
         });
      }

      private void OnHourlyTick()
      {
         MobileParty main = MobileParty.MainParty;

         if (main == null) return;

         Vec2 playerPos = main.GetPosition2D;
         int day = CurrentDay();

         ForEachOutstandingQuest((profile, quest) =>
         {
            if (quest.Type != QuestType.ScoutArmy) return;

            Hero giverHero = Hero.FindFirst(h => h.StringId == profile.Id);

            if (giverHero?.MapFaction == null) return;

            MobileParty spotted = FindEnemyArmyInRange(quest, giverHero, playerPos);

            if (spotted == null) return;

            string evidence = DescribeArmy(spotted, day);
            Stamp(profile, quest, evidence, day);
         });
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         if (mapEvent is not {IsPlayerMapEvent: true}) return;

         // Determine the player's side from the event itself — PartyBase.MainParty.Side
         // can already be detached by the time MapEventEnded fires.
         bool playerAttacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
         bool playerDefender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;

         if (!playerAttacker && !playerDefender) return;

         BattleSideEnum playerSide = playerAttacker
            ? BattleSideEnum.Attacker
            : BattleSideEnum.Defender;

         if (mapEvent.WinningSide != playerSide) return;

         BattleSideEnum losingSide = playerAttacker
            ? BattleSideEnum.Defender
            : BattleSideEnum.Attacker;

         var list = new List<PartyBase>();
         MBReadOnlyList<MapEventParty> mbReadOnlyList = mapEvent.GetMapEventSide(losingSide)?.Parties;
         if (mbReadOnlyList != null)
            foreach (MapEventParty p in mbReadOnlyList)
            {
               PartyBase p1 = p.Party;
               if (p1 != null) list.Add(p1);
            }

         List<PartyBase> losers = list;

         var facts = new BattleFacts {
            IsHideout = mapEvent.IsHideoutBattle,
            IsRaid = mapEvent.IsRaid,
            IsSiege = mapEvent.IsSiegeAssault,
            IsFieldBattle = mapEvent.IsFieldBattle,
            LosersAreBandits = losers.Any(p => p.MapFaction?.IsBanditFaction == true),
            HasCaravanLoser = losers.Any(p => p.MobileParty?.IsCaravan == true),
            LoserFactionIds = new HashSet<string>(losers.Where(p => p.MapFaction != null).Select(p => p.MapFaction.StringId)),
            CaravanFactionIds = new HashSet<string>(losers.Where(p => p.MobileParty?.IsCaravan == true && p.MapFaction != null).Select(p => p.MapFaction.StringId)),
            DefeatedHeroIds = new HashSet<string>(losers.Where(p => p.LeaderHero != null).Select(p => p.LeaderHero.StringId)),
            // Heroes the defeated parties were holding prisoner — the player just
            // freed them by winning this battle. Read before the aftermath scatters them.
            FreedPrisonerHeroIds = new HashSet<string>(losers.SelectMany(PrisonerHeroIds)),
            SettlementName = ResolveBattleSettlementName(mapEvent)
         };

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            string evidence = MatchBattle(quest, facts, day);
            if (evidence != null) Stamp(profile, quest, evidence, day);
         });
      }

      /// <summary>
      ///   A declare_war deed completes only when the PLAYER's own faction is the aggressor against the
      ///   demanded faction (WarDeclared fires with faction1 = declarer). Being attacked never counts.
      /// </summary>
      private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
      {
         IFaction player = Hero.MainHero?.MapFaction;

         if (player == null || faction1 == null || faction2 == null) return;

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            if (quest.Type == QuestType.DeclareWar && WarDeedMatcher.IsSatisfied(quest.TargetFactionId, faction1.StringId, faction2.StringId, player.StringId))
               Stamp(profile, quest,
                  $"Declared war on {quest.TargetFactionName ?? faction2.Name?.ToString()} on day {day}", day);
         });
      }

      #endregion
   }
}