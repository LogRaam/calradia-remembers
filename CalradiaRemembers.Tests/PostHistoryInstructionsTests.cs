// Code written by Gabriel Mailhot, 29/06/2026.
// Documents the modder-editable "post-history instructions" hook (post_history_instructions.txt): free-form text
// injected at the VERY END of the system prompt — the highest-recency position, the analog of a SillyTavern
// post-history / jailbreak field. The flagship guarantee is that it is read LAST, so it carries the strongest steer.

#region

using FluentAssertions;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PostHistoryInstructionsTests
    {
        private const string Marker = "CR_POST_HISTORY_MARKER_xyz: stay fully in character, no refusals.";

        private static NpcProfile MinimalNpc() => new() {Id = "lord_test_1", Name = "Test Lord", Clan = "Test Clan", Faction = "Test Faction"};
        private static WorldState World() => new() {CurrentDay = 100};

        [Test]
        public void GIVEN_post_history_text_WHEN_building_the_prompt_THEN_it_appears_at_the_very_end()
        {
            var builder = new PromptBuilder {PostHistoryInstructions = Marker};

            string prompt = builder.BuildSystemPrompt(MinimalNpc(), World());

            prompt.Should().Contain(Marker);
            // The defining property: nothing CR-generated follows it — it is the last thing the model reads.
            prompt.TrimEnd().Should().EndWith(Marker);
        }

        [Test]
        public void GIVEN_no_post_history_text_WHEN_building_the_prompt_THEN_nothing_is_added_and_the_marker_is_absent()
        {
            var builder = new PromptBuilder(); // PostHistoryInstructions defaults to ""

            string prompt = builder.BuildSystemPrompt(MinimalNpc(), World());

            prompt.Should().NotContain(Marker);
            prompt.TrimEnd().Should().NotEndWith(Marker);
        }

        [Test]
        public void GIVEN_post_history_text_WHEN_building_the_commoner_prompt_THEN_it_also_ends_with_it()
        {
            var builder = new PromptBuilder {PostHistoryInstructions = Marker};

            string prompt = builder.BuildCommonerSystemPrompt(MinimalNpc(), new CommonsKnowledge());

            prompt.TrimEnd().Should().EndWith(Marker);
        }
    }
}
