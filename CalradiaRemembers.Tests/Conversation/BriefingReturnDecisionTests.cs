// Code written by Gabriel Mailhot, 13/09/2026.
// What these pin: the SHAPE of the briefing-to-meeting sequence, which is the only thing that ever went wrong
// with it.
//
// Three attempts failed in one day and not one of them was a mistake about what a step does:
//   1. a step never called, 2. a precondition inverted, 3. a step that should not have existed - staging a
// scene, which opened an interior mission from the campaign map and crashed the game natively.
//
// Gabriel's reading: make the composition into data and a pure test can read it. Faults 2 and 3 each have a
// test below that fails in milliseconds, with no game, no save, and no scene to load. Fault 3 goes further -
// there is no longer any value of ReturnMove that means "stage a scene", so the crash is unrepresentable
// rather than merely untaken.
//
// What no test here can do is tell you an engine call crashes. The plan is provable; the effects are not.
// That is what the in-game self-tests are for, and why both exist.

#region

using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class BriefingReturnDecisionTests
   {
      // ── the precondition that was inverted (fault 2) ──

      // THE ONE THAT WOULD HAVE CAUGHT IT. The guard read "wait until no mission is live", copied from
      // DuelCoordinator, where it is right for the opposite reason: a duel needs its OWN arena. A briefing
      // return wants the scene the player never left, so a live scene is the reason to ACT.
      [Test]
      public void GIVEN_the_chat_is_down_and_a_scene_is_live_WHEN_deciding_THEN_it_opens_over_that_scene()
      {
         BriefingReturnDecision.Decide(armed: true, chatIsOpen: false, sceneIsLive: true, ticksWaited: 1)
                               .Should()
                               .Be(ReturnMove.OpenOverLiveScene);
      }

      // And the mirror, stated so the inversion cannot come back by accident: no scene is NOT a reason to
      // wait for one, because nothing is going to produce one.
      [Test]
      public void GIVEN_no_scene_at_all_WHEN_deciding_THEN_it_gives_the_world_back_rather_than_waiting()
      {
         BriefingReturnDecision.Decide(true, chatIsOpen: false, sceneIsLive: false, ticksWaited: 1)
                               .Should()
                               .Be(ReturnMove.GiveTheWorldBack);
      }

      // ── the step that must not exist (fault 3) ──

      // Staging is not a move this decision can express. The crash of 13/09/2026 was an interior mission
      // opened from the campaign map; there is now no answer this function can give that would ask for one.
      [Test]
      public void GIVEN_every_possible_situation_WHEN_deciding_THEN_it_never_asks_for_a_scene_to_be_built()
      {
         string[] names = Enum.GetNames(typeof(ReturnMove));

         names.Should().NotContain(n => n.IndexOf("Stage", StringComparison.OrdinalIgnoreCase) >= 0);
         names.Should().NotContain(n => n.IndexOf("Build", StringComparison.OrdinalIgnoreCase) >= 0);
      }

      // ── the rest of the shape ──

      [Test]
      public void GIVEN_the_window_is_still_closing_WHEN_deciding_THEN_it_waits()
      {
         BriefingReturnDecision.Decide(true, chatIsOpen: true, sceneIsLive: true, ticksWaited: 1)
                               .Should()
                               .Be(ReturnMove.Wait);
      }

      // A return that never fires must not lie in wait and open something into an unrelated moment later on.
      [Test]
      public void GIVEN_the_wait_has_run_long_WHEN_deciding_THEN_it_stops_waiting_and_settles_up()
      {
         BriefingReturnDecision
            .Decide(true, chatIsOpen: true, sceneIsLive: true, BriefingReturnDecision.PatienceTicks + 1)
            .Should()
            .Be(ReturnMove.GiveTheWorldBack);
      }

      // Nothing armed is nothing to do - this runs on every application tick of the game.
      [Test]
      public void GIVEN_nothing_armed_WHEN_deciding_THEN_it_does_nothing_whatever_else_is_true()
      {
         foreach (bool chat in new[] {true, false})
         foreach (bool scene in new[] {true, false})
            BriefingReturnDecision.Decide(false, chat, scene, 0).Should().Be(ReturnMove.Idle);
      }

      // ── the two closes ──

      // A hand-off records and takes the window down, and touches nothing else: every world-restoring step is
      // something the very next open would immediately undo, and that churn is where the loading screens were.
      [Test]
      public void GIVEN_a_hand_off_WHEN_planning_the_close_THEN_it_gives_none_of_the_world_back()
      {
         var plan = ChatClosePlan.For(ChatCloseKind.HandOff);

         plan.Should().Equal(ChatCloseStep.RecordWhatHappened,
                             ChatCloseStep.ReleasePortraits,
                             ChatCloseStep.KillWindow);
      }

      // An ordinary close gives all of it back, in the order the engine needs.
      [Test]
      public void GIVEN_an_ordinary_close_WHEN_planning_THEN_the_world_comes_back_in_order()
      {
         ChatClosePlan.For(ChatCloseKind.Ordinary)
                      .Should()
                      .Equal(ChatCloseStep.RecordWhatHappened,
                             ChatCloseStep.ReleasePortraits,
                             ChatCloseStep.KillWindow,
                             ChatCloseStep.ResumeMapScene,
                             ChatCloseStep.EndVanillaConversation,
                             ChatCloseStep.EndStagedMission,
                             ChatCloseStep.EndMapEncounter,
                             ChatCloseStep.RestoreTime);
      }

      // The private word is remembered whichever close this is. A hand-off defers the WORLD, never the memory:
      // dropping the recording would lose the briefing in exactly the case it was given for.
      [Test]
      public void GIVEN_either_close_WHEN_planning_THEN_what_happened_is_always_recorded_and_the_window_always_goes()
      {
         foreach (ChatCloseKind kind in Enum.GetValues(typeof(ChatCloseKind)).Cast<ChatCloseKind>())
         {
            var plan = ChatClosePlan.For(kind);

            plan.Should().Contain(ChatCloseStep.RecordWhatHappened);
            plan.Should().Contain(ChatCloseStep.KillWindow);
            plan.Should().HaveCountGreaterThan(0);
         }
      }

      // The debt a hand-off takes on, derived from the two plans rather than written a third time - so it can
      // never drift from them, which is how a "give the world back" path would quietly stop restoring one of
      // its five pieces.
      [Test]
      public void GIVEN_a_hand_off_WHEN_it_is_not_caught_THEN_exactly_what_it_skipped_is_owed()
      {
         ChatClosePlan.Deferred()
                      .Should()
                      .Equal(ChatCloseStep.ResumeMapScene,
                             ChatCloseStep.EndVanillaConversation,
                             ChatCloseStep.EndStagedMission,
                             ChatCloseStep.EndMapEncounter,
                             ChatCloseStep.RestoreTime);

         ChatClosePlan.Deferred()
                      .Concat(ChatClosePlan.For(ChatCloseKind.HandOff))
                      .Should()
                      .BeEquivalentTo(ChatClosePlan.For(ChatCloseKind.Ordinary),
                                      "a hand-off plus its debt must equal an ordinary close, or something is lost");
      }
   }
}
