// Code written by Gabriel Mailhot, 15/07/2026.
// Duels pillar, Inc 1: the REAL duel mission (the spike proved the wilderness launch; this is the actual fight).
// A consented, NON-LETHAL single combat between the player and one named hero, before whatever witnesses are in
// the scene. Non-lethality is the load-bearing safety rule (a chat must never be able to delete a named hero):
// this logic is the mission's IAgentStateDecider and answers Unconscious for every downed agent, so a killing
// blow knocks out instead of killing. There is deliberately NO casualty/combatant logic wired, so a knockout
// never touches Hero.IsAlive on the campaign side. The loser is simply the first to fall; the winner is reported
// to the caller, which is where relation / renown / stakes are applied (by policy, not here).

#region

using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>
   ///   Stages and adjudicates a non-lethal 1v1 duel: spawns the two fighters a clear distance apart so the
   ///   clash is seen coming, makes every downed agent fall unconscious rather than die, detects who fell, and
   ///   returns to the map reporting the winner. The real duel mechanics (stakes, world-event) live in the
   ///   caller's <see cref="_onDuelEnd" /> callback; this class is only the mission glue.
   /// </summary>
   public sealed class DuelMissionLogic : MissionLogic, IAgentStateDecider
   {
      /// <summary>Metres between the two fighters at the start: far enough that the duel is seen coming, close enough to close quickly.</summary>
      private const float DuelSeparation = 15f;

      /// <summary>Seconds to linger after a fall before returning to the map, so the knockout is seen.</summary>
      private const float EndLingerSeconds = 4f;

      private readonly Action<bool> _onDuelEnd;
      private readonly Hero _opponent;

      private bool _ended;
      private float _endElapsed;
      private Agent _opponentAgent;
      private Agent _playerAgent;
      private bool _playerWon;

      public DuelMissionLogic(Hero opponent, Action<bool> onDuelEnd)
      {
         _opponent = opponent;
         _onDuelEnd = onDuelEnd;
      }

      /// <summary>
      ///   The mission asks its <see cref="IAgentStateDecider" /> what state a downed agent enters; answering
      ///   Unconscious for everyone is what makes the duel non-lethal (the arena duel's own mechanism).
      /// </summary>
      public AgentState GetAgentState(Agent affectedAgent, float deathProbability, out bool usedSurgery)
      {
         usedSurgery = false;

         return AgentState.Unconscious;
      }

      public override void AfterStart()
      {
         try
         {
            SpawnFighters();

            // Battle mode is what frees walking and weapon switching once the agent stands on the navmesh
            // (proven by the spike: without it the player could only look and swing).
            Mission.SetMissionMode(MissionMode.Battle, false);
            ModLog.Info("[DUEL] fighters spawned, mission in Battle mode. The duel is on.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUEL] AfterStart failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   The moment a fighter falls (unconscious, per <see cref="GetAgentState" />), the duel is decided: the
      ///   winner is whoever is still standing. We latch the result and let a short timer close the mission so
      ///   the knockout is seen before the return to the map.
      /// </summary>
      public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
      {
         if (_ended) return;
         if (affectedAgent != _playerAgent && affectedAgent != _opponentAgent) return;

         _playerWon = affectedAgent == _opponentAgent;
         _ended = true;
         _endElapsed = 0f;
         ModLog.Info($"[DUEL] a fighter fell. playerWon={_playerWon}. Closing shortly.");
      }

      public override void OnMissionTick(float dt)
      {
         if (!_ended) return;

         _endElapsed += dt;
         if (_endElapsed < EndLingerSeconds) return;

         // Fire the outcome callback exactly once, then close. The callback (stakes / relation / world-event)
         // is the caller's; guarded here so a thrown callback can never strand the player in the scene.
         if (_onDuelEnd != null)
         {
            try { _onDuelEnd(_playerWon); }
            catch (Exception ex) { ModLog.Error($"[DUEL] onDuelEnd callback threw: {ex.GetType().Name}: {ex.Message}"); }
         }

         _ended = false; // prevent a second callback if EndMission takes a frame
         try { Mission.EndMission(); }
         catch (Exception ex) { ModLog.Error($"[DUEL] EndMission threw: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>A duel is not a fight you can walk away from: refuse the leave request until someone has fallen.</summary>
      public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
      {
         canPlayerLeave = _ended;

         return null;
      }

      #region private

      /// <summary>
      ///   A point on the scene's navigable ground, near the centre of its bounding box (the same method the
      ///   spike validated; (0,0,0) is the off-navmesh corner).
      /// </summary>
      private Vec3 NavigableCentre()
      {
         Scene scene = Mission.Scene;
         scene.GetBoundingBox(out Vec3 min, out Vec3 max);
         var midXy = new Vec3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, max.z + 10f);

         return new Vec3(midXy.x, midXy.y, scene.GetGroundHeightAtPosition(midXy));
      }

      private void SpawnFighters()
      {
         Team playerTeam = Mission.Teams.Add(BattleSideEnum.Defender, uint.MaxValue, uint.MaxValue, null, true, false, true);
         Team enemyTeam = Mission.Teams.Add(BattleSideEnum.Attacker, uint.MaxValue, uint.MaxValue, null, false, false, true);
         Mission.PlayerTeam = playerTeam;
         playerTeam.SetIsEnemyOf(enemyTeam, true);

         // Place the two a clear distance apart along one axis, each facing the other, so the duel is seen
         // coming and they close the gap on their own (the opponent charges once Alarmed).
         Vec3 centre = NavigableCentre();
         var half = new Vec3(0f, DuelSeparation * 0.5f, 0f);
         Vec3 playerPos = GroundAt(centre - half);
         Vec3 foePos = GroundAt(centre + half);

         _playerAgent = SpawnOne(CharacterObject.PlayerCharacter, playerTeam, playerPos, new Vec2(0f, 1f));
         if (_playerAgent != null) _playerAgent.Controller = AgentControllerType.Player;

         CharacterObject foe = _opponent?.CharacterObject ?? CharacterObject.PlayerCharacter;
         _opponentAgent = SpawnOne(foe, enemyTeam, foePos, new Vec2(0f, -1f));
         if (_opponentAgent != null)
         {
            // Alarmed + the mission's AgentHumanAILogic is the arena duel's own 1v1 engagement pattern: the AI
            // advances and fights on the navmesh, no formation or MapEvent needed.
            _opponentAgent.SetWatchState(Agent.WatchState.Alarmed);
            _opponentAgent.Defensiveness = 1f;
         }
      }

      /// <summary>Snaps an x/y point down onto the scene's ground.</summary>
      private Vec3 GroundAt(Vec3 point)
      {
         float z = Mission.Scene.GetGroundHeightAtPosition(new Vec3(point.x, point.y, point.z + 10f));

         return new Vec3(point.x, point.y, z);
      }

      /// <summary>Builds and spawns one dismounted fighter in battle gear, or logs and returns null on failure.</summary>
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
            agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp);

            return agent;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUEL] spawning {character?.StringId ?? "(null)"} failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #endregion
   }
}
