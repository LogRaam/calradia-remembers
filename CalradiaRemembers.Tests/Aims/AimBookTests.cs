// Code written by Gabriel Mailhot, 14/10/2026.
// The door another mod registers a lord's aim through (tashmetu, BC-CR bridge author, Nexus 14/10/2026: "a way for
// another mod to register an aim for an NPC, and Calradia Remembers decides when it comes up and remembers that it
// did"). The book decides what is admitted, keeps one aim per lord, bounds the text that rides into the save and the
// prompt, and keeps the record of what the lord has already raised. If it is wrong: a lord carries five wants into
// every prompt, a careless source floods the save, or a reworded aim hands the lord a fresh leave to bring up again
// the thing he already raised, which is the exact repetition tashmetu feared ("a lord who brings up Lakonon in every
// conversation").

#region

using System.Linq;
using CalradiaRemembers.Logic.Aims;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Aims
{
   [TestFixture]
   public sealed class AimBookTests
   {
      private static StandingAim Lakonon(string id = "bc_aim_lakonon", string npc = "lord_herra", string source = "BCBridge")
         => new StandingAim
         {
            Id = id,
            NpcId = npc,
            SourceMod = source,
            Want = "Lakonon. By right it is the Osticos', but your house has been quietly building a claim to it.",
            PlayerPart = "His men can take what a claim alone cannot."
         };

      // tashmetu's own example must go through as he wrote it: a lord, a want, the player's part.
      [Test]
      public void GIVEN_a_complete_aim_WHEN_registered_THEN_the_lord_holds_it()
      {
         var book = new AimBook();

         book.Register(Lakonon(), today: 100).Should().Be(AimAdmission.Accepted);

         book.ForNpc("lord_herra").Want.Should().StartWith("Lakonon.");
         book.ForNpc("lord_herra").RegisteredOnDay.Should().Be(100);
      }

      // Without its id the bridge could never update or retract it, and without its source a misbehaving mod could
      // not be told apart from the others.
      [Test]
      public void GIVEN_an_aim_without_id_lord_or_source_WHEN_registered_THEN_it_is_refused()
      {
         var book = new AimBook();

         var noId = Lakonon(); noId.Id = " ";
         var noLord = Lakonon(); noLord.NpcId = null;
         var noSource = Lakonon(); noSource.SourceMod = "";

         book.Register(noId, 1).Should().Be(AimAdmission.MissingIdentity);
         book.Register(noLord, 1).Should().Be(AimAdmission.MissingIdentity);
         book.Register(noSource, 1).Should().Be(AimAdmission.MissingIdentity);
         book.All.Should().BeEmpty();
      }

      // An aim that says nothing would put an empty "WHAT YOU WANT" before the lord.
      [Test]
      public void GIVEN_an_empty_want_WHEN_registered_THEN_it_is_refused()
      {
         var aim = Lakonon(); aim.Want = "   ";

         new AimBook().Register(aim, 1).Should().Be(AimAdmission.EmptyWant);
      }

      // The text rides into the save and into every prompt the lord gets: an unbounded string from another mod is a
      // save-size and prompt-size risk.
      [Test]
      public void GIVEN_an_overlong_want_WHEN_registered_THEN_it_is_bounded()
      {
         var aim = Lakonon(); aim.Want = new string('x', AimRules.MaxWantChars + 300);
         var book = new AimBook();

         book.Register(aim, 1);

         book.ForNpc("lord_herra").Want.Length.Should().Be(AimRules.MaxWantChars);
      }

      // A lord who wants five things at once wants nothing in particular, and every one of them would be in his prompt.
      [Test]
      public void GIVEN_a_lord_who_already_holds_an_aim_WHEN_another_is_registered_for_him_THEN_it_is_refused()
      {
         var book = new AimBook();
         book.Register(Lakonon(), 1);

         book.Register(Lakonon(id: "rs_aim_rematch", source: "RegardStanding"), 2).Should().Be(AimAdmission.LordAlreadyHasAnAim);

         book.ForNpc("lord_herra").Id.Should().Be("bc_aim_lakonon");
      }

      // The heart of tashmetu's worry: a bridge refreshing its wording each day must not reset what the lord already
      // said, or the lord raises Lakonon again after every refresh.
      [Test]
      public void GIVEN_an_aim_already_raised_WHEN_its_source_rewords_it_THEN_the_lord_still_remembers_raising_it()
      {
         var book = new AimBook();
         book.Register(Lakonon(), 1);
         book.MarkRaised("bc_aim_lakonon", day: 5, channel: AimChannels.Conversation);

         var reworded = Lakonon(); reworded.Want = "Lakonon, and the Osticos know it now.";
         book.Register(reworded, 9).Should().Be(AimAdmission.Updated);

         var aim = book.ForNpc("lord_herra");
         aim.Want.Should().Be("Lakonon, and the Osticos know it now.");
         aim.TimesRaised.Should().Be(1);
         aim.LastRaisedDay.Should().Be(5);
         aim.RegisteredOnDay.Should().Be(1);
      }

      // tashmetu's design is that most lords get nothing; the cap keeps one source to it and bounds the save.
      [Test]
      public void GIVEN_a_source_at_its_cap_WHEN_it_registers_one_more_THEN_it_is_refused_while_other_sources_still_may()
      {
         var book = new AimBook();
         for (int i = 0; i < AimRules.MaxStandingAimsPerSource; i++)
            book.Register(Lakonon(id: "a" + i, npc: "lord_" + i), 1).Should().Be(AimAdmission.Accepted);

         book.Register(Lakonon(id: "one_more", npc: "lord_extra"), 1).Should().Be(AimAdmission.SourceAtCapacity);
         book.Register(Lakonon(id: "other", npc: "lord_extra", source: "RegardStanding"), 1).Should().Be(AimAdmission.Accepted);
      }

      // When the aim no longer holds (the fief was retaken, the rival died), the bridge takes it back and the lord
      // stops wanting it at once.
      [Test]
      public void GIVEN_a_standing_aim_WHEN_its_source_retracts_it_THEN_the_lord_no_longer_holds_it()
      {
         var book = new AimBook();
         book.Register(Lakonon(), 1);

         book.Retract("bc_aim_lakonon").Should().BeTrue();

         book.ForNpc("lord_herra").Should().BeNull();
         book.Retract("bc_aim_lakonon").Should().BeFalse();
      }

      // A feast that was last week is no aim; and one past its day drops out without the bridge having to remember it.
      [Test]
      public void GIVEN_an_aim_whose_last_day_is_past_WHEN_registered_or_pruned_THEN_it_does_not_stand()
      {
         var book = new AimBook();
         var feast = Lakonon(); feast.ExpiresOnDay = 10;

         book.Register(feast, today: 11).Should().Be(AimAdmission.AlreadyExpired);

         book.Register(feast, today: 8).Should().Be(AimAdmission.Accepted);
         book.PruneExpired(today: 10).Should().BeEmpty();
         book.PruneExpired(today: 11).Select(a => a.Id).Should().Equal("bc_aim_lakonon");
         book.ForNpc("lord_herra").Should().BeNull();
      }

      // An old save has no aims and a damaged entry must not stop the rest from loading.
      [Test]
      public void GIVEN_saved_aims_with_a_damaged_entry_WHEN_the_book_is_loaded_THEN_the_good_ones_stand_with_their_ledger()
      {
         var raised = Lakonon(); raised.TimesRaised = 1; raised.LastRaisedDay = 40;
         var damaged = Lakonon(id: "x", npc: null);

         var book = new AimBook(new[] { raised, damaged });

         book.All.Should().HaveCount(1);
         book.ForNpc("lord_herra").TimesRaised.Should().Be(1);
         new AimBook(null).All.Should().BeEmpty();
      }
   }
}
