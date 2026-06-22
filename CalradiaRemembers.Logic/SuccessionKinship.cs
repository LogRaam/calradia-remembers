// Code written by Gabriel Mailhot, 22/06/2026.
// Dynastic succession: the word an NPC uses for the late predecessor relative to the heir now before
// them ("your father helped me at the siege…"). Pure so the label mapping stays tested; the engine
// supplies the family facts, this turns them into the kinship word.

namespace CalradiaRemembers.Logic
{
    /// <summary>Maps a predecessor's family relation to the heir into the word an NPC would use.</summary>
    public static class SuccessionKinship
    {
        /// <summary>
        ///   The kinship word for the deceased predecessor as seen from the heir: "father"/"mother"
        ///   when the predecessor was the heir's parent, "brother"/"sister" for a sibling, "spouse" for
        ///   a widow(er), else a generic "kin". Parent precedes sibling precedes spouse when several hold.
        /// </summary>
        public static string Label(
            bool predecessorWasParent,
            bool predecessorWasSibling,
            bool predecessorWasSpouse,
            bool predecessorIsFemale)
        {
            if (predecessorWasParent)  return predecessorIsFemale ? "mother" : "father";
            if (predecessorWasSibling) return predecessorIsFemale ? "sister" : "brother";
            if (predecessorWasSpouse)  return "spouse";
            return "kin";
        }
    }
}
