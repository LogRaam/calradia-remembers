// Code written by Gabriel Mailhot, 26/05/2026.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers
{
    /// <summary>
    ///   Mod-wide static settings. Sprint 8.2 keeps this simple: hard-coded
    ///   values that a future sprint (mod options UI) will replace with a
    ///   proper settings menu.
    ///   To change the adult content level for testing, edit
    ///   <see cref="AdultContentLevel" /> below.
    /// </summary>
    public static class ModSettings
    {
        /// <summary>
        ///   How much of the romantic profile is injected into the LLM system
        ///   prompt. Default is <see cref="NpcMemoryService.Core.Models.AdultContentLevel.Mature" /> — safe
        ///   for general play, allows romance and attraction in dialogue but
        ///   nothing explicit.
        /// </summary>
        public static AdultContentLevel AdultContentLevel { get; set; }
            = AdultContentLevel.Hardcore;
    }
}