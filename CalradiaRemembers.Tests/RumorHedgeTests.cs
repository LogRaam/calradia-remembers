// Code written by Gabriel Mailhot, 15/11/2026.
// What RumorHedge decides: how news that its first speaker only guessed at is told to whoever hears it later. VonLemberg
// (Discord, 15/11/2026, source audit of 2.8.10): "Later recipients see confidence derived from awareness, not necessarily
// the original speaker's uncertainty ... Hearsay remains hearsay, not established truth." Gabriel: "oui". If this is
// wrong, a lord's idle "there may be a wedding come spring" reaches the next town as a wedding that took place.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RumorHedgeTests
   {
      // THE AUDIT: the speaker's doubt travels with the news, and says it was theirs.
      [Test]
      public void GIVEN_news_its_speaker_only_guessed_at_WHEN_told_THEN_it_is_told_as_their_guess()
      {
         RumorHedge.TagFor(true).Should().Contain("only a guess").And.Contain("the one who first said it");
      }

      // STAKE: a thing stated flatly, and every event the game itself records, are told as before, with no doubt added.
      [Test]
      public void GIVEN_news_stated_flatly_WHEN_told_THEN_nothing_is_added()
      {
         RumorHedge.TagFor(false).Should().BeEmpty();
      }
   }
}
