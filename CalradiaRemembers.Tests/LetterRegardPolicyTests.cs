// Code written by Gabriel Mailhot, 28/09/2026.
// Regard earned by letter. Lordfadooboo (Nexus, 28/09/2026): "seems off to be exchanging letters with someone a dozen
// times and yet when you meet them again they are still only a +1 or something like that. perhaps you wouldnt get as
// much through letters though which would make complete sense." Ruled by Gabriel the same day: a warm letter earns +1
// at most once every 5 days per correspondent, correspondence alone never lifts regard past "friendly regard", and a
// cold or insulting letter costs 1. The character judges the letter in the call that already decides the reply; the
// mod decides what it is worth. If this is wrong, either letters stay worthless (the report), or a player farms regard
// by writing a letter a day and never needs to meet anyone, which is what the chat's own gate exists to prevent.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterRegardPolicyTests
    {
        private static LetterRegardFacts Facts(LetterTone tone, int regard = 0, int? lastGainDay = null, int today = 100,
                                               RelationWarmth warmth = RelationWarmth.Normal)
            => new LetterRegardFacts {Tone = tone, CurrentRegard = regard, LastGainDay = lastGainDay, Today = today, Warmth = warmth};

        // Lordfadooboo's report: a dozen kind letters and the character is where they started.
        [Test]
        public void GIVEN_a_warm_letter_WHEN_it_is_read_THEN_the_writer_is_thought_of_a_little_better()
        {
            LetterRegardPolicy.Shift(Facts(LetterTone.Warmer)).Should().Be(1);
        }

        // Gabriel: +1 at most every 5 days per correspondent. Without it a letter a day is +1 a day, faster than
        // conversation, which the report itself says it should not be.
        [Test]
        public void GIVEN_a_warm_letter_soon_after_the_last_one_that_counted_WHEN_read_THEN_it_earns_nothing_more()
        {
            LetterRegardPolicy.Shift(Facts(LetterTone.Warmer, lastGainDay: 97, today: 100)).Should().Be(0);
            LetterRegardPolicy.Shift(Facts(LetterTone.Warmer, lastGainDay: 95, today: 100)).Should().Be(1);
        }

        // The MCM warmth setting already speeds regard in conversation; letters follow the same dial.
        [TestCase(RelationWarmth.Normal, 5)]
        [TestCase(RelationWarmth.Easier, 3)]
        [TestCase(RelationWarmth.VeryEasy, 1)]
        public void GIVEN_the_warmth_setting_WHEN_the_wait_between_two_letter_gains_is_asked_THEN_warmer_settings_wait_less(
            RelationWarmth warmth, int days)
        {
            LetterRegardPolicy.CooldownDaysFor(warmth).Should().Be(days);
        }

        // Gabriel: letters alone stop at "friendly regard"; what lies beyond is won in person.
        [Test]
        public void GIVEN_a_correspondent_already_at_friendly_regard_WHEN_a_warm_letter_is_read_THEN_it_earns_nothing()
        {
            LetterRegardPolicy.Shift(Facts(LetterTone.Warmer, regard: Regard.FriendlyTier)).Should().Be(0);
            LetterRegardPolicy.Shift(Facts(LetterTone.Warmer, regard: Regard.FriendlyTier - 1)).Should().Be(1);
        }

        // An insulting letter is felt, and not on a timer: losing regard is quick, as it is in conversation.
        [Test]
        public void GIVEN_a_cold_letter_WHEN_read_THEN_it_costs_a_point_even_right_after_another()
        {
            LetterRegardPolicy.Shift(Facts(LetterTone.Cooler, regard: 5, lastGainDay: 100, today: 100)).Should().Be(-1);
        }

        // Letters alone cannot drive a correspondent past "wary": deep enmity takes deeds, not ink.
        [Test]
        public void GIVEN_a_correspondent_already_wary_WHEN_a_cold_letter_is_read_THEN_it_costs_nothing_more()
        {
            LetterRegardPolicy.Shift(Facts(LetterTone.Cooler, regard: Regard.WaryTier)).Should().Be(0);
        }

        // A letter that is neither, or a model that did not say, changes nothing: no judgement, no shift.
        [TestCase(LetterTone.Same)]
        [TestCase(LetterTone.Unjudged)]
        public void GIVEN_a_letter_judged_neither_warm_nor_cold_WHEN_read_THEN_nothing_changes(LetterTone tone)
        {
            LetterRegardPolicy.Shift(Facts(tone)).Should().Be(0);
        }

        // The character's judgement rides the reply as a control line; a weak model's spelling still counts.
        [TestCase("DELAY: 2\n\nDear Arwa...\n[LETTER_TONE] warmer", LetterTone.Warmer)]
        [TestCase("PASS: it insults my house.\n[letter_tone] Cooler", LetterTone.Cooler)]
        [TestCase("Dear Arwa...\n[LETTER_TONE] same", LetterTone.Same)]
        [TestCase("Dear Arwa, no line at all.", LetterTone.Unjudged)]
        [TestCase("Dear Arwa...\n[LETTER_TONE] delighted", LetterTone.Unjudged)]
        public void GIVEN_a_reply_WHEN_its_tone_line_is_read_THEN_the_judgement_is_found(string raw, LetterTone expected)
        {
            LetterRegardPolicy.ParseTone(raw).Should().Be(expected);
        }

        // The prompt (SDK) asks for the line and this parser (mod) reads it: two spellings of one marker. If they drift,
        // every letter reads Unjudged and letters are worthless again, with nothing failing.
        [Test]
        public void GIVEN_the_prompts_that_ask_for_the_judgement_WHEN_built_THEN_they_ask_for_the_line_this_parser_reads()
        {
            var npc = new NpcMemoryService.Core.Models.NpcProfile {Id = "npc_test", Name = "Ira", Faction = "Battania", Clan = "Fen Duran"};

            NpcMemoryService.Core.Prompts.LetterPromptBuilder.BuildPlayerLetterReplyDecisionMessage(npc, "A word.", "Arwa")
                .Should().Contain(LetterRegardPolicy.Marker + " warmer");
            NpcMemoryService.Core.Prompts.LetterPromptBuilder.BuildReplyLetterMessage(
                    npc, "Well met.", NpcMemoryService.Core.Models.LetterReason.RomanticCorrespondence, "Arwa")
                .Should().Contain(LetterRegardPolicy.Marker + " warmer");
        }

        // The line is for the mod: the player must never read "[LETTER_TONE] warmer" at the foot of a love letter.
        [Test]
        public void GIVEN_a_reply_carrying_its_tone_line_WHEN_the_letter_is_kept_THEN_the_line_is_gone()
        {
            LetterRegardPolicy.StripToneLine("Dear Arwa,\nWell met.\n[LETTER_TONE] warmer").Should().Be("Dear Arwa,\nWell met.");
        }
    }
}
