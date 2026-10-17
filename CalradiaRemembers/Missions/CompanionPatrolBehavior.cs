// Code written by Gabriel Mailhot, 17/10/2026.
// A companion patrols a settlement for the player (jaserms1, Nexus, 16/10/2026: a war council's patrol order did
// nothing). Gabriel's rulings: anywhere, with the player's own men, until told to stop, a letter each month; the men
// are chosen on the game's own "create a party" screen, companions included, up to the leader's party size, and the
// clan pays the wages as for any clan party. The rules live in CompanionPatrolPolicy; his engine spike of 17/10 showed
// that a patrol with the AI free engages bandits but drifts off its post, hence the hourly leash.
//
// The screen cannot open inside a conversation or a council (both are Missions), so an order waits here and the screen
// opens on the next campaign tick back on the map. Nothing leaves the player's party before the screen is confirmed.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Travel;
using Helpers;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Missions
{
   /// <summary>Owns the companion patrols: the order, the troop screen, the hourly leash, the battles, and the way home.</summary>
   public sealed class CompanionPatrolBehavior : CampaignBehaviorBase
   {
      private const float ArrivalDistance = 2.0f;
      private const int LetterRecallDays = 2;
      private const string SaveKey = "CalradiaRemembers_CompanionPatrols_v1bytes";

      /// <summary>The live instance (one per campaign).</summary>
      public static CompanionPatrolBehavior Instance;

      private readonly List<PendingOrder> _pending = new();
      private List<CompanionPatrol> _patrols = new();

      public CompanionPatrolBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      /// <summary>True when this hero leads a patrol (out or riding home).</summary>
      public bool IsPatrolling(string heroId) => !string.IsNullOrEmpty(heroId) && _patrols.Any(p => p.CompanionId == heroId);

      /// <summary>The companions leading a patrol.</summary>
      public IEnumerable<string> PatrollingIds => _patrols.Select(p => p.CompanionId).ToList();

      /// <summary>One line per patrol, for the console and the errands listing.</summary>
      public IEnumerable<string> DescribeLines()
         => _patrols.Select(p => $"- {p.CompanionName}: {(p.State == CompanionPatrolState.Patrolling ? "patrols" : "rides home from")} {p.SettlementName} "
                                 + $"(since day {p.StartDay}, {p.MenAtDeparture} men out, won {p.BattlesWon}, lost {p.BattlesLost}, {FindParty(p.PartyId)?.MemberRoster.TotalHealthyCount ?? 0} fit now).");

      // ── The order ─────────────────────────────────────────────────────────

      /// <summary>
      ///   Takes a patrol order: validates it and queues the troop screen, which opens back on the map. Nothing moves
      ///   yet; the player chooses the men there, or cancels. <paramref name="men" /> is the number the order named, if any.
      /// </summary>
      public (bool Accepted, string Message) RequestPatrol(Hero companion, Settlement target, int? men)
      {
         string refusal = Refusal(companion, target);
         if (refusal != null) return (false, refusal);

         _pending.RemoveAll(o => o.CompanionId == companion.StringId);
         _pending.Add(new PendingOrder {CompanionId = companion.StringId, SettlementId = target.StringId, Men = men});
         ModLog.Info($"[PATROL] order taken: {companion.Name} to patrol {target.Name} ({(men.HasValue ? men + " men named" : "no number named")}); the troop screen opens on the map.");

         return (true, $"{companion.Name} will patrol around {target.Name}. Choose the men who ride with them.");
      }

      /// <summary>Why this order cannot be given, or null when it can.</summary>
      private string Refusal(Hero companion, Settlement target)
      {
         if (companion == null || companion == Hero.MainHero) return "No companion to send.";
         if (!IsUsableCompanion(companion)) return $"{companion.Name} is not a companion in your party to send.";
         if (CompanionMissionBehavior.Instance?.IsAway(companion.StringId) == true || IsPatrolling(companion.StringId))
            return $"{companion.Name} is already away.";
         if (target == null || target.IsHideout) return "There is no such place to patrol.";
         if (companion.PartyBelongedTo?.MapEvent != null) return $"{companion.Name} cannot leave in the middle of a battle.";

         return null;
      }

      private static bool IsUsableCompanion(Hero h)
         => Mapping.ActionGate.Allows(h, ActionKind.CompanionMission) && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan) && h.PartyBelongedTo == MobileParty.MainParty;

      private void OnTick(float dt)
      {
         if (_pending.Count == 0) return;
         if (Mission.Current != null || Game.Current?.GameStateManager?.ActiveState is not MapState) return;
         if (MobileParty.MainParty?.MapEvent != null) return;

         PendingOrder order = _pending[0];
         _pending.RemoveAt(0);
         SafeRun.Step("CompanionPatrolBehavior.OpenTroopScreen", () => OpenTroopScreen(order));
      }

      /// <summary>The game's own "create a party" screen, with the leader fixed and the prefilled men on the left.</summary>
      private void OpenTroopScreen(PendingOrder order)
      {
         Hero companion = HeroById(order.CompanionId);
         Settlement target = Settlement.Find(order.SettlementId);
         string refusal = Refusal(companion, target);
         if (refusal != null)
         {
            Notify($"The patrol order lapses: {refusal}", companion);

            return;
         }

         TroopRoster left = TroopRoster.CreateDummyTroopRoster();
         TroopRoster leftPrisoners = TroopRoster.CreateDummyTroopRoster();
         TroopRoster right = MobileParty.MainParty.MemberRoster.CloneRosterData();
         TroopRoster rightPrisoners = MobileParty.MainParty.PrisonRoster.CloneRosterData();

         left.AddToCounts(companion.CharacterObject, 1);
         right.AddToCounts(companion.CharacterObject, -1);

         int prefill = CompanionPatrolPolicy.PrefillMen(order.Men, HealthyRegulars(MobileParty.MainParty.MemberRoster));
         foreach (TroopRosterElement e in right.GetTroopRoster().Where(e => e.Character is {IsHero: false}).OrderBy(e => e.Character.Tier).ToList())
         {
            if (prefill <= 0) break;
            int take = Math.Min(e.Number - e.WoundedNumber, prefill);
            if (take <= 0) continue;
            left.AddToCounts(e.Character, take);
            right.AddToCounts(e.Character, -take);
            prefill -= take;
         }

         int limit = Campaign.Current.Models.PartySizeLimitModel.GetAssumedPartySizeForLordParty(companion, Clan.PlayerClan.MapFaction, Clan.PlayerClan);
         var title = new TextObject($"{companion.Name}'s patrol of {target.Name}");

         PartyScreenHelper.OpenScreenWithDummyRoster(left, leftPrisoners, right, rightPrisoners, title, MobileParty.MainParty.Name, limit,
            MobileParty.MainParty.Party.PartySizeLimit,
            (lm, lp, rm, rp, leftLimit, rightLimit) => DoneCondition(companion, lm, leftLimit),
            (lo, lm, lp, ro, rm, rp, fromCancel) => OnScreenClosed(companion, target, lm, fromCancel),
            (character, type, side, leftOwner) => IsTransferable(companion, character, type),
            CannotTalkWhileChoosing);
      }

      private static bool CannotTalkWhileChoosing(Hero hero, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side, PartyBase leftOwnerParty, out TextObject reason)
      {
         reason = new TextObject("You choose the patrol's men; talk once they are chosen.");

         return false;
      }

      private static Tuple<bool, TextObject> DoneCondition(Hero companion, TroopRoster chosen, int limit)
      {
         if (!chosen.Contains(companion.CharacterObject))
            return new Tuple<bool, TextObject>(false, new TextObject($"{companion.Name} leads the patrol."));
         if (chosen.TotalManCount > limit)
            return new Tuple<bool, TextObject>(false, new TextObject($"{companion.Name} can lead at most {limit} men."));

         return new Tuple<bool, TextObject>(true, null);
      }

      /// <summary>Soldiers move freely; of the heroes, only the player's companions other than the leader (the leader stays put).</summary>
      private static bool IsTransferable(Hero leader, CharacterObject character, PartyScreenLogic.TroopType type)
      {
         if (type != PartyScreenLogic.TroopType.Member || character == null) return false;
         if (!character.IsHero) return true;

         Hero h = character.HeroObject;

         return h != null && h != Hero.MainHero && h != leader && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan)
                && Mapping.ActionGate.Allows(h, ActionKind.CompanionMission);
      }

      private void OnScreenClosed(Hero companion, Settlement target, TroopRoster chosen, bool fromCancel)
      {
         if (fromCancel)
         {
            Notify($"The patrol is called off; {companion.Name} stays with you.", companion);
            ModLog.Info($"[PATROL] troop screen cancelled: {companion.Name} stays.");

            return;
         }

         SafeRun.Step("CompanionPatrolBehavior.Confirm", () => Confirm(companion, target, chosen));
      }

      // ── Riding out ────────────────────────────────────────────────────────

      /// <summary>
      ///   The patrol leaves: a clan party led by the companion, with the chosen men and companions moved (never
      ///   copied) out of the player's party, a few days of the player's food, and the patrol order. Returns a notice,
      ///   or a reason it could not leave. The troop screen's confirm and cr.testall both come here.
      /// </summary>
      internal string Confirm(Hero companion, Settlement target, TroopRoster chosen)
      {
         string refusal = Refusal(companion, target);
         if (refusal != null) return refusal;

         int threshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
         if (companion.Gold < threshold) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, companion, threshold - companion.Gold);

         MobileParty party = MobilePartyHelper.CreateNewClanMobileParty(companion, Clan.PlayerClan);
         if (party == null) return $"{companion.Name} could not set out just now.";

         var others = new List<string>();
         TroopRoster main = MobileParty.MainParty.MemberRoster;
         foreach (TroopRosterElement e in chosen.GetTroopRoster().ToList())
         {
            if (e.Character == null || e.Character == companion.CharacterObject) continue;
            if (e.Character.IsHero)
            {
               Hero h = e.Character.HeroObject;
               if (h == null || h == Hero.MainHero || h.PartyBelongedTo != MobileParty.MainParty) continue;
               AddHeroToPartyAction.Apply(h, party, false);
               others.Add(h.Name.ToString());

               continue;
            }

            // The screen worked on a copy: take from the real roster what is really there, once.
            int index = main.FindIndexOfTroop(e.Character);
            if (index < 0) continue;
            TroopRosterElement real = main.GetElementCopyAtIndex(index);
            int number = Math.Min(e.Number, real.Number);
            int wounded = Math.Min(Math.Min(e.WoundedNumber, real.WoundedNumber), number);
            if (number <= 0) continue;
            int xp = real.Number > 0 ? real.Xp * number / real.Number : 0;
            main.AddToCounts(e.Character, -number, false, -wounded, -xp);
            party.MemberRoster.AddToCounts(e.Character, number, false, wounded, xp);
         }

         int men = party.MemberRoster.TotalHealthyCount;
         MoveFood(MobileParty.MainParty, party, CompanionPatrolPolicy.FoodUnitsForDeparture(party.MemberRoster.TotalManCount, 1f / Campaign.Current.Models.MobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood));
         IssuePatrol(party, target);

         var patrol = new CompanionPatrol {
            Id = Guid.NewGuid().ToString("N"),
            CompanionId = companion.StringId,
            CompanionName = companion.Name.ToString(),
            SettlementId = target.StringId,
            SettlementName = target.Name.ToString(),
            PartyId = party.StringId,
            StartDay = Today,
            LastReportDay = Today,
            MenAtDeparture = men,
            State = CompanionPatrolState.Patrolling
         };
         _patrols.Add(patrol);

         string with = others.Count > 0 ? $", with {string.Join(", ", others)}" : "";
         Remember(companion, $"Rode out at {Hero.MainHero.Name}'s order to patrol around {target.Name}, with {men} men{with}.");
         string notice = $"{companion.Name} rides out to patrol around {target.Name} with {men} men{with}.";
         Notify(notice, companion);
         ModLog.Info($"[PATROL] {notice} (party {party.StringId}).");

         return notice;
      }

      private static int HealthyRegulars(TroopRoster roster)
         => roster.GetTroopRoster().Where(e => e.Character is {IsHero: false}).Sum(e => e.Number - e.WoundedNumber);

      private static void MoveFood(MobileParty from, MobileParty to, int units)
      {
         foreach (ItemRosterElement e in from.ItemRoster.Where(e => e.EquipmentElement.Item is {IsFood: true}).OrderByDescending(e => e.Amount).ToList())
         {
            if (units <= 0) break;
            int take = Math.Min(units, e.Amount);
            from.ItemRoster.AddToCounts(e.EquipmentElement, -take);
            to.ItemRoster.AddToCounts(e.EquipmentElement, take);
            units -= take;
         }
      }

      private static void IssuePatrol(MobileParty party, Settlement target)
      {
         // The AI stays free: with the lock on, a patrol only watches bandits ride past (the spike, 17/10/2026).
         party.Ai.SetDoNotMakeNewDecisions(false);
         party.SetMovePatrolAroundSettlement(target, MobileParty.NavigationType.Default, false);
      }

      // ── Every hour ────────────────────────────────────────────────────────

      /// <summary>Each day: a patrol out for a month writes to the player with its record and asks whether to go on.</summary>
      private void OnDailyTick()
      {
         foreach (CompanionPatrol p in _patrols.Where(x => x.State == CompanionPatrolState.Patrolling).ToList())
         {
            CompanionPatrol patrol = p;
            SafeRun.Step($"CompanionPatrolBehavior.Report({patrol.CompanionName})", () =>
            {
               if (!CompanionPatrolPolicy.ReportDue(patrol.LastReportDay, Today)) return;
               patrol.LastReportDay = Today;
               SendReport(patrol);
            });
         }
      }

      /// <summary>Set by cr.testall so a forced report is logged rather than written.</summary>
      internal static bool DebugSuppressLetters;

      private void SendReport(CompanionPatrol p)
      {
         Hero companion = HeroById(p.CompanionId);
         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         if (companion == null || behavior?.LlmServices == null || behavior.Store == null) return;

         string context = PatrolLetterText.Context(new PatrolReportFacts {
            SettlementName = p.SettlementName,
            DaysOut = Today - p.StartDay,
            MenAtDeparture = p.MenAtDeparture,
            MenFitNow = FindParty(p.PartyId)?.MemberRoster.TotalHealthyCount ?? 0,
            BattlesWon = p.BattlesWon,
            BattlesLost = p.BattlesLost,
            FoesBeaten = p.FoesBeaten
         });

         if (DebugSuppressLetters)
         {
            ModLog.Info($"[PATROL] (testall) monthly letter from {p.CompanionName} not written: {context}");

            return;
         }

         NpcProfile profile = behavior.Store.Get(companion.StringId) ?? Mapping.HeroProfileMapper.FromHero(companion);
         Letters.LetterGenerationService.QueueInitialLetter(profile, companion, LetterReason.PatrolReport, context, behavior.LlmServices, behavior.Store, Today);
         ModLog.Info($"[PATROL] monthly letter from {p.CompanionName}: {context}");
      }

      /// <summary>Runs the daily handler now, with <paramref name="forceReport" /> treating every patrol's letter as due.</summary>
      internal void DebugDay(bool forceReport)
      {
         if (forceReport) foreach (CompanionPatrol p in _patrols) p.LastReportDay = Today - CompanionPatrolPolicy.ReportEveryDays;
         OnDailyTick();
      }

      private void OnHourlyTick()
      {
         foreach (CompanionPatrol p in _patrols.ToList())
         {
            CompanionPatrol patrol = p;
            SafeRun.Step($"CompanionPatrolBehavior.Hour({patrol.CompanionName})", () => Hour(patrol));
         }
      }

      private void Hour(CompanionPatrol p)
      {
         MobileParty party = FindParty(p.PartyId);
         Hero companion = HeroById(p.CompanionId);
         if (party is not {IsActive: true} || companion == null || companion.PartyBelongedTo != party)
         {
            Scattered(p, companion, party);

            return;
         }

         if (p.State == CompanionPatrolState.Patrolling && p.RecallOrderDay > 0 && Today >= p.RecallOrderDay)
         {
            p.State = CompanionPatrolState.Returning;
            Notify($"{p.CompanionName} has your letter and leaves {p.SettlementName} to bring the men back to you.", companion);
         }

         if (p.State == CompanionPatrolState.Returning)
         {
            if (party.MapEvent != null) return;
            if (party.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D) < ArrivalDistance)
               BringHome(p, companion, party);
            else
               TravelingParty.DirectToMovingPoint(party, MobileParty.MainParty.GetPosition2D);

            return;
         }

         Settlement target = Settlement.Find(p.SettlementId);
         if (target == null)
         {
            p.State = CompanionPatrolState.Returning;

            return;
         }

         AiBehavior st = party.ShortTermBehavior;
         var facts = new PatrolLeashFacts {
            InBattle = party.MapEvent != null || party.SiegeEvent != null,
            EngagingOrFleeing = st is AiBehavior.EngageParty or AiBehavior.FleeToPoint or AiBehavior.FleeToGate or AiBehavior.FleeToParty,
            Patrolling = party.DefaultBehavior == AiBehavior.PatrolAroundPoint,
            AnchoredOnOrderedSettlement = party.TargetSettlement == target,
            DistanceToSettlement = party.GetPosition2D.Distance(target.GatePosition.ToVec2()),
            FoodLow = party.GetNumDaysForFoodToLast() < CompanionPatrolPolicy.FoodLowDays
         };

         if (!CompanionPatrolPolicy.ShouldReissue(facts)) return;

         IssuePatrol(party, target);
         ModLog.Info($"[PATROL] leash: {p.CompanionName} sent back to {p.SettlementName} (was {party.DefaultBehavior}, anchored {facts.AnchoredOnOrderedSettlement}, {facts.DistanceToSettlement:0.0} away).");
      }

      // ── Battles, capture, death ───────────────────────────────────────────

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         if (mapEvent == null || _patrols.Count == 0) return;

         foreach (CompanionPatrol p in _patrols)
         {
            MobileParty party = FindParty(p.PartyId);
            if (party == null) continue;

            BattleSideEnum? side = SideOf(mapEvent, party);
            if (side == null) continue;

            MapEventSide foe = mapEvent.GetMapEventSide(side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            bool won = mapEvent.WinningSide == side;
            if (won)
            {
               p.BattlesWon++;
               p.FoesBeaten += foe?.HealthyTroopCountAtMapEventStart ?? 0;
            }
            else
            {
               p.BattlesLost++;
            }

            ModLog.Info($"[PATROL] {p.CompanionName} {(won ? "won" : "lost")} against {foe?.LeaderParty?.Name} ({foe?.HealthyTroopCountAtMapEventStart} men) near {p.SettlementName}; {party.MemberRoster.TotalHealthyCount} fit.");
         }
      }

      private static BattleSideEnum? SideOf(MapEvent mapEvent, MobileParty party)
      {
         if (mapEvent.AttackerSide?.Parties?.Any(x => x.Party?.MobileParty == party) == true) return BattleSideEnum.Attacker;
         if (mapEvent.DefenderSide?.Parties?.Any(x => x.Party?.MobileParty == party) == true) return BattleSideEnum.Defender;

         return null;
      }

      private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
      {
         if (prisoner == null) return;
         CompanionPatrol p = _patrols.FirstOrDefault(x => x.CompanionId == prisoner.StringId);
         if (p != null)
         {
            _patrols.Remove(p);
            CompanionHappinessService.ApplyDriver(prisoner, HappinessDriver.Captured);
            Notify($"{p.CompanionName} was taken prisoner by {capturer?.Name?.ToString() ?? "the enemy"} near {p.SettlementName}; the patrol is broken.", prisoner);
            ModLog.Info($"[PATROL] {p.CompanionName} captured by {capturer?.Name} near {p.SettlementName}; patrol closed.");
         }
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (victim == null) return;
         CompanionPatrol p = _patrols.FirstOrDefault(x => x.CompanionId == victim.StringId);
         if (p == null) return;

         _patrols.Remove(p);
         Notify($"{p.CompanionName} fell while patrolling around {p.SettlementName}; the patrol is no more.", null);
         ModLog.Info($"[PATROL] {p.CompanionName} killed near {p.SettlementName}; patrol closed.");
      }

      /// <summary>The party is gone with no capture or death recorded (scattered): a free companion makes their way back.</summary>
      private void Scattered(CompanionPatrol p, Hero companion, MobileParty party)
      {
         _patrols.Remove(p);
         if (party is {IsActive: true}) BringHomeMembers(party);

         bool cameBack = companion is {IsPrisoner: false} && !companion.IsDead
                         && CompanionLifecyclePolicy.MayReintegrate(companion.IsPlayerCompanion, companion.Clan == Clan.PlayerClan);
         if (cameBack && companion.PartyBelongedTo != MobileParty.MainParty)
            try { AddHeroToPartyAction.Apply(companion, MobileParty.MainParty, false); }
            catch (Exception ex) { ModLog.Error($"[PATROL] rejoin failed for {p.CompanionName}: {ex.Message}"); }

         Notify(cameBack
            ? $"{p.CompanionName}'s patrol around {p.SettlementName} was scattered; {p.CompanionName} has made their way back to you."
            : $"{p.CompanionName}'s patrol around {p.SettlementName} is no more.", companion);
         ModLog.Info($"[PATROL] {p.CompanionName}'s patrol party is gone; closed (came back {cameBack}).");
      }

      // ── Home ──────────────────────────────────────────────────────────────

      /// <summary>Breaks off the patrol: the band rides back to the player and hands over every man (the chat and console path).</summary>
      public (bool Recalled, string Message) Recall(Hero companion)
      {
         CompanionPatrol p = _patrols.FirstOrDefault(x => x.CompanionId == companion?.StringId);
         if (p == null) return (false, $"{companion?.Name} is not on patrol.");
         if (p.State == CompanionPatrolState.Returning) return (true, $"{companion.Name} is already riding back to you.");

         p.State = CompanionPatrolState.Returning;
         MobileParty party = FindParty(p.PartyId);
         if (party != null) TravelingParty.DirectToMovingPoint(party, MobileParty.MainParty.GetPosition2D);
         ModLog.Info($"[PATROL] {p.CompanionName} recalled; riding back.");

         return (true, $"{companion.Name} breaks off the patrol of {p.SettlementName} and rides back to you with the men.");
      }

      /// <summary>The player's letter, now delivered, orders the patrol home: it turns back at once.</summary>
      public void ObeyLetterToStop(string companionId)
      {
         CompanionPatrol p = _patrols.FirstOrDefault(x => x.CompanionId == companionId && x.State == CompanionPatrolState.Patrolling);
         if (p == null) return;

         p.RecallOrderDay = Today;
         ModLog.Info($"[PATROL] {p.CompanionName} reads the player's letter to come back; turning for home.");
      }

      /// <summary>A letter ordering the patrol home: it takes effect when the courier reaches it.</summary>
      public (bool Sent, string Message) OrderRecallByLetter(Hero companion)
      {
         CompanionPatrol p = _patrols.FirstOrDefault(x => x.CompanionId == companion?.StringId);
         if (p == null) return (false, $"{companion?.Name} is not on patrol.");

         p.RecallOrderDay = Today + LetterRecallDays;
         ModLog.Info($"[PATROL] recall letter to {p.CompanionName}; takes effect day {p.RecallOrderDay}.");

         return (true, $"A courier rides out with your order for {companion.Name} to end the patrol and come back.");
      }

      private void BringHome(CompanionPatrol p, Hero companion, MobileParty party)
      {
         _patrols.Remove(p);
         int men = BringHomeMembers(party);
         if (companion.PartyBelongedTo != MobileParty.MainParty)
            try { AddHeroToPartyAction.Apply(companion, MobileParty.MainParty, false); }
            catch (Exception ex) { ModLog.Error($"[PATROL] rejoin failed for {p.CompanionName}: {ex.Message}"); }

         TravelingParty.Destroy(party);

         string record = p.BattlesWon + p.BattlesLost == 0
            ? "without a fight"
            : $"after {p.BattlesWon} victor{(p.BattlesWon == 1 ? "y" : "ies")} and {p.BattlesLost} defeat{(p.BattlesLost == 1 ? "" : "s")}";
         Remember(companion, $"Patrolled around {p.SettlementName} for {Today - p.StartDay} days {record}, and brought {men} men back to {Hero.MainHero.Name}.");
         Notify($"{p.CompanionName} is back from {p.SettlementName} with {men} men, {record}.", companion);
         ModLog.Info($"[PATROL] {p.CompanionName} home: {men} men handed over; won {p.BattlesWon}, lost {p.BattlesLost}, foes beaten {p.FoesBeaten}.");
      }

      /// <summary>Moves every member but the leader (soldiers with their wounds, companions, prisoners, food) to the player. Returns the soldiers moved.</summary>
      private static int BringHomeMembers(MobileParty party)
      {
         var soldiers = 0;
         Hero leader = party.LeaderHero;
         foreach (TroopRosterElement e in party.MemberRoster.GetTroopRoster().ToList())
         {
            if (e.Character == null) continue;
            if (e.Character.IsHero)
            {
               Hero h = e.Character.HeroObject;
               if (h != null && h != leader)
                  try { AddHeroToPartyAction.Apply(h, MobileParty.MainParty, false); }
                  catch (Exception ex) { ModLog.Error($"[PATROL] {h.Name} could not rejoin: {ex.Message}"); }

               continue;
            }

            party.MemberRoster.AddToCounts(e.Character, -e.Number, false, -e.WoundedNumber, -e.Xp);
            MobileParty.MainParty.MemberRoster.AddToCounts(e.Character, e.Number, false, e.WoundedNumber, e.Xp);
            soldiers += e.Number;
         }

         foreach (TroopRosterElement e in party.PrisonRoster.GetTroopRoster().Where(e => e.Character is {IsHero: false}).ToList())
         {
            party.PrisonRoster.AddToCounts(e.Character, -e.Number, false, -e.WoundedNumber, -e.Xp);
            MobileParty.MainParty.PrisonRoster.AddToCounts(e.Character, e.Number, false, e.WoundedNumber, e.Xp);
         }

         MoveFood(party, MobileParty.MainParty, party.ItemRoster.TotalFood);

         return soldiers;
      }

      // ── Plumbing ──────────────────────────────────────────────────────────

      private static void Remember(Hero companion, string memory)
      {
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         NpcProfile profile = companion == null ? null : store?.Get(companion.StringId);
         if (profile == null) return;

         profile.Events.Add(new NotableEvent(Today, NotableEventType.Collaboration, memory));
         store.Set(profile);
      }

      private static void Notify(string text, Hero about)
         => MBInformationManager.AddQuickInformation(new TextObject(Mapping.ShownText.Of(text)), 0, about?.CharacterObject);

      private static MobileParty FindParty(string id)
         => string.IsNullOrEmpty(id) ? null : MobileParty.All.FirstOrDefault(p => p.StringId == id);

      private static Hero HeroById(string id)
         => string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

      // ── cr.testall seams ──────────────────────────────────────────────────

      /// <summary>Confirms a patrol with <paramref name="men" /> of the player's healthy soldiers, lowest tier first, as the screen would.</summary>
      internal string DebugConfirm(Hero companion, Settlement target, int men)
      {
         TroopRoster chosen = TroopRoster.CreateDummyTroopRoster();
         chosen.AddToCounts(companion.CharacterObject, 1);
         foreach (TroopRosterElement e in MobileParty.MainParty.MemberRoster.GetTroopRoster().Where(e => e.Character is {IsHero: false}).OrderBy(e => e.Character.Tier).ToList())
         {
            if (men <= 0) break;
            int take = Math.Min(e.Number - e.WoundedNumber, men);
            if (take <= 0) continue;
            chosen.AddToCounts(e.Character, take);
            men -= take;
         }

         return Confirm(companion, target, chosen);
      }

      /// <summary>Runs the hourly handler now.</summary>
      internal void DebugHour() => OnHourlyTick();

      /// <summary>The patrol's state and the day of its last letter, for cr.testall.</summary>
      internal (CompanionPatrolState State, int LastReportDay)? DebugStateOf(Hero companion)
      {
         CompanionPatrol p = _patrols.FirstOrDefault(x => x.CompanionId == companion?.StringId);

         return p == null ? null : (p.State, p.LastReportDay);
      }

      /// <summary>The patrol's party, for cr.testall.</summary>
      internal MobileParty DebugPartyOf(Hero companion) => FindParty(_patrols.FirstOrDefault(x => x.CompanionId == companion?.StringId)?.PartyId);

      /// <summary>Brings the patrol home at once, wherever it is (cr.testall cleanup and the console).</summary>
      internal string DebugBringHomeNow(Hero companion)
      {
         CompanionPatrol p = _patrols.FirstOrDefault(x => x.CompanionId == companion?.StringId);
         if (p == null) return $"{companion?.Name} is not on patrol.";
         MobileParty party = FindParty(p.PartyId);
         if (party == null)
         {
            Scattered(p, companion, null);

            return "The patrol party was gone; closed.";
         }

         BringHome(p, companion, party);

         return $"{companion.Name} and the patrol's men are back.";
      }

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
         CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
         CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_patrols, Formatting.None)); }
            catch (Exception ex)
            {
               ModLog.Error($"[PATROL] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
               bytes = Encoding.UTF8.GetBytes("[]");
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex) { ModLog.Error($"[PATROL] save failed: {ex.GetType().Name}: {ex.Message}"); }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in older saves */
            }

            if (bytes is {Length: > 0})
               try { _patrols = JsonConvert.DeserializeObject<List<CompanionPatrol>>(Encoding.UTF8.GetString(bytes)) ?? new List<CompanionPatrol>(); }
               catch (Exception ex)
               {
                  _patrols = new List<CompanionPatrol>();
                  ModLog.Error($"[PATROL] load failed, dropping patrols so the campaign opens. {ex.GetType().Name}: {ex.Message}");
               }

            ModLog.Info($"[PATROL] loaded {_patrols.Count} patrol(s) from save.");
         }
      }

      private sealed class PendingOrder
      {
         public string CompanionId { get; set; }
         public int? Men { get; set; }
         public string SettlementId { get; set; }
      }
   }
}
