// Code written by Gabriel Mailhot, 11/11/2026.
// What every character knows of the realms of Calradia: the fiefs each holds, and which have fallen.
//
// Schmidt (Discord, 11/11/2026): "NPC's know about the end of their realm and others, as well as the number of fiefs in
// the realm, as right now everyone acts surprised when I tell them that Sturgia is no more, and their last fief taken."
// The world-state line named the wars and nothing else, so a realm that lost its last fief simply vanished from what
// characters were told, and the model filled the gap with a Sturgia still standing. The fall of a realm is the talk of
// every hall; a character who has not heard of it is not ignorant, the mod failed to tell them.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One realm as the world-state line tells it.</summary>
    public sealed class RealmStanding
    {
        /// <summary>The realm's name as the game shows it ("Sturgia").</summary>
        public string Name { get; init; }

        /// <summary>Towns it holds now.</summary>
        public int Towns { get; init; }

        /// <summary>Castles it holds now.</summary>
        public int Castles { get; init; }

        /// <summary>True when the game counts the realm destroyed: it holds nothing and is no longer a power.</summary>
        public bool HasFallen { get; init; }
    }

    /// <summary>Renders the realms line of the world state. Pure and engine-free.</summary>
    public static class RealmStandingFormatter
    {
        /// <summary>
        ///   The most fallen realms named. A long campaign can see many short-lived rebel realms rise and fall; the line
        ///   names the first ones the game lists (the great realms, in vanilla) and counts the rest.
        /// </summary>
        public const int MaxFallenNamed = 6;

        /// <summary>
        ///   "Vlandia holds 6 towns and 10 castles; Battania holds 4 towns and 5 castles. Fallen, holding nothing: Sturgia."
        ///   Empty when there is no realm to tell, which the prompt treats as saying nothing.
        /// </summary>
        public static string Describe(IEnumerable<RealmStanding> realms)
        {
            List<RealmStanding> all = (realms ?? Enumerable.Empty<RealmStanding>()).Where(r => !string.IsNullOrWhiteSpace(r?.Name)).ToList();

            List<string> standing = all.Where(r => !r.HasFallen).Select(r => $"{r.Name} holds {Fiefs(r)}").ToList();
            List<string> fallen = all.Where(r => r.HasFallen).Select(r => r.Name).ToList();

            var parts = new List<string>();
            if (standing.Count > 0) parts.Add(string.Join("; ", standing) + ".");
            if (fallen.Count > 0)
            {
                string named = string.Join(", ", fallen.Take(MaxFallenNamed));
                int more = fallen.Count - MaxFallenNamed;
                parts.Add($"Fallen, holding nothing and no longer a power: {named}{(more > 0 ? $" and {more} other{(more == 1 ? "" : "s")}" : "")}.");
            }

            return string.Join(" ", parts);
        }

        private static string Fiefs(RealmStanding r)
        {
            if (r.Towns <= 0 && r.Castles <= 0) return "no town or castle";

            var held = new List<string>();
            if (r.Towns > 0) held.Add($"{r.Towns} town{(r.Towns == 1 ? "" : "s")}");
            if (r.Castles > 0) held.Add($"{r.Castles} castle{(r.Castles == 1 ? "" : "s")}");

            return string.Join(" and ", held);
        }
    }
}
