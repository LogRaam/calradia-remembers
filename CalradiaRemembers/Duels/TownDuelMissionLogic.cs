// Code written by Gabriel Mailhot, 22/07/2026.
// Duels pillar: the fight driver for a duel staged in the town-centre scene. It is the town twin of
// DuelMissionLogic, and it differs from it in exactly two ways, both forced by the venue:
//   - It does NOT spawn the fighters. The player is already in the scene (the mission put them there) and the
//     opponent was injected among the crowd by TownDuelBehavior, so this waits for that agent to exist, walks
//     him into place, and starts a custom fight between the two.
//   - It starts the fight through MissionFightHandler.StartCustomFight, the same seam the street brawl uses,
//     because in a town the two are ordinary civilians until someone declares a fight; there are no battle
//     teams to put them on.
// Non-lethality is unchanged and load-bearing: this logic is the mission's IAgentStateDecider and answers
// Unconscious for every downed agent, so a killing blow knocks out and a chat can never delete a named hero.
// Bystanders are deliberately left alone (unlike the ambush, which scatters them): a duel is a public matter,
// and the pillar's own design says witnesses STAY. They are not in either side of the custom fight.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Adjudicates a non-lethal duel inside the town-centre scene: waits for the opponent, starts the fight, reports the winner.</summary>
   public sealed class TownDuelMissionLogic : MissionLogic, IAgentStateDecider
   {
      /// <summary>Person-sized clearance for the path-reachability check, the value the street brawl uses.</summary>
      private const float CombatantNavRadius = 0.4f;

      /// <summary>Seconds to linger after a fall before returning to the town, so the knockout is seen.</summary>
      private const float EndLingerSeconds = 4f;

      /// <summary>Roughly two seconds of grace for the injected opponent to finish spawning before we give up.</summary>
      private const int MaxWaitTicks = 120;

      /// <summary>Metres between the two when the duel starts: seen coming, closed quickly. Falls back inward when walled off.</summary>
      private static readonly float[] RingRadiusTries = {12f, 9f, 15f, 7f};

      private readonly Action<bool> _onDuelEnd;
      private readonly string _opponentId;

      private bool _closed; // the outcome callback has fired and the mission is closing; nothing may reopen it
      private bool _decided;
      private float _endElapsed;
      private Agent _opponentAgent;
      private bool _playerWon;
      private bool _started;
      private int _waitTicks;

      public TownDuelMissionLogic(string opponentId, Action<bool> onDuelEnd)
      {
         _opponentId = opponentId;
         _onDuelEnd = onDuelEnd;
      }

      /// <summary>Answering Unconscious for every downed agent is what makes the duel non-lethal (the arena's own mechanism).</summary>
      public AgentState GetAgentState(Agent affectedAgent, float deathProbability, out bool usedSurgery)
      {
         usedSurgery = false;

         return AgentState.Unconscious;
      }

      /// <summary>
      ///   The moment one of the two falls, the duel is decided. The player's own fall is caught here rather
      ///   than through the fight handler's callback for the reason the street brawl documents: a downed player
      ///   ends the mission on the vanilla knocked-unconscious flow, which returns to the town menu BEFORE the
      ///   handler ever reports a loss.
      /// </summary>
      public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
      {
         if (_decided || !_started) return;
         if (affectedAgent == null) return;
         if (affectedAgent != Mission?.MainAgent && affectedAgent != _opponentAgent) return;

         Decide(affectedAgent == _opponentAgent, "a duellist fell");
      }

      public override void OnMissionTick(float dt)
      {
         try
         {
            if (_decided)
            {
               TickEnding(dt);

               return;
            }

            if (_started || Mission?.MainAgent == null) return;

            _waitTicks++;
            Agent opponent = TownDuelBehavior.OpponentAgent;
            bool ready = opponent != null && opponent.IsActive();

            if (!ready && _waitTicks <= MaxWaitTicks) return;

            if (!ready)
            {
               // Nothing to fight. Say so loudly (the injection lines above are what diagnose it) and report a
               // player win rather than stranding the caller with no outcome at all: no blades were crossed,
               // but the opponent is the one who failed to appear.
               _started = true;
               ModLog.Error($"[DUEL] the town duel opponent (id {_opponentId}) never spawned after {_waitTicks} ticks; the duel fizzles.");
               Decide(true, "opponent never appeared");

               return;
            }

            _opponentAgent = opponent;
            StartFight(opponent);
         }
         catch (Exception ex) { ModLog.Error($"[DUEL] town duel tick failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>A duel is not a fight you can walk away from: refuse the leave request until someone has fallen.</summary>
      public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
      {
         canPlayerLeave = _decided;

         return null;
      }

      #region private

      /// <summary>Finds a navmesh point about a duel's distance from the player, reachable by a real path.</summary>
      private static bool TryFindStandingPoint(Scene scene, WorldPosition centre, out Vec3 dest)
      {
         dest = default;
         Vec2 origin = centre.AsVec2;

         for (var j = 0; j < 8; j++)
         {
            double angle = 2.0 * Math.PI * j / 8.0;

            foreach (float r in RingRadiusTries)
            {
               var target = new Vec2(origin.x + r * (float) Math.Cos(angle), origin.y + r * (float) Math.Sin(angle));

               WorldPosition candidate = centre;
               candidate.SetVec2(target);

               if (candidate.GetNavMesh() == UIntPtr.Zero) continue; // a wall or a building, not standable

               WorldPosition from = centre;

               if (!scene.GetPathDistanceBetweenPositions(ref candidate, ref from, CombatantNavRadius, out float pathDist)) continue;
               if (pathDist > r * 2f) continue; // reachable only by a long detour, so not across from the player

               dest = candidate.GetGroundVec3();

               return true;
            }
         }

         return false;
      }

      /// <summary>Latches the result exactly once and starts the linger timer that closes the mission.</summary>
      private void Decide(bool playerWon, string reason)
      {
         if (_closed) return; // the duel is already over and paid out; a late fall changes nothing

         _playerWon = playerWon;
         _decided = true;
         _endElapsed = 0f;
         ModLog.Info($"[DUEL] town duel decided ({reason}): playerWon={playerWon}. Closing shortly.");
      }

      /// <summary>Walks the opponent across from the player, then declares the two sides to the fight handler.</summary>
      private void StartFight(Agent opponent)
      {
         _started = true;

         var fight = Mission.GetMissionBehavior<MissionFightHandler>();

         if (fight == null)
         {
            ModLog.Error("[DUEL] no MissionFightHandler in the town-centre mission; cannot start the duel.");
            Decide(true, "no fight handler");

            return;
         }

         // Put him across from the player rather than wherever the crowd spawn point happened to be. Fail-soft:
         // if no reachable point is found he simply walks over from where he stands.
         Scene scene = Mission.Scene;

         if (scene != null && TryFindStandingPoint(scene, Mission.MainAgent.GetWorldPosition(), out Vec3 dest))
            try { opponent.TeleportToPosition(dest); }
            catch (Exception ex) { ModLog.Info($"[DUEL] could not place the opponent ({ex.GetType().Name}); he closes from where he stands."); }

         fight.StartCustomFight(
            new List<Agent> {Mission.MainAgent},
            new List<Agent> {opponent},
            false,
            false,
            OnFightEnd);
         ModLog.Info("[DUEL] town duel started in the town centre.");
      }

      /// <summary>
      ///   The fight handler's own verdict. It is the SECOND path to a decision, not the first: a downed player
      ///   is caught in OnAgentRemoved before this ever fires, and Decide is idempotent, so whichever arrives
      ///   first wins and the other is ignored.
      /// </summary>
      private void OnFightEnd(bool isPlayerSideWon)
      {
         if (_decided) return;

         Decide(isPlayerSideWon, "the fight handler reported the end");
      }

      /// <summary>Fires the outcome callback exactly once after the linger, then closes the mission.</summary>
      private void TickEnding(float dt)
      {
         _endElapsed += dt;

         if (_endElapsed < EndLingerSeconds) return;

         _closed = true;  // one payout, whatever happens in the frames EndMission takes
         _decided = false;

         if (_onDuelEnd != null)
            try { _onDuelEnd(_playerWon); }
            catch (Exception ex) { ModLog.Error($"[DUEL] onDuelEnd callback threw: {ex.GetType().Name}: {ex.Message}"); }

         try { Mission.EndMission(); }
         catch (Exception ex) { ModLog.Error($"[DUEL] EndMission threw: {ex.GetType().Name}: {ex.Message}"); }
      }

      #endregion
   }
}
