// Code written by Gabriel Mailhot, 08/10/2026.
// Intimacy that happened in another mod. tashmetu (Nexus, 08/10/2026): "Affairs of Calradia's nights reach your characters
// through my bridge as Intimacy memories, but they don't touch your romantic status, so your consent threshold never
// learns they happened (Maifailun, playing a Casanova: several nights, regard still 0)". He asked to count such a night,
// and to read CR's verdict so his mod opens "spend the night" only when CR's chat would allow it. Gabriel (08/10/2026)
// ratified both, on one condition: a plain memory never unlocks the romance arc (any mod could write one), only an
// explicit call, held to the very bar the chat holds, and recorded as if the night had happened in the chat.
// If this breaks, a mod mints a lover CR never would have allowed, or a night CR would have allowed is never counted.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SharedIntimacyPolicyTests
   {
      private static SharedIntimacyFacts Welcome() => new() {RomanceOn = true, HeroKnown = true, SummaryGiven = true, ChatWouldAllow = true};

      // A night CR's own chat would have allowed is recorded as one.
      [Test]
      public void GIVEN_a_night_the_chat_would_have_allowed_WHEN_recorded_THEN_it_counts()
      {
         SharedIntimacyPolicy.Decide(Welcome()).Should().Be(SharedIntimacyResult.Recorded);
      }

      // Maifailun's Casanova at regard 0: below the bar the chat holds, the night is refused and nothing is written.
      [Test]
      public void GIVEN_a_night_below_the_bar_the_chat_holds_WHEN_recorded_THEN_it_is_refused()
      {
         SharedIntimacyPolicy.Decide(new SharedIntimacyFacts {RomanceOn = true, HeroKnown = true, SummaryGiven = true, ChatWouldAllow = false})
                             .Should().Be(SharedIntimacyResult.NotWelcome);
      }

      // Adult content off means no romance system at all; an unknown hero or an empty line is refused as such.
      [Test]
      public void GIVEN_romance_off_or_a_bad_call_WHEN_recorded_THEN_it_is_refused_for_that_reason()
      {
         SharedIntimacyPolicy.Decide(new SharedIntimacyFacts {RomanceOn = false, HeroKnown = true, SummaryGiven = true, ChatWouldAllow = true})
                             .Should().Be(SharedIntimacyResult.RomanceOff);
         SharedIntimacyPolicy.Decide(new SharedIntimacyFacts {RomanceOn = true, HeroKnown = false, SummaryGiven = true, ChatWouldAllow = true})
                             .Should().Be(SharedIntimacyResult.RejectedUnknownHero);
         SharedIntimacyPolicy.Decide(new SharedIntimacyFacts {RomanceOn = true, HeroKnown = true, SummaryGiven = false, ChatWouldAllow = true})
                             .Should().Be(SharedIntimacyResult.RejectedInvalid);
      }
   }
}
