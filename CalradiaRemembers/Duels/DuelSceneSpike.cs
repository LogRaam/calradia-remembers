// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 1 WILDERNESS SPIKE. The one engine unknown of the whole pillar: outside a settlement there
// is no LocationComplex and no "center" location, so the proven town-ambush launch path does not apply. This
// answers the single binary question before Inc 1 commits to the wilderness, does a field COMBAT scene open
// and then CLOSE cleanly, with no VRAM leak, when there is no MapEvent and no PlayerEncounter behind it? It
// opens a curated singleplayer battle scene through MissionState.OpenNew (the raw, controllable entry a custom
// two-fighter scene needs, not OpenBattleMission which expects armies), spawns the player and one opponent
// itself, holds briefly, then ends. It is a diagnostic, driven by cr.duel_here, deliberately best-effort: the
// agent spawn is wrapped so that even if it fails, the open/close/VRAM measurement, which is the real point,
// still happens. The actual duel (non-lethal knock-out, witnesses, stakes, the challenge_duel action and its
// bridge guard) is the rest of Inc 1 and is NOT here.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Foundation;
using SandBox;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>
   ///   Opens a throwaway field-combat scene to prove the wilderness launch path, and measures VRAM across the
   ///   open and the return. Nothing here persists or touches a save; it exists to be run from the console and
   ///   watched.
   /// </summary>
   public static class DuelSceneSpike
   {
      /// <summary>How long the launch may sit on a loading screen before the spike calls it a failed open.</summary>
      private const float OpenTimeoutSeconds = 90f;

      private static SpikeState _state = SpikeState.Idle;
      private static string _baseline;
      private static float _elapsedInState;

      /// <summary>
      ///   Where the spike is in the open -> in-scene -> back-on-map cycle. MissionState.OpenNew is ASYNC (the
      ///   scene loads behind a loading screen), so the return measurement must wait until the mission has
      ///   actually gone live; treating the still-null Mission.Current right after the call as "already closed"
      ///   is the bug that produced a same-second, meaningless before/after in the first run.
      /// </summary>
      private enum SpikeState
      {
         Idle,     // no spike running
         Opening,  // OpenNew called, waiting for the scene to load (Mission.Current still null)
         InScene   // the mission is live; now watch for the return to the map
      }

      /// <summary>
      ///   Opens the spike field duel against <paramref name="opponent" />. Must be called from the campaign
      ///   map (no mission running). Returns a human line for the console; the detail goes to the log under
      ///   [DUELSPIKE].
      /// </summary>
      public static string OpenFieldDuel(Hero opponent)
      {
         if (Mission.Current != null) return "A mission is already running. Leave it, stand on the open map, and try again.";
         if (Campaign.Current == null) return "No campaign is running.";

         string scene = PickFieldBattleScene();
         if (string.IsNullOrEmpty(scene)) return "Found no singleplayer battle scene to open (see the log).";

         _baseline = VramProbe.Describe();
         ModLog.Info($"[DUELSPIKE] opening field scene '{scene}' vs {opponent?.Name?.ToString() ?? "(a training dummy)"}. VRAM baseline | {_baseline}");

         try
         {
            // Build the record through the game's OWN helper, not by hand: it fills the fields a field scene
            // cannot load without (AtmosphereOnCampaign from the live map weather at the party's position, the
            // map-face TerrainType, the difficulty multipliers). The first run built the record by hand and set
            // none of these, so the scene hung on its loading screen forever. DecalAtlasGroup.Battle is the
            // field-battle atlas (Town is what the settlement paths pass).
            MissionInitializerRecord rec =
               SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, string.Empty, false, DecalAtlasGroup.Battle);

            _state = SpikeState.Opening;
            _elapsedInState = 0f;

            MissionState.OpenNew(
               "CalradiaRemembersDuelSpike",
               rec,
               // AgentHumanAILogic is the engine's human-combat AI: without it a field-spawned opponent just
               // stands there (a field agent has no civilian navigator, so the settlement-brawl helper does not
               // apply). With it, plus the opponent set to Alarmed by DuelSpikeMissionLogic, the AI engages on
               // the navmesh, no formation or MapEvent needed, matching the arena duel's own 1v1 pattern.
               mission => new List<MissionBehavior> {
                  new AgentHumanAILogic(),
                  new DuelSpikeMissionLogic(opponent)
               },
               true,   // add the engine's default mission behaviors (camera, agent locator, and so on)
               true);  // needsMemoryCleanup: the flag that should release the mission's render targets on end

            return $"Opening a field duel scene ('{scene}'). It auto-closes after about " +
                   $"{DuelSpikeMissionLogic.AutoCloseSeconds:0} seconds; watch the log for the VRAM before/after.";
         }
         catch (Exception ex)
         {
            _state = SpikeState.Idle;
            ModLog.Error($"[DUELSPIKE] OpenNew failed: {ex.GetType().Name}: {ex.Message}");

            return $"Could not open the field scene: {ex.GetType().Name}: {ex.Message} (see the log).";
         }
      }

      /// <summary>
      ///   Pumped every frame from the submodule (including during the loading screen, since it rides the
      ///   application tick, not the mission tick). Drives the open -> in-scene -> back-on-map state machine and,
      ///   on the return, logs the VRAM before/after so a render-target leak (or its absence) is visible without
      ///   any in-mission hook surviving teardown.
      /// </summary>
      public static void Tick(float dt)
      {
         if (_state == SpikeState.Idle) return;

         _elapsedInState += dt;

         if (_state == SpikeState.Opening)
         {
            if (Mission.Current != null)
            {
               _state = SpikeState.InScene;
               _elapsedInState = 0f;
               ModLog.Info("[DUELSPIKE] the field scene opened (the mission is live). Now watching for the return to the map.");

               return;
            }

            // Still on the loading screen. If the mission never goes live, the raw field launch does not work
            // from here: say so once and stand down (a stuck loading screen is itself the spike's answer).
            if (_elapsedInState > OpenTimeoutSeconds)
            {
               _state = SpikeState.Idle;
               ModLog.Error($"[DUELSPIKE] the mission never went live after {OpenTimeoutSeconds:0}s: the field scene did not open " +
                            "(a stuck loading screen). Spike FAILURE for this launch path.");
            }

            return;
         }

         // InScene: the mission was live; the first frame it is gone, we are back on the map.
         if (Mission.Current != null) return;

         _state = SpikeState.Idle;
         string after = VramProbe.Describe();
         ModLog.Info($"[DUELSPIKE] back on the map after the field scene closed. VRAM before | {_baseline}");
         ModLog.Info($"[DUELSPIKE] back on the map after the field scene closed. VRAM after  | {after}");
         ModLog.Info("[DUELSPIKE] compare the two lines above: a clean return leaves the 'after' at or near the 'before'. " +
                     "A large climb that does not come back is the render-target leak the spike is hunting.");
      }

      #region private

      /// <summary>
      ///   A land (non-naval) battle scene from the game's own curated list, so the scene is guaranteed to exist
      ///   and load. The spike only needs SOME valid field scene to answer the open/close/VRAM question, so it
      ///   takes the first land one; matching the biome to where the player stands is an Inc 1 nicety, not a
      ///   plumbing concern. Null only if the list is empty (a broken install). Note
      ///   <see cref="SingleplayerBattleSceneData" /> is a value type, hence the index-based selection.
      /// </summary>
      private static string PickFieldBattleScene()
      {
         try
         {
            var all = GameSceneDataManager.Instance?.SingleplayerBattleScenes;
            if (all == null || all.Count == 0)
            {
               ModLog.Error("[DUELSPIKE] GameSceneDataManager has no singleplayer battle scenes.");

               return null;
            }

            foreach (SingleplayerBattleSceneData s in all)
               if (!s.IsNaval && !string.IsNullOrEmpty(s.SceneID))
               {
                  ModLog.Info($"[DUELSPIKE] chose the first land battle scene '{s.SceneID}' (terrain {s.Terrain}).");

                  return s.SceneID;
               }

            ModLog.Error("[DUELSPIKE] no land battle scenes in the curated list.");

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUELSPIKE] picking a scene failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #endregion
   }
}
