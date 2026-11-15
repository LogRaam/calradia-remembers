// Code written by Gabriel Mailhot, 15/11/2026.
// What CallbackKeyPolicy decides: how a companion's memory, once brought up in an audience ("do you remember when..."),
// is remembered as spent. VonLemberg (Discord, 15/11/2026, source audit of 2.8.10): two memories of the same kind on
// the same day shared one key, so calling back to one silenced the other for good; "Please preserve existing
// spend-once behavior and compatibility with saved UsedCallbackKeys." If this is wrong, a companion either brings up
// the same old memory at every audience, or never brings up one of two things that happened the same day.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CallbackKeyPolicyTests
    {
        private static CallbackMemory Feast() => new() {DayAndKindKey = "companion_1:40:9", Summary = "We shared a night after the feast."};
        private static CallbackMemory Dawn() => new() {DayAndKindKey = "companion_1:40:9", Summary = "We woke together at dawn and spoke of the future."};

        // THE AUDIT: two memories of the same kind on the same day; calling back to one must leave the other.
        [Test]
        public void GIVEN_two_memories_of_the_same_day_and_kind_WHEN_one_is_called_back_to_THEN_the_other_can_still_come_up()
        {
            var spent = new HashSet<string> {CallbackKeyPolicy.KeyOf(Feast())};

            CallbackKeyPolicy.IsSpent(Feast(), spent).Should().BeTrue();
            CallbackKeyPolicy.IsSpent(Dawn(), spent).Should().BeFalse();
        }

        // STAKE (spend once): the same memory is never brought up at a second audience.
        [Test]
        public void GIVEN_a_memory_called_back_to_WHEN_asked_again_THEN_it_is_spent()
        {
            var spent = new HashSet<string> {CallbackKeyPolicy.KeyOf(Dawn())};

            CallbackKeyPolicy.IsSpent(Dawn(), spent).Should().BeTrue();
        }

        // STAKE (saved games): a key saved by 2.8.10 or older never said which memory of that day it stood for, so it
        // still stands for all of them; otherwise a memory the player already heard about would come up again.
        [Test]
        public void GIVEN_a_key_saved_by_an_older_version_WHEN_asked_THEN_the_memories_of_that_day_and_kind_stay_spent()
        {
            var savedByAnOlderVersion = new HashSet<string> {"companion_1:40:9"};

            CallbackKeyPolicy.IsSpent(Feast(), savedByAnOlderVersion).Should().BeTrue();
            CallbackKeyPolicy.IsSpent(Dawn(), savedByAnOlderVersion).Should().BeTrue();
        }

        // STAKE: a memory with an identity is known by it, so a later rewording of its summary does not bring it up twice.
        [Test]
        public void GIVEN_a_memory_with_an_identity_WHEN_its_words_change_THEN_it_stays_spent()
        {
            var before = new CallbackMemory {DayAndKindKey = "companion_1:40:9", MemoryId = "abc", Summary = "Spoke with Arwa."};
            var after = new CallbackMemory {DayAndKindKey = "companion_1:40:9", MemoryId = "abc", Summary = "Arwa swore to ride with us to Pravend."};
            var spent = new HashSet<string> {CallbackKeyPolicy.KeyOf(before)};

            CallbackKeyPolicy.IsSpent(after, spent).Should().BeTrue();
        }
    }
}
