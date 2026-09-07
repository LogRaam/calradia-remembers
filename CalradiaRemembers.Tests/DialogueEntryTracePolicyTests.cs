// Code written by Gabriel Mailhot, 06/09/2026.
// Pins the one diagnostic line the dialogue collector writes at the end of every hero conversation.
// It exists to distinguish two unreproducible player reports: reporter A (1.5.2 beta, CR 2.5.1) picked
// "A word with you...", the LLM fired, but no CR overlay opened; reporter B (1.3.15, CR 2.5.0, the TOR
// total conversion) never saw the CR option at all for some wanderers. LlmDialogFlow registers the CR
// entry option on exactly two hub tokens (hero_main_options, alley_player_owned_start); a conversation
// that never reaches one of those silently has no CR option. "Offered = YES" points investigation at the
// scene-opening code (reporter A's shape); "offered = NO" plus a foreign token in the trail names the
// other dialogue tree's own hub (reporter B's shape), with nothing more to ask the player. If this
// wording drifts, the log stops answering the one question it exists to answer.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class DialogueEntryTracePolicyTests
    {
        private static readonly string[] EntryHubTokens = {"hero_main_options", "alley_player_owned_start"};

        // Reporter A's shape: the LLM fired (CR reached one of its registered hubs) but the overlay never
        // opened. "Offered = YES" is exactly the signal that rules out reporter B's hypothesis (a foreign
        // hub) and points investigation at the scene-opening code instead.
        [Test]
        public void GIVEN_entry_was_offered_WHEN_composing_the_line_THEN_the_verdict_reads_YES()
        {
            DialogueEntryTracePolicy.ComposeLine(
                    "Ira of Fen Company (Battanian)",
                    "wanderer",
                    entryOffered: true,
                    tokenTrail: new List<string> {"start", "hero_main_options"},
                    registeredHubTokens: EntryHubTokens)
                .Should().Contain("CR entry offered = YES");
        }

        // Reporter B's shape: the option never appeared. This is the exact line shape from the
        // investigation write-up, a foreign hub (tor_custom_hub, the TOR total conversion's own hub)
        // ends the trail instead of hero_main_options, naming the cause without asking the player anything
        // further.
        [Test]
        public void GIVEN_entry_was_not_offered_and_the_trail_ends_on_a_foreign_hub_WHEN_composing_the_line_THEN_it_names_the_foreign_token()
        {
            DialogueEntryTracePolicy.ComposeLine(
                    "Ira of Fen Company (Battanian)",
                    "wanderer",
                    entryOffered: false,
                    tokenTrail: new List<string> {"start", "wanderer_meet_player_response", "wanderer_preintroduction", "tor_custom_hub"},
                    registeredHubTokens: EntryHubTokens)
                .Should().Be(
                    "[DIALOGUE] conversation with Ira of Fen Company (Battanian), wanderer: CR entry offered = NO; " +
                    "tokens seen: start > wanderer_meet_player_response > wanderer_preintroduction > tor_custom_hub");
        }

        // A conversation that ends (or crashes out) before a single ConsequenceRunned fires would otherwise
        // render an empty join ("tokens seen: ") that reads as a formatting bug rather than as "we captured
        // nothing" (the two must not look the same in the log).
        [Test]
        public void GIVEN_no_tokens_were_captured_WHEN_composing_the_line_THEN_the_trail_reads_as_none_captured()
        {
            DialogueEntryTracePolicy.ComposeLine(
                    "a wanderer",
                    "wanderer",
                    entryOffered: false,
                    tokenTrail: new List<string>(),
                    registeredHubTokens: EntryHubTokens)
                .Should().Contain("tokens seen: (none captured)");
        }

        // A foreign dialogue tree (a total conversion's own hub chain, reporter B's hypothesis) can churn
        // through many more than a dozen states before the conversation ends. The line must stay one
        // readable line rather than growing without bound, so the cap and its "(+N more)" marker are pinned
        // at the exact MaxTrailTokens boundary.
        [Test]
        public void GIVEN_a_trail_longer_than_the_cap_WHEN_composing_the_line_THEN_it_is_truncated_with_a_count_of_the_rest()
        {
            var longTrail = new List<string>();
            for (int i = 0; i < DialogueEntryTracePolicy.MaxTrailTokens + 3; i++)
                longTrail.Add($"state_{i}");

            string line = DialogueEntryTracePolicy.ComposeLine(
                "a wanderer", "wanderer", entryOffered: false, longTrail, EntryHubTokens);

            line.Should().Contain($"state_{DialogueEntryTracePolicy.MaxTrailTokens - 1}");
            line.Should().NotContain($"state_{DialogueEntryTracePolicy.MaxTrailTokens}");
            line.Should().Contain("(+3 more)");
        }

        // The task's own design assigns the policy a third decision beyond wording and capping: whether the
        // trail's own evidence (a registered hub token appearing in it) agrees with the offered flag, which
        // is set elsewhere from a different signal (IsHeroEligible actually running). Offered-but-no-hub-in-
        // trail means the trail itself is missing evidence (capped, or a gap in the collector's reflection),
        // worth flagging separately from either player report.
        [Test]
        public void GIVEN_entry_was_offered_but_the_trail_shows_no_registered_hub_WHEN_composing_the_line_THEN_it_notes_the_disagreement()
        {
            DialogueEntryTracePolicy.ComposeLine(
                    "a wanderer",
                    "wanderer",
                    entryOffered: true,
                    tokenTrail: new List<string> {"start", "some_unrelated_state"},
                    registeredHubTokens: EntryHubTokens)
                .Should().Contain("note: offered but no registered hub token appears in the trail");
        }

        // The mirror disagreement: the trail proves CR reached one of its own registered hubs, yet the
        // offered flag says no. That points at a different bug class than a foreign hub (reporter B): CR's
        // own IsHeroEligible check refusing the option at a hub CR does own, so it is worded distinctly.
        [Test]
        public void GIVEN_entry_was_not_offered_but_the_trail_reached_a_registered_hub_WHEN_composing_the_line_THEN_it_notes_the_disagreement()
        {
            DialogueEntryTracePolicy.ComposeLine(
                    "a wanderer",
                    "wanderer",
                    entryOffered: false,
                    tokenTrail: new List<string> {"start", "hero_main_options"},
                    registeredHubTokens: EntryHubTokens)
                .Should().Contain("note: trail reached a registered hub but the entry was never offered");
        }

        // No player report ever involves a missing kind or partner label, but the collector composes this
        // line unconditionally at the end of EVERY hero conversation, so a null/blank input (a hero whose
        // occupation could not be read) must never be the reason the one diagnostic line is lost.
        [Test]
        public void GIVEN_no_kind_string_is_available_WHEN_composing_the_line_THEN_it_omits_the_kind_rather_than_failing()
        {
            DialogueEntryTracePolicy.ComposeLine(
                    "Ira of Fen Company (Battanian)",
                    kind: null,
                    entryOffered: true,
                    tokenTrail: new List<string> {"hero_main_options"},
                    registeredHubTokens: EntryHubTokens)
                .Should().Be("[DIALOGUE] conversation with Ira of Fen Company (Battanian): CR entry offered = YES; tokens seen: hero_main_options");
        }
    }
}
