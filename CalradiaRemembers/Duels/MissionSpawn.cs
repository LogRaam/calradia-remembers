// Code written by Gabriel Mailhot, 25/10/2026.
// tashmetu (Nexus, 25/10/2026): Mission.SpawnAgent(AgentBuildData, bool) is gone in the 1.5.5 beta, which added two
// optional parameters; optional parameters are filled at compile time, so a call compiled against 1.4.8 throws
// MissingMethodException there "the moment a fighter is spawned". Gabriel (25/10/2026): one build for both versions,
// the method found at run time. SpawnAgentSignaturePolicy picks the overload; this reads the game's shapes, makes the
// call with each extra parameter at its own default, and caches the method. cr.test spawn_agent_signature checks the
// running game offers one.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Duels;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Duels
{
   /// <summary>Spawns an agent through whichever Mission.SpawnAgent overload the running game version has.</summary>
   internal static class MissionSpawn
   {
      private static MethodInfo _method;
      private static bool _looked;

      /// <summary>The overload in use, or null when the game offers none CR can call (cr.test all's seam).</summary>
      internal static MethodInfo Method
      {
         get
         {
            if (_looked) return _method;

            MethodInfo[] candidates = typeof(Mission).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                                     .Where(m => m.Name == SpawnAgentSignaturePolicy.MethodName).ToArray();
            var shapes = new List<SpawnMethodShape>();
            foreach (MethodInfo m in candidates)
            {
               ParameterInfo[] ps = m.GetParameters();
               shapes.Add(new SpawnMethodShape
               {
                  Name = m.Name,
                  IsPublic = m.IsPublic,
                  IsStatic = m.IsStatic,
                  ParameterTypes = ps.Select(p => p.ParameterType.Name).ToList(),
                  ParameterOptional = ps.Select(p => p.HasDefaultValue).ToList()
               });
            }

            int pick = SpawnAgentSignaturePolicy.Pick(shapes);
            _method = pick >= 0 ? candidates[pick] : null;
            _looked = true;
            if (_method == null) ModLog.Error("[DUEL] the game offers no Mission.SpawnAgent(AgentBuildData, ...) CR can call; fighters cannot be spawned.");

            return _method;
         }
      }

      /// <summary>
      ///   Mission.SpawnAgent(build) with every other parameter at its default (spawnFromAgentVisuals false on every
      ///   version). Throws what the engine throws, unwrapped, as the direct call did.
      /// </summary>
      internal static Agent Spawn(Mission mission, AgentBuildData build)
      {
         MethodInfo method = Method ?? throw new MissingMethodException("Mission", SpawnAgentSignaturePolicy.MethodName);
         ParameterInfo[] ps = method.GetParameters();
         var args = new object[ps.Length];
         args[0] = build;
         for (int i = 1; i < ps.Length; i++) args[i] = ps[i].DefaultValue;

         try
         {
            return (Agent) method.Invoke(mission, args);
         }
         catch (TargetInvocationException ex) when (ex.InnerException != null)
         {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
         }
      }
   }
}
