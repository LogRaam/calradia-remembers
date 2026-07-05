// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar (ratified 2026-07-05): deterministic per-culture name + epithet minting for a freshly
// rolled nemesis identity. FIRST-PASS pools only (modest, per Gabriel's ask) — flag for expansion. Culture
// keys mirror the vanilla bandit-clan StringIds ("looters", "forest_bandits", "mountain_bandits",
// "sea_raiders", "desert_bandits", "steppe_bandits") so the boss's flavour matches whichever band took the
// player prisoner.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The minted name and byname for one nemesis identity.</summary>
    public sealed class NemesisNameResult
    {
        public NemesisNameResult(string name, string epithet)
        {
            Name = name;
            Epithet = epithet;
        }

        /// <summary>Given name, e.g. "Ranulf".</summary>
        public string Name { get; }

        /// <summary>Byname/title, e.g. "the Faceless Fox".</summary>
        public string Epithet { get; }

        /// <summary>Name and epithet joined for display, e.g. "Ranulf the Faceless Fox".</summary>
        public string FullName => $"{Name} {Epithet}";
    }

    /// <summary>
    ///   Pure, deterministic name/epithet minting. Given the same band culture and seed, ALWAYS returns
    ///   the same result (so re-deriving an identity from a persisted seed never drifts); the caller owns
    ///   sourcing the seed (e.g. a stable hash of the new identity's <see cref="NemesisIdentity.Id"/>), not
    ///   this generator.
    /// </summary>
    public static class NemesisNameGenerator
    {
        /// <summary>TUNE: multiplier used to derive the epithet index from the seed independently of the name index.</summary>
        public const int EpithetSeedMultiplier = 31;

        /// <summary>TUNE: offset used to derive the epithet index from the seed independently of the name index.</summary>
        public const int EpithetSeedOffset = 17;

        // FIRST-PASS pools (Gabriel: expand freely, keep them culture-flavoured and em-dash free).

        private static readonly string[] LooterNames =
        {
            "Wick", "Nabb", "Coll", "Harker", "Sley", "Grubb", "Pock", "Dredge"
        };

        private static readonly string[] ForestBanditNames =
        {
            "Bastian", "Draven", "Garrick", "Harlan", "Roderick",
            "Thorne", "Wolfram", "Everard", "Fulke", "Oswin"
        };

        private static readonly string[] MountainBanditNames =
        {
            "Ivar", "Bjorn", "Ragnvald", "Halvard", "Torvald",
            "Ulfric", "Sigmund", "Erling", "Gunnar", "Vidar"
        };

        private static readonly string[] SeaRaiderNames =
        {
            "Skarde", "Ormund", "Hrolf", "Kettil", "Snorri",
            "Thrain", "Grimolf", "Ake", "Roar", "Egil"
        };

        private static readonly string[] DesertBanditNames =
        {
            "Rafiq", "Zayd", "Nasir", "Qasim", "Farid",
            "Idris", "Hamza", "Sabir", "Jalal", "Tarek"
        };

        private static readonly string[] SteppeBanditNames =
        {
            "Toqta", "Baibars", "Kutlug", "Sartaq", "Yesugei",
            "Arslan", "Batu", "Dayir", "Jebe", "Subotai"
        };

        /// <summary>Generic fallback pool for an unrecognized/modded band culture key (keeps a modder's custom bandit clan from crashing minting).</summary>
        private static readonly string[] GenericNames =
        {
            "Ranulf", "Corvin", "Balthazar", "Godric", "Percival", "Thane", "Bram", "Kestrel"
        };

        /// <summary>Shared/culture-flavoured epithet pool, drawn from regardless of band culture.</summary>
        private static readonly string[] Epithets =
        {
            "the Faceless Fox", "the Cutthroat", "the Red Hand", "the Nightblade",
            "the Wolf's Head", "the Grim", "the Butcher", "the Silent",
            "the Bloody Handed", "the Scourge", "the Widowmaker", "the Merciless",
            "the Crow", "the Reaver", "the Black Hearted", "the Unbroken"
        };

        private static readonly Dictionary<string, string[]> NamePoolsByCulture = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["looters"] = LooterNames,
            ["forest_bandits"] = ForestBanditNames,
            ["mountain_bandits"] = MountainBanditNames,
            ["sea_raiders"] = SeaRaiderNames,
            ["desert_bandits"] = DesertBanditNames,
            ["steppe_bandits"] = SteppeBanditNames
        };

        /// <summary>
        ///   Mints a deterministic name + epithet for a nemesis of <paramref name="bandCultureKey"/>. The
        ///   name pool is selected by culture (falling back to a generic pool for an unrecognized key, e.g.
        ///   a modded bandit clan); the epithet is drawn from the shared pool. Both indices are derived
        ///   from <paramref name="seed"/> alone (no RNG), and derived independently of each other so a name
        ///   and epithet don't always change in lockstep.
        /// </summary>
        public static NemesisNameResult Generate(string bandCultureKey, int seed)
        {
            string[] namePool = bandCultureKey != null && NamePoolsByCulture.TryGetValue(bandCultureKey, out string[] pool)
                ? pool
                : GenericNames;

            int nameIndex = PositiveMod(seed, namePool.Length);
            int epithetSeed = unchecked(seed * EpithetSeedMultiplier + EpithetSeedOffset);
            int epithetIndex = PositiveMod(epithetSeed, Epithets.Length);

            return new NemesisNameResult(namePool[nameIndex], Epithets[epithetIndex]);
        }

        #region private

        private static int PositiveMod(int value, int modulus)
        {
            int remainder = value % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }

        #endregion
    }
}
