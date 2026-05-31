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

        /// <summary>
        ///   Sound event path played when a negative relation change occurs.
        ///   Example: "event:/ui/notification/relation_decrease"
        /// </summary>
        /// <summary>
        ///   Sound event path played when a negative relation change occurs.
        ///   Vanilla fallback — replace with "event:/CR_Negative" once the custom
        ///   soundbank strings-loading issue is resolved in FMOD Studio.
        /// </summary>
        public static string RelationDecreaseSound { get; set; } = "event:/ui/persuasion/critical_fail";

        /// <summary>
        ///   Sound event path played when a positive relation change occurs.
        ///   Vanilla fallback — replace with "event:/CR_Positive" once the custom
        ///   soundbank strings-loading issue is resolved in FMOD Studio.
        /// </summary>
        public static string RelationIncreaseSound { get; set; } = "event:/ui/persuasion/success";
    }
}