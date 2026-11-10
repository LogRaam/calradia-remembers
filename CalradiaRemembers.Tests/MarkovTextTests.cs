// Code written by Gabriel Mailhot, 10/11/2026.
// What MarkovText gives cr.inflate_history: new sentences in the manner of a campaign's real memories, the same ones for
// the same seed. If it is wrong, the inflated save is either the same sentences copied over and over (which flatters
// long-window codecs) or text no campaign could hold, and the codec comparison it serves measures nothing real.

#region

using System;
using System.Linq;
using CalradiaRemembers.Logic.Saving;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MarkovTextTests
   {
      private static readonly string[] Samples = {
         "He spoke of the tournament at Pravend and the prize he won there.",
         "She remembers the night the bandits came to the village of Ab Comer.",
         "He spoke of the war against the Battanians and the friends he lost there.",
         "She remembers the feast in the hall of Pravend and the songs of the bards."
      };

      // STAKE: the inflated memories read like the campaign's own, word pairs taken from the real text only.
      [Test]
      public void GIVEN_real_sentences_WHEN_new_ones_are_written_THEN_every_word_pair_comes_from_them()
      {
         var chain = new MarkovText(Samples);
         string joined = string.Join(" ", Samples);

         for (var seed = 0; seed < 50; seed++)
         {
            string[] words = chain.Sentence(new Random(seed), 40).Split(' ');
            words.Should().NotBeEmpty();
            for (var i = 0; i + 1 < words.Length; i++)
               joined.Should().Contain(words[i] + " " + words[i + 1]);
         }
      }

      // STAKE: the same seed writes the same save, so two codec runs compare the same data.
      [Test]
      public void GIVEN_the_same_seed_WHEN_sentences_are_written_THEN_they_are_the_same()
      {
         var chain = new MarkovText(Samples);

         chain.Sentence(new Random(7), 40).Should().Be(chain.Sentence(new Random(7), 40));
      }

      // STAKE: not one sentence copied again and again: many seeds give many different sentences.
      [Test]
      public void GIVEN_many_seeds_WHEN_sentences_are_written_THEN_they_vary()
      {
         var chain = new MarkovText(Samples);

         Enumerable.Range(0, 50).Select(s => chain.Sentence(new Random(s), 40)).Distinct().Count().Should().BeGreaterThan(4);
      }

      // STAKE: a campaign with no memory yet gives no text rather than a fault.
      [Test]
      public void GIVEN_nothing_to_learn_from_WHEN_a_sentence_is_asked_THEN_it_is_empty()
      {
         var chain = new MarkovText(new[] {"", "   "});

         chain.IsEmpty.Should().BeTrue();
         chain.Sentence(new Random(1), 20).Should().BeEmpty();
      }
   }
}
