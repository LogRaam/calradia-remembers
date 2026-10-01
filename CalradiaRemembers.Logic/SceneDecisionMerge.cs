// Code written by Gabriel Mailhot, 30/09/2026.
// Lufaine and Maifailun (Nexus, 30/09/2026): asked for privacy (by the button or in words), the character agreed and
// emoted a departure, and the witnesses stayed. In the two-call chat the action interpreter alone supplied the turn's
// actions and the scene model's own [ACTION] blocks were dropped; but it is the SCENE model that is taught to answer a
// privacy request (request_privacy, accepted or refused, decided in character), and the interpreter is told not to
// record a privacy that is only implied. So the decision about the scene is taken from the character who made it
// when the interpreter recorded none. Deeds stay the interpreter's: that is the two-call design.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Merges the scene model's decisions about the scene into the interpreter's actions. Pure.</summary>
    public static class SceneDecisionMerge
    {
        /// <summary>The decisions about the scene that the character who makes them is taught to record himself.</summary>
        public static readonly IReadOnlyList<string> SceneDecisions = new[] {"request_privacy"};

        /// <summary>
        ///   The interpreter's actions, plus each scene decision the scene model recorded and the interpreter did not.
        ///   Never adds a deed.
        /// </summary>
        public static List<GameAction> Merge(IReadOnlyList<GameAction> interpreted, IReadOnlyList<GameAction> prose)
        {
            List<GameAction> merged = (interpreted ?? new List<GameAction>()).Where(a => a != null).ToList();

            foreach (string decision in SceneDecisions)
            {
                if (merged.Any(a => string.Equals(a.Type, decision, StringComparison.OrdinalIgnoreCase))) continue;

                GameAction fromScene = (prose ?? new List<GameAction>())
                    .FirstOrDefault(a => a != null && string.Equals(a.Type, decision, StringComparison.OrdinalIgnoreCase));
                if (fromScene != null) merged.Add(fromScene);
            }

            return merged;
        }
    }
}
