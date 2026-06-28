// Code written by Gabriel Mailhot, 28/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Arms the kidnap capture when the player is defeated in the town-ambush alley fight.</summary>
   public sealed class AmbushFightMissionLogic : MissionLogic
   {
      private const float SpawnRingRadius = 30.0f; // how close the cutthroats end up around the player (tunable)

      private readonly string _paymasterId;
      private bool _arranged; // the one-shot spawn diagnostic + reposition has run
      private bool _resolved;

      public AmbushFightMissionLogic(string paymasterId)
      {
         _paymasterId = paymasterId;
      }

      public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
      {
         // The player going down in a street ambush IS the defeat — alley fights end there.
         if (_resolved || affectedAgent == null || affectedAgent != Mission.MainAgent) return;

         _resolved = true;

         Hero paymaster = Hero.FindFirst(h => h != null && h.StringId == _paymasterId);
         if (paymaster != null) AmbushCaptivityBehavior.Instance?.Capture(paymaster);

         ModLog.Info("[AMBUSH] player defeated in the alley fight — capture armed.");
      }

      public override void OnMissionTick(float dt)
      {
         if (_arranged) return;

         Agent player = Mission?.MainAgent;

         if (player == null) return;

         List<Agent> attackers = Mission.Agents
                                        .Where(a => a is {IsHuman: true} && a != player && a.IsEnemyOf(player))
                                        .ToList();

         if (attackers.Count == 0) return; // wait until the rival side has spawned

         _arranged = true;
         ArrangeAttackersAroundPlayer(player, attackers);
      }

      #region private

      /// <summary>
      ///   Logs where the engine put the attackers relative to the player (confirming Gabriel's "spawned beyond
      ///   the walls" hypothesis when the distances are large) and teleports them into a ring around the player so
      ///   the fight reads as a street ambush, not a siege.
      /// </summary>
      private static void ArrangeAttackersAroundPlayer(Agent player, List<Agent> attackers)
      {
         try
         {
            Vec3 c = player.Position;
            ModLog.Info($"[AMBUSH] spawn check — player at ({c.x:0.0},{c.y:0.0},{c.z:0.0}); {attackers.Count} attacker(s).");

            for (var i = 0; i < attackers.Count; i++)
            {
               Agent a = attackers[i];
               float before = a.Position.Distance(c);

               var angle = (float) (2.0 * Math.PI * i / attackers.Count);
               var target = new Vec3(
                  c.x + SpawnRingRadius * (float) Math.Cos(angle),
                  c.y + SpawnRingRadius * (float) Math.Sin(angle),
                  c.z);

               // CanTeleport() is false for active AI agents, but TeleportToPosition itself still moves them.
               try { a.TeleportToPosition(target); }
               catch (Exception ex) { ModLog.Error($"[AMBUSH]   attacker {i}: teleport threw {ex.GetType().Name}: {ex.Message}"); }

               float after = a.Position.Distance(c);
               ModLog.Info($"[AMBUSH]   attacker {i}: was {before:0.0}m away → now {after:0.0}m away (target ({target.x:0.0},{target.y:0.0})).");
            }
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] arrange attackers failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      #endregion
   }
}