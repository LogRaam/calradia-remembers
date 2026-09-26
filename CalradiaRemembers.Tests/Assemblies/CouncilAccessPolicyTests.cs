// Code written by Gabriel Mailhot, 27/07/2026.
// Documents the council/assembly OPEN gate (audit M1/M2). What breaks without it: convening while a chat is
// already open hijacked that live conversation (its NPC was reassigned as the council anchor and the other
// members bolted on as witnesses); convening from captivity or a battle scene opened a map gathering where none
// could exist. This policy is the single tested authority the mod's ChatManager defers to for the verdict.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilAccessPolicyTests
    {
        // The clean case: nothing in the way, so the council may open. If this returned a block the feature would
        // never open at all.
        [Test]
        public void GIVEN_a_free_player_on_the_map_with_no_chat_open_WHEN_evaluated_THEN_nothing_blocks()
            => CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = false, PlayerIsPrisoner = false, InMissionScene = false})
                .Should().Be(CouncilAccessBlock.None);

        // M2: an already-open chat is the direct hijack risk (ChatManager.Open returns early but leaves the old
        // view-model live, which OpenForCouncil would then reassign). It must block, and outrank the others.
        [Test]
        public void GIVEN_a_chat_already_open_WHEN_evaluated_THEN_it_blocks_as_chat_open_even_over_other_conditions()
        {
            CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = true, PlayerIsPrisoner = false, InMissionScene = false})
                .Should().Be(CouncilAccessBlock.ChatAlreadyOpen);

            // Precedence: an open chat wins even when the player is also captive and in a scene.
            CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = true, PlayerIsPrisoner = true, InMissionScene = true})
                .Should().Be(CouncilAccessBlock.ChatAlreadyOpen);
        }

        // M1: a captive player cannot convene their own house. Checked after the chat guard, before the scene one.
        [Test]
        public void GIVEN_a_captive_player_and_no_chat_open_WHEN_evaluated_THEN_it_blocks_as_captive()
            => CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = false, PlayerIsPrisoner = true, InMissionScene = true})
                .Should().Be(CouncilAccessBlock.PlayerCaptive);

        // M1: a running mission scene (battle, town walkabout) is no place for a map gathering; blocks when
        // nothing higher does.
        [Test]
        public void GIVEN_a_mission_scene_and_a_free_unengaged_player_WHEN_evaluated_THEN_it_blocks_as_in_scene()
            => CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = false, PlayerIsPrisoner = false, InMissionScene = true})
                .Should().Be(CouncilAccessBlock.InScene);

        // Arrests (26/09/2026): a TRIAL is held for a captive player by definition, the one sitting captivity does
        // not block. Every other guard still holds: an open chat still blocks it.
        [Test]
        public void GIVEN_a_captive_player_WHEN_a_trial_is_evaluated_THEN_captivity_does_not_block_it()
            => CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {PlayerIsPrisoner = true, IsTrial = true})
                .Should().Be(CouncilAccessBlock.None);

        [Test]
        public void GIVEN_a_chat_already_open_WHEN_a_trial_is_evaluated_THEN_it_still_blocks()
            => CouncilAccessPolicy.Evaluate(new CouncilAccessPolicy.CouncilAccessFacts {ChatAlreadyOpen = true, PlayerIsPrisoner = true, IsTrial = true})
                .Should().Be(CouncilAccessBlock.ChatAlreadyOpen);
    }
}
