// Code written by Gabriel Mailhot, 15/10/2026.
// A stage's on_enter: consequences carried out when a tale enters a stage, not only at an ending. tashmetu (15/10/2026)
// read the engine right: consequences ran only at an ending, so in one tale a lady could never leave midway and be given
// back later, and "return_spouse" in another ending of the same tale found her still married. The Troy-inspired design
// (Gabriel, 12/10/2026) needed exactly that: she leaves, a war follows, and her return is the price of peace. If this is
// wrong, an elopement the player chose never happens (or happens on a path the player did not take), the request's
// stage carries something out before the tale is even accepted, or a tale that cannot work loads anyway.

#region

using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredStageOnEnterTests
   {
      private const string Troy = """
      {
        "id": "cr_test_troy", "title": "The Queen",
        "requires": { "giver": { "married": true }, "husband": { "role": "spouse_of_giver" } },
        "binds": { "giver": "a lady", "husband": "her husband", "village": "a village" },
        "stages": [
          { "id": "ask", "journal": "{giver} asks to speak in the garden.", "objective": { "type": "talk_to", "hero": "{giver}" },
            "on_complete": { "next": "garden" } },
          { "id": "garden", "journal": "{giver} waits for an answer.",
            "choices": [
              { "id": "take_her", "label": "Leave with her", "sets": "with_her",
                "said": { "to": "{giver}", "meaning": "the player agrees to take {giver} away" }, "next": "war" },
              { "id": "honour", "label": "Honour the host",
                "said": { "to": "{giver}", "meaning": "the player refuses" }, "next": "war" } ] },
          { "id": "war", "journal": "{husband}'s realm goes to war.",
            "on_enter": [
              { "type": "elope", "if": "with_her" },
              { "type": "memory", "who": "{husband}", "text": "{giver} is gone." , "if": "with_her" },
              { "type": "memory", "who": "{giver}", "text": "The garden, and the answer." } ],
            "choices": [
              { "id": "give_back", "label": "Give her back",
                "said": { "to": "{husband}", "meaning": "the player gives {giver} back to {husband}" }, "next": "end:returned" } ] }
        ],
        "endings": { "returned": { "consequences": [ { "type": "return_spouse" } ] } }
      }
      """;

      private static AuthoredQuestRun Started(AuthoredQuest q)
      {
         AuthoredQuestRun run = AuthoredTales.Start(q);
         run.BoundIds["husband"] = "lord_husband";
         run.BoundNames["husband"] = "King Mened";
         AuthoredQuestEngine.Accept(run, q, 100).ToStage.Should().Be("garden");

         return run;
      }

      // STAKE: tashmetu's Gilded Cage and the Troy tale load: an elopement on a stage, a return at an ending, nothing
      // reported, so an author is not told a sound tale is broken.
      [Test]
      public void GIVEN_an_elopement_on_a_stage_and_a_return_at_an_ending_WHEN_loaded_THEN_the_tale_loads_clean()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);

         q.Stages.Single(s => s.Id == "war").OnEnter.Should().HaveCount(3);
      }

      // STAKE: the player chose to leave with her: entering the war stage carries out the elopement and her husband's
      // memory, under the flag the choice that led there just set.
      [Test]
      public void GIVEN_the_player_chose_to_take_her_WHEN_the_tale_enters_the_next_stage_THEN_its_on_enter_includes_the_elopement()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = Started(q);

         AuthoredStep step = AuthoredQuestEngine.ForceChoice(run, q, "take_her", 101);

         step.Kind.Should().Be(AuthoredStepKind.Advanced);
         AuthoredQuestEngine.OnEnter(run, q).Select(c => c.Type).Should().Equal("elope", "memory", "memory");
      }

      // STAKE: the player honoured the host: the same stage is entered, but no elopement under the flag the player never
      // earned; only what holds on every path is carried out.
      [Test]
      public void GIVEN_the_player_honoured_the_host_WHEN_the_tale_enters_the_same_stage_THEN_the_elopement_is_not_carried_out()
      {
         AuthoredQuest q = AuthoredTales.Load(Troy);
         AuthoredQuestRun run = Started(q);

         AuthoredQuestEngine.ForceChoice(run, q, "honour", 101);

         AuthoredQuestEngine.OnEnter(run, q).Select(c => c.Text).Should().Equal("The garden, and the answer.");
      }

      // STAKE: the first stage is the request, already behind the player once they accept. Its on_enter would run before
      // anyone said yes, so it never runs, and the author is told.
      [Test]
      public void GIVEN_an_on_enter_on_the_first_stage_WHEN_loaded_and_played_THEN_it_is_never_carried_out_and_the_author_is_told()
      {
         string json = Troy.Replace("""
          { "id": "ask", "journal": "{giver} asks to speak in the garden.",
          """, """
          { "id": "ask", "journal": "{giver} asks to speak in the garden.", "on_enter": [ { "type": "memory", "who": "{giver}", "text": "asked" } ],
          """);
         AuthoredQuestLoadResult r = AuthoredTales.Read(json);
         r.Problems.Should().Contain(p => p.Contains("stage 'ask' is the request") && p.Contains("on_enter"));

         AuthoredQuest q = r.Quests.Single();
         AuthoredQuestEngine.OnEnter(AuthoredTales.Start(q), q).Should().BeEmpty();
      }

      // STAKE: an elopement on a stage with no husband bound would end nobody's marriage while the tale says she left; the
      // tale is refused, as the same elopement at an ending is.
      [Test]
      public void GIVEN_an_elopement_on_a_stage_with_no_husband_bound_WHEN_loaded_THEN_the_tale_is_refused()
      {
         string json = Troy.Replace(@", ""husband"": { ""role"": ""spouse_of_giver"" }", "")
                           .Replace(@"""husband"": ""her husband"", ", "")
                           .Replace("{husband}", "{giver}")
                           .Replace(@"{ ""type"": ""return_spouse"" }", @"{ ""type"": ""memory"", ""who"": ""{giver}"", ""text"": ""home"" }");
         json.Should().NotContain("husband");

         AuthoredQuestLoadResult r = AuthoredTales.Read(json);

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("elope") && p.Contains("husband"));
      }

      // STAKE: a flag tested only by an on_enter is still a flag tested; a tale that sets it is not warned of a flag
      // "tested but never set", and one that never sets it is.
      [Test]
      public void GIVEN_an_on_enter_testing_a_flag_no_choice_sets_WHEN_loaded_THEN_the_author_is_told_it_never_holds()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Troy.Replace("""
         "sets": "with_her",
         """.Trim(), ""));

         r.Problems.Should().Contain(p => p.Contains("flag 'with_her' is tested but never set"));
      }
   }
}
