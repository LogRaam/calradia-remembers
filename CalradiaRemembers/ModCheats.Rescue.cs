// Code written by Gabriel Mailhot, 11/07/2026.

#region

using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.rescue [name]
        ///   Simulates freeing the named lord (or the conversation NPC) by a public siege: the lord and their
        ///   family record a grateful memory and warm toward you, a few close friends warm too, and the captor
        ///   rolls to discover it and hold a grudge. Bypasses battle detection and the once-per-lord gate.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("rescue", "cr")]
        public static string ForceRescue(List<string> args)
        {
            if (!TryContext(out Behaviors.NpcMemoryCampaignBehavior _, out string error)) return error;
            if (Rescue.RescueBehavior.Instance == null) return "RescueBehavior not active (start or load a campaign first).";

            Hero lord = ResolveHero(args);
            if (lord == null) return "No NPC found. Pass a name fragment or start a conversation first.";

            return Rescue.RescueBehavior.Instance.ForceRescueForTest(lord);
        }
    }
}
