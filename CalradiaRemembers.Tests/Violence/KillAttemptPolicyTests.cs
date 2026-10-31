// Code written by Gabriel Mailhot, 31/10/2026.
// KillAttemptPolicy shows a killing attempt's roll and decides what follows it. zhenja1 (Nexus, 31/10/2026)
// confirmed a killing twice and the man lived: the roll had failed, but nothing on screen said there was a roll.
// Gabriel's answer is a window that names the chance and plays the number out, then a scene told by the model.
// If the shown number disagreed with the engine's roll, the player would watch "12" land under a chance of 15 and be
// told he failed, which is worse than no window at all. If the aftermath were wrong, a dead character could be asked
// to answer, or a captive who killed their captor would stay in chains. If the narrator were not told the outcome
// overrides the conversation, a reply that already narrated a death would stand beside a failed roll.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Violence;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Violence
{
    [TestFixture]
    public sealed class KillAttemptPolicyTests
    {
        private static ViolenceFacts Facts(int player, int target, ViolenceExposure exposure = ViolenceExposure.Alone)
            => new ViolenceFacts(player, target, exposure, 0);

        // STAKE: the number the player watches land must say the same thing the engine decided, for every roll and
        // every chance; a "12" shown as a failure against a chance of 15 would make the window a lie.
        [Test]
        public void GIVEN_every_roll_and_chance_WHEN_shown_THEN_the_shown_number_succeeds_exactly_when_the_engine_does()
        {
            for (var chance = ViolencePolicy.MinChancePercent; chance <= ViolencePolicy.MaxChancePercent; chance++)
            for (var roll = 0; roll < 100; roll++)
                KillAttemptPolicy.Succeeds(KillAttemptPolicy.ShownRoll(roll), chance).Should().Be(roll < chance, $"roll {roll} against {chance}");
        }

        // STAKE: the player is told the roll runs from 1 to 100; a 0 or a 101 on screen would be a number the game cannot roll.
        [Test]
        public void GIVEN_the_engine_roll_range_WHEN_shown_THEN_the_numbers_run_from_1_to_100()
        {
            KillAttemptPolicy.ShownRoll(0).Should().Be(1);
            KillAttemptPolicy.ShownRoll(99).Should().Be(100);
        }

        // STAKE: the roll must land on the number the game rolled, or the window shows one result and the scene tells another.
        [Test]
        public void GIVEN_a_rolled_number_WHEN_the_roll_plays_THEN_it_lands_on_that_number()
        {
            IReadOnlyList<KillRollFrame> frames = KillAttemptPolicy.Frames(37, seed: 5);

            frames.Should().HaveCount(KillAttemptPolicy.FrameCount);
            frames.Last().Value.Should().Be(37);
        }

        // STAKE: Gabriel asked for "the number changing quickly until it stops": a roll that stood still, or showed
        // the result before the end, would be no roll at all.
        [Test]
        public void GIVEN_a_roll_WHEN_it_plays_THEN_the_number_moves_every_step_and_slows_down()
        {
            IReadOnlyList<KillRollFrame> frames = KillAttemptPolicy.Frames(50, seed: 11);

            for (var i = 1; i < frames.Count; i++) frames[i].Value.Should().NotBe(frames[i - 1].Value, $"step {i}");
            frames.Take(frames.Count - 1).Should().OnlyContain(f => f.Value >= 1 && f.Value <= 100);
            frames[frames.Count - 2].Value.Should().NotBe(50, "the result is not shown before the roll lands");
            frames[frames.Count - 2].Seconds.Should().BeGreaterThan(frames[0].Seconds);
        }

        // STAKE: Gabriel wants the chance in the window; the line under it must say why it is low, or a 12 reads as
        // the game cheating. zhenja1's second attempt was in a town, where guards make it hard.
        [Test]
        public void GIVEN_a_stronger_guarded_foe_WHEN_the_chance_is_explained_THEN_both_reasons_are_named()
        {
            string why = KillAttemptPolicy.ChanceReasons(Facts(100, 200, ViolenceExposure.Guarded));

            why.Should().Contain("They are the better fighter").And.Contain("guards");
        }

        // STAKE: Gabriel, 31/10/2026: after a failure the character answers it; a dead character must never be asked to.
        [Test]
        public void GIVEN_a_failed_attempt_WHEN_deciding_what_follows_THEN_the_character_reacts()
        {
            KillAttemptPolicy.Decide(new KillAftermathFacts {Killed = false}).Should().Be(KillAftermath.NpcReacts);
            KillAttemptPolicy.Decide(new KillAftermathFacts {Killed = false, PlayerIsCaptive = true}).Should().Be(KillAftermath.NpcReacts);
        }

        // STAKE: Gabriel, 31/10/2026: "la conclusion s'affiche dans le chat et il ne reste plus qu'à quitter".
        [Test]
        public void GIVEN_a_kill_WHEN_deciding_what_follows_THEN_the_conversation_ends()
        {
            KillAttemptPolicy.Decide(new KillAftermathFacts {Killed = true}).Should().Be(KillAftermath.ConversationEnds);
        }

        // STAKE: Gabriel, 31/10/2026: "si captif, le joueur se sauve"; zhenja1 was his killer's prisoner.
        [Test]
        public void GIVEN_a_captive_who_kills_the_captor_WHEN_deciding_what_follows_THEN_the_player_escapes()
        {
            KillAttemptPolicy.Decide(new KillAftermathFacts {Killed = true, PlayerIsCaptive = true}).Should().Be(KillAftermath.PlayerEscapes);
        }

        // STAKE: the character's line that carried the attempt was written before the roll and may already tell a
        // death; the narrator must be told the outcome wins, and on a failure must leave the character's answer to them.
        [Test]
        public void GIVEN_a_failed_attempt_WHEN_the_narrator_is_briefed_THEN_it_is_told_the_failure_and_to_stop_before_the_answer()
        {
            string task = KillAttemptPolicy.NarratorTask(new KillNarrationFacts {
                PlayerName = "Zhen", VictimName = "Garet", Killed = false, Exposure = ViolenceExposure.Guarded,
                RecentLines = new[] {"Zhen: I draw my knife.", "Garet: You'd dare?"}
            });

            task.Should().Contain("OUTCOME: you fail").And.Contain("Garet survives").And.Contain("before Garet says or decides anything");
            task.Should().Contain("Zhen: I draw my knife.");
            KillAttemptPolicy.NarratorInstructions.Should().Contain("the outcome given to you is what happened");
        }

        // STAKE: Gabriel asked that a kill be told; a captive's kill must also tell the escape, or the scene ends in
        // chains while the game frees the player.
        [Test]
        public void GIVEN_a_captive_kill_WHEN_the_narrator_is_briefed_THEN_the_death_and_the_escape_are_told()
        {
            string task = KillAttemptPolicy.NarratorTask(new KillNarrationFacts {
                PlayerName = "Zhen", VictimName = "Garet", Killed = true, PlayerIsCaptive = true, RecentLines = new string[0]
            });

            task.Should().Contain("OUTCOME: you succeed").And.Contain("Garet dies").And.Contain("slip away");
            task.Should().NotContain("THE CONVERSATION SO FAR");
        }

        // STAKE: the narrator reads only the last lines; a long conversation must not flood the call.
        [Test]
        public void GIVEN_a_long_conversation_WHEN_the_narrator_is_briefed_THEN_only_the_last_lines_are_read()
        {
            string[] lines = Enumerable.Range(1, 20).Select(i => $"Line {i:00}").ToArray();

            string task = KillAttemptPolicy.NarratorTask(new KillNarrationFacts {VictimName = "Garet", RecentLines = lines});

            task.Should().Contain("Line 20").And.Contain($"Line {20 - KillAttemptPolicy.RecentLineCount + 1:00}");
            task.Should().NotContain($"Line {20 - KillAttemptPolicy.RecentLineCount:00}");
        }

        // STAKE: when the model gives nothing, the chat must still say plainly what happened, never stay silent as before.
        [Test]
        public void GIVEN_no_narration_WHEN_falling_back_THEN_the_plain_outcome_is_said()
        {
            KillAttemptPolicy.FallbackNarration(new KillNarrationFacts {VictimName = "Garet", Killed = true}).Should().Be("You kill Garet.");
            KillAttemptPolicy.FallbackNarration(new KillNarrationFacts {VictimName = "Garet", Exposure = ViolenceExposure.Guarded})
                .Should().Be("Your attempt on Garet fails. Those at their side saw to that.");
        }
    }
}
