// Code written by Gabriel Mailhot, 25/10/2026.
// tashmetu (Nexus, 25/10/2026), from a check of every mod DLL against the 1.5.5 beta: "Mission.SpawnAgent(AgentBuildData,
// bool) is gone in 1.5.5. The method gained two optional parameters there [...] a DLL built against 1.4.8 still calls the
// two-parameter method. On 1.5.5 that should throw MissingMethodException the moment a fighter is spawned." The duel, the
// captive's escape and the duel spike all spawn their fighters that way. Gabriel (25/10/2026): build it for 2.7.21, one
// build for both versions, finding the method at run time as tashmetu suggested. This picks which overload to call from
// the shapes the game offers; the engine glue (Duels/MissionSpawn) reads the shapes and makes the call.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>One method the game's Mission type offers, as reflection sees it.</summary>
   public sealed class SpawnMethodShape
   {
      /// <summary>The method's name ("SpawnAgent").</summary>
      public string Name { get; init; }

      /// <summary>Whether it is public (an internal overload is the engine's own business).</summary>
      public bool IsPublic { get; init; }

      /// <summary>Whether it is static (SpawnAgent is called on the mission itself).</summary>
      public bool IsStatic { get; init; }

      /// <summary>The simple type name of each parameter, in order ("AgentBuildData", "Boolean", "Equipment").</summary>
      public IReadOnlyList<string> ParameterTypes { get; init; }

      /// <summary>Whether each parameter, in order, has a default value the caller may leave out.</summary>
      public IReadOnlyList<bool> ParameterOptional { get; init; }
   }

   /// <summary>
   ///   Which Mission.SpawnAgent overload spawns a fighter from an AgentBuildData alone, whatever the game version: a
   ///   public instance SpawnAgent whose first parameter is AgentBuildData and every other one optional (1.4.8 has
   ///   one bool after it, 1.5.5 a bool, an Equipment and an ItemObject). Among several, the fewest parameters wins,
   ///   so the choice does not hang on the order reflection lists them in.
   /// </summary>
   public static class SpawnAgentSignaturePolicy
   {
      /// <summary>The method looked for.</summary>
      public const string MethodName = "SpawnAgent";

      /// <summary>The type its first parameter must be.</summary>
      public const string BuildDataType = "AgentBuildData";

      /// <summary>The index in <paramref name="methods" /> of the overload to call, or -1 when none fits.</summary>
      public static int Pick(IReadOnlyList<SpawnMethodShape> methods)
      {
         int best = -1;
         if (methods == null) return best;

         for (int i = 0; i < methods.Count; i++)
         {
            if (!Fits(methods[i])) continue;
            if (best < 0 || methods[i].ParameterTypes.Count < methods[best].ParameterTypes.Count) best = i;
         }

         return best;
      }

      private static bool Fits(SpawnMethodShape m)
      {
         if (m == null || m.Name != MethodName || !m.IsPublic || m.IsStatic) return false;
         if (m.ParameterTypes == null || m.ParameterOptional == null || m.ParameterTypes.Count == 0) return false;
         if (m.ParameterOptional.Count != m.ParameterTypes.Count || m.ParameterTypes[0] != BuildDataType) return false;

         for (int i = 1; i < m.ParameterOptional.Count; i++)
            if (!m.ParameterOptional[i]) return false;

         return true;
      }
   }
}
