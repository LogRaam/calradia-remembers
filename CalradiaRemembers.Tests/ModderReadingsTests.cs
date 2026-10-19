// Code written by Gabriel Mailhot, 08/10/2026.
// What tashmetu's Regard and Standing (Nexus, 08/10/2026) needs from CR's public door, ratified by Gabriel:
// - its memories of nobles' own lives ("I have learned that my wife...") filed as OwnLife, kept apart from the player;
// - how strictly a culture holds marriage, which it read from CR's internal JealousyCultureLoader: any rename here
//   would have broken it silently. It is now a public reading with the same answer.
// If these break, a noble's affair is read as history with the player, or a bridge stops reading cultures one update.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ModderReadingsTests
   {
      // A mod files a noble's own affair as OwnLife, and it is kept as such.
      [Test]
      public void GIVEN_a_mod_filing_a_nobles_own_affair_WHEN_injected_as_own_life_THEN_it_is_kept_as_own_life()
      {
         NpcMemoryInjection.BuildInjectedEvent("I have learned that my wife has taken a lover.", "OwnLife", 0, 100).type
                           .Should().Be(NotableEventType.OwnLife);
      }

      // The strict monogamous fallback reads as strict, sharing never accepted.
      [Test]
      public void GIVEN_a_strictly_monogamous_culture_WHEN_read_THEN_it_is_strict_and_never_shares()
      {
         MarriageCustomReading r = MarriageCustomReading.Of(JealousyCulture.Monogamous, clanTier: 6);

         r.Strictness.Should().Be(2);
         r.SharesPartners.Should().BeFalse();
      }

      // A lenient culture whose elite share partners: low strictness, sharing from its tier up, the same rule CR plays by.
      [Test]
      public void GIVEN_a_lenient_culture_WHEN_read_at_and_below_its_elite_tier_THEN_sharing_follows_the_tier()
      {
         JealousyCulture lenient = JealousyCulture.FromRaw(new JealousyCulture.JealousyCultureRaw {ExclusivityOutrage = "low", PolygamyEliteTier = 4});

         MarriageCustomReading.Of(lenient, clanTier: 4).Should().Be(new MarriageCustomReading(0, true));
         MarriageCustomReading.Of(lenient, clanTier: 2).SharesPartners.Should().BeFalse();
      }
   }
}
