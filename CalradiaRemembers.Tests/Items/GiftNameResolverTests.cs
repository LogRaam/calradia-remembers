// Code written by Gabriel Mailhot, 28/08/2026.
// Player report (Windmolen): married, pregnant wife, offered her "a pendant" in free text. The LLM emitted
// give_item item: pendant, and the old exact-name-only match failed even though the player held a "Jewelry"
// trade good: the bridge answered "(You don't seem to have 'pendant' to give.)" and the wife "was not
// amused." GiftNameResolver.Resolve is the fix under test here: a narrated personal-gift word should resolve
// to a real matching roster item (by exact name, by a shared word, or by a jewelry synonym mapped to
// category) instead of hard-failing, and when several items could match, it must always prefer the CHEAPEST
// one so a narrated flourish never accidentally siphons the player's most valuable possession.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Items;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Items
{
    [TestFixture]
    public sealed class GiftNameResolverTests
    {
        // STAKE: this is the exact Windmolen player report. Without this, a narrated "a pendant" against a
        // "Jewelry" trade good (no item literally named "pendant") shows a false "you don't have that" and
        // the gift never lands, even though the player plainly has jewelry to give.
        [Test]
        public void GIVEN_pendant_requested_and_only_a_jewelry_trade_good_held_WHEN_resolving_THEN_it_resolves_to_the_jewelry_item()
        {
            var candidates = new List<GiftCandidate>
            {
                new GiftCandidate("jewelry", "Jewelry", "jewelry", 675),
                new GiftCandidate("grain", "Grain", "grain", 40),
                new GiftCandidate("sword", "Arming Sword", null, 120)
            };

            GiftCandidate? result = GiftNameResolver.Resolve("pendant", candidates);

            result.Should().NotBeNull();
            result.Value.Id.Should().Be("jewelry");
        }

        // STAKE: an exact name in the roster must never be shadowed by a synonym/category guess; tier a
        // (exact match) has to run and win before tiers b/c are even considered.
        [Test]
        public void GIVEN_the_exact_item_name_requested_WHEN_resolving_THEN_tier_a_wins_over_any_synonym_route()
        {
            var candidates = new List<GiftCandidate>
            {
                new GiftCandidate("jewelry", "Jewelry", "jewelry", 675),
                new GiftCandidate("trinket_box", "Trinket Box", "furniture", 30)
            };

            GiftCandidate? result = GiftNameResolver.Resolve("Jewelry", candidates);

            result.Should().NotBeNull();
            result.Value.Id.Should().Be("jewelry");
        }

        // STAKE: a narrated gift often names the item more specifically than the roster entry does ("silver
        // necklace" for an item just called "Silver Necklace", or the bare word "necklace" alone); the shared
        // whole-word tier must catch this without requiring the player or the model to hit the name exactly.
        [Test]
        public void GIVEN_a_word_shared_with_a_candidates_full_name_WHEN_resolving_THEN_the_token_tier_resolves_it()
        {
            var candidates = new List<GiftCandidate>
            {
                new GiftCandidate("silver_necklace", "Silver Necklace", "jewelry", 300),
                new GiftCandidate("grain", "Grain", "grain", 40)
            };

            GiftNameResolver.Resolve("silver necklace", candidates).Value.Id.Should().Be("silver_necklace");
            GiftNameResolver.Resolve("necklace", candidates).Value.Id.Should().Be("silver_necklace");
        }

        // STAKE: the anti-abuse guard. A narrated flourish like "a ring" must never be free to pick the
        // player's finest heirloom when a cheap trinket of the same category also qualifies: the resolver
        // always prefers the CHEAPER match so the model cannot inflate the value of what actually changes
        // hands just by narrating loosely.
        [Test]
        public void GIVEN_two_jewelry_candidates_of_different_value_WHEN_a_synonym_is_requested_THEN_the_cheaper_one_is_chosen()
        {
            // Names deliberately avoid the word "ring" itself, so this exercises tier c (synonym-to-category)
            // rather than tier b (shared word) - a "Fine Golden Ring" would match "ring" on the name alone.
            var candidates = new List<GiftCandidate>
            {
                new GiftCandidate("fine_bracelet", "Fine Golden Bracelet", "jewelry", 900),
                new GiftCandidate("plain_jewelry", "Jewelry", "jewelry", 675)
            };

            GiftCandidate? result = GiftNameResolver.Resolve("ring", candidates);

            result.Should().NotBeNull();
            result.Value.Id.Should().Be("plain_jewelry");
        }

        // STAKE: same anti-abuse guard, but for the token tier (b) rather than the synonym tier (c): two
        // candidates sharing a word with the request must also fall back to the cheapest, not the first
        // found or the costliest.
        [Test]
        public void GIVEN_two_candidates_sharing_a_token_WHEN_resolving_THEN_the_cheaper_one_is_chosen()
        {
            var candidates = new List<GiftCandidate>
            {
                new GiftCandidate("gold_necklace", "Gold Necklace", "jewelry", 800),
                new GiftCandidate("bead_necklace", "Bead Necklace", "jewelry", 60)
            };

            GiftCandidate? result = GiftNameResolver.Resolve("necklace", candidates);

            result.Should().NotBeNull();
            result.Value.Id.Should().Be("bead_necklace");
        }

        // STAKE: without a minimum token length, a short narrated filler word ("a", "my", "an") could
        // spuriously match a short candidate name or synonym key; the resolver must ignore anything under
        // MinTokenLength rather than let it drive a match.
        [Test]
        public void GIVEN_a_trivially_short_requested_word_WHEN_resolving_THEN_it_never_spuriously_matches()
        {
            var candidates = new List<GiftCandidate>
            {
                new GiftCandidate("jewelry", "Jewelry", "jewelry", 675)
            };

            GiftNameResolver.Resolve("a", candidates).Should().BeNull();
            GiftNameResolver.Resolve("of", candidates).Should().BeNull();
        }

        // STAKE: a refusal must still be possible. When nothing in the roster is even plausibly related
        // (no shared word, no category hit), the resolver must say so rather than grabbing an unrelated item.
        [Test]
        public void GIVEN_no_plausible_match_in_the_roster_WHEN_resolving_THEN_it_returns_none()
        {
            var candidates = new List<GiftCandidate>
            {
                new GiftCandidate("jewelry", "Jewelry", "jewelry", 675),
                new GiftCandidate("grain", "Grain", "grain", 40)
            };

            GiftNameResolver.Resolve("warhorse", candidates).Should().BeNull();
        }

        // STAKE: the bridge calls this on every free-text give_item; a blank request or an empty/null roster
        // (a destitute party, or a malformed call) must degrade to "no match" and never throw mid-conversation.
        [Test]
        public void GIVEN_a_blank_request_or_an_empty_or_null_candidate_list_WHEN_resolving_THEN_it_is_safe_and_returns_none()
        {
            var candidates = new List<GiftCandidate> {new GiftCandidate("jewelry", "Jewelry", "jewelry", 675)};

            GiftNameResolver.Resolve(null, candidates).Should().BeNull();
            GiftNameResolver.Resolve("", candidates).Should().BeNull();
            GiftNameResolver.Resolve("   ", candidates).Should().BeNull();
            GiftNameResolver.Resolve("pendant", new List<GiftCandidate>()).Should().BeNull();
            GiftNameResolver.Resolve("pendant", null).Should().BeNull();
        }
    }
}
