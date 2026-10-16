// Code written by Gabriel Mailhot, 16/10/2026.
// The door another mod passes to hand the player a match to broker, and the book that keeps it. tashmetu (Nexus,
// 16/10/2026): a head of house agreed in the chat to give his kinswoman to a lord of the player's house, as Regard and
// Standing's matchmaker had proposed, and nothing happened, because arrange_match is taught only for a match asked of
// the player by letter. Gabriel approved the door the same day. Wrong one way, a head of house is taught a match the
// law then refutes (CR declares before the model speaks, never refutes after); wrong the other way, a lawful match is
// refused at the door, or a match already settled is taught again.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PendingMatchBookTests
   {
      private static MatchRegistrationFacts Lawful() => new() {AllKnown = true, LawfulCouple = true, DeciderHasSay = true};

      // STAKE: the reported case; a lawful match with the head of her house as decider is taken.
      [Test]
      public void GIVEN_a_lawful_couple_and_the_head_of_her_house_WHEN_registered_THEN_it_is_taken()
      {
         PendingMatchBook.Judge(Lawful()).Should().Be(MatchRegistrationResult.Registered);
      }

      // STAKE: a decider with no say over her hand would be taught to give away someone not theirs to give.
      [Test]
      public void GIVEN_a_decider_with_no_say_over_her_WHEN_registered_THEN_it_is_refused_with_that_reason()
      {
         PendingMatchBook.Judge(new MatchRegistrationFacts {AllKnown = true, LawfulCouple = true, DeciderHasSay = false})
                         .Should().Be(MatchRegistrationResult.RejectedNoAuthority);
      }

      // STAKE: a couple the game will never wed (kin, a marriage already, the same sex) is refused before anyone consents.
      [Test]
      public void GIVEN_a_couple_the_game_would_not_wed_WHEN_registered_THEN_it_is_refused()
      {
         PendingMatchBook.Judge(new MatchRegistrationFacts {AllKnown = true, LawfulCouple = false, DeciderHasSay = true})
                         .Should().Be(MatchRegistrationResult.RejectedNotLawful);
      }

      // STAKE: an id that names no one, or one person on both sides, is a mistake in the calling mod, refused plainly.
      [Test]
      public void GIVEN_an_unknown_id_or_one_person_twice_WHEN_registered_THEN_it_is_refused()
      {
         PendingMatchBook.Judge(new MatchRegistrationFacts {AllKnown = false}).Should().Be(MatchRegistrationResult.RejectedUnknown);
         PendingMatchBook.Judge(new MatchRegistrationFacts {AllKnown = true, SamePerson = true, LawfulCouple = true, DeciderHasSay = true})
                         .Should().Be(MatchRegistrationResult.RejectedSamePerson);
      }

      // STAKE: the player's own marriage goes through CR's own rules; through this door the player would be wed by a third
      // party, and the decider taught that the player comes on behalf of themselves.
      [Test]
      public void GIVEN_the_player_as_suitor_or_bride_WHEN_registered_THEN_it_is_refused()
      {
         PendingMatchBook.Judge(new MatchRegistrationFacts {AllKnown = true, PlayerIsOneOfThem = true, LawfulCouple = true, DeciderHasSay = true})
                         .Should().Be(MatchRegistrationResult.RejectedPlayer);
      }

      // STAKE: the head of house is taught the match registered for him, and no other decider is.
      [Test]
      public void GIVEN_a_registered_match_WHEN_its_decider_is_asked_THEN_it_is_found_and_another_decider_finds_nothing()
      {
         var book = new PendingMatchBook();
         book.Add(new PendingMatchEntry {SuitorId = "suitor", BrideId = "bride", DeciderId = "head", Source = "R&S"});

         book.ForDecider("head").BrideId.Should().Be("bride");
         book.ForDecider("someone_else").Should().BeNull();
      }

      // STAKE: registering the same couple again (the other mod reloading) keeps one match, not two.
      [Test]
      public void GIVEN_the_same_couple_registered_twice_WHEN_counted_THEN_one_match_stands_with_the_latest_decider()
      {
         var book = new PendingMatchBook();
         book.Add(new PendingMatchEntry {SuitorId = "suitor", BrideId = "bride", DeciderId = "father"});
         book.Add(new PendingMatchEntry {SuitorId = "suitor", BrideId = "bride", DeciderId = "head"});

         book.Entries.Should().ContainSingle().Which.DeciderId.Should().Be("head");
      }

      // STAKE: a match whose bride or suitor died or was wed meanwhile is never taught again.
      [Test]
      public void GIVEN_a_match_that_no_longer_stands_WHEN_pruned_THEN_it_is_dropped()
      {
         var book = new PendingMatchBook();
         book.Add(new PendingMatchEntry {SuitorId = "suitor", BrideId = "wed_meanwhile", DeciderId = "head"});
         book.Add(new PendingMatchEntry {SuitorId = "suitor", BrideId = "still_free", DeciderId = "head"});

         book.Prune(e => e.BrideId != "wed_meanwhile").Should().Be(1);
         book.ForDecider("head").BrideId.Should().Be("still_free");
      }

      // STAKE: a mod that withdraws its match takes it out of the decider's teaching.
      [Test]
      public void GIVEN_a_withdrawn_match_WHEN_its_decider_is_asked_THEN_nothing_is_found()
      {
         var book = new PendingMatchBook();
         book.Add(new PendingMatchEntry {SuitorId = "suitor", BrideId = "bride", DeciderId = "head"});

         book.Remove("suitor", "bride").Should().BeTrue();
         book.ForDecider("head").Should().BeNull();
      }
   }
}
