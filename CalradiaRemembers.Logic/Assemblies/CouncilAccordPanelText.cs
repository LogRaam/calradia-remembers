// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 3: the same standing council accords CouncilAccordClause renders for the NPC's own prompt (first
// person), rendered instead in the THIRD person for the player reading this hero's Encyclopedia page ("Owes you a
// tribute of N a day"). Kept a separate pure describer from CouncilAccordClause on purpose: the point of view is
// the load-bearing difference, and mixing the two would sooner or later put "You owe..." on a page the PLAYER
// reads. Pure so the wording and the direction (who owes whom) are unit-tested, not eyeballed in the encyclopedia.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Renders a standing council accord as a third-person line for the hero's Encyclopedia page (player POV).</summary>
    public static class CouncilAccordPanelText
    {
        /// <summary>A tribute this hero pays the player daily (CouncilTribute). MissedDays &gt; 0 notes they have fallen behind.</summary>
        public static string TributeOwed(int amountPerDay, int daysRemaining, int missedDays)
        {
            string behind = missedDays > 0 ? " They have fallen behind on it lately." : "";

            return $"Owes you a tribute of {amountPerDay} denars a day, sworn at a parley{Days(daysRemaining)}.{behind}";
        }

        /// <summary>A stipend the player pays this hero daily (CouncilStipend).</summary>
        public static string StipendReceived(int amountPerDay, int daysRemaining)
            => $"Receives a stipend of {amountPerDay} denars a day from you, settled at a council{Days(daysRemaining)}.";

        /// <summary>A non-aggression pact binding this hero's realm and the player's (CouncilPact).</summary>
        public static string Pact(int daysRemaining)
            => $"Bound to you by a non-aggression pact, sworn at a parley{Days(daysRemaining)}.";

        /// <summary>This hero is the hostage the player holds (CouncilHostage).</summary>
        public static string HeldAsHostage(int daysRemaining)
            => $"Held by you as a hostage of their house's good faith, given at a parley{Days(daysRemaining)}. If their house takes arms against you, they are forfeit to your dungeon.";

        /// <summary>This hero's house gave a hostage to the player (CouncilHostage), named where known.</summary>
        public static string HostageGiven(string hostageName, int daysRemaining)
        {
            string h = string.IsNullOrWhiteSpace(hostageName) ? "one of their kin" : hostageName.Trim();

            return $"Gave {h} to you as a hostage of good faith, sworn at a parley{Days(daysRemaining)}. If their house breaks faith, {h} is forfeit to you.";
        }

        /// <summary>This hero is a ward the player's council sponsored, mentored by another clan member (CouncilWard).</summary>
        public static string WardAsProtege(string mentorName)
        {
            string m = string.IsNullOrWhiteSpace(mentorName) ? "one of your clan" : mentorName.Trim();

            return $"Fostered as a ward, mentored by {m}, sponsored at your council.";
        }

        /// <summary>This hero mentors a ward the player's council sponsored (CouncilWard).</summary>
        public static string WardAsMentor(string protegeName)
        {
            string p = string.IsNullOrWhiteSpace(protegeName) ? "a young companion" : protegeName.Trim();

            return $"Mentors {p} as a ward, sponsored at your council.";
        }

        /// <summary>The parenthetical remaining-term note, singular-aware, or empty when the term is spent or unknown (a ward has no term).</summary>
        private static string Days(int daysRemaining)
        {
            if (daysRemaining <= 0) return "";

            return daysRemaining == 1 ? " (1 day remains)" : $" ({daysRemaining} days remain)";
        }
    }
}
