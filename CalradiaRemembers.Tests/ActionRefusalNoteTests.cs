// Code written by Gabriel Mailhot, 18/07/2026.
// When the bridge refuses an action the prompt let through, the note this builds is what tells the MODEL.
// The UI line reaches only the player; without a transcript note the model keeps believing the marriage or
// the transfer it just celebrated happened, and every later turn builds on it. The rule lived inside
// ChatViewModel.Exchanges until now, which meant it had NO test at all (the mod is net472, this is net10).
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
   public sealed class ActionRefusalNoteTests
   {
      // The note has three jobs and must do all three: mark itself out of character (so the model does not
      // read it as the player speaking), carry the bridge's reason, and state outright that it did not
      // happen. A note that only reported the reason would leave the outcome ambiguous.
      [Test]
      public void GIVEN_a_refused_action_WHEN_building_the_note_THEN_it_carries_the_reason_and_denies_the_deed()
      {
         string note = ActionRefusalNote.For(true, "You are at war with her kingdom.");

         note.Should().StartWith("(Out of character:");
         note.Should().Contain("the game refused that");
         note.Should().Contain("You are at war with her kingdom.");
         note.Should().Contain("It did not happen.");
         note.Should().Contain("Do not treat it as done.");
      }

      // tashmetu (Nexus, 27/09/2026): a refused second payment said the lord's purse held nothing, and the model
      // EXPLAINED it in the world (the player had seized her treasury), lost her regard over it, and remembered it.
      // The note already said the deed did not happen; it never said the REASON is a rule of the game rather than
      // something that happened in the story. It must, or every refusal is a seed for an invented event.
      [Test]
      public void GIVEN_a_refusal_with_a_reason_WHEN_building_the_note_THEN_the_reason_is_not_to_be_explained_or_remembered_as_an_event()
      {
         string note = ActionRefusalNote.For(true, "(Domina Ingunde has no coin to give, so none changed hands.)");

         note.Should().Contain("That reason is a rule of the game, not an event in the story");
         note.Should().Contain("do not explain it within the world");
         note.Should().Contain("do not remember it as something that happened");
      }

      // bdionysis80 (Discord, 21/11/2026): the model invented a verb the same turn the player paid Queen Taarna
      // 2406 denars. The refusal note named no deed, and she took the coin itself to be what "did not happen". The
      // note for an invented verb must name that verb and say the rest of the turn stands.
      [Test]
      public void GIVEN_an_invented_verb_beside_a_real_payment_WHEN_building_its_note_THEN_it_names_only_that_verb_and_the_payment_stands()
      {
         string note = ActionRefusalNote.ForUnknownVerb("complete_revenue_farming");

         note.Should().StartWith("(Out of character:");
         note.Should().Contain("'complete_revenue_farming'");
         note.Should().Contain("that one thing did not happen");
         note.Should().Contain("a payment");
         note.Should().Contain("IS done");
      }

      // Same report, the other half: the player pressed Y on "Give Queen Taarna 2406 coins?", the coin moved, and
      // the model never learned it. The window showed "You give Queen Taarna 2406 denars"; the transcript the model
      // reads next turn had nothing, so the queen kept asking whether the taxes were collected.
      [Test]
      public void GIVEN_a_confirmed_payment_the_game_carried_out_WHEN_building_the_note_THEN_the_model_is_told_it_is_done()
      {
         string note = ActionRefusalNote.ForConfirmedDeed(false, "You give Queen Taarna 2406 denars.");

         note.Should().StartWith("(Out of character:");
         note.Should().Contain("You give Queen Taarna 2406 denars.");
         note.Should().Contain("It is done");
      }

      // A confirmed deed the bridge then refused must not ALSO be called done: the refusal note already says it did
      // not happen, and two notes saying opposite things would be worse than none.
      [Test]
      public void GIVEN_a_confirmed_deed_the_bridge_refused_WHEN_building_the_done_note_THEN_nothing_is_written()
      {
         ActionRefusalNote.ForConfirmedDeed(true, "Your purse holds too little.").Should().BeNull();
      }

      // The gate itself: an action the bridge carried out must leave the transcript alone. A note on a
      // successful action would tell the model its own accomplished deed never happened, which is the exact
      // fault this class exists to prevent, inverted.
      [Test]
      public void GIVEN_an_action_that_was_not_refused_WHEN_building_the_note_THEN_nothing_is_written()
      {
         ActionRefusalNote.For(false, "The gold changed hands.").Should().BeNull();
      }

      // The defect the extraction caught. The refusal is what matters and the bridge does not always explain
      // itself; the controller interpolated the message unconditionally, so an outcome with no message
      // produced a note trailing a dangling separator into the prompt. The denial must still be stated
      // plainly, reason or no reason.
      [TestCase("")]
      [TestCase("   ")]
      [TestCase(null)]
      public void GIVEN_a_refusal_the_bridge_did_not_explain_WHEN_building_the_note_THEN_it_still_denies_the_deed_cleanly(string message)
      {
         string note = ActionRefusalNote.For(true, message);

         note.Should().Be("(Out of character: the game refused that. It did not happen. Do not treat it as done.)");
      }
   }
}
