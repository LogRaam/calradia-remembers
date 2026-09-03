// Code written by Gabriel Mailhot, 03/09/2026.
// ConversationEndPolicy backs the "NPCs can end conversations" MCM toggle. Get it wrong two ways: an ordinary
// chat that ignores the toggle keeps trapping the player who turned it off (the feature does nothing), or a
// CAPTIVE scene that obeys the toggle would break its own Continue/resume flow (a player could dodge a scene's
// pacing). These tests pin both: the ordinary Chatroom follows the setting, a captive/captor scene always locks.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ConversationEndPolicyTests
    {
        // Default behaviour: with the toggle on, an ordinary conversation locks when the NPC ends it, exactly as
        // before the feature. Turning it OFF is the whole point of the request, the chat stays open.
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void GIVEN_an_ordinary_conversation_WHEN_the_npc_ends_THEN_it_locks_only_if_the_toggle_allows(bool setting, bool expectedLock)
        {
            ConversationEndPolicy.NpcEndLocks(setting, isCaptiveOrCaptorScene: false).Should().Be(expectedLock);
        }

        // THE GUARD: a captive or captor scene must ALWAYS lock on end, whatever the toggle, so its own
        // Continue/resume flow is never bypassed (Gabriel: do not apply this to captivity events).
        [TestCase(true)]
        [TestCase(false)]
        public void GIVEN_a_captive_or_captor_scene_WHEN_the_npc_ends_THEN_it_always_locks(bool setting)
        {
            ConversationEndPolicy.NpcEndLocks(setting, isCaptiveOrCaptorScene: true).Should().BeTrue();
        }
    }
}
