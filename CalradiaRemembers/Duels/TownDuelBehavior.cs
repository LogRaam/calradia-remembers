// Code written by Gabriel Mailhot, 22/07/2026.
// Duels pillar: staging a duel INSIDE a town. This matters more than it sounds, because most conversations in
// this mod happen in a settlement, and until now the duel verb was only ever taught on the open map (see
// DuelStagingSupport), so most players would never meet the feature at all.
// There is no second scene to invent: the town-centre street ambush already solved this exact problem, and its
// recipe is proven in game (TownAmbushBehavior). This is that recipe with one consented opponent instead of a
// gang, and non-lethality on top:
//   1. Arm, then open the walkable town centre exactly as the "take a walk" menu does.
//   2. Inject the opponent hero among the crowd on LocationCharactersAreReadyToSpawnEvent (the ONLY point where
//      a location's characters can be added), capturing the spawned Agent through the afterAgentCreated hook.
//   3. Attach the fight driver from the Harmony postfix on Mission.AfterStart, because every campaign
//      mission-start event fires BEFORE the agents exist (see AmbushMissionStartPatch's own header).
// The opponent is spawned NEUTRAL, not Enemy: this is a consented duel, not a crime, so the town must not treat
// it as an assault. MissionFightHandler.StartCustomFight assigns the two sides explicitly (the arena's own
// pattern), so the relation is not what makes them fight.

#region

