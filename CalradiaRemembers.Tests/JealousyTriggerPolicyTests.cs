// Code written by Gabriel Mailhot, 09/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class JealousyTriggerPolicyTests
    {
        [Test]
        public void GIVEN_a_same_sex_pair_WHEN_evaluating_courtship_THEN_no_jealousy_ever_fires()
        {
            // The reported case: a male player does a male lord a service; his wife is no rival to a man.
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isOppositeSexPair: false, romanticEventCount: 5, completedQuestThisTurn: false)
                                 .Should().BeFalse("a same-sex pair is no threat to the marriage in this romance model");
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Intimacy, isOppositeSexPair: false, romanticEventCount: 5, completedQuestThisTurn: false)
                                 .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_first_ever_flirt_WHEN_evaluating_courtship_THEN_no_jealousy_fires()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isOppositeSexPair: true, romanticEventCount: 1, completedQuestThisTurn: false)
                                 .Should().BeFalse("one warm word is a pleasantry, not courtship");
        }

        [Test]
        public void GIVEN_a_repeated_flirtation_pattern_WHEN_evaluating_THEN_jealousy_may_fire()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isOppositeSexPair: true, romanticEventCount: 2, completedQuestThisTurn: false)
                                 .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_flirt_stamped_on_a_quest_turn_in_WHEN_evaluating_THEN_it_is_read_as_gratitude_never_courtship()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Flirt, isOppositeSexPair: true, romanticEventCount: 5, completedQuestThisTurn: true)
                                 .Should().BeFalse("the model mislabels thanks at a reward hand-over as flirtation");
        }

        [Test]
        public void GIVEN_an_intimacy_deed_WHEN_evaluating_THEN_it_always_counts_even_on_a_first_event()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Intimacy, isOppositeSexPair: true, romanticEventCount: 1, completedQuestThisTurn: false)
                                 .Should().BeTrue("intimacy is a deed, not words");
        }

        [Test]
        public void GIVEN_an_intimacy_deed_on_a_quest_turn_in_WHEN_evaluating_THEN_it_still_counts()
        {
            JealousyTriggerPolicy.IsPlausibleCourtship(RomanticActTier.Intimacy, isOppositeSexPair: true, romanticEventCount: 1, completedQuestThisTurn: true)
                                 .Should().BeTrue("a real deed is not explained away by a reward hand-over");
        }
    }
}
