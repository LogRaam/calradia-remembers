// Code written by Gabriel Mailhot, 11/11/2026.
// What a lord knows of the army he marches in: who leads it, which lords answered the summons, how many men each brought,
// and how many there are in all. Schmidt (Discord, 11/11/2026): "Lords know which lords they invited to their army when
// they form a grand army, as well as the army size of the lords and the total number of men in the grand army." The
// prompt told a leader his army only as a band ("several hundred soldiers") and a member only his own party, so asked who
// marched with him the model invented names. Counts are told as a captain reckons them (ForcesInSight.MenWords), never the
// exact roster figure. Pure; the host reads the army.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.War
{
    /// <summary>One party in an army.</summary>
    public sealed class ArmyRosterParty
    {
        /// <summary>The lord leading the party, as the prompt names him.</summary>
        public string Lord { get; init; }

        /// <summary>The men in the party now.</summary>
        public int Men { get; init; }

        /// <summary>True for the party of the character being spoken to.</summary>
        public bool IsSpeaker { get; init; }

        /// <summary>True for the party of the player, who marches in this army too.</summary>
        public bool IsPlayer { get; init; }
    }

    /// <summary>The army a character marches in, as the host read it.</summary>
    public sealed class ArmyRosterFacts
    {
        /// <summary>The leader's party first, then the others in the game's order.</summary>
        public IReadOnlyList<ArmyRosterParty> Parties { get; init; }
    }

    /// <summary>Renders what a lord knows of his own army.</summary>
    public static class ArmyRoster
    {
        /// <summary>
        ///   The most parties named besides the leader's. A grand army of the late game can count twenty parties; the line
        ///   names the largest and counts the rest, whose men still count in the total.
        /// </summary>
        public const int MaxNamed = 10;

        /// <summary>
        ///   The lines the prompt prints, the leader's first, then the total. Empty when the character marches in no army of
        ///   two parties or more (a lone party is already told as the character's own command).
        /// </summary>
        public static IReadOnlyList<string> Describe(ArmyRosterFacts facts)
        {
            List<ArmyRosterParty> parties = facts?.Parties?.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Lord)).ToList() ?? new List<ArmyRosterParty>();
            if (parties.Count < 2) return new List<string>();

            ArmyRosterParty leader = parties[0];
            List<ArmyRosterParty> others = parties.Skip(1).OrderByDescending(p => p.Men).ToList();

            var lines = new List<string> {
                leader.IsSpeaker ? $"You lead it, with {ForcesInSight.MenWords(leader.Men)} in your own party." : $"Led by {Who(leader)}, with {ForcesInSight.MenWords(leader.Men)} in their own party."
            };
            foreach (ArmyRosterParty p in others.Take(MaxNamed))
                lines.Add($"{Who(p)}, {ForcesInSight.MenWords(p.Men)}.");

            int unnamed = others.Count - MaxNamed;
            if (unnamed > 0)
                lines.Add($"And {unnamed} lesser part{(unnamed == 1 ? "y" : "ies")}.");

            lines.Add($"In all, {ForcesInSight.MenWords(parties.Sum(p => p.Men))} in {parties.Count} parties.");

            return lines;
        }

        private static string Who(ArmyRosterParty p) => p.IsSpeaker ? "you" : p.IsPlayer ? $"{p.Lord} (the player)" : p.Lord;
    }
}
