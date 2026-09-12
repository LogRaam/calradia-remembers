// Code written by Gabriel Mailhot, 12/09/2026.
// What these pin: that the two things this mod writes into the world are actually HEARD by the right people,
// and not heard by the wrong ones.
//
// WHY THEY EXIST, and it is a mistake of mine caught in the wild. Both features shipped with a magnitude
// chosen as a RANK - "an invented rumour must never outrank a battle", "a season's turn is above a claim and
// below a siege" - which is the right instinct applied to the wrong scale. Magnitude is not a rank. It is a
// REACH IN POINTS, and RumorAwareness spends it like this:
//
//     score = (magnitude + 25 if the event touches the observer's faction + up to 25 for proximity) x recency
//     heard at all when score >= 50
//
// At magnitude 2, an invented claim maxed out at 52: heard only by somebody of the same faction standing on
// the doorstep on the very day. At 4, a seasonal chronicle entry was unreachable by anybody at all. Both
// features were writing rows no character would ever reach, and every unit test passed the whole time,
// because every unit test asked about the NUMBER.
//
// It surfaced in Gabriel's live run as a t.Note in two self-tests - "a third party can reach it through the
// rumour road=False" - written as an observation, which turned out to be the finding.
//
// So these tests ask the only question that matters: WHO HEARS IT. They are in the mod's test project rather
// than the SDK's because that is the one place that can see both the magnitude (SDK) and the awareness rule
// (Logic) at once - which is itself why nothing had ever compared them.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.WorldAlmanac;
using FluentAssertions;
using NpcMemoryService.Core.Knowledge;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class NewsReachTests
   {
      private const int OnTheDoorstep = 25; // RumorAwareness geo bonus at its maximum
      private const int AcrossTheMap = 0;
      private const int Today = 0;

      private static bool Hears(int magnitude, bool sameRealm, int nearness, int ageDays = Today)
         => RumorAwareness.Knows(RumorAwareness.Score(magnitude, sameRealm, nearness, ageDays));

      // ── an invented fact: local news, and it must travel locally ─────────

      // THE BUG. At its shipped magnitude of 2 this was false for everybody, including a neighbour of the same
      // realm standing in the next street - so the whole invented-facts feature recorded rows nobody could
      // reach. It is the reason these tests exist.
      [Test]
      public void GIVEN_a_blight_a_lord_spoke_of_WHEN_his_own_neighbours_are_asked_THEN_word_has_reached_them()
      {
         Hears(WorldClaimPolicy.Magnitude, true, OnTheDoorstep).Should().BeTrue();
         Hears(WorldClaimPolicy.Magnitude, true, AcrossTheMap).Should().BeTrue();
      }

      // And the other half, which is what keeps it local news rather than a proclamation: somebody of another
      // realm, far away, has simply not heard that a village they have never seen lost its grain.
      [Test]
      public void GIVEN_the_same_blight_WHEN_a_stranger_across_the_map_is_asked_THEN_it_never_reached_him()
      {
         Hears(WorldClaimPolicy.Magnitude, false, AcrossTheMap).Should().BeFalse();
      }

      // A thing wondered aloud must travel less far than a thing stated flatly, and by enough to matter: at
      // one point apart the distinction was arithmetic rather than a difference anybody could observe.
      [Test]
      public void GIVEN_a_thing_merely_wondered_aloud_WHEN_it_travels_THEN_it_needs_more_help_to_arrive()
      {
         Hears(WorldClaimPolicy.HedgedMagnitude, true, OnTheDoorstep).Should().BeTrue();
         Hears(WorldClaimPolicy.HedgedMagnitude, true, AcrossTheMap).Should().BeFalse();

         WorldClaimPolicy.HedgedMagnitude.Should().BeLessThan(WorldClaimPolicy.Magnitude);
      }

      // ── a season's turn: realm news, and it must reach a realm ───────────

      // The fiction the number has to serve: a realm's own people hear how their realm fared last season.
      [Test]
      public void GIVEN_a_season_that_changed_a_realm_WHEN_its_own_people_are_asked_THEN_they_know_how_it_fared()
      {
         Hears(SeasonalTurnPolicy.Magnitude, true, AcrossTheMap).Should().BeTrue();
      }

      // And a stranger of another realm, far off, does not follow another kingdom's harvest.
      [Test]
      public void GIVEN_the_same_season_WHEN_a_distant_foreigner_is_asked_THEN_he_has_not_been_following_it()
      {
         Hears(SeasonalTurnPolicy.Magnitude, false, AcrossTheMap).Should().BeFalse();
      }

      // A neighbour on the border hears it even though it is not his realm, which is what a border IS.
      [Test]
      public void GIVEN_a_season_in_the_next_realm_WHEN_a_border_neighbour_is_asked_THEN_word_carries_that_far()
      {
         Hears(SeasonalTurnPolicy.Magnitude, false, OnTheDoorstep).Should().BeTrue();
      }

      // ── the ordering the original reasoning was actually after ───────────

      // The instinct behind the wrong numbers was sound and is kept: what somebody made up must never travel
      // as far as what actually happened. Now stated against the real scale instead of against a guess.
      [Test]
      public void GIVEN_an_invention_and_a_real_event_WHEN_compared_THEN_the_invention_never_travels_further()
      {
         const int aTownsRealHardship = 30;
         const int aWarDeclared = 75;

         WorldClaimPolicy.Magnitude.Should().BeLessThan(aTownsRealHardship);
         WorldClaimPolicy.Magnitude.Should().BeLessThan(aWarDeclared);
         SeasonalTurnPolicy.Magnitude.Should().BeLessThan(aWarDeclared);
      }

      // Everything fades, and how fast is worth pinning: recency MULTIPLIES the whole score, so a value that
      // only just clears the bar today stops being heard almost at once - a subtler version of the same bug.
      //
      // A week is what these actually buy, and there is a ceiling behind that worth knowing. For news to reach
      // its own realm and NOT a stranger, the magnitude must sit between 25 and 49 - above the bar with the
      // faction's 25, below it without. The best any such event can do is about thirteen days. Wanting a full
      // season would mean out-shouting a declaration of war, which is not what a harvest is.
      [Test]
      public void GIVEN_news_a_week_old_WHEN_a_countryman_is_asked_THEN_he_has_still_heard_of_it()
      {
         Hears(SeasonalTurnPolicy.Magnitude, true, AcrossTheMap, 7).Should().BeTrue();
         Hears(WorldClaimPolicy.Magnitude, true, OnTheDoorstep, 7).Should().BeTrue();
      }

      // And a month later nobody brings it up, which is the pipeline working: a world that remembered every
      // season's harvest for ever would drown its own present in its past.
      [Test]
      public void GIVEN_news_a_month_old_WHEN_anyone_is_asked_THEN_it_has_passed_out_of_talk()
      {
         Hears(SeasonalTurnPolicy.Magnitude, true, OnTheDoorstep, 30).Should().BeFalse();
         Hears(WorldClaimPolicy.Magnitude, true, OnTheDoorstep, 30).Should().BeFalse();
      }
   }
}
