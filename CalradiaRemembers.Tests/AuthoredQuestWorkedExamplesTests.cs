// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales (docs/design/quest-editor/goal.md, step 5): the two worked examples Gabriel reviews, walked path by
// path through the real loader and the real stage machine. smoke_over_the_hills is his draft rewritten with its three
// outcomes (expose to the lord, confront the rival, keep silent); the_bell_in_the_marsh is a tale in the werewolf's
// spirit whose turn is taken in a scene (left or right on the causeway). If these fail, a drafted path strands the
// player, leads to the wrong ending, or pays what another path should.

#region

using System.Collections.Generic;
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
   public sealed class AuthoredQuestWorkedExamplesTests
   {
      private const string Giver = "lord_giver";
      private const string Rival = "lord_rival";
      private const string Village = "village_ashby";

      private static AuthoredQuest Draft(string file)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string path = Path.Combine(SourceTree.ModRepo!, "docs", "design", "quests", file);
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(File.ReadAllText(path), file);
         r.Problems.Should().BeEmpty();

         return r.Quests.Single();
      }

      private static AuthoredQuestRun Start(AuthoredQuest q)
      {
         AuthoredQuestRun run = AuthoredQuestEngine.Start(q,
            new Dictionary<string, string> {["giver"] = Giver, ["village"] = Village, ["rival"] = Rival},
            new Dictionary<string, string> {["giver"] = "Lord Edric", ["village"] = "Ashby", ["rival"] = "Lord Varn"}, 1);
         AuthoredQuestEngine.Accept(run, q, 1);

         return run;
      }

      /// <summary>Smoke, played as the game would up to the decision: two survivors heard, the barns reached, the scene closed.</summary>
      private static AuthoredQuestRun SmokeAtTheDecision(AuthoredQuest q, bool sawTheTracks = false)
      {
         AuthoredQuestRun run = Start(q);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 2);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_b", Village, 2).ToStage.Should().Be("the_burned_barn");
         AuthoredQuestEngine.Arrived(run, q, Village, 3).ToStage.Should().Be("among_the_ashes");
         if (sawTheTracks) AuthoredQuestEngine.Found(run, q, "hoofprints").Should().BeTrue();
         AuthoredQuestEngine.SceneClosed(run, q, 3, out AuthoredStep closed).Select(f => f.Id).Should().Equal("surcoat");
         closed.ToStage.Should().Be("what_to_do");

         return run;
      }

      // Every draft in docs/design/quests loads with nothing to report, except the one that asks for an objective the
      // game cannot check yet (the_lost_banner's deliver_item), which is refused as it always was.
      [Test]
      public void GIVEN_the_drafted_tales_WHEN_loaded_THEN_the_branching_ones_load_clean_and_the_lost_banner_is_refused_as_before()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string dir = Path.Combine(SourceTree.ModRepo!, "docs", "design", "quests");

         AuthoredQuestLoadResult r = AuthoredQuestLoader.ParseAll(Directory.GetFiles(dir, "*.json").Select(f => (Path.GetFileName(f), File.ReadAllText(f))));

         r.Quests.Select(q => q.Id).Should().BeEquivalentTo("cr_smoke_over_the_hills", "cr_the_bell_in_the_marsh", "cr_the_bitter_well", "cr_wolf_of_the_wood");
         r.Problems.Should().ContainSingle().Which.Should().Contain("the_lost_banner.json").And.Contain("deliver_item");
      }

      // Smoke, expose: the lord is shown the surcoat; the realm hears it; the rival remembers who exposed him.
      [Test]
      public void GIVEN_smoke_WHEN_the_player_lays_the_surcoat_before_the_lord_THEN_the_tale_ends_exposed_and_the_realm_hears_it()
      {
         AuthoredQuest q = Draft("smoke_over_the_hills.json");
         AuthoredQuestRun run = SmokeAtTheDecision(q);

         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 5).EndingId.Should().Be("exposed");

         AuthoredOutcome end = AuthoredQuestEngine.Outcome(run, q);
         end.Regard.Should().Be(8);
         AuthoredQuestEngine.Consequences(run, end).Select(c => c.Type).Should().Equal("world_event", "memory", "memory");
         AuthoredQuestPrompt.ForCharacter(run, q, Giver, null, offering: false).Should().Contain("surcoat in Lord Varn's colours");
      }

      // Smoke, confront: the rival is shown the proof and the story waits on what the player does with it; going to the
      // lord after all still exposes him, and the rival remembers the player came to him first.
      [Test]
      public void GIVEN_smoke_WHEN_the_player_confronts_the_rival_then_tells_the_lord_THEN_it_ends_exposed_remembering_the_confrontation()
      {
         AuthoredQuest q = Draft("smoke_over_the_hills.json");
         AuthoredQuestRun run = SmokeAtTheDecision(q);

         AuthoredQuestEngine.ConversationOpened(run, q, Rival, null, 5).ToStage.Should().Be("the_rival_knows");
         AuthoredQuestPrompt.ForCharacter(run, q, Rival, null, offering: false).Should().Contain("neither deny it nor confess it");
         AuthoredQuestEngine.ConversationClosed(run, Rival);
         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 6).EndingId.Should().Be("exposed");

         AuthoredQuestEngine.Consequences(run, AuthoredQuestEngine.Outcome(run, q)).Should().Contain(c => c.Text != null && c.Text.Contains("came to me first"));
      }

      // Smoke, confront then a price: words said to the rival end the tale bought, paid by the game, never by the model.
      [Test]
      public void GIVEN_smoke_WHEN_the_player_agrees_with_the_rival_to_keep_silent_for_a_price_THEN_it_ends_bought()
      {
         AuthoredQuest q = Draft("smoke_over_the_hills.json");
         AuthoredQuestRun run = SmokeAtTheDecision(q);
         AuthoredQuestEngine.ConversationOpened(run, q, Rival, null, 5);

         AuthoredQuestEngine.SaidChoicesFor(run, q, Rival).Select(c => c.Id).Should().Equal("his_price");
         AuthoredQuestEngine.Said(run, q, Rival, "his_price", 5).EndingId.Should().Be("bought");
         AuthoredQuestEngine.Outcome(run, q).Gold.Should().Be(900);
      }

      // Smoke, confront then nothing: a week later the rival is left to carry it, and the lord never had his proof.
      [Test]
      public void GIVEN_smoke_WHEN_the_player_confronts_the_rival_and_lets_a_week_pass_THEN_it_ends_confronted()
      {
         AuthoredQuest q = Draft("smoke_over_the_hills.json");
         AuthoredQuestRun run = SmokeAtTheDecision(q);
         AuthoredQuestEngine.ConversationOpened(run, q, Rival, null, 5);

         AuthoredQuestEngine.Day(run, q, 11).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.Day(run, q, 12).EndingId.Should().Be("confronted");
      }

      // Smoke, keep silent: ten days with nothing done is the silence ending; the war goes on against the wrong enemy.
      [Test]
      public void GIVEN_smoke_WHEN_the_player_does_nothing_with_the_truth_for_ten_days_THEN_it_ends_in_silence()
      {
         AuthoredQuest q = Draft("smoke_over_the_hills.json");
         AuthoredQuestRun run = SmokeAtTheDecision(q);

         AuthoredQuestEngine.Day(run, q, 13).EndingId.Should().Be("silence");
         AuthoredQuestEngine.Outcome(run, q).Regard.Should().Be(-3);
      }

      // Smoke: the tracks behind the barns, if seen, reach the lord when he is shown the surcoat; if not, they do not.
      [Test]
      public void GIVEN_smoke_WHEN_the_tracks_were_seen_or_not_THEN_the_lord_hears_of_them_only_when_they_were()
      {
         AuthoredQuest q = Draft("smoke_over_the_hills.json");
         AuthoredQuestRun seen = SmokeAtTheDecision(q, sawTheTracks: true);
         AuthoredQuestRun unseen = SmokeAtTheDecision(q);
         AuthoredQuestEngine.ConversationOpened(seen, q, Giver, null, 5);
         AuthoredQuestEngine.ConversationOpened(unseen, q, Giver, null, 5);

         AuthoredQuestPrompt.ForCharacter(seen, q, Giver, null, offering: false).Should().Contain("hoofprints");
         AuthoredQuestPrompt.ForCharacter(unseen, q, Giver, null, offering: false).Should().NotContain("hoofprints");
      }

      /// <summary>The bell, played as the game would up to the fork on the causeway: two villagers, nights of watch.</summary>
      private static AuthoredQuestRun BellAtTheFork(AuthoredQuest q)
      {
         AuthoredQuestRun run = Start(q);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 2);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_b", Village, 2).ToStage.Should().Be("keep_watch");
         WatchUntilTheScene(run, q, 3);

         return run;
      }

      private static void WatchUntilTheScene(AuthoredQuestRun run, AuthoredQuest q, int fromDay)
      {
         AuthoredStep night = AuthoredStep.Nothing;
         for (int day = fromDay; day < fromDay + 5 && night.Kind != AuthoredStepKind.OpenScene; day++) night = AuthoredQuestEngine.NightPassed(run, q, true, day);
         night.Kind.Should().Be(AuthoredStepKind.OpenScene);
         run.StageId.Should().Be("the_causeway");
      }

      // The bell, left: toward the bell, the chapel's crates and Oswin bound in a corner; the lord hears the whole truth.
      [Test]
      public void GIVEN_the_bell_WHEN_the_player_turns_left_toward_the_bell_THEN_they_unmask_the_smugglers_and_bring_oswin_home()
      {
         AuthoredQuest q = Draft("the_bell_in_the_marsh.json");
         AuthoredQuestRun run = BellAtTheFork(q);

         AuthoredQuestEngine.ChooseInScene(run, q, "left").Should().BeTrue();
         AuthoredQuestEngine.SceneClosed(run, q, 6, out _);
         run.StageId.Should().Be("the_chapel");
         AuthoredQuestEngine.Meet(run, q, "oswin").Should().BeTrue();
         AuthoredQuestEngine.SceneClosed(run, q, 6, out AuthoredStep closed);
         closed.ToStage.Should().Be("tell_the_lord");

         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 7).EndingId.Should().Be("unmasked");
         AuthoredQuestEngine.Consequences(run, AuthoredQuestEngine.Outcome(run, q)).Should().Contain(c => c.Text != null && c.Text.Contains("Oswin came home"));
      }

      // The bell, right: open only once the boot was seen; it leads to Tam in the reeds, and the lord hears only his word.
      [Test]
      public void GIVEN_the_bell_WHEN_the_player_saw_the_boot_and_turns_right_THEN_they_find_tam_and_the_tale_ends_with_the_survivor()
      {
         AuthoredQuest q = Draft("the_bell_in_the_marsh.json");
         AuthoredQuestRun run = BellAtTheFork(q);

         AuthoredQuestEngine.ChooseInScene(run, q, "right").Should().BeFalse();
         AuthoredQuestEngine.Found(run, q, "boot").Should().BeTrue();
         AuthoredQuestEngine.ChooseInScene(run, q, "right").Should().BeTrue();
         AuthoredQuestEngine.SceneClosed(run, q, 6, out _);
         run.StageId.Should().Be("the_reeds");
         AuthoredQuestEngine.SceneClosed(run, q, 6, out _);

         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 7).EndingId.Should().Be("survivor");
         run.Flags.Should().Contain("found_tam").And.NotContain("smugglers");
      }

      // The bell, back: walking away with no choice made returns to the watch (a loop with a way out); a later night the
      // player can still take the left path.
      [Test]
      public void GIVEN_the_bell_WHEN_the_player_walks_away_from_the_fork_THEN_they_wait_for_another_night_and_can_come_back()
      {
         AuthoredQuest q = Draft("the_bell_in_the_marsh.json");
         AuthoredQuestRun run = BellAtTheFork(q);

         AuthoredQuestEngine.SceneClosed(run, q, 6, out AuthoredStep back);
         back.ToStage.Should().Be("keep_watch");
         WatchUntilTheScene(run, q, 7);
         AuthoredQuestEngine.ChooseInScene(run, q, "left").Should().BeTrue();
      }
   }
}
