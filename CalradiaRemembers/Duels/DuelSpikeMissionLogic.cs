// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 1 WILDERNESS SPIKE, the in-mission half. Once the field scene is live, this spawns the
// player and one opponent on opposing teams and then closes the scene on a timer so the spike always returns
// to the map on its own (the whole point is to measure the RETURN, so it must never strand the player in an
// empty field). Both fighters are made invulnerable: this proves agents spawn, are controllable, and animate
// in a field scene without dragging in death handling and campaign casualties, which belong to the real
// non-lethal duel of Inc 1, not to a plumbing spike. Everything is wrapped: a spawn failure is logged and the
// timer still closes the scene, so the open/close/VRAM question is answered no matter what.

#region

using System;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Spawns the two fighters into the spike field scene, then ends the mission on a timer.</summary>
   public sealed class DuelSpikeMissionLogic : MissionLogic
   {
      /// <summary>How long the field scene stays open before it closes itself. Long enough to look around and swing, short enough not to strand anyone.</summary>
      public const float AutoCloseSeconds = 30f;

      private readonly Hero _opponent;
      private bool _ended;
      private float _elapsed;
      private bool _measuredOnLoad;

      public DuelSpikeMissionLogic(Hero opponent)
      {
         _opponent = opponent;
      }

      public override void AfterStart()
      {
         try
         {
            ModLog.Info("[DUELSPIKE] mission AfterStart: the field scene is live. Spawning the two fighters.");
            SpawnFighters();

            // Movement and weapon-switching are gated by the mission MODE, not just the navmesh: a scene opened
            // with no explicit mode leaves the player able to look and swing but not walk. Battle mode is the
            // one a real fight runs in. If this frees movement on the (confirmed) navmesh, custom field placement
            // is viable; if it does not, the field needs the combat mission behaviours (MissionAgentHandler,
            // AgentHumanAILogic, ...) a bare OpenNew does not install, and the wilderness duel goes through the
            // battle-mission path instead.
            try
            {
               Mission.SetMissionMode(MissionMode.Battle, false);
               ModLog.Info("[DUELSPIKE] mission mode set to Battle. Try to WALK and switch weapons now.");
            }
            catch (Exception ex) { ModLog.Error($"[DUELSPIKE] SetMissionMode(Battle) threw: {ex.GetType().Name}: {ex.Message}"); }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUELSPIKE] spawning fighters failed (the scene is still open, the timer will still close it): " +
                         $"{ex.GetType().Name}: {ex.Message}");
         }
      }

      public override void OnMissionTick(float dt)
      {
         // Sample VRAM once, a beat after the scene has fully loaded, so the "loaded" figure is the honest peak.
         if (!_measuredOnLoad && _elapsed > 1.0f)
         {
            _measuredOnLoad = true;
            ModLog.Info($"[DUELSPIKE] field scene fully loaded. VRAM in-scene | {VramProbe.Describe()} " +
                        $"(MainAgent={(Mission.MainAgent != null ? "present" : "MISSING")}, agents={Mission.Agents?.Count ?? 0}).");
         }

         _elapsed += dt;

         if (_ended || _elapsed < AutoCloseSeconds) return;

         _ended = true;
         ModLog.Info($"[DUELSPIKE] auto-close timer elapsed ({AutoCloseSeconds:0}s). Ending the mission to test the return to map.");

         try { Mission.EndMission(); }
         catch (Exception ex) { ModLog.Error($"[DUELSPIKE] EndMission threw: {ex.GetType().Name}: {ex.Message}"); }
      }

      #region private

      /// <summary>Spawns the player (player-controlled) and the opponent (AI) on opposing, mutually hostile teams, both invulnerable, then starts the fight.</summary>
      private void SpawnFighters()
      {
         Team playerTeam = Mission.Teams.Add(BattleSideEnum.Defender, uint.MaxValue, uint.MaxValue, null, true, false, true);
         Team enemyTeam = Mission.Teams.Add(BattleSideEnum.Attacker, uint.MaxValue, uint.MaxValue, null, false, false, true);
         Mission.PlayerTeam = playerTeam;
         playerTeam.SetIsEnemyOf(enemyTeam, true);

         // The first run spawned at (0,0,0), which is the scene CORNER (off the navmesh), so the player was
         // pinned and could only turn in place. Place the fighters on the scene's navigable ground instead,
         // and report whether that point is actually on a navmesh: that is the binary answer to whether custom
         // placement (like the town ambush, but in a field) is viable, or whether the wilderness duel needs the
         // full battle-spawn system.
         Vec3 centre = FindNavigableCentre();

         Agent player = SpawnOne(CharacterObject.PlayerCharacter, playerTeam, centre, new Vec2(0f, 1f));
         if (player != null)
         {
            player.Controller = AgentControllerType.Player;
            ModLog.Info($"[DUELSPIKE] player agent spawned at {centre} (MainAgent now {(Mission.MainAgent != null ? "set" : "still null")}). " +
                        "Try to WALK: if you can move here, custom field placement works.");
         }

         CharacterObject foe = _opponent?.CharacterObject ?? CharacterObject.PlayerCharacter;
         Vec3 foePos = centre + new Vec3(4f, 0f, 0f);
         Agent opponent = SpawnOne(foe, enemyTeam, foePos, new Vec2(0f, -1f));
         if (opponent != null)
         {
            // The one thing that makes a field-spawned opponent actually engage, straight from the arena duel's
            // 1v1 setup: put the AI agent on alert. Combined with the mission's AgentHumanAILogic, it charges and
            // fights on the navmesh, no formation or MapEvent required. (Defensiveness 1 keeps it from turtling.)
            opponent.SetWatchState(Agent.WatchState.Alarmed);
            opponent.Defensiveness = 1f;
            ModLog.Info($"[DUELSPIKE] opponent agent ({foe.StringId}) spawned at {foePos}, set Alarmed. It should now advance and fight.");
         }
      }

      /// <summary>
      ///   A point on the scene's navigable ground, near the centre of its bounding box. Logs whether the point
      ///   resolved to a navmesh face, which is the datum that decides the wilderness approach.
      /// </summary>
      private Vec3 FindNavigableCentre()
      {
         try
         {
            Scene scene = Mission.Scene;
            scene.GetBoundingBox(out Vec3 min, out Vec3 max);
            var midXy = new Vec3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z + 10f);
            float groundZ = scene.GetGroundHeightAtPosition(midXy);
            var ground = new Vec3(midXy.x, midXy.y, groundZ);

            UIntPtr navFace = scene.GetNavigationMeshForPosition(in ground);
            ModLog.Info($"[DUELSPIKE] scene bounds min={min} max={max}; navigable centre candidate={ground}; " +
                        $"navmesh at that point = {(navFace != UIntPtr.Zero ? "PRESENT (custom placement viable)" : "ABSENT (needs the battle-spawn system)")}.");

            return ground;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUELSPIKE] finding a navigable centre failed, falling back to origin: {ex.GetType().Name}: {ex.Message}");

            return new Vec3(0f, 0f, 0f);
         }
      }

      /// <summary>Builds and spawns one invulnerable, dismounted fighter, or logs and returns null on failure.</summary>
      private Agent SpawnOne(CharacterObject character, Team team, Vec3 position, Vec2 direction)
      {
         try
         {
            var build = new AgentBuildData(character)
               .Team(team)
               .InitialPosition(position)
               .InitialDirection(direction)
               .CivilianEquipment(false)
               .NoHorses(true);

            Agent agent = Mission.SpawnAgent(build, false);
            agent.SetMortalityState(Agent.MortalityState.Invulnerable); // a plumbing spike, not a real fight: no one dies
            agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp);

            return agent;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUELSPIKE] spawning {character?.StringId ?? "(null)"} failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #endregion
   }
}
