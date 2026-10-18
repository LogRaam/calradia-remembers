// Code written by Gabriel Mailhot, 07/10/2026.
// fkasad's walk through the Wolf of the Wood on 2.7.3 (Nexus, 07/10/2026), three things ratified by Gabriel:
// - "I almost clicked on End Conversation instead of Send at least four times during the forest part ... it could be a
//   catastrophe during a quest": closing a scene ends it and moves the tale on, with no way back;
// - the tale's lord also handed him a hideout task, and the journal showed two targets at once;
// - "the companions participating ... should have some memory of it. They talked, gave their opinion, advices".
// If these break, one stray click throws away an hour of a tale, a tale's giver doubles its errand, or a companion who
// tracked the slavers through the wood with the player has no idea it happened.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleUnderWayTests
   {
      // A scene still going asks before it is left; one that has ended by itself, or any ordinary chat, closes at once.
      [Test]
      public void GIVEN_a_scene_still_going_WHEN_the_player_clicks_to_end_it_THEN_they_are_asked_first()
      {
         SceneLeavePolicy.AsksFirst(isScene: true, sceneOver: false).Should().BeTrue();
         SceneLeavePolicy.AsksFirst(isScene: true, sceneOver: true).Should().BeFalse();
         SceneLeavePolicy.AsksFirst(isScene: false, sceneOver: false).Should().BeFalse();
      }

      // The tale's giver, and its rival, hold a role in the tale under way; anyone else does not, nor anyone once it ended.
      [Test]
      public void GIVEN_a_tale_under_way_WHEN_asked_who_holds_a_role_THEN_its_bound_characters_do_and_only_while_it_runs()
      {
         AuthoredQuestRun run = AuthoredTales.Start(AuthoredTales.Load());
         run.BoundIds["rival"] = "lord_rival";

         AuthoredTaleRoles.HoldsARole(run, AuthoredTales.Giver).Should().BeTrue();
         AuthoredTaleRoles.HoldsARole(run, "lord_rival").Should().BeTrue();
         AuthoredTaleRoles.HoldsARole(run, "lord_elsewhere").Should().BeFalse();
         AuthoredTaleRoles.HoldsARole(run, AuthoredTales.Village).Should().BeFalse("a village holds no errand to give");

         run.Status = AuthoredQuestStatus.Completed;
         AuthoredTaleRoles.HoldsARole(run, AuthoredTales.Giver).Should().BeFalse();
      }

      // STAKE: fkasad (Nexus, 18/10/2026): the Drowned Bridge's lord, raising the tale on the road, also set the player a
      // second errand about the same bridge at another village (the model wrote it as a deed the game refused, the purple
      // line). The lord raising a tale holds its errand already; no one else does on that account.
      [Test]
      public void GIVEN_a_lord_raising_a_tale_before_it_is_accepted_WHEN_asked_who_holds_a_role_THEN_he_does_and_no_one_else()
      {
         AuthoredTaleRoles.HoldsARole(null, "lord_raising", "lord_raising").Should().BeTrue();
         AuthoredTaleRoles.HoldsARole(null, "lord_elsewhere", "lord_raising").Should().BeFalse();
         AuthoredTaleRoles.HoldsARole(null, "lord_raising", null).Should().BeFalse();
      }

      // A companion who walked the scene remembers walking it with the player, and what was found there.
      [Test]
      public void GIVEN_a_companion_in_the_scene_WHEN_it_closes_THEN_they_remember_the_search_and_its_findings()
      {
         string memory = SceneCompanionMemory.Compose("The wood by Ortongard", "Can",
            new List<string> {"A wolf pelt stretched on a frame.", "Chains in the bracken."});

         memory.Should().Contain("The wood by Ortongard").And.Contain("Can").And.Contain("wolf pelt").And.Contain("Chains");
      }

      // A memory stays within what a memory may hold, however much was found.
      [Test]
      public void GIVEN_many_findings_WHEN_the_memory_is_written_THEN_it_stays_within_bounds()
      {
         var found = new List<string>();
         for (int i = 0; i < 30; i++) found.Add($"A long description of something found in the wood, number {i}.");

         SceneCompanionMemory.Compose("The wood", "Can", found).Length.Should().BeLessThanOrEqualTo(SceneCompanionMemory.MaxChars);
      }
   }
}
