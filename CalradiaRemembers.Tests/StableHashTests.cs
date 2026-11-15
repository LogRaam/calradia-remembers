// Code written by Gabriel Mailhot, 15/11/2026.
// What StableHash decides: the number behind every draw the mod never saves (a character's habit of speech, the
// gestures they love, their folk tale, their first impression, which letter a lord starts with). Those draws are
// remembered only by drawing them again, so the number must never move: not between two sessions, not between two
// machines, and not when the nine copies of this loop became one (Gabriel, 15/11/2026). The expected numbers below were
// computed outside the code (FNV-1a by hand, in a script), before the copies were replaced. If one fails, every
// character in every save speaks, loves and tells differently after the update.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Folklore;
using CalradiaRemembers.Logic.Hashing;
using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StableHashTests
    {
        // STAKE: these are the numbers the game drew from before the copies became one. A different number here is a
        // different voice, taste and tale for every character already in a save.
        [TestCase("", 2166136261u)]
        [TestCase("lord_1_1", 1293798234u)]
        [TestCase("Arwa of the Banu Ara", 195883270u)]
        public void GIVEN_a_text_WHEN_its_number_is_taken_THEN_it_is_the_number_it_always_was(string text, uint expected)
        {
            StableHash.Of(text).Should().Be(expected);
        }

        // STAKE: a hero with no id yet must not crash a prompt; it draws as the empty text does.
        [Test]
        public void GIVEN_no_text_WHEN_its_number_is_taken_THEN_it_draws_as_the_empty_text()
        {
            StableHash.Of(null).Should().Be(StableHash.Of(""));
        }

        // STAKE: the draws that existed before still give what they gave: the seed of a character's voice and loved
        // gestures, the folk tale picked, the first impression's roll of 0..99.
        [Test]
        public void GIVEN_the_draws_that_existed_before_WHEN_taken_for_a_known_lord_THEN_they_are_unchanged()
        {
            IntimateActPolicy.StableSeed("lord_1_1").Should().Be(unchecked((int) 1293798234u));
            FolkTalePolicy.StableHash("lord_1_1").Should().Be(1293798234u);
            FirstImpressionPolicy.StableRoll("lord_1_1").Should().Be((int) (1293798234u % 100u));
        }
    }
}
