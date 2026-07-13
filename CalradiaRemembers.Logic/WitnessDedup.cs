// Code written by Gabriel Mailhot, 12/07/2026.
// The rule for "these two witness entries are the same person", so a witness added from two sources (an
// auto-resolved lord AND a scene companion, or a companion you also "Bring") is never listed twice. Pure so
// the identity rule stays tested: a hero is matched by StringId; two heroless (synthetic) witnesses by name.

#region

using System;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether two <see cref="WitnessEntry" /> values denote the same present witness.</summary>
    public static class WitnessDedup
    {
        /// <summary>
        ///   True when <paramref name="candidate" /> is already represented by <paramref name="existing" />.
        ///   A witness that carries a <see cref="WitnessEntry.HeroStringId" /> matches only another entry with
        ///   the SAME id (so a hero and a same-named synthetic bystander stay distinct); two heroless entries
        ///   match on name.
        /// </summary>
        public static bool IsSame(WitnessEntry existing, WitnessEntry candidate)
        {
            if (existing == null || candidate == null) return false;

            bool candHasId = !string.IsNullOrEmpty(candidate.HeroStringId);
            bool existingHasId = !string.IsNullOrEmpty(existing.HeroStringId);

            if (candHasId || existingHasId)
                return candHasId && existingHasId
                    && string.Equals(candidate.HeroStringId, existing.HeroStringId, StringComparison.OrdinalIgnoreCase);

            return string.Equals(candidate.Name, existing.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
