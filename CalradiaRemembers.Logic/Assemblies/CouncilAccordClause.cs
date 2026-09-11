// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 2: one-line clauses describing a STILL-STANDING council-born accord between an NPC and the player,
// written from the NPC's own point of view (the 1:1 system prompt is "you are {NPC}"), for injection so the NPC can
// reference the accord in ordinary conversation ("the tribute I owe you"). Pure: the caller (EncounterContextBuilder)
// resolves the live names/amounts/days from each accord behavior and passes them in, so the wording, and above all
// the DIRECTION of each accord (who owes whom), is unit-tested rather than guessed at in game. A wrong direction
// (an NPC told it RECEIVES a tribute it actually PAYS) would have the character contradict the real ledger.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Renders a standing council accord as a first-person (NPC POV) clause for the 1:1 prompt.</summary>
    public static class CouncilAccordClause
    {
        /// <summary>
        ///   A daily tribute this NPC owes the player. THREE INTS in a row, all day- or coin-counts, and the
        ///   sentence prints them straight to the player: swap the rate with the days and the prompt tells a
        ///   lord he owes thirty denars for twelve days when he owes twelve for thirty.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class TributeToPlayer
        {
           /// <summary>How the player is named in the clause.</summary>
           public string PlayerName { get; set; }

           /// <summary>Denars owed each day.</summary>
           public int AmountPerDay { get; set; }

           /// <summary>Days the tribute still has to run.</summary>
           public int DaysRemaining { get; set; }

           /// <summary>Days already fallen behind. Above zero adds the note that they have.</summary>
           public int MissedDays { get; set; }
        }

        /// <summary>A tribute THIS NPC pays the player daily (CouncilTribute). MissedDays &gt; 0 notes they have fallen behind.</summary>
        public static string TributeOwedToPlayer(TributeToPlayer facts)
        {
           if (facts == null) return "";

           string playerName = facts.PlayerName;
           int amountPerDay = facts.AmountPerDay;
           int daysRemaining = facts.DaysRemaining;
           int missedDays = facts.MissedDays;

            string behind = missedDays > 0 ? " You have lately fallen behind on it." : "";

            return $"You owe {Player(playerName)} a tribute of {amountPerDay} denars a day, sworn at a parley{DaysSuffix(daysRemaining)}.{behind}";
        }

        /// <summary>A stipend the player granted THIS NPC daily (CouncilStipend), funded from the player's own purse.</summary>
        public static string StipendGrantedByPlayer(string playerName, int amountPerDay, int daysRemaining)
            => $"{Player(playerName)} grants you a stipend of {amountPerDay} denars a day, settled at their council{DaysSuffix(daysRemaining)}.";

        /// <summary>A non-aggression pact binding THIS NPC's realm and the player's (CouncilPact), sworn at a parley.</summary>
        public static string PactWithPlayer(string playerName, int daysRemaining)
            => $"A non-aggression pact stands between your house and {Player(playerName)}'s, sworn at a parley{DaysSuffix(daysRemaining)}.";

        /// <summary>THIS NPC is the hostage the player holds as a pledge of their house's good faith (CouncilHostage).</summary>
        public static string HeldAsHostageByPlayer(string playerName, int daysRemaining)
            => $"You are held by {Player(playerName)} as a hostage of your house's good faith, given at a parley{DaysSuffix(daysRemaining)}. Should your house take arms against them, you are forfeit to their dungeon.";

        /// <summary>THIS NPC's house gave a hostage to the player (CouncilHostage), named where known.</summary>
        public static string HostageGivenToPlayer(string playerName, string hostageName, int daysRemaining)
        {
            string h = string.IsNullOrWhiteSpace(hostageName) ? "one of your kin" : hostageName.Trim();

            return $"Your house has given {h} to {Player(playerName)} as a hostage of good faith, sworn at a parley{DaysSuffix(daysRemaining)}. Break faith and take arms against them, and {h} is forfeit to their dungeon.";
        }

        #region private

        private static string Player(string playerName)
            => string.IsNullOrWhiteSpace(playerName) ? "the player" : playerName.Trim();

        /// <summary>The parenthetical remaining-term note, singular-aware, or empty when the term is spent or unknown.</summary>
        private static string DaysSuffix(int daysRemaining)
        {
            if (daysRemaining <= 0) return "";

            return daysRemaining == 1 ? " (1 day remains)" : $" ({daysRemaining} days remain)";
        }

        #endregion
    }
}
