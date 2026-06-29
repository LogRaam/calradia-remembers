// Code written by Gabriel Mailhot, 29/06/2026.
// "Stance with teeth", increment 3.2a — the brawl driver for the town-centre street ambush. The cutthroats are
// spawned as hostile LocationCharacters into the real town-centre scene (TownAmbushBehavior). A plain town centre
// is a CIVILIAN mission (no combat), so this logic waits for the cutthroats to finish spawning, then starts a real
// fight through the vanilla MissionFightHandler.StartCustomFight (player side vs the cutthroats) — which switches
// the scene to combat, draws weapons, sets the hostile teams, and reports who won. On a player loss it arms the
// kidnap so the convoy takes them once the scene ends.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Starts the town-centre brawl once the cutthroats have spawned, and arms capture if the player loses.</summary>
   public sealed class AmbushFightMissionLogic : MissionLogic
   {
      private const int MaxWaitTicks = 120; // ~2s grace for the cutthroats to finish spawning before we start anyway

      private readonly string _paymasterId;
      private readonly int _expectedCount;
      private bool _started;
      private int _waitTicks;

      public AmbushFightMissionLogic(string paymasterId, int expectedCount)
      {
         _paymasterId = paymasterId;
         _expectedCount = expectedCount;
      }

      public override void OnMissionTick(float dt)
      {
         if (_started) return;
         if (Mission?.MainAgent == null) return;

         List<Agent> ambushers = TownAmbushBehavior.AmbusherAgents.Where(a => a != null && a.IsActive()).ToList();
         _waitTicks++;

         // Start once they have all spawned, or after a short grace with at least one present.
         bool ready = ambushers.Count >= _expectedCount && _expectedCount > 0
                   || (_waitTicks > MaxWaitTicks && ambushers.Count > 0);
         if (!ready) return;

         StartBrawl(ambushers);
      }

      #region private

      private void StartBrawl(List<Agent> ambushers)
      {
         _started = true;

         MissionFightHandler fight = Mission.GetMissionBehavior<MissionFightHandler>();
         if (fight == null)
         {
            ModLog.Error("[AMBUSH] no MissionFightHandler in the town-centre mission — cannot start the brawl.");
            return;
         }

         // The cutthroats spawn scattered across the town centre (npc_common points), so close the gap before
         // the fight: ring them around the player so the ambush is on them at once, not a hunt through the streets.
         PositionAround(Mission.MainAgent, ambushers);

         var playerSide = new List<Agent> {Mission.MainAgent};
         fight.StartCustomFight(playerSide, ambushers, dropWeapons: false, isItemUseDisabled: false, OnFightEnd);
         ModLog.Info($"[AMBUSH] street brawl started — {ambushers.Count} cutthroat(s) vs the player.");
      }

      /// <summary>Rings the cutthroats tightly around the player (the War Sails ambush teleports its agents too).</summary>
      private static void PositionAround(Agent player, List<Agent> ambushers)
      {
         const float radius = 4.5f;
         Vec3 c = player.Position;
         for (var i = 0; i < ambushers.Count; i++)
         {
            var angle = (float) (2.0 * Math.PI * i / Math.Max(1, ambushers.Count));
            var pos = new Vec3(c.x + radius * (float) Math.Cos(angle), c.y + radius * (float) Math.Sin(angle), c.z);
            try { ambushers[i].TeleportToPosition(pos); }
            catch { /* off-mesh teleport is fail-soft; they still fight from where they are */ }
         }
      }

      private void OnFightEnd(bool isPlayerSideWon)
      {
         ModLog.Info($"[AMBUSH] street brawl ended — player {(isPlayerSideWon ? "won" : "LOST")}.");
         if (isPlayerSideWon) return;

         Hero paymaster = Hero.FindFirst(h => h != null && h.StringId == _paymasterId);
         if (paymaster != null) AmbushCaptivityBehavior.Instance?.Capture(paymaster);
      }

      #endregion
   }
}
