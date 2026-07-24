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

        /// <summary>TUNE: how many re-rolls to try before accepting a name already in use. The pools give 8-10 names x 16 epithets (128-160 combos), so a free one is found almost immediately unless dozens of one culture live at once.</summary>
        public const int DistinctMaxTries = 32;

        /// <summary>TUNE: a large odd step added to the seed on each re-roll, so successive tries land on different name/epithet pairs rather than cycling (Knuth's multiplicative-hash constant 0x9E3779B1 as a signed int).</summary>
        private const int DistinctSeedStep = -1640531535;

        /// <summary>
        ///   Nemesis audit RC3: a distinct name+epithet, avoiding any pair a LIVING identity already carries so
        ///   two nemeses are never homonyms (which made the player believe one man and amplified the perception
        ///   of the shared-memory bug). Re-rolls the seed up to <see cref="DistinctMaxTries" /> times against
        ///   <paramref name="isTaken" />; if the pool is genuinely saturated (never reachable in play), it
        ///   returns the last candidate rather than an ugly synthetic name, and the caller logs the rare
        ///   duplicate. A null predicate degrades to the plain <see cref="Generate" />.
        /// </summary>
        public static NemesisNameResult GenerateDistinct(string bandCultureKey, int seed, Func<string, string, bool> isTaken)
        {
            NemesisNameResult candidate = Generate(bandCultureKey, seed);

            if (isTaken == null) return candidate;

            for (var i = 0; i < DistinctMaxTries && isTaken(candidate.Name, candidate.Epithet); i++)
                candidate = Generate(bandCultureKey, unchecked(seed + (i + 1) * DistinctSeedStep));

            return candidate;
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
