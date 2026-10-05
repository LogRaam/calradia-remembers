// Code written by Gabriel Mailhot, 04/10/2026.
// Documents when a REFUSED beat is redone on the NSFW model. Gabriel (04/10/2026), playing the werewolf tale's
// scene in Enhanced mode: the narrator answered an explicit turn with the model's own safety refusal ("I cannot
// fulfill this request...") and the player had to switch the in-chat model to NSFW by hand. The MCM hint promises
// the NSFW model is "the fallback in Enhanced"; until then that fallback only caught a TRUNCATED beat, never a
// refused one, and the scene narrator had no fallback at all.
//
// WHY IT MATTERS: miss a refusal and the player reads a guardrail message under the narrator's or a character's
// name, then must change models by hand. Redo when nothing would change (the NSFW model IS the model that refused,
// or the player chose Roleplay or NSFW themselves) and every such turn pays a second call for the same refusal.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RefusalFallbackPolicyTests
    {
        private const string Refusal = "I cannot fulfill this request. My purpose is to provide safe and positive interactions.";
        private const string Prose = "deepseek/deepseek-chat";
        private const string Nsfw = "x-ai/grok-4";

        // STAKE: the reported case. In Enhanced mode a refused beat goes to the NSFW model the player configured,
        // without the player touching the model switch.
        [Test]
        public void GIVEN_enhanced_mode_and_a_distinct_nsfw_model_WHEN_the_beat_is_a_refusal_THEN_it_is_redone_on_the_nsfw_model()
        {
            RefusalFallbackPolicy.ShouldRedo(true, Prose, Nsfw, Refusal, "stop").Should().BeTrue();
        }

        // STAKE: an ordinary narrated beat must cost one call, as before.
        [Test]
        public void GIVEN_enhanced_mode_WHEN_the_beat_is_ordinary_prose_that_finished_cleanly_THEN_it_is_not_redone()
        {
            RefusalFallbackPolicy.ShouldRedo(true, Prose, Nsfw, "The wolf's tracks lead north, into the birches.", "stop").Should().BeFalse();
        }

        // STAKE: a provider's own filter or a cut-off reply is the same failure seen from the wire; an empty reply
        // shows the player nothing at all.
        [Test]
        public void GIVEN_enhanced_mode_WHEN_the_beat_was_filtered_cut_off_or_empty_THEN_it_is_redone()
        {
            RefusalFallbackPolicy.ShouldRedo(true, Prose, Nsfw, "The wolf's tracks", "content_filter").Should().BeTrue();
            RefusalFallbackPolicy.ShouldRedo(true, Prose, Nsfw, "The wolf's tracks", "length").Should().BeTrue();
            RefusalFallbackPolicy.ShouldRedo(true, Prose, Nsfw, "  ", "stop").Should().BeTrue();
        }

        // STAKE: a local server often sends no finish reason at all; a full reply without one is not a failure, or
        // every scene turn on such a server would be paid twice.
        [Test]
        public void GIVEN_a_full_reply_with_no_finish_reason_WHEN_checked_THEN_it_is_not_redone()
        {
            RefusalFallbackPolicy.ShouldRedo(true, Prose, Nsfw, "The wolf's tracks lead north.", null).Should().BeFalse();
        }

        // STAKE: Roleplay and NSFW are the player's own choice of model (Roleplay never auto-routes to NSFW, by
        // design): the fallback belongs to Enhanced only.
        [Test]
        public void GIVEN_the_player_chose_roleplay_or_nsfw_WHEN_the_beat_is_a_refusal_THEN_it_is_not_redone()
        {
            RefusalFallbackPolicy.ShouldRedo(false, Prose, Nsfw, Refusal, "stop").Should().BeFalse();
        }

        // STAKE: with no NSFW model of its own (blank, or one that says nothing for its provider) the NSFW model
        // resolves to the very model that refused; asking it again buys the same refusal twice.
        [Test]
        public void GIVEN_no_distinct_nsfw_model_WHEN_the_beat_is_a_refusal_THEN_it_is_not_redone()
        {
            RefusalFallbackPolicy.ShouldRedo(true, Prose, "", Refusal, "stop").Should().BeFalse();
            RefusalFallbackPolicy.ShouldRedo(true, Prose, Prose.ToUpperInvariant(), Refusal, "stop").Should().BeFalse();
        }
    }
}
