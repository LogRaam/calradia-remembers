// Code written by Gabriel Mailhot, 11/10/2026.
// "Beneath the Falls", the Khuzait wife's tale, as it ships in the Adult Tales module (Gabriel, 10/10/2026: the
// player finds his wife bathing, she joins him, he admires her in states he may pass between and back, she lies down
// and invites him; the ending strengthens their bond and may bring a child). If these tests fail, the file no longer
// loads, a state leads nowhere or cannot be left, a picture is missing, or the ending forgets her memory or the child.

#region

using System.IO;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WifeTaleKhuzaitTests
   {
      private static string Folder => Path.Combine(SourceTree.ModRepo!, "Extras", "CalradiaRemembersAdultTales", "ModuleData", "cr_quests", "wife_khuzait_falls");

      private static AuthoredQuest Tale() => AuthoredTales.Load(File.ReadAllText(Path.Combine(Folder, "wife_khuzait_falls.json")));

      private static readonly string[] States = {"shoulder", "breast", "hips", "belly", "legs"};

      // Gabriel's conditions: his Khuzait wife, with him, not expecting, on good terms, explicit content, on the map.
      [Test]
      public void GIVEN_the_shipped_tale_WHEN_read_THEN_it_asks_for_his_khuzait_wife_with_him_on_the_map()
      {
         AuthoredQuest q = Tale();
         GiverRequirement g = q.Requires.Giver;

         (g.Role, g.Gender, g.WithPlayer, g.NotPregnant, g.MinRegard).Should().Be(("spouse", "female", true, true, 10));
         g.Culture.Should().Equal("khuzait");
         q.Requires.Content.Should().Be("explicit");
         q.OnMap.Should().NotBeNull();
         (q.OnMap.StillHours, q.OnMap.ByDay, q.OnMap.InGiversLands, q.OnMap.ChancePerDay).Should().Be((3, true, true, 0.15));
         q.Unique.Should().BeTrue("once per nation (Gabriel)");
      }

      // Every stage names a picture, and every picture is in the folder.
      [Test]
      public void GIVEN_the_shipped_tale_WHEN_its_pictures_are_checked_THEN_each_stage_after_the_invitation_has_one_and_it_is_there()
      {
         AuthoredQuest q = Tale();

         foreach (AuthoredStage s in q.Stages.Skip(1))
         {
            s.Image.Should().NotBeNullOrWhiteSpace(s.Id);
            File.Exists(Path.Combine(Folder, s.Image)).Should().BeTrue(s.Image);
         }
      }

      // The states: from together, each one; from each one, every other and the invitation (Gabriel: "il la retourne et
      // detache sa robe (3), etc.").
      [Test]
      public void GIVEN_the_states_WHEN_their_ways_are_read_THEN_each_leads_to_every_other_and_to_the_invitation()
      {
         AuthoredQuest q = Tale();

         foreach (string state in States.Append("together"))
         {
            AuthoredStage s = q.Stages.Single(x => x.Id == state);
            string[] expected = States.Where(x => x != state).Append("invitation").ToArray();
            s.Choices.Select(c => c.Next).Should().BeEquivalentTo(expected, state);
            s.Choices.Should().OnlyContain(c => c.Said != null && c.Said.To == "{giver}", "every way is taken by words with her");
         }
      }

      // A walk through it: found, together, a state, another, back to the first, the invitation, the ending.
      [Test]
      public void GIVEN_a_walk_through_the_tale_WHEN_the_words_move_it_THEN_she_speaks_from_each_state_and_it_ends_rewarded()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = AuthoredTales.Start(q);
         string Her() => AuthoredQuestPrompt.ForCharacter(run, q, AuthoredTales.Giver, null, offering: false) ?? "";

         AuthoredQuestEngine.Accept(run, q, 100);
         run.StageId.Should().Be("found");
         Her().Should().Contain("beneath the falls");

         foreach ((string choice, string stage, string says) in new[] {
                     ("joins", "together", "soaked and smiling"), ("hips", "hips", "pressed your back"),
                     ("breast", "breast", "breasts"), ("hips", "hips", "pressed your back"), ("invitation", "invitation", "lain back")})
         {
            AuthoredQuestEngine.Said(run, q, AuthoredTales.Giver, choice, 100);
            run.StageId.Should().Be(stage);
            Her().Should().Contain(says, stage);
         }

         AuthoredQuestEngine.Said(run, q, AuthoredTales.Giver, "together", 100);
         run.Status.Should().Be(AuthoredQuestStatus.Completed);
      }

      // The ending: her memory, her regard, and a chance of a child (the game's own 15%).
      [Test]
      public void GIVEN_the_ending_WHEN_read_THEN_it_leaves_her_a_memory_regard_and_a_chance_of_a_child()
      {
         AuthoredOutcome reward = Tale().Reward;

         reward.Memory.Should().Contain("falls");
         reward.Regard.Should().BePositive();
         reward.Consequences.Should().ContainSingle(c => c.Type == "conception" && c.Who == "{giver}" && c.Chance == 0.15);
      }
   }
}
