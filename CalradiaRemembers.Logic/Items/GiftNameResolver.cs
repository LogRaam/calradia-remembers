// Code written by Gabriel Mailhot, 28/08/2026.
// Player report (Windmolen): married, pregnant wife, offered her "a pendant" in free text. The LLM emitted
// give_item item: pendant. The bridge's old FindItemInRoster did an EXACT case-insensitive name match, and
// the player held no item literally named "pendant" (only a "Jewelry" trade good) - so the bridge answered
// "(You don't seem to have 'pendant' to give.)" though he plainly had jewelry to give, and the wife "was not
// amused." This resolver is the fix: a narrated personal-gift word (pendant, necklace, ring, ...) should
// resolve to a real matching roster item instead of hard-failing, so the gift lands and no false contradiction
// reaches the model or the player. It stays pure (no TaleWorlds types) so the matching rules are unit-tested
// on their own; the engine side (CalradiaRemembers.GameState.BannerlordGameStateBridge) builds the candidate
// list from the live roster and calls in.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Items
{
    /// <summary>
    ///   One roster item as far as gift-name resolution is concerned: enough to match a narrated word against
    ///   and, once matched, to hand back to the caller for the real transfer. <see cref="Category" /> is a
    ///   normalized category token (e.g. "jewelry", from the item's own ItemCategory StringId) filled by the
    ///   engine side; it may be null/empty for an item with no category worth matching on.
    /// </summary>
    public readonly struct GiftCandidate
    {
        /// <summary>The item's engine StringId, so the caller can re-look it up in the live roster.</summary>
        public string Id { get; init; }

        /// <summary>The item's display name, matched against tiers a (exact) and b (token/substring).</summary>
        public string Name { get; init; }

        /// <summary>A normalized category token (lower-case), matched against tier c's synonym map. May be null/empty.</summary>
        public string Category { get; init; }

        /// <summary>The item's base value: the tie-break for tiers b and c (see <see cref="GiftNameResolver" />).</summary>
        public int Value { get; init; }

    }

    /// <summary>
    ///   Resolves a narrated free-text gift word (e.g. "pendant") against the player's real roster items, so a
    ///   flourish of narration does not hard-fail just because nothing is literally named that. Three tiers,
    ///   first to yield any match wins: (a) exact name match; (b) a whole word shared between the requested
    ///   text and the candidate's name; (c) a synonym mapped to a category token (jewelry words today). Returns
    ///   null when nothing plausible matches at all, so the caller can fall back to a refusal.
    /// </summary>
    public static class GiftNameResolver
    {
        /// <summary>Tokens shorter than this are ignored everywhere below, so "a"/"of"/"my" never spuriously match.</summary>
        public const int MinTokenLength = 3;

        /// <summary>The category token every jewelry synonym below maps to (matches the live "jewelry" ItemCategory StringId).</summary>
        public const string JewelryCategory = "jewelry";

        private static readonly char[] TokenDelimiters = {' ', '\t', '\r', '\n', '-', '_', ',', '.', '\'', '"'};

        /// <summary>
        ///   Narrated personal-gift words that stand in for the "jewelry" category. Deliberately small and
        ///   focused on the reported case; more categories/words can be added the same way later.
        /// </summary>
        private static readonly Dictionary<string, string> SynonymToCategory =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pendant"] = JewelryCategory,
                ["necklace"] = JewelryCategory,
                ["amulet"] = JewelryCategory,
                ["locket"] = JewelryCategory,
                ["brooch"] = JewelryCategory,
                ["ring"] = JewelryCategory,
                ["earring"] = JewelryCategory,
                ["earrings"] = JewelryCategory,
                ["jewel"] = JewelryCategory,
                ["jewels"] = JewelryCategory,
                ["jewellery"] = JewelryCategory,
                ["jewelry"] = JewelryCategory,
                ["gem"] = JewelryCategory,
                ["gems"] = JewelryCategory,
                ["trinket"] = JewelryCategory,
                ["choker"] = JewelryCategory
            };

        /// <summary>
        ///   Resolves <paramref name="requested" /> against <paramref name="candidates" />. Tier a (exact
        ///   name) wins outright when it hits. Tiers b (token/substring) and c (synonym-to-category) may each
        ///   match several candidates; when they do, the LOWEST-value one is chosen on purpose, so a narrated
        ///   flourish like "a pendant" can never accidentally siphon the player's single most valuable item.
        ///   Returns null (no plausible match) on a blank request, an empty candidate list, or genuinely no hit.
        /// </summary>
        public static GiftCandidate? Resolve(string requested, IReadOnlyList<GiftCandidate> candidates)
        {
            if (string.IsNullOrWhiteSpace(requested) || candidates == null || candidates.Count == 0) return null;

            string trimmed = requested.Trim();

            foreach (GiftCandidate candidate in candidates)
                if (!string.IsNullOrEmpty(candidate.Name) &&
                    string.Equals(candidate.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                    return candidate;

            GiftCandidate? tokenMatch = ResolveByToken(trimmed, candidates);
            if (tokenMatch.HasValue) return tokenMatch;

            return ResolveBySynonymCategory(trimmed, candidates);
        }

        #region private

        /// <summary>Tier b: the requested text and a candidate's name share at least one whole word (>= MinTokenLength).</summary>
        private static GiftCandidate? ResolveByToken(string requested, IReadOnlyList<GiftCandidate> candidates)
        {
            List<string> requestedTokens = Tokenize(requested);
            if (requestedTokens.Count == 0) return null;

            GiftCandidate? best = null;
            foreach (GiftCandidate candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate.Name)) continue;

                List<string> nameTokens = Tokenize(candidate.Name);
                bool sharesToken = false;
                foreach (string token in requestedTokens)
                    if (nameTokens.Contains(token))
                    {
                        sharesToken = true;

                        break;
                    }

                if (!sharesToken) continue;
                if (!best.HasValue || candidate.Value < best.Value.Value) best = candidate;
            }

            return best;
        }

        /// <summary>Tier c: a requested word (e.g. "pendant") maps to a category, matched against candidates' Category.</summary>
        private static GiftCandidate? ResolveBySynonymCategory(string requested, IReadOnlyList<GiftCandidate> candidates)
        {
            List<string> requestedTokens = Tokenize(requested);

            string category = null;
            foreach (string token in requestedTokens)
                if (SynonymToCategory.TryGetValue(token, out string mapped))
                {
                    category = mapped;

                    break;
                }

            if (category == null) return null;

            GiftCandidate? best = null;
            foreach (GiftCandidate candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate.Category)) continue;
                if (!string.Equals(candidate.Category.Trim(), category, StringComparison.OrdinalIgnoreCase)) continue;
                if (!best.HasValue || candidate.Value < best.Value.Value) best = candidate;
            }

            return best;
        }

        /// <summary>Splits on whitespace/punctuation, lower-cases, and drops anything shorter than MinTokenLength.</summary>
        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return tokens;

            foreach (string raw in text.Split(TokenDelimiters, StringSplitOptions.RemoveEmptyEntries))
                if (raw.Length >= MinTokenLength)
                    tokens.Add(raw.ToLowerInvariant());

            return tokens;
        }

        #endregion
    }
}
