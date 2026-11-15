// Code written by Gabriel Mailhot, 10/07/2026.
// Player report (ProudSeal 2026-07-10): NPCs open too neutral and too reasonable; a rivalry needs the player to
// offend first. This is the pure decision that colours the OPENING TONE of a meeting from a barely-known noble's
// traits and the player's standing. It never touches relation numbers (deeds still govern regard); it only picks
// the FirstImpressionDisposition and offers a stable per-NPC roll so a given noble's prejudice is consistent.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The inputs that decide how a barely-known noble opens toward the player. Pure data, no engine types.</summary>
    public sealed class FirstImpressionInputs
    {
        /// <summary>NPC Mercy trait level, -2..+2 (negative = cruel, positive = kind).</summary>
        public int Mercy { get; set; }

        /// <summary>NPC Honor trait level, -2..+2.</summary>
        public int Honor { get; set; }

        /// <summary>NPC Calculating trait level, -2..+2 (positive = scheming).</summary>
        public int Calculating { get; set; }

        /// <summary>NPC Generosity trait level, -2..+2.</summary>
        public int Generosity { get; set; }

        /// <summary>NPC Valor trait level, -2..+2 (reserved; not used in the formula yet).</summary>
        public int Valor { get; set; }

        /// <summary>Player clan tier, 0..6.</summary>
        public int PlayerClanTier { get; set; }

        /// <summary>Player renown band, 0..3 (0 = unknown upstart, 3 = famous).</summary>
        public int PlayerRenownBand { get; set; }

        /// <summary>How many fiefs the player holds, 0..N.</summary>
        public int PlayerFiefCount { get; set; }

        /// <summary>True when the player's clan is a DIFFERENT clan inside the NPC's own kingdom (a shared-reward rival).</summary>
        public bool SameFactionRival { get; set; }

        /// <summary>True when the NPC is in the player's own clan (a hard exemption from prejudice).</summary>
        public bool IsOwnClanMember { get; set; }

        /// <summary>True when a real prior relationship already exists (their standing governs, inject no colour).</summary>
        public bool IsAlreadyKnown { get; set; }

        /// <summary>Stable per-NPC roll, 0..99 (seed by StringId), that gates whether latent prejudice shows.</summary>
        public int Roll { get; set; }
    }

    /// <summary>Pure rules that resolve a barely-known noble's opening posture toward the player.</summary>
    public static class FirstImpressionPolicy
    {
        /// <summary>TUNE: the per-point prejudice pressure converted into a show-it chance, capped at this ceiling.</summary>
        public const int MaxShowChancePercent = 85;

        /// <summary>Resolves the opening posture from the noble's traits and the player's standing.</summary>
        public static FirstImpressionDisposition Resolve(FirstImpressionInputs i)
        {
            if (i == null)
                return FirstImpressionDisposition.Neutral;

            if (i.IsOwnClanMember)
                return FirstImpressionDisposition.Loyal;

            if (i.IsAlreadyKnown)
                return FirstImpressionDisposition.None;

            var pressure = 0;

            if (i.Mercy < 0)
                pressure += (-i.Mercy) * 2;

            if (i.Honor < 0)
                pressure += (-i.Honor);

            if (i.Generosity < 0)
                pressure += (-i.Generosity);

            if (i.Calculating > 0)
                pressure += i.Calculating;

            if (i.SameFactionRival)
                pressure += 2;

            pressure -= i.PlayerClanTier;
            pressure -= i.PlayerRenownBand;
            pressure -= Math.Min(i.PlayerFiefCount, 3);

            if (i.Mercy > 0)
                pressure -= i.Mercy;

            if (i.Generosity > 0)
                pressure -= i.Generosity;

            if (pressure <= 0)
                return pressure <= -3 ? FirstImpressionDisposition.Cordial : FirstImpressionDisposition.Neutral;

            // Prejudice pressure exists. Does THIS noble show it? The stable roll gates it.
            var chance = Math.Min(MaxShowChancePercent, pressure * 15);

            if (i.Roll >= chance)
                return FirstImpressionDisposition.Neutral; // this noble does not show it

            if (i.Generosity < 0 && i.SameFactionRival)
                return FirstImpressionDisposition.Arrogant;

            if (i.Mercy < 0)
                return FirstImpressionDisposition.Demeaning;

            if (i.Honor < 0)
                return FirstImpressionDisposition.Suspicious;

            return FirstImpressionDisposition.Cool;
        }

        /// <summary>
        ///   Deterministic 0..99 hash of a seed (a StringId), the SAME value for the SAME seed across processes.
        ///   Uses Hashing.StableHash, NOT string.GetHashCode (which is randomized per process).
        /// </summary>
        public static int StableRoll(string seed) => (int)(Hashing.StableHash.Of(seed) % 100u);
    }
}
