// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod (Sprint 24 jealousy, v1.5 competition loop): the rule that picks WHICH
// partner in a sharing household feels slighted enough to react. The thresholds are subtle and
// relative ("neglected, AND neglected MORE than the most-attended partner") — they break in silence.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One partner's standing as the competition pass sees it (engine-gathered, pure to reason over).</summary>
    public sealed class PartnerStanding
    {
        /// <summary>The partner's stable id.</summary>
        public string PartnerId { get; set; }

        /// <summary>True when this partner's culture/rank accepts a shared partner (monogamous partners don't merely "compete").</summary>
        public bool ToleratesSharing { get; set; }

        /// <summary>Days since the player last attended to this partner (0 = riding with the player).</summary>
        public int NeglectDays { get; set; }

        /// <summary>True when a competition reaction for this partner fired too recently to repeat.</summary>
        public bool OnCooldown { get; set; }
    }

    /// <summary>
    ///   Picks at most ONE partner to feel competition jealousy on a given pass. A partner reacts only
    ///   when they tolerate sharing (else it is betrayal, handled elsewhere), have been neglected past
    ///   an absolute floor, AND are neglected markedly more than whoever the player attends most — an
    ///   even, if thin, distribution of attention is no slight. Among those, the most-neglected wins.
    /// </summary>
    public static class CompetitionSelector
    {
        /// <summary>Unseen at least this long to count as neglected at all.</summary>
        public const int NeglectThresholdDays = 14;

        /// <summary>...AND neglected at least this much more than the most-attended partner, to feel singled out.</summary>
        public const int InequityGapDays = 8;

        /// <summary>
        ///   The id of the single most-aggrieved sharing-culture partner with a genuine, relative
        ///   slight, or null when no one qualifies (too few partners, all attended, attention shared
        ///   evenly, or everyone eligible is on cooldown).
        /// </summary>
        public static string SelectAggrieved(IReadOnlyList<PartnerStanding> partners)
        {
            if (partners == null || partners.Count < 2) return null; // no rivalry without a household of several

            int mostAttended = partners.Min(p => p.NeglectDays);

            string worst = null;
            int worstNeglect = -1;
            foreach (PartnerStanding p in partners)
            {
                if (p == null || !p.ToleratesSharing) continue;
                if (p.NeglectDays < NeglectThresholdDays) continue;                 // still being seen
                if (p.NeglectDays - mostAttended < InequityGapDays) continue;       // everyone equally (un)attended — no slight
                if (p.OnCooldown) continue;
                if (p.NeglectDays > worstNeglect)
                {
                    worstNeglect = p.NeglectDays;
                    worst = p.PartnerId;
                }
            }
            return worst;
        }
    }
}
