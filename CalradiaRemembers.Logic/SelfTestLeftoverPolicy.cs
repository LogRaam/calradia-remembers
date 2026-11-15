// Code written by Gabriel Mailhot, 15/11/2026.
// Gabriel's play test of 15/11/2026 crashed on a hero cr.test all had left in the player's prison: one test captured a
// nemesis and kept him, the next captured him again (the roster then held him twice), and the leftover copy of the
// executed hero came back to life when the player's prisoners were freed. Nothing noticed the prisoner left behind.
// Gabriel: "tu as un autre garde-fou a mettre en place ou c'est deja fait?". Each test's prison is now compared before
// and after it runs, and a test that leaves a captive behind fails, naming him.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What a self-test left in the player's prison that was not there before it ran.</summary>
    public static class SelfTestLeftoverPolicy
    {
        /// <summary>
        ///   Each captive held more times after the test than before ("Nabb (x2)" when held twice), in name order. A captive
        ///   the test freed, or one already held before it, is no leftover: the player's own prisoners are not the test's.
        /// </summary>
        /// <param name="before">How many times each captive (by a label naming him) was held before the test.</param>
        /// <param name="after">The same, after the test.</param>
        public static IReadOnlyList<string> Leftovers(IReadOnlyDictionary<string, int> before, IReadOnlyDictionary<string, int> after)
        {
            return (after ?? new Dictionary<string, int>())
                   .Where(a => a.Value > (before != null && before.TryGetValue(a.Key, out int had) ? had : 0))
                   .OrderBy(a => a.Key, System.StringComparer.Ordinal)
                   .Select(a => a.Value > 1 ? $"{a.Key} (x{a.Value})" : a.Key)
                   .ToList();
        }
    }
}
