// Code written by Gabriel Mailhot, 10/08/2026.
// War-council OFFER trigger (player suggestion): after the player's army forms or takes a considerable action
// (capturing a settlement, winning a large pitched battle), OFFER to convene the already-built WarCouncil rather
// than making the player type cr.council war. The council SESSION itself is unchanged (the multi-speaker window
// pivoting on the anchor already delivers the "lords gather to discuss the next course of action" behaviour); this
// behavior only decides WHEN to surface the offer. GATED OFF by default (Enabled): the council window is still
// being validated in-game, so this must not reach players yet. Console: cr.warcouncil [on|off|now].

#region

using System;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.WarCouncil
{
   /// <summary>
   ///   Watches the campaign events that mark a considerable army action and, when the player commands an army and
   ///   the anti-spam cooldown has elapsed, offers to convene the war council. The decision rule is the pure
   ///   <see cref="WarCouncilTriggerPolicy" />; this behavior only reads live engine state into its flags and, on
   ///   acceptance, reuses the exact convening path <c>cr.council war</c> takes
   ///   (<see cref="CouncilConversationStager.StageCouncil" />).
   /// </summary>
   public sealed class WarCouncilOfferBehavior : CampaignBehaviorBase
   {
      // Additive, save-safe cooldown key (single int). Absent on older saves -> deserialize leaves the sentinel
      // below untouched, which reads as "never offered" so the first eligible action after loading may offer.
      private const string LastOfferDaySaveKey = "CalradiaRemembers_WarCouncilLastOfferDay_v1";

      /// <summary>
      ///   Kill-switch, OFF by default: the council window is still being validated in-game, so the offer must not
      ///   surface to players yet. A CODE default, never persisted; armed for a session with <c>cr.warcouncil on</c>.
      /// </summary>
      public static bool Enabled;

      public static WarCouncilOfferBehavior Instance;

      // Game day of the last offer shown; -1 = never offered (the sentinel an older save with no key keeps).
      private int _lastOfferDay = -1;

      public WarCouncilOfferBehavior()
      {
         Instance = this;
      }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         // army formed (the gathered host is the "lords gather" moment); a settlement taken by the player's side;
         // and a notable battle the player's army won. Mirrors WorldEventFeeders' own subscriptions.
         CampaignEvents.ArmyGathered.AddNonSerializedListener(this, OnArmyGathered);
         CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
         CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
      }

      public override void SyncData(IDataStore dataStore)
      {
         // Additive single-int key. An older save simply lacks it and keeps -1 (never offered); on that first load
         // the next eligible action may offer, which is harmless. Guarded so a missing key never breaks load.
         try { dataStore.SyncData(LastOfferDaySaveKey, ref _lastOfferDay); }
         catch { /* key absent in older saves */ }
      }

      // ── Debug / self-test seams (no UI, no council opened) ────────────────

      /// <summary>
      ///   Self-test seam (cr.testall war_council_offer): runs the REAL arm + policy + persisted-cooldown decision
      ///   for the supplied flags, WITHOUT reading a live army, checking the council can open, or showing any UI.
      ///   Returns true only when the offer is armed AND <see cref="WarCouncilTriggerPolicy.ShouldOffer" /> agrees,
      ///   so a test can assert "would offer" / "would not offer" deterministically.
      /// </summary>
      public bool DebugWouldOffer(bool playerInOrLeadsArmy, bool actionIsNotable)
         => Enabled && WarCouncilTriggerPolicy.ShouldOffer(
               playerInOrLeadsArmy, actionIsNotable, DaysSinceLastOffer(), WarCouncilTriggerPolicy.DefaultCooldownDays);

      /// <summary>The persisted last-offer day (-1 = never), so a self-test can read and restore it.</summary>
      public int DebugLastOfferDay => _lastOfferDay;

      /// <summary>Sets the persisted last-offer day directly (self-test only): stamp "today" to prove the cooldown bites, or a stale day to prove it clears.</summary>
      public void DebugSetLastOfferDay(int day) => _lastOfferDay = day;

      /// <summary>Forces the offer inquiry to appear now (cr.warcouncil now), bypassing the gates, so the whole convene path can be exercised on demand.</summary>
      public void ForceOfferNow() => ShowOffer();

      /// <summary>A one-line report of the current gate state for the console status path.</summary>
      public string DescribeState()
      {
         string cooldown = _lastOfferDay < 0
            ? "never offered"
            : $"last offered day {_lastOfferDay} ({Today - _lastOfferDay} day(s) ago), cooldown {WarCouncilTriggerPolicy.DefaultCooldownDays} day(s)";

         return $"War-council offer: {(Enabled ? "ARMED" : "DISARMED (shipped default)")}. {cooldown}.";
      }

      #region private

      private int? DaysSinceLastOffer() => _lastOfferDay < 0 ? (int?) null : Today - _lastOfferDay;

      // ── Event handlers (each fail-soft: an offer trigger must never crash an engine event) ──

      private void OnArmyGathered(Army army, IMapPoint gatheringPoint)
      {
         try
         {
            // The gathered host must be the player's own army (the "lords gather round the leader" moment). The
            // in-army gate in Consider re-confirms it, but this keeps another lord's muster from even being considered.
            bool playerArmy = army != null && MobileParty.MainParty?.Army != null
                           && (army == MobileParty.MainParty.Army || army.LeaderParty == MobileParty.MainParty);
            if (!playerArmy) return;

            Consider(actionIsNotable: true);
         }
         catch (Exception ex) { ModLog.Error($"[WARCOUNCIL] army-gathered handler failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnSettlementOwnerChanged(
         Settlement settlement,
         bool openToClaim,
         Hero newOwner,
         Hero oldOwner,
         Hero capturerHero,
         ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
      {
         try
         {
            if (settlement == null) return;

            // "Captured by the player's side": the new owner is the player, or belongs to the player's own faction.
            IFaction playerFaction = Hero.MainHero?.MapFaction;
            bool playerSide = newOwner == Hero.MainHero
                           || (newOwner?.MapFaction != null && playerFaction != null && newOwner.MapFaction == playerFaction);
            if (!playerSide) return;

            Consider(actionIsNotable: true);
         }
         catch (Exception ex) { ModLog.Error($"[WARCOUNCIL] settlement-owner handler failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void OnMapEventEnded(MapEvent mapEvent)
      {
         try
         {
            if (!PlayerWonNotableBattle(mapEvent)) return;

            Consider(actionIsNotable: true);
         }
         catch (Exception ex) { ModLog.Error($"[WARCOUNCIL] map-event handler failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── The decision ──────────────────────────────────────────────────────

      private void Consider(bool actionIsNotable)
      {
         if (!Enabled) return;

         bool inArmy = false;
         try { inArmy = MobileParty.MainParty?.Army != null; }
         catch { /* never let a state read crash an event handler */ }

         if (!WarCouncilTriggerPolicy.ShouldOffer(inArmy, actionIsNotable, DaysSinceLastOffer(), WarCouncilTriggerPolicy.DefaultCooldownDays))
            return;

         // Never offer what cannot be honoured: the SAME gate cr.council consults (chat open / captive / in a
         // scene) plus a non-empty roster, so the player is not offered a council that would open on nobody.
         if (!CouncilCanOpenNow()) return;

         ShowOffer();
      }

      // Mirrors ChatManager.CouncilOpenBlockedReason (the gate cr.council consults) via the same tested policy,
      // then confirms the war-council roster would actually seat someone.
      private static bool CouncilCanOpenNow()
      {
         try
         {
            if (CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = ChatManager.IsOpen, PlayerIsPrisoner = Hero.MainHero?.IsPrisoner == true, InMissionScene = Mission.Current != null})
                != CouncilAccessBlock.None)
               return false;

            return CouncilRosterResolver.Seated(CouncilScope.WarCouncil).Count > 0;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[WARCOUNCIL] council-open check failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      private void ShowOffer()
      {
         // Stamp on SHOW, not on accept: the cooldown must space out OFFERS regardless of whether the player
         // convenes or defers, or a player who keeps choosing "Later" would be re-nagged on every battle.
         _lastOfferDay = Today;

         try
         {
            InformationManager.ShowInquiry(new InquiryData(
               "War Council",
               "Your captains would take counsel on what comes next.",
               true, true,
               "Convene now", "Later",
               Convene, () => { }), true);
         }
         catch (Exception ex) { ModLog.Error($"[WARCOUNCIL] show-offer failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // Reuses the exact convening path cr.council war takes: the VRAM-safe conversation-staged council, not the
      // raw ChatManager.OpenForCouncil overlay (StageCouncil routes to it internally once the scene has loaded).
      private void Convene()
      {
         try
         {
            string result = CouncilConversationStager.StageCouncil(CouncilScope.WarCouncil);
            ModLog.Info($"[WARCOUNCIL] convene -> {result}");

            // If staging did not start (empty war roster: no allied lords, or the player leads no faction at war),
            // the council never opens. Surface WHY to the player instead of the popup simply doing nothing (player
            // report 2026-08-23: "the popup appears but nothing happens after"); cr.council war already reports it
            // to the console, but this offer path swallowed it.
            if (!CouncilConversationStager.IsStaging && !string.IsNullOrEmpty(result))
               InformationManager.DisplayMessage(new InformationMessage(result));
         }
         catch (Exception ex) { ModLog.Error($"[WARCOUNCIL] convene failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      // ── Battle notability (twin of WorldEventFeeders' own player-battle detection; BattleNotability is shared) ──

      private static bool PlayerWonNotableBattle(MapEvent mapEvent)
      {
         if (mapEvent == null || mapEvent.WinningSide == BattleSideEnum.None) return false;

         bool playerAttacker = mapEvent.AttackerSide?.IsMainPartyAmongParties() == true;
         bool playerDefender = mapEvent.DefenderSide?.IsMainPartyAmongParties() == true;
         if (!playerAttacker && !playerDefender) return false;

         bool playerWon = mapEvent.WinningSide == (playerAttacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender);
         if (!playerWon) return false;

         bool isSiege = mapEvent.EventType == MapEvent.BattleTypes.Siege;
         bool attackerHasArmy = SideHasArmy(mapEvent, BattleSideEnum.Attacker);
         bool defenderHasArmy = SideHasArmy(mapEvent, BattleSideEnum.Defender);
         int total = (mapEvent.GetMapEventSide(BattleSideEnum.Attacker)?.TroopCount ?? 0)
                   + (mapEvent.GetMapEventSide(BattleSideEnum.Defender)?.TroopCount ?? 0);

         return BattleNotability.IsNotableForPlayer(new BattleNotability.BattleFacts {IsSiege = isSiege, AttackerHasArmy = attackerHasArmy, DefenderHasArmy = defenderHasArmy, TotalCombatants = total});
      }

      private static bool SideHasArmy(MapEvent mapEvent, BattleSideEnum side)
      {
         MBReadOnlyList<MapEventParty> parties = mapEvent.GetMapEventSide(side)?.Parties;

         if (parties == null) return false;

         foreach (MapEventParty p in parties)
            if (p.Party?.MobileParty?.Army != null)
               return true;

         return false;
      }

      #endregion
   }
}
