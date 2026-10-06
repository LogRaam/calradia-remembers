// Code written by Gabriel Mailhot, 06/10/2026.
// The chat header's line of standings. Fakade (Nexus, 06/10/2026), with True Relations (each noble has a relation of
// their own with the player): "I'm talking to a person that is +10 relation with me, but the conversation window has
// them at -61, because that's my relation with the clan leader." Since 2.7.0 the character is TOLD their own relation
// apart from the clan's; the header still showed only the clan and CR's regard, so the player could not see the number
// the character was working from. It is shown when it differs, and only then.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RelationSummaryTextTests
   {
      // Fakade's case: the clan at -61, the character's own +10, CR's regard 0. All three are shown, each named.
      [Test]
      public void GIVEN_a_character_whose_own_relation_differs_from_their_clans_WHEN_the_header_is_written_THEN_it_shows_their_own_too()
      {
         RelationSummaryText.Compose(new RelationSummaryFacts {Clan = -61, Own = 10, Personal = 0})
                            .Should().Be("Clan -61   ·   Own +10   ·   Personal 0");
      }

      // The base game, or a character with no relation of their own: the header is exactly what it was.
      [Test]
      public void GIVEN_no_relation_of_their_own_WHEN_the_header_is_written_THEN_it_is_as_before()
      {
         RelationSummaryText.Compose(new RelationSummaryFacts {Clan = 12, Own = null, Personal = 5})
                            .Should().Be("Clan +12   ·   Personal +5");
      }
   }
}
