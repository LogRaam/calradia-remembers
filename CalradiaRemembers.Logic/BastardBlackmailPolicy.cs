// Code written by Gabriel Mailhot, 26/06/2026.
// Bastards (design layer) — the mother of a HIDDEN bastard may extort the player for her silence. Mirrors the
// Phase-3 intimacy-leverage pattern: she holds leverage only while the secret is still concealed; her demand
// scales with the player's wealth (she extracts what she thinks she can), within a band. Pure so the rule —
// and its tuning — stays protected by tests. The interaction layer (conversation vs letter, paying) is host-side.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   When the mother of a bastard can blackmail the player for her silence, and how much she demands.
    ///   Leverage lives only while the secret is hidden — once it is out, there is nothing left to sell.
    /// </summary>
    public static class BastardBlackmailPolicy
    {
        /// <summary>Share of the player's wealth she reaches for. Tuning.</summary>
        public const int DemandPercentOfWealth = 10;

        /// <summary>A floor so even a poor lord pays something for silence. Tuning.</summary>
        public const int MinDemand = 500;

        /// <summary>
        ///   The largest single payment the wired settlement path can carry: the take_gold action clamps one
        ///   transfer to this, and <c>BastardBehavior.SettleByPayment</c> only buys silence when the payment
        ///   MEETS the demand in one go. Romance audit C4: a demand above this was structurally impayable, so
        ///   the player's gold was taken with no silence bought and no message; <see cref="MaxDemand" /> must
        ///   never exceed it. Kept here as a named constant so the coupling is explicit and test-guarded.
        /// </summary>
        public const int MaxSinglePaymentSettlement = GoldTransferLimit.MaxSingleTransfer;

        /// <summary>
        ///   A ceiling so the demand never becomes ruinous in one stroke AND stays settleable in a single
        ///   payment (romance audit C4: never above <see cref="MaxSinglePaymentSettlement" />). Tuning.
        /// </summary>
        public const int MaxDemand = MaxSinglePaymentSettlement;

        /// <summary>Seasons of silence a payment buys before she may demand again. Tuning.</summary>
        public const int GraceSeasons = 4;

        /// <summary>Seasons the player has to meet a demand before she leaks the secret. Tuning.</summary>
        public const int DemandDeadlineSeasons = 2;

        /// <summary>The season until which a payment made now buys her silence (recurring — she returns after).</summary>
        public static int SilenceUntil(int currentSeason) => currentSeason + GraceSeasons;

        /// <summary>True when an unmet demand has gone past its deadline. The caller ALSO requires the demand's
        /// letter to have reached the player before leaking (romance audit M-G6b), so a lost letter never leaks.</summary>
        public static bool DemandHasLapsed(int currentSeason, int demandSeason)
            => demandSeason >= 0 && currentSeason - demandSeason >= DemandDeadlineSeasons;

        /// <summary>
        ///   True when she may raise a fresh demand: none is outstanding and any paid silence has expired. The
        ///   caller still checks <see cref="HoldsLeverage" /> (a hidden bastard, a living non-spouse mother).
        /// </summary>
        public static bool CanRaiseDemand(int currentSeason, int silentUntilSeason, bool hasActiveDemand)
            => !hasActiveDemand && currentSeason >= silentUntilSeason;

        /// <summary>
        ///   What the mother of a concealed bastard has to hold over the player. Three booleans in a row, and
        ///   the last is the one that inverts: a wife shares the house, not a secret to sell.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class LeverageFacts
        {
           /// <summary>The child's parentage is still a secret. There is nothing to sell once it is out.</summary>
           public bool BastardConcealed { get; set; }

           /// <summary>She is alive to press it.</summary>
           public bool MotherAlive { get; set; }

           /// <summary>She is the player's own wife, and a wife shares the house rather than blackmails it.</summary>
           public bool MotherIsPlayerSpouse { get; set; }
        }

        /// <summary>
        ///   The mother holds blackmail leverage only while the bastard is still CONCEALED, she is alive, and
        ///   she is not the player's own spouse (a wife shares the house, not a secret to sell).
        /// </summary>
        public static bool HoldsLeverage(LeverageFacts facts)
        {
           if (facts == null) return false;

           bool bastardConcealed = facts.BastardConcealed;
           bool motherAlive = facts.MotherAlive;
           bool motherIsPlayerSpouse = facts.MotherIsPlayerSpouse;

           return bastardConcealed && motherAlive && !motherIsPlayerSpouse;
        }

        /// <summary>
        ///   What she demands for her silence: a share of the player's wealth, clamped to
        ///   [<see cref="MinDemand" />..<see cref="MaxDemand" />]. Negative gold is treated as none.
        /// </summary>
        public static int DemandAmount(int playerGold)
        {
            int byWealth = Math.Max(0, playerGold) * DemandPercentOfWealth / 100;

            return Math.Max(MinDemand, Math.Min(MaxDemand, byWealth));
        }
    }
}
