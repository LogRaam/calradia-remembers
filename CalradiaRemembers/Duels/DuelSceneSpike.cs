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
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
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
      private static bool _awaitingReturnMeasure;
      private static string _baseline;

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
            var rec = new MissionInitializerRecord(scene) {
               PlayingInCampaignMode = true,
               DoNotUseLoadingScreen = false,
               SceneLevels = string.Empty,
               NeedsRandomTerrain = false
            };

            _awaitingReturnMeasure = true;

            MissionState.OpenNew(
               "CalradiaRemembersDuelSpike",
               rec,
               // MissionFightHandler is CR's own proven combat trigger (the town ambush drives its brawl through
               // StartCustomFight on it); adding it here lets the spike start a real fight in the field scene. It
               // is the only extra behaviour added, to keep the untested surface small: DuelSpikeMissionLogic
               // spawns the two fighters and drives the auto-close timer.
               mission => new List<MissionBehavior> {
                  new MissionFightHandler(),
                  new DuelSpikeMissionLogic(opponent)
               },
               true,   // add the engine's default mission behaviors (camera, agent locator, and so on)
               true);  // needsMemoryCleanup: the flag that should release the mission's render targets on end

            return $"Opening a field duel scene ('{scene}'). It auto-closes after about " +
                   $"{DuelSpikeMissionLogic.AutoCloseSeconds:0} seconds; watch the log for the VRAM before/after.";
         }
         catch (Exception ex)
         {
            _awaitingReturnMeasure = false;
            ModLog.Error($"[DUELSPIKE] OpenNew failed: {ex.GetType().Name}: {ex.Message}");

            return $"Could not open the field scene: {ex.GetType().Name}: {ex.Message} (see the log).";
         }
      }

      /// <summary>
      ///   Pumped every frame from the submodule. Once the spike mission has ended and we are back on the map,
      ///   samples VRAM again and logs the before/after so a leak (or its absence) is visible without any
      ///   in-mission hook surviving teardown. One-shot per spike.
      /// </summary>
      public static void Tick()
      {
         if (!_awaitingReturnMeasure) return;
         if (Mission.Current != null) return; // still in the scene

         _awaitingReturnMeasure = false;
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
