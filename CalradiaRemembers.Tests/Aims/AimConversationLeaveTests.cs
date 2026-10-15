// Code written by Gabriel Mailhot, 14/10/2026.
// The leave to raise an aim lasts the whole conversation it was given in, and only that one (tashmetu, 14/10/2026:
// "once in a conversation at most"). The prompt is rebuilt on every reply; if the leave were asked for again each
// time, the ledger marked at the opening would withdraw it from the second reply on, and the lord would have one
// breath to bring up his aim. If the leave outlived the conversation, he would raise it again in the next one, which
// is the repetition tashmetu feared.

#region

using CalradiaRemembers.Logic.Aims;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Aims
{
   [TestFixture]
   public sealed class AimConversationLeaveTests
   {
      private static (AimBook book, StandingAim aim) LordWithAim()
      {
         var book = new AimBook();
         book.Register(new StandingAim { Id = "a", NpcId = "lord_herra", SourceMod = "BCBridge", Want = "Lakonon." }, 1);
         return (book, book.ForNpc("lord_herra"));
      }

      // The trap the leave exists for: a lord given leave at the opening keeps it on the third reply.
      [Test]
      public void GIVEN_a_leave_granted_at_the_opening_WHEN_a_later_reply_is_built_THEN_the_lord_may_still_raise_his_aim()
      {
         var (book, aim) = LordWithAim();
         var leave = new AimConversationLeave();

         leave.Open(new AimOpening { SessionId = "s1", Aim = aim, Today = 10 }, book).Should().Be(AimRaiseVerdict.MayRaise);

         leave.MayRaise("s1").Should().BeTrue();
         leave.MayRaise("s1").Should().BeTrue();
         aim.TimesRaised.Should().Be(1);
      }

      // The next conversation, a few days later, must not bring Lakonon up again.
      [Test]
      public void GIVEN_a_conversation_where_the_aim_was_given_the_floor_WHEN_the_next_one_opens_soon_after_THEN_the_lord_does_not_raise_it_again()
      {
         var (book, aim) = LordWithAim();
         var leave = new AimConversationLeave();
         leave.Open(new AimOpening { SessionId = "s1", Aim = aim, Today = 10 }, book);
         leave.Close("s1");

         leave.Open(new AimOpening { SessionId = "s2", Aim = aim, Today = 14 }, book).Should().Be(AimRaiseVerdict.TooSoon);

         leave.MayRaise("s2").Should().BeFalse();
         leave.MayRaise("s1").Should().BeFalse();
      }

      // The opening event can reach the book twice (a resumed chat); the ledger must count one raise, not two.
      [Test]
      public void GIVEN_the_same_conversation_opened_twice_WHEN_deciding_THEN_the_aim_counts_as_raised_once()
      {
         var (book, aim) = LordWithAim();
         var leave = new AimConversationLeave();

         leave.Open(new AimOpening { SessionId = "s1", Aim = aim, Today = 10 }, book);
         leave.Open(new AimOpening { SessionId = "s1", Aim = aim, Today = 10 }, book);

         aim.TimesRaised.Should().Be(1);
      }

      // A lord refused the leave (an enemy here) has not raised anything: the ledger stays untouched for a later day.
      [Test]
      public void GIVEN_a_lord_refused_the_leave_WHEN_the_conversation_opens_THEN_nothing_is_counted_as_raised()
      {
         var (book, aim) = LordWithAim();
         var leave = new AimConversationLeave();

         leave.Open(new AimOpening { SessionId = "s1", Aim = aim, Today = 10, Facts = new AimRaiseFacts { IsEnemyOfPlayer = true } }, book)
            .Should().Be(AimRaiseVerdict.Hostile);

         aim.TimesRaised.Should().Be(0);
         leave.MayRaise("s1").Should().BeFalse();
      }

      // A source that kept its aim out of conversation (letters only) is honoured at the opening.
      [Test]
      public void GIVEN_an_aim_kept_out_of_conversation_by_its_source_WHEN_a_conversation_opens_THEN_no_leave_is_given()
      {
         var (book, aim) = LordWithAim();
         aim.Channels = AimChannels.Letter;

         new AimConversationLeave().Open(new AimOpening { SessionId = "s1", Aim = aim, Today = 10 }, book)
            .Should().Be(AimRaiseVerdict.ChannelClosed);
      }
   }
}
