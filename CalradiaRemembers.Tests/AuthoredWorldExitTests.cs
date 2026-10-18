// Code written by Gabriel Mailhot, 18/10/2026.
// Ways out of a stage that the world takes, not the player: the war ends, someone dies, the player weds her. Gabriel's
// decisions for the Troy-inspired tale (12/10/2026, docs/design/tale-scenario-bridge): the queen's return is the
// condition of peace, and the war stage has no time limit while the war lasts, so its other ways out are "the ends the
// world may bring (the war ends some other way; she or the king dies; the player marries her, which closes the way
// back)". Without them a tale whose war ended another way, or whose queen died, would sit in the journal forever, and
// since only one tale runs at a time, it would block every other tale for the rest of the campaign. If this is wrong, a
// tale ends on a war that never began, misses a death, or takes a world exit the author wrote for another stage.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredWorldExitTests
   {
      private const string Troy = """
      {
        "id": "cr_test_world_exits", "title": "The Queen",
        "requires": { "giver": { "married": true }, "husband": { "role": "spouse_of_giver" } },
        "binds": { "giver": "a lady", "husband": "her husband" },
        "stages": [
          { "id": "ask", "journal": "{giver} asks to speak.", "objective": { "type": "talk_to", "hero": "{giver}" },
            "on_complete": { "next": "war" } },
          { "id": "war", "journal": "{husband}'s realm is at war with you.",
            "choices": [
              { "id": "give_back", "label": "Give her back",
                "said": { "to": "{husband}", "meaning": "the player gives {giver} back" }, "next": "end:returned" },
              { "id": "she_dies", "label": "She dies", "objective": { "type": "dies", "hero": "{giver}" }, "next": "end:mourned" },
              { "id": "wed", "label": "You wed her", "objective": { "type": "weds_player", "hero": "{giver}" }, "next": "end:wed" },
              { "id": "peace", "label": "The war ends", "objective": { "type": "war_ends", "hero": "{husband}" }, "next": "end:peace" } ] }
        ],
        "endings": { "returned": { "memory": "Home." }, "mourned": { "memory": "Gone." }, "wed": { "memory": "Wed." }, "peace": { "memory": "Peace." } }
      }
      """;

      private static AuthoredQuestRun AtWarStage(AuthoredQuest q)
      {
         AuthoredQuestRun run = AuthoredTales.Start(q);
         run.BoundIds["husband"] = "lord_husband";
         run.BoundNames["husband"] = "King Mened";
         AuthoredQuestEngine.Accept(run, q, 100).ToStage.Should().Be("war");

         return run;
      }

      private static Dictionary<string, TaleRoleState> World(TaleRoleState giver = null, TaleRoleState husband = null)
         => new() {["{giver}"] = giver ?? new TaleRoleState(), ["{husband}"] = husband ?? new TaleRoleState()};

      private static readonly TaleRoleState AtWar = new() {AtWarWithPlayer = true};

      // STAKE: an author's tale with world exits loads clean, and the game asks only about the roles those exits name.
      [Test]
      public void GIVEN_a_war_stage_with_world_exits_WHEN_loaded_THEN_it_loads_and_the_game_watches_the_roles_they_name()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);

         AuthoredQuestEngine.RolesWatched(run, q).Should().BeEquivalentTo("{giver}", "{husband}");
      }

      // STAKE: Gabriel's "she or the king dies": the queen dead, the tale ends on the ending its author wrote for it,
      // instead of waiting forever for a return that can no longer happen.
      [Test]
      public void GIVEN_the_war_stage_WHEN_the_queen_is_no_longer_in_the_world_THEN_the_tale_ends_on_its_mourning()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);

         AuthoredStep step = AuthoredQuestEngine.WorldNow(run, q, World(new TaleRoleState {Gone = true}, AtWar), 101);

         step.Kind.Should().Be(AuthoredStepKind.Completed);
         step.EndingId.Should().Be("mourned");
      }

      // STAKE: Gabriel's "the player marries her, which closes the way back": the tale ends on its wedding ending.
      [Test]
      public void GIVEN_the_war_stage_WHEN_the_player_weds_the_queen_THEN_the_tale_ends_on_the_wedding()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);

         AuthoredStep step = AuthoredQuestEngine.WorldNow(run, q, World(new TaleRoleState {WedToPlayer = true}, AtWar), 101);

         step.EndingId.Should().Be("wed");
      }

      // STAKE: Gabriel's "the war ends some other way": a war that was fought and is now over ends the tale on peace.
      [Test]
      public void GIVEN_a_war_seen_in_this_stage_WHEN_the_realms_are_at_peace_THEN_the_tale_ends_on_peace()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);

         AuthoredQuestEngine.WorldNow(run, q, World(husband: AtWar), 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredStep step = AuthoredQuestEngine.WorldNow(run, q, World(), 140);

         step.EndingId.Should().Be("peace");
      }

      // STAKE: the war is declared by the scenario answering the tale, a moment after the stage begins; a stage that
      // read "no war" on its first day must not end on "the war is over" before it was ever fought.
      [Test]
      public void GIVEN_no_war_yet_in_this_stage_WHEN_the_realms_are_at_peace_THEN_the_tale_does_not_end_on_peace()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);

         AuthoredQuestEngine.WorldNow(run, q, World(), 101).Kind.Should().Be(AuthoredStepKind.None);
         run.Status.Should().Be(AuthoredQuestStatus.Active);
      }

      // STAKE: a war seen in an EARLIER stage does not count: the stage's own war is the one whose end it waits for.
      [Test]
      public void GIVEN_a_war_seen_before_the_stage_was_left_WHEN_a_new_stage_begins_THEN_it_starts_with_no_war_seen()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);
         AuthoredQuestEngine.WorldNow(run, q, World(husband: AtWar), 101);

         AuthoredQuestEngine.ForceChoice(run, q, "give_back", 102);

         run.WarsSeen.Should().BeEmpty();
      }

      // STAKE: the file's order decides when two exits hold on the same day, as for every other way out.
      [Test]
      public void GIVEN_two_world_exits_holding_at_once_WHEN_read_THEN_the_first_in_the_file_is_taken()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);

         AuthoredStep step = AuthoredQuestEngine.WorldNow(run, q, World(new TaleRoleState {Gone = true, WedToPlayer = true}, AtWar), 101);

         step.EndingId.Should().Be("mourned");
      }

      // STAKE: a world exit is checked only while its stage is the one under way; a role's death after the tale ended
      // changes nothing, and a role the game could not read is never taken for dead.
      [Test]
      public void GIVEN_a_tale_already_ended_or_an_unread_role_WHEN_the_world_is_read_THEN_nothing_moves()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = AtWarStage(q);

         AuthoredQuestEngine.WorldNow(run, q, new Dictionary<string, TaleRoleState>(), 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ForceChoice(run, q, "give_back", 102);
         AuthoredQuestEngine.WorldNow(run, q, World(new TaleRoleState {Gone = true}), 103).Kind.Should().Be(AuthoredStepKind.None);
         run.EndingId.Should().Be("returned");
      }

      // STAKE: an exit with no one named could never be read; the author is told before the tale is played.
      [Test]
      public void GIVEN_a_world_exit_naming_no_one_WHEN_loaded_THEN_the_author_is_told_it_needs_a_hero()
      {
         string broken = Troy.Replace("{ \"type\": \"dies\", \"hero\": \"{giver}\" }", "{ \"type\": \"dies\" }");

         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(broken, "test.json");

         r.Problems.Should().Contain(p => p.Contains("dies needs \"hero\""));
      }
   }
}
