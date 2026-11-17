// Code written by Gabriel Mailhot, 17/11/2026.
// EvilPoptart1987 (Nexus, 15 and 16/11/2026): women in the ranks come in "with ridiculously tiny heads", still after the lean
// fix. Gabriel (17/11/2026): "est-ce que tu peux generer des logs qui montrent ce qui est rendu en jeu comme proportions?"
// cr.ranks_probe measures, in the battle under way, the skeleton the engine actually animates for each soldier: the head
// bone's own scale, the head-to-neck and neck-to-pelvis distances, the stature, the agent scale, and the body values the
// face was drawn with. Women CR made are set beside the battle's men and any woman the game itself made (a female troop, a
// lady), so a shrunken head shows as a number rather than a screenshot. Reads only; changes nothing.

#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.ranks_probe [count] - logs the rendered proportions of soldiers in the current battle (women CR made, men, the game's own women).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("ranks_probe", "cr")]
      public static string RanksProbe(List<string> args)
      {
         Mission mission = Mission.Current;
         if (mission == null) return "Run it during a battle (no mission is open).";

         int perGroup = args is {Count: > 0} && int.TryParse(args[0], out int n) && n > 0 ? Math.Min(n, 50) : 12;
         List<Agent> humans = mission.Agents.Where(a => a != null && a.IsHuman && a.IsActive()).ToList();
         if (humans.Count == 0) return "No living human agent in this mission.";

         // A woman CR made: the agent is a woman while her troop's own data is a man.
         List<Agent> crWomen = humans.Where(a => a.IsFemale && a.Character != null && !a.Character.IsFemale).Take(perGroup).ToList();
         List<Agent> gameWomen = humans.Where(a => a.IsFemale && a.Character != null && a.Character.IsFemale).Take(perGroup).ToList();
         List<Agent> men = humans.Where(a => !a.IsFemale).Take(perGroup).ToList();

         var sb = new StringBuilder();
         sb.AppendLine($"[RANKS PROBE] {humans.Count} living humans; showing up to {perGroup} per group.");
         sb.AppendLine("[RANKS PROBE] columns: group | troop | age weight build | agentScale | headBoneScale (s,f,u) | head-neck | neck-pelvis | stature | head/torso | voice");
         var summary = new List<string>();
         foreach ((string group, List<Agent> agents) in new[] {("CR woman", crWomen), ("game woman", gameWomen), ("man", men)})
         {
            var ratios = new List<float>();
            var scales = new List<float>();
            var voices = new List<string>();
            foreach (Agent a in agents)
            {
               string line = Measure(group, a, out float ratio, out float headScale);
               sb.AppendLine(line);
               voices.Add(Voice(a));
               if (ratio > 0) ratios.Add(ratio);
               if (headScale > 0) scales.Add(headScale);
            }

            summary.Add(agents.Count == 0
               ? $"{group}: none in this battle"
               : $"{group}: {agents.Count} measured, mean head/torso {Mean(ratios)}, mean head bone scale {Mean(scales)}, voices {string.Join(", ", voices.Distinct())}");
         }

         foreach (string s in summary) sb.AppendLine("[RANKS PROBE] SUMMARY " + s);
         string report = sb.ToString();
         ModLog.Info(report);

         return string.Join("\n", summary) + "\n(full table in CalradiaRemembers.log, tag [RANKS PROBE])";
      }

      private static string Measure(string group, Agent a, out float headToTorso, out float headBoneScale)
      {
         headToTorso = 0;
         headBoneScale = 0;
         string troop = a.Character?.StringId ?? "?";
         try
         {
            BodyProperties body = a.BodyPropertiesValue;
            Monster m = a.Monster;
            Skeleton skeleton = a.AgentVisuals?.GetSkeleton();
            if (m == null || skeleton == null) return $"[RANKS PROBE] {group} | {troop} | no skeleton";

            MatrixFrame head = skeleton.GetBoneEntitialFrameWithIndex(m.HeadLookDirectionBoneIndex);
            MatrixFrame neck = skeleton.GetBoneEntitialFrameWithIndex(m.NeckRootBoneIndex);
            MatrixFrame pelvis = skeleton.GetBoneEntitialFrameWithIndex(m.PelvisBoneIndex);
            MatrixFrame foot = skeleton.GetBoneEntitialFrameWithIndex(m.PrimaryFootBoneIndex);

            float headNeck = (head.origin - neck.origin).Length;
            float neckPelvis = (neck.origin - pelvis.origin).Length;
            float stature = head.origin.z - foot.origin.z;
            headBoneScale = (head.rotation.s.Length + head.rotation.f.Length + head.rotation.u.Length) / 3f;
            headToTorso = neckPelvis > 0.0001f ? headNeck / neckPelvis : 0;

            return string.Format(CultureInfo.InvariantCulture,
               "[RANKS PROBE] {0} | {1} | {2:0} {3:0.00} {4:0.00} | {5:0.000} | ({6:0.000},{7:0.000},{8:0.000}) | {9:0.000} | {10:0.000} | {11:0.000} | {12:0.000} | {13}",
               group, troop, body.Age, body.Weight, body.Build, a.AgentScale, head.rotation.s.Length, head.rotation.f.Length, head.rotation.u.Length,
               headNeck, neckPelvis, stature, headToTorso, Voice(a));
         }
         catch (Exception ex)
         {
            return $"[RANKS PROBE] {group} | {troop} | failed: {ex.GetType().Name}: {ex.Message}";
         }
      }

      // The voice the engine gave the soldier (Gabriel, 17/11/2026: "est-ce que les femmes que l'on cree avec CR ont une voix feminine?").
      private static string Voice(Agent a)
      {
         try { return a.GetAgentVoiceDefinition() ?? "-"; }
         catch (Exception ex) { return "? " + ex.GetType().Name; }
      }

      private static string Mean(List<float> values)
         => values.Count == 0 ? "-" : values.Average().ToString("0.000", CultureInfo.InvariantCulture);
   }
}
