// Code written by Gabriel Mailhot, 30/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Starts the town-centre brawl once the cutthroats have spawned, and arms capture if the player loses.</summary>
   public sealed class AmbushFightMissionLogic : MissionLogic
   {
      private const float CombatantNavRadius = 0.4f; // person-sized clearance for the path-reachability check

      // The gate guards (and any stray civilian) caught in the ambush scene: they scatter when the knives come
      // out rather than wade into a pitched fight. Made invulnerable so a stray blow can't kill them (a slain
      // townsguard would be a crime / could end the mission oddly), sent running from the player, then faded out.
      private const float FleeFadeSeconds = 2.5f;
      private const int MaxWaitTicks = 120; // ~2s grace for the cutthroats to finish spawning before we start anyway
      private const int RingAngleJitterTries = 6; // angles to try around each cutthroat's ideal slot before giving up

      // Placement: the cutthroats emerge ~20m off (a street ambush across the square, not a huddle in your face),
      // each on the navmesh and reachable. We try a fan of angles and fall to nearer rings when 20m is walled off.
      private static readonly float[] RingRadiusTries = {20f, 16f, 24f, 13f};
      private readonly int _expectedCount;
      private readonly List<Agent> _fleeing = new();

      private readonly string _paymasterId;
      private float _fleeElapsed;
      private bool _fleeStarted;
      private bool _resolved; // capture armed (or the player won) — guards against double-firing
      private bool _started;
      private int _waitTicks;

      public AmbushFightMissionLogic(string paymasterId, int expectedCount)
      {
         _paymasterId = paymasterId;
         _expectedCount = expectedCount;
      }

      /// <summary>
      ///   The player going down ends the mission on a vanilla "knocked unconscious" flow that returns to the town
      ///   menu BEFORE the fight handler reports a loss — so OnFightEnd never fires and the capture is never armed.
      ///   Catch the player's removal during the brawl and arm the capture here, so a beaten player is still taken.
      /// </summary>
      public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
      {
         if (!_started || _resolved) return;
         if (affectedAgent == null || affectedAgent != Mission?.MainAgent) return;

         ArmCapture("player went down");
      }

      public override void OnMissionTick(float dt)
      {
         TickBystanderFlee(dt); // fade fleeing gate guards once they've had a moment to scatter (runs post-start too)

         if (_started) return;
         if (Mission?.MainAgent == null) return;

         _waitTicks++;
         List<Agent> ambushers = TownAmbushBehavior.AmbusherAgents.Where(a => a != null && a.IsActive()).ToList();

         bool allSpawned = ambushers.Count >= _expectedCount && _expectedCount > 0;
         bool graceElapsed = _waitTicks > MaxWaitTicks;

         // Keep waiting until they have all spawned, or the short grace elapses.
         if (!allSpawned && !graceElapsed) return;

         // Grace elapsed with nothing to fight: log it loudly (so the spawn-injection path can be diagnosed from
         // the lines above) and stop polling, instead of looping silently as before — the old "no aggressors" case.
         if (ambushers.Count == 0)
         {
            _started = true;
            ModLog.Error($"[AMBUSH] no cutthroats present after {_waitTicks} ticks (expected {_expectedCount}) — the " +
                         "ambush fizzles. Check the '[AMBUSH] spawn event' / 'injected ... cutthroat(s)' lines above.");

            return;
         }

         ModLog.Info($"[AMBUSH] brawl trigger: {ambushers.Count}/{_expectedCount} cutthroat(s) active after {_waitTicks} ticks.");
         StartBrawl(ambushers);
      }

      #region private

      /// <summary>Marks one bystander invulnerable and sends them running to a navmesh point away from the fight.</summary>
      private static void FleeOne(Agent agent, Scene scene, Vec2 centre)
      {
         try
         {
            agent.SetMortalityState(Agent.MortalityState.Invulnerable); // a fleeing guard must not be cut down

            Vec2 here = agent.Position.AsVec2;
            Vec2 away = here - centre;
            if (away.Length < 0.01f) away = new Vec2(1f, 0f);
            away.Normalize();

            WorldPosition dest = agent.GetWorldPosition();
            dest.SetVec2(here + away * 18f); // slide the away-point onto the navmesh
            if (scene != null && dest.GetNavMesh() != UIntPtr.Zero)
               agent.SetScriptedPosition(ref dest, false);

            agent.SetWatchState(Agent.WatchState.Alarmed); // panic posture — run, don't fight
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] flee one failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Rings the cutthroats ~20m around the player on the navmesh: each is placed at the first navmesh point
      ///   that is also REACHABLE (a real path back to the player), searched over a fan of angles and falling
      ///   radii. A cutthroat for whom no reachable ring point is found keeps its own spawn spot (fail-soft).
      /// </summary>
      private static void PositionAround(Agent player, List<Agent> ambushers)
      {
         Scene scene = Mission.Current?.Scene;

         if (scene == null || ambushers == null) return;

         WorldPosition centre = player.GetWorldPosition();
         int n = Math.Max(1, ambushers.Count);

         for (var i = 0; i < ambushers.Count; i++)
         {
            double baseAngle = 2.0 * Math.PI * i / n;

            if (!TryFindRingPoint(scene, centre, baseAngle, out Vec3 dest)) continue;

            try { ambushers[i].TeleportToPosition(dest); }
            catch
            {
               /* off-mesh teleport is fail-soft; they fight from where they are */
            }
         }
      }

      /// <summary>Finds a navmesh point ~20m from the centre, reachable by a real path, near the given angle.</summary>
      private static bool TryFindRingPoint(Scene scene, WorldPosition centre, double baseAngle, out Vec3 dest)
      {
         dest = default;
         Vec2 origin = centre.AsVec2;

         for (var j = 0; j < RingAngleJitterTries; j++)
         {
            // Spiral out from the ideal slot: 0, +22.5°, -22.5°, +45°, -45°, …
            double angle = baseAngle +
                           (j + 1.0f) /
                           2 *
                           (Math.PI / 8.0f) *
                           (j % 2 == 0
                              ? 1
                              : -1);

            foreach (float r in RingRadiusTries)
            {
               var target = new Vec2(origin.x + r * (float) Math.Cos(angle), origin.y + r * (float) Math.Sin(angle));

               WorldPosition candidate = centre;
               candidate.SetVec2(target);

               if (candidate.GetNavMesh() == UIntPtr.Zero) continue; // off the navmesh (a wall / building) — skip

               WorldPosition from = centre;

               if (!scene.GetPathDistanceBetweenPositions(ref candidate, ref from, CombatantNavRadius, out float pathDist)) continue;
               if (pathDist > r * 2f) continue; // on the navmesh but reachable only by a long detour — skip

               dest = candidate.GetGroundVec3();

               return true;
            }
         }

         return false;
      }

      /// <summary>Arms the post-mission capture by the paymaster, exactly once per ambush (idempotent).</summary>
      private void ArmCapture(string reason)
      {
         if (_resolved) return;
         _resolved = true;

         Hero paymaster = Hero.FindFirst(h => h != null && h.StringId == _paymasterId);
         if (paymaster != null)
         {
            AmbushCaptivityBehavior.Instance?.Capture(paymaster);
            ModLog.Info($"[AMBUSH] capture armed ({reason}) — paymaster {paymaster.Name}.");
         }
         else
         {
            ModLog.Error($"[AMBUSH] cannot arm capture ({reason}): paymaster id '{_paymasterId}' not found.");
         }
      }

      private void OnFightEnd(bool isPlayerSideWon)
      {
         ModLog.Info($"[AMBUSH] street brawl ended — player {(isPlayerSideWon ? "won" : "LOST")}.");
         if (isPlayerSideWon)
         {
            _resolved = true;

            return;
         }

         ArmCapture("brawl lost");
      }

      /// <summary>
      ///   The knives are out: any human agent in the scene who is NOT a combatant (the gate guard or two, a
      ///   stray civilian) scatters rather than joining the brawl. Each is made invulnerable, sent running away
      ///   from the player on the navmesh, and faded out a couple of seconds later by <see cref="TickBystanderFlee" />.
      /// </summary>
      private void SendBystandersFleeing(List<Agent> playerSide, List<Agent> ambushers)
      {
         try
         {
            Mission m = Mission.Current;

            if (m?.MainAgent == null) return;

            var combatants = new HashSet<Agent>();
            foreach (Agent a in playerSide)
               if (a != null)
                  combatants.Add(a);
            foreach (Agent a in ambushers)
               if (a != null)
                  combatants.Add(a);

            Scene scene = m.Scene;
            Vec2 centre = m.MainAgent.Position.AsVec2;

            foreach (Agent agent in m.Agents.ToList())
            {
               if (agent == null || !agent.IsActive() || !agent.IsHuman || combatants.Contains(agent)) continue;

               FleeOne(agent, scene, centre);
               _fleeing.Add(agent);
            }

            _fleeStarted = true;
            if (_fleeing.Count > 0)
               ModLog.Info($"[AMBUSH] {_fleeing.Count} bystander(s) at the gate flee the ambush.");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] bystander flee failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private void StartBrawl(List<Agent> ambushers)
      {
         _started = true;

         var fight = Mission.GetMissionBehavior<MissionFightHandler>();
         if (fight == null)
         {
            ModLog.Error("[AMBUSH] no MissionFightHandler in the town-centre mission — cannot start the brawl.");

            return;
         }

         // The ambush plays out at the town gate, where the player spawns: snapping them to the navmesh "centre"
         // dragged them outside the walls (the ground-level centroid lands beyond the gatehouse), so we keep the
         // gate spot and ring the cutthroats ~20m off on the navmesh — figures closing in, not a huddle on top
         // of the player.
         PositionAround(Mission.MainAgent, ambushers);

         var playerSide = new List<Agent> {Mission.MainAgent};
         playerSide.AddRange(TownAmbushBehavior.AllyAgents.Where(a => a != null && a.IsActive()));

         fight.StartCustomFight(playerSide, ambushers, false, false, OnFightEnd);
         ModLog.Info($"[AMBUSH] street brawl started — {ambushers.Count} attacker(s) vs the player and " +
                     $"{playerSide.Count - 1} companion(s).");

         SendBystandersFleeing(playerSide, ambushers);
      }

      /// <summary>Fades the fleeing bystanders out once they've had ~2.5s to clear the scene. One-shot.</summary>
      private void TickBystanderFlee(float dt)
      {
         if (!_fleeStarted || _fleeing.Count == 0) return;

         _fleeElapsed += dt;

         if (_fleeElapsed < FleeFadeSeconds) return;

         foreach (Agent a in _fleeing)
            try
            {
               if (a != null && a.IsActive()) a.FadeOut(false, false);
            }
            catch (Exception ex) { ModLog.Error($"[AMBUSH] fade fleeing guard failed: {ex.GetType().Name}: {ex.Message}"); }

         _fleeing.Clear();
      }

      #endregion
   }
}