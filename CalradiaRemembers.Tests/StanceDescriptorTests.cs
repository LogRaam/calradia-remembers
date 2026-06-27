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
            StanceDescriptor.AffectionPhrase(50).Should().Be("You are genuinely fond of the player.");
            StanceDescriptor.AffectionPhrase(20).Should().Be("You warm to the player.");
            StanceDescriptor.AffectionPhrase(0).Should().BeNull();
            StanceDescriptor.AffectionPhrase(-20).Should().Be("You dislike the player.");
            StanceDescriptor.AffectionPhrase(-50).Should().Be("You bear the player real hatred.");
        }

        [Test]
        public void GIVEN_trust_across_the_range_WHEN_phrasing_THEN_each_tier_reads_right()
        {
            StanceDescriptor.TrustPhrase(40).Should().Be("You trust the player implicitly.");
            StanceDescriptor.TrustPhrase(15).Should().Be("You trust the player's word.");
            StanceDescriptor.TrustPhrase(0).Should().BeNull();
            StanceDescriptor.TrustPhrase(-15).Should().Be("You are wary of the player's word.");
            StanceDescriptor.TrustPhrase(-40).Should().Be("You deeply distrust the player.");
        }

        [Test]
        public void GIVEN_respect_across_the_range_WHEN_phrasing_THEN_each_tier_reads_right()
        {
            StanceDescriptor.RespectPhrase(45).Should().Be("You hold the player in high regard.");
            StanceDescriptor.RespectPhrase(20).Should().Be("You respect the player's worth.");
            StanceDescriptor.RespectPhrase(0).Should().BeNull();
            StanceDescriptor.RespectPhrase(-20).Should().Be("You think little of the player.");
            StanceDescriptor.RespectPhrase(-45).Should().Be("You hold the player in open contempt.");
        }

        [Test]
        public void GIVEN_fear_across_the_range_WHEN_phrasing_THEN_it_is_unipolar()
        {
            // Fear GRADUATES: the more afraid, the more ground given — a flicker defers on small things,
            // true dread grants what would never otherwise be conceded.
            StanceDescriptor.FearPhrase(85).Should().StartWith("You are gripped by fear of the player");
            StanceDescriptor.FearPhrase(85).Should().Contain("grant what you would"); // top tier = capitulation
            StanceDescriptor.FearPhrase(70).Should().StartWith("You are openly afraid of the player, and it shows");
            StanceDescriptor.FearPhrase(70).Should().Contain("yield ground"); // fear is voiced as conduct, not a hidden feeling
            StanceDescriptor.FearPhrase(70).Should().Contain("concede");
            StanceDescriptor.FearPhrase(35).Should().StartWith("You are wary and intimidated by the player");
            StanceDescriptor.FearPhrase(35).Should().Contain("tread");
            StanceDescriptor.FearPhrase(12).Should().StartWith("You feel a flicker of unease around the player");
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
            block.Should().Be("You are genuinely fond of the player.\nYou are wary of the player's word.");
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
            block.Should().Contain("respect the player's worth");
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
