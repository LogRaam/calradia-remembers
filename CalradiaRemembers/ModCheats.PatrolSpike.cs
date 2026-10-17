// Code written by Gabriel Mailhot, 16/10/2026.
// SPIKE, to be removed once the companion patrol is built. jaserms1 (Nexus, 16/10/2026): companions ordered in a war
// council to patrol villages did nothing, and the player inherited the task. Gabriel the same day: companions must
// carry out missions for the player, a patrol first, anywhere, with men taken from the player's party, until told to
// stop. Before any rule is written, this answers what the engine does with a player-clan party told to patrol: with
// the AI lock on (what the mod does elsewhere) a patrol is passive; with it off the party engages, but its AI may wander
// off. The log ([PATROL SPIKE], once per game hour) says which, and whether a leash (re-issuing the patrol when the
// party is idle elsewhere or too far) holds without breaking its fights. Session only: nothing is saved.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Travel;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      private static readonly object PatrolSpikeOwner = new();
      private static MobileParty _spikeParty;
      private static Hero _spikeCompanion;
      private static Settlement _spikeTarget;
      private static bool _spikeLeash;
      private static bool _spikeListening;
      private static int _spikeHours;

      /// <summary>cr.patrol_spike &lt;companion&gt; &lt;settlement&gt; [men] [leash] | stop | status: the companion-patrol engine spike.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("patrol_spike", "cr")]
      public static string PatrolSpike(List<string> args)
      {
         if (Campaign.Current == null || MobileParty.MainParty == null) return "No campaign is running.";
         if (args == null || args.Count == 0) return "Usage: cr.patrol_spike <companion> <settlement> [men] [leash] | stop | status";

         if (args[0].Equals("status", StringComparison.OrdinalIgnoreCase)) return SpikeStatus("status");
         if (args[0].Equals("stop", StringComparison.OrdinalIgnoreCase)) return StopPatrolSpike();
         if (_spikeParty is {IsActive: true}) return "A spike patrol is already out; cr.patrol_spike stop first.";
         if (args.Count < 2) return "Usage: cr.patrol_spike <companion> <settlement> [men] [leash]";

         Hero companion = MobileParty.MainParty.MemberRoster.GetTroopRoster()
                                    .Select(e => e.Character?.HeroObject)
                                    .FirstOrDefault(h => h != null && h != Hero.MainHero && h.Name.ToString().IndexOf(args[0], StringComparison.OrdinalIgnoreCase) >= 0);
         if (companion == null) return $"No hero in your party matches '{args[0]}'.";

         Settlement target = Settlement.All.FirstOrDefault(s => s != null && !s.IsHideout && s.Name.ToString().IndexOf(args[1], StringComparison.OrdinalIgnoreCase) >= 0);
         if (target == null) return $"No settlement matches '{args[1]}'.";

         int men = 20;
         if (args.Count > 2 && int.TryParse(args[2], out int n) && n > 0) men = n;
         _spikeLeash = args.Any(a => a.Equals("leash", StringComparison.OrdinalIgnoreCase));

         try
         {
            MobileParty party = MobilePartyHelper.CreateNewClanMobileParty(companion, Clan.PlayerClan);
            if (party == null) return "The party could not be created.";

            int moved = MoveHealthyTroops(MobileParty.MainParty, party, men);
            ItemObject grain = Game.Current.ObjectManager.GetObject<ItemObject>("grain");
            if (grain != null) party.ItemRoster.AddToCounts(grain, 30);

            _spikeParty = party;
            _spikeCompanion = companion;
            _spikeTarget = target;
            _spikeHours = 0;
            IssueSpikePatrol("start");

            if (!_spikeListening)
            {
               CampaignEvents.HourlyTickEvent.AddNonSerializedListener(PatrolSpikeOwner, OnPatrolSpikeHour);
               CampaignEvents.MapEventEnded.AddNonSerializedListener(PatrolSpikeOwner, OnPatrolSpikeBattle);
               _spikeListening = true;
            }

            ModLog.Info($"[PATROL SPIKE] {companion.Name} rides out to patrol {target.Name} with {moved} men (leash {_spikeLeash}).");

            return $"{companion.Name} patrols {target.Name} with {moved} men, leash {(_spikeLeash ? "on" : "off")}. Let it run 10-15 days, then cr.patrol_spike stop; the log has a [PATROL SPIKE] line per hour.";
         }
         catch (Exception ex)
         {
            return $"Spike failed: {ex.GetType().Name}: {ex.Message}";
         }
      }

      private static int MoveHealthyTroops(MobileParty from, MobileParty to, int wanted)
      {
         var moved = 0;
         foreach (TroopRosterElement e in from.MemberRoster.GetTroopRoster().Where(e => e.Character is {IsHero: false}).OrderBy(e => e.Character.Tier).ToList())
         {
            if (moved >= wanted) break;
            int healthy = e.Number - e.WoundedNumber;
            int take = Math.Min(healthy, wanted - moved);
            if (take <= 0) continue;

            from.MemberRoster.AddToCounts(e.Character, -take);
            to.MemberRoster.AddToCounts(e.Character, take);
            moved += take;
         }

         return moved;
      }

      private static void IssueSpikePatrol(string why)
      {
         _spikeParty.Ai.SetDoNotMakeNewDecisions(false);
         _spikeParty.SetMovePatrolAroundSettlement(_spikeTarget, MobileParty.NavigationType.Default, false);
         ModLog.Info($"[PATROL SPIKE] patrol issued ({why}).");
      }

      private static void OnPatrolSpikeHour()
      {
         try
         {
            if (_spikeParty == null) return;
            if (!_spikeParty.IsActive)
            {
               ModLog.Info($"[PATROL SPIKE] the party is gone after {_spikeHours} hours (companion {( _spikeCompanion?.IsPrisoner == true ? "captured" : _spikeCompanion?.IsDead == true ? "dead" : "free")}).");
               _spikeParty = null;

               return;
            }

            _spikeHours++;
            float distance = _spikeParty.GetPosition2D.Distance(_spikeTarget.GatePosition.ToVec2());
            ModLog.Info(SpikeStatus($"h{_spikeHours}") + $" dist={distance:0.0}");

            if (!_spikeLeash || _spikeParty.MapEvent != null) return;
            AiBehavior st = _spikeParty.ShortTermBehavior;
            bool busyFighting = st is AiBehavior.EngageParty or AiBehavior.FleeToPoint or AiBehavior.FleeToGate or AiBehavior.FleeToParty;
            bool offPatrol = _spikeParty.DefaultBehavior != AiBehavior.PatrolAroundPoint;
            if (!busyFighting && (offPatrol || distance > 15f)) IssueSpikePatrol(offPatrol ? $"leash: was {_spikeParty.DefaultBehavior}" : $"leash: {distance:0.0} away");
         }
         catch (Exception ex) { ModLog.Error($"[PATROL SPIKE] hourly failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static void OnPatrolSpikeBattle(MapEvent mapEvent)
      {
         try
         {
            if (_spikeParty == null || mapEvent == null) return;
            BattleSideEnum? side = mapEvent.AttackerSide?.Parties?.Any(p => p.Party?.MobileParty == _spikeParty) == true ? BattleSideEnum.Attacker
               : mapEvent.DefenderSide?.Parties?.Any(p => p.Party?.MobileParty == _spikeParty) == true ? BattleSideEnum.Defender
               : null;
            if (side == null) return;

            MapEventSide foe = mapEvent.GetMapEventSide(side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            ModLog.Info($"[PATROL SPIKE] battle: {(side == BattleSideEnum.Attacker ? "attacked" : "was attacked by")} {foe?.LeaderParty?.Name} "
                        + $"({foe?.HealthyTroopCountAtMapEventStart} men), {(mapEvent.WinningSide == side ? "won" : "lost")}; troops now {_spikeParty.MemberRoster.TotalHealthyCount}.");
         }
         catch (Exception ex) { ModLog.Error($"[PATROL SPIKE] battle read failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static string SpikeStatus(string tag)
      {
         if (_spikeParty is not {IsActive: true}) return "No spike patrol is out.";

         return $"[PATROL SPIKE] {tag}: default={_spikeParty.DefaultBehavior} short={_spikeParty.ShortTermBehavior} "
                + $"target={_spikeParty.ShortTermTargetParty?.Name} mapEvent={_spikeParty.MapEvent != null} army={_spikeParty.Army != null} "
                + $"lock={_spikeParty.Ai.DoNotMakeNewDecisions} men={_spikeParty.MemberRoster.TotalHealthyCount} food={_spikeParty.TotalFoodAtInventory} "
                + $"inSettlement={_spikeParty.CurrentSettlement?.Name}";
      }

      private static string StopPatrolSpike()
      {
         if (_spikeParty is not {IsActive: true}) return "No spike patrol is out.";

         try
         {
            MobileParty party = _spikeParty;
            int men = MoveHealthyTroops(party, MobileParty.MainParty, int.MaxValue);
            AddHeroToPartyAction.Apply(_spikeCompanion, MobileParty.MainParty, false);
            TravelingParty.Destroy(party);
            ModLog.Info($"[PATROL SPIKE] stopped after {_spikeHours} hours; {_spikeCompanion.Name} and {men} men are back.");
            _spikeParty = null;

            return $"{_spikeCompanion.Name} and {men} healthy men are back in your party (wounded stay with the disbanded party).";
         }
         catch (Exception ex)
         {
            return $"Stop failed: {ex.GetType().Name}: {ex.Message}";
         }
      }
   }
}
