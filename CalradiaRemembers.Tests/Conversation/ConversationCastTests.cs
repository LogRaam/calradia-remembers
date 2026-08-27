// Code written by Gabriel Mailhot, 27/08/2026.
// Spotlight pillar foundation. A conversation starts focused on one character (the anchor) with others watching
// from the edge, and the player report this pillar answers is a witness who was spoken to yet stayed a mute
// bystander. The cast is the source of truth for who is a full participant and who is not, so the whole feature
// rests on these transitions being exact: a witness spoken to becomes active AND STAYS active; one told to hold
// their tongue drops back; and the anchor, the reason the conversation exists, can never be silenced. Get a sign
// wrong and either a drawn-in character keeps being ignored (the very bug) or the person you came to talk to
// gets shoved out of their own scene.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class ConversationCastTests
   {
      // The opening frame: the anchor is the one active voice, everyone else watches. If witnesses opened active,
      // an ordinary one-on-one with a bystander would already be a group scene, which is exactly what we do NOT want.
      [Test]
      public void GIVEN_a_new_conversation_WHEN_it_opens_THEN_only_the_anchor_is_active_and_the_rest_are_witnesses()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a", "guard_b"});

         cast.IsActive("lord_anchor").Should().BeTrue();
         cast.IsWitness("lady_a").Should().BeTrue();
         cast.IsWitness("guard_b").Should().BeTrue();
         cast.ActiveCount.Should().Be(1);
         cast.IsEnsemble.Should().BeFalse();
      }

      // The core move: a witness who is spoken to becomes a full participant. Returns true because it changed
      // something; this is the promotion the addressed-witness bug ultimately needs so they answer as themselves.
      [Test]
      public void GIVEN_a_witness_WHEN_promoted_THEN_they_become_active()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a"});

         cast.Promote("lady_a").Should().BeTrue();
         cast.IsActive("lady_a").Should().BeTrue();
      }

      // Two active voices is the ensemble threshold: from here every active participant may speak the turn (the
      // council model Gabriel chose). Pinned so the boundary that flips the turn style sits exactly at the second voice.
      [Test]
      public void GIVEN_one_promoted_witness_WHEN_checking_THEN_the_scene_is_an_ensemble()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a"});

         cast.Promote("lady_a");

         cast.ActiveCount.Should().Be(2);
         cast.IsEnsemble.Should().BeTrue();
      }

      // Promotion sticks: once drawn in, they remain a participant for the rest of the exchange, so a re-promote
      // is a no-op. If it did not stick, a drawn-in character would lapse back to silent between turns, the bug again.
      [Test]
      public void GIVEN_an_already_active_participant_WHEN_promoted_again_THEN_it_is_a_no_op()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a"});
         cast.Promote("lady_a");

         cast.Promote("lady_a").Should().BeFalse();
         cast.IsActive("lady_a").Should().BeTrue();
      }

      // Only people actually in the scene, and never the anchor (already active), can be promoted. A stray id
      // must not silently join the cast, or an off-scene character could be handed a voice they never had.
      [Test]
      public void GIVEN_the_anchor_or_an_absent_character_WHEN_promoted_THEN_nothing_happens()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a"});

         cast.Promote("lord_anchor").Should().BeFalse();
         cast.Promote("stranger_x").Should().BeFalse();
         cast.RoleOf("stranger_x").Should().BeNull();
      }

      // The reversal Gabriel asked for: a participant told to hold their tongue, having accepted it, drops back to
      // a witness. Returns true because it changed the cast; this is what "you'll speak when I command it" resolves to.
      [Test]
      public void GIVEN_a_promoted_participant_WHEN_demoted_THEN_they_return_to_witness()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a"});
         cast.Promote("lady_a");

         cast.Demote("lady_a").Should().BeTrue();
         cast.IsWitness("lady_a").Should().BeTrue();
         cast.IsEnsemble.Should().BeFalse();
      }

      // The anchor is the conversation's reason to exist and can never be silenced out of their own scene, even
      // if the fiction throws a "be quiet" at them. Without this guard, an order could leave the scene with no one to talk to.
      [Test]
      public void GIVEN_the_anchor_WHEN_demotion_is_attempted_THEN_it_is_refused()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a"});

         cast.Demote("lord_anchor").Should().BeFalse();
         cast.IsActive("lord_anchor").Should().BeTrue();
      }

      // Demoting someone who is already a witness (or absent) changes nothing: only a real active-to-witness move
      // returns true, so a caller can trust the result to mean "a participant was actually silenced."
      [Test]
      public void GIVEN_a_witness_or_absent_character_WHEN_demoted_THEN_it_is_a_no_op()
      {
         var cast = new ConversationCast("lord_anchor", new[] {"lady_a"});

         cast.Demote("lady_a").Should().BeFalse();   // already a witness
         cast.Demote("stranger_x").Should().BeFalse(); // not present
      }

      // A character who walks up mid-scene joins as a witness and can then be promoted like any other, so the
      // cast is not frozen at the opening roster. Introducing an existing member must not disturb their role.
      [Test]
      public void GIVEN_a_newcomer_WHEN_introduced_THEN_they_join_as_a_witness_and_can_be_promoted()
      {
         var cast = new ConversationCast("lord_anchor", new List<string>());

         cast.Introduce("late_c").Should().BeTrue();
         cast.IsWitness("late_c").Should().BeTrue();
         cast.Introduce("late_c").Should().BeFalse(); // already present
         cast.Promote("late_c").Should().BeTrue();
         cast.IsActive("late_c").Should().BeTrue();
      }
   }
}
