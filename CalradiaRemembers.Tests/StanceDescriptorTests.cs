// Code written by Gabriel Mailhot, 21/06/2026.
// Protects how a stance reads in the prompt — per-axis tiers, and that believable COMBINATIONS compose
// correctly (the whole point of the multi-axis system). A drift would have NPCs misread their own
// posture, or voice a neutral feeling as a strong one.
//
// SECOND PERSON is load-bearing, not stylistic (StanceDescriptor's own header): a third-person phrasing
// was observed to make the model FLIP the feeling onto the player ("a feared lord told the player THEY
// looked afraid"). Every phrase this file pins must stay "You are/warm to/trust/fear the player."

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
        // AffectionPhrase itself is no longer wired into Summarize (P0.2 fixed the double-voiced-regard
        // bug below), but is kept as documented, still-public phrasing: this is the only test left that
        // would catch a regression if it is ever reused.
        [Test]
        public void GIVEN_warmth_across_the_range_WHEN_phrasing_affection_THEN_each_tier_reads_right()
        {
            StanceDescriptor.AffectionPhrase(50).Should().Be("You are genuinely fond of the player.");
            StanceDescriptor.AffectionPhrase(20).Should().Be("You warm to the player.");
            StanceDescriptor.AffectionPhrase(0).Should().BeNull();
            StanceDescriptor.AffectionPhrase(-20).Should().Be("You dislike the player.");
            StanceDescriptor.AffectionPhrase(-50).Should().Be("You bear the player real hatred.");
        }

        // Pins the 5-tier Trust ladder (Strong/Mild each pole + neutral gap) that DOES feed Summarize:
        // wrong here changes what every NPC actually says about the player's trustworthiness.
        [Test]
        public void GIVEN_trust_across_the_range_WHEN_phrasing_THEN_each_tier_reads_right()
        {
            StanceDescriptor.TrustPhrase(40).Should().Be("You trust the player implicitly.");
            StanceDescriptor.TrustPhrase(15).Should().Be("You trust the player's word.");
            StanceDescriptor.TrustPhrase(0).Should().BeNull();
            StanceDescriptor.TrustPhrase(-15).Should().Be("You are wary of the player's word.");
            StanceDescriptor.TrustPhrase(-40).Should().Be("You deeply distrust the player.");
        }

        // Pins the 5-tier Respect ladder, same threshold discipline as Trust.
        [Test]
        public void GIVEN_respect_across_the_range_WHEN_phrasing_THEN_each_tier_reads_right()
        {
            StanceDescriptor.RespectPhrase(45).Should().Be("You hold the player in high regard.");
            StanceDescriptor.RespectPhrase(20).Should().Be("You respect the player's worth.");
            StanceDescriptor.RespectPhrase(0).Should().BeNull();
            StanceDescriptor.RespectPhrase(-20).Should().Be("You think little of the player.");
            StanceDescriptor.RespectPhrase(-45).Should().Be("You hold the player in open contempt.");
        }

        // Fear is voiced as VISIBLE CONDUCT, not a private feeling (StanceDescriptor's own doc): each tier
        // must actually escalate the NPC's described behaviour (defers -> yields ground -> capitulates),
        // or a terrified NPC would read no differently from a merely wary one.
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

        // A wholly neutral NPC must produce NO posture block at all (null, not an empty one), otherwise
        // every conversation would carry a pointless blank section in the prompt.
        [Test]
        public void GIVEN_a_wholly_neutral_npc_WHEN_summarizing_THEN_the_block_is_skipped()
        {
            StanceDescriptor.Summarize(new NpcStance()).Should().BeNull();
        }

        // The P0.2 fix pinned directly: Affection must NEVER appear here even when non-neutral, because
        // the CURRENT STANCE line already states that exact number in words once
        // (PromptBuilder.DescribePersonalRegard): voicing it again here was the bug this reform closed.
        [Test]
        public void GIVEN_a_mixed_stance_WHEN_summarizing_THEN_only_the_axes_that_matter_are_voiced()
        {
            //Arrange — fond (Affection), but wary of the player's word; respect and fear neutral.
            NpcStance s = new NpcStance { Affection = 50, Trust = -25, Respect = 0, Fear = 0 };

            //Act
            string block = StanceDescriptor.Summarize(s);

            //Assert — Affection is NEVER voiced here (P0.2): it is already stated once, in words, by the
            // CURRENT STANCE line elsewhere in the prompt (PromptBuilder.DescribePersonalRegard), so only
            // the remaining non-neutral axis (Trust) appears.
            block.Should().Be("You are wary of the player's word.");
        }

        // ── Believable combinations (the point of the multi-axis system) ───────

        // The multi-axis system's whole point: a defeated foe can respect AND fear the player while still
        // hating them: Summarize must compose that believable combination without restating the hatred
        // itself (already said once, elsewhere).
        [Test]
        public void GIVEN_a_beaten_rival_WHEN_summarizing_THEN_respect_and_fear_coexist_without_restating_affection()
        {
            //Arrange — hates you (Affection), yet respects your prowess and fears you: a defeated foe.
            NpcStance s = new NpcStance { Affection = -50, Trust = 0, Respect = 60, Fear = 70 };

            //Act
            string block = StanceDescriptor.Summarize(s);

            //Assert — the hatred itself is stated once elsewhere (CURRENT STANCE), not restated here.
            block.Should().NotContain("hatred");
            block.Should().Contain("high regard");
            block.Should().Contain("openly afraid");
            block.Should().NotContain("trust"); // trust is neutral, so it is left unsaid
        }

        // Another believable combination guard: fondness plus distrust is not a contradiction the composer
        // should collapse or garble, and warmth still must not leak in from AffectionPhrase.
        [Test]
        public void GIVEN_a_charming_rogue_the_lord_likes_but_cannot_trust_WHEN_summarizing_THEN_distrust_and_respect_show_without_restating_warmth()
        {
            //Arrange — fond of you and impressed (Affection), but does not trust your word.
            NpcStance s = new NpcStance { Affection = 45, Trust = -45, Respect = 30, Fear = 0 };

            //Act
            string block = StanceDescriptor.Summarize(s);

            //Assert — warmth is not restated here (P0.2); only Trust/Respect/Fear are this note's business.
            block.Should().NotContain("fond");
            block.Should().Contain("deeply distrust");
            block.Should().Contain("respect the player's worth");
        }

        // Cowed deference with zero personal warmth: pins that Fear/Respect alone can carry a full posture
        // block with nothing implying affection either way.
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
