// Code written by Gabriel Mailhot, 15/09/2026.
// The wars list stops repeating the same kingdom once per enemy.
//
// The world-state line named every war as its own pair - "Vlandia vs Battania; Vlandia vs Sturgia;
// Vlandia vs the Aserai" - so a kingdom at war with five neighbours wrote its own name five times. In
// vanilla that is untidy. fkasad (Nexus, 15/09/2026) plays a total conversion with far more factions,
// where the same list grows by about half again for nothing: "A 50% gain in size for this wars list in
// my case."
//
// Same pairs, same order, same information - the repeated name is written once. Nothing about who is at
// war with whom changes, which is why this is a formatting policy and not a world-state change.

#region

using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Renders the active-wars line, collapsing a run of wars that share a side onto one name. Pure and
    ///   engine-free: the caller hands in the pairs, in the order they should be read.
    /// </summary>
    public static class ActiveConflictsFormatter
    {
        /// <summary>
        ///   "Vlandia vs Battania, Sturgia; the Aserai vs the Khuzait" — one entry per side that opens a
        ///   run, its enemies joined after it. Empty when there are no wars, which is what the prompt
        ///   treats as "say nothing about wars at all".
        /// </summary>
        public static string Describe(IEnumerable<(string Side, string Enemy)> wars) => Describe(wars, null);

        /// <summary>
        ///   As <see cref="Describe(IEnumerable{(string Side, string Enemy)})" />, the wars of
        ///   <paramref name="playersRealm" /> first and told from its side (fkasad, Nexus, 05/10/2026: in a long list the
        ///   player's own wars lost their weight in the middle). Null, or a realm at peace, leaves the order as given.
        /// </summary>
        public static string Describe(IEnumerable<(string Side, string Enemy)> wars, string playersRealm)
        {
            if (wars == null) return "";
            if (!string.IsNullOrWhiteSpace(playersRealm))
            {
                var all = new List<(string Side, string Enemy)>(wars);
                var own = new List<(string Side, string Enemy)>();
                var rest = new List<(string Side, string Enemy)>();
                foreach ((string side, string enemy) in all)
                    if (side == playersRealm) own.Add((side, enemy));
                    else if (enemy == playersRealm) own.Add((enemy, side));
                    else rest.Add((side, enemy));
                own.AddRange(rest);
                wars = own;
            }

            var entries = new StringBuilder();
            string openSide = null;

            foreach ((string side, string enemy) in wars)
            {
                if (string.IsNullOrWhiteSpace(side) || string.IsNullOrWhiteSpace(enemy)) continue;

                // Only a CONSECUTIVE run is collapsed, so the reading order the caller chose is preserved
                // exactly. The engine emits all of one side's wars together, which is why this is enough.
                if (side == openSide)
                {
                    entries.Append(", ").Append(enemy);

                    continue;
                }

                if (openSide != null) entries.Append("; ");

                entries.Append(side).Append(" vs ").Append(enemy);
                openSide = side;
            }

            return entries.ToString();
        }
    }
}
