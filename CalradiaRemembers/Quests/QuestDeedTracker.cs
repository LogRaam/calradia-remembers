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

      // TUNE: generous radius (campaign-map units) around the quest's named town within which bandits felled in
      // the open count as clearing them "near" that town, replacing the brittle exact-settlement-name match.
      // Internal so ViableQuestScanner offers a bandit_clear deed only when a bandit party roams within this SAME
      // radius, keeping the "offered => completable" invariant true by construction (single source of truth).
      internal const float BanditClearZoneRadius = 80f;

      public void Subscribe()
      {
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         // v2.5.3 REVERSAL of a deliberate v1.x decision (player report: freed a captive via the vanilla
         // PRISON BREAK mission, then days later read "so and so is lost, they are no longer on the map" as
         // the rescue quest voided). Rescue used to be detected ONLY from the player's own field-battle
         // victory (OnMapEventEnded), never from HeroPrisonerReleased, because that event carries no rescuer
         // and cannot tell a player rescue from an AI one, crediting it was judged to break the player-link
         // rule, and that reasoning was correct as far as it went. It did not go far enough: a staged prison
         // break (SandBox's PrisonBreakCampaignBehavior calling EndCaptivityAction.ApplyByEscape, reported as
         // ReleasedAfterEscape) raises no MapEvent, so the deed went uncredited, and the daily void sweep
         // later read the freed target as proof the task was impossible, cancelling a quest the player had
         // just completed themselves. Gabriel's ruling reverses the old decision: credit a release HOWEVER it
         // happened, except a ransom or one bundled with a peace treaty (ApplyByRansom/ApplyByPeace; death
         // frees nobody either), see QuestRescueCreditPolicy. Benefit of the doubt goes to the player
         // everywhere else, including a captive who slips free entirely unaided (the engine reports that the
         // same way as a staged break, naming no facilitator either way).
         CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
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

         // The specific hideout that fell (its settlement id), for a hideout battle; null otherwise.
         public string HideoutSettlementId;

         // Where the battle happened (the player's own position at its end), for zone-based bandit-clear matching.
         public Vec2 BattlePosition;
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
               // Zone-based, not an exact settlement-name match: bandits felled in the open anywhere within a
               // generous radius of the named town count. The old exact match left the player unable to complete
               // after clearing bandits that reported a slightly different nearest settlement.
               return f.LosersAreBandits && !f.IsHideout && (SettlementMatches(quest, f) || WithinBanditClearZone(quest, f))
                  ? $"Defeated the bandits near {quest.TargetSettlement} on day {day}"
                  : null;

            case QuestType.BanditHideout:
               if (!f.LosersAreBandits || !f.IsHideout) return null;

               // New quests are bound to a SPECIFIC existing lair (chosen in the giver's region at issue time);
               // only that lair's fall counts, and since only the player can ever clear a hideout, its fall is
               // unambiguously their deed. Quests saved before this carry no target lair and, as before, accept
               // any hideout clear so they stay completable.
               if (!string.IsNullOrEmpty(quest.TargetHideoutId)
                   && !string.Equals(quest.TargetHideoutId, f.HideoutSettlementId, StringComparison.Ordinal))
                  return null;

               return quest.DirectionHint != null
                  ? $"Cleared the bandit hideout {quest.DirectionHint} on day {day}"
                  : $"Cleared the bandit hideout near {quest.TargetSettlement} on day {day}";

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

      /// <summary>True when the battle happened within a generous radius of the quest's named town.</summary>
      private static bool WithinBanditClearZone(InformalQuest quest, BattleFacts f)
      {
         Settlement town = ResolveSettlementByName(quest.TargetSettlement);

         return town != null && town.GetPosition2D.Distance(f.BattlePosition) <= BanditClearZoneRadius;
      }

      /// <summary>Resolves a settlement by its display name (exact match), or null.</summary>
      private static Settlement ResolveSettlementByName(string name)
      {
         if (string.IsNullOrWhiteSpace(name)) return null;

         foreach (Settlement s in Settlement.All)
            if (string.Equals(s?.Name?.ToString(), name, StringComparison.OrdinalIgnoreCase))
               return s;

         return null;
      }

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
         // v1.34.2 fault/no-fault hardening (audit Pattern 2): return only on a missing victim, NOT on any
         // non-player kill. The old "killer != MainHero" early-out made the ExecuteEnemy branch below
         // unreachable for a THIRD-PARTY kill, so a named target slain by another lord left the quest a zombie
         // that later expired with an unjust penalty. Player-causation is now weighed per quest KIND
         // (QuestKillCreditPolicy): a paid NemesisBounty still pays ONLY for the player's own hand, while an
         // ExecuteEnemy is satisfied by the target's death whoever dealt it (the giver's end is met either way).
         if (victim == null) return;

         int day = CurrentDay();
         bool killerIsPlayer = killer == Hero.MainHero;
         ForEachOutstandingQuest((profile, quest) =>
         {
            if (!string.Equals(quest.TargetHeroId, victim.StringId, StringComparison.Ordinal)) return;
            if (!QuestKillCreditPolicy.IsSatisfiedByKill(quest.Type, killerIsPlayer)) return;

            // A NemesisBounty pays for the player's own kill (a nemesis cut down by a third hand is left for
            // NemesisResolutionBehavior to cancel, never rewarded here); an ExecuteEnemy simply needed the
            // target dead, so its evidence stays honest about causation when another hand dealt the blow.
            string evidence = quest.Type == QuestType.NemesisBounty
               ? $"Killed {victim.Name} on day {day}"
               : killerIsPlayer
                  ? $"Killed {victim.Name} on day {day}"
                  : $"{victim.Name} was slain on day {day}";
            Stamp(profile, quest, evidence, day);
         });
      }

      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         if (prisoner == null) return;
         bool byPlayer = capturer == PartyBase.MainParty || prisoner.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;

         if (!byPlayer) return;

         StampCaptureQuests(prisoner);
      }

      /// <summary>
      ///   v2.5.3 (player report, see the comment on <see cref="Subscribe" />): stamps an outstanding
      ///   RescuePrisoner quest targeting a hero whose captivity just ended, PROVIDED the way it ended
      ///   credits the player per <see cref="QuestRescueCreditPolicy" />. Fires for every captivity end
      ///   (battle, escape, mercy release, ...), not only the free_prisoner bridge action, so a rescue is
      ///   credited exactly once regardless of which detection path sees it first: OnMapEventEnded's own
      ///   battle-evidence stamp and this hook both target the same outstanding-only quest, so whichever
      ///   fires first satisfies it and the other finds nothing left to stamp.
      /// </summary>
      private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction faction, EndCaptivityDetail detail, bool showNotification)
      {
         if (prisoner == null) return;

         CaptivityEndReason reason = MapCaptivityEndReason(detail);
         if (!QuestRescueCreditPolicy.CreditsPlayerRescue(reason)) return;

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            if (quest.Type != QuestType.RescuePrisoner) return;
            if (!string.Equals(quest.TargetHeroId, prisoner.StringId, StringComparison.Ordinal)) return;

            Stamp(profile, quest, $"Freed {quest.TargetHeroName} from captivity on day {day}", day);
         });
      }

      /// <summary>
      ///   Maps the engine's own <see cref="EndCaptivityDetail" /> onto the engine-free
      ///   <see cref="CaptivityEndReason" /> QuestRescueCreditPolicy judges. A value this switch does not
      ///   recognise (an engine addition made after this was written) maps to <see cref="CaptivityEndReason.Other" />,
      ///   which the policy resolves to a defined, deliberate answer rather than this mapping silently
      ///   dropping it.
      /// </summary>
      private static CaptivityEndReason MapCaptivityEndReason(EndCaptivityDetail detail)
      {
         switch (detail)
         {
            case EndCaptivityDetail.Ransom: return CaptivityEndReason.Ransom;
            case EndCaptivityDetail.ReleasedAfterPeace: return CaptivityEndReason.Peace;
            case EndCaptivityDetail.ReleasedAfterBattle: return CaptivityEndReason.Battle;
            case EndCaptivityDetail.ReleasedAfterEscape: return CaptivityEndReason.Escape;
            case EndCaptivityDetail.ReleasedByChoice: return CaptivityEndReason.ByChoice;
            case EndCaptivityDetail.Death: return CaptivityEndReason.Death;
            case EndCaptivityDetail.ReleasedByCompensation: return CaptivityEndReason.Compensation;
            default: return CaptivityEndReason.Other;
         }
      }

      /// <summary>
      ///   Stamps any outstanding CapturePrisoner/NemesisBounty quest targeting <paramref name="prisoner" />
      ///   as satisfied. Called from the capture hook above (real battle captures) and directly by
      ///   <c>SellPrisonerVerb</c>: buying a captive moves custody via TransferPrisonerAction, which raises
      ///   no HeroPrisonerTaken — and dispatching that event by hand would also fire the stance, chronicle,
      ///   companion-mission and nemesis listeners, all tuned for a real capture, not a purchase.
      /// </summary>
      public void StampCaptureQuests(Hero prisoner)
      {
         if (prisoner == null) return;

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            if (!string.Equals(quest.TargetHeroId, prisoner.StringId, StringComparison.Ordinal)) return;

            // Nemesis pillar increment 3 (bounty missions): see the matching comment in OnHeroKilled, a
            // capture satisfies a NemesisBounty quest exactly like an ordinary CapturePrisoner one.
            if (quest.Type == QuestType.CapturePrisoner || quest.Type == QuestType.NemesisBounty)
               Stamp(profile, quest, $"Captured {prisoner.Name} on day {day}", day);

            // Capturing the target ALSO satisfies an AttackLord quest: you cannot take a lord prisoner
            // without defeating them, so a capture is a stronger outcome than the "defeat in battle" the
            // quest asks for. Player report (tthheerroocckk, Nexus 2026-07-19): given an attack_lord quest for
            // Decantia, captured her, and it never advanced, because AttackLord only matched the LEADER of a
            // losing party (DefeatedHeroIds) and missed a lord taken as an army MEMBER. Capture closes that gap.
            else if (quest.Type == QuestType.AttackLord)
               Stamp(profile, quest, $"Defeated and captured {prisoner.Name} on day {day}", day);
         });
      }

      /// <summary>
      ///   Save-repair pass, run once at session launch. A capture/attack quest is normally stamped by the
      ///   live HeroPrisonerTaken hook when the deed happens. But a save made on a build that did NOT stamp
      ///   AttackLord (or where the event was missed) leaves the quest stuck even though the target is sitting
      ///   in the player's dungeon. This scans every outstanding CapturePrisoner/NemesisBounty/AttackLord quest
      ///   and stamps it if the player CURRENTLY holds the target prisoner, so the deed is credited on load.
      ///   Idempotent (Stamp only touches outstanding quests) and evidence-based (the target really IS held).
      /// </summary>
      public void ReconcileHeldTargetsOnLoad()
      {
         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            if (quest.Type != QuestType.CapturePrisoner
                && quest.Type != QuestType.NemesisBounty
                && quest.Type != QuestType.AttackLord) return;
            if (string.IsNullOrEmpty(quest.TargetHeroId)) return;

            Hero target = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);

            // The proof we require: the target is a prisoner AND the party holding them is the player's.
            if (target == null || !target.IsPrisoner) return;
            if (target.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero) return;

            string verb = quest.Type == QuestType.AttackLord ? "Defeated and captured" : "Captured";
            Stamp(profile, quest, $"{verb} {target.Name} (held prisoner) on day {day}", day);
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
            SettlementName = ResolveBattleSettlementName(mapEvent),
            // The specific lair that fell (a hideout battle's settlement IS the hideout), for targeted matching.
            HideoutSettlementId = mapEvent.IsHideoutBattle
               ? mapEvent.MapEventSettlement?.StringId
               : null,
            // The player fought here, so their party position marks where the battle was, for zone matching.
            BattlePosition = MobileParty.MainParty?.GetPosition2D ?? default
         };

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            string evidence = MatchBattle(quest, facts, day);
            if (evidence != null) Stamp(profile, quest, evidence, day);

            // fkasad (01/10/2026): the player beat the lord a capture or delivery task names, and (as beaten lords
            // do) he may escape. Recorded on the task, with his realm, so the giver can take the victory or a lord
            // of that realm in his place, and a lapsed deadline is no fault of the player's (EscapedTargetPolicy).
            if (EscapedTargetPolicy.RecordsTheVictory(quest.Type) && quest.TargetBeatenOnDay == null
                && quest.TargetHeroId != null && facts.DefeatedHeroIds.Contains(quest.TargetHeroId))
            {
               Hero target = Hero.FindFirst(h => h.StringId == quest.TargetHeroId);
               quest.TargetBeatenOnDay = day;
               if (string.IsNullOrEmpty(quest.TargetFactionId) && target?.MapFaction != null)
               {
                  quest.TargetFactionId = target.MapFaction.StringId;
                  quest.TargetFactionName = target.MapFaction.Name?.ToString();
               }

               NpcMemoryCampaignBehavior.Instance?.Store.Set(profile);
               ModLog.Info($"[QUEST] {profile.Name}'s task names {quest.TargetHeroName}, beaten by the player on day {day}: recorded.");
            }
         });
      }

      /// <summary>
      ///   A declare_war deed completes only when the PLAYER's own faction is the aggressor against the
      ///   demanded faction (WarDeclared fires with faction1 = declarer). Being attacked never counts.
      /// </summary>
      /// <summary>
      ///   The SDK's one regard-change notice (subscribed once, at module load): an earn_regard task whose target
      ///   has just reached the goal is done. (No self-test guard: cr.testall's contract is a reload afterwards, and
      ///   asking Diagnostics from here would tie Quests into it, which the dependency map flagged.)
      /// </summary>
      internal static void OnRegardChanged(NpcMemoryService.Core.Standing.RegardChange change)
      {
         try
         {
            if (change == null || Campaign.Current == null) return;

            CreditRegard(change.NpcId, change.After);
         }
         catch (Exception ex) { ModLog.Error($"[QUEST] earn_regard check failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Stamps every outstanding earn_regard task whose target is <paramref name="npcId" /> and whose goal
      ///   <paramref name="regard" /> reaches (Lordfadooboo, 01/10/2026). Returns how many. Internal for cr.testall.
      /// </summary>
      internal static int CreditRegard(string npcId, int regard)
      {
         if (string.IsNullOrEmpty(npcId)) return 0;

         int day = CurrentDay(), stamped = 0;
         ForEachOutstandingQuest((profile, quest) =>
         {
            if (quest.Type != QuestType.EarnRegard || quest.TargetHeroId != npcId) return;
            if (!EarnRegardPolicy.IsMet(regard, quest.RegardGoal)) return;

            Stamp(profile, quest, $"Won the good graces of {quest.TargetHeroName} on day {day}", day);
            stamped++;
         });

         return stamped;
      }

      private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
      {
         IFaction player = Hero.MainHero?.MapFaction;

         if (player == null || faction1 == null || faction2 == null) return;

         int day = CurrentDay();
         ForEachOutstandingQuest((profile, quest) =>
         {
            if (quest.Type == QuestType.DeclareWar && WarDeedMatcher.IsSatisfied(new WarSides(faction1.StringId, faction2.StringId), quest.TargetFactionId, player.StringId))
               Stamp(profile, quest,
                  $"Declared war on {quest.TargetFactionName ?? faction2.Name?.ToString()} on day {day}", day);
         });
      }

      #endregion
   }
}