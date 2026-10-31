// Code written by Gabriel Mailhot, 30/10/2026.
// Which fixed text a lord's unbidden friendly letter carries. tashmetu (Nexus, 30/10/2026) relayed two players with
// Bellum Civile's collection receiving a crowd of lords' letters, every one the same words; Gabriel (30/10/2026): fixed
// texts, no model call. If this is wrong, the crowd writes one letter again, or the same lord repeats himself, or a
// player's letter differs from one session to the next.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StanceFavorLetterTextTests
    {
        // STAKE: the reported crowd. Lords who turn friendly the same week must not all send the one same letter.
        [Test]
        public void GIVEN_many_lords_writing_their_first_friendly_letter_WHEN_the_texts_are_chosen_THEN_they_do_not_all_match()
        {
            var lords = new[] {"lord_1_1", "lord_1_3", "lord_2_1", "lord_3_5", "lord_4_2", "lord_5_1", "lord_6_4", "lord_1_14"};

            lords.Select(id => StanceFavorLetterText.For(id, 0)).Distinct().Count().Should().BeGreaterThan(2);
        }

        // STAKE: a lasting friendship must not mail the same words twice in a row, the regard audit's rule for threats.
        [Test]
        public void GIVEN_one_lord_WHEN_he_writes_again_THEN_each_letter_differs_from_the_one_before()
        {
            var sent = new List<string>();
            for (int i = 0; i < StanceFavorLetterText.Letters.Length; i++) sent.Add(StanceFavorLetterText.For("lord_1_1", i));

            sent.Distinct().Count().Should().Be(StanceFavorLetterText.Letters.Length);
        }

        // STAKE: the same save read in another session (or on another machine) gives the lord the same letter: the choice
        // must not ride on string.GetHashCode, which .NET randomises per process.
        [Test]
        public void GIVEN_the_same_lord_and_count_WHEN_chosen_twice_THEN_the_text_is_the_same()
        {
            // FNV-1a of "lord_2_1" is 3535331563, which starts his sequence at the second text: pinned, not recomputed.
            StanceFavorLetterText.For("lord_2_1", 0).Should().Be(StanceFavorLetterText.Letters[1]);
            StanceFavorLetterText.For("lord_2_1", 3).Should().Be(StanceFavorLetterText.Letters[4]);
            StanceFavorLetterText.For(null, -1).Should().NotBeNullOrEmpty();
        }
    }
}
