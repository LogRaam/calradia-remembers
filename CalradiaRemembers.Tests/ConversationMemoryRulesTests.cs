// Code written by Gabriel Mailhot, 03/07/2026.
// Documents when a closing conversation earns a fallback memory: two turns always, one turn only when the
// player's words carried substance (the reported "NPC forgot our one-exchange conversation" fault).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ConversationMemoryRulesTests
    {
        [Test]
        public void GIVEN_two_completed_turns_WHEN_closing_THEN_a_fallback_memory_is_deserved()
        {
            ConversationMemoryRules.DeservesFallbackMemory(2, 10).Should().BeTrue();
            ConversationMemoryRules.DeservesFallbackMemory(5, 0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_one_substantial_turn_WHEN_closing_THEN_a_fallback_memory_is_deserved()
        {
            // The reported case: "this is done, Lucon took it very well..." said in ONE turn, then leave.
            ConversationMemoryRules.DeservesFallbackMemory(1, ConversationMemoryRules.SubstantialSoloTurnChars)
                .Should().BeTrue();
            ConversationMemoryRules.DeservesFallbackMemory(1, 120).Should().BeTrue();
        }

        [Test]
        public void GIVEN_one_trivial_turn_WHEN_closing_THEN_no_memory_spam()
        {
            // A drive-by "hello" then leave still records nothing.
            ConversationMemoryRules.DeservesFallbackMemory(1, ConversationMemoryRules.SubstantialSoloTurnChars - 1)
                .Should().BeFalse();
            ConversationMemoryRules.DeservesFallbackMemory(1, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_no_completed_turn_WHEN_closing_THEN_nothing_is_recorded()
        {
            ConversationMemoryRules.DeservesFallbackMemory(0, 500).Should().BeFalse();
        }
    }
}
