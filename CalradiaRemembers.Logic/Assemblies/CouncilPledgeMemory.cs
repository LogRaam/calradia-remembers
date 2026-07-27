// Code written by Gabriel Mailhot, 27/07/2026.
// Council audit M7: the first-person memory a council member keeps of a pledge they made at the table, whatever
// became of it. The council's whole point is that a promise is PUBLIC and remembered; before this, only a KEPT
// pledge (which became a real quest, and so got a quest memory) left any trace, so a withdrawn or failed pledge
// vanished as if it had never been spoken. Kept out of the mod so the wording is documented by tests.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>What became of a council pledge, for the memory the promiser keeps of it.</summary>
    public enum CouncilPledgeOutcome
    {
        /// <summary>Carried out for real at the lift.</summary>
        Kept,

        /// <summary>Recorded, but could not be carried out at the lift.</summary>
        Failed,

        /// <summary>Taken back during the sitting, before it was sealed.</summary>
        Withdrawn
    }

    /// <summary>Builds the memory line a council member stores about a pledge they made.</summary>
    public static class CouncilPledgeMemory
    {
        /// <summary>
        ///   The member's own first-person memory of the pledge (matching the voice of every other stored
        ///   memory). The recorded <paramref name="detail" /> is carried after a colon rather than folded into
        ///   the sentence: it is phrased about the member in the third person ("Ira will ride to Pravend..."),
        ///   which would read wrong inside a first-person clause. A blank detail falls back to a neutral phrase.
        /// </summary>
        public static string Summary(string detail, CouncilPledgeOutcome outcome)
        {
            string pledge = string.IsNullOrWhiteSpace(detail) ? "a matter put before the table" : detail.Trim();

            switch (outcome)
            {
                case CouncilPledgeOutcome.Kept:
                    return $"I made good on a pledge I gave before the council: {pledge}";
                case CouncilPledgeOutcome.Withdrawn:
                    return $"I took back a pledge I had made before the council: {pledge}";
                default: // Failed
                    return $"A pledge I gave before the council came to nothing: {pledge}";
            }
        }
    }
}
