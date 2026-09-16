// Code written by Gabriel Mailhot, 16/09/2026.
// What these pin: that the narrative files the host injects into EVERY prompt stay the size the SDK's
// Compact budget guard assumes they are.
//
// DuskSymphony (Nexus, v2.5.7 then v2.5.9) cannot hold a conversation on an 8,192-token local model:
//
//   send_error: request (9496 tokens) exceeds the available context size (8192 tokens)
//
// LeanRealPromptBudgetTests over in the SDK was written FOR him, names his ceiling in its own comment, and
// was green throughout — because it built a PromptBuilder with no WorldDescription and no
// BehaviorGuidelinesOverride, while LlmServices sets both from NarrativeLoader on every real prompt.
// Between them those two files are over nine thousand characters that went into every prompt the game has
// ever sent and into nothing that guard ever measured. It is the third time that exact shape of blindness
// has been found here, and the second time in this very file's subject.
//
// The SDK repository must stand alone, so the guard there uses payloads of these SIZES rather than reading
// across repositories. This is the other half of that arrangement: if a file here grows, the budget measured
// there is no longer the budget sent, and this goes red first.

#region

using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ShippedNarrativeSizeTests
    {
        /// <summary>The sizes LeanRealPromptBudgetTests assumes. Change one, change both.</summary>
        private const int WorldTxtChars = 2432;

        private const int BehaviorGuidelinesChars = 6629;

        /// <summary>
        ///   Tolerance for ordinary editing. Wide enough that fixing a typo is not a failed build, tight
        ///   enough that a new paragraph — which is what actually costs a player their conversation — is.
        /// </summary>
        private const int Slack = 250;

        // world.txt goes into every prompt at every prompt level. A paragraph added here is a paragraph
        // removed from what a small local model has left for the conversation itself.
        [Test]
        public void GIVEN_world_txt_WHEN_measured_THEN_it_is_the_size_the_budget_guard_assumes()
        {
            Measure("world.txt").Should().BeCloseTo(WorldTxtChars, Slack,
                "LeanRealPromptBudgetTests measures the Compact budget with a payload of this size");
        }

        // The larger of the two, and the one most likely to be edited: it is where a modder or a ruling adds
        // guidance. Since 2026-09-15 a lord receives it narrowed to his own culture, which takes ~2.1k off
        // what reaches the model — but the FILE is still what the budget guard stands on.
        [Test]
        public void GIVEN_behavior_guidelines_WHEN_measured_THEN_it_is_the_size_the_budget_guard_assumes()
        {
            Measure("behavior_guidelines.txt").Should().BeCloseTo(BehaviorGuidelinesChars, Slack,
                "LeanRealPromptBudgetTests measures the Compact budget with a payload of this size");
        }

        // The two empty ones are empty ON PURPOSE (player_description.txt ships blank so the mod reads the
        // live hero instead, and post_history_instructions.txt is an opt-in modder hook). If either ever
        // gains content it silently enters every prompt, so the budget assumption must be revisited — which
        // is exactly what this failing will say.
        [Test]
        public void GIVEN_the_optional_narrative_files_WHEN_measured_THEN_they_are_still_empty()
        {
            Measure("player_description.txt").Should().Be(0);
            Measure("post_history_instructions.txt").Should().Be(0);
        }

        #region private

        private static int Measure(string fileName)
        {
            string path = Path.Combine(NarrativeDirectory, fileName);

            File.Exists(path).Should().BeTrue(
                $"{fileName} ships with the mod, is read by NarrativeLoader, and is copied beside this test "
              + $"assembly by the .csproj. Looked in: {NarrativeDirectory}");

            return File.ReadAllText(path).Length;
        }

        /// <summary>
        ///   Beside the test assembly, where the .csproj copies the shipped files.
        ///
        ///   The first version located them from this file's own <c>[CallerFilePath]</c> — the path the
        ///   COMPILER saw. That is correct under <c>dotnet test</c> and wrong under NCrunch, which compiles
        ///   from its own workspace copy and does not carry these .txt files into it, so all three tests were
        ///   red in the runner Gabriel actually watches while green on the command line. A test that passes in
        ///   one runner and fails in another is measuring the runner.
        /// </summary>
        private static string NarrativeDirectory
            => Path.Combine(AppContext.BaseDirectory, "ShippedNarrative");

        #endregion
    }
}
