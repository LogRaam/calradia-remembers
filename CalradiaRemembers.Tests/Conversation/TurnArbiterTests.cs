// Code written by Gabriel Mailhot, 26/11/2026.
// TurnArbiter: who holds the floor next in a group conversation. Gabriel's design (26/11/2026): speech is exclusive,
// intention is concurrent; the one addressed has a priority, not a monopoly; reactions are not replies; intentions are
// re-judged after every line, never queued; a cheap judge model gives each listener's wish to speak and a reaction, the
// game gives the rest. Each test below is one of the fourteen scenarios Gabriel validated on 26/11/2026 ("ok pour les
// scenarios"), plus rooms of five and six he asked about ("Est-ce que tu simules une conversation a plus que 3?"). If the
// referee is wrong, two people talk at once, a provoked character sits silent, a question hangs, or the characters talk
// among themselves while the player waits.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class TurnArbiterTests
   {
      private static FloorDecision AfterPlayer(params FloorBid[] bids) => TurnArbiter.Decide(new FloorRound {Bids = bids, PlayerSpokeLast = true});

      private static FloorDecision AfterNpcLines(int lines, params FloorBid[] bids) => TurnArbiter.Decide(new FloorRound {Bids = bids, NpcLinesSincePlayer = lines});

      // ── the fourteen validated scenarios ──

      // STAKE (1): the player asks N1 the way to Pravend; N2 has nothing to say. N1 answers, N2 stays quiet.
      [Test]
      public void GIVEN_a_direct_question_and_a_listener_with_nothing_to_say_WHEN_decided_THEN_the_one_asked_answers()
      {
         FloorDecision d = AfterPlayer(new FloorBid {Id = "N1", Addressed = true, Intent = JudgedIntent.High}, new FloorBid {Id = "N2", Intent = JudgedIntent.None});

         d.SpeakerId.Should().Be("N1");
         d.ReactorIds.Should().BeEmpty();
      }

      // STAKE (2): "did you kill his brother?" N1 answers; N2 shows surprise without taking the floor.
      [Test]
      public void GIVEN_a_listener_who_only_reacts_WHEN_decided_THEN_the_reaction_shows_and_the_one_asked_speaks()
      {
         FloorDecision d = AfterPlayer(new FloorBid {Id = "N1", Addressed = true, Intent = JudgedIntent.High}, new FloorBid {Id = "N2", Intent = JudgedIntent.None, Reaction = "*stares, startled*"});

         d.SpeakerId.Should().Be("N1");
         d.ReactorIds.Should().Equal("N2");
      }

      // STAKE (3), Gabriel's own case: mocked while the player talks to N1, N2 cuts in; N1, cut off, is owed the next line.
      [Test]
      public void GIVEN_the_player_mocks_N2_while_speaking_to_N1_WHEN_decided_THEN_N2_cuts_in_and_N1_answers_next()
      {
         FloorDecision cut = AfterPlayer(new FloorBid {Id = "N1", Addressed = true, Intent = JudgedIntent.High}, new FloorBid {Id = "N2", Targeted = true, Intent = JudgedIntent.Urgent});

         cut.SpeakerId.Should().Be("N2");
         cut.CutOffId.Should().Be("N1");

         FloorDecision next = AfterNpcLines(1, new FloorBid {Id = "N1", WasCutOff = true, Intent = JudgedIntent.High}, new FloorBid {Id = "N2", SpokeLast = true, Intent = JudgedIntent.High});
         next.SpeakerId.Should().Be("N1");
      }

      // STAKE (4): "what do you all think?" One speaks, the one with the most at stake or the highest rank; never two at once.
      [Test]
      public void GIVEN_a_line_to_everyone_WHEN_decided_THEN_one_speaks_by_stake_then_rank()
      {
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.High, Standing = 1}, new FloorBid {Id = "N2", Intent = JudgedIntent.Urgent, Standing = 1})
            .SpeakerId.Should().Be("N2");
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.High, Standing = 1}, new FloorBid {Id = "N2", Intent = JudgedIntent.High, Standing = 3})
            .SpeakerId.Should().Be("N2");
      }

      // STAKE (5), the war proposal: N1 wins and says the treasury is empty; N2 is judged again on THAT line and may fall
      // silent. The referee keeps no queued reply: what N2 wanted before N1 spoke does not count.
      [Test]
      public void GIVEN_N2_rejudged_after_N1_WHEN_decided_THEN_only_the_new_wish_counts()
      {
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.Urgent, Standing = 2}, new FloorBid {Id = "N2", Intent = JudgedIntent.High}).SpeakerId.Should().Be("N1");

         AfterNpcLines(1, new FloorBid {Id = "N1", SpokeLast = true}, new FloorBid {Id = "N2", Intent = JudgedIntent.None}).Outcome.Should().Be(FloorOutcome.BackToPlayer);
         AfterNpcLines(1, new FloorBid {Id = "N1", SpokeLast = true}, new FloorBid {Id = "N2", Intent = JudgedIntent.High}).SpeakerId.Should().Be("N2");
      }

      // STAKE (6): two characters arguing get two lines, then the player has the floor back unless it is urgent; past the
      // ceiling, even urgent, it returns to the player.
      [Test]
      public void GIVEN_characters_arguing_WHEN_their_lines_mount_THEN_the_floor_returns_to_the_player()
      {
         AfterNpcLines(2, new FloorBid {Id = "N1", SpokeLast = true}, new FloorBid {Id = "N2", Intent = JudgedIntent.High}).Outcome.Should().Be(FloorOutcome.BackToPlayer);
         AfterNpcLines(2, new FloorBid {Id = "N1", SpokeLast = true}, new FloorBid {Id = "N2", Intent = JudgedIntent.Urgent}).SpeakerId.Should().Be("N2");
         AfterNpcLines(TurnArbiter.NpcLinesCeiling, new FloorBid {Id = "N1", SpokeLast = true}, new FloorBid {Id = "N2", Intent = JudgedIntent.Urgent}).Outcome.Should().Be(FloorOutcome.BackToPlayer);
      }

      // STAKE (7): a ruler with a burning view speaks before the companion addressed; with a mild one, the companion answers.
      [Test]
      public void GIVEN_a_ruler_present_WHEN_the_player_speaks_to_a_companion_THEN_rank_wins_only_with_an_urgent_wish()
      {
         var companion = new FloorBid {Id = "companion", Addressed = true, Intent = JudgedIntent.High, Standing = 0};

         AfterPlayer(companion, new FloorBid {Id = "king", Intent = JudgedIntent.Urgent, Standing = 4}).SpeakerId.Should().Be("king");
         AfterPlayer(companion, new FloorBid {Id = "king", Intent = JudgedIntent.Low, Standing = 4}).SpeakerId.Should().Be("companion");
      }

      // STAKE (8): a remark on the weather to nobody. At most one brief reply; never two idle ones.
      [Test]
      public void GIVEN_an_idle_remark_to_nobody_WHEN_decided_THEN_one_brief_reply_at_most()
      {
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.Low}, new FloorBid {Id = "N2", Intent = JudgedIntent.Low, TieBreak = 1}).SpeakerId.Should().Be("N1");

         AfterNpcLines(1, new FloorBid {Id = "N1", SpokeLast = true}, new FloorBid {Id = "N2", Intent = JudgedIntent.Low}).Outcome.Should().Be(FloorOutcome.BackToPlayer);
      }

      // STAKE (9): N2 has been silent five lines and the talk touches his town; his silence lifts him over an equal wish.
      [Test]
      public void GIVEN_a_long_silent_character_WHEN_both_want_to_speak_THEN_the_silent_one_rises()
      {
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.High, TurnsSilent = 0}, new FloorBid {Id = "N2", Intent = JudgedIntent.High, TurnsSilent = 5})
            .SpeakerId.Should().Be("N2");
      }

      // STAKE (10): nobody speaks twice running unless the line was put to them.
      [Test]
      public void GIVEN_N1_just_spoke_WHEN_nobody_asked_him_again_THEN_he_does_not_take_the_next_line()
      {
         AfterNpcLines(1, new FloorBid {Id = "N1", SpokeLast = true, Intent = JudgedIntent.Urgent}, new FloorBid {Id = "N2", Intent = JudgedIntent.None})
            .Outcome.Should().Be(FloorOutcome.BackToPlayer);
         AfterNpcLines(1, new FloorBid {Id = "N1", SpokeLast = true, Addressed = true, Intent = JudgedIntent.High}, new FloorBid {Id = "N2", Intent = JudgedIntent.None})
            .SpeakerId.Should().Be("N1");
      }

      // STAKE (11): "And you, N2?" while N1 was eager: a question by name beats almost everything.
      [Test]
      public void GIVEN_a_question_by_name_WHEN_another_is_eager_THEN_the_one_named_answers()
      {
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.Urgent, Standing = 4}, new FloorBid {Id = "N2", AskedByName = true, Addressed = true, Intent = JudgedIntent.Low})
            .SpeakerId.Should().Be("N2");
      }

      // STAKE (12): a perfect tie is broken by rank, then by time waited, then by the drawn order; never by seat alone.
      [Test]
      public void GIVEN_a_perfect_tie_WHEN_decided_THEN_rank_then_waiting_then_the_draw_break_it()
      {
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.High, TieBreak = 0}, new FloorBid {Id = "N2", Intent = JudgedIntent.High, TieBreak = 1}).SpeakerId.Should().Be("N1");
         AfterPlayer(new FloorBid {Id = "N1", Intent = JudgedIntent.High, TieBreak = 1}, new FloorBid {Id = "N2", Intent = JudgedIntent.High, TieBreak = 0}).SpeakerId.Should().Be("N2");
      }

      // STAKE (13): the player speaks to N1, who has little to say; N2 and N3 both want to contradict. Exactly one of them speaks.
      [Test]
      public void GIVEN_two_listeners_eager_to_contradict_WHEN_decided_THEN_exactly_one_speaks_and_the_other_waits()
      {
         FloorDecision d = AfterPlayer(new FloorBid {Id = "N1", Addressed = true, Intent = JudgedIntent.Low},
                                       new FloorBid {Id = "N2", Intent = JudgedIntent.Urgent, TieBreak = 1},
                                       new FloorBid {Id = "N3", Intent = JudgedIntent.Urgent, TieBreak = 0});

         d.SpeakerId.Should().Be("N3");
         d.CutOffId.Should().Be("N1");
      }

      // STAKE (14): a grunt or a laugh respects exclusive speech: reactions come one at a time, before the next line, in order.
      [Test]
      public void GIVEN_several_reactions_WHEN_decided_THEN_they_come_one_after_another_before_the_line()
      {
         FloorDecision d = AfterPlayer(new FloorBid {Id = "N1", Addressed = true, Intent = JudgedIntent.High, Reaction = "*sets down the cup*"},
                                       new FloorBid {Id = "N2", Intent = JudgedIntent.None, Reaction = "*laughs aloud*"},
                                       new FloorBid {Id = "N3", Intent = JudgedIntent.None});

         d.ReactorIds.Should().Equal("N1", "N2");
         d.SpeakerId.Should().Be("N1");
      }

      // ── deference and temperament (Gabriel, 26/11/2026: "oui, fais la regle de deference. timide reste silencieux") ──

      // STAKE: measured the same day, the judge reads Ira as Urgent whether a king or a peasant mocks her; before the king
      // she must hold her tongue (her anger shows in her reaction), before the peasant she answers at once.
      [Test]
      public void GIVEN_a_slight_from_a_king_WHEN_the_mercenary_is_urgent_THEN_she_holds_back_but_answers_a_peasant()
      {
         FloorDecision beforeKing = AfterNpcLines(1, new FloorBid {Id = "Ira", Targeted = true, Intent = JudgedIntent.Urgent, Standing = 1, Reaction = "*jaw tightens*", LastSpeakerStanding = 4});
         beforeKing.Outcome.Should().Be(FloorOutcome.BackToPlayer);
         beforeKing.ReactorIds.Should().Equal("Ira");

         AfterNpcLines(1, new FloorBid {Id = "Ira", Targeted = true, Intent = JudgedIntent.Urgent, Standing = 1, LastSpeakerStanding = 0}).SpeakerId.Should().Be("Ira");
      }

      // STAKE: a bold temperament holds back a notch less: the mercenary who scorns noble airs may still retort to a king.
      [Test]
      public void GIVEN_a_bold_listener_slighted_by_a_king_WHEN_decided_THEN_she_still_speaks()
      {
         AfterNpcLines(1, new FloorBid {Id = "Ira", Targeted = true, Intent = JudgedIntent.Urgent, Standing = 1, LastSpeakerStanding = 4, Bold = true})
            .SpeakerId.Should().Be("Ira");
      }

      // STAKE: one step of rank (a lord rebuked by his king) costs one notch: a burning wish still speaks, a mere wish does not.
      [Test]
      public void GIVEN_one_step_of_rank_WHEN_the_lord_is_rebuked_THEN_only_a_burning_wish_speaks()
      {
         AfterNpcLines(1, new FloorBid {Id = "Garios", Targeted = true, Intent = JudgedIntent.Urgent, Standing = 3, LastSpeakerStanding = 4}).SpeakerId.Should().Be("Garios");
         AfterNpcLines(1, new FloorBid {Id = "Garios", Targeted = true, Intent = JudgedIntent.High, Standing = 3, LastSpeakerStanding = 4}).Outcome.Should().Be(FloorOutcome.BackToPlayer);
      }

      // STAKE: spoken to by the king, nobody defers: a question from above is answered.
      [Test]
      public void GIVEN_the_king_addresses_the_listener_WHEN_decided_THEN_rank_does_not_silence_the_answer()
      {
         AfterNpcLines(1, new FloorBid {Id = "Bryn", Addressed = true, Intent = JudgedIntent.Low, Standing = 0, LastSpeakerStanding = 4}).SpeakerId.Should().Be("Bryn");
      }

      // STAKE: "timide reste silencieux": the squire mocked before his lord burns red but never takes the floor; asked, he answers.
      [Test]
      public void GIVEN_a_timid_squire_mocked_WHEN_he_is_urgent_THEN_he_stays_silent_unless_spoken_to()
      {
         FloorDecision mocked = AfterPlayer(new FloorBid {Id = "Bryn", Timid = true, Targeted = true, Intent = JudgedIntent.Urgent, Reaction = "*face burns red*"});
         mocked.Outcome.Should().Be(FloorOutcome.Silence);
         mocked.ReactorIds.Should().Equal("Bryn");

         AfterPlayer(new FloorBid {Id = "Bryn", Timid = true, AskedByName = true, Addressed = true, Intent = JudgedIntent.None}).SpeakerId.Should().Be("Bryn");
      }

      // STAKE: the simulated scene of 26/11/2026: Arwa asks the timid squire, his lord cuts in, and the squire must still get
      // his answer back on the next round, though nobody addresses him any more and his lord outranks everyone.
      [Test]
      public void GIVEN_a_timid_squire_cut_off_by_his_lord_WHEN_the_next_round_comes_THEN_he_still_answers()
      {
         FloorDecision cut = AfterPlayer(new FloorBid {Id = "Bryn", Timid = true, Addressed = true, Intent = JudgedIntent.Urgent, Standing = 0, LastSpeakerStanding = 2},
                                         new FloorBid {Id = "Garios", Targeted = true, Intent = JudgedIntent.Urgent, Standing = 3, LastSpeakerStanding = 2});
         cut.SpeakerId.Should().Be("Garios");
         cut.CutOffId.Should().Be("Bryn");

         AfterNpcLines(1, new FloorBid {Id = "Bryn", Timid = true, WasCutOff = true, Intent = JudgedIntent.Low, Standing = 0, LastSpeakerStanding = 3},
                          new FloorBid {Id = "Garios", SpokeLast = true, Standing = 3})
            .SpeakerId.Should().Be("Bryn");
      }

      // ── larger rooms ──

      // STAKE: six in the room, four of them eager: still one voice at a time, and the one the player asked by name.
      [Test]
      public void GIVEN_six_characters_four_of_them_eager_WHEN_decided_THEN_still_one_voice_the_one_asked()
      {
         FloorDecision d = AfterPlayer(Enumerable.Range(1, 6).Select(i => new FloorBid {
            Id = "N" + i, Intent = i <= 4 ? JudgedIntent.Urgent : JudgedIntent.None, Standing = i % 3, TieBreak = i,
            AskedByName = i == 6, Addressed = i == 6
         }).ToArray());

         d.Outcome.Should().Be(FloorOutcome.Speak);
         d.SpeakerId.Should().Be("N6");
      }

      // STAKE: five characters bickering never run past the ceiling: the player always gets the floor back.
      [Test]
      public void GIVEN_five_characters_all_urgent_WHEN_lines_mount_THEN_the_ceiling_returns_the_floor_to_the_player()
      {
         var spoken = new List<string>();
         string last = null;
         for (var lines = 0; lines < 10; lines++)
         {
            string previous = last;
            FloorDecision d = AfterNpcLines(lines, Enumerable.Range(1, 5).Select(i => new FloorBid {Id = "N" + i, Intent = JudgedIntent.Urgent, SpokeLast = "N" + i == previous, TieBreak = i}).ToArray());
            if (d.Outcome != FloorOutcome.Speak) break;

            spoken.Add(d.SpeakerId);
            last = d.SpeakerId;
         }

         spoken.Count.Should().Be(TurnArbiter.NpcLinesCeiling);
         spoken.Zip(spoken.Skip(1), (a, b) => a != b).Should().AllBeEquivalentTo(true, "nobody speaks twice running");
      }

      // STAKE: in a room of six, a judge call per head per line is too slow and too dear; the ones the line concerns are
      // judged first, then the longest silent, and the one who just spoke is not judged at all.
      [Test]
      public void GIVEN_six_listeners_WHEN_choosing_whom_to_judge_THEN_those_concerned_come_first_within_the_limit()
      {
         var room = new List<FloorBid> {
            new FloorBid {Id = "spoke", SpokeLast = true},
            new FloorBid {Id = "quiet_long", TurnsSilent = 6},
            new FloorBid {Id = "named", AskedByName = true},
            new FloorBid {Id = "mocked", Targeted = true},
            new FloorBid {Id = "addressed", Addressed = true},
            new FloorBid {Id = "quiet_short", TurnsSilent = 1},
            new FloorBid {Id = "quiet_mid", TurnsSilent = 3}
         };

         IReadOnlyList<string> judged = TurnArbiter.WhoToJudge(room);

         judged.Should().HaveCount(TurnArbiter.JudgedPerRound);
         judged.Should().Equal("named", "addressed", "mocked", "quiet_long");
      }
   }
}
