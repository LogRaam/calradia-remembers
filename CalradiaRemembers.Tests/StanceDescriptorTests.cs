// Code written by Gabriel Mailhot, 21/06/2026.
// Protects how a stance reads in the prompt — per-axis tiers, and that believable COMBINATIONS compose
// correctly (the whole point of the multi-axis system). A drift would have NPCs misread their own
// posture, or voice a neutral feeling as a strong one.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StanceDescriptorTests
    {
        [Test]
        public void GIVEN_warmth_across_the_range_WHEN_phrasing_affection_THEN_each_tier_reads_right()
        {
            StanceDescriptor.AffectionPhrase(50).Should().Be("They are genuinely fond of you.");
            StanceDescriptor.AffectionPhrase(20).Should().Be("They warm to you.");
            StanceDescriptor.AffectionPhrase(0).Should().BeNull();
            StanceDescriptor.AffectionPhrase(-20).Should().Be("They dislike you.");
            StanceDescriptor.AffectionPhrase(-50).Should().Be("They bear you real hatred.");
        }

        [Test]
        public void GIVEN_trust_across_the_range_WHEN_phrasing_THEN_each_tier_reads_right()
        {
            StanceDescriptor.TrustPhrase(40).Should().Be("They trust you implicitly.");
            StanceDescriptor.TrustPhrase(15).Should().Be("They trust your word.");
            StanceDescriptor.TrustPhrase(0).Should().BeNull();
            StanceDescriptor.TrustPhrase(-15).Should().Be("They are wary of your word.");
            StanceDescriptor.TrustPhrase(-40).Should().Be("They deeply distrust you.");
        }

        [Test]
        public void GIVEN_respect_across_the_range_WHEN_phrasing_THEN_each_tier_reads_right()
        {
            StanceDescriptor.RespectPhrase(45).Should().Be("They hold you in high regard.");
            StanceDescriptor.RespectPhrase(20).Should().Be("They respect your worth.");
            StanceDescriptor.RespectPhrase(0).Should().BeNull();
            StanceDescriptor.RespectPhrase(-20).Should().Be("They think little of you.");
            StanceDescriptor.RespectPhrase(-45).Should().Be("They hold you in open contempt.");
        }

        [Test]
        public void GIVEN_fear_across_the_range_WHEN_phrasing_THEN_it_is_unipolar()
        {
            StanceDescriptor.FearPhrase(70).Should().Be("They are openly afraid of you.");
            StanceDescriptor.FearPhrase(35).Should().Be("They are wary and intimidated by you.");
            StanceDescriptor.FearPhrase(12).Should().Be("They feel a flicker of unease around you.");
            StanceDescriptor.FearPhrase(0).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_wholly_neutral_npc_WHEN_summarizing_THEN_the_block_is_skipped()
        {
            StanceDescriptor.Summarize(new NpcStance()).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_mixed_stance_WHEN_summarizing_THEN_only_the_axes_that_matter_are_voiced()
        {
            //Arrange — fond, but wary of the player's word; respect and fear neutral.
            NpcStance s = new NpcStance { Affection = 50, Trust = -25, Respect = 0, Fear = 0 };

            //Act
            string block = StanceDescriptor.Summarize(s);

            //Assert
            block.Should().Be("They are genuinely fond of you.\nThey are wary of your word.");
        }

        // ── Believable combinations (the point of the multi-axis system) ───────

        [Test]
        public void GIVEN_a_beaten_rival_WHEN_summarizing_THEN_hatred_respect_and_fear_coexist()
        {
            //Arrange — hates you, yet respects your prowess and fears you: a defeated foe.
            NpcStance s = new NpcStance { Affection = -50, Trust = 0, Respect = 60, Fear = 70 };

            //Act
            string block = StanceDescriptor.Summarize(s);

            //Assert
            block.Should().Contain("real hatred");
            block.Should().Contain("high regard");
            block.Should().Contain("openly afraid");
            block.Should().NotContain("trust"); // trust is neutral, so it is left unsaid
        }

        [Test]
        public void GIVEN_a_charming_rogue_the_lord_likes_but_cannot_trust_WHEN_summarizing_THEN_warmth_and_suspicion_coexist()
        {
            //Arrange — fond of you and impressed, but does not trust your word.
            NpcStance s = new NpcStance { Affection = 45, Trust = -45, Respect = 30, Fear = 0 };

            //Act
            string block = StanceDescriptor.Summarize(s);

            //Assert
            block.Should().Contain("genuinely fond");
            block.Should().Contain("deeply distrust");
            block.Should().Contain("respect your worth");
        }

        [Test]
        public void GIVEN_a_feared_but_unloved_overlord_WHEN_summarizing_THEN_fear_and_respect_carry_without_warmth()
        {
            //Arrange — no affection, but real respect and fear: cowed deference.
            NpcStance s = new NpcStance { Affection = 0, Trust = 0, Respect = 50, Fear = 50 };

            //Act
            string block = StanceDescriptor.Summarize(s);

            //Assert
            block.Should().Contain("high regard");
            block.Should().Contain("wary and intimidated");
            block.Should().NotContain("fond");
            block.Should().NotContain("hatred");
        }
    }
}
