// Code written by Gabriel Mailhot, 07/09/2026.
// Console entry for the throwaway map-tick spike (see CrMapTickSpike): measures whether pushing a game state, the
// way vanilla opens the clan screen, actually silences the live map underneath, where pushing a screen does not.

#region

using System.Collections.Generic;
using CalradiaRemembers.UI.Spike;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.tick_probe [none|screen|state] [seconds]: opens the chosen host for a fixed window and reports how
        ///   many times the map kept ticking underneath it. Run all three: 'none' says what a live map ticks at,
        ///   'screen' is how CR hosts panels today, 'state' is the candidate. The verdict lands in the log.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("tick_probe", "cr")]
        public static string TickProbe(List<string> args)
        {
            string mode = args != null && args.Count > 0 ? args[0] : "none";
            float seconds = args != null && args.Count > 1 && float.TryParse(args[1], out float parsed) ? parsed : 4f;

            return CrMapTickSpike.Start(mode, seconds);
        }
    }
}
