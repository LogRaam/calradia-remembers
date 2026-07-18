// Code written by Gabriel Mailhot, 18/07/2026.
// Round-table audit D7. When the player opens the floor, the lap that follows gives each person in the room
// a real turn in their own voice. The main NPC's [WITNESS_REACTION] lines on that same turn are the model
// ventriloquizing those same people, so showing them made a witness speak twice in a row, and since the two
// lines come from two different model calls the second could flatly contradict the first. This policy picks
// which voice wins. It was an inline condition in ChatViewModel.Exchanges and therefore untestable (the mod
// is net472, this project is net10).
//
// Each test below names the fault it guards.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class VentriloquizedReactionPolicyTests
   {
      // The fault itself: floor open, host speaking, ordinary conversation still running. The lap is about to
      // voice these people properly, so the ventriloquized version is the one to drop.
      [Test]
      public void GIVEN_the_floor_is_open_on_the_hosts_turn_WHEN_deciding_THEN_the_reactions_are_dropped()
      {
         VentriloquizedReactionPolicy.DropsReactions(true, true, false, false).Should().BeTrue();
      }

      // The default, and the guard against a policy that silences the room by always answering yes: with no
      // lap coming, witness reactions are the ONLY way anyone but the host speaks.
      [Test]
      public void GIVEN_no_floor_request_is_pending_WHEN_deciding_THEN_the_reactions_are_kept()
      {
         VentriloquizedReactionPolicy.DropsReactions(false, true, false, false).Should().BeFalse();
      }

      // A reply routed to a witness is already that witness's own voice, not the host ventriloquizing the
      // room, and the lap is not about to repeat it. Dropping here would cost a turn for nothing.
      [Test]
      public void GIVEN_a_witness_and_not_the_host_is_replying_WHEN_deciding_THEN_the_reactions_are_kept()
      {
         VentriloquizedReactionPolicy.DropsReactions(true, false, false, false).Should().BeFalse();
      }

      // A captive scene never opens the floor to a round table, so a pending flag reaching this path is
      // stale state rather than an instruction. Dropping the captor's staged reactions would gut the scene.
      [Test]
      public void GIVEN_a_captive_scene_WHEN_deciding_THEN_the_reactions_are_kept()
      {
         VentriloquizedReactionPolicy.DropsReactions(true, true, true, false).Should().BeFalse();
      }

      // Once the conversation has ended there is no lap left to defer to. Dropping the lines would silence
      // the room's last word instead of replacing it with a better voice.
      [Test]
      public void GIVEN_the_conversation_has_already_ended_WHEN_deciding_THEN_the_reactions_are_kept()
      {
         VentriloquizedReactionPolicy.DropsReactions(true, true, false, true).Should().BeFalse();
      }
   }
}
