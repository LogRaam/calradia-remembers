// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence (ratified 2026-07-2x): the holder "WEARS the most advantageous pieces
// and keeps the remainder in his inventory" (Gabriel's brief). This is that rule, made deterministic and
// pure: for each equipment slot, pick the best held item by ItemObject.Tier, tie-broken by Value, tie-broken
// again by a fixed ordinal key so the outcome never depends on collection ordering or randomness (mirrors
// NemesisRosterPolicy.FindRecurring's own tie-break philosophy). Everything not picked for a slot is the
// "remainder", and it must never be dropped: the ratified rule is explicit that gear is NEVER lost forever,
// so SelectUnwornRemainder + SelectWorn together must always account for every item in the pool.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Captivity
{
    /// <summary>Pure decisions over an accumulated pool of <see cref="HeldItem" />s: what merges in across
    /// escapes, and which single item per slot the holder wears.</summary>
    public static class CaptiveGearStockPolicy
    {
        /// <summary>
        ///   Additive merge of a holder's existing stock with a freshly-taken haul: the player may escape
        ///   (and be re-caught, and flee again) more than once, so accumulation must never replace the prior
        ///   stock, only grow it. Null lists are treated as empty so a holder's first-ever escape (no prior
        ///   stock) and a defensive call both degrade safely rather than throwing.
        /// </summary>
        public static IReadOnlyList<HeldItem> Merge(IReadOnlyList<HeldItem> existing, IReadOnlyList<HeldItem> incoming)
        {
            var merged = new List<HeldItem>();
            if (existing != null) merged.AddRange(existing);
            if (incoming != null) merged.AddRange(incoming);

            return merged;
        }

        /// <summary>
        ///   For each slot present anywhere in <paramref name="pool" />, the single best item to wear:
        ///   highest Tier first, then highest Value, then a fixed ordinal key (ItemStringId, then
        ///   ModifierStringId) so two otherwise-identical candidates resolve the SAME way regardless of the
        ///   pool's iteration order. Slots with no candidate are simply absent from the result (the holder
        ///   keeps whatever he already wore there). The mount slot (Horse) and its harness are two
        ///   INDEPENDENT slots, resolved exactly like any other: no special-casing needed or wanted.
        /// </summary>
        public static IReadOnlyDictionary<HeldEquipmentSlot, HeldItem> SelectWorn(IEnumerable<HeldItem> pool)
        {
            var worn = new Dictionary<HeldEquipmentSlot, HeldItem>();
            if (pool == null) return worn;

            foreach (IGrouping<HeldEquipmentSlot, HeldItem> bySlot in pool.Where(i => i != null).GroupBy(i => i.Slot))
            {
                HeldItem best = bySlot
                    .OrderByDescending(i => i.Tier)
                    .ThenByDescending(i => i.Value)
                    .ThenBy(i => i.ItemStringId ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(i => i.ModifierStringId ?? string.Empty, StringComparer.Ordinal)
                    .First();
                worn[bySlot.Key] = best;
            }

            return worn;
        }

        /// <summary>
        ///   Every item in <paramref name="pool" /> NOT chosen by <see cref="SelectWorn" />: the remainder
        ///   that "stays in his inventory" per the brief. Matched by REFERENCE, not by field equality, so two
        ///   accumulated duplicates of the same item+modifier (escaped twice with the same sword) are
        ///   correctly kept as two distinct entries, one worn and one still in the remainder, rather than
        ///   the duplicate silently vanishing.
        /// </summary>
        public static IReadOnlyList<HeldItem> SelectUnwornRemainder(IEnumerable<HeldItem> pool)
        {
            if (pool == null) return new List<HeldItem>();

            List<HeldItem> poolList = pool.Where(i => i != null).ToList();
            var wornInstances = new HashSet<HeldItem>(SelectWorn(poolList).Values); // reference equality (no Equals override)

            return poolList.Where(i => !wornInstances.Contains(i)).ToList();
        }
    }
}
