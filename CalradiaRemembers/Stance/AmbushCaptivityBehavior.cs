// Code written by Gabriel Mailhot, 28/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Travel;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Capture-and-convoy after a lost town ambush: the paymaster's men carry the player to their lord.</summary>
   public sealed class AmbushCaptivityBehavior : CampaignBehaviorBase
   {
      private const int CaptorEscort = 5; // a handful of cutthroats — enough to hold a beaten captive, still beatable
      private const float ArrivalDistance = 2.5f; // a bandit band can't ENTER the lord's town, so "arrived" = this close
      private const float ApproachRange = 6f; // when the convoy is this near the lord's seat, the lord rides out to meet it
      private const int RescueForce = 15; // troops a rescuing companion brings — enough to beat the small captor band
      public static AmbushCaptivityBehavior Instance;
      private bool _arrivedLogged; // transient: the meeting with the lord has happened (fires once)
      private bool _lordNudged; // transient: we've ordered the paymaster's party to ride out to the convoy
      private bool _rescueMounted; // transient: the companions have already ridden to the rescue (one attempt)
      private int _rescueRollDay = -1; // transient: the last day we rolled for a rescue (one roll per day)
      private string _chiefId;

      // The live convoy (the captor party + the paymaster + the temp bandit chief), persisted so a save
      // mid-journey survives. The chief is a throwaway hero we create to LEAD the party (a leaderless looter
      // band ignores move orders) and remove when the convoy ends.
      private string _convoyPartyId;
      private float _ejectAccum; // transient: real-time accumulator to retry the auto-"Leave" a few times
      private int _ejectTries; // transient: how many auto-"Leave" attempts so far (manual leave is the fallback)
      private bool _endedPostFightMission; // transient: ended the post-defeat town walkabout once per arming
      private bool _leaving; // transient: re-entrancy guard while running a menu's Leave consequence
      private int _menuLeaveTries; // transient: how many menu auto-leaves attempted this arming (loop guard)
      private string _paymasterId;
      private bool _timeLocked; // transient: we locked the clock to play so the paused map can tick the capture

      // The capture is ARMED here but only SPRINGS on a later map tick, once the player is on the open map
      // (not in a town menu / encounter / mission). Doing the spawn + captivity synchronously from a console
      // or menu callback, mid-frame and mid-encounter-teardown, leaves the captor party on a half-built navmesh
      // face and crashes the native pathfinding on the next cursor hover. Deferring to a stable map tick fixes it.
      private string _pendingCaptureLordId;
      private bool _waitLogged; // transient: log the "armed, waiting" reason once per arming, not every frame

      public AmbushCaptivityBehavior()
      {
         Instance = this;
      }

      /// <summary>True between a lost ambush and the capture springing — used to bar the player re-entering a town.</summary>
      public bool IsCapturePending => !string.IsNullOrEmpty(_pendingCaptureLordId);

      /// <summary>
      ///   True when the given party is our live ambush convoy. The random captive-encounter roll consults this
      ///   so it never fires a scene with the throwaway bandit chief that leads the band — the ambush owns its
      ///   own scene (the paymaster lord's audience on arrival).
      /// </summary>
      public bool IsAmbushCaptor(MobileParty party)
         => party != null && !string.IsNullOrEmpty(_convoyPartyId) && party.StringId == _convoyPartyId;

      /// <summary>Console/test seam: open the 3.2b gloating audience with a lord now (skips the convoy journey).</summary>
      public static bool DebugLordAudience(Hero paymaster) => paymaster != null && TryOpenLordAudience(paymaster);

      /// <summary>
      ///   ARMS the capture: the player has lost the ambush, and the paymaster's men will take them the moment
      ///   they are out on the open road. It does NOT capture on the spot (that corrupts the map navmesh) — it
      ///   springs on the next stable map tick. Returns false only if there is no valid paymaster.
      /// </summary>
      public bool Capture(Hero paymaster)
      {
         if (paymaster == null || Hero.MainHero == null) return false;
         if (Hero.MainHero.IsPrisoner)
         {
            ModLog.Info("[AMBUSH] player is already a prisoner — capture not armed.");

            return false;
         }

         _pendingCaptureLordId = paymaster.StringId;
         _waitLogged = false;
         _ejectAccum = 0f;
         _ejectTries = 0;
         _endedPostFightMission = false;
         _leaving = false;
         _menuLeaveTries = 0;
         _timeLocked = false;
         _arrivedLogged = false;
         _lordNudged = false;
         _rescueMounted = false;
         _rescueRollDay = -1;
         ModLog.Info($"[AMBUSH] capture armed by {paymaster.Name} — it will spring once the player is on the open map.");

         return true;
      }

      public override void RegisterEvents()
      {
         CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
         CampaignEvents.MissionTickEvent.AddNonSerializedListener(this, OnMissionTick);
         // The town menu PAUSES the campaign clock, so neither tick fires there. GameMenuOpened is an event,
         // not a tick — it fires even while paused — so it is the reliable hook to auto-leave after a lost ambush.
         CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
         // After the auto-leave the map is PAUSED, so the capture tick won't fire (and the player could re-enter
         // the town to slip it). This event fires the instant the town is left — we use it to nudge the player
         // away from the gates and resume time, so the capture springs at once.
         CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
         dataStore.SyncData("CR_AmbushConvoyPartyId", ref _convoyPartyId);
         dataStore.SyncData("CR_AmbushPaymasterId", ref _paymasterId);
         dataStore.SyncData("CR_AmbushChiefId", ref _chiefId);
         dataStore.SyncData("CR_AmbushPendingLordId", ref _pendingCaptureLordId);
      }

      #region private

      private void ClearConvoy()
      {
         // Remove the throwaway bandit chief so we don't leak a hero into the world.
         if (!string.IsNullOrEmpty(_chiefId))
         {
            Hero chief = Hero.FindFirst(h => h != null && h.StringId == _chiefId);
            if (chief is {IsAlive: true}) KillCharacterAction.ApplyByRemove(chief);
         }

         _convoyPartyId = null;
         _paymasterId = null;
         _chiefId = null;

         ReleaseTime(); // journey over — hand time control back to the player
      }

      /// <summary>
      ///   3.2b — the gloating audience. The band has brought the player to the lord's seat; the lord who paid for
      ///   the abduction now receives the captive and savours it. We do NOT transfer custody to the town's party
      ///   (the paymaster rarely owns the settlement, and the transfer makes the player some OTHER lord's prisoner,
      ///   whom the captivity system then releases) — the player stays the band's captive, and after the audience
      ///   vanilla escape eventually frees them. The lord-owned imprisonment / ransom disposition is future work.
      /// </summary>
      private void TheMeeting(Hero paymaster, MobileParty convoy, MobileParty lordParty)
      {
         if (_arrivedLogged) return;
         _arrivedLogged = true;

         ReleaseTime(); // journey over — hand the clock back so the meeting runs at the player's pace

         // If the lord came in person, transfer custody to HIS party (a real lord-imprisonment — ransom, dungeon,
         // and all) and disband the bandits. Transferring to his OWN mobile party is what avoids the release the
         // old settlement-garrison transfer caused. If he could not come (no party / busy), the band keeps the
         // player and vanilla escape eventually frees them; the at-seat audience is the fallback either way.
         bool transferred = lordParty != null && TransferToLord(paymaster, lordParty, convoy);
         ModLog.Info($"[AMBUSH] the meeting at {paymaster.Name}'s seat — custody {(transferred ? "transferred to the lord" : "kept by the band")}.");

         if (TryOpenLordAudience(paymaster)) return;

         InformationManager.DisplayMessage(new InformationMessage(
            $"{paymaster.Name} receives you. You are a captive now, at their mercy."));
      }

      /// <summary>
      ///   The paymaster is dead, captured, or otherwise gone, so the band cuts the player loose and scatters.
      ///   Releases captivity through the captor's "choice" (the chief), shows a flavour line, and disbands the band.
      /// </summary>
      private void ReleaseCaptive(MobileParty convoy, string reason)
      {
         _arrivedLogged = true;
         ReleaseTime();

         try
         {
            if (Hero.MainHero.IsPrisoner)
            {
               Hero releaser = Hero.FindFirst(h => h != null && h.StringId == _chiefId);
               if (releaser != null) EndCaptivityAction.ApplyByReleasedByChoice(Hero.MainHero, releaser);
               else EndCaptivityAction.ApplyByReleasedAfterBattle(Hero.MainHero);
            }
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] release failed: {ex.GetType().Name}: {ex.Message}"); }

         InformationManager.DisplayMessage(new InformationMessage(
            "Your captors fall to muttering among themselves. Then, with no word of why, they cut your bonds and wave " +
            "you off the road — go, they growl, before they think better of it.", Colors.Yellow));

         if (convoy is {IsActive: true}) TravelingParty.Destroy(convoy);
         ClearConvoy();
         ModLog.Info($"[AMBUSH] captive released — {reason}.");
      }

      /// <summary>
      ///   Hands the player from the bandit convoy to the lord's own party (a verified re-capture), then disbands
      ///   the band. Returns false if the transfer did not hold (then the band keeps the player and we do not
      ///   touch the convoy).
      /// </summary>
      private bool TransferToLord(Hero paymaster, MobileParty lordParty, MobileParty convoy)
      {
         try
         {
            TakePrisonerAction.Apply(lordParty.Party, Hero.MainHero); // re-assign custody to the lord

            bool held = Hero.MainHero.IsPrisoner && PlayerCaptivity.CaptorParty == lordParty.Party;
            ModLog.Info($"[AMBUSH] transfer to {paymaster.Name}: held={held}, captor={PlayerCaptivity.CaptorParty?.Name?.ToString() ?? "none"}.");
            if (!held) return false;

            // The player is the lord's prisoner now, so disbanding the band cannot release them. The lord's own AI
            // resumes (we never hard-locked it) and carries the prisoner home.
            if (convoy is {IsActive: true}) TravelingParty.Destroy(convoy);
            ClearConvoy();

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AMBUSH] transfer to {paymaster.Name} failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>Opens the captive encounter with the paymaster lord as captor (the reproachful gloat). False if it can't.</summary>
      private static bool TryOpenLordAudience(Hero paymaster)
      {
         try
         {
            BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;
            NpcProfile profile = store?.Get(paymaster.StringId);
            if (profile == null)
            {
               ModLog.Info($"[AMBUSH] no stored profile for {paymaster.Name} — the gloating audience is skipped.");

               return false;
            }

            ModLog.Info($"[AMBUSH] opening the lord's reckoning with {paymaster.Name}.");
            ChatManager.OpenForCaptiveEncounter(paymaster, profile, new List<WitnessEntry>(), false, CaptiveSceneIntent.Reckoning);

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AMBUSH] lord audience failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      private void DoCapture(Hero paymaster)
      {
         // Belt-and-suspenders: never capture unless the player is fully on the open map. The game can still
         // report a settlement/encounter/menu for a tick or two right after a town mission ends.
         if (Hero.MainHero.CurrentSettlement != null || PlayerEncounter.Current != null || Campaign.Current.CurrentMenuContext != null)
         {
            ModLog.Info("[AMBUSH] not fully on the open map yet — re-arming the capture for a later tick.");
            _pendingCaptureLordId = paymaster.StringId;

            return;
         }

         Settlement destination = paymaster.HomeSettlement ?? paymaster.CurrentSettlement ?? paymaster.PartyBelongedTo?.CurrentSettlement;

         // We are on the open map now, so spawn at the player party's own (valid) position.
         Vec2 spawnPos = Hero.MainHero.PartyBelongedTo?.GetPosition2D ?? destination?.GetPosition2D ?? default;
         ModLog.Info($"[AMBUSH] springing capture on the open map. spawnPos=({spawnPos.x:0.0},{spawnPos.y:0.0}), dest={destination?.Name?.ToString() ?? "none"}.");

         MobileParty convoy = SpawnCaptors(spawnPos, destination);
         if (convoy == null)
         {
            ModLog.Error("[AMBUSH] failed to spawn the captor party.");

            return;
         }

         Vec2 partyPos = convoy.GetPosition2D;
         ModLog.Info($"[AMBUSH] captor party spawned: id={convoy.StringId}, active={convoy.IsActive}, pos=({partyPos.x:0.0},{partyPos.y:0.0}), members={convoy.MemberRoster.TotalManCount}, leader={convoy.LeaderHero?.Name?.ToString() ?? "none"}.");

         TakePrisonerAction.Apply(convoy.Party, Hero.MainHero); // also flips the player into captive state (StartCaptivity)

         InformationManager.DisplayMessage(new InformationMessage(
            "You come to, bound and disarmed in the back of a cart. The cutthroats are carrying you off to whoever paid them.", Colors.Red));

         ModLog.Info($"[AMBUSH] after capture: IsPrisoner={Hero.MainHero.IsPrisoner}, IsCaptive={PlayerCaptivity.IsCaptive}, captor={PlayerCaptivity.CaptorParty?.Name?.ToString() ?? "none"}.");

         if (destination != null) TravelingParty.DirectToSettlement(convoy, destination);
         else if (paymaster.PartyBelongedTo != null) convoy.SetMoveEscortParty(paymaster.PartyBelongedTo, MobileParty.NavigationType.Default, false);

         _convoyPartyId = convoy.StringId;
         _paymasterId = paymaster.StringId;

         ModLog.Info($"[AMBUSH] {paymaster.Name}'s men took the player prisoner — convoy '{convoy.StringId}' bound for {destination?.Name?.ToString() ?? paymaster.Name?.ToString()}.");

         // Vanilla captivity can leave the map paused; keep the clock running so the captor convoy actually moves
         // toward the paymaster (and OnHourlyTick keeps re-steering it). Released when the convoy ends.
         ForceTimeRunning();
      }

      /// <summary>
      ///   Forces the campaign clock to FAST-FORWARD (unstoppable) and holds it there until
      ///   <see cref="ReleaseTime" />. Releasing to a player-controllable mode mid-journey lets vanilla captivity
      ///   re-pause the map, which freezes the captor convoy — so we keep it forced (the player has no control)
      ///   but at top speed, so the trip to the paymaster is quick, until the journey ends (arrival or escape).
      /// </summary>
      private void ForceTimeRunning()
      {
         if (_timeLocked || Campaign.Current == null) return;

         Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
         Campaign.Current.SetTimeControlModeLock(true);
         _timeLocked = true;
      }

      /// <summary>Releases the forced clock, handing time control back to the player.</summary>
      private void ReleaseTime()
      {
         if (!_timeLocked || Campaign.Current == null) return;

         Campaign.Current.SetTimeControlModeLock(false);
         Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppablePlay;
         _timeLocked = false;
      }

      private void OnHourlyTick()
      {
         try
         {
            if (string.IsNullOrEmpty(_convoyPartyId)) return;

            MobileParty convoy = MobileParty.All.Find(p => p.StringId == _convoyPartyId);
            Hero paymaster = string.IsNullOrEmpty(_paymasterId)
               ? null
               : Hero.FindFirst(h => h != null && h.StringId == _paymasterId);

            // Convoy gone (beaten/disbanded) or the player slipped captivity → the journey is over for now.
            if (convoy is not {IsActive: true} || !Hero.MainHero.IsPrisoner)
            {
               ClearConvoy();

               return;
            }

            // 3.2c: while convoyed, the player's free companions may ride to cut them loose (a real map battle).
            TryRescueRoll(convoy);

            if (_arrivedLogged) return; // the meeting already happened — the lord (or the band) holds the player now

            // (b)/(c) the lord is dead or himself taken prisoner — there is no one to deliver to (and no one
            // paying), so the cutthroats cut the player loose and melt away.
            if (paymaster == null || !paymaster.IsAlive || paymaster.IsPrisoner)
            {
               ReleaseCaptive(convoy, paymaster == null ? "paymaster gone" : !paymaster.IsAlive ? "paymaster dead" : "paymaster captured");

               return;
            }

            // Head to where the lord ACTUALLY is (the town he's sitting in, else his home), not blindly to home.
            Settlement target = paymaster.CurrentSettlement ?? paymaster.HomeSettlement;
            if (target == null) return;

            MobileParty lordParty = paymaster.PartyBelongedTo;
            Vec2 convoyPos = convoy.GetPosition2D;

            // (a) the lord is mid-battle — the band waits nearby until it's over (never meet or transfer into a
            // battle). Hold position and try again next hour.
            if (lordParty?.MapEvent != null)
            {
               convoy.SetMoveModeHold();

               return;
            }

            // (d) the lord is at sea — a land band cannot meet him on the water. He is only "reachable" on land.
            bool lordReachable = lordParty is {IsActive: true} && !lordParty.IsCurrentlyAtSea;
            // Many paymasters (ladies, garrisoned lords) have NO field party — they sit in a town. Detect their
            // presence by the hero's own settlement, so the at-the-gates audience still fires for them.
            bool lordInTown = paymaster.CurrentSettlement != null && paymaster.CurrentSettlement == target;

            // The map handoff (with custody transfer) fires when the lord's own party comes alongside the convoy.
            bool lordAlongside = lordReachable && convoyPos.Distance(lordParty.GetPosition2D) <= ArrivalDistance;
            bool convoyAtTarget = convoy.CurrentSettlement == target || convoyPos.Distance(target.GetPosition2D) <= ArrivalDistance;

            ModLog.Info($"[AMBUSH] convoy tick — target={target.Name}, dist={convoyPos.Distance(target.GetPosition2D):0.0}, " +
                        $"lordParty={(lordParty != null ? "yes" : "none")}, lordAtSea={lordParty?.IsCurrentlyAtSea}, lordInTown={lordInTown}, " +
                        $"alongside={lordAlongside}, atTarget={convoyAtTarget}, nudged={_lordNudged}.");

            if (lordAlongside) { TheMeeting(paymaster, convoy, lordParty); return; }        // map handoff + transfer
            if (convoyAtTarget && lordInTown) { TheMeeting(paymaster, convoy, null); return; } // lord in his town — audience at the gates

            // Still travelling, or waiting for the lord (at sea / abroad): keep the band pointed at the lord's
            // town, and once near, if the lord HAS a party he rides OUT to meet them on the map (a soft escort
            // order, once — his AI is never hard-locked, so he resumes after). If he never comes, vanilla escape frees you.
            TravelingParty.DirectToSettlement(convoy, target);
            if (!_lordNudged && lordReachable && !lordParty.IsMainParty
                && convoyPos.Distance(target.GetPosition2D) <= ApproachRange)
            {
               _lordNudged = true;
               try
               {
                  lordParty.SetMoveEscortParty(convoy, MobileParty.NavigationType.Default, false);
                  ModLog.Info($"[AMBUSH] {paymaster.Name} rides out from {target.Name} to meet the convoy.");
               }
               catch (Exception ex) { ModLog.Error($"[AMBUSH] lord ride-out failed: {ex.GetType().Name}: {ex.Message}"); }
            }
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] convoy tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   The campaign tick (OnTick) is FROZEN while a mission runs. After a lost ambush the player is dropped
      ///   into the town-centre walkabout mission, where OnTick can never fire to auto-leave — so we run here, on
      ///   the mission tick (which DOES fire during missions). When a capture is armed and we are standing in a
      ///   settlement mission that is NOT the ambush fight itself (that one carries AmbushFightMissionLogic), we
      ///   end it once: that drops us back to the town menu, where OnTick resumes and clicks Leave → map → capture.
      /// </summary>
      private void OnMissionTick(float dt)
      {
         if (string.IsNullOrEmpty(_pendingCaptureLordId) || _endedPostFightMission) return;

         Mission m = Mission.Current;
         if (m == null) return;
         if (m.GetMissionBehavior<AmbushFightMissionLogic>() != null) return; // this IS the ambush fight — leave it be

         _endedPostFightMission = true;
         ModLog.Info($"[AMBUSH] ending the post-fight mission (scene '{m.SceneName}', settlement {Settlement.CurrentSettlement?.Name?.ToString() ?? "none"}) so the campaign tick can leave + capture.");
         try { m.EndMission(); }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] EndMission failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   The instant the town is left with a capture armed: nudge the player party away from the gates and
      ///   resume time. The map sits PAUSED after the menu auto-leave, so without this the capture tick never
      ///   fires and the player can re-click the town to slip the capture. A tick or two of movement and the
      ///   open-map DoCapture springs — the player is a prisoner before they could turn back.
      /// </summary>
      private void OnSettlementLeft(MobileParty party, Settlement settlement)
      {
         if (string.IsNullOrEmpty(_pendingCaptureLordId) || party != MobileParty.MainParty || settlement == null) return;

         Hero paymaster = Hero.FindFirst(h => h != null && h.StringId == _pendingCaptureLordId);
         if (paymaster is not {IsAlive: true}) { _pendingCaptureLordId = null; return; }

         // On the map now — try to spring the capture immediately. DoCapture safely RE-ARMS (no spawn) if the
         // context is not yet fully clean, so this can never crash; re-entry is barred by the access model while
         // pending, so there is no window to slip it. If it deferred, nudge the player off the gates + resume time.
         _pendingCaptureLordId = null;
         ModLog.Info($"[AMBUSH] left {settlement.Name} with capture pending — springing the capture now.");
         DoCapture(paymaster);

         if (string.IsNullOrEmpty(_pendingCaptureLordId)) return; // captured cleanly — done

         try
         {
            Vec2 here = MobileParty.MainParty.GetPosition2D;
            Vec2 away = here - settlement.GetPosition2D;
            if (away.Length > 0.001f)
            {
               away.Normalize();
               MobileParty.MainParty.SetMoveGoToPoint(new CampaignVec2(here + away * 15f, true), MobileParty.NavigationType.Default);
            }

            // StoppablePlay gets auto-stopped by the menu close (and even locking it didn't hold). UnstoppablePlay
            // + lock FORCES the clock to run and cannot be auto-stopped — so the map ticks DoCapture without a
            // manual move, and time keeps running through captivity so the convoy actually travels to the paymaster.
            ForceTimeRunning();
            ModLog.Info($"[AMBUSH] capture deferred — nudged the player from {settlement.Name}'s gates + forced the clock to run.");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] post-leave nudge failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   The reliable auto-leave hook. After a lost ambush the player lands in the town menu, where the campaign
      ///   clock is PAUSED and no tick fires — but this menu-opened EVENT still fires. With a capture armed, we run
      ///   the menu's own Leave consequence the way the engine does on a click — RunConsequencesOfMenuOption with
      ///   the VIRTUAL option index (OnConsequence alone only flags the selection, it does not fire the delegate) —
      ///   dropping the player to the open map where DoCapture springs.
      /// </summary>
      private void OnGameMenuOpened(MenuCallbackArgs args)
      {
         if (string.IsNullOrEmpty(_pendingCaptureLordId) || _leaving) return;

         _leaving = true; // re-entrancy guard: running the consequence can synchronously re-fire this event
         try
         {
            MenuContext ctx = args?.MenuContext ?? Campaign.Current?.CurrentMenuContext;
            GameMenuManager mgr = Campaign.Current?.GameMenuManager;
            if (ctx == null || mgr == null) return;

            if (++_menuLeaveTries > 12) { ModLog.Error($"[AMBUSH] gave up auto-leaving '{ctx.GameMenu?.StringId}' after {_menuLeaveTries} tries."); return; }

            int count = mgr.GetVirtualMenuOptionAmount(ctx);
            for (var i = 0; i < count; i++)
            {
               if (!mgr.GetVirtualMenuOptionIsLeave(ctx, i)) continue;

               ModLog.Info($"[AMBUSH] capture pending — clicking Leave (option {i}) of menu '{ctx.GameMenu?.StringId}'.");
               mgr.RunConsequencesOfMenuOption(ctx, i);

               return;
            }

            ModLog.Info($"[AMBUSH] capture pending — menu '{ctx.GameMenu?.StringId}' has no leave option among {count}.");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] menu auto-leave failed: {ex.GetType().Name}: {ex.Message}"); }
         finally { _leaving = false; }
      }

      /// <summary>
      ///   Automates the "Leave" the player would click after a lost ambush, so captivity can spring without them
      ///   lingering in town. Faithful to the plan: run the CURRENT menu's own Leave consequence (whatever it is)
      ///   via <see cref="MenuContext.OnConsequence" /> — never LeaveSettlementAction (it has stranded the player
      ///   at the gates). Falls back to <see cref="PlayerEncounter.Finish" /> when there is a live encounter but no
      ///   menu yet (the brief post-mission tick).
      /// </summary>
      private void LeaveToMap()
      {
         MenuContext ctx = Campaign.Current?.CurrentMenuContext;
         GameMenuOption leave = ctx != null ? Campaign.Current?.GameMenuManager?.GetLeaveMenuOption(ctx) : null;

         if (ctx != null && leave != null)
         {
            ModLog.Info($"[AMBUSH] auto-leaving via the menu's own Leave option ('{leave.IdString}'), attempt {_ejectTries}.");
            ctx.OnConsequence(leave);

            return;
         }

         if (PlayerEncounter.Current != null)
         {
            ModLog.Info($"[AMBUSH] auto-leaving via PlayerEncounter.Finish, attempt {_ejectTries}.");
            PlayerEncounter.Finish();
         }
      }

      /// <summary>Springs an armed capture, but ONLY in a stable open-map context — never mid-menu/encounter/mission.</summary>
      private void OnTick(float dt)
      {
         if (string.IsNullOrEmpty(_pendingCaptureLordId))
         {
            // Release the forced clock only once the WHOLE sequence is done — no pending capture AND no convoy
            // still travelling (the convoy needs the clock running to reach the paymaster).
            if (_timeLocked && string.IsNullOrEmpty(_convoyPartyId)) ReleaseTime();

            return;
         }

         try
         {
            _ejectAccum += dt;
            Hero paymaster = Hero.FindFirst(h => h != null && h.StringId == _pendingCaptureLordId);
            if (paymaster is not {IsAlive: true})
            {
               _pendingCaptureLordId = null;

               return;
            } // paymaster gone

            // Wait for the player to be free on the open map: not already captive, not in a settlement, not in
            // an encounter, not in a battle, not in a game menu. Springing in any of those corrupts the navmesh.
            if (Hero.MainHero.IsPrisoner)
            {
               _pendingCaptureLordId = null;

               return;
            }

            string gate = Hero.MainHero.CurrentSettlement != null ? "in a settlement"
               : PlayerEncounter.Current != null ? "in an encounter"
               : Hero.MainHero.PartyBelongedTo?.MapEvent != null ? "in a battle"
               : Campaign.Current.CurrentMenuContext != null ? "in a game menu"
               : null;

            if (gate != null)
            {
               // After a defeat the player shouldn't dawdle in the town menu before captivity — eject them onto
               // the open map. CampaignEvents.TickEvent does NOT fire during a mission/conclusion, so by the time
               // we get here the player has already pressed Continue and is back in the town menu — exactly when
               // we want to "click Leave". The game can stay "stuck" for a few ticks after a mission, so we RETRY
               // every ~0.4s real-time for up to 8 tries.
               if ((gate == "in a settlement" || gate == "in an encounter" || gate == "in a game menu")
                   && _ejectTries < 8 && _ejectAccum >= 0.4f)
               {
                  _ejectAccum = 0f;
                  _ejectTries++;
                  LeaveToMap();

                  return;
               }

               if (_waitLogged) return;
               ModLog.Info($"[AMBUSH] capture armed, waiting — player is {gate} (will spring on the open map).");
               _waitLogged = true;

               return;
            }

            _pendingCaptureLordId = null;
            DoCapture(paymaster);
         }
         catch (Exception ex)
         {
            _pendingCaptureLordId = null;
            ModLog.Error($"[AMBUSH] capture spring failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   3.2c — once per day while the player is convoyed, the free clan companions may mount a rescue: the
      ///   chance scales with how many can ride (CompanionRescuePolicy). On success a companion-led clan party
      ///   spawns and ENGAGES the captor convoy; vanilla resolves the battle, and beating the convoy frees the
      ///   player (our convoy-gone cleanup then fires). One attempt per capture.
      /// </summary>
      private void TryRescueRoll(MobileParty convoy)
      {
         if (_rescueMounted) return;

         var today = (int) CampaignTime.Now.ToDays;
         if (today == _rescueRollDay) return; // one roll per day
         _rescueRollDay = today;

         List<Hero> free = FreeRescuers();
         if (free.Count == 0) return;
         if (!CompanionRescuePolicy.ShouldAttempt(free.Count, MBRandom.RandomInt(100))) return;

         Hero leader = free[0];
         MobileParty rescue = SpawnRescuers(leader, convoy);
         if (rescue == null) return;

         _rescueMounted = true;
         try { rescue.SetMoveEngageParty(convoy, MobileParty.NavigationType.Default); }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] rescue engage failed: {ex.GetType().Name}: {ex.Message}"); }

         InformationManager.DisplayMessage(new InformationMessage(
            $"Word reaches you, bound in the cart: {leader.Name} has gathered your people and rides hard to cut you free.", Colors.Green));
         ModLog.Info($"[AMBUSH] rescue mounted by {leader.Name} ({free.Count} free companion(s)) — engaging the convoy.");
      }

      /// <summary>Clan heroes who could ride to the rescue: alive, free, not the player, and not already leading a party.</summary>
      private static List<Hero> FreeRescuers()
         => Clan.PlayerClan == null
            ? new List<Hero>()
            : Clan.PlayerClan.Heroes
                  .Where(h => h is {IsAlive: true} && !h.IsPrisoner && h != Hero.MainHero
                           && (h.PartyBelongedTo == null || h.PartyBelongedTo.LeaderHero != h))
                  .ToList();

      private MobileParty SpawnRescuers(Hero leader, MobileParty convoy)
      {
         try
         {
            Settlement from = leader.CurrentSettlement ?? leader.HomeSettlement;
            if (from == null) { ModLog.Info($"[AMBUSH] rescue: {leader.Name} has no seat to ride from — skipping."); return null; }

            MobileParty party = LordPartyComponent.CreateLordParty(
               "cr_ambush_rescue_" + Guid.NewGuid().ToString("N"),
               leader,
               new CampaignVec2(from.GetPosition2D, true),
               1f,
               from,
               leader);
            if (party == null) return null;

            CharacterObject troop = Hero.MainHero.Culture?.BasicTroop ?? leader.Culture?.BasicTroop;
            if (troop != null) party.MemberRoster.AddToCounts(troop, RescueForce);

            ModLog.Info($"[AMBUSH] rescue party spawned: leader={leader.Name}, from={from.Name}, troops={party.MemberRoster.TotalManCount}.");

            return party;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[AMBUSH] rescue spawn failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private MobileParty SpawnCaptors(Vec2 spawnPos, Settlement spawnAt)
      {
         // Bandit faction = hostile to everyone, so vanilla captivity HOLDS the player. But a LEADERLESS looter
         // band ignores move orders and just sits there — so we give it a throwaway bandit-CHIEF hero and make
         // it a proper LED party (which travels). The chief is removed when the convoy ends.
         Clan bandits = Clan.BanditFactions?.FirstOrDefault(c => c.StringId == "looters") ?? Clan.BanditFactions?.FirstOrDefault();
         if (bandits == null)
         {
            ModLog.Error("[AMBUSH] no bandit clan found.");

            return null;
         }

         // A hero-capable template: a bandit-chief if it resolves, else any wanderer (built to become a hero).
         CharacterObject template = MBObjectManager.Instance?.GetObject<CharacterObject>("bandit_chief") ?? CharacterObject.All?.FirstOrDefault(c => c != null && c.Occupation == Occupation.Wanderer) ?? CharacterObject.All?.FirstOrDefault(c => c != null && c.IsHero);
         if (template == null)
         {
            ModLog.Error("[AMBUSH] no hero template resolved for the bandit chief.");

            return null;
         }

         Hero chief = HeroCreator.CreateSpecialHero(template, null, bandits, null, 35);
         if (chief == null)
         {
            ModLog.Error($"[AMBUSH] CreateSpecialHero returned null (template {template.StringId}).");

            return null;
         }

         MobileParty party = LordPartyComponent.CreateLordParty(
            "cr_ambush_captors_" + Guid.NewGuid().ToString("N"),
            chief,
            new CampaignVec2(spawnPos, true),
            1f, // let the engine place it at a VALID reachable point in this radius...
            spawnAt, // ...around a real settlement, so the position is never off-map/in-water
            chief);

         if (party == null)
         {
            ModLog.Error("[AMBUSH] CreateLordParty returned null.");
            KillCharacterAction.ApplyByRemove(chief);

            return null;
         }

         CharacterObject thug = MBObjectManager.Instance?.GetObject<CharacterObject>("looter") ?? bandits.Culture?.BasicTroop;
         if (thug != null) party.MemberRoster.AddToCounts(thug, CaptorEscort);

         // Pack horses so the band makes real headway carrying the captive to the lord.
         var sumpter = Game.Current?.ObjectManager?.GetObject<ItemObject>("sumpter_horse");
         if (sumpter != null) party.ItemRoster.AddToCounts(sumpter, party.MemberRoster.TotalManCount + 6);

         _chiefId = chief.StringId;

         return party;
      }

      #endregion
   }
}