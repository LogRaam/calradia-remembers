// Code written by Gabriel Mailhot, 21/09/2026.
// What this pins: WHEN a conversation the player walked away from may be picked up again, and when putting it
// back would be a lie.
//
// fkasad, Nexus, 20/09/2026: "That chat memory being deleted as soon as you open a new chat (after being reused
// if it's the same npc/same day)." The feature he asked for is bounded by that sentence, and the bound is the
// interesting half. CR's standing rule is that an NPC's continuity comes from what he REMEMBERS, never from what
// the player claims was said (Gabriel has refused the looser version of this twice). A resumed transcript is not
// an exception to that rule: it is the real words, kept, for the one case where the conversation did not
// actually end. Stretch it any further and the mod starts reciting an afternoon that, as far as the world is
// concerned, is over.
//
// So this policy exists to say no more often than it says yes, and each no below is a different way of being
// wrong in front of the player.

#region

using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class ConversationResumePolicyTests
   {
      private const string Rhagaea = "lord_1_37";
      private const string Someone = "lord_2_11";

      /// <summary>The state fkasad describes on coming back: same lord, same day, four lines still unfinished.</summary>
      private static ConversationResumeFacts TheBreakHeTook()
         => new ConversationResumeFacts {
            KeptForHeroId = Rhagaea,
            KeptOnDay = 412,
            KeptLineCount = 4,
            OpeningWithHeroId = Rhagaea,
            Today = 412,
            IsOrdinaryConversation = true
         };

      // THE REQUEST ITSELF. "For the game, no time elapsed. I didn't move on the map or talk to someone else. I
      // go back to the npc I was talking to before and reopen the chat box." Nothing in the world has changed,
      // so nothing about the conversation should have either.
      [Test]
      public void GIVEN_the_same_lord_on_the_same_day_with_words_still_kept_WHEN_the_chat_reopens_THEN_the_exchange_resumes()
         => ConversationResumePolicy.Decide(TheBreakHeTook()).Should().Be(ConversationResumeVerdict.Resume);

      // THE MOST DANGEROUS FAILURE, so it is the first no. Handing Rhagaea's unfinished bargain to the next lord
      // the player greets would show him another character's words in his own mouth, and would feed them to that
      // character's next prompt as his own history. This is the same family as the "Garios thinks he's Fercread"
      // fault: a conversation written to the wrong profile.
      [Test]
      public void GIVEN_the_player_opens_a_chat_with_somebody_else_WHEN_it_is_checked_THEN_the_other_lords_words_are_not_put_in_his_mouth()
      {
         ConversationResumeFacts anotherLord = Facts(TheBreakHeTook(), openingWith: Someone);

         ConversationResumePolicy.Decide(anotherLord).Should().Be(ConversationResumeVerdict.SomebodyElse);
      }

      // A DAY IS THE LINE, and it is drawn where the world redraws it. Once the campaign clock has turned, the
      // conversation genuinely ended: its summary is in the NPC's memory, his day has happened, and replaying
      // yesterday's raw words would have him answer as though he were still standing in that room. That would
      // read as the model losing track, which is precisely the impression Gabriel refuses to create.
      [Test]
      public void GIVEN_a_day_has_passed_in_the_campaign_WHEN_the_chat_reopens_THEN_yesterdays_words_are_not_recited_as_if_still_standing_there()
      {
         ConversationResumeFacts tomorrow = Facts(TheBreakHeTook(), today: 413);

         ConversationResumePolicy.Decide(tomorrow).Should().Be(ConversationResumeVerdict.ANewDay);
      }

      // AND THE CLOCK IS NOT ASSUMED TO MOVE FORWARD. A campaign loaded from an older save, or any path that
      // hands back an earlier day, must be refused for the same reason rather than matching by accident on a
      // comparison that only ever looked for "later".
      [Test]
      public void GIVEN_a_save_from_an_earlier_day_is_loaded_WHEN_the_chat_reopens_THEN_it_is_refused_like_any_other_day_that_is_not_this_one()
      {
         ConversationResumeFacts yesterday = Facts(TheBreakHeTook(), today: 411);

         ConversationResumePolicy.Decide(yesterday).Should().Be(ConversationResumeVerdict.ANewDay);
      }

      // NOTHING KEPT IS THE ORDINARY CASE, every first conversation of every campaign, so it must be a plain
      // answer and not an edge. It also covers the save written before this feature existed, where the blob is
      // simply absent.
      [TestCase("", 4, TestName = "GIVEN_no_conversation_was_kept_WHEN_a_chat_opens_THEN_there_is_simply_nothing_to_resume")]
      [TestCase(Rhagaea, 0, TestName = "GIVEN_a_kept_conversation_with_no_words_in_it_WHEN_a_chat_opens_THEN_there_is_simply_nothing_to_resume")]
      public void GIVEN_nothing_worth_resuming_WHEN_a_chat_opens_THEN_the_chat_starts_clean(string keptFor, int keptLines)
      {
         var nothing = new ConversationResumeFacts {
            KeptForHeroId = keptFor,
            KeptOnDay = 412,
            KeptLineCount = keptLines,
            OpeningWithHeroId = Rhagaea,
            Today = 412,
            IsOrdinaryConversation = true
         };

         ConversationResumePolicy.Decide(nothing).Should().Be(ConversationResumeVerdict.NothingKept);
      }

      // A CAPTIVE SCENE, A BRIEFING OR A POST-DUEL MEETING IS A SCENE, NOT A CHAT THE PLAYER STEPPED OUT OF.
      // Each of those opens with its own staging and its own opening cue, and several of them deliberately keep
      // their words off the shared record (the companion briefing must never reach the person it is about). A
      // resumed transcript dropped into one of them would both contradict the scene's own opener and, in the
      // briefing's case, leak. The post-duel scene already has its own resume, carried in memory.
      [Test]
      public void GIVEN_the_chat_that_opens_is_a_staged_scene_rather_than_an_ordinary_talk_WHEN_it_is_checked_THEN_nothing_is_resumed_into_it()
      {
         ConversationResumeFacts scene = Facts(TheBreakHeTook(), ordinary: false);

         ConversationResumePolicy.Decide(scene).Should().Be(ConversationResumeVerdict.NotAnOrdinaryConversation);
      }

      // ORDER OF REFUSAL, stated because the log line is read by whoever is diagnosing a resume that did not
      // happen. When several reasons hold at once the answer must be the most fundamental one: there is no point
      // telling anybody it was "somebody else" when nothing was kept for anybody.
      [Test]
      public void GIVEN_several_reasons_to_refuse_at_once_WHEN_it_is_checked_THEN_the_answer_names_the_most_fundamental_one()
      {
         var everythingWrong = new ConversationResumeFacts {
            KeptForHeroId = "",
            KeptOnDay = 400,
            KeptLineCount = 0,
            OpeningWithHeroId = Someone,
            Today = 412,
            IsOrdinaryConversation = false
         };

         ConversationResumePolicy.Decide(everythingWrong).Should().Be(ConversationResumeVerdict.NothingKept);
      }

      #region private

      /// <summary>fkasad's own case with one fact changed, so each test says only what it is about.</summary>
      private static ConversationResumeFacts Facts(
         ConversationResumeFacts basis, string openingWith = null, int? today = null, bool? ordinary = null)
         => new ConversationResumeFacts {
            KeptForHeroId = basis.KeptForHeroId,
            KeptOnDay = basis.KeptOnDay,
            KeptLineCount = basis.KeptLineCount,
            OpeningWithHeroId = openingWith ?? basis.OpeningWithHeroId,
            Today = today ?? basis.Today,
            IsOrdinaryConversation = ordinary ?? basis.IsOrdinaryConversation
         };

      #endregion
   }
}
