// Code written by Gabriel Mailhot, 17/09/2026.
// What these pin: that a meeting staged inside a settlement is not judged by the clock of a meeting staged on
// the map.
//
// Two reports, a week apart, both in a town:
//
//   Lordfadooboo (11/09/2026): "i enter a city and it says (companion) wants to talk to you, i say yes, and
//   nothing happens."
//   Zinced121212 (17/09/2026): "a companion may approach me to talk when entering a city or settlement but if
//   i accept the chat window doesn't actually open."
//
// On the map the engine registers a conversation before it loads anything, so a staging that has produced no
// conversation after a few frames has failed. Inside a settlement the mod opens an interior mission instead,
// and a whole scene has to load before a conversation can exist, so the same reading means only "the scene is
// still coming". One grace was written for the first case and used for both, which declares a healthy
// settlement staging dead in a sixth of a second and opens the chat over the town menu just as the arriving
// scene takes the screen away.
//
// These tests are about the RULE, not the numbers: the settlement wait must be far longer than the map wait,
// and neither may be unbounded, because an unbounded one is the other player report (2026-07-19, an endless
// chain of people trying to talk to you and never managing it).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class StagingGracePolicyTests
    {
        // THE REPORTED CASE. Inside a settlement a scene must load first, so the wait has to outlast a scene
        // load rather than a function return. If this ever collapses back toward the map figure, the two men
        // above get their bug back.
        [Test]
        public void GIVEN_a_staging_waiting_on_an_interior_scene_WHEN_asked_THEN_it_is_given_far_longer_than_a_map_one()
        {
            int scene = StagingGracePolicy.FramesFor(true);
            int map = StagingGracePolicy.FramesFor(false);

            scene.Should().BeGreaterThan(map * 10,
                "an interior scene load and a function return are not the same kind of wait");
        }

        // A scene load is measured in seconds on a tired machine, and the tick this counts runs per frame.
        // Anything under a few seconds is a wait sized for the wrong call again.
        [Test]
        public void GIVEN_a_staging_waiting_on_an_interior_scene_WHEN_asked_THEN_it_outlasts_a_slow_load()
        {
            StagingGracePolicy.FramesFor(true).Should().BeGreaterThanOrEqualTo(600,
                "ten seconds at 60 fps is the least a settlement scene may take without being called dead");
        }

        // The map call registers at once, so its grace must stay SHORT. Lengthening it would leave the player
        // in the endless chain of 2026-07-19 for as long as it takes: staging blocks every later meeting.
        [Test]
        public void GIVEN_a_staging_waiting_on_a_map_conversation_WHEN_asked_THEN_it_gives_up_quickly()
        {
            StagingGracePolicy.FramesFor(false).Should().BeLessThanOrEqualTo(30,
                "a call that registers immediately has failed the moment it has not");
        }

        // NEITHER IS UNBOUNDED. A staging that never resolves is not a missed conversation, it is a mod that
        // can no longer open any conversation at all, which is what makes waiting forever the worse answer.
        [Test]
        public void GIVEN_either_kind_of_staging_WHEN_asked_THEN_the_wait_is_finite_and_positive()
        {
            foreach (bool waitingOnAScene in new[] {true, false})
                StagingGracePolicy.FramesFor(waitingOnAScene).Should().BePositive().And.BeLessThan(int.MaxValue);
        }
    }
}
