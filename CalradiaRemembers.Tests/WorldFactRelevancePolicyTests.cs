// Code written by Gabriel Mailhot, 29/08/2026.
// Protects the query-relevant world-fact scoring: the player asks a lord about Clan X's war and gets a
// hallucinated answer because the real fact ranked too low on ambient AWARENESS to be injected; entity-id
// relevance must surface it. This is deliberately downstream of the knowledge gate (RumorAwareness /
// GetKnownEventsRated), it only ranks candidates ALREADY cleared to be known, it never widens who knows what.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WorldFactRelevancePolicyTests
   {
      // STAKE: the lead case from the design doc. The player asks a lord about Clan X's war and the real
      // fact ranked too low on ambient awareness to be injected; without entity-id relevance, the NPC has
      // nothing to go on and hallucinates. An exact hero/faction id match must clear the threshold alone.
      [Test]
      public void GIVEN_the_event_names_a_hero_the_player_also_named_WHEN_scored_THEN_it_is_relevant()
      {
         //Arrange
         var eventEntityIds = new[] {"hero_derthert", "faction_vlandia"};
         var messageEntityIds = new[] {"hero_derthert"};

         //Act
         WorldFactRelevance result = WorldFactRelevancePolicy.Evaluate(
            eventEntityIds, "Derthert put the raiders to flight near Pravend.", messageEntityIds, new HashSet<string>());

         //Assert
         result.EntityMatch.Should().BeTrue();
         result.Score.Should().Be(WorldFactRelevancePolicy.EntityMatchScore);
         result.IsRelevant.Should().BeTrue();
      }

      // STAKE: a player who does not name a hero by StringId (they typed a place or topic) still deserves
      // the fact surfaced when their wording plainly echoes it. Two overlapping keywords, or one long
      // distinctive one, must be enough on their own, with no entity match at all.
      [Test]
      public void GIVEN_strong_keyword_overlap_with_no_entity_match_WHEN_scored_THEN_it_is_relevant()
      {
         //Arrange: no ids in common; the message tokens are already pre-filtered (as the resolver hands them).
         var messageTokens = new HashSet<string> {"garrison", "sturgian"};

         //Act
         WorldFactRelevance result = WorldFactRelevancePolicy.Evaluate(
            new List<string>(), "The Sturgian garrison at Varcheg fell after a long siege.",
            new List<string>(), messageTokens);

         //Assert
         result.EntityMatch.Should().BeFalse();
         result.KeywordOverlapCount.Should().Be(2);
         result.IsRelevant.Should().BeTrue();
      }

      // STAKE: the no-signal floor. A candidate that shares nothing with the message, id or wording, must
      // never be surfaced as "relevant to what they just said", that would inject noise instead of an
      // actual answer and defeat the whole point of a message-relevant block.
      [Test]
      public void GIVEN_no_id_match_and_no_keyword_overlap_WHEN_scored_THEN_it_is_not_relevant()
      {
         //Arrange
         var messageTokens = new HashSet<string> {"marriage", "dowry"};

         //Act
         WorldFactRelevance result = WorldFactRelevancePolicy.Evaluate(
            new[] {"hero_unrelated"}, "A skirmish broke out near the eastern border.",
            new[] {"hero_someone_else"}, messageTokens);

         //Assert
         result.Score.Should().Be(0);
         result.IsRelevant.Should().BeFalse();
      }

      // STAKE: without stop-word/length filtering, a common short connector shared between almost any
      // summary and almost any message would spuriously inflate overlap, flooding the block with facts
      // that merely share filler words rather than substance.
      [Test]
      public void GIVEN_the_only_shared_tokens_are_short_or_stop_words_WHEN_scored_THEN_they_do_not_count()
      {
         //Arrange: "the", "was", "for" are stop words; "war" is only 3 chars but IS meaningful (not a stop word),
         // so it is isolated in its own case below via a purely-stop-word message.
         var messageTokens = new HashSet<string> {"the", "was", "for", "and"};

         //Act
         WorldFactRelevance result = WorldFactRelevancePolicy.Evaluate(
            new List<string>(), "The war was fought for three long years.", new List<string>(), messageTokens);

         //Assert
         result.KeywordOverlapCount.Should().Be(0);
         result.Score.Should().Be(0);
         result.IsRelevant.Should().BeFalse();
      }

      // STAKE: pins the exact boundary so a future retune of the weights cannot silently make a single
      // generic word enough to surface a fact (too noisy) or require three overlaps (too strict, starving
      // the very case this policy exists to fix). One short overlap sits just below the line; a second
      // short overlap (or one long word) reaches it.
      [Test]
      public void GIVEN_exactly_one_short_keyword_overlap_WHEN_scored_THEN_it_falls_just_short_of_relevant()
      {
         //Arrange
         var messageTokens = new HashSet<string> {"siege"};

         //Act
         WorldFactRelevance result = WorldFactRelevancePolicy.Evaluate(
            new List<string>(), "A long siege ended in surrender.", new List<string>(), messageTokens);

         //Assert
         result.Score.Should().Be(WorldFactRelevancePolicy.ShortKeywordWeight);
         result.Score.Should().BeLessThan(WorldFactRelevancePolicy.RelevanceThreshold);
         result.IsRelevant.Should().BeFalse();
      }

      // The other side of the same boundary: a second short overlap lands exactly on the threshold and
      // must count as relevant (inclusive), matching RumorAwareness.Knows' own inclusive-at-threshold rule.
      [Test]
      public void GIVEN_two_short_keyword_overlaps_WHEN_scored_THEN_it_lands_on_the_threshold_and_is_relevant()
      {
         //Arrange
         var messageTokens = new HashSet<string> {"siege", "fell"};

         //Act
         WorldFactRelevance result = WorldFactRelevancePolicy.Evaluate(
            new List<string>(), "The siege fell to the attackers at dawn.", new List<string>(), messageTokens);

         //Assert
         result.Score.Should().Be(WorldFactRelevancePolicy.RelevanceThreshold);
         result.IsRelevant.Should().BeTrue();
      }

      // One long, distinctive word (>= LongKeywordLength) must be able to carry relevance alone, without
      // needing a second overlap: a rare, specific term ("excommunication", "usurpation") is a far stronger
      // signal than two generic short ones, and the weight table must reflect that.
      [Test]
      public void GIVEN_one_long_keyword_overlap_WHEN_scored_THEN_it_alone_is_relevant()
      {
         //Arrange
         var messageTokens = new HashSet<string> {"garrison"}; // 8 chars, well over LongKeywordLength

         //Act
         WorldFactRelevance result = WorldFactRelevancePolicy.Evaluate(
            new List<string>(), "The garrison surrendered without a fight.", new List<string>(), messageTokens);

         //Assert
         result.Score.Should().Be(WorldFactRelevancePolicy.LongKeywordWeight);
         result.IsRelevant.Should().BeTrue();
      }
   }
}
