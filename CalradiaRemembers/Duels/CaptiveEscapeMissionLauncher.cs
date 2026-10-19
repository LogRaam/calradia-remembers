// Code written by Gabriel Mailhot, 20/07/2026.
// Captivity escape encounter (W1 spike): stages the real 1-vs-N scene using the same wilderness field-launch the
// duel spike validated (mod d3de0db). The scene record MUST come from the game's own helper so it gets its
// atmosphere and terrain, or it hangs on the loading screen forever. The outcome (freed / lost gear+horse /
// recaptured / nemesis memory) rides the onEnd callback; this launcher is only the staging.

#region

using System;
using System.Collections.Generic;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Opens the non-lethal captive-escape mission (player unarmed vs up to three club-armed aggressors).</summary>
   public static class CaptiveEscapeMissionLauncher
   {
      /// <summary>
      ///   Stages the escape encounter against <paramref name="foes" /> (clamped 1..3; a null entry becomes a
      ///   bandit filler, so pass the real captor as the first entry to make the player face him). Must be
      ///   called from the campaign map (no mission running). <paramref name="onEnd" /> fires once with the
      ///   outcome just before the return to the map. Returns a human line; null-safe throughout.
      /// </summary>
      public static string StartEscape(IReadOnlyList<CharacterObject> foes, Action<EscapeOutcome> onEnd)
      {
         if (Campaign.Current == null) return "No campaign is running.";
         if (Mission.Current != null) return "A mission is already running. Return to the open map and try again.";

         string scene = FirstLandBattleScene();
         if (string.IsNullOrEmpty(scene)) return "Found no field scene to stage the escape in (see the log).";

         int count = Math.Max(1, Math.Min(foes?.Count ?? 1, 3));

         try
         {
            MissionInitializerRecord rec =
               SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, string.Empty, false, DecalAtlasGroup.Battle);

            MissionState.OpenNew(
               "CalradiaRemembersEscape",
               rec,
               mission => new MissionBehavior[] {
                  new AgentHumanAILogic(),
                  new CaptiveEscapeMissionLogic(foes, onEnd)
               },
               true,
               true);

            ModLog.Info($"[ESCAPE] opening an escape scene in '{scene}' vs {count} aggressor(s).");

            return $"An escape is set against {count} aggressor(s). Knock them all down OR run to the edge; " +
                   "get knocked out yourself and you stay a prisoner.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ESCAPE] OpenNew failed: {ex.GetType().Name}: {ex.Message}");

            return $"Could not stage the escape: {ex.GetType().Name}: {ex.Message} (see the log).";
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
            ModLog.Error($"[ESCAPE] picking a scene failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      #endregion
   }
}
