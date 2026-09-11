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
        ///   How the deceased predecessor stood to the heir. FOUR booleans in a row where the first three are
        ///   asked in PRIORITY order (parent, then sibling, then spouse) and the fourth is a different kind of
        ///   fact entirely: it does not choose the relation, it chooses the word's gender.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class PredecessorFacts
        {
           /// <summary>The predecessor was the heir's parent. Checked first.</summary>
           public bool WasParent { get; set; }

           /// <summary>The predecessor was the heir's sibling. Checked next.</summary>
           public bool WasSibling { get; set; }

           /// <summary>The predecessor was the heir's spouse. Checked last.</summary>
           public bool WasSpouse { get; set; }

           /// <summary>
           ///   The predecessor was a woman, which picks mother over father and sister over brother. It never
           ///   decides WHICH relation is named, only how that relation is spelt.
           /// </summary>
           public bool IsFemale { get; set; }
        }

        /// <summary>
        ///   The kinship word for the deceased predecessor as seen from the heir: "father"/"mother"
        ///   when the predecessor was the heir's parent, "brother"/"sister" for a sibling, "spouse" for
        ///   a widow(er), else a generic "kin". Parent precedes sibling precedes spouse when several hold.
        /// </summary>
        public static string Label(PredecessorFacts facts)
        {
           if (facts == null) return "kin";

           bool predecessorWasParent = facts.WasParent;
           bool predecessorWasSibling = facts.WasSibling;
           bool predecessorWasSpouse = facts.WasSpouse;
           bool predecessorIsFemale = facts.IsFemale;

            if (predecessorWasParent)  return predecessorIsFemale ? "mother" : "father";
            if (predecessorWasSibling) return predecessorIsFemale ? "sister" : "brother";
            if (predecessorWasSpouse)  return "spouse";
            return "kin";
        }
    }
}
