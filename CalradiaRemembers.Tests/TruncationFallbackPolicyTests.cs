// Code written by Gabriel Mailhot, 15/08/2026.
// Documents the PROSE FALLBACK decision (Prose + Interpreter composition): whether a beat the prose model just
// produced should be redone, in one INTEGRATED call, on the configured fallback (grok) model. The prose call is
// issued with the SDK's internal double-budget retry disabled so a truncated reply returns immediately; this is
// what decides, from that reply's finish reason alone, whether the fast failure earns the fallback.
//
// WHY IT MATTERS: get this wrong in either direction and the safety net fails silently. Fall back on a CLEAN
// "stop" finish and every Prose+Interpret turn pays for a redundant second call on the fallback model for no
// reason. Fail to fall back on a genuine truncation (length/content_filter/empty) and the mod's whole reason for
// building this feature - completing a hard explicit beat the prose model choked on - never fires.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class TruncationFallbackPolicyTests
    {
        // STAKE: "length" is the provider cutting the reply off at the token budget mid-sentence - the exact
        // failure mode this feature exists to catch (a heavy internal reasoner burning the budget before
        // finishing the prose). Missing this means the truncated half-sentence just ships to the player.
        [Test]
        public void GIVEN_a_length_finish_reason_WHEN_a_fallback_model_is_configured_THEN_it_falls_back()
        {
            TruncationFallbackPolicy.ShouldFallBack("length", true).Should().BeTrue();
        }

        // STAKE: a content-filter cut (OpenRouterClient.IsContentFiltered: "content_filter"/"content-filter"/
        // "moderation") reads to the player as the MOD censoring the scene, per OpenRouterClient's own retry
        // comment. The fallback model is chosen for being the least likely to trip this, so it must fire here too.
        [Test]
        public void GIVEN_a_content_filter_finish_reason_WHEN_a_fallback_model_is_configured_THEN_it_falls_back()
        {
            TruncationFallbackPolicy.ShouldFallBack("content_filter", true).Should().BeTrue();
        }

        // STAKE: OpenRouterClient's own retry gate treats a null/empty finish reason (or empty content) as an
        // incomplete reply, never a legitimate empty turn. This policy must not let a null finish reason read as
        // "stop", or a reasoning model that spent its whole budget thinking would ship silence instead of a beat.
        [Test]
        public void GIVEN_a_null_finish_reason_WHEN_a_fallback_model_is_configured_THEN_it_falls_back()
        {
            TruncationFallbackPolicy.ShouldFallBack(null, true).Should().BeTrue();
        }

        // STAKE: this is the byte-for-byte-unchanged path the feature promises to preserve. A normal, complete
        // reply must NEVER trigger the fallback's extra call, or every ordinary Prose+Interpret turn pays for a
        // redundant second LLM call on the fallback model for no reason.
        [Test]
        public void GIVEN_a_stop_finish_reason_WHEN_a_fallback_model_is_configured_THEN_it_does_not_fall_back()
        {
            TruncationFallbackPolicy.ShouldFallBack("stop", true).Should().BeFalse();
        }

        // STAKE: the exact rp_bench scene-4 report that prompted this rule - a captor Director beat came back on a
        // clean "stop" finish with a full [NARRATION] block but NO spoken line ("Susada: (no spoken line)"). A pure
        // finish-reason gate treats that as complete and ships a silent captor; the fallback must fire so the driving
        // character is redone into the scene rather than vanishing from it.
        [Test]
        public void GIVEN_a_clean_stop_but_no_spoken_dialogue_WHEN_a_fallback_model_is_configured_THEN_it_falls_back()
        {
            TruncationFallbackPolicy.ShouldFallBack("stop", true, false).Should().BeTrue();
        }

        // STAKE: the no-dialogue trigger must still respect the "nowhere to send the redo" guard, exactly like the
        // finish-reason path: with no fallback model configured a silent beat has to ship as-is rather than call out
        // to an empty model id.
        [Test]
        public void GIVEN_no_spoken_dialogue_WHEN_no_fallback_model_is_configured_THEN_it_does_not_fall_back()
        {
            TruncationFallbackPolicy.ShouldFallBack("stop", false, false).Should().BeFalse();
        }

        // STAKE: pins the byte-for-byte-unchanged happy path of the 3-arg overload: an ordinary complete beat (clean
        // "stop" AND a real spoken line) must never pay for the fallback's extra call. This is the common case on
        // every non-degenerate turn, so a regression here would tax every Prose+Interpret turn.
        [Test]
        public void GIVEN_a_clean_stop_with_a_spoken_line_WHEN_a_fallback_model_is_configured_THEN_it_does_not_fall_back()
        {
            TruncationFallbackPolicy.ShouldFallBack("stop", true, true).Should().BeFalse();
        }

        // STAKE: the MCM field defaults BLANK, and blank means "use the Action Interpreter model" (resolved one
        // layer up, by LlmServices.ResolveProseFallbackModel), never "no fallback configured at all". But if
        // that resolution somehow still yields nothing, there is nowhere to send the redo, so a truncated beat
        // must ship as-is rather than call out to an empty model id.
        [Test]
        public void GIVEN_a_truncated_finish_reason_WHEN_no_fallback_model_is_configured_THEN_it_does_not_fall_back()
        {
            TruncationFallbackPolicy.ShouldFallBack("length", false).Should().BeFalse();
        }
    }
}
