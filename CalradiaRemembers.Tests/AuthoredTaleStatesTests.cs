// Code written by Gabriel Mailhot, 11/10/2026.
// A tale whose stages are states of one conversation (Gabriel, 10/10/2026: "Les etapes optionnelles sont comme des etats
// plutot que des etapes qui font avancer le recit"): the player and his wife pass from one to another and back by what
// is said, each with its own picture. If these rules are wrong, she keeps speaking of the moment they already left
// (the conversation that took the choice froze her on the old stage), she is never told the moment she is in, or the
// picture stays on the first state while the words move on.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredTaleStatesTests
   {
      private const string Giver = AuthoredTales.Giver;

      private const string Falls = """
      {
        "id": "cr_test_falls",
        "title": "By the Falls",
        "binds": { "giver": "the player's wife" },
        "stages": [
          { "id": "invite", "journal": "{giver} has gone down to the water.",
            "objective": { "type": "talk_to", "hero": "{giver}" }, "on_complete": { "next": "together" } },
          { "id": "together", "journal": "{giver} waits in the pool.", "image": "together.jpg",
            "canvas": "You stand in the pool beneath the falls; the player has just found you.",
            "choices": [
              { "id": "shoulder", "label": "Her shoulder", "said": { "to": "{giver}", "meaning": "the player's attention goes to her shoulder" }, "next": "shoulder" },
              { "id": "lie_down", "label": "She lies down", "said": { "to": "{giver}", "meaning": "she lies down and invites the player" }, "next": "reward" }
            ] },
          { "id": "shoulder", "journal": "{giver}'s shoulder.", "image": "shoulder.jpg",
            "canvas": "Your wet dress has slipped from one shoulder.",
            "choices": [
              { "id": "back", "label": "Together again", "said": { "to": "{giver}", "meaning": "they turn back to each other" }, "next": "together" },
              { "id": "lie_down", "label": "She lies down", "said": { "to": "{giver}", "meaning": "she lies down and invites the player" }, "next": "reward" }
            ] }
        ],
        "reward": { "regard": 5, "memory": "A day by the falls with the player." }
      }
      """;

      private static (AuthoredQuest q, AuthoredQuestRun run) Together()
      {
         AuthoredQuest q = AuthoredTales.Load(Falls);
         AuthoredQuestRun run = AuthoredTales.Start(q);
         AuthoredQuestEngine.Accept(run, q, 100);
         run.StageId.Should().Be("together");

         return (q, run);
      }

      private static string Her(AuthoredQuestRun run, AuthoredQuest q) => AuthoredQuestPrompt.ForCharacter(run, q, Giver, null, offering: false) ?? "";

      // The conversation opens in the first state: she is told the moment she is in.
      [Test]
      public void GIVEN_a_state_decided_by_words_with_her_WHEN_she_speaks_THEN_she_is_told_that_moment()
      {
         (AuthoredQuest q, AuthoredQuestRun run) = Together();

         Her(run, q).Should().Contain("beneath the falls");
      }

      // The player's words move them to another state: she now speaks from it, not from the one they left.
      [Test]
      public void GIVEN_the_words_move_to_another_state_with_her_WHEN_she_speaks_THEN_she_speaks_from_the_new_state()
      {
         (AuthoredQuest q, AuthoredQuestRun run) = Together();

         AuthoredQuestEngine.Said(run, q, Giver, "shoulder", 100);

         run.StageId.Should().Be("shoulder");
         Her(run, q).Should().Contain("slipped from one shoulder").And.NotContain("beneath the falls");
      }

      // And back again, as often as they like (Gabriel: the player may return to an earlier state).
      [Test]
      public void GIVEN_they_return_to_an_earlier_state_WHEN_she_speaks_THEN_she_speaks_from_it_again()
      {
         (AuthoredQuest q, AuthoredQuestRun run) = Together();

         AuthoredQuestEngine.Said(run, q, Giver, "shoulder", 100);
         AuthoredQuestEngine.Said(run, q, Giver, "back", 100);

         run.StageId.Should().Be("together");
         Her(run, q).Should().Contain("beneath the falls").And.NotContain("slipped from one shoulder");
      }

      // The picture follows the state: a new one replaces the old; a state with none keeps the one already shown.
      [Test]
      public void GIVEN_the_state_changes_WHEN_the_picture_is_decided_THEN_a_new_picture_shows_and_none_keeps_the_old()
      {
         AuthoredTaleImages.ImageToShow("together.jpg", "shoulder.jpg").Should().Be("shoulder.jpg");
         AuthoredTaleImages.ImageToShow("together.jpg", null).Should().BeNull("a state without a picture keeps the one shown");
         AuthoredTaleImages.ImageToShow("shoulder.jpg", "shoulder.jpg").Should().BeNull("the same picture is not shown twice");
      }
   }
}
