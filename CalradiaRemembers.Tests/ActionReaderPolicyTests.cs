// Code written by Gabriel Mailhot, 25/10/2026.
// Documents who reads a reply's actions: the reply's own tags (the same call), or the action interpreter (a second call).
// Gabriel (25/10/2026): "la seule jonction devrait etre a quel service je demande l'interpretation." ActionReaderPolicy is
// that junction, and the rules are the ones the live turn applied before they moved there (ChatViewModel.Exchanges).
//
// WHY IT MATTERS: read by the wrong side, a reply's deeds are lost or read twice. A turn the prose fallback redid in one
// call already carries real tags, and the interpreter would be "targeting the wrong prose, the fallback's".

#region

using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Logic.Providers;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ActionReaderPolicyTests
    {
        private static ActionReadingFacts OrdinaryTurn(ChatCompositionMode composition) => new() {
            Composition = composition, IsMainSpeaker = true
        };

        // STAKE: Enhanced exists for this turn: a player typing to the character they came to see has the interpreter
        // read the deeds out of the prose.
        [Test]
        public void GIVEN_an_ordinary_enhanced_turn_WHEN_deciding_THEN_the_interpreter_reads_its_actions()
        {
            ActionReaderPolicy.ReaderFor(OrdinaryTurn(ChatCompositionMode.Enhanced), false).Should().Be(ActionReader.Interpreter);
        }

        // STAKE: in Standard the prose model writes its own tags; asking an interpreter too would read every deed twice.
        [Test]
        public void GIVEN_an_ordinary_standard_turn_WHEN_deciding_THEN_the_reply_reads_its_own_actions()
        {
            ActionReaderPolicy.ReaderFor(OrdinaryTurn(ChatCompositionMode.Standard), false).Should().Be(ActionReader.SameCall);
        }

        // STAKE: "captive/captor scenes, councils, routed-witness turns and synthetic openers deliberately stay Integrated"
        // (ChatViewModel.Exchanges): in each, the reply's own tags are the ones read, even in Enhanced.
        [Test]
        public void GIVEN_a_turn_enhanced_leaves_single_call_WHEN_deciding_THEN_the_reply_reads_its_own_actions()
        {
            var turns = new[] {
                new ActionReadingFacts {Composition = ChatCompositionMode.Enhanced, IsMainSpeaker = false},
                new ActionReadingFacts {Composition = ChatCompositionMode.Enhanced, IsMainSpeaker = true, IsSyntheticPlayerLine = true},
                new ActionReadingFacts {Composition = ChatCompositionMode.Enhanced, IsMainSpeaker = true, IsCaptiveEncounter = true},
                new ActionReadingFacts {Composition = ChatCompositionMode.Enhanced, IsMainSpeaker = true, IsCaptorScene = true}
            };

            foreach (ActionReadingFacts turn in turns)
            {
                ActionReaderPolicy.IsInterpreterTurn(turn).Should().BeFalse();
                ActionReaderPolicy.ReaderFor(turn, false).Should().Be(ActionReader.SameCall);
            }
        }

        // STAKE: the prose fallback redoes the beat as one integrated call on another model; that reply's own tags stand,
        // since the interpreter "would target the wrong prose, the fallback's".
        [Test]
        public void GIVEN_an_enhanced_turn_the_prose_fallback_redid_WHEN_deciding_THEN_the_fallback_reply_reads_its_own_actions()
        {
            ActionReadingFacts turn = OrdinaryTurn(ChatCompositionMode.Enhanced);

            ActionReaderPolicy.IsInterpreterTurn(turn).Should().BeTrue("the prose call is still asked to fail fast, so the fallback can take over");
            ActionReaderPolicy.ReaderFor(turn, true).Should().Be(ActionReader.SameCall);
        }
    }
}
