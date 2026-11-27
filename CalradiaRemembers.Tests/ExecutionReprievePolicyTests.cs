// Code written by Gabriel Mailhot, 26/11/2026.
// ExecutionReprieveTracker / ExecutionReprievePolicy: a lord the player sentenced to death and then spared remembers it.
// froggyluv (Nexus, 26/11/2026): a cocky lord, spared after the "Execute" order was rescinded, later denied it had ever
// happened; the game's threat and reprieve never reached his memory. If these rules are wrong, the spared lord still
// denies it, a lord who was never threatened "remembers" a sentence, or a reprieve is remembered twice.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ExecutionReprievePolicyTests
    {
        // STAKE: froggyluv's case: "Off with your head!", the lord protests, the player takes it back. He must remember.
        [Test]
        public void GIVEN_a_sentence_taken_back_in_the_talk_WHEN_followed_THEN_the_lord_was_spared_face_to_face()
        {
            var tracker = new ExecutionReprieveTracker();

            tracker.OnSentence(ExecutionReprieveTracker.ThreatSentence, "lord_a").Should().Be(ReprieveWay.None);
            tracker.OnSentence(ExecutionReprieveTracker.RevokeSentence, "lord_a").Should().Be(ReprieveWay.RevokedInTalk);
        }

        // STAKE: "Prepare to die!" then a cancelled execution scene: spared at the block after the sentence, not a cold
        // party-screen reprieve.
        [Test]
        public void GIVEN_the_order_given_then_the_scene_cancelled_WHEN_followed_THEN_he_was_spared_at_the_block_after_the_battle()
        {
            var tracker = new ExecutionReprieveTracker();
            tracker.OnSentence(ExecutionReprieveTracker.ThreatSentence, "lord_a");
            tracker.OnSentence(ExecutionReprieveTracker.OrderSentence, "lord_a").Should().Be(ReprieveWay.None);

            tracker.OnExecutionCancelled("lord_a").Should().Be(ReprieveWay.StayedAtTheBlockAfterBattle);
        }

        // STAKE: the same scene opened from the party screen and cancelled: spared at the block, no sentence to his face.
        [Test]
        public void GIVEN_a_cancelled_execution_with_no_sentence_spoken_WHEN_followed_THEN_he_was_spared_at_the_block()
        {
            new ExecutionReprieveTracker().OnExecutionCancelled("lord_b").Should().Be(ReprieveWay.StayedAtTheBlock);
        }

        // STAKE: the plain "You are my prisoner now" with no threat, or a protest heard from another lord, is no reprieve.
        [Test]
        public void GIVEN_no_threat_to_this_lord_WHEN_the_prisoner_line_is_said_THEN_nothing_is_remembered()
        {
            var tracker = new ExecutionReprieveTracker();
            tracker.OnSentence(ExecutionReprieveTracker.RevokeSentence, "lord_a").Should().Be(ReprieveWay.None);

            tracker.OnSentence(ExecutionReprieveTracker.ThreatSentence, "lord_a");
            tracker.OnSentence(ExecutionReprieveTracker.RevokeSentence, "lord_b").Should().Be(ReprieveWay.None);
        }

        // STAKE: a reprieve is remembered once; the same line said again later is not a second sentence.
        [Test]
        public void GIVEN_a_reprieve_already_followed_WHEN_the_line_comes_again_THEN_it_is_not_remembered_twice()
        {
            var tracker = new ExecutionReprieveTracker();
            tracker.OnSentence(ExecutionReprieveTracker.ThreatSentence, "lord_a");
            tracker.OnSentence(ExecutionReprieveTracker.RevokeSentence, "lord_a");

            tracker.OnSentence(ExecutionReprieveTracker.RevokeSentence, "lord_a").Should().Be(ReprieveWay.None);
        }

        // STAKE: an execution carried out leaves no pending sentence to turn a later, unrelated cancel into "after the battle".
        [Test]
        public void GIVEN_an_execution_carried_out_WHEN_another_scene_is_cancelled_THEN_it_reads_as_a_plain_reprieve()
        {
            var tracker = new ExecutionReprieveTracker();
            tracker.OnSentence(ExecutionReprieveTracker.ThreatSentence, "lord_a");
            tracker.OnSentence(ExecutionReprieveTracker.OrderSentence, "lord_a");
            tracker.OnExecutionCarriedOut("lord_a");

            tracker.OnExecutionCancelled("lord_a").Should().Be(ReprieveWay.StayedAtTheBlock);
        }

        // STAKE: the memory says what happened, in the lord's voice, naming the player; no reprieve, no memory.
        [Test]
        public void GIVEN_each_reprieve_WHEN_remembered_THEN_the_line_tells_it_and_none_tells_nothing()
        {
            ExecutionReprievePolicy.Memory(ReprieveWay.RevokedInTalk, "Arwa").Should().Contain("Arwa sentenced me to death to my face");
            ExecutionReprievePolicy.Memory(ReprieveWay.StayedAtTheBlockAfterBattle, "Arwa").Should().Contain("brought to the block");
            ExecutionReprievePolicy.Memory(ReprieveWay.StayedAtTheBlock, "Arwa").Should().Contain("spared me at the last moment");
            ExecutionReprievePolicy.Memory(ReprieveWay.None, "Arwa").Should().BeNull();
        }
    }
}
