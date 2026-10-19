// Code written by Gabriel Mailhot, 09/07/2026.
// Guards JealousyTriggerPolicy.IsPlausibleCourtship, the gate ChatViewModel.Exchanges consults before
// ever calling JealousyBehavior.OnRomanticAct. Player report (v1.29.0): a male player did a male lord
// a service and the lord's WIFE sent jealous threats over a pair that could never be a courtship.
// Two independent guards fix it: pair plausibility (the courted NPC's authored orientation, never a
// flat same-sex-is-never-jealous rule now orientation is authorable) and act-tier plausibility
// (spoken flirtation counts only once it forms a pattern, never on the same turn as a quest reward,
// since the model tends to mislabel gratitude as flirting). Get either wrong and jealousy either
// fires on an impossible pairing again, or a real courtship goes unnoticed.

#region

using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class JealousyTriggerPolicyTests
    {
        // The exact v1.29.0 player report this policy was built to fix: a male lord's wife should
        // never be read as a rival to a man she cannot romantically compete with. Without this gate,
        // jealousy fires on a pairing the mod's own romance model says cannot exist.
        [Test]
        public void GIVEN_an_implausible_pair_WHEN_evaluating_courtship_THEN_no_jealousy_ever_fires()
        {
            // The reported case: a male player does a male lord a service; his wife is no rival to a man.
            // The host decides pair plausibility from the courted NPC's orientation (default: opposite-sex).
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isPlausiblePair: false, romanticEventCount: 5, completedQuestThisTurn: false)
                                 .Should().BeFalse("a pair that cannot be a courtship is no threat to anyone");
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Intimacy, isPlausiblePair: false, romanticEventCount: 5, completedQuestThisTurn: false)
                                 .Should().BeFalse();
        }

        // Fixes the v1.29.1 gate's blind spot (ROADMAP): the old rule judged rivalry by raw sex, so a
        // deliberately enabled same-sex romance (via authored orientation) was invisible to spouses
        // and rivals. isPlausiblePair now follows the courted NPC's real orientation, not a hardcoded
        // opposite-sex assumption.
        [Test]
        public void GIVEN_an_orientation_sanctioned_same_sex_pair_WHEN_evaluating_THEN_it_counts_like_any_courtship()
        {
            // Since orientation became authorable, the host passes plausible=true for a same-sex pair the
            // courted NPC's orientation truly allows: such a romance rouses jealousy like any other.
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Intimacy, isPlausiblePair: true, romanticEventCount: 1, completedQuestThisTurn: false)
                                 .Should().BeTrue("an enabled same-sex romance is a real courtship");
        }

        // "Words nudge, deeds prove" (production doc): a single warm word is not yet courtship, so a
        // one-off pleasantry must not be able to rouse a jealous party on its own.
        [Test]
        public void GIVEN_a_first_ever_flirt_WHEN_evaluating_courtship_THEN_no_jealousy_fires()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isPlausiblePair: true, romanticEventCount: 1, completedQuestThisTurn: false)
                                 .Should().BeFalse("one warm word is a pleasantry, not courtship");
        }

        // Pins the exact boundary of FlirtPatternMinimum (3): a second flirt is still only an echo of
        // the first, but once flirtation repeats a third time it stops reading as pleasantries and
        // becomes the pattern that makes it real courtship worth a jealous reaction.
        [Test]
        public void GIVEN_a_repeated_flirtation_pattern_WHEN_evaluating_THEN_jealousy_may_fire()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isPlausiblePair: true, romanticEventCount: 3, completedQuestThisTurn: false)
                                 .Should().BeTrue();
        }

        // The lower half of the FlirtPatternMinimum (3) boundary: two recorded flirts must NOT yet
        // count, the model mislabels warm small talk as flirtation readily enough that a pair of
        // stamps is still not a courtship pattern.
        [Test]
        public void GIVEN_only_two_flirts_WHEN_evaluating_THEN_no_jealousy_fires_yet()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isPlausiblePair: true, romanticEventCount: 2, completedQuestThisTurn: false)
                                 .Should().BeFalse("two warm conversations are not yet a courtship pattern");
        }

        // The other half of the v1.29.0 root cause: the model sometimes stamps warm thanks at a
        // reward hand-over as an [EVENT] flirt. Without this guard, completing a quest for someone
        // could itself provoke their jealous spouse, the exact bug the player reported.
        [Test]
        public void GIVEN_a_flirt_stamped_on_a_quest_turn_in_WHEN_evaluating_THEN_it_is_read_as_gratitude_never_courtship()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isPlausiblePair: true, romanticEventCount: 5, completedQuestThisTurn: true)
                                 .Should().BeFalse("the model mislabels thanks at a reward hand-over as flirtation");
        }

        // Intimacy is a deed, not spoken words, so it must never wait for a "pattern" the way flirt
        // does: a single real act is already enough to provoke a plausible jealous party.
        [Test]
        public void GIVEN_an_intimacy_deed_WHEN_evaluating_THEN_it_always_counts_even_on_a_first_event()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Intimacy, isPlausiblePair: true, romanticEventCount: 1, completedQuestThisTurn: false)
                                 .Should().BeTrue("intimacy is a deed, not words");
        }

        // The quest-turn-in exemption exists only to excuse mislabeled GRATITUDE (words); it must
        // never extend to a genuine deed. Confirms the two guards (pair, and flirt-vs-quest) are
        // independent and the quest check does not leak into the intimacy branch.
        [Test]
        public void GIVEN_an_intimacy_deed_on_a_quest_turn_in_WHEN_evaluating_THEN_it_still_counts()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Intimacy, isPlausiblePair: true, romanticEventCount: 1, completedQuestThisTurn: true)
                                 .Should().BeTrue("a real deed is not explained away by a reward hand-over");
        }
    }
}