using System;
using CalradiaRemembers.Logic;
using SandBox;
using SandBox.Missions.AgentBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using FaceGen = TaleWorlds.Core.FaceGen;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Stages a duel in the town-centre scene, reusing the proven street-ambush injection recipe.</summary>
   public sealed class TownDuelBehavior : CampaignBehaviorBase
   {
      // Set when a town duel is armed, consumed by AttachDuelIfPending once the mission has fully started. One
      // player, one duel at a time, and a duel never spans a save, so static process-only state is correct here.
      private static string _pendingOpponentId;
      private static Action<bool> _pendingOnDuelEnd;
      private static string _spawnTag;

      /// <summary>The opponent's agent as it spawns, handed to the fight driver as the other side of the duel.</summary>
      public static Agent OpponentAgent { get; private set; }

      /// <summary>
      ///   Arms a town duel against <paramref name="opponent" /> and opens the town centre. Must be called with
      ///   the player inside a TOWN and no mission running. Returns a human line for the caller; the fight
      ///   itself is attached later, from the Mission.AfterStart postfix. On any failure the pending state is
      ///   cleared, so a botched launch cannot leave a duel armed against the next mission the player opens.
      /// </summary>
      public static string Arm(Hero opponent, Action<bool> onDuelEnd)
      {
         if (opponent == null) return "No opponent to duel.";
         if (Settlement.CurrentSettlement is not {IsTown: true}) return "A town duel needs the player to be inside a town.";
         if (Mission.Current != null) return "A mission is already running.";

         // THE SCENE HAS TO EXIST BEFORE THE ENGINE IS ASKED TO LOAD IT. GetLocationWithId returns null for a
         // settlement whose complex carries no "center", which a modded or total-conversion town may well not
         // — and a null went straight into CreateAndOpenMissionController, where the engine crashed loading
         // the scene rather than refusing it (player report, 17/09/2026). A missing venue is a thing to say,
         // not a thing to hand to the engine.
         //
         // The same test DuelEligibilityResolver uses to decide whether to OFFER the duel at all, which is the
         // point: it was already right, and this path was asking a different question.
         Location centre = LocationComplex.Current?.GetLocationWithId("center");

         if (centre == null || PlayerEncounter.LocationEncounter == null)
         {
            ModLog.Error($"[DUEL] {Settlement.CurrentSettlement?.Name} has no town centre to fight in "
                       + $"(centre={centre != null}, encounter={PlayerEncounter.LocationEncounter != null}); refused.");

            return $"There is nowhere in {Settlement.CurrentSettlement?.Name} to hold a duel.";
         }

         try
         {
            _pendingOpponentId = opponent.StringId;
            _pendingOnDuelEnd = onDuelEnd;
            OpponentAgent = null;

            PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(centre);
            ModLog.Info($"[DUEL] town duel armed against {opponent.Name}; opening the town centre.");

            return $"You step out to meet {opponent.Name}. The loser is knocked out, not killed.";
         }
         catch (Exception ex)
         {
            Clear();
            ModLog.Error($"[DUEL] failed to open the town centre: {ex.GetType().Name}: {ex.Message}");

            return $"Could not stage the duel here: {ex.GetType().Name} (see the log).";
         }
      }

      /// <summary>
      ///   Called from the Harmony postfix on <c>Mission.AfterStart</c> (the one reliable "the mission has FULLY
      ///   started" seam, shared with the street ambush): attaches the duel driver and consumes the pending
      ///   state. A no-op for every ordinary mission.
      /// </summary>
      public static void AttachDuelIfPending(Mission mission)
      {
         try
         {
            if (string.IsNullOrEmpty(_pendingOpponentId)) return;

            string opponentId = _pendingOpponentId;
            Action<bool> onDuelEnd = _pendingOnDuelEnd;
            _pendingOpponentId = null; // consume; OpponentAgent stays, the driver needs it
            _pendingOnDuelEnd = null;

            mission?.AddMissionBehavior(new TownDuelMissionLogic(opponentId, onDuelEnd));
            ModLog.Info($"[DUEL] town duel driver attached after AfterStart (opponent id {opponentId}).");
         }
         catch (Exception ex) { ModLog.Error($"[DUEL] attach town duel driver failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      public override void RegisterEvents()
      {
         CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(this, OnLocationCharactersReadyToSpawn);
         // The fight driver is attached from the Harmony postfix on Mission.AfterStart (AmbushMissionStartPatch),
         // for the reason that patch's own header gives: the campaign events all fire before the agents spawn.
      }

      public override void SyncData(IDataStore dataStore) { } // a duel never spans a save

      #region private

      /// <summary>Gives the spawned opponent the behaviour groups a town fight needs, exactly as the ambush does.</summary>
      private static void AddDuellistBehaviors(IAgent agent)
      {
         if (agent is not Agent a) return;

         AgentNavigator nav = a.GetComponent<CampaignAgentComponent>()?.AgentNavigator;

         if (nav == null) return;

         nav.AddBehaviorGroup<DailyBehaviorGroup>();
         nav.AddBehaviorGroup<AlarmedBehaviorGroup>();
      }

      private static void Clear()
      {
         _pendingOpponentId = null;
         _pendingOnDuelEnd = null;
         OpponentAgent = null;
      }

      /// <summary>
      ///   Builds the opponent as himself: his own hero appearance and BATTLE gear (a duel is fought armed, so
      ///   no civilian equipment), on foot, spawned Neutral because the fight is consented and the fight handler
      ///   assigns the sides itself. Captured into <see cref="OpponentAgent" /> as it spawns.
      /// </summary>
      private static LocationCharacter CreateDuellist(Hero opponent)
      {
         CharacterObject troop = opponent.CharacterObject;
         Monster monster = FaceGen.GetMonsterWithSuffix(troop.Race, "_settlement");
         AgentData data = new AgentData(new SimpleAgentOrigin(troop)).Monster(monster).NoHorses(true);

         return new LocationCharacter(
            data,
            AddDuellistBehaviors,
            _spawnTag,
            false,
            LocationCharacter.CharacterRelations.Neutral,
            null,
            false, // his battle gear: a duel is fought armed
            false,
            null,
            false,
            false,
            true,
            OnDuellistSpawned);
      }

      private static void OnDuellistSpawned(IAgent agent)
      {
         if (agent is not Agent a) return;

         OpponentAgent = a;
         ModLog.Info($"[DUEL] opponent agent spawned in the town scene (active={a.IsActive()}).");
      }

      /// <summary>The usable spawn-point tag with the most free points, so the opponent actually appears.</summary>
      private static string PickSpawnTag(System.Collections.Generic.Dictionary<string, int> usablePointCount)
      {
         if (usablePointCount == null || usablePointCount.Count == 0) return null;

         string best = null;
         var bestCount = 0;
         foreach (System.Collections.Generic.KeyValuePair<string, int> kv in usablePointCount)
            if (kv.Value > bestCount)
            {
               bestCount = kv.Value;
               best = kv.Key;
            }

         return best;
      }

      /// <summary>
      ///   Injects the opponent among the town-centre crowd while the location is being populated: the only
      ///   moment a location's characters can be added. The pending id is deliberately LEFT SET, and consumed
      ///   later by <see cref="AttachDuelIfPending" />, which is what attaches the driver.
      /// </summary>
      private void OnLocationCharactersReadyToSpawn(System.Collections.Generic.Dictionary<string, int> usablePointCount)
      {
         try
         {
            if (string.IsNullOrEmpty(_pendingOpponentId)) return; // an ordinary town visit

            Location center = LocationComplex.Current?.GetLocationWithId("center");

            if (center == null)
            {
               ModLog.Error("[DUEL] no 'center' location to stage the town duel in.");

               return;
            }

            Hero opponent = Hero.FindFirst(h => h != null && h.StringId == _pendingOpponentId);

            if (opponent?.CharacterObject == null)
            {
               ModLog.Error($"[DUEL] town duel opponent id '{_pendingOpponentId}' resolved to nobody; nothing injected.");

               return;
            }

            _spawnTag = PickSpawnTag(usablePointCount);
            OpponentAgent = null;

            Hero captured = opponent; // capture for the closure
            center.AddLocationCharacters((c, r) => CreateDuellist(captured), opponent.Culture ?? Settlement.CurrentSettlement?.Culture,
                                         LocationCharacter.CharacterRelations.Neutral, 1);
            ModLog.Info($"[DUEL] injected {opponent.Name} into the town centre at tag '{_spawnTag ?? "(null)"}'.");
         }
         catch (Exception ex) { ModLog.Error($"[DUEL] town duel injection failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      #endregion
   }
}
