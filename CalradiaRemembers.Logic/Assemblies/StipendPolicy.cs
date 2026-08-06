// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: grant_stipend's pure policy, the fifth executable resolution and the first
// that touches persisted save state (the mod's CouncilStipendBehavior escrow). A stipend is a RECURRING payment
// to a seated clan companion, FUNDED UP FRONT for a FIXED 30-day tranche (Kimi review, adopted as-is,
// COUNCIL_ACTIONS.md "grant_stipend": "the promise is financed to advance -> jamais de rupture silencieuse" -
// the promise is fully funded so it can never silently fail mid-term). The only variable the LLM proposes is the
// amount PER DAY, clamped here to [50, 500] denars so neither a token gesture nor a purse-breaking promise ever
// reaches the lift unchecked. TotalCost is the whole escrow the lift must deduct from the player's gold at once
// (the author's design: the promise can never fail partway through its term); CanAfford is the single gate both
// the offering (MinTrancheCost, the cheapest possible tranche, before the LLM has proposed any real amount) and
// the lift (the actual proposed amount, re-checked at settlement) read.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for grant_stipend: the amount clamp, the escrow cost, and the affordability gate.</summary>
    public static class StipendPolicy
    {
        /// <summary>A stipend always runs a FIXED tranche of 30 days (Kimi review, adopted as-is): the only variable is the amount per day.</summary>
        public const int TrancheDays = 30;

        public const int MinAmountPerDay = 50;
        public const int MaxAmountPerDay = 500;

        /// <summary>
        ///   The cheapest possible real tranche (30 days at the floor rate): the OFFERING's own affordability
        ///   floor, read before the LLM has proposed any amount (ResolutionOfferingResolver, ChatViewModel.Council's
        ///   PlayerCanAffordStipend). The lift re-checks the ACTUAL proposed amount separately via CanAfford.
        /// </summary>
        public const int MinTrancheCost = MinAmountPerDay * TrancheDays;

        /// <summary>Clamps a proposed daily amount into [<see cref="MinAmountPerDay" />, <see cref="MaxAmountPerDay" />].</summary>
        public static int ClampAmountPerDay(int amountPerDay)
        {
            if (amountPerDay < MinAmountPerDay) return MinAmountPerDay;
            if (amountPerDay > MaxAmountPerDay) return MaxAmountPerDay;

            return amountPerDay;
        }

        /// <summary>The whole escrow the lift must fund at once for a tranche at this daily rate: amountPerDay * <see cref="TrancheDays" />.</summary>
        public static int TotalCost(int amountPerDay) => amountPerDay * TrancheDays;

        /// <summary>True when the player's purse can cover the FULL tranche cost, never a partial fund.</summary>
        public static bool CanAfford(int playerGold, int amountPerDay) => playerGold >= TotalCost(amountPerDay);

        /// <summary>
        ///   Whether the 1:1 conversation path should TEACH this NPC the grant_stipend action this turn (2026-08-05,
        ///   grant_stipend brought to BOTH the council and 1:1 pipelines). It reduces the SAME eligibility the
        ///   council's own offering proves (ResolutionOfferingResolver / ChatViewModel.PlayerCanAffordStipend: a
        ///   seated companion is available AND the player can cover at least the cheapest tranche) to the three facts
        ///   the 1:1 grounding reads: the conversation NPC is one of the player's own clan companions (a stipend is a
        ///   clan-internal keep), NO stipend is already running for them (never double-escrow a second concurrent
        ///   tranche on top of one already funded, re-checked live at commit), and the player's purse can cover at
        ///   least <see cref="MinTrancheCost" /> (the floor, before the LLM has proposed any real amount). Pure and
        ///   total, mirroring GovernorAppointmentPolicy.CanOfferAppointment: the bridge re-checks the companion
        ///   status, the already-active guard, and the FULL proposed amount against the player's gold again at commit
        ///   time (CourtActionResolver.TryGrantStipend -> CouncilStipendBehavior.TryStart), so a true answer here
        ///   only ever OFFERS a teaching, it never funds a stipend.
        /// </summary>
        public static bool CanOfferStipend(bool npcIsPlayerCompanion, bool npcHasActiveStipend, bool playerCanAffordMinTranche)
            => npcIsPlayerCompanion && !npcHasActiveStipend && playerCanAffordMinTranche;
    }
}
