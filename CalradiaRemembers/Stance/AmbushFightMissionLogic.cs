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
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Starts the town-centre brawl once the cutthroats have spawned, and arms capture if the player loses.</summary>
   public sealed class AmbushFightMissionLogic : MissionLogic
   {
      private const int MaxWaitTicks = 120; // ~2s grace for the cutthroats to finish spawning before we start anyway

      // Placement: the cutthroats emerge ~20m off (a street ambush across the square, not a huddle in your face),
      // each on the navmesh and reachable. We try a fan of angles and fall to nearer rings when 20m is walled off.
      private static readonly float[] RingRadiusTries = {20f, 16f, 24f, 13f};
      private const int RingAngleJitterTries = 6;   // angles to try around each cutthroat's ideal slot before giving up
      private const float CombatantNavRadius = 0.4f; // person-sized clearance for the path-reachability check
      private const float GroundBandZ = 3.5f;        // keep "town centre" to faces within this height of the (ground) gate spawn

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

      private void StartBrawl(List<Agent> ambushers)
      {
         _started = true;

         MissionFightHandler fight = Mission.GetMissionBehavior<MissionFightHandler>();
         if (fight == null)
         {
            ModLog.Error("[AMBUSH] no MissionFightHandler in the town-centre mission — cannot start the brawl.");
            return;
         }

         // Move the player into the heart of the town (they spawn at the gate), then ring the cutthroats ~20m off
         // on the navmesh, so the ambush reads as figures closing in from across the square, not a huddle on top
         // of the player.
         MoveToTownCentre(Mission.MainAgent);
         PositionAround(Mission.MainAgent, ambushers);

         var playerSide = new List<Agent> {Mission.MainAgent};
         fight.StartCustomFight(playerSide, ambushers, dropWeapons: false, isItemUseDisabled: false, OnFightEnd);
         ModLog.Info($"[AMBUSH] street brawl started — {ambushers.Count} cutthroat(s) vs the player.");
      }

      /// <summary>
      ///   Teleports the player from the gate (where the town-centre mission spawns them) into the heart of the
      ///   town: the centroid of the GROUND-LEVEL walkable area, snapped to the nearest actual face centre so it
      ///   is guaranteed on the navmesh and lands in the streets/square. Only faces within a small height band of
      ///   the (ground-level) gate spawn are counted, so the walls/ramparts — walkable navmesh that otherwise
      ///   dragged the average up onto the battlements — are excluded. Fail-soft: no navmesh, the player stays put.
      /// </summary>
      private static void MoveToTownCentre(Agent player)
      {
         Scene scene = Mission.Current?.Scene;
         if (player == null || scene == null) return;

         int faceCount = scene.GetNavMeshFaceCount();
         if (faceCount <= 0) { ModLog.Info("[AMBUSH] no navmesh faces — the player stays at the gate."); return; }

         float groundZ = player.Position.z; // the player spawns at the gate, on the ground

         var centres = new List<Vec3>(faceCount);
         Vec3 sum = Vec3.Zero;
         for (var f = 0; f < faceCount; f++)
         {
            Vec3 c = Vec3.Zero;
            scene.GetNavMeshCenterPosition(f, ref c);
            if (!c.IsValid) continue;
            if (Math.Abs(c.z - groundZ) > GroundBandZ) continue; // skip ramparts / elevated walkways
            centres.Add(c);
            sum += c;
         }

         if (centres.Count == 0) { ModLog.Info("[AMBUSH] no ground-level navmesh centres — the player stays at the gate."); return; }

         Vec3 average = sum * (1f / centres.Count);

         // Snap the (possibly between-faces) average to the nearest real face centre.
         Vec3 best = centres[0];
         float bestDistSq = float.MaxValue;
         foreach (Vec3 c in centres)
         {
            float d = c.AsVec2.DistanceSquared(average.AsVec2);
            if (d < bestDistSq) { bestDistSq = d; best = c; }
         }

         try
         {
            float moved = player.Position.AsVec2.Distance(best.AsVec2);
            player.TeleportToPosition(best);
            ModLog.Info($"[AMBUSH] player moved to the town centre ({moved:0.0}m, from {centres.Count} ground-level navmesh faces).");
         }
         catch (Exception ex) { ModLog.Error($"[AMBUSH] centre teleport failed: {ex.GetType().Name}: {ex.Message}"); }
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
            catch { /* off-mesh teleport is fail-soft; they fight from where they are */ }
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
            double angle = baseAngle + (j + 1) / 2 * (Math.PI / 8.0) * (j % 2 == 0 ? 1 : -1);

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
