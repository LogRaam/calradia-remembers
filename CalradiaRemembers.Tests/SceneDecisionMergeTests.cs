// Code written by Gabriel Mailhot, 30/09/2026.
// Lufaine (Nexus, 30/09/2026): "the speak privately function doesn't work most of the time. Whether through requesting
// privacy through dialogue, or the button at the top of the chat, the npc will agree, emote a private departure, but
// the audience is still there listening to every word." Maifailun: the same, several times.
//
// In the two-call chat (the default), the ACTION INTERPRETER alone supplied the turn's actions, and the scene model's
// own [ACTION] blocks were thrown away. But the scene model is the one taught to answer a privacy request with
// request_privacy (it decides, in character, whether to clear the room), while the interpreter is told NOT to record
// a privacy that is only implied. So an agreement written as a gesture cleared nobody. The scene model's own decision
// about the scene now survives when the interpreter recorded none; deeds stay the interpreter's.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SceneDecisionMergeTests
    {
        private static GameAction Act(string type, string result = null)
            => new GameAction {Type = type, Parameters = result == null ? new Dictionary<string, string>() : new Dictionary<string, string> {["result"] = result}};

        // Lufaine's case: the character agreed in his own reply; the interpreter recorded nothing; the room clears.
        [Test]
        public void GIVEN_the_character_accepted_privacy_and_the_interpreter_recorded_none_WHEN_merged_THEN_the_acceptance_stands()
        {
            List<GameAction> merged = SceneDecisionMerge.Merge(new List<GameAction>(), new List<GameAction> {Act("request_privacy", "accepted")});

            merged.Should().ContainSingle(a => a.Type == "request_privacy");
        }

        // When the interpreter did record the decision, it is not recorded twice.
        [Test]
        public void GIVEN_both_recorded_the_privacy_decision_WHEN_merged_THEN_it_appears_once_as_the_interpreter_read_it()
        {
            List<GameAction> merged = SceneDecisionMerge.Merge(new List<GameAction> {Act("request_privacy", "refused")},
                                                               new List<GameAction> {Act("request_privacy", "accepted")});

            merged.Where(a => a.Type == "request_privacy").Should().ContainSingle().Which.Parameters["result"].Should().Be("refused");
        }

        // Deeds stay the interpreter's: a gift the scene model tagged is not carried over (the two-call design).
        [Test]
        public void GIVEN_the_scene_model_tagged_a_deed_WHEN_merged_THEN_only_the_interpreters_deeds_apply()
        {
            SceneDecisionMerge.Merge(new List<GameAction>(), new List<GameAction> {Act("give_gold")})
                              .Should().BeEmpty();
        }

        // When the interpreter failed outright (no tags at all), the character's privacy decision still stands.
        [Test]
        public void GIVEN_no_interpreter_reading_at_all_WHEN_merged_THEN_the_privacy_decision_still_stands()
        {
            SceneDecisionMerge.Merge(null, new List<GameAction> {Act("request_privacy", "accepted")})
                              .Should().ContainSingle(a => a.Type == "request_privacy");
        }
    }
}
