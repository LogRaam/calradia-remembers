// Code written by Gabriel Mailhot, 15/07/2026.
// Duels pillar, Inc 1: opens the real duel mission, using the field-launch recipe the cr.duel_here spike
// validated (mod d3de0db). For now it stages the duel in a WILDERNESS field scene; the town-centre and
// town-gates locations (the proven ambush path) are a later step, chosen from where the chat encounter sits.
// The record MUST come from the game's own helper so the scene gets its atmosphere and terrain, or it hangs on
// the loading screen forever.

#region

using System;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Opens the non-lethal duel mission against a single hero and hands the winner back through a callback.</summary>
   public static class DuelMissionLauncher
   {
      /// <summary>
      ///   Stages a wilderness field duel against <paramref name="opponent" />. Must be called from the campaign
      ///   map (no mission running). <paramref name="onDuelEnd" /> is invoked with playerWon once someone falls,
      ///   just before the return to the map. Returns a human line for the caller; null-safe throughout.
      /// </summary>
      public static string StartWildernessDuel(Hero opponent, Action<bool> onDuelEnd)
      {
         if (Campaign.Current == null) return "No campaign is running.";
         if (Mission.Current != null) return "A mission is already running. Return to the open map and try again.";
         if (opponent == null) return "No opponent to duel.";

         string scene = FirstLandBattleScene();
         if (string.IsNullOrEmpty(scene)) return "Found no field scene to stage the duel in (see the log).";

         try
         {
            // The game's own helper fills AtmosphereOnCampaign / TerrainType / difficulty from the live map
            // position; a hand-built record has no atmosphere and never finishes loading. DecalAtlasGroup.Battle
            // is the field-battle atlas (Town is for settlement scenes).
            MissionInitializerRecord rec =
               SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, string.Empty, false, DecalAtlasGroup.Battle);

            MissionState.OpenNew(
               "CalradiaRemembersDuel",
               rec,
               // AgentHumanAILogic drives the opponent's combat AI (it engages on the navmesh once Alarmed);
               // DuelMissionLogic spawns the fighters, enforces non-lethality, and adjudicates the winner.
               mission => new MissionBehavior[] {
                  new AgentHumanAILogic(),
                  new DuelMissionLogic(opponent, onDuelEnd)
               },
               true,   // add the engine's default mission behaviours (camera, agent locator, and so on)
               true);  // needsMemoryCleanup: releases the mission's render targets on end (no VRAM leak)

            ModLog.Info($"[DUEL] opening a wilderness duel in '{scene}' vs {opponent.Name}.");

            return $"A duel is set against {opponent.Name}. Close the gap and fight; the loser is knocked out, not killed.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUEL] OpenNew failed: {ex.GetType().Name}: {ex.Message}");

            return $"Could not stage the duel: {ex.GetType().Name}: {ex.Message} (see the log).";
         }
      }

      #region private

      /// <summary>The first land (non-naval) battle scene from the game's curated list, or null on a broken install.</summary>
      private static string FirstLandBattleScene()
      {
         try
         {
            var all = GameSceneDataManager.Instance?.SingleplayerBattleScenes;
            if (all == null || all.Count == 0) return null;

            foreach (SingleplayerBattleSceneData s in all)
               if (!s.IsNaval && !string.IsNullOrEmpty(s.SceneID))
                  return s.SceneID;

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DUEL] picking a scene failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #endregion
   }
}
