// Code written by Gabriel Mailhot, 15/11/2026.
// What CompressionMergePolicy decides: what a character's history becomes when a memory compression pass comes back,
// given that the game went on while the pass was away. VonLemberg (Discord, 15/11/2026, source audit of 2.8.10): "Remove
// an injected memory during compression. After completion, it must remain absent from both active memories and
// background context. Preserve concurrent additions and in-place summary enrichments." Icezera (Discord, 15/11/2026):
// memories written by hand went missing. If this is wrong, a memory a mod or the player took back returns, or one made
// during the pass is lost, or a character's background speaks of something that was withdrawn.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompressionMergePolicyTests
    {
        private static readonly string First = new string("first meeting".ToCharArray());
        private static readonly string Skirmish = new string("a skirmish".ToCharArray());
        private static readonly string Rumor = new string("a rumor another mod told".ToCharArray());

        // The ordinary pass: nothing happened meanwhile, the dropped memory goes and the rest stays in order.
        [Test]
        public void GIVEN_nothing_happened_meanwhile_WHEN_the_pass_returns_THEN_only_what_it_dropped_is_gone()
        {
            CompressionMergeOutcome<string> outcome = CompressionMergePolicy.Resolve(new CompressionMerge<string> {
                Before = new[] {First, Skirmish, Rumor}, Kept = new[] {First, Rumor}, Live = new[] {First, Skirmish, Rumor}
            });

            outcome.Stale.Should().BeFalse();
            outcome.History.Should().Equal(First, Rumor);
        }

        // THE AUDIT: a mod took its rumor back while the pass was away, and the pass had kept it. It must stay gone.
        [Test]
        public void GIVEN_a_kept_memory_taken_back_meanwhile_WHEN_the_pass_returns_THEN_it_does_not_come_back()
        {
            CompressionMergeOutcome<string> outcome = CompressionMergePolicy.Resolve(new CompressionMerge<string> {
                Before = new[] {First, Skirmish, Rumor}, Kept = new[] {First, Rumor}, Live = new[] {First, Skirmish}
            });

            outcome.Stale.Should().BeFalse();
            outcome.History.Should().Equal(First);
        }

        // THE AUDIT, second half: the rumor taken back had been folded into the pass's summary, so that summary would
        // put it into the character's background. The whole pass is set aside.
        [Test]
        public void GIVEN_a_memory_folded_into_the_summary_taken_back_meanwhile_WHEN_the_pass_returns_THEN_nothing_of_it_is_applied()
        {
            CompressionMergeOutcome<string> outcome = CompressionMergePolicy.Resolve(new CompressionMerge<string> {
                Before = new[] {First, Skirmish, Rumor}, Kept = new[] {First, Skirmish}, Live = new[] {First, Skirmish}
            });

            outcome.Stale.Should().BeTrue();
            outcome.History.Should().Equal(First, Skirmish);
        }

        // STAKE (Icezera): a memory made while the pass was away, wherever it was filed, is never lost. cr.memory_add
        // files a memory dated in the past among the old ones, not at the end.
        [Test]
        public void GIVEN_a_memory_added_among_the_old_ones_meanwhile_WHEN_the_pass_returns_THEN_it_is_kept_in_its_place()
        {
            const string written = "we fought side by side";

            CompressionMergeOutcome<string> outcome = CompressionMergePolicy.Resolve(new CompressionMerge<string> {
                Before = new[] {First, Skirmish, Rumor}, Kept = new[] {First, Rumor}, Live = new[] {First, written, Skirmish, Rumor, "spoke today"}
            });

            outcome.History.Should().Equal(First, written, Rumor, "spoke today");
        }

        // STAKE: a memory rewritten in place meanwhile (a conversation's summary enriched, cr.memory_set) keeps its new
        // words; the pass never writes the old ones back over it.
        [Test]
        public void GIVEN_a_kept_memory_rewritten_meanwhile_WHEN_the_pass_returns_THEN_its_new_words_stand()
        {
            const string enriched = "first meeting, at the feast in Pravend";

            CompressionMergeOutcome<string> outcome = CompressionMergePolicy.Resolve(new CompressionMerge<string> {
                Before = new[] {First, Skirmish, Rumor}, Kept = new[] {First, Rumor}, Live = new[] {enriched, Skirmish, Rumor}
            });

            outcome.Stale.Should().BeFalse();
            outcome.History.Should().Equal(enriched, Rumor);
        }

        // STAKE: two memories with the same words are two memories. Dropping one must not take its twin.
        [Test]
        public void GIVEN_two_memories_with_the_same_words_WHEN_the_pass_drops_one_THEN_its_twin_stays()
        {
            string twin = new string(Skirmish.ToCharArray());

            CompressionMergeOutcome<string> outcome = CompressionMergePolicy.Resolve(new CompressionMerge<string> {
                Before = new[] {First, Skirmish, twin}, Kept = new[] {First, twin}, Live = new[] {First, Skirmish, twin}
            });

            outcome.History.Should().HaveCount(2);
            ReferenceEquals(outcome.History[1], twin).Should().BeTrue();
        }
    }
}
