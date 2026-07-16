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
using System.Collections.Generic;
using CalradiaRemembers.Diagnostics;
using SandBox;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
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

         Vec3 centre = Mission.GetRandomPositionAroundPoint(new Vec3(0f, 0f, 0f), 6f, 0f, true);

         Agent player = SpawnOne(CharacterObject.PlayerCharacter, playerTeam, centre, new Vec2(0f, 1f));
         if (player != null)
         {
            player.Controller = AgentControllerType.Player;
            ModLog.Info($"[DUELSPIKE] player agent spawned at {centre} (MainAgent now {(Mission.MainAgent != null ? "set" : "still null")}).");
         }

         CharacterObject foe = _opponent?.CharacterObject ?? CharacterObject.PlayerCharacter;
         Vec3 foePos = centre + new Vec3(4f, 0f, 0f);
         Agent opponent = SpawnOne(foe, enemyTeam, foePos, new Vec2(0f, -1f));
         if (opponent != null) ModLog.Info($"[DUELSPIKE] opponent agent ({foe.StringId}) spawned at {foePos}.");

         StartTheFight(player, opponent);
      }

      /// <summary>
      ///   Puts the mission in Battle mode and starts CR's own custom fight (the same MissionFightHandler.
      ///   StartCustomFight the town ambush uses) between the two agents. Every step is guarded on its own: if
      ///   the combat never engages (a field-spawned agent may lack the settlement navigator the fight behaviour
      ///   leans on), the opponent simply stands there and the scene/VRAM/return measurement, the real point of
      ///   the spike, is untouched. The fighters are invulnerable, so the fight never resolves on its own; the
      ///   timer is what closes the scene.
      /// </summary>
      private void StartTheFight(Agent player, Agent opponent)
      {
         if (player == null || opponent == null)
         {
            ModLog.Info("[DUELSPIKE] a fighter is missing, so no fight is started (the scene still opens and closes for the VRAM test).");

            return;
         }

         try { Mission.SetMissionMode(MissionMode.Battle, false); }
         catch (Exception ex) { ModLog.Error($"[DUELSPIKE] SetMissionMode(Battle) threw: {ex.GetType().Name}: {ex.Message}"); }

         TryGiveFightBehaviors(player);
         TryGiveFightBehaviors(opponent);

         try
         {
            var handler = Mission.GetMissionBehavior<MissionFightHandler>();
            if (handler == null)
            {
               ModLog.Info("[DUELSPIKE] no MissionFightHandler in the mission, so the opponent will not engage (scene test still valid).");

               return;
            }

            handler.StartCustomFight(
               new List<Agent> {player},
               new List<Agent> {opponent},
               false, // do not drop weapons
               false, // item use enabled
               OnDuelEnd,
               0f);
            ModLog.Info("[DUELSPIKE] custom fight started between the player and the opponent.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUELSPIKE] StartCustomFight threw (scene test still valid): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Best-effort: gives an agent the settlement behaviour groups the custom fight relies on, IF it has a
      ///   navigator. A field-spawned agent may have none (battle agents are driven by team AI, not the civilian
      ///   navigator), which is not a failure, just a note that the fight may not engage.
      /// </summary>
      private static void TryGiveFightBehaviors(Agent agent)
      {
         try
         {
            AgentNavigator nav = agent?.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
            if (nav == null)
            {
               ModLog.Info($"[DUELSPIKE] agent {(agent != null ? "has no navigator (field-spawned)" : "is null")}; relying on team AI for engagement.");

               return;
            }

            nav.AddBehaviorGroup<DailyBehaviorGroup>();
            nav.AddBehaviorGroup<AlarmedBehaviorGroup>();
         }
         catch (Exception ex) { ModLog.Error($"[DUELSPIKE] giving fight behaviors failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The custom fight reporting an end. With invulnerable fighters this is unlikely; the timer is the real close.</summary>
      private void OnDuelEnd(bool playerWon)
         => ModLog.Info($"[DUELSPIKE] the custom fight reported an end (playerWon={playerWon}).");

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
