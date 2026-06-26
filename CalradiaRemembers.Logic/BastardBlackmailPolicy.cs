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

        /// <summary>A ceiling so the demand never becomes ruinous in one stroke. Tuning.</summary>
        public const int MaxDemand = 20000;

        /// <summary>
        ///   The mother holds blackmail leverage only while the bastard is still CONCEALED, she is alive, and
        ///   she is not the player's own spouse (a wife shares the house, not a secret to sell).
        /// </summary>
        public static bool HoldsLeverage(bool bastardConcealed, bool motherAlive, bool motherIsPlayerSpouse)
            => bastardConcealed && motherAlive && !motherIsPlayerSpouse;

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
