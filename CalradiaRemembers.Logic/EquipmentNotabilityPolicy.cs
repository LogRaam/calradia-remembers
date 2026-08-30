// Code written by Gabriel Mailhot, 29/08/2026.
// Equipment-awareness pillar (pure core): decides which of a hero's equipped pieces are worth telling the
// model about, and renders them as one natural-language clause. Player report (a modder on TOR): CR never
// feeds equipment to the prompt, so an NPC's reaction to fine or shabby attire is pure guesswork and a
// companion's mastercraft blade or a gifted legendary sword is invisible in conversation. This layer makes a
// STANDOUT piece a real, deterministic prompt signal, for the player, for present companions and witnesses
// (Subject voice), and for the speaking NPC's own gear (Self voice) so a gift they now wield is something
// they can actually reference. Only standouts speak: an ordinary (Common) piece is left unvoiced, the same
// discipline as AppearanceComposer. No engine types here; the host maps EquipmentElement onto EquipmentPieceFacts.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure, engine-free scoring and phrasing of a hero's NOTABLE equipment. Reuses
    ///   <see cref="AppearanceVoice" /> so gear reads TO the NPC in the third person (a player/companion/witness
    ///   the NPC can see) or as the NPC's OWN kit in the second person. See this file's header for the design.
    /// </summary>
    public static class EquipmentNotabilityPolicy
    {
        /// <summary>
        ///   Value at/above which an UNMARKED (Common-modifier) piece is promoted to <see cref="GearQuality.Fine" />
        ///   and thus voiced. Vanilla masterwork/legendary items carry a modifier and are caught by quality
        ///   already; this catches modded uniques (e.g. a TOR legendary blade) that stand out by sheer worth
        ///   without a vanilla modifier. Set high enough that ordinary top-tier troop gear stays silent.
        /// </summary>
        public const int NotableValueThreshold = 15000;

        /// <summary>At most this many pieces are voiced, so the block stays a short clause and never bloats the lean prompt.</summary>
        public const int MaxPieces = 2;

        /// <summary>True when the piece stands out enough to voice (any band but Common, after value promotion).</summary>
        public static bool IsNotable(EquipmentPieceFacts piece)
            => piece != null && EffectiveQuality(piece) != GearQuality.Common;

        /// <summary>
        ///   Composes the notable-gear clause for a hero from all their equipped pieces, in the requested
        ///   <paramref name="voice" />. Picks the single most notable piece PER KIND (so a full harness never
        ///   spells out five armor slots), then the top <see cref="MaxPieces" /> across kinds, and renders them
        ///   in a natural reading order (worn, then carried, then ridden). Returns "" (never null) when nothing
        ///   stands out, so the caller renders no line at all.
        /// </summary>
        public static string Compose(IEnumerable<EquipmentPieceFacts> pieces, bool isFemale, AppearanceVoice voice = AppearanceVoice.Subject)
        {
            if (pieces == null) return "";

            List<EquipmentPieceFacts> chosen = pieces
                .Where(IsNotable)
                .GroupBy(p => p.Kind)
                .Select(g => g.OrderByDescending(Rank).ThenByDescending(p => p.Value).First())
                .OrderByDescending(Rank)
                .ThenByDescending(p => p.Value)
                .Take(MaxPieces)
                .OrderBy(p => DisplayOrder(p.Kind))
                .ToList();

            if (chosen.Count == 0) return "";

            List<string> clauses = chosen.Select(p => Clause(p, voice)).ToList();
            string subject = voice == AppearanceVoice.Self ? "You" : (isFemale ? "She" : "He");

            return subject + " " + JoinClauses(clauses) + ".";
        }

        #region private

        /// <summary>An unmarked but very costly piece reads as Fine; otherwise the piece's own band stands.</summary>
        private static GearQuality EffectiveQuality(EquipmentPieceFacts p)
            => p.Quality == GearQuality.Common && p.Value >= NotableValueThreshold
                ? GearQuality.Fine
                : p.Quality;

        /// <summary>Notability rank for choosing which pieces to voice: better make ranks higher; Poor still ranks above nothing.</summary>
        private static int Rank(EquipmentPieceFacts p)
        {
            switch (EffectiveQuality(p))
            {
                case GearQuality.Legendary:  return 4;
                case GearQuality.Masterwork: return 3;
                case GearQuality.Fine:       return 2;
                case GearQuality.Poor:       return 1;
                default:                     return 0;
            }
        }

        /// <summary>Reading order once pieces are chosen: what they wear, then carry, then ride.</summary>
        private static int DisplayOrder(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Armor:  return 0;
                case GearKind.Weapon: return 1;
                default:              return 2;
            }
        }

        // Worn things that read as MASS nouns take no article ("wears fine armor"); a countable garment or
        // piece does ("wears a fine gown", "wears a shabby helm"). The player's own example was a "dress", so
        // attire has to phrase as cleanly as plate does.
        private static readonly HashSet<string> MassArmorNouns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "armor", "armour", "mail", "chainmail", "plate", "harness", "attire", "leather"
        };

        /// <summary>One finite-verb clause for a piece, sharing the sentence's subject ("He wears..."/"You wear...").</summary>
        private static string Clause(EquipmentPieceFacts p, AppearanceVoice voice)
        {
            bool self = voice == AppearanceVoice.Self;
            string adj = Adjective(EffectiveQuality(p), p.Kind);

            switch (p.Kind)
            {
                case GearKind.Armor:
                    string armorNoun = Noun(p, "armor");
                    string worn = MassArmorNouns.Contains(armorNoun)
                        ? adj + " " + armorNoun
                        : Article(adj) + " " + adj + " " + armorNoun;

                    return (self ? "wear" : "wears") + " " + worn;
                case GearKind.Weapon:
                    return (self ? "bear" : "bears") + " " + Article(adj) + " " + adj + " " + Noun(p, "weapon");
                default:
                    return (self ? "ride" : "rides") + " " + Article(adj) + " " + adj + " " + Noun(p, "horse");
            }
        }

        /// <summary>Quality adjective; the negative band is worded per kind so "shabby armor" and a "notched" blade both read true.</summary>
        private static string Adjective(GearQuality quality, GearKind kind)
        {
            switch (quality)
            {
                case GearQuality.Legendary:  return "legendary";
                case GearQuality.Masterwork: return "masterwork";
                case GearQuality.Fine:       return "fine";
                case GearQuality.Poor:
                    switch (kind)
                    {
                        case GearKind.Armor:  return "shabby";
                        case GearKind.Weapon: return "notched";
                        default:              return "sorry-looking";
                    }
                default: return ""; // Common is never rendered (filtered out upstream)
            }
        }

        private static string Noun(EquipmentPieceFacts p, string fallback)
            => string.IsNullOrWhiteSpace(p.KindNoun) ? fallback : p.KindNoun.Trim();

        private static string Article(string nextWord)
        {
            if (string.IsNullOrEmpty(nextWord)) return "a";
            char c = char.ToLowerInvariant(nextWord[0]);

            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' ? "an" : "a";
        }

        /// <summary>Joins one or two clauses with "and"; capped at <see cref="MaxPieces" /> so no comma list is needed.</summary>
        private static string JoinClauses(List<string> clauses)
            => clauses.Count == 1 ? clauses[0] : clauses[0] + " and " + clauses[1];

        #endregion
    }
}
