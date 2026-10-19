// Code written by Gabriel Mailhot, 29/08/2026.
// Equipment-awareness pillar: maps a Bannerlord ItemModifier onto the pure GearQuality band, engine-agnostic
// (the host passes plain values, never a TaleWorlds type, matching EquipmentPieceFacts' own discipline). The
// PRIMARY signal is the modifier's own native quality name: TaleWorlds' ItemModifier.ItemQuality enum
// (Poor/Inferior/Common/Fine/Masterwork/Legendary, read straight off item_modifiers.xml's "quality" attribute)
// is the game's own authoritative band, so a vanilla modifier is placed correctly with no guessing at all once
// the host passes its name through. The PRICE MULTIPLIER is the fallback, for a modded ItemModifier whose
// quality name is missing or unrecognized: verified against the decompiled
// Modules/Native/ModuleData/item_modifiers.xml (checked 29/08/2026), vanilla holds these price_factor values
// UNIFORMLY across every weapon and armor group: legendary=1.8, masterwork=1.5, fine=1.2, inferior=0.6,
// poor=0.3 (0.1-0.4 for horses and companion gear). The thresholds below sit at or just under each verified
// value so a vanilla modifier read by price alone still lands in its true band, with headroom above 1.8 for a
// modded item that goes further still. A small keyword fallback catches a modded ItemModifier that sets
// neither a recognized quality name nor a telling price, going by its own descriptive name instead (the
// "Rusty" or "Masterwork" prefix baked into the item's display name).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Items
{
    /// <summary>Pure, engine-free mapping from an item modifier's plain facts to a <see cref="GearQuality" /> band.</summary>
    public static class EquipmentModifierQualityPolicy
    {
        /// <summary>Verified vanilla legendary price_factor (1.8, uniform across weapon/armor groups); the price-fallback floor.</summary>
        public const double LegendaryPriceThreshold = 1.8;

        /// <summary>Verified vanilla masterwork price_factor (1.5, uniform across weapon/armor groups); the price-fallback floor.</summary>
        public const double MasterworkPriceThreshold = 1.5;

        /// <summary>Verified vanilla fine price_factor (1.2, uniform across weapon/armor groups); the price-fallback floor.</summary>
        public const double FinePriceThreshold = 1.1;

        /// <summary>
        ///   At/below this, an unnamed modifier reads Poor. Sits above vanilla's inferior band (0.6) so both
        ///   the poor (0.3) and inferior (0.6) native bands, which our simpler 5-band GearQuality folds into
        ///   one negative band, are caught by price alone.
        /// </summary>
        public const double PoorPriceThreshold = 0.85;

        /// <summary>
        ///   A modded ItemModifier's own descriptive name containing one of these words reads as the paired
        ///   positive band, when its native quality name is unrecognized and its price is inconclusive.
        /// </summary>
        private static readonly (string Keyword, GearQuality Quality)[] PositiveKeywords =
        {
            ("Legendary", GearQuality.Legendary),
            ("Masterwork", GearQuality.Masterwork),
            ("Fine", GearQuality.Fine)
        };

        /// <summary>Descriptive negative keywords per the ratified scope: a shabby item's own name usually says so.</summary>
        private static readonly string[] NegativeKeywords =
        {
            "Rusty", "Tattered", "Cracked", "Chipped", "Battered", "Rusted", "Bent", "Crude"
        };

        /// <summary>
        ///   Maps a modifier's plain facts to a <see cref="GearQuality" /> band: the native quality name first
        ///   (authoritative for any vanilla modifier), then a descriptive-keyword match, then the price
        ///   multiplier as a last resort. <paramref name="nativeQualityName" /> may be null/blank (an unmarked
        ///   item, or a host that could not resolve one) and <paramref name="priceMultiplier" /> defaults to 1.0
        ///   (an ordinary, unmodified price) when the caller has nothing better.
        /// </summary>
        public static GearQuality FromModifier(string nativeQualityName, double priceMultiplier)
        {
            GearQuality? byName = FromNativeQualityName(nativeQualityName);
            if (byName.HasValue) return byName.Value;

            GearQuality? byKeyword = FromKeyword(nativeQualityName);
            if (byKeyword.HasValue) return byKeyword.Value;

            return FromPriceMultiplier(priceMultiplier);
        }

        /// <summary>The price-only fallback path, exposed separately so its verified thresholds can be tested on their own.</summary>
        public static GearQuality FromPriceMultiplier(double priceMultiplier)
        {
            if (priceMultiplier >= LegendaryPriceThreshold) return GearQuality.Legendary;
            if (priceMultiplier >= MasterworkPriceThreshold) return GearQuality.Masterwork;
            if (priceMultiplier >= FinePriceThreshold) return GearQuality.Fine;
            if (priceMultiplier <= PoorPriceThreshold) return GearQuality.Poor;

            return GearQuality.Common;
        }

        #region private

        /// <summary>TaleWorlds' own ItemModifier.ItemQuality enum names, passed through by the host as a plain string.</summary>
        private static GearQuality? FromNativeQualityName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            switch (name.Trim().ToLowerInvariant())
            {
                // Inferior (vanilla's "dull"/"dented"/"worn" band) folds into Poor: our 5-band GearQuality has
                // no middle negative band, and an inferior piece is still worth the "shabby" clause.
                case "poor":
                case "inferior":
                    return GearQuality.Poor;
                case "common":
                    return GearQuality.Common;
                case "fine":
                    return GearQuality.Fine;
                case "masterwork":
                    return GearQuality.Masterwork;
                case "legendary":
                    return GearQuality.Legendary;
                default:
                    return null;
            }
        }

        private static GearQuality? FromKeyword(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            foreach (string kw in NegativeKeywords)
                if (name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    return GearQuality.Poor;

            foreach ((string Keyword, GearQuality Quality) pair in PositiveKeywords)
                if (name.IndexOf(pair.Keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return pair.Quality;

            return null;
        }

        #endregion
    }
}
