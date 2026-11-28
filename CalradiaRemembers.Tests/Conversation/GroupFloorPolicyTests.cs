// Code written by Gabriel Mailhot, 28/11/2026.
// The group engine of the Dramaturge mode (Stage 7 of the group-scene plan): in an ensemble scene the floor judge and the turn
// arbiter decide who speaks next, in place of the fixed order (Gabriel, 26/11/2026: "speech is exclusive, intention is
// concurrent; a reaction is not a reply; the one addressed has a priority, not a monopoly"). NOT A/B-validated: only in the
// Dramaturge mode.
//
// What the policy decides and what breaks if it is wrong: the bids the referee weighs (a player who speaks to someone gets no
// answer, a character called on by name is ignored, a lord's rank counts for nothing, the one cut off is never owed the
// floor), the cap (a player line draws a monologue of the room, or no answer at all), and the narrated reaction (every line
// drowned in gestures, the speaker's own gesture narrated before they speak).

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class GroupFloorPolicyTests
   {
      private static FloorJudgeVerdict V(JudgedIntent intent, string reaction = null) => new() {Intent = intent, Reaction = reaction, Parsed = true};

      private static GroupFloorListener L(string id, JudgedIntent intent, string reaction = null, int standing = 1, int silent = 1, bool named = false, int tie = 0)
         => new() {Id = id, Verdict = V(intent, reaction), Standing = standing, TurnsSilent = silent, Named = named, TieBreak = tie};

      // STAKE: the rule for the night's build: Standard and Enhanced, and the Roleplay switch, keep the fixed ensemble order.
      [Test]
      public void GIVEN_any_mode_but_Dramaturge_with_its_switch_WHEN_asked_THEN_the_group_engine_does_not_run()
      {
         GroupFloorPolicy.Runs(new GroupFloorGateFacts {DramaturgeMode = true, SwitchOnTheMcmMode = true}).Should().BeTrue();
         GroupFloorPolicy.Runs(new GroupFloorGateFacts {DramaturgeMode = false, SwitchOnTheMcmMode = true}).Should().BeFalse();
         GroupFloorPolicy.Runs(new GroupFloorGateFacts {DramaturgeMode = true, SwitchOnTheMcmMode = false}).Should().BeFalse();
      }

      // STAKE: the MCM "NPC Cross-Talk" still bounds how much of a beat the player gives up, the arbiter's ceiling holds above
      // it, and even at 0 the player's line gets its one answer.
      [Test]
      public void GIVEN_the_cross_talk_setting_WHEN_the_cap_is_read_THEN_one_line_plus_the_rounds_within_the_ceiling()
      {
         GroupFloorPolicy.LineCap(0).Should().Be(1);
         GroupFloorPolicy.LineCap(1).Should().Be(2);
         GroupFloorPolicy.LineCap(2).Should().Be(TurnArbiter.NpcLinesCeiling);
         GroupFloorPolicy.LineCap(3).Should().Be(TurnArbiter.NpcLinesCeiling);
         GroupFloorPolicy.LineCap(-4).Should().Be(1);
      }

      // STAKE: Gabriel's deference rule needs rank: a ruler above a great house above a minor lord; a companion serves the player.
      [Test]
      public void GIVEN_ranks_WHEN_standing_is_read_THEN_a_ruler_stands_highest_and_a_companion_lowest()
      {
         GroupFloorPolicy.StandingOf(new Stature(2, true), false).Should().Be(4);
         GroupFloorPolicy.StandingOf(new Stature(Stature.GreatHouseTier, false), false).Should().Be(3);
         GroupFloorPolicy.StandingOf(new Stature(Stature.LandedNobleTier, false), false).Should().Be(2);
         GroupFloorPolicy.StandingOf(new Stature(Stature.EstablishedTier, false), false).Should().Be(1);
         GroupFloorPolicy.StandingOf(new Stature(0, false), false).Should().Be(0);
         GroupFloorPolicy.StandingOf(new Stature(5, false), true).Should().Be(0);
      }

      // STAKE: one who has said nothing for long rises; the count is lines by others since their own.
      [Test]
      public void GIVEN_the_speakers_in_order_WHEN_silence_is_counted_THEN_it_is_the_lines_since_their_own()
      {
         var order = new List<string> {null, "ira", null, "nabb", null};

         GroupFloorPolicy.TurnsSilent(order, "ira").Should().Be(3);
         GroupFloorPolicy.TurnsSilent(order, "nabb").Should().Be(1);
         GroupFloorPolicy.TurnsSilent(order, "edric").Should().Be(5);
      }

      // STAKE: the player spoke to Ira: she answers even when the judge says she has nothing to add (no question left hanging).
      [Test]
      public void GIVEN_the_player_speaks_to_one_WHEN_the_round_is_decided_THEN_the_one_addressed_speaks_though_judged_none()
      {
         List<FloorBid> bids = GroupFloorPolicy.BuildBids(new GroupFloorRound {
            Listeners = new[] {L("ira", JudgedIntent.None), L("nabb", JudgedIntent.Low)}, PlayerSpokeLast = true, AddressedId = "ira", LastSpeakerStanding = 1
         });

         FloorDecision d = TurnArbiter.Decide(new FloorRound {Bids = bids, PlayerSpokeLast = true});

         d.Outcome.Should().Be(FloorOutcome.Speak);
         d.SpeakerId.Should().Be("ira");
         bids.Single(b => b.SpokeLast).Id.Should().Be("_player");
      }

      // STAKE: "the one addressed has a priority, not a monopoly": a listener mocked by name and urgent takes the floor.
      [Test]
      public void GIVEN_a_listener_mocked_by_name_and_urgent_WHEN_the_line_was_put_to_another_THEN_the_mocked_one_cuts_in()
      {
         List<FloorBid> bids = GroupFloorPolicy.BuildBids(new GroupFloorRound {
            Listeners = new[] {L("ira", JudgedIntent.Low), L("nabb", JudgedIntent.Urgent, "held-back anger", named: true)},
            PlayerSpokeLast = true, AddressedId = "ira", LastSpeakerStanding = 1
         });

         FloorDecision d = TurnArbiter.Decide(new FloorRound {Bids = bids, PlayerSpokeLast = true});

         bids.Single(b => b.Id == "nabb").Targeted.Should().BeTrue();
         d.SpeakerId.Should().Be("nabb");
         d.CutOffId.Should().Be("ira");
      }

      // STAKE: a character who calls another by name hands them the floor: they are asked, and answer.
      [Test]
      public void GIVEN_a_character_calls_another_by_name_WHEN_the_bids_are_built_THEN_that_one_is_asked_by_name()
      {
         List<FloorBid> bids = GroupFloorPolicy.BuildBids(new GroupFloorRound {
            Listeners = new[] {L("ira", JudgedIntent.None)}, LastSpeakerId = "nabb", CalledOnId = "ira", LastSpeakerStanding = 1
         });

         FloorBid ira = bids.Single(b => b.Id == "ira");
         ira.AskedByName.Should().BeTrue();
         ira.Addressed.Should().BeTrue();
         bids.Single(b => b.SpokeLast).Id.Should().Be("nabb");
      }

      // STAKE: the one cut off while answering is owed the next chance (the simulated scene of 26/11/2026 hung five beats on it).
      [Test]
      public void GIVEN_one_cut_off_last_round_WHEN_the_bids_are_built_THEN_they_are_owed_the_floor()
      {
         List<FloorBid> bids = GroupFloorPolicy.BuildBids(new GroupFloorRound {
            Listeners = new[] {L("ira", JudgedIntent.Low)}, LastSpeakerId = "nabb", CutOffId = "ira"
         });

         bids.Single(b => b.Id == "ira").WasCutOff.Should().BeTrue();
      }

      // STAKE: the simulator's measured rule: one sign a line at most, never on two lines running, never the speaker's own.
      [Test]
      public void GIVEN_reactors_WHEN_a_reaction_is_picked_THEN_one_at_most_never_the_speaker_never_two_lines_running()
      {
         var d = new FloorDecision {Outcome = FloorOutcome.Speak, SpeakerId = "ira", ReactorIds = new List<string> {"ira", "nabb", "edric"}};

         GroupFloorPolicy.PickReaction(new GroupReactionFacts {Decision = d, LinesSinceReaction = 1}).Should().Be("nabb");
         GroupFloorPolicy.PickReaction(new GroupReactionFacts {Decision = d, LinesSinceReaction = 0}).Should().BeNull();
      }

      // STAKE: Kael's jealousy narrated nine times in run 2: without an inner life to say a reaction is news, only a character's
      // first sign in the scene is shown.
      [Test]
      public void GIVEN_a_character_who_already_gave_a_sign_WHEN_a_reaction_is_picked_THEN_only_a_first_sign_is_shown()
      {
         var d = new FloorDecision {Outcome = FloorOutcome.Speak, SpeakerId = "ira", ReactorIds = new List<string> {"nabb", "edric"}};

         GroupFloorPolicy.PickReaction(new GroupReactionFacts {Decision = d, LinesSinceReaction = 2, ShownEver = new[] {"nabb"}}).Should().Be("edric");
         GroupFloorPolicy.PickReaction(new GroupReactionFacts {Decision = d, LinesSinceReaction = 2, ShownEver = new[] {"nabb", "edric"}}).Should().BeNull();
      }

      // STAKE: the reaction line reaches the player as the narrator's: no asterisks, no refusal, no copy of a line already shown.
      [Test]
      public void GIVEN_a_written_reaction_WHEN_read_THEN_it_is_cleaned_and_an_echo_or_refusal_is_dropped()
      {
         GroupReactionPrompt.Read("*Nabb sets his cup down without a sound.*\nmore", new string[0]).Should().Be("Nabb sets his cup down without a sound.");
         GroupReactionPrompt.Read("Nabb sets his cup down.", new[] {"*Nabb sets his cup down.*"}).Should().BeNull();
         GroupReactionPrompt.Read("", new string[0]).Should().BeNull();
      }
   }
}
