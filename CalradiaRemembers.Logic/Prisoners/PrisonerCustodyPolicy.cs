// Code written by Gabriel Mailhot, 30/08/2026.
// Prisoner custody, pure core: decides which prisons count as the PLAYER's and merges the captives they hold
// into one deduped roster. Player report: a ransom-offer letter's Accept button vanished, and a captive could
// not be sold in conversation, once the captured lord was moved from the travelling party to the player's own
// town dungeon, because the enumeration only ever looked at the main party. This isolates the load-bearing
// rule (a prison is the player's iff it is their own main party OR a settlement their clan owns) behind a seam
// the engine feeds, so it can be unit-tested and read as documentation without a running campaign.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Prisoners
{
    /// <summary>One prison the game holds captives in, reduced to the plain facts custody scoring needs.</summary>
    public readonly struct PrisonHolding
    {
        public PrisonHolding(bool isPlayerMainParty, bool isSettlementOwnedByPlayerClan, IReadOnlyList<string> aliveHeroPrisonerIds)
        {
            IsPlayerMainParty = isPlayerMainParty;
            IsSettlementOwnedByPlayerClan = isSettlementOwnedByPlayerClan;
            AliveHeroPrisonerIds = aliveHeroPrisonerIds;
        }

        /// <summary>True when this prison is the player's own travelling main party.</summary>
        public bool IsPlayerMainParty { get; }

        /// <summary>True when this prison is a settlement (town/castle dungeon) the player's own clan owns.</summary>
        public bool IsSettlementOwnedByPlayerClan { get; }

        /// <summary>The StringIds of every alive hero captive in this prison (may be null/empty).</summary>
        public IReadOnlyList<string> AliveHeroPrisonerIds { get; }
    }

    /// <summary>Pure, engine-free custody rules: which prisons are the player's, and the captives they hold.</summary>
    public static class PrisonerCustodyPolicy
    {
        /// <summary>
        ///   A prison is in the player's custody iff it is their own main party OR a settlement their clan owns.
        ///   A captive left behind bars in the player's own town is every bit as held as one in the party (the
        ///   ransom-letter bug); a prison the player does not own is never theirs to sell.
        /// </summary>
        public static bool IsPlayerCustody(bool isPlayerMainParty, bool isSettlementOwnedByPlayerClan)
            => isPlayerMainParty || isSettlementOwnedByPlayerClan;

        /// <summary>
        ///   The StringIds of every alive hero the player holds captive across all given prisons: only prisons in
        ///   the player's custody contribute, each hero appears once (deduped by id, in first-seen order), and
        ///   null/blank ids are dropped. Never null.
        /// </summary>
        public static List<string> HeldByPlayer(IEnumerable<PrisonHolding> prisons)
        {
            var result = new List<string>();
            var seen = new HashSet<string>();
            if (prisons == null) return result;

            foreach (PrisonHolding prison in prisons)
            {
                if (!IsPlayerCustody(prison.IsPlayerMainParty, prison.IsSettlementOwnedByPlayerClan)) continue;
                if (prison.AliveHeroPrisonerIds == null) continue;

                foreach (string id in prison.AliveHeroPrisonerIds)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (seen.Add(id)) result.Add(id);
                }
            }

            return result;
        }
    }
}
