// Code written by Gabriel Mailhot, 06/09/2026.
// Battle-memory enrichment, Inc 2: turns a whole battle's ledger into the FEW things an NPC would actually bring
// up. A battle can leave dozens of entries; a memory that recited them all would be a casualty list, not a
// recollection, and would bloat every profile in the save. So this picks at most one standout deed, the mirror
// deed (who put the player down, which is the seed of a rivalry or a grudge), and a rank-and-file toll only when
// it is genuinely worth remarking on. Pure, so what an NPC chooses to remember stays testable and tunable.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Battles
{
    /// <summary>The few facts worth remembering out of one battle, chosen from the whole ledger.</summary>
    public sealed class BattleFeatDigest
    {
        public BattleFeatDigest(CombatFeat bestFeat, string downedThePlayer, int tollToMention)
        {
            BestFeat = bestFeat;
            DownedThePlayer = downedThePlayer;
            TollToMention = tollToMention;
        }

        /// <summary>The single deed worth naming, or null when nothing the player did rose above a scuffle.</summary>
        public CombatFeat BestFeat { get; }

        /// <summary>The name of the foe who put the player down, or null when the player stayed on their feet.</summary>
        public string DownedThePlayer { get; }

        /// <summary>The rank-and-file toll worth mentioning, or 0 when it was an ordinary day's fighting.</summary>
        public int TollToMention { get; }

        /// <summary>True when the battle left nothing worth adding beyond the ordinary "we fought together" line.</summary>
        public bool IsEmpty => BestFeat == null && string.IsNullOrEmpty(DownedThePlayer) && TollToMention <= 0;
    }

    /// <summary>Chooses the few facts from a battle that an NPC would actually raise.</summary>
    public static class BattleFeatDistiller
    {
        /// <summary>
        ///   How many rank-and-file the player must fell by their own hand before it is worth a word. Below this it
        ///   is an ordinary day's fighting, and an NPC reciting a small number would read as a statistic. Tuning.
        /// </summary>
        public const int RemarkableTollThreshold = 15;

        /// <summary>
        ///   The standout deed, the mirror deed, and a remarkable toll. An <see cref="CombatFeatTier.Unremarkable" />
        ///   feat is dropped entirely (beating someone far beneath you is not a memory). Among the rest the highest
        ///   tier wins, and on a tie the FIRST recorded wins, so the choice is stable rather than depending on
        ///   enumeration order.
        /// </summary>
        public static BattleFeatDigest Distil(IEnumerable<CombatFeat> feats, string downedThePlayer, int troopsFelled)
        {
            CombatFeat best = null;

            if (feats != null)
                foreach (CombatFeat feat in feats)
                {
                    if (feat == null || feat.Tier == CombatFeatTier.Unremarkable) continue;
                    if (best == null || feat.Tier > best.Tier) best = feat;
                }

            int toll = troopsFelled >= RemarkableTollThreshold ? troopsFelled : 0;

            return new BattleFeatDigest(best, string.IsNullOrWhiteSpace(downedThePlayer) ? null : downedThePlayer.Trim(), toll);
        }
    }
}
